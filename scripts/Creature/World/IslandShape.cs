// NOTE:
//   The outline of the islands of the coast (Creature/Coast.cs) and everything that
//   follows from it: how deep a point lies inside the nearest island, the way back into
//   the sea, the landing point and where a neighbor island may stand. Pure math on value
//   structs without any engine call, so it runs in a unit test.
//   The outline is the same formula as island_radius in shaders/ocean_floor.gdshaderinc.
//   All distances are world units.
//
//   float depth = IslandShape.LandDepth(islands, point);   // negative in the sea
//   Vector2 sea = IslandShape.ToSea(islands, point, margin);

//
//   Chapters:
//     OUTLINE        The radius of an island towards an angle.
//     LAND AND SEA   How deep a point is inside the islands and the way back into the sea.
//     PLACEMENT      The landing point of an island and the place of a neighbor island.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public static class IslandShape
{
    // ===========================================================================
    // ===============================   OUTLINE   ===============================
    // ===========================================================================
    //
    //   MAX_BULGE   Radius never exceeds the base radius of the island times this.
    //   Radius      Radius of an island towards an angle (circle with three soft bulges).
    //

    public const float MAX_BULGE = 1.25f;

    /// <summary>Radius of an island towards an angle, the same formula as island_radius in the shaders.</summary>
    /// <param name="island">(Required) The island, its base radius in world units.</param>
    /// <param name="angle">(Required) Direction from the island center in radians.</param>
    /// <returns>The distance from the center to the shore in that direction.</returns>
    /// <example><code>IslandShape.Radius(new Coast.Island(Vector2.Zero, 10f, 0f), 0f)  // 10</code></example>
    public static float Radius(Coast.Island island, float angle)
    {
        float s = island.Seed;
        return island.Radius * (1f
            + 0.12f * Mathf.Sin(2f * angle + s * 1.7f)
            + 0.08f * Mathf.Sin(3f * angle + s * 2.9f)
            + 0.05f * Mathf.Sin(5f * angle + s * 4.3f));
    }

    // ===========================================================================
    // ============================   LAND AND SEA   =============================
    // ===========================================================================
    //
    //   LandDepth   Signed distance of a plane point into the nearest island, negative in the sea.
    //   ToSea       A plane point pushed back into the sea, at least a margin from every shore.
    //

    /// <summary>Signed distance into the nearest island, negative in the sea.</summary>
    /// <param name="islands">(Required) All islands of the archipelago.</param>
    /// <param name="point">(Required) Plane point.</param>
    /// <returns>The depth in world units, float.MinValue without any island.</returns>
    /// <example><code>IslandShape.LandDepth(islands, islands[0].Center)  // about the island radius</code></example>
    public static float LandDepth(IReadOnlyList<Coast.Island> islands, Vector2 point)
    {
        float depth = float.MinValue;
        for (int i = 0; i < islands.Count; i++)
        {
            Coast.Island island = islands[i];
            Vector2      rel    = point - island.Center;
            depth = Mathf.Max(depth, Radius(island, rel.Angle()) - rel.Length());
        }
        return depth;
    }

    /// <summary>The point itself in the sea, otherwise moved straight out of the island it is in or near.</summary>
    /// <param name="islands">(Required) All islands of the archipelago, checked in their order.</param>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="margin">(Required) Distance to keep from the shore.</param>
    /// <returns>The point in the sea.</returns>
    /// <example><code>IslandShape.ToSea(islands, islands[0].Center, 1f)  // one unit off the east shore</code></example>
    public static Vector2 ToSea(IReadOnlyList<Coast.Island> islands, Vector2 point, float margin)
    {
        for (int i = 0; i < islands.Count; i++)
        {
            Coast.Island island = islands[i];
            Vector2      rel    = point - island.Center;
            float        radius = Radius(island, rel.Angle());
            if (radius - rel.Length() > -margin)
            {
                Vector2 outward = rel.LengthSquared() > 0.0001f ? rel.Normalized() : Vector2.Right;
                point = island.Center + outward * (Radius(island, outward.Angle()) + margin);
            }
        }
        return point;
    }

    // ===========================================================================
    // ==============================   PLACEMENT   ==============================
    // ===========================================================================
    //
    //   Landing          The point on the shore facing a viewer and the direction onto the island.
    //   NeighborCenter   Center of a new island next to an anchor island, a gap between the shores.
    //   IsFree           Whether a new island keeps its distance to the player and to every island.
    //

    /// <summary>The landing of an island: the shore point facing a viewer and the way from there onto the land.</summary>
    /// <param name="island">(Required) The island.</param>
    /// <param name="from">(Required) Where the landing is seen from, not the island center.</param>
    /// <returns>Origin on the shore and the unit direction Normal from the sea onto the island.</returns>
    /// <example><code>IslandShape.Landing(new Coast.Island(Vector2.Zero, 10f, 0f), new Vector2(-20f, 0f))  // ((-10, 0), (1, 0))</code></example>
    public static (Vector2 Origin, Vector2 Normal) Landing(Coast.Island island, Vector2 from)
    {
        Vector2 toFrom = (from - island.Center).Normalized();
        return (island.Center + toFrom * Radius(island, toFrom.Angle()), -toFrom);
    }

    /// <summary>Center of a new island next to an anchor: both largest bulges plus the gap apart.</summary>
    /// <param name="anchor">(Required) The island already placed.</param>
    /// <param name="radius">(Required) Base radius of the new island.</param>
    /// <param name="gap">(Required) Sea between the two shores at least.</param>
    /// <param name="angle">(Required) Direction from the anchor in radians.</param>
    /// <example><code>IslandShape.NeighborCenter(new Coast.Island(Vector2.Zero, 10f, 0f), 6f, 5f, 0f)  // (25, 0)</code></example>
    public static Vector2 NeighborCenter(Coast.Island anchor, float radius, float gap, float angle)
    {
        return anchor.Center + Vector2.FromAngle(angle) * ((anchor.Radius + radius) * MAX_BULGE + gap);
    }

    /// <summary>Whether a new island stays off the player and off every island, with its largest bulge.</summary>
    /// <param name="islands">(Required) The islands already placed.</param>
    /// <param name="center">(Required) Center of the new island.</param>
    /// <param name="radius">(Required) Base radius of the new island.</param>
    /// <param name="player">(Required) Plane position of the player, it stays in the sea.</param>
    /// <param name="minGap">(Required) Sea to keep between two shores and to the player.</param>
    /// <returns>True when the place is free.</returns>
    public static bool IsFree(IReadOnlyList<Coast.Island> islands, Vector2 center, float radius, Vector2 player, float minGap)
    {
        bool isFree = player.DistanceTo(center) > radius * MAX_BULGE + minGap;
        for (int i = 0; i < islands.Count; i++)
        {
            Coast.Island other = islands[i];
            isFree = isFree && other.Center.DistanceTo(center) >= (other.Radius + radius) * MAX_BULGE + minGap;
        }
        return isFree;
    }
}
