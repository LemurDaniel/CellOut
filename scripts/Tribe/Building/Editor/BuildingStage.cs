// NOTE:
//   The 3D stage of the village builder, the whole screen behind its panels and see-through
//   around its content (the editor shows the blurred game behind it): a round patch of
//   grass on sand, an orbit camera that turns freely, and on it the draft of the session
//   built from its blocks. It answers which cell lies under a pixel (the first block the
//   ray meets, else the ground; the raster of the cells is not drawn, wish of the user
//   2026-11-17) and which placed block shows there, puts the gizmo on the picked block and
//   shows the block picked from the palette as a ghost where it would land. The stage reads
//   no input on its own: the BuildingEditor connects GuiInput and calls Press, Release,
//   Orbit, Zoom and Hover.
//
//   var stage = new BuildingStage();
//   stage.Setup(session);
//   stage.GuiInput += OnStageInput;
//   BuildingStage.Hit? hit = stage.CellAt(mousePosition);

//
//   Chapters:
//     BUILD STAGE      Viewport, camera, the patch of ground and the draft.
//     ORBIT CAMERA     Press, drag and wheel turn and zoom the view.
//     PICK ON SCREEN   The cell and block under a pixel.
//     GIZMO            The move, turn and scale handles on the picked block.
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
    //   Setup     Builds SubViewport, camera, ground, the draft, the gizmo and follows the session.
    //   Rebuild   Builds the draft again after a change.
    //

    private const float CAMERA_FOV = 40f;
    private const float GROUND_RADIUS = BuildingDesign.GRID_SIZE * BuildingVisual3D.CELL * 0.82f;     // the corners of the grid just fit
    private static readonly Color SAND_COLOR = new(0.88f, 0.8f, 0.55f);
    private static readonly Color GROUND_COLOR = new(0.36f, 0.46f, 0.26f);

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

        SubViewport viewport = PartIcon.NewStudio(new Vector2I(1280, 720));
        AddChild(viewport);
        _camera = new Camera3D { Fov = CAMERA_FOV, Current = true };
        viewport.AddChild(_camera);
        _scene = new Node3D();
        viewport.AddChild(_scene);

        // A round patch of grass on a wider slab of sand
        CellVisual3D.AddMesh(_scene,
            new CylinderMesh { TopRadius = GROUND_RADIUS * 1.12f, BottomRadius = GROUND_RADIUS * 1.2f, Height = 0.22f, RadialSegments = 48, Rings = 1 },
            CellVisual3D.Material(SAND_COLOR, 0f, 1f), new Vector3(0f, -0.19f, 0f), Vector3.One)
            .CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        MeshInstance3D ground = CellVisual3D.AddMesh(_scene,
            new CylinderMesh { TopRadius = GROUND_RADIUS, BottomRadius = GROUND_RADIUS * 1.03f, Height = 0.16f, RadialSegments = 48, Rings = 1 },
            CellVisual3D.Material(GROUND_COLOR, 0f, 1f), new Vector3(0f, -0.08f, 0f), Vector3.One);
        ground.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _building = new BuildingVisual3D();
        _scene.AddChild(_building);

        _ghost = new Node3D { Visible = false };
        _scene.AddChild(_ghost);

        Gizmo = new PartGizmo { Camera = _camera };
        _scene.AddChild(Gizmo);

        _session.Changed          += Rebuild;
        _session.SelectionChanged += RebuildGhost;
        _session.SelectionChanged += PlaceGizmo;
        _session.Posed            += FollowPose;
        ResetCamera();
    }

    /// <summary>Builds the draft of the function shown again.</summary>
    public void Rebuild()
    {
        _building.Build(_session.Draft);
        PlaceGizmo();
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

    private const float CAMERA_DISTANCE = 15f;
    private const float START_YAW = 0.7f;
    private const float START_PITCH = 0.42f;
    private const float MIN_PITCH = -1.2f;           // free all around (wish of the user 2026-11-17): from below the eaves up to
    private const float MAX_PITCH = 1.55f;           // straight from above
    private const float ORBIT_SPEED = 0.008f;
    private const float DRAG_THRESHOLD = 5f;
    private const float ZOOM_STEP = 1.1f;
    private const float MIN_ZOOM = 0.35f;
    private const float MAX_ZOOM = 1.6f;
    private static readonly Vector3 CAMERA_TARGET = new(0f, 1.8f, 0f);

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
    //   CellAt   The cell under a pixel: the first block on the ray, else the ground (where a dropped block lands).
    //   BlockAt  The placed block that shows under a pixel (picking, removing).
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

    /// <summary>The placed block that shows under a pixel, wherever its pose put it.</summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    /// <returns>Index in Draft.Blocks, -1 for none.</returns>
    public int BlockAt(Vector2 pixel)
    {
        return _building.BlockAt(_camera.ProjectRayOrigin(pixel), _camera.ProjectRayNormal(pixel));
    }

    // ===========================================================================
    // ================================   GIZMO   ================================
    // ===========================================================================
    //
    //   Gizmo        The handles, the editor asks them what a drag does.
    //   PlaceGizmo   Shows the gizmo in the middle of the picked block, hides it without one.
    //   FollowPose   The picked block moved: only its transform and the gizmo follow.
    //

    public PartGizmo Gizmo { get; private set; }

    private void PlaceGizmo()
    {
        Vector3? center = _session.Picked is int index ? _building.CenterOf(index) : null;
        Gizmo.Visible = null != center;
        if (center is Vector3 at)
        {
            Gizmo.GlobalPosition = at;
            Gizmo.ShowHandles(PartGizmo.SHAPE_HANDLES);
        }
    }

    private void FollowPose()
    {
        if (_session.Picked is int index)
        {
            _building.Pose(index, _session.Draft.Blocks[index]);
            PlaceGizmo();
        }
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
            _ghost.AddChild(BuildingVisual3D.NewBlock(kind, _session.Paint, _session.Pattern));
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
