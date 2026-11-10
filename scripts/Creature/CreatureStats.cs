// NOTE:
//   Traits of an organism computed from its body plan: weight, carrying capacity of the
//   legs, speed in the water and on land, turning, health, senses, weapons, lift of the
//   wings and the evolution point bonus of a brain. The one formula behind
//   Player.ApplyCreature and the stats column of the CreatureEditor, so the preview
//   always shows what the player gets. Multipliers are relative to the base values of
//   the Player (1 = unchanged). The numbers come from data/balance.json (Core/Balance.cs).
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
    public float LandSpeed { get; private set; } = Balance.Current.Creature.Land.CrawlSpeed;

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

    private const int BASE_VERTEBRAE = 5;           // body length the vertebra bonuses count from
    private const float CLAW_ANGLE = 0.5f;           // claws scratch this far off the nose, radians

    /*
      Traits multiplied together, every number from data/balance.json (creature.body,
      creature.land, creature.effects):
      - Type:      a fish swims faster, a marine mammal is tougher
      - Girth:     thick bodies are tougher and slower (mean girth 0.25 .. 1)
      - Length:    every vertebra above BASE_VERTEBRAE adds speed and health
      - Weight:    body volume times the density plus every part
      - Parts:     see the switch, one line per part, a single half of a pair counts half;
                   a sculpted limb whose tip reaches the ground is a leg (CreatureDesign.IsWalkingLimb)
      - On land:   without legs the crawl speed, otherwise
                   walk speed * stride * clamp(loadBase - loadSlope * load) * (1 + flap bonus per wing pair)
                   stride = mean of the leg kinds, weighted by what they carry
    */
    /// <summary>Computes every trait of a body plan.</summary>
    /// <param name="design">(Required) The body plan, not changed.</param>
    /// <returns>The traits, relative to the base values of the Player.</returns>
    /// <example><code>CreatureStats.From(CreatureDesign.Blob()).Weight   // about 10</code></example>
    public static CreatureStats From(CreatureDesign design)
    {
        var stats = new CreatureStats();
        CreatureBodyBalance    body    = Balance.Current.Creature.Body;
        CreatureLandBalance    land    = Balance.Current.Creature.Land;
        CreatureEffectsBalance effects = Balance.Current.Creature.Effects;

        // ---------------------------------------------------------------------------
        // Body
        //   Type, girth and length set speed and health, the volume the weight.
        // ---------------------------------------------------------------------------

        float girth = design.MeanGirth;
        int   extra = design.Girths.Count - BASE_VERTEBRAE;
        float typeHealth = CreatureType.MarineMammal == design.Type ? body.MammalHealth : 1f;
        float typeSwim   = CreatureType.Fish == design.Type ? body.FishSwimSpeed : 1f;
        stats.Health    = typeHealth * (body.HealthBase + body.HealthPerGirth * girth) * (1f + body.HealthPerVertebra * extra);
        stats.SwimSpeed = typeSwim * (body.SwimBase - body.SwimPerGirth * girth) * (1f + body.SwimPerVertebra * extra);
        stats.Weight    = design.Volume() * body.Density;

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
                    stats.SwimSpeed *= effects.CaudalFinSwim;
                    break;
                case CreaturePartKind.Fluke:
                    stats.SwimSpeed    *= effects.FlukeSwim;
                    stats.Acceleration *= effects.FlukeAcceleration;
                    break;
                case CreaturePartKind.DorsalFin:
                    stats.Acceleration *= effects.DorsalFinAcceleration;
                    break;
                case CreaturePartKind.PectoralFin:
                    stats.Acceleration *= Scaled(effects.PectoralFinAcceleration);
                    break;
                // Weapons: the jaw bites forward, spines hurt at both flanks and the back
                case CreaturePartKind.Jaw:
                    stats._jawAngles.Add(0f);
                    stats.DigestMeat *= effects.JawDigestMeat;
                    break;
                case CreaturePartKind.Spines:
                    stats._spikeAngles.Add(Mathf.Pi * 0.5f);
                    stats._spikeAngles.Add(-Mathf.Pi * 0.5f);
                    stats._spikeAngles.Add(Mathf.Pi);
                    break;
                // Eyes: food is pulled in from farther away
                case CreaturePartKind.PitEye:
                    stats.MagnetBonus += effects.PitEyeMagnet * share;
                    break;
                case CreaturePartKind.Eye:
                    stats.MagnetBonus += effects.EyeMagnet * share;
                    break;
                case CreaturePartKind.StalkEye:
                    stats.MagnetBonus += effects.StalkEyeMagnet * share;
                    break;
                // Sculpted limb: reaching the ground it is a leg (carries, LAND below), else an arm that grabs
                case CreaturePartKind.StubLimb:
                case CreaturePartKind.BentLimb:
                case CreaturePartKind.Limb:
                case CreaturePartKind.Tentacle:
                    stats.SwimSpeed *= Scaled(effects.LimbDrag);
                    if (design.IsWalkingLimb(part))
                    {
                        stats.Carry += effects.LimbCarry * share;
                        strideCarry += effects.LimbStride * effects.LimbCarry * share;
                    }
                    else
                    {
                        stats.MagnetBonus += effects.LimbReach * share;
                    }
                    break;
                // Feet: carry on land (LAND below), drag a little in the water
                case CreaturePartKind.Legs:
                case CreaturePartKind.StubbyLegs:
                case CreaturePartKind.ClawLegs:
                case CreaturePartKind.CrabLegs:
                    stats.SwimSpeed *= Scaled(effects.LimbDrag);
                    break;
                // Hands grab food, claws scratch at both front flanks (a single claw at its own flank,
                // +Z is +Y on the plane, see Plane3D)
                case CreaturePartKind.Hands:
                    stats.MagnetBonus += effects.HandsMagnet * share;
                    break;
                case CreaturePartKind.Claws:
                    stats.MagnetBonus += effects.ClawsMagnet * share;
                    if (part.Side >= 0) stats._spikeAngles.Add(CLAW_ANGLE);
                    if (part.Side <= 0) stats._spikeAngles.Add(-CLAW_ANGLE);
                    break;
                // Brain: more evolution points per level, the big one also steers better
                case CreaturePartKind.SmallBrain:
                    stats.EpPerLevelBonus += effects.SmallBrainEp;
                    break;
                case CreaturePartKind.BigBrain:
                    stats.EpPerLevelBonus += effects.BigBrainEp;
                    stats.Acceleration    *= effects.BigBrainAcceleration;
                    break;
                // Lungs: air on land (CreatureDesign.BreathesAir), more air means more health
                case CreaturePartKind.Lung:
                    break;
                case CreaturePartKind.Blowhole:
                    stats.Health *= effects.BlowholeHealth;
                    break;
                case CreaturePartKind.BigLung:
                    stats.Health *= effects.BigLungHealth;
                    break;
                // Wings: lift for later, a little flapping help on land, drag in the water
                case CreaturePartKind.MembraneWings:
                case CreaturePartKind.FeatherWings:
                    stats.SwimSpeed *= Scaled(effects.LimbDrag);
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
            stats.LandSpeed = land.CrawlSpeed;
            return stats;
        }
        float stride     = strideCarry / stats.Carry;
        float loadFactor = Mathf.Clamp(land.LoadBase - land.LoadSlope * stats.Load, land.MinLoadFactor, land.MaxLoadFactor);
        stats.LandSpeed  = land.WalkSpeed * stride * loadFactor * (1f + land.WingFlapBonus * wingPairs);
        return stats;
    }
}
