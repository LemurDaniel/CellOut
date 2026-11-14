// NOTE:
//   Evolve animation from the full cluster to the clay blob: the 64 cells keep dividing,
//   128, 256, 512, while the flat cluster curls up into a hollow ball (a blastula). Then
//   the cells melt into one lump of modelling clay that wobbles and settles. Runs while
//   the game is paused; after Finished the blob keeps breathing behind the creature
//   editor until Main frees the cutscene.
//
//   var cutscene = new EvolveCutscene { Camera = camera, Center = center, Size = player.BodySize(), BodyColor = player.BodyColor };
//   AddChild(cutscene);
//   cutscene.Finished += OnEvolveCutsceneFinished;

//
//   Chapters:
//     SETTINGS        Inputs from Main, the clay color and the timeline.
//     BUILD           Cell multimesh and the hidden clay blob.
//     PLAY TIMELINE   Camera, divisions into a ball, melting into clay, settling.
//     HELPERS         Cell positions per cluster size.
//

using Godot;

namespace CellOut;

public partial class EvolveCutscene : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Camera      (Required) The world camera, flown closer by the cutscene.
    //   Center      (Required) Center of the player in 3D.
    //   Size        (Required) Body size of the player, everything scales with it.
    //   BodyColor   (Required) Color of the cells, they melt into ClayColor.
    //   ClayColor   The coral of the blob, the creature's default color (Player.ORGANISM_COLOR).
    //   VisibleCells  Cells shown right now, 64 up to 512.
    //   Finished    The blob has settled, Main opens the creature editor.
    //
    //   Timeline in seconds:
    //     0.0 - 2.6   camera eases closer and lower
    //     0.5 - 3.0   three divisions: 64 -> 128 -> 256 -> 512 cells
    //     0.5 - 3.2   the flat cluster curls up into a hollow ball
    //     3.3 - 4.2   the cells melt, the clay blob swells out of them
    //     4.2 - 5.4   the blob wobbles and settles
    //     5.4         Finished, the blob keeps breathing
    //

    [Signal] public delegate void FinishedEventHandler();

    public Camera3D Camera { get; set; }
    public Vector3 Center { get; set; }
    public float Size { get; set; } = 14f;
    public Color BodyColor { get; set; } = new(0.45f, 0.85f, 0.65f);
    public Color ClayColor => Player.ORGANISM_COLOR;

    private const float DURATION = 5.4f;
    private const float CAMERA_END = 2.6f;
    private const float DIVIDE_START = 0.5f;
    private const float DIVIDE_STEP = 0.85f;         // one division after the other
    private const float SPLIT_SECONDS = 0.7f;
    private const float BALL_END = 3.2f;
    private const float MELT_START = 3.3f;
    private const float MELT_END = 4.2f;
    private const float BALL_RADIUS = 0.55f;         // hollow ball, in body sizes
    private const float BLOB_RADIUS = 0.62f;         // the settled clay blob, in body sizes
    private const float CAMERA_DISTANCE = 5.5f;      // in body sizes
    private const float CAMERA_TILT = 0.85f;         // radians from vertical, lower than in game
    private static readonly int DIVISIONS = System.Numerics.BitOperations.Log2(Player.ORGANISM_CELLS / Player.MAX_CELLS);

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready   Keeps running while paused, builds the cells and the blob.
    //

    private static readonly Shader CLAY_SHADER = GD.Load<Shader>("res://shaders/clay.gdshader");

    private MultiMesh _cells;
    public int VisibleCells => _cells.VisibleInstanceCount;
    private MeshInstance3D _blob;
    private ShaderMaterial _clay;
    private Color[] _cellTints;
    private Transform3D _cameraStart;
    private Transform3D _cameraEnd;
    private float _time;
    private bool _isFinished;

    public override void _Ready()
    {
        ProcessMode  = ProcessModeEnum.Always;
        Position     = Center;
        _cameraStart = Camera.GlobalTransform;
        Vector3 offset = new Vector3(0f, Mathf.Cos(CAMERA_TILT), Mathf.Sin(CAMERA_TILT)) * Size * CAMERA_DISTANCE;
        _cameraEnd = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), Center + offset);

        // Cells: one multimesh with room for the last division, each cell a little lighter or darker
        _cells = new MultiMesh
        {
            TransformFormat     = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors           = true,
            InstanceCount       = Player.ORGANISM_CELLS,
            VisibleInstanceCount = Player.MAX_CELLS,
            Mesh                = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 },
        };
        _cellTints = new Color[Player.ORGANISM_CELLS];
        for (int i = 0; i < _cellTints.Length; i++)
        {
            _cellTints[i] = BodyColor.Lightened(Rand.Range(-0.1f, 0.15f));
        }
        var cellMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness              = 0.3f,
            RimEnabled             = true,
            Rim                    = 0.5f,
        };
        AddChild(new MultiMeshInstance3D { Multimesh = _cells, MaterialOverride = cellMaterial });

        // Clay blob, hidden until the cells melt
        _clay = new ShaderMaterial { Shader = CLAY_SHADER };
        _clay.SetShaderParameter("clay_color", ClayColor);
        _clay.SetShaderParameter("seed", Rand.Range(0f, 100f));
        _clay.SetShaderParameter("knead", 1f);
        _blob = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(), _clay, Vector3.Zero, Vector3.One * 0.001f);
        Log.Verbose("EvolveCutscene", $"Started: {Player.MAX_CELLS} -> {Player.ORGANISM_CELLS} cells, size {Size:F1}.");
        UpdateTimeline();
    }

    // ===========================================================================
    // ============================   PLAY TIMELINE   ============================
    // ===========================================================================
    //
    //   1. Camera     Eases into a closer, lower pose and orbits slowly.
    //   2. Cells      Divide three times while the cluster curls up into a ball.
    //   3. Clay       The cells shrink into the swelling blob.
    //   4. Settle     The wobble fades, Finished fires once, the blob keeps breathing.
    //

    public override void _Process(double delta)
    {
        _time += (float)delta;
        UpdateTimeline();
    }

    private void UpdateTimeline()
    {
        // ---------------------------------------------------------------------------
        // Camera
        //   Eases into a closer, lower pose and orbits slowly.
        // ---------------------------------------------------------------------------

        float flight = Ease.InOut(Mathf.Clamp(_time / CAMERA_END, 0f, 1f));
        Transform3D orbit = _cameraEnd;
        orbit.Origin = Center + (orbit.Origin - Center).Rotated(Vector3.Up, Mathf.Sin(_time * 0.25f) * 0.35f * flight);
        orbit.Basis  = Basis.LookingAt(Center - orbit.Origin, Vector3.Up);
        Camera.GlobalTransform = _cameraStart.InterpolateWith(orbit, flight);

        // ---------------------------------------------------------------------------
        // Cells
        //   Divide three times while the cluster curls up into a ball.
        // ---------------------------------------------------------------------------

        // Which division runs and how far: before the first 64 cells, after the last 512
        float sinceStart = _time - DIVIDE_START;
        int   division   = Mathf.Clamp(Mathf.FloorToInt(sinceStart / DIVIDE_STEP), 0, DIVISIONS - 1);
        float split      = sinceStart < 0f ? 0f : Mathf.Clamp((sinceStart - division * DIVIDE_STEP) / SPLIT_SECONDS, 0f, 1f);
        int   mothers    = Player.MAX_CELLS << division;
        int   daughters  = mothers * 2;
        float ball       = Ease.Smooth(Mathf.Clamp((_time - DIVIDE_START) / (BALL_END - DIVIDE_START), 0f, 1f));
        float melt       = Mathf.Clamp((_time - MELT_START) / (MELT_END - MELT_START), 0f, 1f);
        float glide      = Ease.BackOut(split);

        _cells.VisibleInstanceCount = split > 0f ? daughters : mothers;
        for (int i = 0; i < _cells.VisibleInstanceCount; i++)
        {
            // Daughter i glides out of mother i / 2
            bool    isSplitting = split > 0f;
            Vector3 from        = CellPosition(mothers, isSplitting ? i / 2 : i, ball);
            Vector3 to          = isSplitting ? CellPosition(daughters, i, ball) : from;
            float   radius      = isSplitting ? Mathf.Lerp(CellRadius(mothers, ball), CellRadius(daughters, ball), split) : CellRadius(mothers, ball);
            Vector3 position    = from.Lerp(to, glide) * (1f - melt * 0.6f);
            float   scale       = radius * (1f - Ease.Smooth(melt));
            _cells.SetInstanceTransform(i, new Transform3D(Basis.FromScale(Vector3.One * Mathf.Max(scale, 0.0001f)), position));
            _cells.SetInstanceColor(i, _cellTints[i].Lerp(ClayColor, melt));
        }

        // ---------------------------------------------------------------------------
        // Clay
        //   The cells shrink into the swelling blob.
        // ---------------------------------------------------------------------------

        float swell  = Ease.BackOut(melt);
        float settle = Mathf.Clamp((_time - MELT_END) / (DURATION - MELT_END), 0f, 1f);
        float blob   = Mathf.Max(Mathf.Lerp(BALL_RADIUS * 0.7f, BLOB_RADIUS, swell) * Size * Mathf.Min(melt * 4f, 1f), 0.001f);
        _blob.Scale = new Vector3(blob, blob * 0.9f, blob);
        _blob.RotateY((float)GetProcessDeltaTime() * 0.3f);
        _clay.SetShaderParameter("wobble", melt > 0f ? Mathf.Lerp(1.2f, 0.15f, settle) : 0f);

        // ---------------------------------------------------------------------------
        // Settle
        //   The wobble fades, Finished fires once, the blob keeps breathing.
        // ---------------------------------------------------------------------------

        if (_time >= DURATION && !_isFinished)
        {
            _isFinished = true;
            Log.Verbose("EvolveCutscene", "Clay blob settled -> creature editor.");
            EmitSignal(SignalName.Finished);
        }
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   CellPosition   Cell i of a cluster, between the flat dome and the hollow ball.
    //   CellRadius     Cell radius of a cluster, growing a little on the ball to close gaps.
    //
    //   The easing curves come from Core/Ease.cs.
    //

    /// <summary>
    /// Flat: the sunflower layout of CellBody.ClusterLayout, scaled so the cluster keeps
    /// the body size. Ball: the same cell on a sphere, the disk distance d (0 center,
    /// 1 edge) mapped to the polar angle acos(1 - 2 d²), which keeps the cells evenly
    /// spread (equal area), the center on top, the edge underneath.
    /// </summary>
    private Vector3 CellPosition(int count, int index, float ball)
    {
        Vector3 unit   = CellBody.ClusterLayout(count)[index];
        float   extent = CellBody.ClusterExtent(count);
        Vector3 flat   = unit * Size / extent;

        float   reach  = Mathf.Max(extent - 1f, 0.001f);
        float   d      = Mathf.Clamp(new Vector2(unit.X, unit.Z).Length() / reach, 0f, 1f);
        float   polar  = Mathf.Acos(1f - 2f * d * d);
        float   azimut = Mathf.Atan2(unit.Z, unit.X);
        Vector3 sphere = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimut), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimut)) * BALL_RADIUS * Size;
        return flat.Lerp(sphere, ball);
    }

    private float CellRadius(int count, float ball)
    {
        float flat   = Size / CellBody.ClusterExtent(count);
        float sphere = BALL_RADIUS * Size * 2.2f / Mathf.Sqrt(count);
        return Mathf.Lerp(flat, sphere, ball);
    }
}
