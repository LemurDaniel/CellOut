// NOTE:
//   The primordial soup in 3D: a sea floor with caustics below the plane and
//   floating motes at different depths (parallax), plus big soft blobs close to the
//   camera, out of focus like dust under a microscope. Motes and blobs are placed per
//   grid cell with a seeded generator, so the endless world needs no stored objects.
//   World/FollowCamera.cs calls Follow once per frame and Tint when the water changes color.

//
//   Chapters:
//     BUILD SCENERY   Sea floor with the ocean shader, the mote and the blob multimesh.
//     FOLLOW VIEW     Moves the floor and refills motes and blobs around the view, colors of the water.
//

using Godot;

namespace CellOut;

public partial class Backdrop : Node3D
{
    private const int MAX_MOTES = 900;
    private const int MOTES_PER_CELL = 2;
    private const float FLOOR_DEPTH = 3f;      // sea floor depth below the plane in player sizes
    private const float GRID_CELLS = 9f;       // grid cells across the view diameter
    private const int MAX_BLOBS = 80;
    private const int BLOB_GRID = 5;           // one blob per BLOB_GRID x BLOB_GRID mote cells, rare on purpose
    private const float BLOB_LOW = 3f;         // blobs float this far above the plane, in player sizes
    private const float BLOB_HIGH = 8f;        // well below the camera (about 12 sizes up)
    private const int BLOB_TEXTURE_SIZE = 64;
    private static readonly Shader OCEAN_SHADER = GD.Load<Shader>("res://shaders/ocean.gdshader");

    // ===========================================================================
    // ============================   BUILD SCENERY   ============================
    // ===========================================================================
    //
    //   1. Sea floor   A plane with the ocean shader, casts no shadow but receives them.
    //   2. Motes       One multimesh with vertex colors, filled in FOLLOW VIEW.
    //   3. Blobs       Soft discs facing the camera, close to it and out of focus.
    //

    private MeshInstance3D _floor;
    private ShaderMaterial _floorMaterial;
    private MultiMesh _motes;

    public override void _Ready()
    {
        // ---------------------------------------------------------------------------
        // Sea floor
        //   A plane with the ocean shader, casts no shadow but receives them.
        // ---------------------------------------------------------------------------

        _floorMaterial = new ShaderMaterial { Shader = OCEAN_SHADER };
        // Subdivided, so it bends with the planet (planet.gdshaderinc)
        var floorMesh = new PlaneMesh { Size = Vector2.One, SubdivideWidth = 48, SubdivideDepth = 48 };
        _floor = CellVisual3D.AddMesh(this, floorMesh, _floorMaterial, Vector3.Zero, Vector3.One);
        _floor.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // ---------------------------------------------------------------------------
        // Motes
        //   One multimesh with vertex colors, filled in FOLLOW VIEW.
        // ---------------------------------------------------------------------------

        var moteMaterial = new StandardMaterial3D
        {
            ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
        };
        _motes = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            Mesh                 = CellVisual3D.SphereMesh(true),
            InstanceCount        = MAX_MOTES,
            VisibleInstanceCount = 0,
        };
        _moteInstance = new MultiMeshInstance3D
        {
            Multimesh        = _motes,
            MaterialOverride = moteMaterial,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_moteInstance);

        // ---------------------------------------------------------------------------
        // Blobs
        //   Soft discs facing the camera, close to it and out of focus.
        // ---------------------------------------------------------------------------

