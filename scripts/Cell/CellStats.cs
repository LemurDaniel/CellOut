// NOTE:
//   Traits a cell gets from its placed editor parts: one formula for the Player and the
//   NPC cells (Critter), like CreatureStats for organisms. Directions of spikes, jaws and
//   plates are part angles relative to the facing (0 = forward).
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
        foreach (PlacedPart part in design.Parts)
        {
            switch (part.Kind)
            {
                // Herbivore
                case PartKind.Chloroplast:
                    stats.Photosynthesis += 0.4f;
                    break;
                case PartKind.FilterMouth:
                    stats.MagnetBonus += 0.4f;
                    stats.DigestPlant *= 1.25f;
                    break;
                case PartKind.ArmorPlate:
                    stats.Health *= 1.1f;
                    stats.PlateAngles.Add(part.Angle);
                    break;
                // Omnivore: a flagellum pushes with 30% from the front up to 100% from the back
                case PartKind.Flagellum:
                    float thrust = Mathf.Lerp(0.3f, 1f, (1f - Mathf.Cos(part.Angle)) * 0.5f);
                    stats.Speed *= 1f + 0.22f * thrust;
                    break;
                case PartKind.Eyespot:
                    stats.MagnetBonus += 0.5f;
                    stats.SightBonus  += 0.5f;
                    break;
                case PartKind.Vacuole:
                    stats.DigestAll *= 1.2f;
                    break;
                case PartKind.Cilia:
                    stats.Acceleration *= 1.4f;
                    break;
                // Carnivore
                case PartKind.Spike:
                    stats.SpikeAngles.Add(part.Angle);
                    break;
                case PartKind.Jaw:
                    stats.JawAngles.Add(part.Angle);
                    stats.DigestMeat *= 1.2f;
                    break;
                case PartKind.Toxin:
                    stats.Toxin *= 1.6f;
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
    //   ContactDamage   Spike and jaw damage towards a local direction, any angle lists.
    //   IsWithin        Whether a local direction lies within the arc of a part angle.
    //   *Toward/From    The same for the parts of this design.
    //

    public const float SPIKE_ARC = 0.7f;            // half angle a spike covers, radians
    public const float JAW_ARC = 0.8f;
    public const float PLATE_ARC = 0.9f;
    public const float SPIKE_DAMAGE = 8f;           // at REFERENCE_SIZE
    public const float JAW_DAMAGE = 10f;
    public const float PLATE_DAMAGE_FACTOR = 0.5f;

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
            if (IsWithin(localAngle, angle, SPIKE_ARC)) damage += SPIKE_DAMAGE;
        }
        foreach (float angle in jaws)
        {
            if (IsWithin(localAngle, angle, JAW_ARC)) damage += JAW_DAMAGE;
        }
        return damage * toxin;
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
        return JawAngles.Exists(angle => IsWithin(localAngle, angle, JAW_ARC));
    }

    /// <summary>Share of the damage taken from a local direction, plates halve it.</summary>
    public float DamageFactorFrom(float localAngle)
    {
        return PlateAngles.Exists(angle => IsWithin(localAngle, angle, PLATE_ARC)) ? PLATE_DAMAGE_FACTOR : 1f;
    }
}
