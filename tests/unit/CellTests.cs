// NOTE:
//   Unit tests for the cell stages: the part catalog (Cell/PartCatalog.cs), the traits
//   of a design (Cell/CellStats.cs), the diet bar (Cell/DietTracker.cs) and the
//   mutations of the prelude (Prelude/Mutation.cs).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter CellTests

//
//   Chapters:
//     UNLOCK PARTS     Catalog entries and the diet lock.
//     COMPUTE TRAITS   Part effects and the directional combat values.
//     TRACK DIET       Meals, fading and the mix with the part lean.
//     ROLL MUTATIONS   The table of the prelude.
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
    //   MeatLeansCarnivore       15 meat give 0.75, damped by the neutral mass.
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
        tracker.Add(BiomassSource.Meat, 15f);

        Assert.Equal(0.75f, tracker.Value(partsLean: 0f, hasParts: false), PRECISION);
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
        tracker.Add(BiomassSource.Meat, 15f);
        tracker.Decay(90f);

        // 7.5 meat left: 7.5 / (7.5 + 5)
        Assert.Equal(0.6f, tracker.Value(partsLean: 0f, hasParts: false), PRECISION);
    }

    [Fact]
    public void PartsMixWithEating()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Meat, 15f);

        // lerp(-1, 0.75, 0.6)
        Assert.Equal(0.05f, tracker.Value(partsLean: -1f, hasParts: true), PRECISION);
    }

    // ===========================================================================
    // ===========================   ROLL MUTATIONS   ============================
    // ===========================================================================
    //
    //   TableListsEveryMutation   Six mutations, a third of them bad, values from the balance file.
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
}
