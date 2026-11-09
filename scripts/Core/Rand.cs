// NOTE:
//   Short random helpers on top of the global Godot generator (GD.Randomize in Main).
//
//   float radius = Rand.Range(0.4f, 0.8f) * playerSize;

//
//   Chapters:
//     RANDOM VALUES   Ranges, angles, chances and picks.
//

using Godot;

namespace CellOut;

public static class Rand
{
    // ===========================================================================
    // ============================   RANDOM VALUES   ============================
    // ===========================================================================
    //
    //   Range    A float between min and max.
    //   Angle    A full-turn angle in radians.
    //   Chance   True with the given probability.
    //   Index    An index below count.
    //

    /// <summary>A uniformly distributed float between min and max.</summary>
    /// <param name="min">(Required) Lower bound.</param>
    /// <param name="max">(Required) Upper bound.</param>
    public static float Range(float min, float max)
    {
        return Mathf.Lerp(min, max, GD.Randf());
    }

    /// <summary>A random angle between 0 and Tau.</summary>
    public static float Angle()
    {
        return GD.Randf() * Mathf.Tau;
    }

    /// <summary>True with the given probability.</summary>
    /// <param name="probability">(Required) Probability between 0 and 1.</param>
    public static bool Chance(float probability)
    {
        return GD.Randf() < probability;
    }

    /// <summary>A random index between 0 and count - 1.</summary>
    /// <param name="count">(Required) Number of options, must be positive.</param>
    public static int Index(int count)
    {
        if (count <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(count), $"count must be a positive number, got '{count}'.");
        }
        return (int)(GD.Randi() % (uint)count);
    }
}
