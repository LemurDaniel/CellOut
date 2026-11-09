// NOTE:
//   Traits of an organism computed from its body plan: weight, carrying capacity of the
//   legs, speed in the water and on land, turning, health, senses, weapons, lift of the
//   wings and the evolution point bonus of a brain. The one formula behind
//   Player.ApplyCreature and the stats column of the CreatureEditor, so the preview
//   always shows what the player gets. Multipliers are relative to the base values of
//   the Player (1 = unchanged).
//
//   CreatureStats stats = CreatureStats.From(design);
//   float landShare     = stats.LandSpeed;      // share of the swim speed of the body size
//   bool  isFlightReady = stats.CanFly;         // flying itself comes later

//
//   Chapters:
//     STATS     The computed values and the derived ratios.
//     COMPUTE   Body, parts, land and air from a design.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public sealed class CreatureStats
{
    // ===========================================================================
    // ================================   STATS   ================================
    // ===========================================================================
    //
    //   Water        Swim speed, turning and health multipliers.
    //   Senses       Food range and the brain bonus.
    //   Weapons      Bite and spike directions, meat digestion.
    //   Weight       Body and part weight, what the legs carry and the wings lift.
    //

    // ---------------------------------------------------------------------------
    // Water
    //   Swim speed, turning and health multipliers.
    // ---------------------------------------------------------------------------

    public float SwimSpeed { get; private set; } = 1f;
    public float Acceleration { get; private set; } = 1f;
    public float Health { get; private set; } = 1f;

    // ---------------------------------------------------------------------------
    // Senses
    //   Food range and the brain bonus.
    // ---------------------------------------------------------------------------

    /// <summary>Added to the magnet range of the player (body sizes).</summary>
    public float MagnetBonus { get; private set; }

    /// <summary>Extra evolution points per level.</summary>
    public int EpPerLevelBonus { get; private set; }

    // ---------------------------------------------------------------------------
    // Weapons
    //   Bite and spike directions, meat digestion.
    // ---------------------------------------------------------------------------

    private readonly List<float> _jawAngles = new();
    private readonly List<float> _spikeAngles = new();

    /// <summary>Directions (radians, 0 = forward) a jaw bites towards.</summary>
    public IReadOnlyList<float> JawAngles => _jawAngles;

    /// <summary>Directions (radians, 0 = forward) spikes or claws hurt towards.</summary>
    public IReadOnlyList<float> SpikeAngles => _spikeAngles;

    public float DigestMeat { get; private set; } = 1f;

    // ---------------------------------------------------------------------------
    // Weight
    //   Body and part weight, what the legs carry and the wings lift.
    // ---------------------------------------------------------------------------

    /// <summary>Body plus parts, kg in design scale (the clay blob weighs about 10).</summary>
    public float Weight { get; private set; }

    /// <summary>Weight all legs together carry on land, 0 without legs.</summary>
    public float Carry { get; private set; }

    /// <summary>Weight all wings together lift, 0 without wings.</summary>
    public float Lift { get; private set; }

    /// <summary>Speed on land as share of the swim speed of the body size.</summary>
    public float LandSpeed { get; private set; } = CRAWL_SPEED;

    /// <summary>Weight per carrying capacity, above 1 the legs struggle. Infinite without legs.</summary>
    public float Load => Carry > 0f ? Weight / Carry : float.PositiveInfinity;

    /// <summary>Lift per weight, from 1 on the body could fly.</summary>
    public float FlightRatio => Weight > 0f ? Lift / Weight : 0f;

    public bool CanFly => FlightRatio >= 1f;

    // ===========================================================================
    // ===============================   COMPUTE   ===============================
    // ===========================================================================
    //
    //   1. Body    Type, girth and length set speed and health, the volume the weight.
    //   2. Parts   Every part adds its weight and its effect.
    //   3. Land    Legs carry the weight: walking speed from stride and load.
    //

    public const float CRAWL_SPEED = 0.3f;           // share of the swim speed without legs on land
    public const float WALK_SPEED = 0.85f;           // normal legs at load 1
    private const float BODY_DENSITY = 7.5f;         // kg per unit volume, the blob weighs about 10
    private const int BASE_VERTEBRAE = 5;
    private const float MIN_LOAD_FACTOR = 0.4f;      // overloaded legs still drag the body along
    private const float MAX_LOAD_FACTOR = 1.2f;      // strong legs under a light body
    private const float WING_FLAP_BONUS = 0.08f;     // per pair, flapping helps a little on land
    private const float LIMB_DRAG = 0.95f;           // legs and wings slow the swimming per pair
    private const float LIMB_CARRY = 10f;            // a sculpted limb pair that reaches the ground, weaker than ready-made legs
    private const float LIMB_STRIDE = 0.9f;
    private const float LIMB_REACH = 0.2f;           // a sculpted limb pair held up grabs food like a short arm

    /*
      Traits multiplied together:
      - Type:      fish +10% speed, marine mammal +30% health
      - Girth:     thick bodies are tougher and slower (mean girth 0.25 .. 1)
      - Length:    every vertebra above five +3% speed and +5% health
      - Weight:    body volume times BODY_DENSITY plus every part
      - Parts:     see the switch, one line per part, a single half of a pair counts half;
                   a sculpted limb whose tip reaches the ground is a leg (CreatureDesign.IsWalkingLimb)
      - On land:   without legs CRAWL_SPEED, otherwise
                   WALK_SPEED * stride * clamp(1.5 - 0.5 * load) * (1 + 0.08 per wing pair)
                   stride = mean of the leg kinds, weighted by what they carry
    */
    /// <summary>Computes every trait of a body plan.</summary>
    /// <param name="design">(Required) The body plan, not changed.</param>
    /// <returns>The traits, relative to the base values of the Player.</returns>
    /// <example><code>CreatureStats.From(CreatureDesign.Blob()).Weight   // about 10</code></example>
    public static CreatureStats From(CreatureDesign design)
    {
        var stats = new CreatureStats();

        // ---------------------------------------------------------------------------
        // Body
        //   Type, girth and length set speed and health, the volume the weight.
        // ---------------------------------------------------------------------------

        float girth = design.MeanGirth;
        int   extra = design.Girths.Count - BASE_VERTEBRAE;
        stats.Health    = (CreatureType.MarineMammal == design.Type ? 1.3f : 1f) * (0.7f + 0.6f * girth) * (1f + 0.05f * extra);
        stats.SwimSpeed = (CreatureType.Fish == design.Type ? 1.1f : 1f) * (1.15f - 0.3f * girth) * (1f + 0.03f * extra);
        stats.Weight    = design.Volume() * BODY_DENSITY;

        // ---------------------------------------------------------------------------
        // Parts
        //   Every part adds its weight and its effect.
        // ---------------------------------------------------------------------------

        float strideCarry = 0f;                      // stride times carry, for the weighted mean
        float wingPairs   = 0f;
        foreach (CreaturePart part in design.Parts)
        {
            // A single half of a pair (symmetry off) weighs and works half: added values halve,
            // multipliers count with their square root (two halves multiply to the pair)
            CreaturePartInfo info  = part.Info;
            float            share = part.Share;
            float Scaled(float multiplier) => Mathf.Pow(multiplier, share);
            float carry = info.IsLeg && design.IsWalkingLimb(part) ? info.Carry : 0f;   // a foot held up carries nothing
            stats.Weight += info.Weight * share;
            stats.Carry  += carry * share;
            stats.Lift   += info.Lift * share;
            strideCarry  += info.Stride * carry * share;
            switch (part.Kind)
            {
                // Fins
                case CreaturePartKind.CaudalFin:
                    stats.SwimSpeed *= 1.3f;
                    break;
                case CreaturePartKind.Fluke:
                    stats.SwimSpeed    *= 1.25f;
                    stats.Acceleration *= 1.2f;
                    break;
                case CreaturePartKind.DorsalFin:
                    stats.Acceleration *= 1.25f;
                    break;
                case CreaturePartKind.PectoralFin:
                    stats.Acceleration *= Scaled(1.3f);
                    break;
                // Weapons: the jaw bites forward, spines hurt at both flanks and the back
                case CreaturePartKind.Jaw:
                    stats._jawAngles.Add(0f);
                    stats.DigestMeat *= 1.2f;
                    break;
                case CreaturePartKind.Spines:
                    stats._spikeAngles.Add(Mathf.Pi * 0.5f);
                    stats._spikeAngles.Add(-Mathf.Pi * 0.5f);
                    stats._spikeAngles.Add(Mathf.Pi);
                    break;
                // Eyes: food is pulled in from farther away
                case CreaturePartKind.PitEye:
                    stats.MagnetBonus += 0.3f * share;
                    break;
                case CreaturePartKind.Eye:
                    stats.MagnetBonus += 0.5f * share;
                    break;
                case CreaturePartKind.StalkEye:
                    stats.MagnetBonus += 0.8f * share;
                    break;
                // Sculpted limb: reaching the ground it is a leg (carries, LAND below), else an arm that grabs
                case CreaturePartKind.StubLimb:
                case CreaturePartKind.BentLimb:
                case CreaturePartKind.Limb:
                case CreaturePartKind.Tentacle:
                    stats.SwimSpeed *= Scaled(LIMB_DRAG);
                    if (design.IsWalkingLimb(part))
                    {
                        stats.Carry += LIMB_CARRY * share;
                        strideCarry += LIMB_STRIDE * LIMB_CARRY * share;
                    }
                    else
                    {
                        stats.MagnetBonus += LIMB_REACH * share;
                    }
                    break;
                // Feet: carry on land (LAND below), drag a little in the water
                case CreaturePartKind.Legs:
                case CreaturePartKind.StubbyLegs:
                case CreaturePartKind.ClawLegs:
                    stats.SwimSpeed *= Scaled(LIMB_DRAG);
                    break;
                // Hands grab food, claws scratch at both front flanks (a single claw at its own flank,
                // +Z is +Y on the plane, see Plane3D)
                case CreaturePartKind.Hands:
                    stats.MagnetBonus += 0.4f * share;
                    break;
                case CreaturePartKind.Claws:
                    stats.MagnetBonus += 0.15f * share;
                    if (part.Side >= 0) stats._spikeAngles.Add(0.5f);
                    if (part.Side <= 0) stats._spikeAngles.Add(-0.5f);
                    break;
                // Brain: more evolution points per level, the big one also steers better
                case CreaturePartKind.SmallBrain:
                    stats.EpPerLevelBonus += 1;
                    break;
                case CreaturePartKind.BigBrain:
                    stats.EpPerLevelBonus += 2;
                    stats.Acceleration    *= 1.15f;
                    break;
                // Lungs: air on land (CreatureDesign.BreathesAir), more air means more health
                case CreaturePartKind.Lung:
                    break;
                case CreaturePartKind.Blowhole:
                    stats.Health *= 1.25f;
                    break;
                case CreaturePartKind.BigLung:
                    stats.Health *= 1.2f;
                    break;
                // Wings: lift for later, a little flapping help on land, drag in the water
                case CreaturePartKind.MembraneWings:
                case CreaturePartKind.FeatherWings:
                    stats.SwimSpeed *= Scaled(LIMB_DRAG);
                    wingPairs       += share;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(design), $"design must hold known CreaturePartKinds, got '{part.Kind}'.");
            }
        }

        // ---------------------------------------------------------------------------
        // Land
        //   Legs carry the weight: walking speed from stride and load.
        // ---------------------------------------------------------------------------

        if (stats.Carry <= 0f)
        {
            stats.LandSpeed = CRAWL_SPEED;
            return stats;
        }
        float stride     = strideCarry / stats.Carry;
        float loadFactor = Mathf.Clamp(1.5f - 0.5f * stats.Load, MIN_LOAD_FACTOR, MAX_LOAD_FACTOR);
        stats.LandSpeed  = WALK_SPEED * stride * loadFactor * (1f + WING_FLAP_BONUS * wingPairs);
        return stats;
    }
}
