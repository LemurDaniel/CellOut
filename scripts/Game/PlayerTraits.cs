// NOTE:
//   Every trait the player takes from its body, as one value: of a cell from its placed
//   parts (CellStats, the same formula as the NPC cells) and the mutations of the RNA
//   prelude, of an organism from its body plan (CreatureStats). The Player asks for a new
//   one whenever the design changes and only reads it. Pure logic without the engine,
//   tested in tests/unit.
//
//   PlayerTraits traits = PlayerTraits.OfCell(design, mutations);
//   float damage = traits.ContactDamage(localAngle);
//   PlayerTraits body = PlayerTraits.OfCreature(creature);

//
//   Chapters:
//     TRAITS   The values and how a cell or a creature fills them.
//     COMBAT   What spikes, jaws and plates do towards a direction.
//

using System.Collections.Generic;

namespace CellOut;

public sealed class PlayerTraits
{
    // ===========================================================================
    // ================================   TRAITS   ===============================
    // ===========================================================================
    //
    //   Health           Multiplier of the maximum health (without the divisions).
    //   Speed            Multiplier of the swim speed.
    //   LandSpeed        Multiplier of the speed on land: crawling until legs carry the body.
    //   Acceleration     How fast the body takes up speed.
    //   DigestAll / DigestPlant / DigestMeat   Biomass gained per bite by source.
    //   Photosynthesis   Biomass per second from light at reference size.
    //   Toxin            Multiplier of spike and jaw damage.
    //   MagnetRange      Body sizes from which food drifts to the player.
    //   EpPerLevel       Evolution points per level, a brain adds to it.
    //   Cell             Traits only a cell has, neutral for an organism.
    //   Base             The traits of a bare body.
    //   OfCell           Traits of a cell from its parts and mutations.
    //   OfCreature       Traits of an organism from its body plan.
    //

    /// <summary>Base values of the player from data/balance.json (player).</summary>
    private static PlayerBalance Tune => Balance.Current.Player;

    private readonly List<float> _spikeAngles = new();
    private readonly List<float> _jawAngles = new();
    private readonly List<float> _plateAngles = new();

    public float Health { get; private set; } = 1f;
    public float Speed { get; private set; } = 1f;
    public float LandSpeed { get; private set; } = Balance.Current.Creature.Land.CrawlSpeed;
    public float Acceleration { get; private set; } = Tune.Acceleration;
    public float DigestAll { get; private set; } = 1f;
    public float DigestPlant { get; private set; } = 1f;
    public float DigestMeat { get; private set; } = 1f;
    public float Photosynthesis { get; private set; }
    public float Toxin { get; private set; } = 1f;
    public float MagnetRange { get; private set; } = Tune.MagnetRange;
    public int EpPerLevel { get; private set; } = Tune.EpPerLevel;
    public CellStats Cell { get; private set; } = new();

    private PlayerTraits()
    {
    }

    /// <summary>The traits of a body without parts.</summary>
    public static PlayerTraits Base()
    {
        return new PlayerTraits();
    }

    /// <summary>The traits of a cell: its placed parts, then the mutations of the RNA prelude on top.</summary>
    /// <param name="design">(Required) The cell design with its placed parts.</param>
    /// <param name="mutations">(Required) Mutations of the prelude, empty for none.</param>
    public static PlayerTraits OfCell(CellDesign design, IReadOnlyList<MutationKind> mutations)
    {
        CellStats cell   = CellStats.From(design);
        var       traits = new PlayerTraits { Cell = cell };
        traits.Health         *= cell.Health;
        traits.Speed          *= cell.Speed;
        traits.Acceleration   *= cell.Acceleration;
        traits.DigestAll      *= cell.DigestAll;
        traits.DigestPlant    *= cell.DigestPlant;
        traits.DigestMeat     *= cell.DigestMeat;
        traits.Photosynthesis += cell.Photosynthesis;
        traits.Toxin          *= cell.Toxin;
        traits.MagnetRange    += cell.MagnetBonus;
        traits._spikeAngles.AddRange(cell.SpikeAngles);
        traits._jawAngles.AddRange(cell.JawAngles);
        traits._plateAngles.AddRange(cell.PlateAngles);

        foreach (MutationKind kind in mutations)
        {
            Mutation mutation = Mutation.Get(kind);
            traits.Health      *= mutation.Health;
            traits.Speed       *= mutation.Speed;
            traits.DigestAll   *= mutation.Digest;
            traits.MagnetRange *= mutation.Magnet;
        }
        return traits;
    }

    /// <summary>The traits of an organism: speed, turning, senses, diet, weapons and shell of its body plan, the cell parts are history.</summary>
    /// <param name="creature">(Required) The body plan.</param>
    public static PlayerTraits OfCreature(CreatureDesign creature)
    {
        CreatureStats stats  = CreatureStats.From(creature);
        var           traits = new PlayerTraits();
        traits.Health        = stats.Health;
        traits.Speed         = stats.SwimSpeed;
        traits.Acceleration *= stats.Acceleration;
        traits.DigestPlant  *= stats.DigestPlant;
        traits.DigestMeat   *= stats.DigestMeat;
        traits.Toxin        *= stats.Toxin;
        traits.LandSpeed     = stats.LandSpeed;
        traits.EpPerLevel    = Tune.EpPerLevel + stats.EpPerLevelBonus;
        traits.MagnetRange  += stats.MagnetBonus;
        traits._jawAngles.AddRange(stats.JawAngles);
        traits._spikeAngles.AddRange(stats.SpikeAngles);
        traits._plateAngles.AddRange(stats.PlateAngles);
        return traits;
    }

    // ===========================================================================
    // ================================   COMBAT   ===============================
    // ===========================================================================
    //
    //   HasThorns       Whether any spike or jaw exists.
    //   ContactDamage   Spike and jaw damage towards a direction of the body.
    //   CanBite         Whether a jaw points towards a direction.
    //   DamageFactor    Share of the damage taken from a direction: plates halve it, slime lowers it everywhere.
    //
    //   Angles are local to the body: 0 is its front.
    //

    public bool HasThorns => _spikeAngles.Count > 0 || _jawAngles.Count > 0;

    public float ContactDamage(float localAngle)
    {
        return CellStats.ContactDamage(_spikeAngles, _jawAngles, Toxin, localAngle);
    }

    public bool CanBite(float localAngle)
    {
        return CellStats.CanBite(_jawAngles, localAngle);
    }

    public float DamageFactor(float localAngle)
    {
        return CellStats.DamageFactor(_plateAngles, localAngle) * Cell.DamageTaken;
    }
}
