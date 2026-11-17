// NOTE:
//   Every star system of the Milky Way of the menu: the playable ones at the galaxy spots
//   of data/planets.json (out on the arms), CORE_SYSTEMS of old stars packed around the bar,
//   and WILD_SYSTEMS more without life, most of them strung along the
//   spiral arms of shaders/galaxy.gdshader (the same arm formula: the ridge of an arm runs
//   where theta = ARM_WIND * ln(r) - offset + k * pi), the rest scattered over the disk.
//   Always the same galaxy: seeded by a fixed number, every system by its key. Pure logic,
//   no engine. Drawn by Game/Ui/Menu/GalaxySystemsLayer.cs.
//
//   foreach (StarSystem system in Galaxy.Systems) { ... system.Spot ... }

//
//   Chapters:
//     SYSTEMS   The playable systems, then the wild ones on the arms and in the disk.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public static class Galaxy
{
    // ===========================================================================
    // ===============================   SYSTEMS   ===============================
    // ===========================================================================
    //
    //   ARM_WIND, BAR_ANGLE   The spiral of the shader.
    //   WILD_SYSTEMS          Systems without life.
    //   Systems               Every system, the playable ones first, built once.
    //   InCore                A point in the bulge along the bar.
    //   OnArm                 A point near the ridge of an arm.
    //

    public const float ARM_WIND = 3.1f;              // shaders/galaxy.gdshader ARM_WIND
    public const float BAR_ANGLE = 0.62f;            // shaders/galaxy.gdshader BAR_ANGLE
    public const int WILD_SYSTEMS = 150;
    public const int CORE_SYSTEMS = 28;              // old stars packed around the bar, Sgr A* keeps a little room
    private const float CORE_GAP = 0.032f;
    private const float CORE_HOLE = 0.06f;           // no system this close to Sgr A*
    private const float ARM_SHARE = 0.75f;           // share of the wild systems on the arms
    private const float MIN_GAP = 0.045f;            // disk radii between two systems
    private const int GALAXY_SEED = 1977;

    private static IReadOnlyList<StarSystem> _systems;

    public static IReadOnlyList<StarSystem> Systems => _systems ??= Build();

    private static IReadOnlyList<StarSystem> Build()
    {
        var systems = Planets.All.Select(StarSystem.Of).ToList();
        var dice    = new Random(GALAXY_SEED);
        for (int attempt = 0; attempt < CORE_SYSTEMS * 20 && systems.Count < Planets.All.Count + CORE_SYSTEMS; attempt++)
        {
            Vector2 spot = InCore(dice);
            if (spot.Length() < CORE_HOLE || systems.Any(system => system.Spot.DistanceTo(spot) < CORE_GAP))
            {
                continue;
            }
            systems.Add(StarSystem.Wild($"S-{systems.Count:000}", spot, isOld: true));
        }
        int target = systems.Count + WILD_SYSTEMS;
        for (int attempt = 0; attempt < WILD_SYSTEMS * 8 && systems.Count < target; attempt++)
        {
            Vector2 spot = dice.NextDouble() < ARM_SHARE ? OnArm(dice) : InDisk(dice);
            if (systems.Any(system => system.Spot.DistanceTo(spot) < MIN_GAP))
            {
                continue;
            }
            systems.Add(StarSystem.Wild($"S-{systems.Count:000}", spot));
        }
        return systems;
    }

    /// <summary>A point near the ridge of one of the four arms (two main ones, two side ones a quarter turn on).</summary>
    private static Vector2 OnArm(Random dice)
    {
        float r      = 0.26f + (float)Math.Pow(dice.NextDouble(), 0.8) * 0.8f;
        float offset = BAR_ANGLE + (dice.NextDouble() < 0.65 ? 0f : Mathf.Pi * 0.5f);
        float theta  = ARM_WIND * Mathf.Log(r) - offset + (dice.Next(2) * Mathf.Pi) + ((float)dice.NextDouble() - 0.5f) * 0.22f;
        r += ((float)dice.NextDouble() - 0.5f) * 0.04f;
        return Vector2.FromAngle(theta) * r;
    }

    /// <summary>A point in the bulge, stretched along the bar.</summary>
    private static Vector2 InCore(Random dice)
    {
        float r     = (float)Math.Sqrt(dice.NextDouble()) * 0.3f;
        float angle = (float)(dice.NextDouble() * Math.PI * 2.0);
        var   along = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.45f);
        return along.Rotated(BAR_ANGLE);
    }

    /// <summary>A point anywhere on the disk outside the bulge.</summary>
    private static Vector2 InDisk(Random dice)
    {
        float r = 0.2f + (float)Math.Sqrt(dice.NextDouble()) * 0.85f;
        return Vector2.FromAngle((float)(dice.NextDouble() * Math.PI * 2.0)) * r;
    }
}
