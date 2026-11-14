// NOTE:
//   The 3D stage of the village builder: a SubViewport with an orbit camera, a patch of
//   ground with the grid on it and the draft of the session built from its blocks. It
//   answers which cell lies under a pixel (the first block the ray meets, else the
//   ground) and shows the picked block as a ghost where it would land. The stage reads
//   no input on its own: the BuildingEditor connects GuiInput and calls Press, Release,
//   Orbit, Zoom and Hover.
//
//   var stage = new BuildingStage();
//   stage.Setup(session);
//   stage.GuiInput += OnStageInput;
//   BuildingStage.Hit? hit = stage.CellAt(mousePosition);

//
//   Chapters:
//     BUILD STAGE      Viewport, camera, ground, grid and the draft.
//     ORBIT CAMERA     Press, drag and wheel turn and zoom the view.
//     PICK ON SCREEN   The cell and block under a pixel.
//     GHOST            The picked block where it would land.
//

using Godot;

namespace CellOut;

public partial class BuildingStage : SubViewportContainer
{
    // ===========================================================================
    // =============================   BUILD STAGE   =============================
    // ===========================================================================
    //
    //   Setup     Builds SubViewport, camera, ground, grid, the draft and follows the session.
    //   Rebuild   Builds the draft again after a change.
    //

    private const float CAMERA_FOV = 40f;
    private const float GROUND_RADIUS = 3.3f;
    private static readonly Color GROUND_COLOR = new(0.36f, 0.46f, 0.26f);
    private static readonly Color GRID_COLOR = new(0.9f, 0.95f, 0.8f, 0.22f);

    private BuildingEditorSession _session;
    private Camera3D _camera;
    private Node3D _scene;
    private BuildingVisual3D _building;

    /// <summary>Builds the stage, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting whose draft stands on the ground.</param>
    public void Setup(BuildingEditorSession session)
    {
        _session            = session;
        Stretch             = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical   = SizeFlags.ExpandFill;
        MouseFilter         = MouseFilterEnum.Stop;

        SubViewport viewport = PartIcon.NewStudio(new Vector2I(640, 640));
        AddChild(viewport);
        _camera = new Camera3D { Fov = CAMERA_FOV, Current = true };
        viewport.AddChild(_camera);
        _scene = new Node3D();
        viewport.AddChild(_scene);

        // A round patch of grass, the grid of the cells drawn on it
        MeshInstance3D ground = CellVisual3D.AddMesh(_scene,
            new CylinderMesh { TopRadius = GROUND_RADIUS, BottomRadius = GROUND_RADIUS * 1.03f, Height = 0.16f, RadialSegments = 48, Rings = 1 },
            CellVisual3D.Material(GROUND_COLOR, 0f, 1f), new Vector3(0f, -0.08f, 0f), Vector3.One);
        ground.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var   line  = new BoxMesh { Size = Vector3.One };
        float width = BuildingDesign.GRID_SIZE * BuildingVisual3D.CELL;
        for (int i = 0; i <= BuildingDesign.GRID_SIZE; i++)
        {
            float offset = i * BuildingVisual3D.CELL - width * 0.5f;
            foreach (bool isAlongX in new[] { true, false })
            {
                MeshInstance3D bar = CellVisual3D.AddMesh(_scene, line, CellVisual3D.Material(GRID_COLOR, 0f, 1f),
                    isAlongX ? new Vector3(0f, 0.004f, offset) : new Vector3(offset, 0.004f, 0f),
                    isAlongX ? new Vector3(width, 0.004f, 0.015f) : new Vector3(0.015f, 0.004f, width));
                bar.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }

        _building = new BuildingVisual3D();
        _scene.AddChild(_building);

        _ghost = new Node3D { Visible = false };
        _scene.AddChild(_ghost);

        _session.Changed          += Rebuild;
        _session.SelectionChanged += RebuildGhost;
        ResetCamera();
    }

    /// <summary>Builds the draft of the function shown again.</summary>
    public void Rebuild()
    {
        _building.Build(_session.Draft);
    }

    // ===========================================================================
    // ============================   ORBIT CAMERA   =============================
    // ===========================================================================
    //
    //   IsPressed     A left press is held: a drag turns the view.
    //   Press         A left press went down.
    //   Release       The left button went up; true for a click without rotation.
    //   Orbit         Turns the view once the drag passed the threshold.
    //   Zoom          One wheel notch closer or farther.
    //   ResetCamera   From the front right, slightly above.
    //   UpdateCamera  Orbit position from yaw, pitch and zoom.
    //

    private const float CAMERA_DISTANCE = 10.5f;
    private const float START_YAW = 0.7f;
    private const float START_PITCH = 0.5f;
    private const float MIN_PITCH = 0.08f;           // never below the ground
    private const float MAX_PITCH = 1.45f;
    private const float ORBIT_SPEED = 0.008f;
    private const float DRAG_THRESHOLD = 5f;
    private const float ZOOM_STEP = 1.1f;
    private const float MIN_ZOOM = 0.45f;
    private const float MAX_ZOOM = 1.6f;
    private static readonly Vector3 CAMERA_TARGET = new(0f, 1.4f, 0f);

    private float _yaw;
    private float _pitch;
    private float _zoom = 1f;
    private bool _isLeftDown;
    private bool _isOrbiting;
    private Vector2 _leftDownAt;

    public bool IsPressed => _isLeftDown;

    public void Press(Vector2 pixel)
    {
        _isLeftDown = true;
        _isOrbiting = false;
        _leftDownAt = pixel;
    }

    /// <summary>Ends the left press.</summary>
    /// <returns>True for a click: a press that did not turn the view.</returns>
    public bool Release()
    {
        bool isClick = _isLeftDown && !_isOrbiting;
        _isLeftDown = false;
        _isOrbiting = false;
        return isClick;
    }

    /// <summary>Left drag: rotate once past the threshold.</summary>
    public void Orbit(InputEventMouseMotion motion)
    {
        _isOrbiting = _isOrbiting || motion.Position.DistanceTo(_leftDownAt) > DRAG_THRESHOLD;
        if (_isOrbiting)
        {
            _yaw  -= motion.Relative.X * ORBIT_SPEED;
            _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * ORBIT_SPEED, MIN_PITCH, MAX_PITCH);
            UpdateCamera();
        }
    }

