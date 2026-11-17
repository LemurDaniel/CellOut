// NOTE:
//   Unit tests for the cell stages: the part catalog (Cell/PartCatalog.cs), the traits
//   of a design (Cell/CellStats.cs), the diet bar (Cell/DietTracker.cs) and the
//   mutations of the prelude (Prelude/Mutation.cs), the NPC colonies (Cell/ColonyRule.cs)
//   and the body forms of the NPC cells (Cell/CellForms.cs).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter CellTests

//
//   Chapters:
//     UNLOCK PARTS     Catalog entries and the diet lock.
//     COMPUTE TRAITS   Part effects and the directional combat values.
//     TRACK DIET       Meals, fading and the mix with the part lean.
//     ROLL MUTATIONS   The table of the prelude.
//     FIRE ABILITIES   Triggers, key slots, cooldowns, running effects and shields (Cell/Abilities/AbilitySet.cs).
//     SIZE COLONIES    Cell count and burst size of the NPC colonies.
//     FORM CELLS       Body forms of the NPC cells (Cell/CellForms.cs).
//

using System.Linq;
using Godot;
using Xunit;

namespace CellOut.Tests;

public class CellTests
{
    private const int PRECISION = 4;

    // ===========================================================================
    // ============================   UNLOCK PARTS   =============================
    // ===========================================================================
    //
    //   CatalogListsEveryKind      One entry per PartKind, in palette order.
    //   JawNeedsCarnivoreDiet      Locked at 0.1, open at 0.3.
    //   PlateNeedsHerbivoreDiet    Locked at 0, open at -0.3.
    //   CiliaNeedTheMiddle         Locked at 0.6 on both sides.
    //   ProtocellGetsNoParts       Stage 0 unlocks nothing.
    //

    [Fact]
    public void CatalogListsEveryKind()
    {
        PartKind[] listed = PartCatalog.All().Select(part => part.Kind).ToArray();

        Assert.Equal(System.Enum.GetValues<PartKind>(), listed);
        Assert.Equal(-1f, PartCatalog.Get(PartKind.LightSail).Lean);
        Assert.Equal(1f, PartCatalog.Get(PartKind.Electrocyte).Lean);
        Assert.All(PartCatalog.All(), part => Assert.Equal(Placement.Rim, part.Placement));      // every part sits visibly outside
        Assert.Equal("PART_FILTERMOUTH_NAME", PartCatalog.Get(PartKind.FilterMouth).NameKey);
    }

    [Fact]
    public void JawNeedsCarnivoreDiet()
    {
        PartInfo jaw = PartCatalog.Get(PartKind.Jaw);

        Assert.True(jaw.HasDietLock);
        Assert.False(PartCatalog.IsUnlocked(jaw, stage: 1, diet: 0.1f));
        Assert.True(PartCatalog.IsUnlocked(jaw, stage: 1, diet: 0.3f));
    }

    [Fact]
    public void PlateNeedsHerbivoreDiet()
    {
        PartInfo plate = PartCatalog.Get(PartKind.ArmorPlate);

        Assert.False(PartCatalog.IsUnlocked(plate, stage: 1, diet: 0f));
        Assert.True(PartCatalog.IsUnlocked(plate, stage: 1, diet: -0.3f));
    }

    [Fact]
    public void CiliaNeedTheMiddle()
    {
        PartInfo cilia = PartCatalog.Get(PartKind.Cilia);

        Assert.True(PartCatalog.IsUnlocked(cilia, stage: 1, diet: 0f));
        Assert.False(PartCatalog.IsUnlocked(cilia, stage: 1, diet: 0.6f));
        Assert.False(PartCatalog.IsUnlocked(cilia, stage: 1, diet: -0.6f));
    }

    [Fact]
    public void ProtocellGetsNoParts()
    {
        PartInfo spike = PartCatalog.Get(PartKind.Spike);

        Assert.False(spike.HasDietLock);
        Assert.False(PartCatalog.IsUnlocked(spike, stage: 0, diet: 0f));
    }