        var falloff = new Gradient { Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) }, Offsets = new[] { 0.15f, 1f } };
        var disc = new GradientTexture2D
        {
            Gradient = falloff,
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1f, 0.5f),
            Width    = BLOB_TEXTURE_SIZE,
            Height   = BLOB_TEXTURE_SIZE,
        };
        var blobMaterial = new StandardMaterial3D
        {
            ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode              = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode          = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale     = true,
            AlbedoTexture          = disc,
            VertexColorUseAsAlbedo = true,
        };
        _blobs = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            Mesh                 = new QuadMesh { Size = Vector2.One },
            InstanceCount        = MAX_BLOBS,
            VisibleInstanceCount = 0,
        };
        _blobInstance = new MultiMeshInstance3D
        {
            Multimesh        = _blobs,
            MaterialOverride = blobMaterial,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_blobInstance);
    }

    private MultiMeshInstance3D _moteInstance;
    private MultiMesh _blobs;
    private MultiMeshInstance3D _blobInstance;

    /// <summary>Motes float in the soup, over the open sea of the third person view they would hang in the air.</summary>
    public bool IsMotesShown
    {
        get => null == _moteInstance || _moteInstance.Visible;
        set
        {
            if (null != _moteInstance)
            {
                _moteInstance.Visible = value;
                _blobInstance.Visible = value;
            }
        }
    }

    // ===========================================================================
    // =============================   FOLLOW VIEW   =============================
    // ===========================================================================
    //
    //   Tint       Colors of the water: sea floor, its fade into the depth and the motes.
    //   Follow     1. Floor   Centers the floor below the view and scales its pattern with the view.
    //              2. Motes   Seeds every grid cell around the view and places its motes.
    //              3. Blobs   Seeds every fifth cell and places a faint soft blob close to the camera.
    //

    private Color _light = SoupPalette.SEA.Light;

    /// <summary>Colors the water: sea floor patches, the fade into the depth, motes and blobs.</summary>
    /// <param name="palette">(Required) The colors of the current stage, FollowCamera eases between them.</param>
    public void Tint(SoupPalette palette)
    {
        _light = palette.Light;
        _floorMaterial.SetShaderParameter("shallow_color", palette.FloorShallow);
        _floorMaterial.SetShaderParameter("deep_color", palette.FloorDeep);
        _floorMaterial.SetShaderParameter("fog_color", palette.Background);
        _floorMaterial.SetShaderParameter("caustic_color", palette.Caustics);
    }

    /// <summary>Moves the sea floor and the motes around the view, called every frame.</summary>
    /// <param name="center">(Required) Plane point the camera looks at.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), sets the depths.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, sets extent and density.</param>
    public void Follow(Vector2 center, float playerSize, float viewRadius)
    {
        // ---------------------------------------------------------------------------
        // Floor
        //   Centers the floor below the view and scales its pattern with the view.
        // ---------------------------------------------------------------------------

        _floor.Position = Plane3D.To3D(center, -playerSize * FLOOR_DEPTH);
        _floor.Scale    = new Vector3(viewRadius * 4f, 1f, viewRadius * 4f);
        _floorMaterial.SetShaderParameter("world_scale", viewRadius * 2f);

        // ---------------------------------------------------------------------------
        // Motes
        //   Seeds every grid cell around the view and places its motes.
        // ---------------------------------------------------------------------------

        // Cell size rounded to a power of two: constant density at every zoom
        float cell  = Mathf.Pow(2f, Mathf.Round(Mathf.Log(viewRadius * 2f / GRID_CELLS) / Mathf.Log(2f)));
        float time  = Time.GetTicksMsec() / 1000f;
        float reach = viewRadius * 1.3f;
        int   x0    = Mathf.FloorToInt((center.X - reach) / cell);
        int   x1    = Mathf.CeilToInt((center.X + reach) / cell);
        int   y0    = Mathf.FloorToInt((center.Y - reach) / cell);
        int   y1    = Mathf.CeilToInt((center.Y + reach) / cell);

        var rng   = new RandomNumberGenerator();
        int count = 0;
        for (int gx = x0; gx <= x1 && count < MAX_MOTES; gx++)
        {
            for (int gy = y0; gy <= y1 && count < MAX_MOTES; gy++)
            {
                // Same cell and size -> same seed -> same motes every frame
                rng.Seed = (ulong)(uint)System.HashCode.Combine(gx, gy, (int)cell);
                for (int k = 0; k < MOTES_PER_CELL && count < MAX_MOTES; k++)
                {
                    var   point = new Vector2(gx + rng.Randf(), gy + rng.Randf()) * cell;
                    float phase = rng.Randf() * Mathf.Tau;
                    point += new Vector2(Mathf.Sin(time * 0.4f + phase), Mathf.Cos(time * 0.3f + phase)) * cell * 0.08f;
                    float depth = rng.RandfRange(-FLOOR_DEPTH * 0.9f, 0.8f) * playerSize;
                    float size  = rng.RandfRange(0.6f, 2.2f) * cell / 50f;
                    _motes.SetInstanceTransform(count, new Transform3D(Basis.FromScale(Vector3.One * size), Plane3D.To3D(point, depth)));
                    _motes.SetInstanceColor(count, new Color(_light, rng.RandfRange(0.08f, 0.25f)));
                    count++;
                }
            }
        }
        _motes.VisibleInstanceCount = count;

        // ---------------------------------------------------------------------------
        // Blobs
        //   Seeds every fifth cell and places a faint soft blob close to the camera.
        // ---------------------------------------------------------------------------

        float blobCell = cell * BLOB_GRID;
        int   blobs    = 0;
        for (int gx = Mathf.FloorToInt((center.X - reach) / blobCell); gx <= Mathf.CeilToInt((center.X + reach) / blobCell) && blobs < MAX_BLOBS; gx++)
        {
            for (int gy = Mathf.FloorToInt((center.Y - reach) / blobCell); gy <= Mathf.CeilToInt((center.Y + reach) / blobCell) && blobs < MAX_BLOBS; gy++)
            {
                rng.Seed = (ulong)(uint)System.HashCode.Combine(gx, gy, (int)blobCell, BLOB_GRID);
                var   point  = new Vector2(gx + rng.Randf(), gy + rng.Randf()) * blobCell;
                float phase  = rng.Randf() * Mathf.Tau;
                point += new Vector2(Mathf.Sin(time * 0.25f + phase), Mathf.Cos(time * 0.2f + phase)) * blobCell * 0.06f;
                float height = rng.RandfRange(BLOB_LOW, BLOB_HIGH) * playerSize;
                float size   = rng.RandfRange(0.3f, 0.7f) * cell;
                _blobs.SetInstanceTransform(blobs, new Transform3D(Basis.FromScale(Vector3.One * size), Plane3D.To3D(point, height)));
                _blobs.SetInstanceColor(blobs, new Color(_light, rng.RandfRange(0.012f, 0.03f)));
                blobs++;
            }
        }
        _blobs.VisibleInstanceCount = blobs;
    }
}