    /// <summary>Zooms by one wheel notch.</summary>
    /// <param name="notch">(Required) 1 wheel up (closer), -1 wheel down (farther).</param>
    public void Zoom(float notch)
    {
        _zoom = Mathf.Clamp(_zoom * (notch > 0f ? 1f / ZOOM_STEP : ZOOM_STEP), MIN_ZOOM, MAX_ZOOM);
        UpdateCamera();
    }

    public void ResetCamera()
    {
        _yaw   = START_YAW;
        _pitch = START_PITCH;
        _zoom  = 1f;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        Vector3 offset = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * CAMERA_DISTANCE * _zoom;
        _camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), CAMERA_TARGET + offset);
    }

    // ===========================================================================
    // ===========================   PICK ON SCREEN   ============================
    // ===========================================================================
    //
    //   Hit      A pointed cell and the block that fills it, -1 for the bare ground.
    //   CellAt   The cell under a pixel: the first block on the ray, else the ground.
    //

    public readonly record struct Hit(int X, int Z, int Block);

    private const float RAY_STEP = 0.04f;            // model units, well below half a cell

    /// <summary>
    /// The ray of the pixel is walked in small steps from the camera: the first step
    /// inside a block gives its cell (a dropped block then lands on top of it). A ray
    /// that reaches the ground first gives the ground cell there.
    /// </summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    /// <returns>The cell, null when the ray leaves the grid without meeting anything.</returns>
    public Hit? CellAt(Vector2 pixel)
    {
        Vector3 origin    = _camera.ProjectRayOrigin(pixel);
        Vector3 direction = _camera.ProjectRayNormal(pixel);
        float   reach     = CAMERA_DISTANCE * _zoom * 2f;
        float   half      = BuildingDesign.GRID_SIZE * 0.5f;
        for (float walked = 0f; walked < reach; walked += RAY_STEP)
        {
            Vector3 point = (origin + direction * walked) / BuildingVisual3D.CELL;
            int x = Mathf.FloorToInt(point.X + half);
            int z = Mathf.FloorToInt(point.Z + half);
            bool isOnGrid = x >= 0 && z >= 0 && x < BuildingDesign.GRID_SIZE && z < BuildingDesign.GRID_SIZE;
            if (point.Y < 0f)
            {
                return isOnGrid ? new Hit(x, z, -1) : null;
            }
            if (!isOnGrid)
            {
                continue;
            }
            int block = _session.Draft.BlockAt(x, Mathf.FloorToInt(point.Y), z);
            if (block >= 0)
            {
                return new Hit(x, z, block);
            }
        }
        return null;
    }

    // ===========================================================================
    // ================================   GHOST   ================================
    // ===========================================================================
    //
    //   Hover          Moves the ghost to where the picked block would land from a pixel.
    //   HideGhost      The pointer left the stage.
    //   RebuildGhost   The picked block, its turn or paint changed.
    //   _Process       The ghost bobs so it reads as not yet placed.
    //

    private const float GHOST_BOB = 0.05f;

    private Node3D _ghost;
    private Vector3 _ghostRest;
    private Vector2? _hoverPixel;
    private float _clock;

    /// <summary>Shows the picked block as a ghost where a click at the pixel would drop it.</summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    public void Hover(Vector2 pixel)
    {
        _hoverPixel    = pixel;
        _ghost.Visible = false;
        if (_session.Selected is not BlockKind kind || CellAt(pixel) is not Hit hit)
        {
            return;
        }
        if (_session.AnchorFor(hit.X, hit.Z) is not (int x, int z) || _session.Draft.RestHeight(kind, x, z, _session.Turns) is not int y)
        {
            return;
        }
        BuildingVisual3D.Place(_ghost, kind, x, y, z, _session.Turns);
        _ghostRest     = _ghost.Position;
        _ghost.Visible = true;
    }

    public void HideGhost()
    {
        _hoverPixel    = null;
        _ghost.Visible = false;
    }

    private void RebuildGhost()
    {
        foreach (Node child in _ghost.GetChildren())
        {
            child.QueueFree();
        }
        if (_session.Selected is BlockKind kind)
        {
            _ghost.AddChild(BuildingVisual3D.NewBlock(kind, _session.Paint));
        }
        if (_hoverPixel is Vector2 pixel)
        {
            Hover(pixel);
        }
        else
        {
            _ghost.Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        _clock += (float)delta;
        if (_ghost.Visible)
        {
            _ghost.Position = _ghostRest + Vector3.Up * (GHOST_BOB + GHOST_BOB * Mathf.Sin(_clock * 5f));
        }
    }
}
