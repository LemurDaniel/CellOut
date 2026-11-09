// NOTE:
//   The coast of the organism stage: an archipelago of small islands in the open sea
//   (the logic: a signed distance into the nearest island), the first one ahead of the
//   player, the others scattered around it. Drawn with life on top of it:
//     - ground (shaders/coast_ground.gdshader): one round mesh per island, rocky sea
//       floor that fades into the Backdrop floor with depth, shallow sand with caustics,
//       beach with ripples and a wet band behind the swash, dunes, meadows on rolling hills
//     - water (shaders/coast_water.gdshader): a sea surface around the player with a
//       wobbling water line, rolling wave crests and foam at every shore; as open sea
//       (third person) blue with fresnel towards the horizon
//     - props: seaweed and rocks under water, pebbles, shells and driftwood on the
//       beach, grass, bushes and flowers on the meadow, seeded per grid cell
//     - trees: generated primeval trees (Core/TreeModels.cs) in forests with clearings,
//       cycads on the dunes, seeded per grid cell, in chunks around the player
//   The island outline is the same formula in C# (IslandRadius) and in the shaders
//   (ocean_floor.gdshaderinc, island_radius). Horizontal distances are world units (the
//   islands stay put), heights are player sizes (Follow rescales them).
//
//   var coast = Coast.Scatter(player.GlobalPosition, direction, shoreDistance, player.BodySize(), seed);
//   AddChild(coast);
//   coast.Follow(player.GlobalPosition, player.BodySize(), viewRadius, drawReach, openSea);   // every frame

//
//   Chapters:
//     SETTINGS          Islands, extents, the ground profile and the shaders.
//     ARCHIPELAGO       The island outline and the scattering of the islands.
//     LAND AND SEA      Which side a point is on, how deep inside, the ground height.
//     BUILD             One ground mesh per island, the water and the three prop multimeshes.
//     FOLLOW            Rescales the ground, moves the water, refills the props and trees.
//     PROPS             Seeded props per grid cell, by band.
//     TREES             Seeded primeval trees in forests and clearings, chunked, trunks block the way.
//

