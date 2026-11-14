// NOTE:
//   Unit tests for the cell stages: the part catalog (Cell/PartCatalog.cs), the traits
//   of a design (Cell/CellStats.cs), the diet bar (Cell/DietTracker.cs) and the
//   mutations of the prelude (Prelude/Mutation.cs), the NPC colonies (Cell/ColonyRule.cs).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter CellTests

//
//   Chapters:
//     UNLOCK PARTS     Catalog entries and the diet lock.
//     COMPUTE TRAITS   Part effects and the directional combat values.
//     TRACK DIET       Meals, fading and the mix with the part lean.
//     ROLL MUTATIONS   The table of the prelude.
//     SIZE COLONIES    Cell count and burst size of the NPC colonies.
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
        Assert.Equal(-1f, PartCatalog.Get(PartKind.Chloroplast).Lean);
        Assert.Equal(1f, PartCatalog.Get(PartKind.Toxin).Lean);
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
    //   BareCellHasBaseTraits        No parts: every multiplier 1, no thorns.
    //   FlagellumPushesBestFromBack  +22% at the back, +6.6% at the front.
    //   SpikeHurtsOnlyTowardsItsSide 8 inside its arc, 0 outside, toxin multiplies.
    //   JawBitesTowardsItsSide       Inside the jaw arc only.
    //   PlateHalvesDamageFromItsSide 0.5 towards the plate, 1 from behind.
    //   ArcWrapsAroundTheBack        A part at 3 rad covers -3 rad.
    //   MitochondriaAndStarchTradeSpeed  +8% speed and +10% agility, starch doubles regen for -6% speed.
    //   SlimeAndPigmentStopAtTheirFloor  0.8 and 0.75 for one, never below 0.5 and 0.4.
    //   SlimeLowersDamageFromEverySide   Plate and slime: 0.4 in front, 0.8 from behind.
    //   PiliAndPseudopodsWork            Half the knockback, +0.8 pull range.
    //   NematocystStingsTowardsItsSide   5 inside its arc, 0 outside, toxin multiplies.
    //   SuckerHoldsTowardsItsSide        Inside the sucker arc only.
    //   PartSizeIsClampedAndLooksOnly  1 at first, clamped to 0.5..2, the speed stays.
    //

    [Fact]
    public void BareCellHasBaseTraits()
    {
        CellStats stats = CellStats.From(new CellDesign());

        Assert.Equal(1f, stats.Speed);
        Assert.Equal(1f, stats.Health);
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
    public void PlateHalvesDamageFromItsSide()
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

    /// <summary>A design with the given parts, inner ones spread over the cell, rim ones at their direction.</summary>
    private static CellDesign DesignWith(params (PartKind Kind, Vector2 Point)[] parts)
    {
        var design = new CellDesign();
        foreach ((PartKind kind, Vector2 point) in parts)
        {
            Assert.True(design.TryPlace(kind, point, paidCost: 0));
        }
        return design;
    }

    private static readonly Vector2[] INNER_SPOTS = { Vector2.Zero, new(0.4f, 0f), new(-0.4f, 0f), new(0f, 0.4f) };

    [Fact]
    public void MitochondriaAndStarchTradeSpeed()
    {
        CellStats mito   = CellStats.From(DesignWith((PartKind.Mitochondrion, Vector2.Zero)));
        CellStats starch = CellStats.From(DesignWith((PartKind.StarchGranule, Vector2.Zero)));

        Assert.Equal(1.08f, mito.Speed, PRECISION);
        Assert.Equal(1.1f, mito.Acceleration, PRECISION);
        Assert.Equal(2f, starch.Regen, PRECISION);
        Assert.Equal(0.94f, starch.Speed, PRECISION);
        Assert.Equal(1f, mito.Regen);
    }

    [Fact]
    public void SlimeAndPigmentStopAtTheirFloor()
    {
        CellStats oneSlime   = CellStats.From(DesignWith((PartKind.SlimeCoat, Vector2.Zero)));
        CellStats fourSlimes = CellStats.From(DesignWith(INNER_SPOTS.Select(spot => (PartKind.SlimeCoat, spot)).ToArray()));
        CellStats onePigment = CellStats.From(DesignWith((PartKind.Camouflage, Vector2.Zero)));
        CellStats fourPigments = CellStats.From(DesignWith(INNER_SPOTS.Select(spot => (PartKind.Camouflage, spot)).ToArray()));

        Assert.Equal(0.8f, oneSlime.DamageTaken, PRECISION);
        Assert.Equal(0.5f, fourSlimes.DamageTaken, PRECISION);
        Assert.Equal(0.75f, onePigment.Camouflage, PRECISION);
        Assert.Equal(0.4f, fourPigments.Camouflage, PRECISION);
    }

    [Fact]
    public void SlimeLowersDamageFromEverySide()
    {
        CellStats stats = CellStats.From(DesignWith((PartKind.ArmorPlate, Vector2.Right), (PartKind.SlimeCoat, Vector2.Zero)));

        Assert.Equal(0.4f, stats.DamageFactorFrom(0f), PRECISION);
        Assert.Equal(0.8f, stats.DamageFactorFrom(Mathf.Pi), PRECISION);
    }

    [Fact]
    public void PiliAndPseudopodsWork()
    {
        CellStats stats = CellStats.From(DesignWith((PartKind.Pili, Vector2.Left), (PartKind.Pseudopod, Vector2.Right)));

        Assert.Equal(0.5f, stats.Knockback, PRECISION);
        Assert.Equal(0.8f, stats.MagnetBonus, PRECISION);
        Assert.False(stats.HasThorns);
    }

    [Fact]
    public void NematocystStingsTowardsItsSide()
    {
        CellStats plain = CellStats.From(DesignWith((PartKind.Nematocyst, Vector2.Right)));
        CellStats toxic = CellStats.From(DesignWith((PartKind.Nematocyst, Vector2.Right), (PartKind.Toxin, Vector2.Zero)));

        Assert.Equal(5f, plain.StingToward(0.4f), PRECISION);
        Assert.Equal(0f, plain.StingToward(0.6f), PRECISION);
        Assert.Equal(8f, toxic.StingToward(0f), PRECISION);
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
}