    // ===========================================================================
    // ===========================   COMPUTE TRAITS   ============================
    // ===========================================================================
    //
    //   BareCellHasBaseTraits            No parts: speed 1, no light, no pull, no thorns.
    //   FlagellumPushesBestFromBack      +22% at the back, +6.6% at the front.
    //   SpikeHurtsOnlyTowardsItsSide     8 inside its arc, 0 outside, toxin multiplies.
    //   JawBitesTowardsItsSide           Inside the jaw arc only.
    //   ShellHalvesDamageFromItsSide     0.5 towards a creature shell, 1 from behind.
    //   ArcWrapsAroundTheBack            A part at 3 rad covers -3 rad.
    //   SailsAndMouthsAddUp              Two sails 5 light, a filter mouth 2.5 pull range and pull 4.
    //   PlateShieldsItsSide              A cell plate covers its arc only.
    //   NematocystStingsTowardsItsSide   5 inside its arc, 0 outside, no contact damage.
    //   SuckerHoldsTowardsItsSide        Inside the sucker arc only.
    //   PartSizeIsClampedAndLooksOnly    1 at first, clamped to 0.5..2, the speed stays.
    //

    [Fact]
    public void BareCellHasBaseTraits()
    {
        CellStats stats = CellStats.From(new CellDesign());

        Assert.Equal(1f, stats.Speed);
        Assert.Equal(0f, stats.Photosynthesis);
        Assert.Equal(0f, stats.MagnetBonus);
        Assert.False(stats.HasThorns);
    }

    [Fact]
    public void FlagellumPushesBestFromBack()
    {
        var back  = new CellDesign();
        var front = new CellDesign();
        Assert.True(back.TryPlace(PartKind.Flagellum, Vector2.Left, paidCost: 0));
        Assert.True(front.TryPlace(PartKind.Flagellum, Vector2.Right, paidCost: 0));

        Assert.Equal(1.22f, CellStats.From(back).Speed, PRECISION);
        Assert.Equal(1.066f, CellStats.From(front).Speed, PRECISION);
    }

    [Fact]
    public void SpikeHurtsOnlyTowardsItsSide()
    {
        float[] spikes = { 0f };
        float[] none   = System.Array.Empty<float>();

        Assert.Equal(8f, CellStats.ContactDamage(spikes, none, toxin: 1f, localAngle: 0.5f), PRECISION);
        Assert.Equal(0f, CellStats.ContactDamage(spikes, none, toxin: 1f, localAngle: 1f), PRECISION);
        Assert.Equal(28.8f, CellStats.ContactDamage(spikes, jaws: spikes, toxin: 1.6f, localAngle: 0f), PRECISION);
    }

    [Fact]
    public void JawBitesTowardsItsSide()
    {
        float[] jaws = { 0f };

        Assert.True(CellStats.CanBite(jaws, 0.75f));
        Assert.False(CellStats.CanBite(jaws, 0.85f));
        Assert.False(CellStats.CanBite(System.Array.Empty<float>(), 0f));
    }

    [Fact]
    public void ShellHalvesDamageFromItsSide()
    {
        float[] plates = { 0f };

        Assert.Equal(0.5f, CellStats.DamageFactor(plates, 0.5f));
        Assert.Equal(1f, CellStats.DamageFactor(plates, Mathf.Pi));
    }

    [Fact]
    public void ArcWrapsAroundTheBack()
    {
        Assert.True(CellStats.IsWithin(localAngle: -3f, partAngle: 3f, arc: 0.7f));
        Assert.False(CellStats.IsWithin(localAngle: 0f, partAngle: 3f, arc: 0.7f));
    }

    /// <summary>A design with the given parts on the rim at their direction.</summary>
    private static CellDesign DesignWith(params (PartKind Kind, Vector2 Point)[] parts)
    {
        var design = new CellDesign();
        foreach ((PartKind kind, Vector2 point) in parts)
        {
            Assert.True(design.TryPlace(kind, point, paidCost: 0));
        }
        return design;
    }

