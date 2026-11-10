// NOTE:
//   Traits a cell gets from its placed editor parts: one formula for the Player and the
//   NPC cells (Critter), like CreatureStats for organisms. Directions of spikes, jaws and
//   plates are part angles relative to the facing (0 = forward). The numbers come from
//   data/balance.json (Core/Balance.cs).
//
//   CellStats stats = CellStats.From(design);
//   float hit = stats.ContactDamageToward(0f);   // spike and jaw damage straight ahead

//
//   Chapters:
//     TRAITS       Multipliers, bonuses and the part directions.
//     FROM DESIGN  The effect of every part.
//     DIRECTIONS   Contact damage, bite and armor towards a direction.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public sealed class CellStats
{
    // ===========================================================================
    // ===============================   TRAITS   ================================
    // ===========================================================================
    //
    //   Speed           Swim speed multiplier (flagella, pushing best from the back).
    //   Acceleration    Steering multiplier (cilia).
    //   Health          Health multiplier (armor plates).
    //   MagnetBonus     Food pull range bonus in body sizes (filter mouth, eyespot).
    //   SightBonus      Sense range bonus of NPC cells, share of the base range (eyespot).
    //   Digest*         Biomass multipliers for every meal, plants and meat.
    //   Photosynthesis  Light biomass per second at REFERENCE_SIZE (chloroplasts).
    //   Toxin           Multiplier on spike and jaw damage.
    //   *Angles         Directions of spikes, jaws and plates relative to the facing.
    //

    public float Speed { get; private set; } = 1f;
    public float Acceleration { get; private set; } = 1f;
    public float Health { get; private set; } = 1f;
    public float MagnetBonus { get; private set; }
    public float SightBonus { get; private set; }
    public float DigestAll { get; private set; } = 1f;
    public float DigestPlant { get; private set; } = 1f;
    public float DigestMeat { get; private set; } = 1f;
    public float Photosynthesis { get; private set; }
    public float Toxin { get; private set; } = 1f;
    public List<float> SpikeAngles { get; } = new();
    public List<float> JawAngles { get; } = new();
    public List<float> PlateAngles { get; } = new();

    public bool HasThorns => SpikeAngles.Count > 0 || JawAngles.Count > 0;

    // ===========================================================================
    // ============================   FROM DESIGN   ==============================
    // ===========================================================================
    //
    //   From   Adds up the effect of every placed part.
    //

    /// <summary>Traits of a design, every part adds its effect.</summary>
    /// <param name="design">(Required) The placed parts.</param>
    /// <example><code>CellStats.From(new CellDesign()).Speed   // 1, no parts</code></example>
    public static CellStats From(CellDesign design)
    {
        var stats = new CellStats();
        CellEffectsBalance effects = Balance.Current.Cell.Effects;
        foreach (PlacedPart part in design.Parts)
        {
            switch (part.Kind)
            {
                // Herbivore
                case PartKind.Chloroplast:
                    stats.Photosynthesis += effects.ChloroplastLight;
                    break;
                case PartKind.FilterMouth:
                    stats.MagnetBonus += effects.FilterMouthMagnet;
                    stats.DigestPlant *= effects.FilterMouthDigestPlant;
                    break;
                case PartKind.ArmorPlate:
                    stats.Health *= effects.ArmorPlateHealth;
                    stats.PlateAngles.Add(part.Angle);
                    break;
                // Omnivore: a flagellum pushes with FlagellumFrontThrust from the front up to 100% from the back
                case PartKind.Flagellum:
                    float thrust = Mathf.Lerp(effects.FlagellumFrontThrust, 1f, (1f - Mathf.Cos(part.Angle)) * 0.5f);
                    stats.Speed *= 1f + effects.FlagellumSpeed * thrust;
                    break;
                case PartKind.Eyespot:
                    stats.MagnetBonus += effects.EyespotMagnet;
                    stats.SightBonus  += effects.EyespotSight;
                    break;
                case PartKind.Vacuole:
                    stats.DigestAll *= effects.VacuoleDigest;
                    break;
                case PartKind.Cilia:
                    stats.Acceleration *= effects.CiliaAcceleration;
                    break;
                // Carnivore
                case PartKind.Spike:
                    stats.SpikeAngles.Add(part.Angle);
                    break;
                case PartKind.Jaw:
                    stats.JawAngles.Add(part.Angle);
                    stats.DigestMeat *= effects.JawDigestMeat;
                    break;
                case PartKind.Toxin:
                    stats.Toxin *= effects.ToxinDamage;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(design), $"design must hold known PartKinds only, got '{part.Kind}'.");
            }
        }
        return stats;
    }

    // ===========================================================================
    // =============================   DIRECTIONS   ==============================
    // ===========================================================================
    //
    //   Combat          Arcs and damage from data/balance.json (cell.combat).
    //   ContactDamage   Spike and jaw damage towards a local direction, any angle lists.
    //   CanBite         Whether a jaw of an angle list points towards a local direction.
    //   DamageFactor    Share of the damage that gets past the plates of an angle list.
    //   IsWithin        Whether a local direction lies within the arc of a part angle.
    //   *Toward/From    The same for the parts of this design.
    //


    /// <summary>Arcs (half angles in radians) and damage (at REFERENCE_SIZE) of spikes, jaws and plates.</summary>
    private static CellCombatBalance Combat => Balance.Current.Cell.Combat;

    /// <summary>Spike and jaw damage dealt towards a local direction, times the toxin.</summary>
    /// <param name="spikes">(Required) Spike angles relative to the facing.</param>
    /// <param name="jaws">(Required) Jaw angles relative to the facing.</param>
    /// <param name="toxin">(Required) Damage multiplier.</param>
    /// <param name="localAngle">(Required) Direction relative to the facing.</param>
    public static float ContactDamage(IEnumerable<float> spikes, IEnumerable<float> jaws, float toxin, float localAngle)
    {
        float damage = 0f;
        foreach (float angle in spikes)
        {
            if (IsWithin(localAngle, angle, Combat.SpikeArc)) damage += Combat.SpikeDamage;
        }
        foreach (float angle in jaws)
        {
            if (IsWithin(localAngle, angle, Combat.JawArc)) damage += Combat.JawDamage;
        }
        return damage * toxin;
    }

    /// <summary>Whether a jaw at one of the angles points towards a local direction.</summary>
    /// <example><code>CellStats.CanBite(new[] { 0f }, 0.5f)   // true, inside the jaw arc</code></example>
    public static bool CanBite(IEnumerable<float> jaws, float localAngle)
    {
        foreach (float angle in jaws)
        {
            if (IsWithin(localAngle, angle, Combat.JawArc)) return true;
        }
        return false;
    }

    /// <summary>Share of the damage taken from a local direction, a plate towards it lets only part through.</summary>
    /// <example><code>CellStats.DamageFactor(new[] { 0f }, Mathf.Pi)   // 1, the plate faces away</code></example>
    public static float DamageFactor(IEnumerable<float> plates, float localAngle)
    {
        foreach (float angle in plates)
        {
            if (IsWithin(localAngle, angle, Combat.PlateArc)) return Combat.PlateDamageFactor;
        }
        return 1f;
    }

    /// <summary>Whether a local direction lies within arc of a part angle.</summary>
    public static bool IsWithin(float localAngle, float partAngle, float arc)
    {
        return Mathf.Abs(Mathf.Wrap(localAngle - partAngle, -Mathf.Pi, Mathf.Pi)) <= arc;
    }

    /// <summary>Spike and jaw damage of this design towards a local direction.</summary>
    public float ContactDamageToward(float localAngle)
    {
        return ContactDamage(SpikeAngles, JawAngles, Toxin, localAngle);
    }

    /// <summary>Whether a jaw of this design points towards a local direction.</summary>
    public bool CanBiteToward(float localAngle)
    {
        return CanBite(JawAngles, localAngle);
    }

    /// <summary>Share of the damage taken from a local direction, plates halve it.</summary>
    public float DamageFactorFrom(float localAngle)
    {
        return DamageFactor(PlateAngles, localAngle);
    }
}
