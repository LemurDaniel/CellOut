// NOTE:
//   The primordial soup in 3D: a sea floor with caustics below the plane and
//   floating motes at different depths (parallax). Motes are placed per grid cell
//   with a seeded generator, so the endless world needs no stored objects.
//   Main calls Follow once per frame.

//
//   Chapters:
//     BUILD SCENERY   Sea floor with the ocean shader and the mote multimesh.
//     FOLLOW VIEW     Moves the floor and refills the motes around the view.
//

using Godot;

namespace CellOut;

public partial class Backdrop : Node3D
{
    private const int MAX_MOTES = 900;
    private const int MOTES_PER_CELL = 2;
    private const float FLOOR_DEPTH = 3f;      // sea floor depth below the plane in player sizes
    private const float GRID_CELLS = 9f;       // grid cells across the view diameter
    private static readonly Shader OCEAN_SHADER = GD.Load<Shader>("res://shaders/ocean.gdshader");

    // ===========================================================================
    // ============================   BUILD SCENERY   ============================
    // ===========================================================================
    //
    //   1. Sea floor   A plane with the ocean shader, casts no shadow but receives them.
    //   2. Motes       One multimesh with vertex colors, filled in FOLLOW VIEW.
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
    }

    private MultiMeshInstance3D _moteInstance;

    /// <summary>Motes float in the soup, over the open sea of the third person view they would hang in the air.</summary>
    public bool IsMotesShown
    {
        get => null == _moteInstance || _moteInstance.Visible;
        set
        {
            if (null != _moteInstance)
            {
                _moteInstance.Visible = value;
            }
        }
    }

    // ===========================================================================
    // =============================   FOLLOW VIEW   =============================
    // ===========================================================================
    //
    //   1. Floor   Centers the floor below the view and scales its pattern with the view.
    //   2. Motes   Seeds every grid cell around the view and places its motes.
    //

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
                    _motes.SetInstanceColor(count, new Color(0.7f, 0.95f, 0.9f, rng.RandfRange(0.08f, 0.25f)));
                    count++;
                }
            }
        }
        _motes.VisibleInstanceCount = count;
    }
}