using System.Collections.Generic;
using System.Linq;
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
    //   Ground profile, d in Unit (negative = sea), height in player sizes:
    //     d -30 .. -14    -3 -> -2.9    sea floor of the Backdrop, rocky relief fades in
    //     d -14 .. -2     -2.9 -> -1    the slope up
    //     d -2 .. 0       -1 -> -0.6    shallow water
    //     d 0 .. 3        -0.6 -> -0.5  beach, then dunes and meadow at GROUND_HEIGHT
    //     d 8 .. 45       + up to HILL_HEIGHT: rolling hills and flat valleys inland
    //

    public float Unit { get; set; } = 14f;
    public Vector2 Origin { get; private set; }
    public Vector2 Normal { get; private set; } = Vector2.Right;

    public const float GROUND_HEIGHT = -0.5f;        // dry land below the body center, in player sizes
    public const float NATURE_SCALE = 3f;            // plants, rocks and trees on land against the body: mouse to dog sized
    public const int MAX_ISLANDS = 8;                // the size of the islands array in ocean_floor.gdshaderinc
    private const float WATER_HEIGHT = 0.7f;         // water surface above the body center, in player sizes
    public const float OPEN_SEA_HEIGHT = -0.58f;     // third person: the sea meets the beach (ground -0.6 at the shore)
    private const float HILL_HEIGHT = 7f;            // in player sizes, real hills for a small animal
    private const int SEGMENTS = 256;                // ground mesh columns around an island
    private static readonly float[] PROFILE_D = { -30f, -14f, -8f, -2f, 0f, 1f, 3f, 6f, 12f, 400f };
    private static readonly float[] PROFILE_H = { -3f, -2.9f, -2f, -1f, -0.6f, -0.55f, GROUND_HEIGHT, GROUND_HEIGHT, -0.45f, -0.45f };
    private static readonly Shader GROUND_SHADER = GD.Load<Shader>("res://shaders/coast_ground.gdshader");
    private static readonly Shader WATER_SHADER = GD.Load<Shader>("res://shaders/coast_water.gdshader");
    private static readonly Shader PLANT_SHADER = GD.Load<Shader>("res://shaders/coast_plant.gdshader");

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
    private const float MAX_BULGE = 1.25f;           // IslandRadius never exceeds Radius times this

    private readonly List<Island> _islands = new();

    public IReadOnlyList<Island> Islands => _islands;

    /// <summary>Radius of an island towards an angle, the same formula as island_radius in the shaders.</summary>
    /// <example><code>Coast.IslandRadius(new Coast.Island(Vector2.Zero, 10f, 0f), 0f)  // 10</code></example>
    public static float IslandRadius(Island island, float angle)
    {
        float s = island.Seed;
        return island.Radius * (1f
            + 0.12f * Mathf.Sin(2f * angle + s * 1.7f)
            + 0.08f * Mathf.Sin(3f * angle + s * 2.9f)
            + 0.05f * Mathf.Sin(5f * angle + s * 4.3f));
    }

    /// <summary>Adds one island, the first one sets the landing point (Origin, Normal) towards the given point.</summary>
    /// <param name="island">(Required) The island, center and radius in world units.</param>
    /// <param name="from">(Optional) Where the landing is seen from. Defaults to the west of the island.</param>
    public void AddIsland(Island island, Vector2? from = null)
    {
        if (_islands.Count >= MAX_ISLANDS)
        {
            Log.Verbose("Coast", $"Island at {island.Center} dropped, at most {MAX_ISLANDS} islands.");
            return;
        }
        _islands.Add(island);
        if (1 == _islands.Count)
        {
            Vector2 toFrom = ((from ?? island.Center + Vector2.Left) - island.Center).Normalized();
            Origin = island.Center + toFrom * IslandRadius(island, toFrom.Angle());
            Normal = -toFrom;
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
        float startReach = IslandRadius(start, (-direction).Angle());
        coast.AddIsland(start with { Center = player + direction * (shoreDistance + startReach) }, player);

        // Neighbors
        for (int attempt = 0; attempt < 200 && coast._islands.Count < ISLAND_COUNT; attempt++)
        {
            Island  anchor   = coast._islands[rng.RandiRange(0, coast._islands.Count - 1)];
            float   radius   = rng.RandfRange(MIN_RADIUS, MAX_RADIUS) * unit;
            float   gap      = rng.RandfRange(MIN_GAP, MAX_GAP) * unit;
            Vector2 center   = anchor.Center + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * ((anchor.Radius + radius) * MAX_BULGE + gap);
            bool    isFree   = player.DistanceTo(center) > radius * MAX_BULGE + MIN_GAP * unit;
            foreach (Island other in coast._islands)
            {
                isFree = isFree && other.Center.DistanceTo(center) >= (other.Radius + radius) * MAX_BULGE + MIN_GAP * unit;
            }
            if (isFree)
            {
                coast.AddIsland(new Island(center, radius, rng.Randf() * 100f));
            }
        }
        Log.Verbose("Coast", $"Archipelago of {coast._islands.Count} islands, landing at {coast.Origin}.");
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

    /// <summary>Signed distance into the nearest island, negative in the sea.</summary>
    /// <example><code>coast.LandDepth(island.Center)  // about the island radius</code></example>
    public float LandDepth(Vector2 point)
    {
        float depth = float.MinValue;
        foreach (Island island in _islands)
        {
            Vector2 rel = point - island.Center;
            depth = Mathf.Max(depth, IslandRadius(island, rel.Angle()) - rel.Length());
        }
        return depth;
    }

    public bool IsOnLand(Vector2 point)
    {
        return LandDepth(point) > 0f;
    }

    /// <summary>The point itself in the sea, otherwise moved straight out of the island it is in or near.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="margin">(Required) Distance to keep from the shore.</param>
    public Vector2 ToSea(Vector2 point, float margin)
    {
        foreach (Island island in _islands)
        {
            Vector2 rel    = point - island.Center;
            float   radius = IslandRadius(island, rel.Angle());
            if (radius - rel.Length() > -margin)
            {
                Vector2 outward = rel.LengthSquared() > 0.0001f ? rel.Normalized() : Vector2.Right;
                point = island.Center + outward * (IslandRadius(island, outward.Angle()) + margin);
            }
        }
        return point;
    }

    private readonly FastNoiseLite _noise = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.12f, Seed = 7 };
    private readonly FastNoiseLite _hills = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.012f, Seed = 11 };

    /*
      Height = profile (linear between the PROFILE points, d = LandDepth / Unit)
             + relief under water: up to 0.35 sizes, fading in from d -2 to -6 and out
               towards the Backdrop floor from d -22 to -30
             + dunes on land: up to 0.36 sizes, growing from d 3 to 8
             + rolling hills inland: 0..HILL_HEIGHT from broad noise (hills ~80 Unit apart,
               flat valleys between them), growing in from d 8 to 45
    */
    /// <summary>Ground height in player sizes at a plane point.</summary>
    /// <param name="point">(Required) Plane point.</param>
    public float GroundHeight(Vector2 point)
    {
        float d       = LandDepth(point) / Unit;
        float profile = PROFILE_H[^1];
        for (int i = 0; i < PROFILE_D.Length - 1; i++)
        {
            if (d <= PROFILE_D[i + 1])
            {
                float t = Mathf.Clamp((d - PROFILE_D[i]) / (PROFILE_D[i + 1] - PROFILE_D[i]), 0f, 1f);
                profile = Mathf.Lerp(PROFILE_H[i], PROFILE_H[i + 1], t);
                break;
            }
        }
        Vector2 q      = point / Unit;
        float   noise  = _noise.GetNoise2D(q.X, q.Y);
        float   relief = Mathf.Clamp((-d - 2f) / 4f, 0f, 1f) * (1f - Mathf.Clamp((-d - 22f) / 8f, 0f, 1f));
        float   dunes  = Mathf.Clamp((d - 3f) / 5f, 0f, 1f);
        float   inland = Mathf.SmoothStep(8f, 45f, d);
        float   hills  = Mathf.SmoothStep(-0.2f, 0.8f, _hills.GetNoise2D(q.X, q.Y));
        return profile + noise * (0.35f * relief + 0.36f * dunes + 0.5f * inland) + HILL_HEIGHT * hills * inland;
    }

    /// <summary>Ground above GROUND_HEIGHT in world units (the hill), 0 in the sea and on the beach.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="size">(Required) Player.BodySize(), the ground scales with it.</param>
    public float SurfaceLift(Vector2 point, float size)
    {
        return Mathf.Max(0f, GroundHeight(point) - GROUND_HEIGHT) * size;
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready         One ground mesh per island, the water and the props top level in world space.
    //   BuildGround    Rings around one island from the deep in to its middle.
    //   AddProps       One prop multimesh with the plant shader.
    //

    private const int MAX_PROPS = 16000;

    private readonly List<MeshInstance3D> _grounds = new();
    private ShaderMaterial _groundMaterial;
    private ShaderMaterial _waterMaterial;
    private MeshInstance3D _water;
    private MultiMesh _blades;
    private MultiMesh _lumps;
    private MultiMesh _sticks;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        TopLevel    = true;

        // Ground: one mesh per island, heights in player sizes (scaled in Follow)
        _groundMaterial = new ShaderMaterial { Shader = GROUND_SHADER };
        foreach (Island island in _islands)
        {
            var ground = new MeshInstance3D
            {
                Mesh             = BuildGround(island),
                MaterialOverride = _groundMaterial,
                Position         = Plane3D.To3D(island.Center),
                CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(ground);
            _grounds.Add(ground);
        }

        // Water: one subdivided plane around the player, so it bends with the planet
        _waterMaterial = new ShaderMaterial { Shader = WATER_SHADER };
        _water = new MeshInstance3D
        {
            Mesh             = new PlaneMesh { Size = Vector2.One, SubdivideWidth = 96, SubdivideDepth = 96 },
            MaterialOverride = _waterMaterial,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_water);

        // Islands for the shaders: xy center, z radius, w seed
        var islands = new Vector4[MAX_ISLANDS];
        for (int i = 0; i < _islands.Count; i++)
        {
            islands[i] = new Vector4(_islands[i].Center.X, _islands[i].Center.Y, _islands[i].Radius, _islands[i].Seed);
        }
        foreach (ShaderMaterial material in new[] { _groundMaterial, _waterMaterial })
        {
            material.SetShaderParameter("islands", islands);
            material.SetShaderParameter("island_count", _islands.Count);
            material.SetShaderParameter("unit", Unit);
        }

        // Props in world space: blades (cones) sway, lumps (spheres) and sticks (rods) do not
        _blades = AddProps(this, CellVisual3D.ConeMesh(), 0.12f);
        _lumps  = AddProps(this, CellVisual3D.SphereMesh(true), 0f);
        _sticks = AddProps(this, CellVisual3D.RodMesh(), 0f);
        Log.Verbose("Coast", $"{_islands.Count} islands, landing at {Origin} towards {Normal}, unit {Unit:F1}.");
    }

    /*
      Rings at a land depth d (in Unit), from the deep in to the middle: coarse in the deep,
      every 0.5 Unit around the shore, coarser on the meadow, then one center vertex. A ring
      vertex lies at IslandRadius(angle) - d from the center, so the rings follow the
      outline. Heights from GroundHeight (all islands), so overlapping skirts of two
      islands agree. Winding: inwards x around gives an upward normal (drawn two sided).
    */
    private ArrayMesh BuildGround(Island island)
    {
        float inner = island.Radius * (2f - MAX_BULGE) / Unit * 0.9f;
        var   rows  = new List<float>();
        for (float d = -30f; d < -16f; d += 2f) rows.Add(d);
        for (float d = -16f; d < Mathf.Min(16f, inner); d += 0.5f) rows.Add(d);
        for (float d = 16f; d < inner; d += 2f) rows.Add(d);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (float d in rows)
        {
            for (int j = 0; j < SEGMENTS; j++)
            {
                float   angle  = j * Mathf.Tau / SEGMENTS;
                Vector2 local  = Vector2.FromAngle(angle) * (IslandRadius(island, angle) - d * Unit);
                surface.AddVertex(Plane3D.To3D(local, GroundHeight(island.Center + local)));
            }
        }
        int center = rows.Count * SEGMENTS;
        surface.AddVertex(Plane3D.To3D(Vector2.Zero, GroundHeight(island.Center)));

        for (int i = 0; i < rows.Count; i++)
        {
            for (int j = 0; j < SEGMENTS; j++)
            {
                int a = i * SEGMENTS + j;
                int b = i * SEGMENTS + (j + 1) % SEGMENTS;
                if (i == rows.Count - 1)
                {
                    surface.AddIndex(a);
                    surface.AddIndex(b);
                    surface.AddIndex(center);
                    continue;
                }
                int c = a + SEGMENTS;
                int e = b + SEGMENTS;
                surface.AddIndex(a);
                surface.AddIndex(b);
                surface.AddIndex(c);
                surface.AddIndex(b);
                surface.AddIndex(e);
                surface.AddIndex(c);
            }
        }
        surface.GenerateNormals();
        return surface.Commit();
    }

    private static MultiMesh AddProps(Node3D parent, Mesh mesh, float sway)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            Mesh                 = mesh,
            InstanceCount        = MAX_PROPS,
            VisibleInstanceCount = 0,
        };
        var material = new ShaderMaterial { Shader = PLANT_SHADER };
        material.SetShaderParameter("sway", sway);
        // No shadows: thousands of thin props only print blocky shadow texels at this scale
        parent.AddChild(new MultiMeshInstance3D { Multimesh = multimesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return multimesh;
    }

    // ===========================================================================
    // ================================   FOLLOW   ===============================
    // ===========================================================================
    //
    //   1. Ground   Rescales the heights with the player, the deep pattern matches the Backdrop.
    //   2. Water    Centered on the player, as far as the camera draws, open sea in third person
    //               (low, on the beach height: from the side a high surface looked like a raised plate).
    //   3. Props    Refilled when the player moved a few Units or grew noticeably.
    //   4. Trees    Chunks around the player come and go, all rebuild when the player grew.
    //

    private const float PROP_REACH = 45f;            // props at most this far, in Unit

    private Vector2 _propsCenter = new(float.NaN, float.NaN);
    private float _propsSize;

    /// <summary>Keeps ground, water and props around the player, called every frame.</summary>
    /// <param name="playerPosition">(Required) Plane position of the player.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), heights scale with it.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, the same the Backdrop gets.</param>
    /// <param name="drawReach">(Optional) How far the water reaches, e.g. the camera far plane. Defaults to 2 view radii.</param>
    /// <param name="openSea">(Optional) 0 top view tint .. 1 open sea of the third person view. Defaults to 0.</param>
    public void Follow(Vector2 playerPosition, float playerSize, float viewRadius, float drawReach = 0f, float openSea = 0f)
    {
        // ---------------------------------------------------------------------------
        // Ground
        //   Rescales the heights with the player, the deep pattern matches the Backdrop.
        // ---------------------------------------------------------------------------

        foreach (MeshInstance3D ground in _grounds)
        {
            ground.Scale = new Vector3(1f, playerSize, 1f);
        }
        _groundMaterial.SetShaderParameter("world_scale", viewRadius * 2f);

        // ---------------------------------------------------------------------------
        // Water
        //   Centered on the player, as far as the camera draws, open sea in third person.
        // ---------------------------------------------------------------------------

        float reach = Mathf.Max(drawReach, viewRadius * 2f);
        // From above the water lies over the bodies (tint), from the side it meets the beach
        _water.Position = Plane3D.To3D(playerPosition, Mathf.Lerp(WATER_HEIGHT, OPEN_SEA_HEIGHT, openSea) * playerSize);
        _water.Scale    = new Vector3(reach * 2f, 1f, reach * 2f);
        _waterMaterial.SetShaderParameter("open_sea", openSea);
        _waterMaterial.SetShaderParameter("focus", playerPosition);
        _waterMaterial.SetShaderParameter("focus_size", playerSize);

        // ---------------------------------------------------------------------------
        // Props
        //   Refilled when the player moved a few Units or grew noticeably.
        // ---------------------------------------------------------------------------

        bool hasMoved = float.IsNaN(_propsCenter.X) || playerPosition.DistanceTo(_propsCenter) > Unit * 3f;
        bool hasGrown = Mathf.Abs(playerSize / Mathf.Max(_propsSize, 0.001f) - 1f) > 0.05f;
        if (hasMoved || hasGrown)
        {
            _propsCenter = playerPosition;
            _propsSize   = playerSize;
            FillProps(playerPosition, playerSize, Mathf.Min(viewRadius * 1.3f + Unit * 4f, PROP_REACH * Unit));
        }

        // ---------------------------------------------------------------------------
        // Trees
        //   Chunks around the player come and go, all rebuild when the player grew.
        // ---------------------------------------------------------------------------

        UpdateTrees(playerPosition, playerSize);
    }

    // ===========================================================================
    // ================================   PROPS   ================================
    // ===========================================================================
    //
    //   FillProps   Seeds every grid cell around the player and places its props by band.
    //   AddTuft     A tuft of grass blades.
    //   Place       World transform of a prop at a plane point, on the ground.
    //   Add         One instance into a multimesh, ignored when it is full.
    //
    //   Bands (d = LandDepth in Unit), per cell of PROP_CELL Units, on land every size
    //   times NATURE_SCALE (under water they would reach out of the water):
    //     -16 .. -1.5   seaweed clumps, rocks
    //     -1.5 .. 2.5   pebbles, shells, driftwood
    //     2.5 .. 5      tall dune grass
    //     5 ..          grass tufts, bushes, flowers, a few rocks
    //

    private const float PROP_CELL = 2.5f;            // in Unit

    private readonly RandomNumberGenerator _rng = new();
    private int _bladeCount;
    private int _lumpCount;
    private int _stickCount;

    private void FillProps(Vector2 center, float size, float reach)
    {
        _bladeCount = 0;
        _lumpCount  = 0;
        _stickCount = 0;
        float cell = PROP_CELL * Unit;
        int   x0   = Mathf.FloorToInt((center.X - reach) / cell);
        int   x1   = Mathf.CeilToInt((center.X + reach) / cell);
        int   y0   = Mathf.FloorToInt((center.Y - reach) / cell);
        int   y1   = Mathf.CeilToInt((center.Y + reach) / cell);

        for (int gx = x0; gx <= x1; gx++)
        {
            for (int gy = y0; gy <= y1; gy++)
            {
                // Same cell -> same seed -> the same props every time
                _rng.Seed = (ulong)(uint)System.HashCode.Combine(gx, gy, 31);
                var   point = new Vector2(gx + _rng.Randf(), gy + _rng.Randf()) * cell;
                float roll  = _rng.Randf();
                float d     = LandDepth(point) / Unit;
                if (d < -16f || point.DistanceSquaredTo(center) > reach * reach)
                {
                    continue;
                }

                // Under water: seaweed clumps reaching up, mossy rocks
                if (d < -1.5f)
                {
                    if (roll < 0.35f)
                    {
                        int blades = _rng.RandiRange(3, 6);
                        for (int k = 0; k < blades; k++)
                        {
                            float height = _rng.RandfRange(0.6f, 1.4f);
                            Color kelp   = new Color(0.16f, 0.36f, 0.22f).Lerp(new Color(0.42f, 0.36f, 0.16f), _rng.Randf());
                            Add(_blades, ref _bladeCount, Place(Jitter(point, 0.3f), new Vector3(0.45f, height / 0.6f, 0.3f) * size * 0.25f,
                                height * 0.5f * size, size, tilt: 0.25f), kelp);
                        }
                    }
                    else if (roll < 0.5f)
                    {
                        float rock = _rng.RandfRange(0.15f, 0.45f);
                        Add(_lumps, ref _lumpCount, Place(point, new Vector3(rock, rock * 0.6f, rock * 0.8f) * size, 0f, size, tilt: 0f),
                            new Color(0.3f, 0.36f, 0.33f).Lightened(_rng.RandfRange(-0.1f, 0.15f)));
                    }
                    continue;
                }

                // On land everything is NATURE_SCALE bigger against the body
                float big = size * NATURE_SCALE;

                // Beach: pebbles, shells and now and then driftwood
                if (d < 2.5f)
                {
                    if (roll < 0.22f)
                    {
                        int pebbles = _rng.RandiRange(2, 4);
                        for (int k = 0; k < pebbles; k++)
                        {
                            float pebble = _rng.RandfRange(0.03f, 0.08f);
                            Add(_lumps, ref _lumpCount, Place(Jitter(point, 0.4f * NATURE_SCALE), new Vector3(pebble, pebble * 0.55f, pebble * 0.8f) * big, 0f, size, tilt: 0f),
                                new Color(0.55f, 0.52f, 0.48f).Lightened(_rng.RandfRange(-0.2f, 0.25f)));
                        }
                    }
                    else if (roll < 0.34f)
                    {
                        Color shell = _rng.Randf() < 0.5f ? new Color(0.97f, 0.82f, 0.78f) : new Color(0.98f, 0.94f, 0.85f);
                        Add(_lumps, ref _lumpCount, Place(point, new Vector3(0.07f, 0.025f, 0.055f) * big, 0f, size, tilt: 0f), shell);
                    }
                    else if (roll < 0.38f)
                    {
                        float length = _rng.RandfRange(0.5f, 1.1f);
                        Transform3D wood = Place(point, new Vector3(0.045f, length, 0.045f) * big, 0.03f * big, size, tilt: 0f,
                            lay: new Basis(Vector3.Back, Mathf.Pi * 0.5f));   // the rod lies down
                        Add(_sticks, ref _stickCount, wood, new Color(0.5f, 0.4f, 0.3f));
                    }
                    continue;
                }

                // Dunes: sparse, tall pale grass
                if (d < 5f)
                {
                    if (roll < 0.5f)
                    {
                        AddTuft(point, size, new Color(0.62f, 0.66f, 0.42f), 0.25f * NATURE_SCALE, 0.45f * NATURE_SCALE);
                    }
                    continue;
                }

                // Meadow: grass tufts, bushes, flowers, a few rocks
                if (roll < 0.62f)
                {
                    AddTuft(point, size, new Color(0.25f, 0.45f, 0.2f).Lerp(new Color(0.42f, 0.58f, 0.26f), _rng.Randf()), 0.15f * NATURE_SCALE, 0.32f * NATURE_SCALE);
                }
                else if (roll < 0.76f)
                {
                    Color leaves = new Color(0.18f, 0.36f, 0.17f).Lightened(_rng.RandfRange(-0.05f, 0.12f));
                    for (int k = 0; k < 3; k++)
                    {
                        float leaf = _rng.RandfRange(0.14f, 0.24f);
                        Add(_lumps, ref _lumpCount, Place(Jitter(point, 0.15f * NATURE_SCALE), Vector3.One * leaf * big, leaf * 0.7f * big, size, tilt: 0f),
                            leaves.Lightened(k * 0.06f));
                    }
                }
                else if (roll < 0.9f)
                {
                    Color bloom = new[] { new Color(0.97f, 0.86f, 0.3f), new Color(0.93f, 0.48f, 0.68f), new Color(0.6f, 0.62f, 0.98f), Colors.White }[_rng.RandiRange(0, 3)];
                    int   heads = _rng.RandiRange(2, 4);
                    for (int k = 0; k < heads; k++)
                    {
                        Vector2 spot = Jitter(point, 0.3f * NATURE_SCALE);
                        float   stem = _rng.RandfRange(0.15f, 0.25f);
                        Add(_blades, ref _bladeCount, Place(spot, new Vector3(0.08f, stem / 0.6f, 0.08f) * big * 0.25f, stem * 0.5f * big, size, tilt: 0.1f),
                            new Color(0.3f, 0.5f, 0.22f));
                        Add(_lumps, ref _lumpCount, Place(spot, Vector3.One * 0.035f * big, stem * big, size, tilt: 0f), bloom);
                    }
                }
                else if (roll < 0.94f)
                {
                    float rock = _rng.RandfRange(0.12f, 0.3f);
                    Add(_lumps, ref _lumpCount, Place(point, new Vector3(rock, rock * 0.65f, rock * 0.85f) * big, 0f, size, tilt: 0f),
                        new Color(0.5f, 0.5f, 0.47f).Lightened(_rng.RandfRange(-0.1f, 0.1f)));
                }
            }
        }

        _blades.VisibleInstanceCount = _bladeCount;
        _lumps.VisibleInstanceCount  = _lumpCount;
        _sticks.VisibleInstanceCount = _stickCount;
    }

    /// <summary>A point moved randomly by up to spread Units on both axes.</summary>
    private Vector2 Jitter(Vector2 point, float spread)
    {
        return point + new Vector2(_rng.RandfRange(-spread, spread), _rng.RandfRange(-spread, spread)) * Unit;
    }

    /// <summary>A tuft of grass blades of one color, heights between min and max player sizes.</summary>
    private void AddTuft(Vector2 point, float size, Color color, float minHeight, float maxHeight)
    {
        int blades = _rng.RandiRange(4, 7);
        for (int k = 0; k < blades; k++)
        {
            float height = _rng.RandfRange(minHeight, maxHeight);
            Add(_blades, ref _bladeCount, Place(Jitter(point, 0.12f * NATURE_SCALE), new Vector3(0.3f, height / 0.6f, 0.2f) * size * 0.25f,
                height * 0.5f * size, size, tilt: 0.35f), color.Lightened(_rng.RandfRange(-0.08f, 0.1f)));
        }
    }

    /// <summary>A transform at a plane point, standing on the ground, randomly turned and tilted.</summary>
    /// <param name="lift">(Required) Height of the prop center above the ground, in world units.</param>
    /// <param name="tilt">(Required) Largest random lean in radians.</param>
    /// <param name="lay">(Optional) Turn of the mesh before the lean, e.g. a rod lying down. Defaults to none.</param>
    private Transform3D Place(Vector2 point, Vector3 scale, float lift, float size, float tilt, Basis? lay = null)
    {
        float ground = GroundHeight(point) * size;
        Basis basis  = new Basis(Vector3.Up, _rng.Randf() * Mathf.Tau)
            * new Basis(Vector3.Right, _rng.RandfRange(-tilt, tilt))
            * (lay ?? Basis.Identity)
            * Basis.FromScale(scale);
        return new Transform3D(basis, Plane3D.To3D(point, ground + lift));
    }

    private static void Add(MultiMesh multimesh, ref int count, Transform3D transform, Color color)
    {
        if (count >= MAX_PROPS)
        {
            return;
        }
        multimesh.SetInstanceTransform(count, transform);
        multimesh.SetInstanceColor(count, color);
        count++;
    }

    // ===========================================================================
    // ================================   TREES   ================================
    // ===========================================================================
    //
    //   Tree             One tree: plane position, species, height and yaw, tint.
    //   TreesIn          The trees of one grid cell, the same every time (seeded by the cell).
    //   PushOutOfTrees   A point moved out of every trunk it overlaps.
    //   UpdateTrees      Adds chunks entering TREE_REACH, frees chunks leaving it.
    //   BuildTreeChunk   One node per chunk with a multimesh per species.
    //
    //   Heights in Unit (player sizes when the coast appeared), the ranges below times NATURE_SCALE:
    //     young 2..4, normal 5..10, giants 15..25 (scale trees, only deep in forests)
    //     -> about 6..75 body sizes, the animal walks below them like a dog in a forest
    //   Bands (d = LandDepth in Unit):
    //     2.5 .. 10   dunes: now and then a cycad
    //     10 ..       forests (broad noise) with clearings between them: broadleaf, giant,
    //                 horsetail, tree ferns, a fern undergrowth; single trees in the clearings
    //

    public readonly record struct Tree(Vector2 Position, TreeSpecies Species, float Height, float Yaw, Color Tint)
    {
        public float TrunkRadius => TreeModels.TRUNK_SHARE[(int)Species] * Height;
    }

    private const float TREE_CELL = 16f;             // in Unit
    private const int TREE_CHUNK = 6;                // cells per chunk side
    private const float TREE_REACH = 260f;           // chunks this far stay loaded, in Unit
    private const float TREE_RELOAD = 10f;           // chunks update after the player went this far, in Unit
    private static readonly float[] TREE_VISIBLE = { 140f, 260f, 260f, 300f, 500f };  // per species, in Unit

    private readonly FastNoiseLite _forest = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.018f, Seed = 23 };
    private readonly RandomNumberGenerator _treeRng = new();
    private readonly Dictionary<Vector2I, Node3D> _treeChunks = new();
    private Vector2 _treesCenter = new(float.NaN, float.NaN);
    private float _treesSize;

    /*
      > Dunes: 22% for a cycad
      > Deeper inland: the forest noise sets the chance of a tree (8% in a clearing, up to
        90% deep in a forest), then the species:
          giant 8% (only deep in a forest), broadleaf 45%, horsetail 22%, tree fern 25%
        and in a forest 40% for a young fern of the undergrowth next to it
    */
    /// <summary>The trees of one grid cell: none, one or two.</summary>
    /// <param name="cell">(Required) Grid cell of TREE_CELL Units.</param>
    public List<Tree> TreesIn(Vector2I cell)
    {
        var trees = new List<Tree>(2);
        _treeRng.Seed = (ulong)(uint)System.HashCode.Combine(cell.X, cell.Y, 57);
        Vector2 point = (new Vector2(cell.X, cell.Y) + new Vector2(_treeRng.RandfRange(0.15f, 0.85f), _treeRng.RandfRange(0.15f, 0.85f))) * TREE_CELL * Unit;
        float   roll  = _treeRng.Randf();
        float   d     = LandDepth(point) / Unit;
        if (d < 2.5f)
        {
            return trees;
        }

        // Dunes: 22% for a cycad
        if (d < 10f)
        {
            if (roll < 0.22f)
            {
                trees.Add(NewTree(point, TreeSpecies.Cycad, _treeRng.RandfRange(4f, 8f)));
            }
            return trees;
        }

        // Deeper inland: forests and clearings
        Vector2 q      = point / Unit;
        float   forest = Mathf.SmoothStep(-0.15f, 0.35f, _forest.GetNoise2D(q.X, q.Y));
        if (roll > 0.08f + 0.82f * forest)
        {
            return trees;
        }
        float species = _treeRng.Randf();
        bool  isYoung = _treeRng.Randf() < 0.3f;
        if (forest > 0.6f && species < 0.08f)
        {
            trees.Add(NewTree(point, TreeSpecies.Giant, _treeRng.RandfRange(15f, 25f)));
        }
        else if (species < 0.53f)
        {
            trees.Add(NewTree(point, TreeSpecies.Broadleaf, isYoung ? _treeRng.RandfRange(2f, 4f) : _treeRng.RandfRange(5f, 10f)));
        }
        else if (species < 0.75f)
        {
            trees.Add(NewTree(point, TreeSpecies.Horsetail, isYoung ? _treeRng.RandfRange(2.5f, 4f) : _treeRng.RandfRange(5f, 9f)));
        }
        else
        {
            trees.Add(NewTree(point, TreeSpecies.Fern, _treeRng.RandfRange(2f, 5f)));
        }

        // Undergrowth: a young fern next to it
        if (forest > 0.4f && _treeRng.Randf() < 0.4f)
        {
            Vector2 next = point + Vector2.FromAngle(_treeRng.Randf() * Mathf.Tau) * _treeRng.RandfRange(2f, 3f) * NATURE_SCALE * Unit;
            if (IsOnLand(next))
            {
                trees.Add(NewTree(next, TreeSpecies.Fern, _treeRng.RandfRange(1.5f, 3f)));
            }
        }
        return trees;
    }

    /// <summary>A tree with a random yaw and a slightly varied tint, base height in Unit (times NATURE_SCALE).</summary>
    private Tree NewTree(Vector2 point, TreeSpecies species, float height)
    {
        float light = _treeRng.RandfRange(0.85f, 1.1f);
        var   tint  = new Color(light * _treeRng.RandfRange(0.95f, 1.08f), light, light * _treeRng.RandfRange(0.9f, 1.02f));
        return new Tree(point, species, height * NATURE_SCALE * Unit, _treeRng.Randf() * Mathf.Tau, tint);
    }

    /// <summary>A point pushed out of every trunk it overlaps, e.g. the walking player.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="radius">(Required) Radius of the body at the point.</param>
    /// <returns>The point itself when it touches no trunk.</returns>
    public Vector2 PushOutOfTrees(Vector2 point, float radius)
    {
        var center = new Vector2I(Mathf.FloorToInt(point.X / (TREE_CELL * Unit)), Mathf.FloorToInt(point.Y / (TREE_CELL * Unit)));
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                foreach (Tree tree in TreesIn(center + new Vector2I(x, y)))
                {
                    Vector2 away  = point - tree.Position;
                    float   reach = tree.TrunkRadius + radius;
                    if (away.LengthSquared() >= reach * reach)
                    {
                        continue;
                    }
                    Vector2 outward = away.LengthSquared() > 0.0001f ? away.Normalized() : Vector2.Right;
                    point = tree.Position + outward * reach;
                }
            }
        }
        return point;
    }

    /*
      > The player grew noticeably: every chunk rebuilds (the ground heights scale with the size)
      > The player went TREE_RELOAD Units: chunks within TREE_REACH come, the others go
    */
    private void UpdateTrees(Vector2 playerPosition, float playerSize)
    {
        if (Mathf.Abs(playerSize / Mathf.Max(_treesSize, 0.001f) - 1f) > 0.05f)
        {
            foreach (Node3D chunk in _treeChunks.Values)
            {
                chunk.QueueFree();
            }
            _treeChunks.Clear();
            _treesSize   = playerSize;
            _treesCenter = new Vector2(float.NaN, float.NaN);
        }
        if (!float.IsNaN(_treesCenter.X) && playerPosition.DistanceTo(_treesCenter) < TREE_RELOAD * Unit)
        {
            return;
        }
        _treesCenter = playerPosition;

        float chunkSize = TREE_CELL * TREE_CHUNK * Unit;
        float reach     = TREE_REACH * Unit;
        var   wanted    = new HashSet<Vector2I>();
        int   x0        = Mathf.FloorToInt((playerPosition.X - reach) / chunkSize);
        int   x1        = Mathf.FloorToInt((playerPosition.X + reach) / chunkSize);
        int   y0        = Mathf.FloorToInt((playerPosition.Y - reach) / chunkSize);
        int   y1        = Mathf.FloorToInt((playerPosition.Y + reach) / chunkSize);
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                Vector2 middle = (new Vector2(x, y) + new Vector2(0.5f, 0.5f)) * chunkSize;
                if (middle.DistanceTo(playerPosition) < reach + chunkSize * 0.71f)
                {
                    wanted.Add(new Vector2I(x, y));
                }
            }
        }
        foreach (Vector2I gone in _treeChunks.Keys.Where(chunk => !wanted.Contains(chunk)).ToList())
        {
            _treeChunks[gone].QueueFree();
            _treeChunks.Remove(gone);
        }
        foreach (Vector2I chunk in wanted.Where(chunk => !_treeChunks.ContainsKey(chunk)).ToList())
        {
            _treeChunks[chunk] = BuildTreeChunk(chunk, playerSize);
        }
    }

    private Node3D BuildTreeChunk(Vector2I chunk, float playerSize)
    {
        var node       = new Node3D();
        var transforms = new List<Transform3D>[TreeModels.COUNT];
        var tints      = new List<Color>[TreeModels.COUNT];
        for (int i = 0; i < TreeModels.COUNT; i++)
        {
            transforms[i] = new List<Transform3D>();
            tints[i]      = new List<Color>();
        }

        // Every tree of the chunk, a little sunk into the ground so roots sit on slopes
        for (int x = 0; x < TREE_CHUNK; x++)
        {
            for (int y = 0; y < TREE_CHUNK; y++)
            {
                foreach (Tree tree in TreesIn(chunk * TREE_CHUNK + new Vector2I(x, y)))
                {
                    float ground = GroundHeight(tree.Position) * playerSize - tree.Height * 0.03f;
                    Basis basis  = new Basis(Vector3.Up, tree.Yaw).Scaled(Vector3.One * tree.Height);
                    transforms[(int)tree.Species].Add(new Transform3D(basis, Plane3D.To3D(tree.Position, ground)));
                    tints[(int)tree.Species].Add(tree.Tint);
                }
            }
        }

        // One multimesh per species, culled per chunk, small trees vanish earlier
        for (int i = 0; i < TreeModels.COUNT; i++)
        {
            if (0 == transforms[i].Count)
            {
                continue;
            }
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors       = true,
                Mesh            = TreeModels.MeshOf((TreeSpecies)i),
                InstanceCount   = transforms[i].Count,
            };
            for (int k = 0; k < transforms[i].Count; k++)
            {
                multimesh.SetInstanceTransform(k, transforms[i][k]);
                multimesh.SetInstanceColor(k, tints[i][k]);
            }
            node.AddChild(new MultiMeshInstance3D { Multimesh = multimesh, VisibilityRangeEnd = TREE_VISIBLE[i] * Unit });
        }
        AddChild(node);
        return node;
    }
}
