// NOTE:
//   Unit tests for the organism stages: the creature catalog (Creature/CreatureCatalog.cs)
//   and the traits of a body plan (Creature/CreatureStats.cs).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter CreatureTests

//
//   Chapters:
//     HELPERS          Places a part wherever it fits.
//     OFFER PARTS      Catalog entries, body types and half pairs.
//     COMPUTE TRAITS   Body, part effects, land speed and flight.
//

using System.Linq;
using Godot;
using Xunit;

namespace CellOut.Tests;

public class CreatureTests
{
    private const int PRECISION = 4;

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   Place   Puts a part on the first spot along the spine that takes it.
    //

    private static readonly float[] SPOTS = { 0.3f, 0.45f, 0.75f, 0.15f, 0.6f, 0.9f, 0.5f, 0f, 1f };

    private static void Place(CreatureDesign design, CreaturePartKind kind, int side = 0)
    {
        bool isPlaced = SPOTS.Any(spot => design.TryPlace(kind, spot, paidCost: 0, side));
        Assert.True(isPlaced, $"{kind} must fit somewhere on the body.");
    }

    // ===========================================================================
    // =============================   OFFER PARTS   =============================
    // ===========================================================================
    //
    //   CatalogListsEveryKind     One entry per CreaturePartKind, in palette order.
    //   BodyTypeFiltersParts      A fish gets the lung, never fluke or blowhole.
    //   HalfPairCostsHalfRoundedUp  2 -> 1, 3 -> 2, a midline part keeps its cost.
    //

    [Fact]
    public void CatalogListsEveryKind()
    {
        CreaturePartKind[] listed = CreatureCatalog.All().Select(part => part.Kind).ToArray();

        Assert.Equal(System.Enum.GetValues<CreaturePartKind>(), listed);
        Assert.True(CreatureCatalog.Get(CreaturePartKind.Legs).IsLeg);
        Assert.True(CreatureCatalog.Get(CreaturePartKind.FeatherWings).IsWing);
        Assert.False(CreatureCatalog.Get(CreaturePartKind.Hands).IsLeg);
    }

    [Fact]
    public void BodyTypeFiltersParts()
    {
        CreaturePartKind[] fishLungs = CreatureCatalog.ForCategory(CreatureType.Fish, CreatureCategory.Lungs).Select(part => part.Kind).ToArray();

        Assert.Equal(new[] { CreaturePartKind.Lung, CreaturePartKind.BigLung }, fishLungs);
        Assert.False(CreatureCatalog.Get(CreaturePartKind.Fluke).IsFor(CreatureType.Fish));
        Assert.True(CreatureCatalog.Get(CreaturePartKind.Jaw).IsFor(CreatureType.MarineMammal));
    }

    [Fact]
    public void HalfPairCostsHalfRoundedUp()
    {
        Assert.Equal(2, CreatureCatalog.Get(CreaturePartKind.Hands).CostFor(side: 0));
        Assert.Equal(1, CreatureCatalog.Get(CreaturePartKind.Hands).CostFor(side: 1));
        Assert.Equal(2, CreatureCatalog.Get(CreaturePartKind.Claws).CostFor(side: -1));
        Assert.Equal(2, CreatureCatalog.Get(CreaturePartKind.Jaw).CostFor(side: 1));
    }

    // ===========================================================================
    // ===========================   COMPUTE TRAITS   ============================
    // ===========================================================================
    //
    //   BlobWeighsAboutTen          The bare clay blob: 7 to 11 kg, crawls on land, cannot fly.
    //   BodyFollowsGirthAndType     Health and swim speed of the blob from the body formula.
    //   TailFinSpeedsUp             Caudal fin x1.3 swim speed and its weight on top.
    //   HalfPairCountsWithItsRoot   A single pectoral fin gives sqrt(1.3) and half the weight.
    //   LegsCarryTheBody            One pair of legs: carry 14, drag in the water, walking speed from the load.
    //   BrainPaysEvolutionPoints    Big brain +2 EP per level and x1.15 steering.
    //   WingsLiftTheirShare         Two pairs of feather wings lift 20, the ratio decides flight.
    //   ClawsScratchAtTheirFlank    A single claw only hurts on its own side.
    //

    [Fact]
    public void BlobWeighsAboutTen()
    {
        CreatureStats blob = CreatureStats.From(CreatureDesign.Blob());

        // The comments in the code say "about 10", the blob of CreatureDesign.Blob() comes to 8.07
        Assert.InRange(blob.Weight, 7f, 11f);
        Assert.Equal(0f, blob.Carry);
        Assert.Equal(0.3f, blob.LandSpeed);
        Assert.True(float.IsPositiveInfinity(blob.Load));
        Assert.False(blob.CanFly);
    }

