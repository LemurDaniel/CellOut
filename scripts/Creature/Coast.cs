// NOTE:
//   The coast of the organism stage: an archipelago of small islands in the open sea
//   (the logic: a signed distance into the nearest island), the first one ahead of the
//   player, the others scattered around it. This class is the coordinator: it scatters
//   the islands, answers the land checks and keeps its parts (Creature/World/) around
//   the player:
//     - World/IslandShape.cs, GroundProfile.cs: the outline and height math (engine-free)
//     - World/CoastTerrain.cs: the islands, land depth and ground height
//     - World/CoastSurface.cs: one ground mesh per island and the sea surface
//     - World/CoastProps.cs: seaweed, rocks, pebbles, grass and flowers, seeded per grid cell
//     - World/TreeGrid.cs, CoastForest.cs: seeded primeval trees, drawn in chunks
//   The island outline is the same formula in C# (IslandShape.Radius) and in the shaders
//   (ocean_floor.gdshaderinc, island_radius). Horizontal distances are world units (the
//   islands stay put), heights are player sizes (Follow rescales them).
//
//   var coast = Coast.Scatter(player.GlobalPosition, direction, shoreDistance, player.BodySize(), seed);
//   AddChild(coast);
//   coast.Follow(player.GlobalPosition, player.BodySize(), viewRadius, drawReach, openSea);   // every frame

