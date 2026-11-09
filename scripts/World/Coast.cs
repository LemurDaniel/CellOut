// NOTE:
//   The coast of the organism stage: a straight shore line on the plane (the logic),
//   land on one side, sea on the other. Drawn with life on top of it:
//     - ground (shaders/coast_ground.gdshader): rocky sea floor that fades into the
//       Backdrop floor with depth, shallow sand with caustics, beach with ripples and a
//       wet band behind the swash, dunes, a meadow with flowers
//     - water (shaders/coast_water.gdshader): a wobbling water line, rolling wave crests
//       and foam over the sea only
//     - props: seaweed and rocks under water, pebbles, shells and driftwood on the
//       beach, grass, bushes and flowers on the meadow, seeded per grid cell
//   Horizontal distances are world units (the coast stays put), heights are player
//   sizes (Follow rescales them), so the ground always fits the current body.
//
//   var coast = new Coast { Origin = point, Normal = direction, Unit = player.BodySize() };
//   AddChild(coast);
//   coast.Follow(player.GlobalPosition, player.BodySize(), viewRadius);   // every frame

//
//   Chapters:
//     SETTINGS          Shore line, extents, the ground profile and the shaders.
//     LAND AND SEA      Which side a point is on, how deep inside, the ground height.
//     BUILD             Ground, water and the three prop multimeshes.
//     FOLLOW            Re-centers the ground along the shore, rescales, refills the props.
//     PROPS             Seeded props per grid cell, by band.
//

using Godot;

namespace CellOut;