    [Fact]
    public void BodyFollowsGirthAndType()
    {
        CreatureDesign design = CreatureDesign.Blob();
        float girth = design.MeanGirth;
        int   extra = design.Girths.Count - 5;
        float typeHealth = CreatureType.MarineMammal == design.Type ? 1.3f : 1f;
        float typeSwim   = CreatureType.Fish == design.Type ? 1.1f : 1f;

        CreatureStats stats = CreatureStats.From(design);

        Assert.Equal(typeHealth * (0.7f + 0.6f * girth) * (1f + 0.05f * extra), stats.Health, PRECISION);
        Assert.Equal(typeSwim * (1.15f - 0.3f * girth) * (1f + 0.03f * extra), stats.SwimSpeed, PRECISION);
        Assert.Equal(design.Volume() * 7.5f, stats.Weight, PRECISION);
    }

    [Fact]
    public void TailFinSpeedsUp()
    {
        CreatureDesign design = CreatureDesign.Blob();
        design.Type = CreatureType.Fish;
        CreatureStats bare = CreatureStats.From(design);
        Place(design, CreaturePartKind.CaudalFin);

        CreatureStats finned = CreatureStats.From(design);

        Assert.Equal(bare.SwimSpeed * 1.3f, finned.SwimSpeed, PRECISION);
        Assert.Equal(bare.Weight + 0.6f, finned.Weight, PRECISION);
    }

    [Fact]
    public void HalfPairCountsWithItsRoot()
    {
        CreatureDesign design = CreatureDesign.Blob();
        CreatureStats  bare   = CreatureStats.From(design);
        Place(design, CreaturePartKind.PectoralFin, side: 1);

        CreatureStats half = CreatureStats.From(design);

        Assert.Equal(bare.Acceleration * Mathf.Sqrt(1.3f), half.Acceleration, PRECISION);
        Assert.Equal(bare.Weight + 0.2f, half.Weight, PRECISION);
    }

    [Fact]
    public void LegsCarryTheBody()
    {
        CreatureDesign design = CreatureDesign.Blob();
        CreatureStats  bare   = CreatureStats.From(design);
        Place(design, CreaturePartKind.Legs);

        CreatureStats legged = CreatureStats.From(design);

        float weight     = bare.Weight + 1.5f;
        float loadFactor = Mathf.Clamp(1.5f - 0.5f * weight / 14f, 0.4f, 1.2f);
        Assert.Equal(1, design.LegPairs);
        Assert.Equal(14f, legged.Carry, PRECISION);
        Assert.Equal(weight, legged.Weight, PRECISION);
        Assert.Equal(bare.SwimSpeed * 0.95f, legged.SwimSpeed, PRECISION);
        Assert.Equal(0.85f * 1f * loadFactor, legged.LandSpeed, PRECISION);
    }

    [Fact]
    public void BrainPaysEvolutionPoints()
    {
        CreatureDesign design = CreatureDesign.Blob();
        CreatureStats  bare   = CreatureStats.From(design);
        Place(design, CreaturePartKind.BigBrain);

        CreatureStats smart = CreatureStats.From(design);

        Assert.Equal(0, bare.EpPerLevelBonus);
        Assert.Equal(2, smart.EpPerLevelBonus);
        Assert.Equal(bare.Acceleration * 1.15f, smart.Acceleration, PRECISION);
    }

    [Fact]
    public void WingsLiftTheirShare()
    {
        CreatureDesign design = CreatureDesign.Blob();
        Place(design, CreaturePartKind.FeatherWings);
        Place(design, CreaturePartKind.FeatherWings);

        CreatureStats winged = CreatureStats.From(design);

        Assert.Equal(20f, winged.Lift, PRECISION);
        Assert.Equal(20f / winged.Weight, winged.FlightRatio, PRECISION);
        Assert.Equal(winged.FlightRatio >= 1f, winged.CanFly);
    }

    [Fact]
    public void ClawsScratchAtTheirFlank()
    {
        CreatureDesign design = CreatureDesign.Blob();
        Place(design, CreaturePartKind.Claws, side: 1);

        CreatureStats clawed = CreatureStats.From(design);

        Assert.Equal(new[] { 0.5f }, clawed.SpikeAngles);
        Assert.Equal(0.075f, clawed.MagnetBonus, PRECISION);
    }
}