    [Fact]
    public void SailsAndMouthsAddUp()
    {
        CellStats stats = CellStats.From(DesignWith((PartKind.LightSail, Vector2.Up), (PartKind.LightSail, Vector2.Down), (PartKind.FilterMouth, Vector2.Right)));

        Assert.Equal(5f, stats.Photosynthesis, PRECISION);
        Assert.Equal(2.5f, stats.MagnetBonus, PRECISION);
        Assert.Equal(4f, stats.VortexPull, PRECISION);
        Assert.Equal(1f, stats.Speed);
    }

    [Fact]
    public void PlateShieldsItsSide()
    {
        CellStats stats = CellStats.From(DesignWith((PartKind.ArmorPlate, Vector2.Right)));

        Assert.True(stats.IsPlatedToward(0.8f));
        Assert.False(stats.IsPlatedToward(Mathf.Pi));
        Assert.False(stats.HasThorns);
    }

    [Fact]
    public void NematocystStingsTowardsItsSide()
    {
        CellStats plain = CellStats.From(DesignWith((PartKind.Nematocyst, Vector2.Right)));

        Assert.Equal(5f, plain.StingToward(0.4f), PRECISION);
        Assert.Equal(0f, plain.StingToward(0.6f), PRECISION);
        Assert.Equal(0f, plain.ContactDamageToward(0f));
    }

    [Fact]
    public void SuckerHoldsTowardsItsSide()
    {
        CellStats stats = CellStats.From(DesignWith((PartKind.Sucker, Vector2.Right)));

        Assert.True(stats.CanSuckToward(0.5f));
        Assert.False(stats.CanSuckToward(0.7f));
        Assert.False(CellStats.From(new CellDesign()).CanSuckToward(0f));
    }

    [Fact]
    public void PartSizeIsClampedAndLooksOnly()
    {
        var design = new CellDesign();
        Assert.True(design.TryPlace(PartKind.Flagellum, Vector2.Left, paidCost: 0));
        float speed = CellStats.From(design).Speed;
        Assert.Equal(1f, design.Parts[0].Size);

        design.SetPartSize(0, 1.3f);
        Assert.Equal(1.3f, design.Parts[0].Size, PRECISION);
        design.SetPartSize(0, 5f);
        Assert.Equal(CellDesign.MAX_PART_SIZE, design.Parts[0].Size);
        design.SetPartSize(0, 0f);
        Assert.Equal(CellDesign.MIN_PART_SIZE, design.Parts[0].Size);

        Assert.Equal(speed, CellStats.From(design).Speed);
    }

    // ===========================================================================
    // ===========================   FIRE ABILITIES   ============================
    // ===========================================================================
    //
    //   EveryPartHasATrigger       Five on keys, five by themselves, five all the time, the eyespot only looks.
    //   SlotsFollowPlacement       Key kinds in placement order, each once, at most four.
    //   CooldownRunsDown           Fired: not ready, share 1; after the cooldown ready again.
    //   MorePartsRechargeFaster    Two flagella recharge in 2 s instead of 3.
    //   EffectRunsForItsDuration   The cloak runs 4 s, then stops.
    //   MissingPartNeverFires      No part: not ready, TryUse false, no slots, no shield.
    //   PlatesBlockAndGrowBack     Two plates block two hits, then one grows back every 4 s.
    //

    [Fact]
    public void EveryPartHasATrigger()
    {
        PartKind[] kinds = System.Enum.GetValues<PartKind>();

        Assert.Equal(5, kinds.Count(kind => AbilityTrigger.Active == AbilitySet.TriggerOf(kind)));
        Assert.Equal(5, kinds.Count(kind => AbilityTrigger.Auto == AbilitySet.TriggerOf(kind)));
        Assert.Equal(5, kinds.Count(kind => AbilityTrigger.Passive == AbilitySet.TriggerOf(kind)));
        Assert.Equal(AbilityTrigger.None, AbilitySet.TriggerOf(PartKind.Eyespot));
    }

