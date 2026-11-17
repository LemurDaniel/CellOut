// NOTE:
//   Ground and water of the coast (Creature/Coast.cs), a component of it:
//     - ground (shaders/coast_ground.gdshader): one round mesh per island, rocky sea
//       floor that fades into the Backdrop floor with depth, shallow sand with caustics,
//       beach with ripples and a wet band behind the swash, dunes, meadows on rolling hills
//     - water (shaders/coast_water.gdshader): a sea surface around the player with a
//       wobbling water line, rolling wave crests and foam at every shore; as open sea
//       (third person) blue with fresnel towards the horizon
//   Both shaders get the islands, so they draw the same outline as World/IslandShape.cs.
//
//   var surface = new CoastSurface();
//   surface.Setup(terrain);          // after the last island was added
//   AddChild(surface);
//   surface.Follow(playerPosition, playerSize, viewRadius, drawReach, openSea);   // every frame

//
//   Chapters:
//     BUILD    One ground mesh per island and the water, the islands for the shaders.
//     FOLLOW   Rescales the ground and moves the water with the player.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CoastSurface : Node3D
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   Setup         (Required) terrain: the islands, their unit and the ground height.
    //                 Builds one ground mesh per island and the water.
    //   BuildGround   Rings around one island from the deep in to its middle.
    //   Level         Rebuilds the islands new foundations lie on (FOLLOW).
    //

    public const float OPEN_SEA_HEIGHT = -0.58f;     // third person: the sea meets the beach (ground -0.6 at the shore)
    private const float WATER_HEIGHT = 0.7f;         // water surface above the body center, in player sizes
    private const float SEGMENT_LENGTH = 1.4f;       // ground mesh columns at most this far apart along the shore, in Unit
    private const float MEADOW_ROW = 1f;             // ring rows inland this far apart, in Unit: the bumps of the meadow (~8 Unit) stay round
    private const int MIN_SEGMENTS = 256;
    private const int MAX_SEGMENTS = 768;            // the biggest islands: building the mesh stays quick
    private static readonly Shader GROUND_SHADER = GD.Load<Shader>("res://shaders/coast_ground.gdshader");
    private static readonly Shader WATER_SHADER = GD.Load<Shader>("res://shaders/coast_water.gdshader");

    private readonly List<MeshInstance3D> _grounds = new();
    private CoastTerrain _terrain;
    private ShaderMaterial _groundMaterial;
    private ShaderMaterial _waterMaterial;
    private MeshInstance3D _water;

    /// <summary>Builds ground and water for the islands of a terrain, called once before AddChild.</summary>
    /// <param name="terrain">(Required) The terrain of the coast with all its islands.</param>
    public void Setup(CoastTerrain terrain)
    {
        _terrain = terrain;

        // Ground: one mesh per island, heights in player sizes (scaled in FOLLOW)
        _groundMaterial = new ShaderMaterial { Shader = GROUND_SHADER };
        foreach (Coast.Island island in terrain.Islands)
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
        var islands = new Vector4[CoastTerrain.MAX_ISLANDS];
        for (int i = 0; i < terrain.Islands.Count; i++)
        {
            Coast.Island island = terrain.Islands[i];
            islands[i] = new Vector4(island.Center.X, island.Center.Y, island.Radius, island.Seed);
        }
        foreach (ShaderMaterial material in new[] { _groundMaterial, _waterMaterial })
        {
            material.SetShaderParameter("islands", islands);
            material.SetShaderParameter("island_count", terrain.Islands.Count);
            material.SetShaderParameter("unit", terrain.Unit);
        }
    }

    /*
      Rings from the deep in to the middle, heights from GroundHeight (all islands), so
      overlapping skirts of two islands agree:
        > Depth rings   at a land depth d (in Unit): coarse in the deep, every 0.5 Unit around
                        the shore, MEADOW_ROW on the meadow. A vertex lies at
                        IslandShape.Radius(angle) - d, so the rings follow the outline. They stop
                        where the narrowest side of the outline would turn inside out.
        > Core rings    shrink the last depth ring towards the middle, MEADOW_ROW apart, with
                        fewer columns the smaller they get (SEGMENT_LENGTH apart), then the middle.
                        Without them the middle of the island (village, hills) was one fan of
                        triangles and bodies, grass and foundations stood off its surface.
      Neighboring rings with different column counts are zipped by angle (Stitch).
      Winding: inwards x around gives an upward normal (drawn two sided).
    */
    private ArrayMesh BuildGround(Coast.Island island)
    {
        float unit  = _terrain.Unit;
        float inner = island.Radius * (2f - IslandShape.MAX_BULGE) / unit * 0.9f;
        var   depths = new List<float>();
        for (float d = -30f; d < -16f; d += 2f) depths.Add(d);
        for (float d = -16f; d < Mathf.Min(16f, inner); d += 0.5f) depths.Add(d);
        for (float d = 16f; d < inner; d += MEADOW_ROW) depths.Add(d);
        float lastDepth = depths[^1];

        // Columns by the circumference of the bulging outline, so the shore and the hills stay smooth on big islands
        int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Tau * island.Radius * IslandShape.MAX_BULGE / (SEGMENT_LENGTH * unit) / 64f) * 64, MIN_SEGMENTS, MAX_SEGMENTS);

        // Rings as (share of the last depth ring or -1 for a depth ring, depth, columns)
        var rings = new List<(float Share, float Depth, int Columns)>();
        foreach (float d in depths)
        {
            rings.Add((-1f, d, segments));
        }
        float coreReach = island.Radius * IslandShape.MAX_BULGE - lastDepth * unit;
        int   coreRings = Mathf.CeilToInt(coreReach / (MEADOW_ROW * unit));
        for (int k = coreRings - 1; k >= 1; k--)
        {
            float share   = (float)k / coreRings;
            int   columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Tau * coreReach * share / (SEGMENT_LENGTH * unit)), 8, segments);
            rings.Add((share, lastDepth, columns));
        }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var starts = new List<int>();
        int count  = 0;
        foreach ((float share, float depth, int columns) in rings)
        {
            starts.Add(count);
            for (int j = 0; j < columns; j++)
            {
                float   angle  = j * Mathf.Tau / columns;
                float   reach  = IslandShape.Radius(island, angle) - depth * unit;
                Vector2 local  = Vector2.FromAngle(angle) * (share < 0f ? reach : reach * share);
                surface.AddVertex(Plane3D.To3D(local, _terrain.GroundHeight(island.Center + local)));
            }
            count += columns;
        }
        int center = count;
        surface.AddVertex(Plane3D.To3D(Vector2.Zero, _terrain.GroundHeight(island.Center)));

        for (int i = 0; i < rings.Count - 1; i++)
        {
            Stitch(surface, starts[i], rings[i].Columns, starts[i + 1], rings[i + 1].Columns);
        }
        int last = rings.Count - 1;
        for (int j = 0; j < rings[last].Columns; j++)
        {
            surface.AddIndex(starts[last] + j);
            surface.AddIndex(starts[last] + (j + 1) % rings[last].Columns);
            surface.AddIndex(center);
        }
        surface.GenerateNormals();
        return surface.Commit();
    }

    /// <summary>Zips an outer ring to the next inner one by angle, both start at angle 0 and go around evenly.</summary>
    private static void Stitch(SurfaceTool surface, int outer, int outerCount, int inner, int innerCount)
    {
        int i = 0;
        int k = 0;
        while (i < outerCount || k < innerCount)
        {
            // Advance on the ring whose next column comes first around the circle
            float nextOuter = (i + 1f) / outerCount;
            float nextInner = (k + 1f) / innerCount;
            int   a = outer + i % outerCount;
            int   c = inner + k % innerCount;
            if (k >= innerCount || (i < outerCount && nextOuter <= nextInner))
            {
                surface.AddIndex(a);
                surface.AddIndex(outer + (i + 1) % outerCount);
                surface.AddIndex(c);
                i++;
                continue;
            }
            surface.AddIndex(a);
            surface.AddIndex(inner + (k + 1) % innerCount);
            surface.AddIndex(c);
            k++;
        }
    }

    // ===========================================================================
    // ===============================   FOLLOW   ================================
    // ===========================================================================
    //
    //   1. Ground   Rescales the heights with the player, the deep pattern matches the Backdrop.
    //   2. Water    Centered on the player, as far as the camera draws, open sea in third person
    //               (low, on the beach height: from the side a high surface looked like a raised plate).
    //

    private int _padsBuilt;

    /// <summary>Rebuilds the ground of every island a new foundation lies on and hands all foundations to the shader.</summary>
    /// <example><code>surface.Level();   // after CoastTerrain.AddPad</code></example>
    public void Level()
    {
        IReadOnlyList<CoastTerrain.Pad> pads = _terrain.Pads;
        float blend = CoastTerrain.PAD_BLEND * _terrain.Unit;
        for (int i = 0; i < _grounds.Count; i++)
        {
            Coast.Island island = _terrain.Islands[i];
            bool isTouched = false;
            for (int p = _padsBuilt; p < pads.Count; p++)
            {
                isTouched |= pads[p].Center.DistanceTo(island.Center) < island.Radius * IslandShape.MAX_BULGE + pads[p].Radius + blend;
            }
            if (isTouched)
            {
                _grounds[i].Mesh = BuildGround(island);
            }
        }
        _padsBuilt = pads.Count;

        // The shader draws trampled earth on them: xy center, z radius
        var packed = new Vector4[CoastTerrain.MAX_PADS];
        for (int p = 0; p < pads.Count; p++)
        {
            packed[p] = new Vector4(pads[p].Center.X, pads[p].Center.Y, pads[p].Radius, 0f);
        }
        _groundMaterial.SetShaderParameter("pads", packed);
        _groundMaterial.SetShaderParameter("pad_count", pads.Count);
    }

    /// <summary>Keeps ground and water around the player, called every frame.</summary>
    /// <param name="playerPosition">(Required) Plane position of the player.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), heights scale with it.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, the same the Backdrop gets.</param>
    /// <param name="drawReach">(Required) How far the water reaches, e.g. the camera far plane. At least 2 view radii.</param>
    /// <param name="openSea">(Required) 0 top view tint .. 1 open sea of the third person view.</param>
    public void Follow(Vector2 playerPosition, float playerSize, float viewRadius, float drawReach, float openSea)
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
    }
}
