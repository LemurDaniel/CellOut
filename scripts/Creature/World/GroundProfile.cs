// NOTE:
//   The height profile of the coast ground (Creature/Coast.cs): from the sea floor up the
//   slope to the beach, the dunes and the hills inland. Pure math without any engine
//   call, the two noise values come from the caller (World/CoastTerrain.cs), so it runs
//   in a unit test. d is the land depth in Unit (negative = sea), heights are player sizes.
//
//   float height = GroundProfile.Height(d, noise, hillNoise);
//   float lift   = GroundProfile.Lift(height, player.BodySize());

//
//   Chapters:
//     PROFILE   The profile points, the height at a land depth and the lift above dry land.
//

using Godot;

namespace CellOut;

public static class GroundProfile
{
    // ===========================================================================
    // ===============================   PROFILE   ===============================
    // ===========================================================================
    //
    //   GROUND_HEIGHT   Dry land below the body center.
    //   BaseHeight      The profile alone, linear between its points.
    //   Height          Profile plus rocky relief, dunes and the hills.
    //   Lift            How far a height lies above GROUND_HEIGHT, in world units.
    //
    //   Ground profile, d in Unit (negative = sea), height in player sizes:
    //     d -30 .. -14    -3 -> -2.9    sea floor of the Backdrop, rocky relief fades in
    //     d -14 .. -2     -2.9 -> -1    the slope up
    //     d -2 .. 0       -1 -> -0.6    shallow water
    //     d 0 .. 3        -0.6 -> -0.5  beach, then dunes and meadow at GROUND_HEIGHT
    //     d 8 .. 45       + up to HILL_HEIGHT: rolling hills and flat valleys inland
    //

    public const float GROUND_HEIGHT = -0.5f;        // dry land below the body center, in player sizes
    private const float HILL_HEIGHT = 7f;            // in player sizes, real hills for a small animal
    private static readonly float[] PROFILE_D = { -30f, -14f, -8f, -2f, 0f, 1f, 3f, 6f, 12f, 400f };
    private static readonly float[] PROFILE_H = { -3f, -2.9f, -2f, -1f, -0.6f, -0.55f, GROUND_HEIGHT, GROUND_HEIGHT, -0.45f, -0.45f };

    /// <summary>The profile height at a land depth, linear between the PROFILE points.</summary>
    /// <param name="d">(Required) Land depth in Unit, negative in the sea.</param>
    /// <returns>The height in player sizes, the last profile height beyond the last point.</returns>
    /// <example><code>GroundProfile.BaseHeight(0f)  // -0.6, the shore</code></example>
    public static float BaseHeight(float d)
    {
        for (int i = 0; i < PROFILE_D.Length - 1; i++)
        {
            if (d <= PROFILE_D[i + 1])
            {
                float t = Mathf.Clamp((d - PROFILE_D[i]) / (PROFILE_D[i + 1] - PROFILE_D[i]), 0f, 1f);
                return Mathf.Lerp(PROFILE_H[i], PROFILE_H[i + 1], t);
            }
        }
        return PROFILE_H[^1];
    }

    /*
      Height = profile (linear between the PROFILE points, d = LandDepth / Unit)
             + relief under water: up to 0.35 sizes, fading in from d -2 to -6 and out
               towards the Backdrop floor from d -22 to -30
             + dunes on land: up to 0.36 sizes, growing from d 3 to 8
             + rolling hills inland: 0..HILL_HEIGHT from broad noise (hills ~80 Unit apart,
               flat valleys between them), growing in from d 8 to 45
    */
    /// <summary>Ground height in player sizes at a land depth.</summary>
    /// <param name="d">(Required) Land depth in Unit, negative in the sea.</param>
    /// <param name="noise">(Required) Fine noise at the point, -1..1: relief, dunes and bumps.</param>
    /// <param name="hillNoise">(Required) Broad noise at the point, -1..1: where the hills stand.</param>
    /// <example><code>GroundProfile.Height(0f, 0f, 0f)  // -0.6, the shore</code></example>
    public static float Height(float d, float noise, float hillNoise)
    {
        float profile = BaseHeight(d);
        float relief  = Mathf.Clamp((-d - 2f) / 4f, 0f, 1f) * (1f - Mathf.Clamp((-d - 22f) / 8f, 0f, 1f));
        float dunes   = Mathf.Clamp((d - 3f) / 5f, 0f, 1f);
        float inland  = Mathf.SmoothStep(8f, 45f, d);
        float hills   = Mathf.SmoothStep(-0.2f, 0.8f, hillNoise);
        return profile + noise * (0.35f * relief + 0.36f * dunes + 0.5f * inland) + HILL_HEIGHT * hills * inland;
    }

    /// <summary>Ground above GROUND_HEIGHT in world units (the hill), 0 in the sea and on the beach.</summary>
    /// <param name="height">(Required) Ground height in player sizes, from Height.</param>
    /// <param name="size">(Required) Player.BodySize(), the ground scales with it.</param>
    /// <example><code>GroundProfile.Lift(1.5f, 2f)  // 4</code></example>
    public static float Lift(float height, float size)
    {
        return Mathf.Max(0f, height - GROUND_HEIGHT) * size;
    }
}
