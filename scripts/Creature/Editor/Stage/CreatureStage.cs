// NOTE:
//   The 3D stage of the creature editor: a SubViewport with an orbit camera, the
//   sculptor's turntable and the clay of the draft on it. It answers what lies under a
//   pixel (a part, a point of the body, a joint), shows the marker where a part or the
//   spray paint would land and celebrates a change with a punch and a ring.
//   The stage reads no input on its own: the CreatureEditor connects GuiInput and calls
//   Press, Release, Orbit and Zoom. Handles, gizmo and drag ghost are separate components
//   that stand next to the clay (AddToScene).
//
//   var stage = new CreatureStage();
//   stage.Setup(session);
//   stage.GuiInput += OnStageInput;
//   frame.AddWorkbench(palette, stage, stats);
//   Vector3? hit = stage.RayToBody(mousePosition);

//
//   Chapters:
//     BUILD STAGE     Viewport, camera, turntable, clay and marker.
//     ORBIT CAMERA    Press, drag and wheel turn and zoom the view.
//     PICK ON SCREEN  The part, body point or joint under a pixel.
//     MARK AND CHEER  Landing marker, spray nozzle and the ring burst.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreatureStage : SubViewportContainer
{
    // ===========================================================================
    // =============================   BUILD STAGE   =============================
    // ===========================================================================
    //
    //   Camera        The orbit camera, for projections of the components.
    //   Clay          The visual of the draft, for part anchors and limb frames.
    //   GlowMaterial  Warm glow shared by the marker and the vertebra handles.
    //   Clock         Seconds the stage ran, drives breathing handles and the marker.
    //   Setup         Builds SubViewport, camera, turntable, the clay and the marker.
    //   AddToScene    Puts a 3D helper (handles, gizmo, ghost) next to the clay.
    //   ShowDraft     Shows the draft of a new sitting in its color.
    //   Recolor       Takes over the color of the draft.
    //   Advance       Runs the clock and turns the turntable.
    //

    private const float CAMERA_FOV = 40f;
    private const float TURNTABLE_RADIUS = 1.7f;
    private const float TURNTABLE_SPEED = 0.15f;

    private CreatureEditorSession _session;
    private Camera3D _camera;
    private Node3D _scene;
    private Node3D _turntable;
    private CreatureVisual3D _clay;
    private readonly StandardMaterial3D _glowMaterial = new()
    {
        AlbedoColor              = new Color(1f, 0.95f, 0.8f),
        EmissionEnabled          = true,
        Emission                 = new Color(1f, 0.85f, 0.55f),
        EmissionEnergyMultiplier = 1.2f,
    };

    public Camera3D Camera => _camera;
    public CreatureVisual3D Clay => _clay;
    public StandardMaterial3D GlowMaterial => _glowMaterial;
    public float Clock { get; private set; }

    /// <summary>Builds the stage, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting whose draft stands on the turntable.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session            = session;
        Stretch             = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical   = SizeFlags.ExpandFill;
        MouseFilter         = MouseFilterEnum.Stop;

        SubViewport viewport = PartIcon.NewStudio(new Vector2I(440, 440));
        AddChild(viewport);
        _camera = new Camera3D { Fov = CAMERA_FOV, Current = true };
        viewport.AddChild(_camera);
        _scene = new Node3D();
        viewport.AddChild(_scene);

        // Sculptor's turntable: a slowly turning slab with a lighter rim and a few studs
        _turntable = new Node3D { Position = new Vector3(0f, -0.95f, 0f) };
        _scene.AddChild(_turntable);
        MeshInstance3D slab = CellVisual3D.AddMesh(_turntable,
            new CylinderMesh { TopRadius = TURNTABLE_RADIUS, BottomRadius = TURNTABLE_RADIUS * 1.04f, Height = 0.12f, RadialSegments = 48, Rings = 1 },
            CellVisual3D.Material(new Color(0.16f, 0.2f, 0.3f), 0f, 0.7f), Vector3.Zero, Vector3.One);
        slab.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        CellVisual3D.AddMesh(_turntable, CellVisual3D.TorusMesh(), CellVisual3D.Material(EditorStyle.RIM_COLOR, 0.3f, 0.4f),
            new Vector3(0f, 0.06f, 0f), new Vector3(TURNTABLE_RADIUS, 0.5f, TURNTABLE_RADIUS));
        for (int i = 0; i < 12; i++)
        {
            float angle = Mathf.Tau * i / 12f;
            CellVisual3D.AddMesh(_turntable, CellVisual3D.SphereMesh(true), CellVisual3D.Material(EditorStyle.RIM_COLOR, 0.6f),
                new Vector3(Mathf.Cos(angle), 0.07f, Mathf.Sin(angle)) * (TURNTABLE_RADIUS * 0.82f), Vector3.One * 0.03f);
        }

        // The clay, nearly still so the handles stay on the back
        _clay = new CreatureVisual3D { Design = _session.Draft, Knead = CreatureVisual3D.EDITOR_KNEAD, SwimScale = 0.3f, Seed = 7f };
        _scene.AddChild(_clay);

        // Marker where a selected part would land
        _marker = CellVisual3D.AddMesh(_scene, CellVisual3D.SphereMesh(true), _glowMaterial, Vector3.Zero, Vector3.One * 0.07f);
        _marker.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _marker.Visible    = false;
    }

    /// <summary>Adds a 3D helper to the scene of the clay, in the same unit space.</summary>
    /// <example><code>stage.AddToScene(handles);</code></example>
    public void AddToScene(Node3D helper)
    {
        _scene.AddChild(helper);
    }

    /// <summary>Puts the draft of the session on the turntable, in its color. Called when a sitting begins.</summary>
    public void ShowDraft()
    {
        _clay.Design    = _session.Draft;
        _clay.BodyColor = _session.DraftColor;
    }

    /// <summary>Takes over the color of the draft.</summary>
    public void Recolor()
    {
        _clay.BodyColor = _session.DraftColor;
    }

    /// <summary>Runs the clock and turns the turntable, called every frame while the editor shows.</summary>
    /// <param name="delta">(Required) Seconds since the last frame.</param>
    public void Advance(float delta)
    {
        Clock += delta;
        _turntable.RotateY(delta * TURNTABLE_SPEED);
    }

    // ===========================================================================
    // ============================   ORBIT CAMERA   =============================
    // ===========================================================================
    //
    //   IsPressed     A left press on free space is held: a drag turns the view.
    //   FacingFlank   The flank of the body that faces the camera.
    //   Press         A left press went down, on free space or on a handle.
    //   Release       The left button went up; true for a click without rotation.
    //   Orbit         Turns the view once the drag passed the threshold.
    //   Zoom          One wheel notch closer or farther.
    //   ResetCamera   Side view from slightly above, forward to the right.
    //   UpdateCamera  Orbit position from yaw, pitch and zoom.
    //

    private const float CAMERA_DISTANCE = 4.6f;
    private const float START_PITCH = 0.35f;
    private const float MIN_PITCH = -1.2f;
    private const float MAX_PITCH = 1.45f;
    private const float ORBIT_SPEED = 0.008f;
    private const float DRAG_THRESHOLD = 5f;
    private const float ZOOM_STEP = 1.1f;
    private const float MIN_ZOOM = 0.55f;
    private const float MAX_ZOOM = 1.8f;

    private float _yaw;
    private float _pitch;
    private float _zoom = 1f;
    private bool _isLeftDown;
    private bool _isOrbiting;
    private Vector2 _leftDownAt;

    public bool IsPressed => _isLeftDown;

    /// <summary>-1 when the camera looks at the left flank, else 1.</summary>
    public int FacingFlank => _camera.Position.Z < 0f ? -1 : 1;

    /// <summary>Notes a left press. Only a press on free space may become a click or a rotation.</summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    /// <param name="isFree">(Required) False when a gizmo, joint or vertebra handle took the press.</param>
    public void Press(Vector2 pixel, bool isFree)
    {
        _isLeftDown = isFree;
        _isOrbiting = false;
        _leftDownAt = pixel;
    }

    /// <summary>Ends the left press.</summary>
    /// <returns>True for a click: a press on free space that did not turn the view.</returns>
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
        _yaw   = 0f;
        _pitch = START_PITCH;
        _zoom  = 1f;
        UpdateCamera();
    }

    /// <summary>Yaw 0 looks from +Z at the flank, so screen right is +X (forward).</summary>
    private void UpdateCamera()
    {
        float distance = CAMERA_DISTANCE * _zoom;
        Vector3 offset = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * distance;
        _camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), offset);
    }

    // ===========================================================================
    // ===========================   PICK ON SCREEN   ============================
    // ===========================================================================
    //
    //   PartAt         The placed part under a pixel and the flank hit, -1 for none.
    //   RayToBody      First point of the body under a pixel (ray marching).
    //   NearestJoint   The joint of a placed limb closest to a pixel.
    //

    private const int RAY_STEPS = 240;

    /// <summary>The placed part under a pixel.</summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    /// <param name="flank">(Required) Receives the flank of the half that was hit.</param>
    /// <returns>Index in Draft.Parts, -1 for none.</returns>
    public int PartAt(Vector2 pixel, out int flank)
    {
        return _clay.PartAt(_camera.ProjectRayOrigin(pixel), _camera.ProjectRayNormal(pixel), out flank);
    }

    /// <summary>
    /// The lofted body has no simple formula for a ray hit, so the ray is walked in
    /// small steps through the box around the camera target until the first step lands
    /// inside (CreatureDesign.Contains). Precise enough to pick a spot on the spine.
    /// </summary>
    /// <returns>The first point inside the body, null when the ray misses it.</returns>
    public Vector3? RayToBody(Vector2 pixel)
    {
        Vector3 origin    = _camera.ProjectRayOrigin(pixel);
        Vector3 direction = _camera.ProjectRayNormal(pixel);
        float   reach     = CAMERA_DISTANCE * _zoom * 2f;
        float   step      = reach / RAY_STEPS;
        for (int i = 0; i < RAY_STEPS; i++)
        {
            Vector3 point = origin + direction * (i * step);
            if (_session.Draft.Contains(point))
            {
                return point;
            }
        }
        return null;
    }

    /// <summary>The joint of a placed limb closest to a pixel on screen, the tip when none can be seen.</summary>
    /// <param name="index">(Required) Index in Draft.Parts of a limb with joints.</param>
    /// <param name="flank">(Required) Flank of the half that was hit, 0 for the right one.</param>
    /// <param name="pixel">(Required) Position in the stage.</param>
    public int NearestJoint(int index, int flank, Vector2 pixel)
    {
        IReadOnlyList<Vector3> joints = _session.Draft.Parts[index].Joints;
        int                    nearest = joints.Count - 1;
        if (!_clay.TryGetLimbFrame(index, 0 == flank ? 1 : flank, out Transform3D frame)
            && !_clay.TryGetLimbFrame(index, -flank, out frame))
        {
            return nearest;
        }
        float best = float.PositiveInfinity;
        for (int i = 0; i < joints.Count; i++)
        {
            Vector3 point = frame * joints[i];
            if (_camera.IsPositionBehind(point))
            {
                continue;
            }
            float distance = _camera.UnprojectPosition(point).DistanceTo(pixel);
            if (distance < best)
            {
                best    = distance;
                nearest = i;
            }
        }
        return nearest;
    }

    // ===========================================================================
    // ===========================   MARK AND CHEER   ============================
    // ===========================================================================
    //
    //   ShowLanding    Sets or clears the point where a selected or dragged part would land.
    //   UpdateMarker   Shows the marker as spray nozzle or at the landing point.
    //   Celebrate      Punches the clay and sends a ring out from a point.
    //

    private const float BURST_SECONDS = 0.45f;

    private MeshInstance3D _marker;
    private Vector3? _landing;

    /// <summary>Sets where a selected or dragged part would land, null hides the marker.</summary>
    /// <param name="point">(Required) Point on the clay, unit space, or null.</param>
    public void ShowLanding(Vector3? point)
    {
        _landing = point;
    }

    /// <summary>
    /// Marker: the spray nozzle where the paint lands (as big as the nozzle), else where a
    /// selected part would land. Called every frame.
    /// </summary>
    /// <param name="nozzle">(Required) Where the spray can points on the body, null when it is off or beside the body.</param>
    /// <param name="nozzleRadius">(Required) Radius of the nozzle, unit space.</param>
    public void UpdateMarker(Vector3? nozzle, float nozzleRadius)
    {
        if (nozzle is Vector3 sprayed)
        {
            _marker.Visible  = true;
            _marker.Position = sprayed;
            _marker.Scale    = Vector3.One * nozzleRadius * 0.45f;
            return;
        }
        _marker.Visible = null != _landing;
        if (_landing is Vector3 point)
        {
            _marker.Position = point;
            _marker.Scale    = Vector3.One * (0.07f + 0.015f * Mathf.Sin(Clock * 8f));
        }
    }

    /// <summary>Cheers a change: the clay punches out and springs back, a ring bursts from a point.</summary>
    /// <param name="point">(Required) Center of the ring, unit space.</param>
    /// <param name="color">(Required) Color of the ring.</param>
    /// <param name="punch">(Required) Scale the clay punches to, above 1 grows, below 1 shrinks.</param>
    /// <example><code>stage.Celebrate(Vector3.Zero, CreatureEditorUi.TypeAccent(type), 1.12f);</code></example>
    public void Celebrate(Vector3 point, Color color, float punch)
    {
        // Clay punch: out and elastic back
        Tween bounce = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        bounce.TweenProperty(_clay, "scale", Vector3.One * punch, 0.08f);
        bounce.TweenProperty(_clay, "scale", Vector3.One, 0.35f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

        // Ring burst at the point, its own material so the fade does not touch other rings
        var material = new StandardMaterial3D
        {
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor  = color,
        };
        MeshInstance3D ring = CellVisual3D.AddMesh(_scene, CellVisual3D.TorusMesh(), material, point, Vector3.One * 0.1f);
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Tween burst = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        burst.TweenMethod(Callable.From<float>(t =>
        {
            ring.Scale           = new Vector3(1f, 0.4f, 1f) * Mathf.Lerp(0.1f, 0.8f, t);
            material.AlbedoColor = new Color(color, 1f - t);
        }), 0f, 1f, BURST_SECONDS).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        burst.TweenCallback(Callable.From(ring.QueueFree));
    }
}
