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
    //   HeadHoldsOneMouth           A beak blocks the jaw and the filter mouth.
    //   PlantMouthsDigestPlants     Filter mouth x1.25 plants and +1 range, beak x1.5, meat untouched.
    //   ShellCoversBackAndFlanks    x1.35 health, x0.9 swim speed, 3 kg, plates at both flanks and the back.
    //   GlandPoisonsTheSpines       x1.5 on the damage of spines and jaw.
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

    [Fact]
    public void HeadHoldsOneMouth()
    {
        CreatureDesign design = CreatureDesign.Blob();

        Assert.True(design.TryPlace(CreaturePartKind.Beak, 1f, paidCost: 0));
        Assert.False(design.TryPlace(CreaturePartKind.Jaw, 1f, paidCost: 0));
        Assert.False(design.TryPlace(CreaturePartKind.FilterMouth, 1f, paidCost: 0));
    }

    [Fact]
    public void PlantMouthsDigestPlants()
    {
        CreatureDesign sieve = CreatureDesign.Blob();
        CreatureDesign beak  = CreatureDesign.Blob();
        Place(sieve, CreaturePartKind.FilterMouth);
        Place(beak, CreaturePartKind.Beak);

        CreatureStats sieved = CreatureStats.From(sieve);
        CreatureStats beaked = CreatureStats.From(beak);

        Assert.Equal(1.25f, sieved.DigestPlant, PRECISION);
        Assert.Equal(1f, sieved.MagnetBonus, PRECISION);
        Assert.Equal(1.5f, beaked.DigestPlant, PRECISION);
        Assert.Equal(0f, beaked.MagnetBonus, PRECISION);
        Assert.Equal(1f, sieved.DigestMeat, PRECISION);
        Assert.Empty(beaked.JawAngles);
    }

    [Fact]
    public void ShellCoversBackAndFlanks()
    {
        CreatureDesign design = CreatureDesign.Blob();
        CreatureStats  bare   = CreatureStats.From(design);
        Place(design, CreaturePartKind.Shell);

        CreatureStats armored = CreatureStats.From(design);

        Assert.Equal(bare.Health * 1.35f, armored.Health, PRECISION);
        Assert.Equal(bare.SwimSpeed * 0.9f, armored.SwimSpeed, PRECISION);
        Assert.Equal(bare.Weight + 3f, armored.Weight, PRECISION);
        Assert.Equal(new[] { Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f, Mathf.Pi }, armored.PlateAngles);
        Assert.Equal(0.5f, CellStats.DamageFactor(armored.PlateAngles, Mathf.Pi));
        Assert.Equal(1f, CellStats.DamageFactor(armored.PlateAngles, 0f));
        Assert.Empty(bare.PlateAngles);
    }

    [Fact]
    public void GlandPoisonsTheSpines()
    {
        CreatureDesign design = CreatureDesign.Blob();
        Place(design, CreaturePartKind.Spines);
        CreatureStats spiny = CreatureStats.From(design);
        Place(design, CreaturePartKind.PoisonGland);

        CreatureStats toxic = CreatureStats.From(design);

        float plain    = CellStats.ContactDamage(spiny.SpikeAngles, spiny.JawAngles, spiny.Toxin, Mathf.Pi);
        float poisoned = CellStats.ContactDamage(toxic.SpikeAngles, toxic.JawAngles, toxic.Toxin, Mathf.Pi);
        Assert.Equal(1f, spiny.Toxin, PRECISION);
        Assert.Equal(1.5f, toxic.Toxin, PRECISION);
        Assert.True(plain > 0f);
        Assert.Equal(plain * 1.5f, poisoned, PRECISION);
    }
}
