// NOTE:
//   Cell editor, opened by Main on every level up and stage advance while the game is
//   paused. The panel slides in from the left (background: shaders/editor_panel.gdshader,
//   a wobbling membrane edge), the paused game on the right is greyed out
//   (shaders/greyout.gdshader). The palette shows one live 3D icon tile per part
//   (PartIcon), tinted per diet, locked ones dimmed. Dragging a tile hangs the bare
//   part at the cursor; over the rotatable 3D cell it snaps as a ghost to where it
//   would go and is placed on release. A click on a tile selects it, hovering the cell
//   shows the ghost, a click places it. Dragging the membrane edge stretches or squashes
//   the cell right there (free form, no mirroring), the swatches above the cell recolor it. Left drag
//   elsewhere rotates, the wheel zooms, right click removes the closest part and refunds
//   it. "Done" commits the draft and slides the panel out.
//   Looks and tweens: EditorStyle.
//
//   editor.Open(player);   // later: Closed signal

//
//   Chapters:
//     STATE AND API   Signal, draft, points and the calls Main and the smoke test use.
//     SLIDE           Slides the panel in and out and fades the grey-out.
//     PREVIEW INPUT   Rotate, zoom, click and hover on the 3D cell.
//     DRAG AND GHOST  The bare part at the cursor, its ghost snapping onto the cell.
//     SHAPE AND COLOR Stretching the membrane by its edge and the color swatches.
//     FEEDBACK        Cell punch and ring burst on place and remove, the turning dish.
//     DIET TINT       The panel, dish and highlights take on a hint of the diet.
//     REFRESH         Palette tiles, points, diet preview and the info card.
//     BUILD UI        Grey-out, panel, palette, 3D preview scene and footer.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CellEditor : CanvasLayer
{
    // ===========================================================================
    // ============================   STATE AND API   ============================
    // ===========================================================================
    //
    //   IsOpen     Whether the editor is shown (false right on Finish, also while sliding out).
    //   Points     Evolution points left: the player's points minus what this session spent.
    //   Open       Copies the player's design into a draft and slides the editor in.
    //   Select     Picks a part from the palette.
    //   TryPlace   Places a part at a unit position if unlocked, affordable and free.
    //   RemoveAt   Removes the closest part and refunds it.
    //   Finish     Commits the draft and slides out.
    //

    [Signal] public delegate void ClosedEventHandler();

    private Player _player;
    private int _spent;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public CellDesign Draft { get; private set; } = new();
    public PartKind? Selected { get; private set; }
    public int Points => null == _player ? 0 : _player.EvolutionPoints - _spent;

    /// <summary>Shows the editor for the player, the game must already be paused.</summary>
    /// <param name="player">(Required) The player whose design and points are edited.</param>
    public void Open(Player player)
    {
        _player  = player;
        _spent   = 0;
        _isOpen  = true;
        _hovered = null;
        Selected = null;
        Draft    = new CellDesign();
        Draft.CopyFrom(player.Design);

        _previewBody.Design    = Draft;
        ShowColor();
        ResetCamera();

        _root.Visible = true;
        _lastPoints   = -1;
        Log.Verbose("CellEditor", $"Opened at stage {player.Stage} with {Points} EP.");
        BuildPalette();
        Refresh();
        SlideTo(1f);
    }

    /// <summary>Picks a part, null clears the selection. Only info card and tile states change.</summary>
    public void Select(PartKind? kind)
    {
        Selected = kind;
        UpdateInfo();
        UpdateTiles();
    }

    /// <summary>Places a part if it is unlocked, affordable and fits there.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="point">(Required) Position in unit space (core radius 1, +X forward).</param>
    /// <returns>True when the part was placed and paid.</returns>
    public bool TryPlace(PartKind kind, Vector2 point)
    {
        PartInfo info = PartCatalog.Get(kind);
        if (!PartCatalog.IsUnlocked(info, _player.Stage, _player.Diet))
        {
            Log.Verbose("CellEditor", $"{kind} is locked at diet {_player.Diet:F2} -> not placed.");
            return false;
        }
        if (info.Cost > Points)
        {
            Log.Verbose("CellEditor", $"{kind} costs {info.Cost} EP, only {Points} left -> not placed.");
            return false;
        }
        if (!Draft.TryPlace(kind, point, info.Cost))
        {
            return false;
        }
        _spent += info.Cost;
        HideGhost();
        Celebrate(Draft.Parts[^1].Position, EditorStyle.DIET_COLORS[info.Diet], 1.1f);
        Refresh();
        return true;
    }

    /// <summary>Removes the part closest to a unit position and refunds what was paid for it.</summary>
    /// <returns>True when a part was removed.</returns>
    public bool RemoveAt(Vector2 point)
    {
        PlacedPart? removed = Draft.RemoveNear(point);
        if (null == removed)
        {
            return false;
        }
        _spent -= removed.Value.PaidCost;
        Celebrate(removed.Value.Position, new Color(0.75f, 0.85f, 0.9f), 0.93f);
        Refresh();
        return true;
    }

    /// <summary>Commits the draft and the points left to the player, the game resumes while the panel slides out.</summary>
    public void Finish()
    {
        _player.CommitDesign(Draft, Points);
        _isOpen = false;
        SlideTo(0f);
        EmitSignal(SignalName.Closed);
    }

    // ===========================================================================
    // ================================   SLIDE   ================================
    // ===========================================================================
    //
    //   SlideTo     Tweens the panel and the grey-out to open (1) or closed (0).
    //   ApplySlide  Sizes the panel to its screen share, moves it by its own width
    //               and sets the grey-out amount.
    //

    private const float SLIDE_SECONDS = 0.4f;

    private Tween _slideTween;
    private float _slide;

    private void SlideTo(float target)
    {
        _slideTween?.Kill();
        _slideTween = CreateTween()
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .SetTrans(target > 0f ? Tween.TransitionType.Quart : Tween.TransitionType.Cubic)
            .SetEase(target > 0f ? Tween.EaseType.Out : Tween.EaseType.In);
        _slideTween.TweenMethod(Callable.From<float>(ApplySlide), _slide, target, SLIDE_SECONDS);

        // Closed: hide everything and drop the draft from the preview once the panel is out
        if (target <= 0f)
        {
            _slideTween.TweenCallback(Callable.From(() =>
            {
                _root.Visible       = false;
                _previewBody.Design = new CellDesign();
            }));
        }
    }

    private void ApplySlide(float slide)
    {
        _slide = slide;
        Vector2 screen = _root.GetViewportRect().Size;
        float   width  = screen.X * PANEL_SHARE + EDGE_PIXELS;
        _panel.OffsetTop    = 0f;
        _panel.OffsetBottom = 0f;
        _panel.OffsetLeft   = -(1f - slide) * width;
        _panel.OffsetRight  = _panel.OffsetLeft + width;
        _panelBackground.SetShaderParameter("rect_size", new Vector2(width, screen.Y));
        _greyout.SetShaderParameter("amount", Mathf.Clamp(slide, 0f, 1f));
    }

    // ===========================================================================
    // ============================   PREVIEW INPUT   ============================
    // ===========================================================================
    //
    //   OnPreviewInput   Left drag rotates (on the edge: stretches), left click places
    //                    the selected part, right click removes, the wheel zooms,
    //                    hovering with a selected part shows where it would go.
    //   ResetCamera      Front view from above, forward to the right.
    //   UpdateCamera     Orbit position from yaw, pitch and zoom.
    //   ToUnit           Container pixel to unit space by a ray against the membrane,
    //                    outside it against the plane.
    //

    private const float CAMERA_DISTANCE = 5.5f;          // fits a cell with flagella, the editor always shows one cell
    private const float CAMERA_FOV = 40f;
    private const float START_PITCH = 0.9f;              // radians above the plane
    private const float MIN_PITCH = -1.2f;
    private const float MAX_PITCH = 1.5f;
    private const float ORBIT_SPEED = 0.008f;            // radians per pixel
    private const float DRAG_THRESHOLD = 5f;             // pixels before a click becomes a rotation
    private const float ZOOM_STEP = 1.1f;
    private const float MIN_ZOOM = 0.55f;
    private const float MAX_ZOOM = 1.8f;

    private float _yaw;
    private float _pitch;
    private float _zoom = 1f;
    private bool _isLeftDown;
    private bool _isOrbiting;
    private Vector2 _leftDownAt;

    private void OnPreviewInput(InputEvent @event)
    {
        switch (@event)
        {
            // Wheel: zoom
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                _zoom = Mathf.Clamp(_zoom * (MouseButton.WheelUp == wheel.ButtonIndex ? 1f / ZOOM_STEP : ZOOM_STEP), MIN_ZOOM, MAX_ZOOM);
                UpdateCamera();
                break;

            // Right click: remove
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right:
                RemoveAt(ToUnit(right.Position));
                break;

            // Left press: maybe a click, maybe the start of a rotation
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.Pressed:
                _isLeftDown = true;
                _isOrbiting = false;
                _leftDownAt = left.Position;
                // Pressed on the membrane edge without a part in hand: stretch instead of rotate
                Vector2 pressed = ToUnit(left.Position);
                _isShaping = null == Selected && IsOnEdge(pressed);
                if (_isShaping)
                {
                    BeginShaping(pressed);
                }
                break;

            // Left release: a click without rotation places the selected part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_isLeftDown && !_isOrbiting && !_isShaping && null != Selected)
                {
                    TryPlace(Selected.Value, ToUnit(left.Position));
                }
                _isLeftDown = false;
                _isOrbiting = false;
                _isShaping  = false;
                break;

            // Edge drag: stretch or squash
            case InputEventMouseMotion shaping when _isLeftDown && _isShaping:
                ShapeTowards(ToUnit(shaping.Position));
                break;

            // Left drag: rotate once past the threshold
            case InputEventMouseMotion motion when _isLeftDown:
                _isOrbiting = _isOrbiting || motion.Position.DistanceTo(_leftDownAt) > DRAG_THRESHOLD;
                if (_isOrbiting)
                {
                    HideGhost();
                    _yaw  -= motion.Relative.X * ORBIT_SPEED;
                    _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * ORBIT_SPEED, MIN_PITCH, MAX_PITCH);
                    UpdateCamera();
                }
                break;

            // Hover with a selected part: show where it would go
            case InputEventMouseMotion hover when null != Selected:
                PreviewPart(Selected.Value, ToUnit(hover.Position));
                break;

            // Hover over the edge with empty hands: the cursor offers to stretch
            case InputEventMouseMotion edgeHover:
                _previewContainer.MouseDefaultCursorShape = IsOnEdge(ToUnit(edgeHover.Position))
                    ? Control.CursorShape.Drag
                    : Control.CursorShape.Arrow;
                break;
        }
    }

    private void ResetCamera()
    {
        _yaw   = 0f;
        _pitch = START_PITCH;
        _zoom  = 1f;
        UpdateCamera();
    }

    /// <summary>Yaw 0 looks from +Z towards the cell, so screen right is +X (forward).</summary>
    private void UpdateCamera()
    {
        float distance = CAMERA_DISTANCE * _zoom;
        Vector3 offset = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * distance;
        // Straight from above or below the default up vector degenerates, so up follows the yaw there
        Vector3 up = Mathf.Abs(Mathf.Cos(_pitch)) < 0.01f ? new Vector3(-Mathf.Sin(_yaw), 0f, -Mathf.Cos(_yaw)) : Vector3.Up;
        _camera.Transform = new Transform3D(Basis.LookingAt(-offset, up), offset);
    }

    /// <summary>
    /// The core membrane is an ellipsoid with radius 1 and height FLATTEN. Scaling the
    /// ray's Y by 1 / FLATTEN turns it into the unit sphere, the first hit is the point
    /// under the cursor. A ray that misses the cell (rim parts) hits the plane y = 0.
    /// </summary>
    private Vector2 ToUnit(Vector2 pixel)
    {
        Vector3 origin    = _camera.ProjectRayOrigin(pixel);
        Vector3 direction = _camera.ProjectRayNormal(pixel);

        // Core membrane as unit sphere
        var scale = new Vector3(1f, 1f / CellVisual3D.FLATTEN, 1f);
        Vector3 o = origin * scale;
        Vector3 d = direction * scale;
        float a = d.Dot(d);
        float b = 2f * o.Dot(d);
        float c = o.Dot(o) - 1f;
        float discriminant = b * b - 4f * a * c;
        if (discriminant >= 0f)
        {
            float t = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            if (t > 0f)
            {
                Vector3 hit = origin + direction * t;
                return new Vector2(hit.X, hit.Z);
            }
        }

        // Plane y = 0, a ray parallel to it gets the center (places nothing on the rim)
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return Vector2.Zero;
        }
        Vector3 planeHit = origin + direction * (-origin.Y / direction.Y);
        return new Vector2(planeHit.X, planeHit.Z);
    }

    // ===========================================================================
    // ===========================   DRAG AND GHOST   ============================
    // ===========================================================================
    //
    //   GetTileDrag       Starts dragging a tile: the bare part hangs at the cursor.
    //   CanDropPart       Over the cell: shows the ghost where the part would snap.
    //   DropPart          Places the dragged part at the ghost.
    //   GhostKind         The part whose ghost shows, null when none.
    //   PreviewPart       The part at its snapped spot plus a pulsing marker ring,
    //                     hidden when it does not fit there.
    //   HideGhost         Removes ghost and marker, the cursor icon shows again.
    //   UpdateGhostHover  Hides the ghost once the mouse leaves the cell, pulses the marker.
    //   SurfaceHeight     Height of the membrane above a unit point, 0 outside.
    //
    //   While the ghost shows, the cursor icon hides, so the part seems to snap onto the
    //   cell. The drag ends in _Notification (REFRESH).
    //

    private const int DRAG_ICON_PIXELS = 96;
    private const float MARKER_PULSE_SPEED = 6f;

    private Control _dragIcon;
    private PartInfo _dragged;
    private PartKind? _ghostKind;

    /// <summary>The part whose ghost shows on the cell, null when none does.</summary>
    public PartKind? GhostKind => _ghostKind;
    private Vector2 _ghostPoint;
    private CellBody _ghostBody;
    private MeshInstance3D _ghostMarker;
    private float _markerTime;

    /// <summary>Locked or unaffordable parts cannot be dragged.</summary>
    private Variant GetTileDrag(PartInfo info)
    {
        bool isAvailable = PartCatalog.IsUnlocked(info, _player.Stage, _player.Diet) && info.Cost <= Points;
        if (!isAvailable)
        {
            return default;
        }

        // The cursor sits in the middle of the bare part
        _dragIcon = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _dragIcon.AddChild(new PartIcon(info.Kind, _player.BodyColor, DRAG_ICON_PIXELS, true)
        {
            Position = -Vector2.One * DRAG_ICON_PIXELS * 0.5f,
        });
        _palette.SetDragPreview(_dragIcon);
        _dragged = info;
        UpdateInfo();
        return (int)info.Kind;
    }

    private bool CanDropPart(Vector2 position, Variant data)
    {
        if (Variant.Type.Int != data.VariantType)
        {
            return false;
        }
        PreviewPart((PartKind)data.AsInt32(), ToUnit(position));
        return true;
    }

    private void DropPart(Vector2 position, Variant data)
    {
        var kind = (PartKind)data.AsInt32();
        HideGhost();
        if (!TryPlace(kind, ToUnit(position)))
        {
            Log.Verbose("CellEditor", $"{kind} dropped where it does not fit -> not placed.");
        }
    }

    /// <summary>Shows the ghost of a part where it would snap, used while dragging, hovering and by the smoke test.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="point">(Required) Position in unit space (core radius 1, +X forward).</param>
    /// <returns>True when the part fits there and its ghost shows, false when the ghost is hidden.</returns>
    public bool PreviewPart(PartKind kind, Vector2 point)
    {
        if (!Draft.TrySnap(kind, point, out Vector2 snapped))
        {
            HideGhost();
            return false;
        }
        if (kind == _ghostKind && snapped.IsEqualApprox(_ghostPoint))
        {
            return true;
        }

        // Ghost: a parts-only body with just this part, rebuilt only when kind or spot change
        var design = new CellDesign();
        design.SetShape(Draft.Shape);
        design.TryPlace(kind, snapped, 0);
        _ghostBody.Design     = design;
        _ghostKind            = kind;
        _ghostPoint           = snapped;
        _ghostMarker.Position = Plane3D.To3D(snapped, SurfaceHeight(snapped) + 0.02f);
        _ghostMarker.Visible  = true;
        if (IsInstanceValid(_dragIcon))
        {
            _dragIcon.Visible = false;
        }
        return true;
    }

    private void HideGhost()
    {
        if (IsInstanceValid(_dragIcon))
        {
            _dragIcon.Visible = true;
        }
        if (null == _ghostKind)
        {
            return;
        }
        _ghostKind           = null;
        _ghostBody.Design    = new CellDesign();
        _ghostMarker.Visible = false;
    }

    private void UpdateGhostHover(float delta)
    {
        if (null == _ghostKind)
        {
            return;
        }
        bool isOverCell = _previewContainer.GetGlobalRect().HasPoint(_previewContainer.GetGlobalMousePosition());
        if (!isOverCell)
        {
            HideGhost();
            return;
        }
        _markerTime += delta;
        float pulse = 0.3f + 0.05f * Mathf.Sin(_markerTime * MARKER_PULSE_SPEED);
        _ghostMarker.Scale = new Vector3(pulse, 0.3f, pulse);
    }

    /// <summary>The flattened membrane: FLATTEN * sqrt(1 - (d / r)^2) inside the stretched outline r, 0 outside.</summary>
    private float SurfaceHeight(Vector2 point)
    {
        float radius = Draft.ShapeRadius(point.Angle());
        float share  = point.Length() / radius;
        return share < 1f ? CellVisual3D.FLATTEN * Mathf.Sqrt(1f - share * share) : 0f;
    }

    // ===========================================================================
    // ===========================   SHAPE AND COLOR   ===========================
    // ===========================================================================
    //
    //   IsOnEdge       Whether a unit point lies on the band around the membrane edge.
    //   BeginShaping   Remembers the grabbed angle and the outline before the drag.
    //   ShapeTowards   Moves the grabbed edge to the cursor, neighbors follow softly.
    //   EdgeWeight     How much a shape point follows the grab, 1 at it, 0 beyond EDGE_FALLOFF.
    //   PickColor      Recolors the draft, null goes back to the stage color.
    //   ShowColor      Paints preview, ghost and the selected swatch in the draft color.
    //

    private const float EDGE_INNER = 0.75f;          // band around the edge, as share of the outline radius
    private const float EDGE_OUTER = 1.35f;
    private const float EDGE_FALLOFF = 1.2f;         // radians: neighbors this far away do not follow any more
    private static readonly Color[] SWATCHES =
    {
        new(0.95f, 0.55f, 0.6f),    // rose
        new(0.98f, 0.7f, 0.4f),     // apricot
        new(0.98f, 0.88f, 0.45f),   // yellow
        new(0.62f, 0.9f, 0.45f),    // lime
        new(0.45f, 0.85f, 0.65f),   // mint
        new(0.45f, 0.8f, 0.95f),    // sky
        new(0.55f, 0.6f, 0.98f),    // blue
        new(0.75f, 0.55f, 0.95f),   // lilac
        new(0.95f, 0.95f, 0.98f),   // pearl
    };

    private bool _isShaping;
    private float _grabAngle;
    private float _grabRadius;
    private readonly float[] _shapeBefore = new float[CellDesign.SHAPE_POINTS];
    private readonly List<(Color? Color, Button Swatch)> _swatches = new();

    private bool IsOnEdge(Vector2 point)
    {
        float radius   = Draft.ShapeRadius(point.Angle());
        float distance = point.Length();
        return distance >= radius * EDGE_INNER && distance <= radius * EDGE_OUTER;
    }

    private void BeginShaping(Vector2 point)
    {
        _grabAngle  = point.Angle();
        _grabRadius = Draft.ShapeRadius(_grabAngle);
        for (int i = 0; i < CellDesign.SHAPE_POINTS; i++)
        {
            _shapeBefore[i] = Draft.Shape[i];
        }
        HideGhost();
    }

    /// <summary>
    /// The edge under the cursor goes to the cursor's distance along the grabbed angle.
    /// Every shape point moves by that change times its EdgeWeight to the grab, so lumpy,
    /// lopsided cells are possible. CellDesign.SetShape clamps the radii.
    /// </summary>
    private void ShapeTowards(Vector2 point)
    {
        float target = Mathf.Clamp(point.Dot(Vector2.FromAngle(_grabAngle)), CellDesign.MIN_SHAPE, CellDesign.MAX_SHAPE);
        float change = target - _grabRadius;
        var   radii  = new float[CellDesign.SHAPE_POINTS];
        for (int i = 0; i < radii.Length; i++)
        {
            float angle  = Mathf.Tau * i / radii.Length;
            radii[i] = _shapeBefore[i] + change * EdgeWeight(angle, _grabAngle);
        }
        Draft.SetShape(radii);
    }

    private static float EdgeWeight(float angle, float grab)
    {
        float distance = Mathf.Abs(Mathf.Wrap(angle - grab, -Mathf.Pi, Mathf.Pi));
        return distance >= EDGE_FALLOFF ? 0f : (Mathf.Cos(distance / EDGE_FALLOFF * Mathf.Pi) + 1f) * 0.5f;
    }

    /// <summary>Recolors the draft, committed with Done like the parts.</summary>
    /// <param name="color">(Required) The new color, null for the color of the stage.</param>
    public void PickColor(Color? color)
    {
        Draft.SetColor(color);
        ShowColor();
        EditorStyle.Punch(_previewContainer, 1.02f);
    }

    private void ShowColor()
    {
        Color color = Draft.Color ?? _player.StageColor;
        _previewBody.BodyColor = color;
        _ghostBody.BodyColor   = color;
        foreach ((Color? swatchColor, Button swatch) in _swatches)
        {
            bool isPicked = swatchColor == Draft.Color;
            swatch.AddThemeStyleboxOverride("normal", SwatchStyle(swatchColor ?? _player.StageColor, isPicked));
            swatch.AddThemeStyleboxOverride("hover", SwatchStyle((swatchColor ?? _player.StageColor).Lightened(0.15f), true));
            swatch.AddThemeStyleboxOverride("pressed", SwatchStyle(swatchColor ?? _player.StageColor, true));
        }
    }

    private static StyleBoxFlat SwatchStyle(Color color, bool isPicked)
    {
        var style = new StyleBoxFlat
        {
            BgColor     = color,
            BorderColor = isPicked ? Colors.White : new Color(color.Darkened(0.5f), 0.8f),
            ShadowColor = new Color(color, isPicked ? 0.6f : 0f),
            ShadowSize  = isPicked ? 8 : 0,
        };
        style.SetBorderWidthAll(isPicked ? 3 : 2);
        style.SetCornerRadiusAll(16);
        return style;
    }

    // ===========================================================================
    // ==============================   FEEDBACK   ===============================
    // ===========================================================================
    //
    //   Celebrate   Punches the cell to a scale and sends a ring out from the part.
    //   _Process    Turns the dotted rim of the petri dish, updates the ghost (DRAG AND GHOST).
    //

    private const float BURST_SECONDS = 0.45f;
    private const float DISH_TURN_SPEED = 0.12f;     // radians per second

    private void Celebrate(Vector2 point, Color color, float punch)
    {
        // Cell punch: out and elastic back
        Tween bounce = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        bounce.TweenMethod(Callable.From<float>(radius => _previewBody.Radius = radius), 1f, punch, 0.08f);
        bounce.TweenMethod(Callable.From<float>(radius => _previewBody.Radius = radius), punch, 1f, 0.35f)
            .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

        // Ring burst at the part, its own material so the fade does not touch other rings
        var material = new StandardMaterial3D
        {
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor  = color,
        };
        float height = Mathf.Max(SurfaceHeight(point), 0.05f);
        MeshInstance3D ring = CellVisual3D.AddMesh(_effects, CellVisual3D.TorusMesh(), material,
            Plane3D.To3D(point, height), Vector3.One * 0.1f);
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Tween burst = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        burst.TweenMethod(Callable.From<float>(t =>
        {
            ring.Scale           = new Vector3(1f, 0.4f, 1f) * Mathf.Lerp(0.1f, 0.7f, t);
            material.AlbedoColor = new Color(color, 1f - t);
        }), 0f, 1f, BURST_SECONDS).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        burst.TweenCallback(Callable.From(ring.QueueFree));
    }

    public override void _Process(double delta)
    {
        if (null != _dishRim && _root.Visible)
        {
            _dishRim.RotateY((float)delta * DISH_TURN_SPEED);
        }
        UpdateGhostHover((float)delta);
    }

    // ===========================================================================
    // ==============================   DIET TINT   ==============================
    // ===========================================================================
    //
    //   TintTo      Eases the tint to the diet the draft would lead to.
    //   ApplyTint   Sets panel shader, dish, arrow and done button to the tint of a diet.
    //
    //   The tint follows the preview diet (current diet plus the draft), so placing a
    //   jaw already blushes the panel a little.
    //

    private const float TINT_SECONDS = 0.6f;

    private readonly StandardMaterial3D _floorMaterial = new()
    {
        ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };
    private readonly StandardMaterial3D _dotMaterial = new()
    {
        Transparency    = BaseMaterial3D.TransparencyEnum.Alpha,
        EmissionEnabled = true,
    };
    private readonly StandardMaterial3D _arrowMaterial = new()
    {
        Transparency             = BaseMaterial3D.TransparencyEnum.Alpha,
        EmissionEnabled          = true,
        EmissionEnergyMultiplier = 0.4f,
    };
    private float _tint;
    private Tween _tintTween;

    private void TintTo(float diet)
    {
        if (Mathf.IsEqualApprox(diet, _tint))
        {
            return;
        }
        _tintTween?.Kill();
        _tintTween = CreateTween()
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _tintTween.TweenMethod(Callable.From<float>(ApplyTint), _tint, diet, TINT_SECONDS);
    }

    private void ApplyTint(float diet)
    {
        _tint = diet;
        (Color color, float amount) = EditorStyle.DietTint(diet);
        _panelBackground.SetShaderParameter("tint_color", color);
        _panelBackground.SetShaderParameter("tint_amount", amount);

        Color accent = EditorStyle.Accent(diet);
        _floorMaterial.AlbedoColor = accent;
        _dotMaterial.AlbedoColor   = new Color(accent, 0.75f);
        _dotMaterial.Emission      = accent;
        _arrowMaterial.AlbedoColor = new Color(accent, 0.6f);
        _arrowMaterial.Emission    = accent;
        EditorStyle.ApplyButton(_doneButton, accent);
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh        Updates title, points, diet preview, info card and tiles.
    //   UpdateInfo     Name and description of the dragged, hovered or selected part, empty otherwise.
    //   _Notification  Re-translates after a locale change, cleans up after a drag.
    //   BuildPalette   One header and a grid of icon tiles per diet class, popping in.
    //   BuildTile      Tinted tile with icon, name and cost, hover grows it.
    //   UpdateTiles    Name, cost, pressed and locked state of every tile.
    //   UnlockHintKey  Which diet side unlocks a locked part.
    //

    private const float TILE_HOVER_SCALE = 1.06f;
    private const float TILE_POP_DELAY = 0.12f;
    private const float TILE_POP_STAGGER = 0.03f;

    private sealed record PaletteTile(PartInfo Info, Button Button, PartIcon Icon, Label Name, Label Cost);

    private readonly List<PaletteTile> _tiles = new();
    private readonly List<(DietClass Diet, Label Header)> _dietHeaders = new();
    private PartInfo _hovered;
    private int _lastPoints = -1;

    /// <summary>Updates every widget, also when new points arrive while the editor is open.</summary>
    public void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }
        _titleLabel.Text      = Tr("EDITOR_TITLE");
        _pointsLabel.Text     = string.Format(Tr("EDITOR_POINTS"), Points);
        _doneButton.Text      = Tr("EDITOR_DONE");
        _dietBar.Value        = _player.Diet;
        _dietBar.PreviewValue = _player.PreviewDiet(Draft);
        TintTo(_dietBar.PreviewValue);
        foreach ((DietClass diet, Label header) in _dietHeaders)
        {
            header.Text = Tr($"DIET_{diet}".ToUpperInvariant());
        }

        // Points changed: the badge pops, all spent: the done button invites
        if (_lastPoints >= 0 && Points != _lastPoints)
        {
            EditorStyle.Punch(_pointsBadge, 1.15f);
            if (0 == Points)
            {
                EditorStyle.Punch(_doneButton, 1.12f);
            }
        }
        _lastPoints = Points;

        UpdateInfo();
        UpdateTiles();
    }

    private void UpdateInfo()
    {
        PartInfo info = _dragged ?? _hovered ?? (null == Selected ? null : PartCatalog.Get(Selected.Value));
        if (null == info)
        {
            _infoName.Text = "";
            _infoText.Text = "";
            return;
        }
        bool isUnlocked = PartCatalog.IsUnlocked(info, _player.Stage, _player.Diet);
        _infoName.Text = Tr(info.NameKey);
        _infoName.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[info.Diet]);
        _infoText.Text = isUnlocked ? Tr(info.DescriptionKey) : Tr(UnlockHintKey(info));
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what)
        {
            Refresh();
        }

        // Drag over (dropped or cancelled): ghost and drag info go, see DRAG AND GHOST
        if (NotificationDragEnd == what && null != _dragged)
        {
            // Godot does not always deliver the drop over the SubViewportContainer, the ghost knows the spot
            if (null != _ghostKind && !GetViewport().GuiIsDragSuccessful())
            {
                TryPlace(_ghostKind.Value, _ghostPoint);
            }
            _dragged  = null;
            _dragIcon = null;
            HideGhost();
            UpdateInfo();
        }
    }

    /// <summary>Rebuilt on every Open: the stage decides which parts are offered and the icons use the body color.</summary>
    private void BuildPalette()
    {
        foreach (Node child in _palette.GetChildren())
        {
            _palette.RemoveChild(child);
            child.QueueFree();
        }
        _tiles.Clear();
        _dietHeaders.Clear();

        var group = new ButtonGroup { AllowUnpress = true };
        foreach (DietClass diet in new[] { DietClass.Herbivore, DietClass.Omnivore, DietClass.Carnivore })
        {
            // Header: diet name and a fading line in its color
            Color accent = EditorStyle.DIET_COLORS[diet];
            var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            header.AddThemeConstantOverride("separation", 10);
            Label title = NewLabel(17);
            title.AddThemeColorOverride("font_color", accent);
            var line = new ColorRect
            {
                Color               = new Color(accent, 0.3f),
                CustomMinimumSize   = new Vector2(0f, 2f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
                MouseFilter         = Control.MouseFilterEnum.Ignore,
            };
            header.AddChild(title);
            header.AddChild(line);
            _palette.AddChild(header);
            _dietHeaders.Add((diet, title));

            var grid = new GridContainer { Columns = TILE_COLUMNS };
            grid.AddThemeConstantOverride("h_separation", 8);
            grid.AddThemeConstantOverride("v_separation", 8);
            _palette.AddChild(grid);

            foreach (PartInfo info in PartCatalog.All())
            {
                if (diet != info.Diet || _player.Stage < info.MinStage)
                {
                    continue;
                }
                Button tile = BuildTile(info, group);
                grid.AddChild(tile);
                EditorStyle.PopIn(tile, TILE_POP_DELAY + _tiles.Count * TILE_POP_STAGGER);
            }
        }
    }

    private Button BuildTile(PartInfo info, ButtonGroup group)
    {
        var button = new Button
        {
            CustomMinimumSize = TILE_SIZE,
            ToggleMode        = true,
            ButtonGroup       = group,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        EditorStyle.ApplyTile(button, EditorStyle.DIET_COLORS[info.Diet]);

        // Icon, name and cost stacked, they let every click through to the button
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        stack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 4);
        stack.AddThemeConstantOverride("separation", 0);
        button.AddChild(stack);
        var icon = new PartIcon(info.Kind, _player.BodyColor, TILE_ICON_PIXELS) { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        Label name = NewLabel(12);
        Label cost = NewLabel(11);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        cost.HorizontalAlignment = HorizontalAlignment.Center;
        cost.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[info.Diet].Lightened(0.3f));
        stack.AddChild(icon);
        stack.AddChild(name);
        stack.AddChild(cost);

        // Switching tiles unpresses the old one, that must not clear the new selection
        PartKind kind = info.Kind;
        button.Toggled += isPressed =>
        {
            if (isPressed) Select(kind);
            else if (kind == Selected) Select(null);
        };

        // Hover grows the tile and shows its card, also for locked parts (why they are locked)
        button.MouseEntered += () =>
        {
            _hovered = info;
            UpdateInfo();
            EditorStyle.ScaleTo(button, TILE_HOVER_SCALE);
        };
        button.MouseExited += () =>
        {
            _hovered = null;
            UpdateInfo();
            EditorStyle.ScaleTo(button, 1f);
        };
        button.SetDragForwarding(Callable.From<Vector2, Variant>(_ => GetTileDrag(info)), new Callable(), new Callable());

        _tiles.Add(new PaletteTile(info, button, icon, name, cost));
        return button;
    }

    private void UpdateTiles()
    {
        foreach (PaletteTile tile in _tiles)
        {
            bool isUnlocked  = PartCatalog.IsUnlocked(tile.Info, _player.Stage, _player.Diet);
            bool isAvailable = isUnlocked && tile.Info.Cost <= Points;
            tile.Name.Text       = Tr(tile.Info.NameKey);
            tile.Cost.Text       = $"{tile.Info.Cost} EP";
            tile.Button.Disabled = !isAvailable;
            tile.Button.SetPressedNoSignal(tile.Info.Kind == Selected);
            SetAlpha(tile.Icon, isAvailable ? 1f : 0.3f);
            SetAlpha(tile.Name, isAvailable ? 1f : 0.45f);
        }
    }

    private static string UnlockHintKey(PartInfo info)
    {
        return info.MaxDiet < 1f && info.MinDiet > -1f
            ? "EDITOR_LOCKED_OMNIVORE"
            : (info.MaxDiet < 1f ? "EDITOR_LOCKED_HERBIVORE" : "EDITOR_LOCKED_CARNIVORE");
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   _Ready         Keeps running while paused, builds everything hidden and closed.
    //   BuildPanel     Grey-out over the game, the membrane panel with header, body and footer.
    //   BuildFooter    Info card, points badge and the done button.
    //   BuildPreview   Color swatches over a SubViewport with its own 3D world, orbit
    //                  camera, petri dish, bubbles, forward arrow, the cell and its ghost.
    //   NewLabel       Label with outline, translated here.
    //   SetAlpha       Fades a canvas item.
    //

    private const float PANEL_SHARE = 0.65f;         // of the screen width, the paused game stays visible right of it
    private const float EDGE_PIXELS = 40f;           // strip for the wobbling membrane, see editor_panel.gdshader
    private const int PANEL_MARGIN = 20;
    private const int TILE_COLUMNS = 3;
    private const int TILE_ICON_PIXELS = 64;
    private const int RIM_DOTS = 48;                 // dots on the rim of the petri dish
    private const float DISH_RADIUS = 1.8f;
    private static readonly Vector2 TILE_SIZE = new(108, 104);
    private static readonly Shader GREYOUT_SHADER = GD.Load<Shader>("res://shaders/greyout.gdshader");
    private static readonly Shader PANEL_SHADER = GD.Load<Shader>("res://shaders/editor_panel.gdshader");

    private Control _root;
    private ShaderMaterial _greyout;
    private ShaderMaterial _panelBackground;
    private Control _panel;
    private Label _titleLabel;
    private PanelContainer _pointsBadge;
    private Label _pointsLabel;
    private Label _infoName;
    private Label _infoText;
    private Button _doneButton;
    private DietBar _dietBar;
    private VBoxContainer _palette;
    private SubViewportContainer _previewContainer;
    private Camera3D _camera;
    private Node3D _dish;
    private Node3D _dishRim;
    private Node3D _effects;
    private CellBody _previewBody;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 20;
        BuildPanel();
        _root.Visible = false;
        ApplySlide(0f);
        ApplyTint(0f);
        GetViewport().SizeChanged += () => ApplySlide(_slide);
    }

    private void BuildPanel()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // Grey-out over the paused game, it also swallows clicks meant for the HUD
        _greyout = new ShaderMaterial { Shader = GREYOUT_SHADER };
        var greyout = new ColorRect { Material = _greyout, MouseFilter = Control.MouseFilterEnum.Stop };
        greyout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(greyout);

        // Panel: full height on the left, animated water with a membrane edge, slides by its own width
        _panel = new Control { AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 1f };
        _root.AddChild(_panel);
        _panelBackground = new ShaderMaterial { Shader = PANEL_SHADER };
        _panelBackground.SetShaderParameter("edge_px", EDGE_PIXELS);
        var background = new ColorRect { Material = _panelBackground, MouseFilter = Control.MouseFilterEnum.Stop };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(background);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", PANEL_MARGIN);
        margin.AddThemeConstantOverride("margin_top", PANEL_MARGIN - 4);
        margin.AddThemeConstantOverride("margin_right", PANEL_MARGIN + (int)EDGE_PIXELS);
        margin.AddThemeConstantOverride("margin_bottom", PANEL_MARGIN - 4);
        _panel.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);

        // Header: title, the diet bar with preview marker on the right
        var header = new HBoxContainer();
        layout.AddChild(header);
        _titleLabel = NewLabel(34);
        GameFonts.UseDisplay(_titleLabel);
        _titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _dietBar = new DietBar { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        header.AddChild(_titleLabel);
        header.AddChild(_dietBar);

        // Body: tile palette left, rotatable preview right
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        layout.AddChild(body);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize    = new Vector2(TILE_COLUMNS * (TILE_SIZE.X + 8) + 16, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body.AddChild(scroll);
        // Room around the tiles, so hover growth and glow are not clipped by the scroll area
        var paletteMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            paletteMargin.AddThemeConstantOverride(side, 6);
        }
        scroll.AddChild(paletteMargin);
        _palette = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _palette.AddThemeConstantOverride("separation", 8);
        paletteMargin.AddChild(_palette);
        BuildPreview(body);

        BuildFooter(layout);
    }

    private void BuildFooter(Container parent)
    {
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 14);
        parent.AddChild(footer);

        // Info card: name in the diet color, description below, fixed height so nothing jumps
        var card = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0f, 62f),
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        card.AddThemeConstantOverride("separation", 0);
        footer.AddChild(card);
        _infoName = NewLabel(20);
        _infoText = NewLabel(14);
        _infoText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        card.AddChild(_infoName);
        card.AddChild(_infoText);

        // Points badge and done button
        _pointsBadge = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        _pointsBadge.AddThemeStyleboxOverride("panel", EditorStyle.Pill(new Color(0.08f, 0.2f, 0.2f), EditorStyle.DIET_COLORS[DietClass.Omnivore], 0.4f));
        _pointsLabel = NewLabel(17);
        _pointsLabel.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[DietClass.Omnivore].Lightened(0.4f));
        _pointsBadge.AddChild(_pointsLabel);
        footer.AddChild(_pointsBadge);

        _doneButton = new Button
        {
            CustomMinimumSize = new Vector2(150, 48),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        _doneButton.AddThemeFontSizeOverride("font_size", 20);
        _doneButton.Pressed      += Finish;
        _doneButton.MouseEntered += () => EditorStyle.ScaleTo(_doneButton, 1.06f);
        _doneButton.MouseExited  += () => EditorStyle.ScaleTo(_doneButton, 1f);
        footer.AddChild(_doneButton);
    }

    private void BuildPreview(Container parent)
    {
        _previewContainer = new SubViewportContainer
        {
            Stretch             = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ExpandFill,
            MouseFilter         = Control.MouseFilterEnum.Stop,
        };
        _previewContainer.GuiInput += OnPreviewInput;
        _previewContainer.SetDragForwarding(new Callable(),
            Callable.From<Vector2, Variant, bool>(CanDropPart),
            Callable.From<Vector2, Variant>(DropPart));

        // Column: color swatches above the cell, the first one is the color of the stage
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        parent.AddChild(column);
        var swatchRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        swatchRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(swatchRow);
        foreach (Color? swatchColor in new Color?[] { null }.Concat(SWATCHES.Select(color => (Color?)color)))
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(30, 30), FocusMode = Control.FocusModeEnum.None };
            swatch.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            Color? picked = swatchColor;
            swatch.Pressed      += () => PickColor(picked);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            swatchRow.AddChild(swatch);
            _swatches.Add((swatchColor, swatch));
        }
        column.AddChild(_previewContainer);

        SubViewport viewport = PartIcon.NewStudio(new Vector2I(440, 440));
        _previewContainer.AddChild(viewport);

        _camera = new Camera3D { Fov = CAMERA_FOV, Current = true };
        viewport.AddChild(_camera);

        // Petri dish: a soft glowing floor and a slowly turning dotted rim
        _dish = new Node3D { Position = new Vector3(0f, -0.65f, 0f) };
        viewport.AddChild(_dish);
        var glow = new GradientTexture2D
        {
            Gradient = new Gradient
            {
                Colors  = new[] { new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.18f), new Color(1f, 1f, 1f, 0f) },
                Offsets = new[] { 0f, 0.6f, 1f },
            },
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1f, 0.5f),
            Width    = 128,
            Height   = 128,
        };
        var floor = new MeshInstance3D
        {
            Mesh             = new PlaneMesh { Size = Vector2.One * DISH_RADIUS * 2.2f },
            MaterialOverride = _floorMaterial,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _floorMaterial.AlbedoTexture = glow;
        _dish.AddChild(floor);
        _dishRim = new Node3D();
        _dish.AddChild(_dishRim);
        for (int i = 0; i < RIM_DOTS; i++)
        {
            float angle = Mathf.Tau * i / RIM_DOTS;
            float size  = 0 == i % 4 ? 0.04f : 0.022f;
            MeshInstance3D dot = CellVisual3D.AddMesh(_dishRim, CellVisual3D.SphereMesh(true), _dotMaterial,
                new Vector3(Mathf.Cos(angle), 0.03f, Mathf.Sin(angle)) * (DISH_RADIUS + 0.12f), Vector3.One * size);
            dot.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }

        // Bubbles rising through the dish
        var bubbleRamp = new Gradient
        {
            Colors  = new[] { new Color(0.85f, 0.9f, 1f, 0f), new Color(0.85f, 0.9f, 1f, 0.5f), new Color(0.85f, 0.9f, 1f, 0f) },
            Offsets = new[] { 0f, 0.3f, 1f },
        };
        var bubbles = new CpuParticles3D
        {
            Amount             = 36,
            Lifetime           = 6f,
            Preprocess         = 6f,
            Mesh               = CellVisual3D.SphereMesh(true),
            EmissionShape      = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(DISH_RADIUS, 0.1f, DISH_RADIUS),
            Direction          = Vector3.Up,
            Spread             = 12f,
            Gravity            = new Vector3(0f, 0.1f, 0f),
            InitialVelocityMin = 0.05f,
            InitialVelocityMax = 0.2f,
            ScaleAmountMin     = 0.015f,
            ScaleAmountMax     = 0.05f,
            ColorRamp          = bubbleRamp,
            MaterialOverride   = new StandardMaterial3D
            {
                ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
            },
            CastShadow         = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _dish.AddChild(bubbles);

        // Forward arrow on the plane in front of the cell, it stays readable from every angle
        var arrow = new Node3D { Position = new Vector3(1.55f, 0f, 0f) };
        viewport.AddChild(arrow);
        MeshInstance3D cone = CellVisual3D.AddMesh(arrow, CellVisual3D.ConeMesh(),
            _arrowMaterial, Vector3.Zero, Vector3.One);
        cone.Basis      = new Basis(Vector3.Back, -Mathf.Pi * 0.5f) * Basis.FromScale(new Vector3(1.6f, 0.6f, 1.6f));
        cone.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        _previewBody = new CellBody { Radius = 1f };
        viewport.AddChild(_previewBody);
        _effects = new Node3D();                     // ring bursts of FEEDBACK
        viewport.AddChild(_effects);

        // Ghost of DRAG AND GHOST: bare part plus a marker ring, both hidden until a part hovers
        _ghostBody = new CellBody { Radius = 1f, IsPartsOnly = true };
        viewport.AddChild(_ghostBody);
        _ghostMarker = CellVisual3D.AddMesh(_effects, CellVisual3D.TorusMesh(), _dotMaterial, Vector3.Zero, Vector3.One * 0.3f);
        _ghostMarker.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _ghostMarker.Visible    = false;
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }

    private static void SetAlpha(CanvasItem item, float alpha)
    {
        Color modulate = item.Modulate;
        modulate.A    = alpha;
        item.Modulate = modulate;
    }
}
