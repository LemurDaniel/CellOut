// NOTE:
//   Rules of the giant cells in the background: how big they spawn, where they float
//   between the plane and the sea floor, how faint they are down there and when they
//   are small enough, compared with the growing player, to rise to the plane and take
//   part in the game. Pure logic without the engine, so DeepKeeper uses it and
//   tests/unit checks it. The numbers come from data/balance.json (critter.giants,
//   Core/Balance.cs).
//
//   float    size    = DeepRule.SpawnSize(player.BodySize(), GD.Randf());
//   DeepPose pose    = DeepRule.Placement(size, player.BodySize());
//   bool     isReady = DeepRule.IsReadyToSurface(size, player.BodySize());

//
//   Chapters:
//     TYPES                The pose of a giant.
//     FLOAT IN THE DEPTH   Spawn size, placement below the plane, surfacing and the rise.
//

using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   DeepPose   Depth, upright squash and fade of a giant.
//

/// <summary>Where and how a giant floats.</summary>
/// <param name="Depth">Depth of its center below the plane in world units, 0 on the plane.</param>
/// <param name="Squash">Upright squash, 1 is its own shape.</param>
/// <param name="Fade">0 fully visible .. 1 gone: deep giants are faint, part of the background.</param>
public readonly record struct DeepPose(float Depth, float Squash, float Fade);

public static class DeepRule
{
    private const float HALF_HEIGHT = 0.6f;      // half height of a cell in radii, CellVisual3D.FLATTEN
    private const float BAND_TOP = 0.35f;        // giants keep this far below the plane, in player sizes
    private const float BAND_BOTTOM = 2.75f;     // and above the sea floor (Backdrop, 3 player sizes down)
    public const float DEEP_FADE = 0.8f;         // a giant in the band is a faint shape in the water

    // ===========================================================================
    // =========================   FLOAT IN THE DEPTH   ==========================
    // ===========================================================================
    //
    //   SpawnSize          Body size of a new giant, MinSize to MaxSize player sizes.
    //   Placement          Depth, upright squash and fade, so it fits the band.
    //   IsReadyToSurface   The player grew enough: the giant is at most SurfaceRatio player sizes big.
    //   Rise               The pose on the way up, eased from the band to the plane.
    //   HasRisen           The rise is over.
    //

    private static GiantBalance Tune => Balance.Current.Critter.Giants;

    /// <summary>Body size of a new giant in the background.</summary>
    /// <param name="playerSize">(Required) Player.BodySize() at the spawn.</param>
    /// <param name="roll">(Required) Random share 0..1, 0 spawns the smallest giant.</param>
    /// <example><code>DeepRule.SpawnSize(10f, 0.5f)   // 60 with MinSize 4 and MaxSize 8</code></example>
    public static float SpawnSize(float playerSize, float roll)
    {
        return playerSize * Mathf.Lerp(Tune.MinSize, Tune.MaxSize, Mathf.Clamp(roll, 0f, 1f));
    }

    /*
      The band between plane and floor is BAND_BOTTOM - BAND_TOP player sizes thick:
      - A giant that fits keeps its shape, its top just below BAND_TOP.
      - A taller one is squashed upright until it fits, a flat disc in the depth.
      The player grows, the band grows with it: the giant regains its shape and its
      center rises on its own, long before it surfaces. Down there it stays faint.
    */
    /// <summary>Where a giant floats below the plane.</summary>
    /// <param name="giantSize">(Required) Body size of the giant.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), the band scales with it.</param>
    /// <example><code>DeepRule.Placement(60f, 10f)   // depth 15.5, squash 0.333: squashed to fit 24 units</code></example>
    public static DeepPose Placement(float giantSize, float playerSize)
    {
        float band       = (BAND_BOTTOM - BAND_TOP) * playerSize;
        float fullHeight = 2f * HALF_HEIGHT * giantSize;
        float squash     = Mathf.Min(1f, band / Mathf.Max(fullHeight, 0.001f));
        float depth      = BAND_TOP * playerSize + HALF_HEIGHT * giantSize * squash;
        return new DeepPose(depth, squash, DEEP_FADE);
    }

    /// <summary>The giant may rise to the plane: the player grew to a size that can meet it.</summary>
    /// <param name="giantSize">(Required) Body size of the giant.</param>
    /// <param name="playerSize">(Required) Player.BodySize().</param>
    /// <example><code>DeepRule.IsReadyToSurface(60f, 28f)   // true with SurfaceRatio 2.2</code></example>
    public static bool IsReadyToSurface(float giantSize, float playerSize)
    {
        return giantSize <= playerSize * Tune.SurfaceRatio;
    }

    /// <summary>The pose on the way up: eased from the placement in the band to the plane, fully visible.</summary>
    /// <param name="giantSize">(Required) Body size of the giant.</param>
    /// <param name="playerSize">(Required) Player.BodySize().</param>
    /// <param name="elapsed">(Required) Seconds since the rise began, RiseSeconds reach the plane.</param>
    /// <example><code>DeepRule.Rise(60f, 28f, 5f)   // depth 0, squash 1, fade 0: on the plane in its own shape</code></example>
    public static DeepPose Rise(float giantSize, float playerSize, float elapsed)
    {
        DeepPose deep = Placement(giantSize, playerSize);
        float    t    = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(elapsed / Mathf.Max(Tune.RiseSeconds, 0.001f), 0f, 1f));
        return new DeepPose(Mathf.Lerp(deep.Depth, 0f, t), Mathf.Lerp(deep.Squash, 1f, t), Mathf.Lerp(deep.Fade, 0f, t));
    }

    /// <summary>The rise is over, the giant swims on the plane.</summary>
    /// <param name="elapsed">(Required) Seconds since the rise began.</param>
    public static bool HasRisen(float elapsed)
    {
        return elapsed >= Tune.RiseSeconds;
    }
}