public partial class Coast : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Origin   (Required) A point on the shore line.
    //   Normal   (Required) Unit direction from the sea onto the land.
    //   Unit     (Required) Player size when the coast appeared, every band scales with it.
    //
    //   Ground profile, d in Unit (negative = sea), height in player sizes:
    //     d -30 .. -14    -3 -> -2.9    sea floor of the Backdrop, rocky relief fades in
    //     d -14 .. -2     -2.9 -> -1    the slope up
    //     d -2 .. 0       -1 -> -0.6    shallow water
    //     d 0 .. 3        -0.6 -> -0.5  beach, then dunes and meadow at GROUND_HEIGHT
    //

    public Vector2 Origin { get; set; }
    public Vector2 Normal { get; set; } = Vector2.Right;
    public float Unit { get; set; } = 14f;

    public const float GROUND_HEIGHT = -0.5f;        // dry land below the body center, in player sizes
    private const float WATER_HEIGHT = 0.7f;         // water surface above the body center, in player sizes
    private const float ALONG_HALF = 100f;           // half length of the ground along the shore, in Unit
    private const float ALONG_STEP = 1.5f;           // column spacing along the shore, in Unit
    private const float RECENTER_DISTANCE = 30f;     // the ground re-centers after the player went this far, in Unit
    private static readonly float[] PROFILE_D = { -30f, -14f, -8f, -2f, 0f, 1f, 3f, 6f, 12f, 400f };
    private static readonly float[] PROFILE_H = { -3f, -2.9f, -2f, -1f, -0.6f, -0.55f, GROUND_HEIGHT, GROUND_HEIGHT, -0.45f, -0.45f };
    private static readonly Shader GROUND_SHADER = GD.Load<Shader>("res://shaders/coast_ground.gdshader");
    private static readonly Shader WATER_SHADER = GD.Load<Shader>("res://shaders/coast_water.gdshader");
    private static readonly Shader PLANT_SHADER = GD.Load<Shader>("res://shaders/coast_plant.gdshader");

    private Vector2 Tangent => new(-Normal.Y, Normal.X);

    // ===========================================================================
    // ============================   LAND AND SEA   =============================
    // ===========================================================================
    //
    //   LandDepth      Signed distance of a plane point into the land, negative in the sea.
    //   IsOnLand       Whether a plane point lies on land.
    //   ToSea          A plane point pushed back into the sea, at least a margin from the shore.
    //   GroundHeight   Ground height in player sizes: profile plus rocky relief and dunes.
    //

    /// <summary>Signed distance into the land, negative in the sea.</summary>
    /// <example><code>coast.LandDepth(coast.Origin + coast.Normal * 5f)  // 5</code></example>
    public float LandDepth(Vector2 point)
    {
        return (point - Origin).Dot(Normal);
    }

    public bool IsOnLand(Vector2 point)
    {
        return LandDepth(point) > 0f;
    }

    /// <summary>The point itself in the sea, otherwise moved straight out to sea.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="margin">(Required) Distance to keep from the shore.</param>
    public Vector2 ToSea(Vector2 point, float margin)
    {
        float depth = LandDepth(point);
        return depth > -margin ? point - Normal * (depth + margin) : point;
    }

    private readonly FastNoiseLite _noise = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.12f, Seed = 7 };

    /*
      Height = profile (linear between the PROFILE points)
             + relief under water: up to 0.35 sizes, fading in from d -2 to -6 and out
               towards the Backdrop floor from d -22 to -30
             + dunes on land: up to 0.12 sizes, growing from d 3 to 8
    */
    /// <summary>Ground height in player sizes at coast coordinates.</summary>
    /// <param name="d">(Required) Distance onto the land in Unit.</param>
    /// <param name="along">(Required) Position along the shore in Unit.</param>
    public float GroundHeight(float d, float along)
    {
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
        float noise  = _noise.GetNoise2D(d, along);
        float relief = Mathf.Clamp((-d - 2f) / 4f, 0f, 1f) * (1f - Mathf.Clamp((-d - 22f) / 8f, 0f, 1f));
        float dunes  = Mathf.Clamp((d - 3f) / 5f, 0f, 1f);
        return profile + noise * (0.35f * relief + 0.12f * dunes);
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready         Ground and water below this node (local +X onto the land),
    //                  the props top level in world space.
    //   BuildGround    A grid in coast coordinates around a center along the shore.
    //   AddProps       One prop multimesh with the plant shader.
    //

    private const int MAX_PROPS = 6000;

    private MeshInstance3D _ground;
    private ShaderMaterial _groundMaterial;
    private ShaderMaterial _waterMaterial;
    private MultiMesh _blades;
    private MultiMesh _lumps;
    private MultiMesh _sticks;
    private float _centerAlong = float.NaN;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Rotation    = new Vector3(0f, Plane3D.FacingToYaw(Normal.Angle()), 0f);

        // Ground, built around the player in Follow
        _groundMaterial = new ShaderMaterial { Shader = GROUND_SHADER };
        _ground = new MeshInstance3D { MaterialOverride = _groundMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ground);

        // Water surface over the sea, it reaches a little onto the beach for the swash
        _waterMaterial = new ShaderMaterial { Shader = WATER_SHADER };
        float seaReach = 280f * Unit;
        AddChild(new MeshInstance3D
        {
            Mesh             = new PlaneMesh { Size = new Vector2(seaReach + 2f * Unit, ALONG_HALF * 2f * Unit) },
            MaterialOverride = _waterMaterial,
            Position         = new Vector3(-seaReach * 0.5f + Unit, WATER_HEIGHT, 0f),
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        foreach (ShaderMaterial material in new[] { _groundMaterial, _waterMaterial })
        {
            material.SetShaderParameter("coast_origin", Origin);
            material.SetShaderParameter("coast_normal", Normal);
            material.SetShaderParameter("unit", Unit);
        }

        // Props in world space: blades (cones) sway, lumps (spheres) and sticks (rods) do not
        var props = new Node3D { TopLevel = true };
        AddChild(props);
        _blades = AddProps(props, CellVisual3D.ConeMesh(), 0.12f);
        _lumps  = AddProps(props, CellVisual3D.SphereMesh(true), 0f);
        _sticks = AddProps(props, CellVisual3D.RodMesh(), 0f);
        Log.Verbose("Coast", $"Coast at {Origin}, land towards {Normal}, unit {Unit:F1}.");
    }

    /*
      Rows across the shore (d): coarse in the deep, every 0.5 Unit around the shore,
      coarse again inland and a few far rows up to 400 Unit. Columns along the shore every
      ALONG_STEP. Heights from GroundHeight at the absolute along position, so a rebuilt
      grid lines up with the old one. Winding as in CreatureVisual3D (drawn two sided).
    */
    private ArrayMesh BuildGround(float centerAlong)
    {
        var rows = new System.Collections.Generic.List<float>();
        for (float d = -30f; d < -16f; d += 2f) rows.Add(d);
        for (float d = -16f; d < 16f; d += 0.5f) rows.Add(d);
        for (float d = 16f; d <= 60f; d += 2f) rows.Add(d);
        rows.AddRange(new[] { 100f, 200f, 400f });
        int columns = Mathf.RoundToInt(ALONG_HALF * 2f / ALONG_STEP) + 1;

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (float d in rows)
        {
            for (int j = 0; j < columns; j++)
            {
                float offset = -ALONG_HALF + j * ALONG_STEP;
                surface.AddVertex(new Vector3(d * Unit, GroundHeight(d, centerAlong + offset), offset * Unit));
            }
        }
        for (int i = 0; i < rows.Count - 1; i++)
        {
            for (int j = 0; j < columns - 1; j++)
            {
                int a = i * columns + j;
                int b = a + 1;
                int c = a + columns;
                int e = c + 1;
                surface.AddIndex(a);
                surface.AddIndex(c);
                surface.AddIndex(b);
                surface.AddIndex(b);
                surface.AddIndex(c);
                surface.AddIndex(e);
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
    //   1. Ground   Re-centers along the shore once the player went far, rescales the heights.
    //   2. Floor    The sea floor pattern scale of the Backdrop, so the deep ground matches it.
    //   3. Props    Refilled when the player moved a few Units or grew noticeably.
    //

    private Vector2 _propsCenter = new(float.NaN, float.NaN);
    private float _propsSize;

    /// <summary>Keeps ground, water and props around the player, called every frame.</summary>
    /// <param name="playerPosition">(Required) Plane position of the player.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), heights scale with it.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, the same the Backdrop gets.</param>
    public void Follow(Vector2 playerPosition, float playerSize, float viewRadius)
    {
        // ---------------------------------------------------------------------------
        // Ground
        //   Re-centers along the shore once the player went far, rescales the heights.
        // ---------------------------------------------------------------------------

        float along = (playerPosition - Origin).Dot(Tangent) / Unit;
        if (float.IsNaN(_centerAlong) || Mathf.Abs(along - _centerAlong) > RECENTER_DISTANCE)
        {
            _centerAlong  = Mathf.Round(along / ALONG_STEP) * ALONG_STEP;
            _ground.Mesh  = BuildGround(_centerAlong);
            Log.Verbose("Coast", $"Ground re-centered at along {_centerAlong:F0}.");
        }
        Position = Plane3D.To3D(Origin + Tangent * _centerAlong * Unit);
        Scale    = new Vector3(1f, playerSize, 1f);

        // ---------------------------------------------------------------------------
        // Floor
        //   The sea floor pattern scale of the Backdrop, so the deep ground matches it.
        // ---------------------------------------------------------------------------

        _groundMaterial.SetShaderParameter("world_scale", viewRadius * 2f);

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
            FillProps(playerPosition, playerSize, viewRadius * 1.3f + Unit * 4f);
        }
    }

    // ===========================================================================
    // ================================   PROPS   ================================
    // ===========================================================================
    //
    //   FillProps   Seeds every grid cell around the player and places its props by band.
    //   Place       World transform of a prop at coast coordinates, on the ground.
    //   Add         One instance into a multimesh, ignored when it is full.
    //
    //   Bands (d in Unit), per cell of PROP_CELL Units:
    //     -16 .. -1.5   seaweed clumps, rocks
    //     -1.5 .. 2.5   pebbles, shells, driftwood
    //     2.5 .. 5      tall dune grass
    //     5 ..          grass tufts, bushes, flowers, a few rocks
    //

    private const float PROP_CELL = 1.5f;            // in Unit

    private readonly RandomNumberGenerator _rng = new();
    private int _bladeCount;
    private int _lumpCount;
    private int _stickCount;

    private void FillProps(Vector2 center, float size, float reach)
    {
        _bladeCount = 0;
        _lumpCount  = 0;
        _stickCount = 0;
        float centerD     = LandDepth(center) / Unit;
        float centerAlong = (center - Origin).Dot(Tangent) / Unit;
        float reachUnits  = reach / Unit;
        int   d0 = Mathf.FloorToInt((centerD - reachUnits) / PROP_CELL);
        int   d1 = Mathf.CeilToInt((centerD + reachUnits) / PROP_CELL);
        int   a0 = Mathf.FloorToInt((centerAlong - reachUnits) / PROP_CELL);
        int   a1 = Mathf.CeilToInt((centerAlong + reachUnits) / PROP_CELL);

        for (int gd = d0; gd <= d1; gd++)
        {
            for (int ga = a0; ga <= a1; ga++)
            {
                // Same cell -> same seed -> the same props every time
                _rng.Seed = (ulong)(uint)System.HashCode.Combine(gd, ga, 31);
                float d     = (gd + _rng.Randf()) * PROP_CELL;
                float along = (ga + _rng.Randf()) * PROP_CELL;
                float roll  = _rng.Randf();
                if (d < -16f)
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
                            Add(_blades, ref _bladeCount, Place(d + _rng.RandfRange(-0.3f, 0.3f), along + _rng.RandfRange(-0.3f, 0.3f),
                                new Vector3(0.12f, height / 0.6f, 0.12f) * size * 0.25f, height * 0.5f * size, size, tilt: 0.25f), kelp);
                        }
                    }
                    else if (roll < 0.5f)
                    {
                        float rock = _rng.RandfRange(0.15f, 0.45f);
                        Add(_lumps, ref _lumpCount, Place(d, along, new Vector3(rock, rock * 0.6f, rock * 0.8f) * size, 0f, size, tilt: 0f),
                            new Color(0.3f, 0.36f, 0.33f).Lightened(_rng.RandfRange(-0.1f, 0.15f)));
                    }
                    continue;
                }

                // Beach: pebbles, shells and now and then driftwood
                if (d < 2.5f)
                {
                    if (roll < 0.22f)
                    {
                        int pebbles = _rng.RandiRange(2, 4);
                        for (int k = 0; k < pebbles; k++)
                        {
                            float pebble = _rng.RandfRange(0.03f, 0.08f);
                            Add(_lumps, ref _lumpCount, Place(d + _rng.RandfRange(-0.4f, 0.4f), along + _rng.RandfRange(-0.4f, 0.4f),
                                new Vector3(pebble, pebble * 0.55f, pebble * 0.8f) * size, 0f, size, tilt: 0f),
                                new Color(0.55f, 0.52f, 0.48f).Lightened(_rng.RandfRange(-0.2f, 0.25f)));
                        }
                    }
                    else if (roll < 0.34f)
                    {
                        Color shell = _rng.Randf() < 0.5f ? new Color(0.97f, 0.82f, 0.78f) : new Color(0.98f, 0.94f, 0.85f);
                        Add(_lumps, ref _lumpCount, Place(d, along, new Vector3(0.07f, 0.025f, 0.055f) * size, 0f, size, tilt: 0f), shell);
                    }
                    else if (roll < 0.38f)
                    {
                        float length = _rng.RandfRange(0.5f, 1.1f);
                        Transform3D wood = Place(d, along, new Vector3(0.045f, length, 0.045f) * size, 0.03f * size, size, tilt: 0f,
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
                        AddTuft(d, along, size, new Color(0.62f, 0.66f, 0.42f), 0.25f, 0.45f);
                    }
                    continue;
                }

                // Meadow: grass tufts, bushes, flowers, a few rocks
                if (roll < 0.62f)
                {
                    AddTuft(d, along, size, new Color(0.25f, 0.45f, 0.2f).Lerp(new Color(0.42f, 0.58f, 0.26f), _rng.Randf()), 0.15f, 0.32f);
                }
                else if (roll < 0.76f)
                {
                    Color leaves = new Color(0.18f, 0.36f, 0.17f).Lightened(_rng.RandfRange(-0.05f, 0.12f));
                    for (int k = 0; k < 3; k++)
                    {
                        float leaf = _rng.RandfRange(0.14f, 0.24f);
                        Add(_lumps, ref _lumpCount, Place(d + _rng.RandfRange(-0.15f, 0.15f), along + _rng.RandfRange(-0.15f, 0.15f),
                            Vector3.One * leaf * size, leaf * 0.7f * size, size, tilt: 0f), leaves.Lightened(k * 0.06f));
                    }
                }
                else if (roll < 0.9f)
                {
                    Color bloom = new[] { new Color(0.97f, 0.86f, 0.3f), new Color(0.93f, 0.48f, 0.68f), new Color(0.6f, 0.62f, 0.98f), Colors.White }[_rng.RandiRange(0, 3)];
                    int   heads = _rng.RandiRange(2, 4);
                    for (int k = 0; k < heads; k++)
                    {
                        float fd = d + _rng.RandfRange(-0.3f, 0.3f);
                        float fa = along + _rng.RandfRange(-0.3f, 0.3f);
                        float stem = _rng.RandfRange(0.15f, 0.25f);
                        Add(_blades, ref _bladeCount, Place(fd, fa, new Vector3(0.08f, stem / 0.6f, 0.08f) * size * 0.25f, stem * 0.5f * size, size, tilt: 0.1f),
                            new Color(0.3f, 0.5f, 0.22f));
                        Add(_lumps, ref _lumpCount, Place(fd, fa, Vector3.One * 0.035f * size, stem * size, size, tilt: 0f), bloom);
                    }
                }
                else if (roll < 0.94f)
                {
                    float rock = _rng.RandfRange(0.12f, 0.3f);
                    Add(_lumps, ref _lumpCount, Place(d, along, new Vector3(rock, rock * 0.65f, rock * 0.85f) * size, 0f, size, tilt: 0f),
                        new Color(0.5f, 0.5f, 0.47f).Lightened(_rng.RandfRange(-0.1f, 0.1f)));
                }
            }
        }

        _blades.VisibleInstanceCount = _bladeCount;
        _lumps.VisibleInstanceCount  = _lumpCount;
        _sticks.VisibleInstanceCount = _stickCount;
    }

    /// <summary>A tuft of grass blades of one color, heights between min and max player sizes.</summary>
    private void AddTuft(float d, float along, float size, Color color, float minHeight, float maxHeight)
    {
        int blades = _rng.RandiRange(4, 7);
        for (int k = 0; k < blades; k++)
        {
            float height = _rng.RandfRange(minHeight, maxHeight);
            Add(_blades, ref _bladeCount, Place(d + _rng.RandfRange(-0.12f, 0.12f), along + _rng.RandfRange(-0.12f, 0.12f),
                new Vector3(0.1f, height / 0.6f, 0.1f) * size * 0.25f, height * 0.5f * size, size, tilt: 0.35f),
                color.Lightened(_rng.RandfRange(-0.08f, 0.1f)));
        }
    }

    /// <summary>A transform at coast coordinates, standing on the ground, randomly turned and tilted.</summary>
    /// <param name="lift">(Required) Height of the prop center above the ground, in world units.</param>
    /// <param name="tilt">(Required) Largest random lean in radians.</param>
    /// <param name="lay">(Optional) Turn of the mesh before the lean, e.g. a rod lying down. Defaults to none.</param>
    private Transform3D Place(float d, float along, Vector3 scale, float lift, float size, float tilt, Basis? lay = null)
    {
        Vector2 point  = Origin + Normal * d * Unit + Tangent * along * Unit;
        float   ground = GroundHeight(d, along) * size;
        Basis   basis  = new Basis(Vector3.Up, _rng.Randf() * Mathf.Tau)
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
}