    [Fact]
    public void SlotsFollowPlacement()
    {
        var points = new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right, new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f) };
        var kinds  = new[] { PartKind.Chromatophore, PartKind.Spike, PartKind.Flagellum, PartKind.Chromatophore, PartKind.JetNozzle, PartKind.Cilia, PartKind.SlimeGland };
        AbilitySet set = AbilitySet.From(DesignWith(kinds.Zip(points).ToArray()));

        Assert.Equal(new[] { PartKind.Chromatophore, PartKind.Flagellum, PartKind.JetNozzle, PartKind.Cilia }, set.Slots);
        Assert.Equal(2, set.Count(PartKind.Chromatophore));
    }

    [Fact]
    public void CooldownRunsDown()
    {
        AbilitySet set = AbilitySet.From(DesignWith((PartKind.Flagellum, Vector2.Left)));

        Assert.True(set.TryUse(PartKind.Flagellum));
        Assert.False(set.IsReady(PartKind.Flagellum));
        Assert.Equal(1f, set.CooldownShare(PartKind.Flagellum), PRECISION);
        set.Tick(1.5f);
        Assert.Equal(0.5f, set.CooldownShare(PartKind.Flagellum), PRECISION);
        set.Tick(1.5f);
        Assert.True(set.IsReady(PartKind.Flagellum));
    }

    [Fact]
    public void MorePartsRechargeFaster()
    {
        AbilitySet one = AbilitySet.From(DesignWith((PartKind.Flagellum, Vector2.Left)));
        AbilitySet two = AbilitySet.From(DesignWith((PartKind.Flagellum, Vector2.Left), (PartKind.Flagellum, new Vector2(-0.7f, 0.7f))));

        Assert.Equal(3f, one.CooldownOf(PartKind.Flagellum), PRECISION);
        Assert.Equal(2f, two.CooldownOf(PartKind.Flagellum), PRECISION);
    }

    [Fact]
    public void EffectRunsForItsDuration()
    {
        AbilitySet set = AbilitySet.From(DesignWith((PartKind.Chromatophore, Vector2.Up)));

        Assert.True(set.TryUse(PartKind.Chromatophore));
        set.Tick(3.9f);
        Assert.True(set.IsRunning(PartKind.Chromatophore));
        set.Tick(0.2f);
        Assert.False(set.IsRunning(PartKind.Chromatophore));
    }

    [Fact]
    public void MissingPartNeverFires()
    {
        AbilitySet set = AbilitySet.From(new CellDesign());

        Assert.False(set.IsReady(PartKind.Electrocyte));
        Assert.False(set.TryUse(PartKind.Electrocyte));
        Assert.Empty(set.Slots);
        Assert.False(set.TryBlock());
    }

    [Fact]
    public void PlatesBlockAndGrowBack()
    {
        AbilitySet set = AbilitySet.From(DesignWith((PartKind.ArmorPlate, Vector2.Left), (PartKind.ArmorPlate, Vector2.Right)));

        Assert.Equal(2, set.ShieldCharges);
        Assert.True(set.TryBlock());
        Assert.True(set.TryBlock());
        Assert.False(set.TryBlock());
        set.Tick(4.1f);     // two plates: cooldown 6 / 1.5 = 4
        Assert.Equal(1, set.ShieldCharges);
        set.Tick(4.1f);
        Assert.Equal(2, set.ShieldCharges);
    }

    // ===========================================================================
    // =============================   TRACK DIET   ==============================
    // ===========================================================================
    //
    //   StartsNeutral            0 before the first meal.
    //   MeatLeansCarnivore       7.5 meat count double: 15 give 0.75, damped by the neutral mass.
    //   PlantCountsDouble        7.5 eaten plant count as 15: -0.75.
    //   LightCountsHalf          10 light count as 5 plant.
    //   RawIsIgnored             Raw biomass does not move the bar.
    //   MealsFadeWithHalfLife    After 90 s a meal counts half.
    //   PartsMixWithEating       40% part lean, 60% eating.
    //

    [Fact]
    public void StartsNeutral()
    {
        Assert.Equal(0f, new DietTracker().Value(partsLean: 0f, hasParts: false));
    }

    [Fact]
    public void MeatLeansCarnivore()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Meat, 7.5f);

        Assert.Equal(0.75f, tracker.Value(partsLean: 0f, hasParts: false), PRECISION);
    }

    [Fact]
    public void PlantCountsDouble()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Plant, 7.5f);

        Assert.Equal(-0.75f, tracker.Value(partsLean: 0f, hasParts: false), PRECISION);
    }

    [Fact]
    public void LightCountsHalf()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Light, 10f);

        Assert.Equal(-0.5f, tracker.Value(partsLean: 0f, hasParts: false), PRECISION);
    }

    [Fact]
    public void RawIsIgnored()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Raw, 100f);

        Assert.Equal(0f, tracker.Value(partsLean: 0f, hasParts: false));
    }

    [Fact]
    public void MealsFadeWithHalfLife()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Meat, 7.5f);
        tracker.Decay(90f);

        // 7.5 meat left: 7.5 / (7.5 + 5)
        Assert.Equal(0.6f, tracker.Value(partsLean: 0f, hasParts: false), PRECISION);
    }

    [Fact]
    public void PartsMixWithEating()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Meat, 7.5f);

        // lerp(-1, 0.75, 0.6)
        Assert.Equal(0.05f, tracker.Value(partsLean: -1f, hasParts: true), PRECISION);
    }

    // ===========================================================================
    // ===========================   ROLL MUTATIONS   ============================
    // ===========================================================================
    //
    //   TableListsEveryMutation   Six mutations, a third of them bad, values from the balance file.
    //   BadOnesRollAsOftenAsGood  The weights give bad mutations half of the roll.
    //   OnlyGoodOnesEarnPoints    EP for good mutations only, capped by the balance file.
    //

    [Fact]
    public void TableListsEveryMutation()
    {
        Assert.Equal(System.Enum.GetValues<MutationKind>(), Mutation.ALL.Select(mutation => mutation.Kind).ToArray());
        Assert.Equal(2, Mutation.ALL.Count(mutation => !mutation.IsGood));
        Assert.Equal(1.05f, Mutation.Get(MutationKind.Tough).Health);
        Assert.Equal(0.97f, Mutation.Get(MutationKind.Sluggish).Speed);
        Assert.Equal(1f, Mutation.Get(MutationKind.Sluggish).Health);
        Assert.Equal("MUTATION_STICKY_NAME", Mutation.Get(MutationKind.Sticky).NameKey);
    }

    [Fact]
    public void BadOnesRollAsOftenAsGood()
    {
        // Weights 1, 1, 1, 1, 2, 2 => total 8, the bad ones own 0.5..1
        Assert.Equal(MutationKind.Tough, Mutation.Pick(0f));
        Assert.Equal(MutationKind.Greedy, Mutation.Pick(0.49f));
        Assert.Equal(MutationKind.Brittle, Mutation.Pick(0.51f));
        Assert.Equal(MutationKind.Sluggish, Mutation.Pick(0.99f));
        Assert.Equal(MutationKind.Sluggish, Mutation.Pick(1f));
    }

    [Fact]
    public void OnlyGoodOnesEarnPoints()
    {
        Assert.Equal(1, Mutation.EvolutionPoints(new[] { MutationKind.Tough, MutationKind.Sluggish }));
        Assert.Equal(0, Mutation.EvolutionPoints(new[] { MutationKind.Brittle }));
        Assert.Equal(5, Mutation.EvolutionPoints(Enumerable.Repeat(MutationKind.Swift, 9)));
    }

    // ===========================================================================
    // ============================   SIZE COLONIES   ============================
    // ===========================================================================
    //
    //   ColonyFollowsPlayerCells    Half, equal or double, clamped to MIN_CELLS..MAX_CELLS.
    //   ColonyRejectsBadInput       No player cells or a step outside -1..1 throw.
    //   BurstShrinksTheArea         Size shrinks with the square root of the cells left.
    //

    [Fact]
    public void ColonyFollowsPlayerCells()
    {
        Assert.Equal(4, ColonyRule.CellCountFor(8, -1));
        Assert.Equal(8, ColonyRule.CellCountFor(8, 0));
        Assert.Equal(16, ColonyRule.CellCountFor(8, 1));
        Assert.Equal(ColonyRule.MIN_CELLS, ColonyRule.CellCountFor(2, -1));
        Assert.Equal(ColonyRule.MAX_CELLS, ColonyRule.CellCountFor(64, 0));
    }

    [Fact]
    public void ColonyRejectsBadInput()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ColonyRule.CellCountFor(0, 0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ColonyRule.CellCountFor(8, 2));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ColonyRule.SizeAfterBurst(10f, 4, 5));
    }

    [Fact]
    public void BurstShrinksTheArea()
    {
        Assert.Equal(10f, ColonyRule.SizeAfterBurst(20f, 8, 2), PRECISION);
        Assert.Equal(20f, ColonyRule.SizeAfterBurst(20f, 8, 8), PRECISION);
        Assert.Equal(15.4919f, ColonyRule.SizeAfterBurst(20f, 10, 6), PRECISION);
    }

    // ===========================================================================
    // =============================   FORM CELLS   ==============================
    // ===========================================================================
    //
    //   FormsStayInsideLimits   Every kind over many seeds: 8 radii and heights inside the CellDesign limits.
    //   FormsKeepTheirSize      The mean radius stays about 1, the contacts fit the look.
    //   FormsVary               Different seeds give different outlines, the same seed the same one.
    //   BacteriaAreLong         Most bacteria are longer than wide (rods and commas).
    //

    private const int FORM_SEEDS = 200;

    [Fact]
    public void FormsStayInsideLimits()
    {
        foreach (CritterKind kind in System.Enum.GetValues<CritterKind>())
        {
            for (int seed = 0; seed < FORM_SEEDS; seed++)
            {
                CellForm form = CellForms.For(kind, new System.Random(seed));
                Assert.Equal(CellDesign.SHAPE_POINTS, form.Shape.Length);
                Assert.Equal(CellDesign.SHAPE_POINTS, form.Heights.Length);
                Assert.All(form.Shape, radius => Assert.InRange(radius, CellDesign.MIN_SHAPE, CellDesign.MAX_SHAPE));
                Assert.All(form.Heights, height => Assert.InRange(height, CellDesign.MIN_HEIGHT, CellDesign.MAX_HEIGHT));
            }
        }
    }

    [Fact]
    public void FormsKeepTheirSize()
    {
        foreach (CritterKind kind in System.Enum.GetValues<CritterKind>())
        {
            for (int seed = 0; seed < FORM_SEEDS; seed++)
            {
                Assert.InRange(CellForms.For(kind, new System.Random(seed)).Shape.Average(), 0.95f, 1.05f);
            }
        }
    }

    [Fact]
    public void FormsVary()
    {
        CellForm first  = CellForms.For(CritterKind.Amoeba, new System.Random(1));
        CellForm again  = CellForms.For(CritterKind.Amoeba, new System.Random(1));
        CellForm second = CellForms.For(CritterKind.Amoeba, new System.Random(2));

        Assert.Equal(first.Shape, again.Shape);
        Assert.NotEqual(first.Shape, second.Shape);
    }

    [Fact]
    public void BacteriaAreLong()
    {
        int longCount = Enumerable.Range(0, FORM_SEEDS)
            .Select(seed => CellForms.For(CritterKind.Bacterium, new System.Random(seed)).Shape)
            .Count(shape => shape[0] + shape[4] > shape[2] + shape[6] + 0.3f);

        Assert.True(longCount > FORM_SEEDS / 2, $"expected most bacteria to be long, got {longCount} of {FORM_SEEDS}");
    }
}