//
//   Chapters:
//     SETTINGS          The unit, the landing point and the shared heights and scales.
//     ARCHIPELAGO       The islands and the scattering of the archipelago.
//     LAND AND SEA      Which side a point is on, how deep inside, the ground height.
//     TREES             The seeded trees of a grid cell, trunks block the way.
//     BUILD             Ground, water, props and forest as components in world space.
//     FOLLOW            Keeps ground, water, props and trees around the player.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Coast : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Unit     (Required) Player size when the coast appeared, every band scales with it.
    //   Origin   Landing point on the shore of the first island, facing the player.
    //   Normal   Direction from the sea onto the first island at Origin.
    //
    //   The ground profile (heights per land depth) is in World/GroundProfile.cs.
    //

    private readonly CoastTerrain _terrain = new();

    public float Unit
    {
        get => _terrain.Unit;
        set => _terrain.Unit = value;
    }
    public Vector2 Origin { get; private set; }
    public Vector2 Normal { get; private set; } = Vector2.Right;

    public const float GROUND_HEIGHT = GroundProfile.GROUND_HEIGHT;       // dry land below the body center, in player sizes
    public const float NATURE_SCALE = CoastTerrain.NATURE_SCALE;          // plants, rocks and trees on land against the body: mouse to dog sized
    public const int MAX_ISLANDS = CoastTerrain.MAX_ISLANDS;              // the size of the islands array in ocean_floor.gdshaderinc
    public const float OPEN_SEA_HEIGHT = CoastSurface.OPEN_SEA_HEIGHT;    // third person: the sea meets the beach (ground -0.6 at the shore)

    // ===========================================================================
    // =============================   ARCHIPELAGO   =============================
    // ===========================================================================
    //
    //   Island          Center on the plane, base radius in world units, seed of the bulges.
    //   Islands         All islands, the first one is the landing island.
    //   IslandRadius    Radius of an island towards an angle (circle with three soft bulges).
    //   AddIsland       Adds one island, before the coast enters the tree.
    //   Scatter         The archipelago: a landing island ahead of the player, more around it.
    //

    public readonly record struct Island(Vector2 Center, float Radius, float Seed);

    private const int ISLAND_COUNT = 7;
    private const float START_RADIUS = 130f;         // landing island, in Unit: room for a tribe
    private const float MIN_RADIUS = 50f;            // the others, in Unit
    private const float MAX_RADIUS = 110f;
    private const float MIN_GAP = 30f;               // sea between two shores at least, in Unit
    private const float MAX_GAP = 90f;

    public IReadOnlyList<Island> Islands => _terrain.Islands;

    /// <summary>Radius of an island towards an angle, the same formula as island_radius in the shaders.</summary>
    /// <example><code>Coast.IslandRadius(new Coast.Island(Vector2.Zero, 10f, 0f), 0f)  // 10</code></example>
    public static float IslandRadius(Island island, float angle)
    {
        return IslandShape.Radius(island, angle);
    }

    /// <summary>Adds one island, the first one sets the landing point (Origin, Normal) towards the given point.</summary>
    /// <param name="island">(Required) The island, center and radius in world units.</param>
    /// <param name="from">(Optional) Where the landing is seen from. Defaults to the west of the island.</param>
    public void AddIsland(Island island, Vector2? from = null)
    {
        if (!_terrain.AddIsland(island))
        {
            Log.Verbose("Coast", $"Island at {island.Center} dropped, at most {MAX_ISLANDS} islands.");
            return;
        }
        if (1 == _terrain.Islands.Count)
        {
            (Origin, Normal) = IslandShape.Landing(island, from ?? island.Center + Vector2.Left);
        }
    }

    /*
      1. Landing island   START_RADIUS, its shore shoreDistance ahead of the player.
      2. Neighbors        Next to a random island already placed, a gap of MIN_GAP..MAX_GAP
                          between the shores (with the largest bulge), never on top of
                          another island or of the player. Gives up after a few tries.
    */
    /// <summary>A new archipelago ahead of the player.</summary>
    /// <param name="player">(Required) Plane position of the player, it stays in the sea.</param>
    /// <param name="direction">(Required) Unit direction towards the landing island.</param>
    /// <param name="shoreDistance">(Required) Distance from the player to the first shore.</param>
    /// <param name="unit">(Required) Player size, every distance scales with it.</param>
    /// <param name="seed">(Required) Seed of the layout.</param>
    /// <returns>The coast, not yet in the tree.</returns>
    public static Coast Scatter(Vector2 player, Vector2 direction, float shoreDistance, float unit, ulong seed)
    {
        var coast = new Coast { Unit = unit };
        var rng   = new RandomNumberGenerator { Seed = seed };

        // Landing island
        var   start      = new Island(Vector2.Zero, START_RADIUS * unit, rng.Randf() * 100f);
        float startReach = IslandShape.Radius(start, (-direction).Angle());
        coast.AddIsland(start with { Center = player + direction * (shoreDistance + startReach) }, player);

        // Neighbors
        for (int attempt = 0; attempt < 200 && coast.Islands.Count < ISLAND_COUNT; attempt++)
        {
            Island  anchor = coast.Islands[rng.RandiRange(0, coast.Islands.Count - 1)];
            float   radius = rng.RandfRange(MIN_RADIUS, MAX_RADIUS) * unit;
            float   gap    = rng.RandfRange(MIN_GAP, MAX_GAP) * unit;
            Vector2 center = IslandShape.NeighborCenter(anchor, radius, gap, rng.Randf() * Mathf.Tau);
            if (IslandShape.IsFree(coast.Islands, center, radius, player, MIN_GAP * unit))
            {
                coast.AddIsland(new Island(center, radius, rng.Randf() * 100f));
            }
        }
        Log.Verbose("Coast", $"Archipelago of {coast.Islands.Count} islands, landing at {coast.Origin}.");
        return coast;
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
    //   All of them delegate to World/CoastTerrain.cs.
    //

    /// <summary>Signed distance into the nearest island, negative in the sea.</summary>
    /// <example><code>coast.LandDepth(island.Center)  // about the island radius</code></example>
    public float LandDepth(Vector2 point)
    {
        return _terrain.LandDepth(point);
    }

    public bool IsOnLand(Vector2 point)
    {
        return _terrain.IsOnLand(point);
    }

    /// <summary>The point itself in the sea, otherwise moved straight out of the island it is in or near.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="margin">(Required) Distance to keep from the shore.</param>
    public Vector2 ToSea(Vector2 point, float margin)
    {
        return _terrain.ToSea(point, margin);
    }

    /// <summary>Ground height in player sizes at a plane point.</summary>
    /// <param name="point">(Required) Plane point.</param>
    public float GroundHeight(Vector2 point)
    {
        return _terrain.GroundHeight(point);
    }

    /// <summary>Ground above GROUND_HEIGHT in world units (the hill), 0 in the sea and on the beach.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="size">(Required) Player.BodySize(), the ground scales with it.</param>
    public float SurfaceLift(Vector2 point, float size)
    {
        return _terrain.SurfaceLift(point, size);
    }

    // ===========================================================================
    // ================================   TREES   ================================
    // ===========================================================================
    //
    //   Tree             One tree: plane position, species, height and yaw, tint.
    //   Coast            Creates the tree grid on the terrain, so the trees answer before _Ready.
    //   TreesIn        The trees of one grid cell, the same every time (seeded by the cell).
    //   PushOutOfTrees   A point moved out of every trunk it overlaps.
    //
    //   Both delegate to World/TreeGrid.cs, heights and bands of the trees are described there.
    //

    public readonly record struct Tree(Vector2 Position, TreeSpecies Species, float Height, float Yaw, Color Tint)
    {
        public float TrunkRadius => TreeModels.TRUNK_SHARE[(int)Species] * Height;
    }

    private readonly TreeGrid _trees;

    public Coast()
    {
        _trees = new TreeGrid(_terrain);
    }

    /// <summary>The trees of one grid cell: none, one or two.</summary>
    /// <param name="cell">(Required) Grid cell of TreeGrid.TREE_CELL Units.</param>
    public List<Tree> TreesIn(Vector2I cell)
    {
        return _trees.TreesIn(cell);
    }

    /// <summary>A point pushed out of every trunk it overlaps, e.g. the walking player.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="radius">(Required) Radius of the body at the point.</param>
    /// <returns>The point itself when it touches no trunk.</returns>
    public Vector2 PushOutOfTrees(Vector2 point, float radius)
    {
        return _trees.PushOutOfTrees(point, radius);
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready   Ground and water, the props and the forest as components, top level in world space.
    //

    private CoastSurface _surface;
    private CoastProps _props;
    private CoastForest _forest;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        TopLevel    = true;

        // Ground and water: one mesh per island, one sea surface around the player
        _surface = new CoastSurface();
        _surface.Setup(_terrain);
        AddChild(_surface);

        // Props in world space: seeded per grid cell
        _props = new CoastProps();
        _props.Setup(_terrain);
        AddChild(_props);

        // Trees: chunks of the seeded trees
        _forest = new CoastForest();
        _forest.Setup(_terrain, _trees);
        AddChild(_forest);
        Log.Verbose("Coast", $"{Islands.Count} islands, landing at {Origin} towards {Normal}, unit {Unit:F1}.");
    }

    // ===========================================================================
    // ===============================   FOLLOW   ================================
    // ===========================================================================
    //
    //   1. Ground and water   Rescales the heights with the player, the water centered on the
    //                         player as far as the camera draws, open sea in third person.
    //   2. Props              Refilled when the player moved a few Units or grew noticeably.
    //   3. Trees              Chunks around the player come and go, all rebuild when the player grew.
    //

    /// <summary>Keeps ground, water and props around the player, called every frame.</summary>
    /// <param name="playerPosition">(Required) Plane position of the player.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), heights scale with it.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, the same the Backdrop gets.</param>
    /// <param name="drawReach">(Optional) How far the water reaches, e.g. the camera far plane. Defaults to 2 view radii.</param>
    /// <param name="openSea">(Optional) 0 top view tint .. 1 open sea of the third person view. Defaults to 0.</param>
    public void Follow(Vector2 playerPosition, float playerSize, float viewRadius, float drawReach = 0f, float openSea = 0f)
    {
        // ---------------------------------------------------------------------------
        // Ground and water
        //   Rescales the heights with the player, the water centered on the
        //   player as far as the camera draws, open sea in third person.
        // ---------------------------------------------------------------------------

        _surface.Follow(playerPosition, playerSize, viewRadius, drawReach, openSea);

        // ---------------------------------------------------------------------------
        // Props
        //   Refilled when the player moved a few Units or grew noticeably.
        // ---------------------------------------------------------------------------

        _props.Follow(playerPosition, playerSize, viewRadius);

        // ---------------------------------------------------------------------------
        // Trees
        //   Chunks around the player come and go, all rebuild when the player grew.
        // ---------------------------------------------------------------------------

        _forest.Follow(playerPosition, playerSize);
    }
}
