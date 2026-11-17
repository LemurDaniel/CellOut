// NOTE:
//   Unit tests for Game/PlayerTraits.cs: the traits the player takes from a bare body,
//   from the parts and mutations of a cell and from the body plan of a creature. The
//   expected values belong to tests/unit/balance.fixture.json (player, cell.effects,
//   prelude.mutations, creature.land).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter PlayerTraitsTests

//
//   Chapters:
//     TAKE THE TRAITS   Base values, a cell with mutations, a creature.
//

using Godot;
using Xunit;

namespace CellOut.Tests;

public class PlayerTraitsTests
{
    // ===========================================================================
    // ===========================   TAKE THE TRAITS   ===========================
    // ===========================================================================
    //
    //   BareBodyHasBaseValues           Acceleration 2.5, magnet 1.6, 2 EP per level, crawling 0.3, no weapons.
    //   CellPartsAndMutationsMultiply   A flagellum behind gives 1.22 speed, Swift 1.03 on top, Tough 1.05 health, Sticky 1.1 magnet.
    //   SpikeHurtsTowardsItsSideOnly    A spike in front deals damage ahead and none behind.
    //   CreatureTakesItsBodyPlan        Speed, health, land speed and EP of CreatureStats, mutations do not count.
    //

    private const int PRECISION = 3;

    [Fact]
    public void BareBodyHasBaseValues()
    {
        PlayerTraits traits = PlayerTraits.Base();

        Assert.Equal(1f, traits.Health);
        Assert.Equal(1f, traits.Speed);
        Assert.Equal(2.5f, traits.Acceleration);
        Assert.Equal(1.6f, traits.MagnetRange);
        Assert.Equal(2, traits.EpPerLevel);
        Assert.Equal(0.3f, traits.LandSpeed);
        Assert.Equal(0f, traits.Photosynthesis);
        Assert.False(traits.HasThorns);
        Assert.Equal(0f, traits.ContactDamage(0f));
        Assert.False(traits.CanBite(0f));
        Assert.Equal(1f, traits.DamageFactor(0f));
    }

    [Fact]
    public void CellPartsAndMutationsMultiply()
    {
        var design = new CellDesign();
        Assert.True(design.TryPlace(PartKind.Flagellum, Vector2.Left, paidCost: 0));

        PlayerTraits plain   = PlayerTraits.OfCell(design, System.Array.Empty<MutationKind>());
        PlayerTraits mutated = PlayerTraits.OfCell(design, new[] { MutationKind.Tough, MutationKind.Swift, MutationKind.Sticky });

        Assert.Equal(1.22f, plain.Speed, PRECISION);
        Assert.Equal(1f, plain.Health, PRECISION);
        Assert.Equal(1.6f, plain.MagnetRange, PRECISION);
        Assert.Equal(1.22f * 1.03f, mutated.Speed, PRECISION);
        Assert.Equal(1.05f, mutated.Health, PRECISION);
        Assert.Equal(1.6f * 1.1f, mutated.MagnetRange, PRECISION);
        Assert.Equal(CellStats.From(design).Speed, mutated.Cell.Speed);
    }

    [Fact]
    public void SpikeHurtsTowardsItsSideOnly()
    {
        var design = new CellDesign();
        Assert.True(design.TryPlace(PartKind.Spike, Vector2.Right, paidCost: 0));

        PlayerTraits traits = PlayerTraits.OfCell(design, System.Array.Empty<MutationKind>());
        CellStats    cell   = CellStats.From(design);

        Assert.True(traits.HasThorns);
        Assert.True(traits.ContactDamage(0f) > 0f);
        Assert.Equal(CellStats.ContactDamage(cell.SpikeAngles, cell.JawAngles, 1f, 0f), traits.ContactDamage(0f));
        Assert.Equal(0f, traits.ContactDamage(Mathf.Pi));
    }

    [Fact]
    public void CreatureTakesItsBodyPlan()
    {
        CreatureDesign creature = CreatureDesign.Blob();
        CreatureStats  stats    = CreatureStats.From(creature);

        PlayerTraits traits = PlayerTraits.OfCreature(creature);

        Assert.Equal(stats.SwimSpeed, traits.Speed);
        Assert.Equal(stats.Health, traits.Health);
        Assert.Equal(stats.LandSpeed, traits.LandSpeed);
        Assert.Equal(2.5f * stats.Acceleration, traits.Acceleration, PRECISION);
        Assert.Equal(2 + stats.EpPerLevelBonus, traits.EpPerLevel);
        Assert.Equal(1.6f + stats.MagnetBonus, traits.MagnetRange, PRECISION);
        Assert.Equal(1f, traits.DigestAll);
        Assert.Equal(0f, traits.Cell.Photosynthesis);
    }
}
