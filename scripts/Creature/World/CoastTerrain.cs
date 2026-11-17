// NOTE:
//   The terrain of the coast (Creature/Coast.cs): its islands, the size unit and the two
//   noise fields of the ground. Answers which side of the shore a point is on, how deep
//   inside and how high the ground is there. The math is in World/IslandShape.cs and
//   World/GroundProfile.cs, this class adds the state and the noise. No node: the Coast
//   creates it and hands it to its components (ground, props, forest), it works before
//   the coast enters the tree. Horizontal distances are world units, heights player sizes.
//
//   var terrain = new CoastTerrain { Unit = player.BodySize() };
//   terrain.AddIsland(new Coast.Island(center, radius, seed));
//   float height = terrain.GroundHeight(point) * player.BodySize();

//
//   Chapters:
//     SETTINGS       The size unit, the scale of nature on land and the islands.
//     LAND AND SEA   Which side a point is on, how deep inside, the ground height.
//     FOUNDATIONS    Flat round pads under buildings, the ground height levels on them.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public sealed class CoastTerrain
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Unit           (Required) Player size when the coast appeared, every band scales with it.
    //   NATURE_SCALE   Plants, rocks and trees on land against the body.
    //   MAX_ISLANDS    The most islands a coast holds.
    //   Islands        All islands, the first one is the landing island.
    //   AddIsland      Adds one island while there is room.
    //

    public float Unit { get; set; } = 14f;

    public const float NATURE_SCALE = 3f;            // plants, rocks and trees on land against the body: mouse to dog sized
    public const int MAX_ISLANDS = 8;                // the size of the islands array in ocean_floor.gdshaderinc

    private readonly List<Coast.Island> _islands = new();

    public IReadOnlyList<Coast.Island> Islands => _islands;

    /// <summary>Adds one island, at most MAX_ISLANDS.</summary>
    /// <param name="island">(Required) The island, center and radius in world units.</param>
    /// <returns>False when the coast is full and the island was dropped.</returns>
    public bool AddIsland(Coast.Island island)
    {
        if (_islands.Count >= MAX_ISLANDS)
        {
            return false;
        }
        _islands.Add(island);
        return true;
    }

    // ===========================================================================
    // ============================   LAND AND SEA   =============================
    // ===========================================================================
    //
    //   LandDepth      Signed distance of a plane point into the nearest island, negative in the sea.
    //   IsOnLand       Whether a plane point lies on land.
    //   ToSea          A plane point pushed back into the sea, at least a margin from every shore.
    //   GroundHeight   Ground height in player sizes: profile, rocky relief, dunes and the hill.
    //   SurfaceLift    How far the ground at a point lies above GROUND_HEIGHT, in world units.
    //

    private readonly FastNoiseLite _noise = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.12f, Seed = 7 };
    private readonly FastNoiseLite _hills = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.012f, Seed = 11 };

    /// <summary>Signed distance into the nearest island, negative in the sea.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <example><code>terrain.LandDepth(island.Center)  // about the island radius</code></example>
    public float LandDepth(Vector2 point)
    {
        return IslandShape.LandDepth(_islands, point);
    }

    /// <summary>Whether a plane point lies on land.</summary>
    /// <param name="point">(Required) Plane point.</param>
    public bool IsOnLand(Vector2 point)
    {
        return LandDepth(point) > 0f;
    }

    /// <summary>The point itself in the sea, otherwise moved straight out of the island it is in or near.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="margin">(Required) Distance to keep from the shore.</param>
    public Vector2 ToSea(Vector2 point, float margin)
    {
        return IslandShape.ToSea(_islands, point, margin);
    }

    /// <summary>Ground height in player sizes at a plane point, the formula is GroundProfile.Height, flattened on every foundation.</summary>
    /// <param name="point">(Required) Plane point.</param>
    public float GroundHeight(Vector2 point)
    {
        float   d      = LandDepth(point) / Unit;
        Vector2 q      = point / Unit;
        float   height = GroundProfile.Height(d, _noise.GetNoise2D(q.X, q.Y), _hills.GetNoise2D(q.X, q.Y));
        float   blend  = PAD_BLEND * Unit;
        foreach (Pad pad in _pads)
        {
            float distance = point.DistanceTo(pad.Center);
            if (distance < pad.Radius + blend)
            {
                height = GroundProfile.Flatten(height, distance, pad.Radius, blend, pad.Height);
            }
        }
        return height;
    }

    // ===========================================================================
    // =============================   FOUNDATIONS   =============================
    // ===========================================================================
    //
    //   Pad        A flat round foundation: plane center, radius in world units, height in player sizes.
    //   Pads       All foundations, the ground shader draws trampled earth on them.
    //   Revision   Counts up with every new foundation: ground, props and trees rebuild.
    //   AddPad     Levels the ground around a building at the height of its middle (or of a foundation it touches).
    //   HasPadAt   Whether a foundation lies at a point already.
    //   StylePad   Changes what the ground shows on a foundation.
    //   IsOnPad    Whether a point lies on a foundation: no props, no trees there.
    //

    public const int MAX_PADS = 48;                  // the size of the pads array in coast_ground.gdshader
    public const float PAD_BLEND = 4f;               // the slope from a foundation back to the land, in Unit

    /// <summary>What the ground shader draws on a foundation: trampled earth, the trodden ways of a village or the streets of a city.</summary>
    public enum PadStyle { Yard, Village, City }

    public readonly record struct Pad(Vector2 Center, float Radius, float Height, PadStyle Style = PadStyle.Yard);

    private readonly List<Pad> _pads = new();

    public IReadOnlyList<Pad> Pads => _pads;

    public int Revision { get; private set; }

    /// <summary>Whether a foundation lies at a point already (a save brought it back).</summary>
    public bool HasPadAt(Vector2 center)
    {
        return _pads.Exists(pad => pad.Center.DistanceSquaredTo(center) < 0.01f);
    }

    /// <summary>Levels the ground in a circle at the height of its middle, so a building stands flat on a slope.</summary>
    /// <param name="center">(Required) Plane point of the middle, on land.</param>
    /// <param name="radius">(Required) Radius of the flat part in world units.</param>
    /// <returns>False when the coast holds MAX_PADS already or the point is not on land: the ground stays as it is.</returns>
    /// <example><code>terrain.AddPad(hut.Position, coast.Unit * 6f);</code></example>
    public bool AddPad(Vector2 center, float radius)
    {
        if (_pads.Count >= MAX_PADS || !IsOnLand(center))
        {
            return false;
        }
        // Overlapping an older foundation: the same height, so a village becomes one terrace without walls between
        float height = GroundHeight(center);
        float nearest = float.MaxValue;
        foreach (Pad pad in _pads)
        {
            float distance = center.DistanceTo(pad.Center);
            if (distance < pad.Radius + radius + PAD_BLEND * Unit && distance < nearest)
            {
                nearest = distance;
                height  = pad.Height;
            }
        }
        _pads.Add(new Pad(center, radius, height));
        Revision++;
        return true;
    }

    /// <summary>Changes what the ground shows on the foundation at a point (the ways of a settlement, the streets of a city).</summary>
    /// <param name="center">(Required) Plane point of the middle of the foundation.</param>
    /// <param name="style">(Required) Its new look.</param>
    /// <returns>True when a foundation lies there and its look changed.</returns>
    public bool StylePad(Vector2 center, PadStyle style)
    {
        int index = _pads.FindIndex(pad => pad.Center.DistanceSquaredTo(center) < 0.01f);
        if (index < 0 || style == _pads[index].Style)
        {
            return false;
        }
        _pads[index] = _pads[index] with { Style = style };
        Revision++;
        return true;
    }

    /// <summary>Whether a plane point lies on a foundation, or within margin of its flat part.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="margin">(Required) Extra distance in world units, e.g. the crown of a tree.</param>
    public bool IsOnPad(Vector2 point, float margin)
    {
        foreach (Pad pad in _pads)
        {
            if (point.DistanceTo(pad.Center) < pad.Radius + margin)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Ground above GROUND_HEIGHT in world units (the hill), 0 in the sea and on the beach.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="size">(Required) Player.BodySize(), the ground scales with it.</param>
    public float SurfaceLift(Vector2 point, float size)
    {
        return GroundProfile.Lift(GroundHeight(point), size);
    }
}
