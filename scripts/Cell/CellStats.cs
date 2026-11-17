// NOTE:
//   What a cell gets from its placed editor parts, one formula for the Player and the NPC
//   cells (Critter), like CreatureStats for organisms. Parts are abilities (decision of the
//   user 2026-11-17), so only little stays here: the thrust of the flagella, the light of
//   the sails, the pull of the filter mouths and the directions of spikes, jaws, plates,
//   nematocysts and suckers. Cooldowns and the abilities themselves live in
//   Cell/Abilities/AbilitySet.cs and Game/Abilities/AbilityResolver.cs. Directions are
//   part angles relative to the facing (0 = forward), numbers from data/balance.json.
//
//   CellStats stats = CellStats.From(design);
//   float hit = stats.ContactDamageToward(0f);   // spike and jaw damage straight ahead
//   bool  isLatched = stats.CanSuckToward(0f);   // a sucker holds the cell in front

//
//   Chapters:
//     TRAITS       Thrust, light, pull and the part directions.
//     FROM DESIGN  What every part adds.
//     DIRECTIONS   Contact damage, bite, sting, sucker and plates towards a direction.
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
    //   Speed           Swim speed multiplier, flagella push best from the back.
    //   Photosynthesis  Light biomass per second at REFERENCE_SIZE (light sails, all the time).
    //   MagnetBonus     Food pull range bonus in body sizes (the vortex of filter mouths).
    //   VortexPull      Body sizes per second the vortex drags food, 0 without a filter mouth.
    //   *Angles         Directions of spikes, jaws, plates, nematocysts and suckers relative to the facing.
    //

    public float Speed { get; private set; } = 1f;
    public float Photosynthesis { get; private set; }
    public float MagnetBonus { get; private set; }
    public float VortexPull { get; private set; }
    public List<float> SpikeAngles { get; } = new();
    public List<float> JawAngles { get; } = new();
    public List<float> PlateAngles { get; } = new();
    public List<float> StingAngles { get; } = new();
    public List<float> SuckerAngles { get; } = new();

    public bool HasThorns => SpikeAngles.Count > 0 || JawAngles.Count > 0;

    // ===========================================================================
    // ============================   FROM DESIGN   ==============================
    // ===========================================================================
    //
    //   From   Adds up what every placed part brings, the rest of a part is its ability.
    //

    /// <summary>Traits of a design, every part adds its share.</summary>
    /// <param name="design">(Required) The placed parts.</param>
    /// <example><code>CellStats.From(new CellDesign()).Speed   // 1, no parts</code></example>
    public static CellStats From(CellDesign design)
    {
        var stats = new CellStats();
        CellEffectsBalance effects   = Balance.Current.Cell.Effects;
        IReadOnlyDictionary<PartKind, AbilityBalance> abilities = Balance.Current.Cell.Abilities;
        foreach (PlacedPart part in design.Parts)
        {
            switch (part.Kind)
            {
                // A flagellum pushes with FlagellumFrontThrust from the front up to 100% from the back, its dash is an ability
                case PartKind.Flagellum:
                    float thrust = Mathf.Lerp(effects.FlagellumFrontThrust, 1f, (1f - Mathf.Cos(part.Angle)) * 0.5f);
                    stats.Speed *= 1f + effects.FlagellumSpeed * thrust;
                    break;
                case PartKind.LightSail:
                    stats.Photosynthesis += abilities[PartKind.LightSail].Power;
                    break;
                case PartKind.FilterMouth:
                    stats.MagnetBonus += abilities[PartKind.FilterMouth].Reach;
                    stats.VortexPull   = Mathf.Max(stats.VortexPull, abilities[PartKind.FilterMouth].Power);
                    break;
                case PartKind.ArmorPlate:
                    stats.PlateAngles.Add(part.Angle);
                    break;
                case PartKind.Spike:
                    stats.SpikeAngles.Add(part.Angle);
                    break;
                case PartKind.Jaw:
                    stats.JawAngles.Add(part.Angle);
                    break;
                case PartKind.Nematocyst:
                    stats.StingAngles.Add(part.Angle);
                    break;
                case PartKind.Sucker:
                    stats.SuckerAngles.Add(part.Angle);
                    break;
                // Abilities only (AbilitySet), the eyespot only looks
                case PartKind.SlimeGland:
                case PartKind.Eyespot:
                case PartKind.JetNozzle:
                case PartKind.Cilia:
                case PartKind.Pseudopod:
                case PartKind.Chromatophore:
                case PartKind.ToxinGland:
                case PartKind.Electrocyte:
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
    //   IsAnyWithin     Whether any angle of a list covers a local direction.
    //   IsWithin        Whether a local direction lies within the arc of a part angle.
    //   *Toward         The same for the parts of this design, stings, suckers and plates included.
    //

    /// <summary>Arcs (half angles in radians) and damage (at REFERENCE_SIZE) of spikes, jaws, plates, stings and suckers.</summary>
    private static CellCombatBalance Combat => Balance.Current.Cell.Combat;

    /// <summary>Spike and jaw damage dealt towards a local direction, times the toxin.</summary>
    /// <param name="spikes">(Required) Spike angles relative to the facing.</param>
    /// <param name="jaws">(Required) Jaw angles relative to the facing.</param>
    /// <param name="toxin">(Required) Damage multiplier, 1 for a cell (its toxin is a cloud now).</param>
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

    /// <summary>Share of the damage taken from a local direction, a creature shell towards it lets only part through.</summary>
    /// <example><code>CellStats.DamageFactor(new[] { 0f }, Mathf.Pi)   // 1, the shell faces away</code></example>
    public static float DamageFactor(IEnumerable<float> plates, float localAngle)
    {
        return IsAnyWithin(plates, localAngle, Combat.PlateArc) ? Combat.PlateDamageFactor : 1f;
    }

    /// <summary>Whether any part angle of a list covers a local direction within arc.</summary>
    /// <example><code>CellStats.IsAnyWithin(new[] { 0f }, 0.3f, 0.5f)   // true</code></example>
    public static bool IsAnyWithin(IEnumerable<float> angles, float localAngle, float arc)
    {
        foreach (float angle in angles)
        {
            if (IsWithin(localAngle, angle, arc)) return true;
        }
        return false;
    }

    /// <summary>Whether a local direction lies within arc of a part angle.</summary>
    public static bool IsWithin(float localAngle, float partAngle, float arc)
    {
        return Mathf.Abs(Mathf.Wrap(localAngle - partAngle, -Mathf.Pi, Mathf.Pi)) <= arc;
    }

    /// <summary>Spike and jaw damage of this design towards a local direction.</summary>
    public float ContactDamageToward(float localAngle)
    {
        return ContactDamage(SpikeAngles, JawAngles, 1f, localAngle);
    }

    /// <summary>Whether a jaw of this design points towards a local direction.</summary>
    public bool CanBiteToward(float localAngle)
    {
        return CanBite(JawAngles, localAngle);
    }

    /// <summary>Whether an armor plate of this design covers a local direction (its shield, AbilitySet.TryBlock).</summary>
    public bool IsPlatedToward(float localAngle)
    {
        return IsAnyWithin(PlateAngles, localAngle, Combat.PlateArc);
    }

    /// <summary>Damage of one sting towards a local direction (nematocysts), 0 when none points there.</summary>
    /// <example><code>stats.StingToward(0f)   // 5 with one nematocyst in front</code></example>
    public float StingToward(float localAngle)
    {
        return IsAnyWithin(StingAngles, localAngle, Combat.StingArc) ? Combat.StingDamage : 0f;
    }

    /// <summary>Whether a sucker points towards a local direction and holds a cell there.</summary>
    public bool CanSuckToward(float localAngle)
    {
        return IsAnyWithin(SuckerAngles, localAngle, Combat.SuckerArc);
    }
}
