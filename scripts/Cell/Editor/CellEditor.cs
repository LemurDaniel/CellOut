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
//   elsewhere rotates, the wheel zooms, right click removes the part under the cursor
//   (else the closest one) and refunds it. A placed or clicked part gets a gizmo
//   (PartGizmo): arrows shift it, rings turn it, looks only. "Done" commits the draft
//   and slides the panel out.
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
    //   RemovePart      Removes one placed part and refunds it.
    //   PickPart        Picks a placed part for the gizmo, -1 for none.
    //   MovePickedPart  Shifts the picked part (gizmo arrows), free of charge, looks only.
    //   TurnPickedPart  Turns the picked part (gizmo rings), free of charge, looks only.
    //   Finish     Commits the draft and slides out.
    //

    [Signal] public delegate void ClosedEventHandler();

    private Player _player;
    private int _spent;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public CellDesign Draft { get; private set; } = new();
    public PartKind? Selected { get; private set; }
    public int PickedPart { get; private set; } = -1;
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
        PickedPart = -1;
        _gizmoDrag = GizmoHandle.None;
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
    /// <param name="lift">(Optional) Height on the membrane for rim parts, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>True when the part was placed and paid.</returns>
    public bool TryPlace(PartKind kind, Vector2 point, float lift = 0f)
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
        if (!Draft.TryPlace(kind, point, info.Cost, lift))
        {
            return false;
        }
        _spent    += info.Cost;
        PickedPart = Draft.Parts.Count - 1;          // the new part gets the gizmo
        HideGhost();
        Celebrate(PartPoint(Draft.Parts[^1]), EditorStyle.DIET_COLORS[info.Diet], 1.1f);
        Select(null);                                // deselected, so the gizmo has focus until the next tile
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
        Refund(removed.Value);
        return true;
    }

    /// <summary>Removes one placed part and refunds what was paid for it.</summary>
    /// <param name="index">(Required) Index in Draft.Parts.</param>
    /// <returns>False for an index outside Draft.Parts.</returns>
    public bool RemovePart(int index)
    {
        if (index < 0 || index >= Draft.Parts.Count)
        {
            return false;
        }
        Refund(Draft.RemovePart(index));
        return true;
    }

    private void Refund(PlacedPart removed)
    {
        _spent    -= removed.PaidCost;
        PickedPart = -1;                             // indices moved
        Celebrate(PartPoint(removed), new Color(0.75f, 0.85f, 0.9f), 0.93f);
        Refresh();
    }

    /// <summary>Picks a placed part for the gizmo.</summary>
    /// <param name="index">(Required) Index in Draft.Parts, -1 clears the pick.</param>
    public void PickPart(int index)
    {
        PickedPart = index >= 0 && index < Draft.Parts.Count ? index : -1;
    }

    /// <summary>Shifts the picked part, clamped to CellDesign.MAX_PART_OFFSET.</summary>
    /// <param name="delta">(Required) Shift in the preview world, turned and scaled into the unit space of the cell.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.PickPart(0); editor.MovePickedPart(new Vector3(0f, 0.1f, 0f));</code></example>
    public bool MovePickedPart(Vector3 delta)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        PlacedPart part = Draft.Parts[PickedPart];
        Draft.SetPartPose(PickedPart, part.Offset + ToCell().Inverse() * delta, part.Rotation);
        return true;
    }

    /// <summary>Turns the picked part around a world axis of the preview.</summary>
    /// <param name="axis">(Required) Normalized world axis.</param>
    /// <param name="angle">(Required) Radians, right hand rule.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.TurnPickedPart(Vector3.Up, 0.5f);</code></example>
    public bool TurnPickedPart(Vector3 axis, float angle)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        PlacedPart part      = Draft.Parts[PickedPart];
        Vector3    localAxis = (ToCell().Orthonormalized().Inverse() * axis).Normalized();
        Basis      turned    = new Basis(localAxis, angle) * Basis.FromEuler(part.Rotation);
        Draft.SetPartPose(PickedPart, part.Offset, turned.Orthonormalized().GetEuler());
        return true;
    }

    /// <summary>Basis from the unit space of the preview cell to the preview world (turn and size of the visual).</summary>
    private Basis ToCell()
    {
        CellVisual3D visual = _previewBody.Visual;
        return null != visual && visual.IsInsideTree() ? visual.GlobalTransform.Basis : Basis.Identity;
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
    //   OnPreviewInput   Left drag on the gizmo shifts or turns the picked part, on a height
    //                    handle raises or flattens, else rotates (on the edge: stretches),
    //                    left click places the selected part (where its dot shows) or picks
    //                    a placed one, right click removes, the wheel zooms, hovering with a
    //                    selected part shows where it would go.
    //   PartAtPixel      The placed part under a pixel, -1 for none.
    //   UpdateGizmo      Puts the gizmo on the picked part, hidden without one.
    //   ResetCamera      Front view from above, forward to the right.
    //   UpdateCamera     Orbit position from yaw, pitch and zoom.
    //   ToUnit           Container pixel to unit space by a ray against the membrane,
    //                    outside it against the plane.
    //   TryHitSurface    The point of the stretched membrane under a pixel with its height (lift), false on a miss.
    //   RayEllipsoid     First hit of a ray on the membrane ellipsoid of one outline radius and height.
    //

    private const float CAMERA_DISTANCE = 5.5f;          // fits a cell with flagella, the editor always shows one cell
    private const float CAMERA_FOV = 40f;
    private const float START_PITCH = 0.9f;              // radians above the plane
    private const float MIN_PITCH = -1.2f;
    private const float MAX_PITCH = 1.5f;
    private const float ORBIT_SPEED = 0.008f;            // radians per pixel
    private const float DRAG_THRESHOLD = 5f;             // pixels before a click becomes a rotation
    private const float PLACE_DRAG_THRESHOLD = 14f;      // with a part in hand a shaky click still places
    private const float ZOOM_STEP = 1.1f;
    private const float MIN_ZOOM = 0.55f;
    private const float MAX_ZOOM = 1.8f;

    private float _yaw;
    private float _pitch;
    private float _zoom = 1f;
    private bool _isLeftDown;
    private bool _isOrbiting;
    private Vector2 _leftDownAt;
    private PartGizmo _gizmo;
    private GizmoHandle _gizmoDrag = GizmoHandle.None;

    private void OnPreviewInput(InputEvent @event)
    {
        // The gizmo lights up under the cursor while nothing is dragged
        if (@event is InputEventMouseMotion moved && GizmoHandle.None == _gizmoDrag)
        {
            _gizmo.Hovered = _gizmo.HandleAt(moved.Position);
        }

        switch (@event)
        {
            // Wheel: zoom
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                _zoom = Mathf.Clamp(_zoom * (MouseButton.WheelUp == wheel.ButtonIndex ? 1f / ZOOM_STEP : ZOOM_STEP), MIN_ZOOM, MAX_ZOOM);
                UpdateCamera();
                break;

            // Right click: remove the part under the cursor, else the closest one
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right:
                if (!RemovePart(PartAtPixel(right.Position)))
                {
                    RemoveAt(ToUnit(right.Position));
                }
                break;

            // Left press on the gizmo: shift or turn the picked part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } grab when GizmoHandle.None != _gizmo.HandleAt(grab.Position):
                _gizmoDrag    = _gizmo.HandleAt(grab.Position);
                _gizmo.Active = _gizmoDrag;
                break;

            // Left release after a gizmo drag
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when GizmoHandle.None != _gizmoDrag:
                _gizmoDrag    = GizmoHandle.None;
                _gizmo.Active = GizmoHandle.None;
                break;

            // Gizmo drag: arrows shift along their axis, rings turn around it
            case InputEventMouseMotion pose when GizmoHandle.None != _gizmoDrag:
                if (PartGizmo.IsTurn(_gizmoDrag))
                {
                    TurnPickedPart(PartGizmo.AxisOf(_gizmoDrag), _gizmo.TurnDelta(_gizmoDrag, pose.Position - pose.Relative, pose.Position));
                }
                else
                {
                    MovePickedPart(_gizmo.MoveDelta(_gizmoDrag, pose.Relative));
                }
                break;

            // Left press on a height handle with empty hands: raise or flatten the membrane there
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } raise when null == Selected && HeightHandleAt(raise.Position) >= 0:
                _heightDrag = HeightHandleAt(raise.Position);
                break;

            // Left release after a height drag
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _heightDrag >= 0:
                Celebrate(HeightHandlePosition(_heightDrag), EditorStyle.DIET_COLORS[DietClass.Omnivore], 1.03f);
                _heightDrag = -1;
                break;

            // Height drag: up raises, down flattens
            case InputEventMouseMotion lifting when _heightDrag >= 0:
                RaiseHeight(_heightDrag, -lifting.Relative.Y * HEIGHT_PER_PIXEL);
                break;

            // Left press: maybe a click, maybe the start of a rotation
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.Pressed:
                _isLeftDown = true;
                _isOrbiting = false;
                _leftDownAt = left.Position;
                // Pressed on the membrane edge without a part in hand: stretch instead of rotate
                Vector2 pressed = ToUnit(left.Position);
                _isShaping = null == Selected && IsOnEdge(pressed) && PartAtPixel(left.Position) < 0;
                if (_isShaping)
                {
                    BeginShaping(pressed);
                }
                break;

            // Left release: a click without rotation places the selected part, without one it picks a placed part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_isLeftDown && !_isOrbiting && !_isShaping)
                {
                    // The dot shows where the part goes: the hit on the membrane, a miss places nothing
                    if (null != Selected)
                    {
                        // A placed part deselects the tile (gizmo gets the focus), a failed one keeps the dot
                        bool isPlaced = TryHitSurface(left.Position, out Vector2 spot, out float lift, out _)
                            && TryPlace(Selected.Value, spot, lift);
                        if (isPlaced)
                        {
                            HideHoverDot();
                        }
                        else
                        {
                            ShowHoverDot(Selected.Value, left.Position);
                        }
                    }
                    else
                    {
                        PickPart(PartAtPixel(left.Position));
                    }
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
                _isOrbiting = _isOrbiting || motion.Position.DistanceTo(_leftDownAt) > (null == Selected ? DRAG_THRESHOLD : PLACE_DRAG_THRESHOLD);
                if (_isOrbiting)
                {
                    HideGhost();
                    HideHoverDot();
                    _yaw  -= motion.Relative.X * ORBIT_SPEED;
                    _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * ORBIT_SPEED, MIN_PITCH, MAX_PITCH);
                    UpdateCamera();
                }
                break;

            // Hover with a selected part: a dot on the membrane right under the cursor
            case InputEventMouseMotion hover when null != Selected:
                ShowHoverDot(Selected.Value, hover.Position);
                break;

            // Hover over a height handle or the edge with empty hands: the cursor offers to raise or stretch
            case InputEventMouseMotion edgeHover:
                HideHoverDot();
                _heightHover = HeightHandleAt(edgeHover.Position);
                _previewContainer.MouseDefaultCursorShape = _heightHover >= 0
                    ? Control.CursorShape.Vsize
                    : IsOnEdge(ToUnit(edgeHover.Position)) ? Control.CursorShape.Drag : Control.CursorShape.Arrow;
                break;
        }
    }

    /// <summary>The placed part under a pixel of the preview, -1 for none.</summary>
    private int PartAtPixel(Vector2 pixel)
    {
        CellVisual3D visual = _previewBody.Visual;
        return null == visual ? -1 : visual.PartAt(_camera.ProjectRayOrigin(pixel), _camera.ProjectRayNormal(pixel));
    }

    /// <summary>Puts the gizmo on the anchor of the picked part, hides it without one.</summary>
    private void UpdateGizmo()
    {
        Vector3 anchor   = Vector3.Zero;
        // A palette part in hand hides the gizmo, so it never catches a click meant for placing
        bool    isPicked = PickedPart >= 0 && PickedPart < Draft.Parts.Count && _root.Visible && null == Selected;
        _gizmo.Visible = isPicked && null != _previewBody.Visual && _previewBody.Visual.TryGetPartAnchor(PickedPart, out anchor);
        if (_gizmo.Visible)
        {
            _gizmo.GlobalPosition = anchor;
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
        Vector3? hit = RayEllipsoid(origin, direction, 1f, CellVisual3D.FLATTEN);
        if (null != hit)
        {
            return new Vector2(hit.Value.X, hit.Value.Z);
        }

        // Plane y = 0, a ray parallel to it gets the center (places nothing on the rim)
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return Vector2.Zero;
        }
        Vector3 planeHit = origin + direction * (-origin.Y / direction.Y);
        return new Vector2(planeHit.X, planeHit.Z);
    }

    private const int SURFACE_RAY_STEPS = 3;         // refinements of the outline radius, the stretch changes slowly

    /*
      Like ToUnit, but against the real membrane: the ellipsoid stretched by the outline
      radius S and the membrane height H at the hit angle. Both depend on the hit, so the
      ray is cast against the ellipsoid of the last guess and refined SURFACE_RAY_STEPS
      times, starting round. The lift is the hit height in sphere units (-1 bottom ..
      1 top). A ray that misses the membrane hits nothing, like the creature editor.
    */
    /// <summary>The point of the membrane under a pixel.</summary>
    /// <param name="pixel">(Required) Pixel in the preview.</param>
    /// <param name="point">The hit seen from above, in unit space.</param>
    /// <param name="lift">Height of the hit, the sine of its latitude.</param>
    /// <param name="surface">The hit in 3D, for the hover dot.</param>
    /// <returns>False when the ray misses the membrane.</returns>
    private bool TryHitSurface(Vector2 pixel, out Vector2 point, out float lift, out Vector3 surface)
    {
        Vector3 origin    = _camera.ProjectRayOrigin(pixel);
        Vector3 direction = _camera.ProjectRayNormal(pixel);

        point   = Vector2.Zero;
        lift    = 0f;
        surface = Vector3.Zero;
        float   stretch = 1f;
        float   height  = CellVisual3D.FLATTEN;
        Vector3? hit    = null;
        for (int step = 0; step < SURFACE_RAY_STEPS; step++)
        {
            Vector3? next = RayEllipsoid(origin, direction, stretch, height);
            if (null == next)
            {
                break;
            }
            float angle = Mathf.Atan2(next.Value.Z, next.Value.X);
            hit     = next;
            stretch = Draft.ShapeRadius(angle);
            height  = Draft.MembraneHeight(angle, new Vector2(next.Value.X, next.Value.Z).Length() / stretch);
        }

        if (null == hit)
        {
            return false;
        }
        point   = new Vector2(hit.Value.X, hit.Value.Z);
        lift    = Mathf.Clamp(hit.Value.Y / height, -1f, 1f);
        surface = hit.Value;
        return true;
    }

    /// <summary>First hit of a ray on the ellipsoid with outline radius stretch and half height height, null on a miss.</summary>
    private static Vector3? RayEllipsoid(Vector3 origin, Vector3 direction, float stretch, float height)
    {
        var scale = new Vector3(1f / stretch, 1f / height, 1f / stretch);
        Vector3 o = origin * scale;
        Vector3 d = direction * scale;
        float a = d.Dot(d);
        float b = 2f * o.Dot(d);
        float c = o.Dot(o) - 1f;
        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            return null;
        }
        float t = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
        return t > 0f ? origin + direction * t : null;
    }

    // ===========================================================================
    // ===========================   DRAG AND GHOST   ============================
    // ===========================================================================
    //
    //   GetTileDrag       Starts dragging a tile: the bare part hangs at the cursor.
    //   CanDropPart       Over the cell: shows the ghost where the part would snap.
    //   DropPart          Places the dragged part at the ghost.
    //   GhostKind         The part whose ghost shows, null when none.
    //   PreviewPart       The part at its snapped spot while dragging, hidden when it does not fit there.
    //   HideGhost         Removes the ghost, the cursor icon shows again.
    //   ShowHoverDot      A glowing dot on the membrane under the cursor, red where the part finds no spot.
    //   HideHoverDot      Hides the dot.
    //   UpdateGhostHover  Hides ghost and dot once the mouse leaves the cell, pulses the dot.
    //   SurfaceHeight     Height of the membrane above a unit point, 0 outside.
    //
    //   While the ghost shows, the cursor icon hides, so the part seems to snap onto the
    //   cell. The drag ends in _Notification (REFRESH).
    //

    private const int DRAG_ICON_PIXELS = 96;
    private const float DOT_PULSE_SPEED = 8f;
    private static readonly Color DOT_FREE = new(1f, 0.85f, 0.55f);
    private static readonly Color DOT_TAKEN = new(1f, 0.3f, 0.25f);

    private Control _dragIcon;
    private PartInfo _dragged;
    private PartKind? _ghostKind;

    /// <summary>The part whose ghost shows on the cell, null when none does.</summary>
    public PartKind? GhostKind => _ghostKind;
    private Vector2 _ghostPoint;
    private float _ghostLift;
    private CellBody _ghostBody;
    private MeshInstance3D _hoverDot;
    private float _dotTime;
    private readonly StandardMaterial3D _hoverDotMaterial = new()
    {
        AlbedoColor              = DOT_FREE,
        EmissionEnabled          = true,
        Emission                 = DOT_FREE,
        EmissionEnergyMultiplier = 1.4f,
        NoDepthTest              = true,     // stays visible inside the translucent membrane
    };

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
        var kind = (PartKind)data.AsInt32();
        ShowHoverDot(kind, position);
        if (TryHitSurface(position, out Vector2 spot, out float lift, out _))
        {
            PreviewPart(kind, spot, lift);
        }
        else
        {
            HideGhost();
        }
        return true;
    }

    private void DropPart(Vector2 position, Variant data)
    {
        var kind = (PartKind)data.AsInt32();
        HideGhost();
        HideHoverDot();
        if (!TryHitSurface(position, out Vector2 spot, out float lift, out _) || !TryPlace(kind, spot, lift))
        {
            Log.Verbose("CellEditor", $"{kind} dropped where it does not fit -> not placed.");
        }
    }

    /// <summary>Shows the ghost of a part where it would snap, used while dragging, hovering and by the smoke test.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="point">(Required) Position in unit space (core radius 1, +X forward).</param>
    /// <param name="lift">(Optional) Height on the membrane for rim parts, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>True when the part fits there and its ghost shows, false when the ghost is hidden.</returns>
    public bool PreviewPart(PartKind kind, Vector2 point, float lift = 0f)
    {
        if (!Draft.TrySnap(kind, point, out Vector2 snapped, out float snappedLift, lift))
        {
            HideGhost();
            return false;
        }
        if (kind == _ghostKind && snapped.IsEqualApprox(_ghostPoint) && Mathf.IsEqualApprox(snappedLift, _ghostLift))
        {
            return true;
        }

        // Ghost: a parts-only body with just this part, rebuilt only when kind or spot change
        var design = new CellDesign();
        design.SetShape(Draft.Shape);
        design.SetHeights(Draft.Heights);
        design.TryPlace(kind, snapped, 0, snappedLift);
        _ghostBody.Design     = design;
        _ghostKind            = kind;
        _ghostPoint           = snapped;
        _ghostLift            = snappedLift;
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
        _ghostKind        = null;
        _ghostBody.Design = new CellDesign();
    }

    private void ShowHoverDot(PartKind kind, Vector2 pixel)
    {
        if (!TryHitSurface(pixel, out Vector2 point, out float lift, out Vector3 surface))
        {
            HideHoverDot();
            return;
        }
        Color color = Draft.TrySnap(kind, point, out _, out _, lift) ? DOT_FREE : DOT_TAKEN;
        _hoverDotMaterial.AlbedoColor = color;
        _hoverDotMaterial.Emission    = color;
        _hoverDot.Position = surface;
        _hoverDot.Visible  = true;
    }

    private void HideHoverDot()
    {
        _hoverDot.Visible = false;
    }

    private void UpdateGhostHover(float delta)
    {
        bool isOverCell = _previewContainer.GetGlobalRect().HasPoint(_previewContainer.GetGlobalMousePosition());
        bool isInHand   = null != Selected || null != _dragged;
        if (!isOverCell || !isInHand || !_root.Visible)
        {
            HideGhost();
            HideHoverDot();
            return;
        }
        _dotTime += delta;
        _hoverDot.Scale = Vector3.One * (0.07f + 0.015f * Mathf.Sin(_dotTime * DOT_PULSE_SPEED));
    }

    /// <summary>Height of the membrane top above a unit point, 0 outside (CellDesign.TopHeight).</summary>
    private float SurfaceHeight(Vector2 point)
    {
        return Draft.TopHeight(point);
    }

    // ===========================================================================
    // ===========================   SHAPE AND COLOR   ===========================
    // ===========================================================================
    //
    //   IsOnEdge       Whether a unit point lies on the band around the membrane edge.
    //   BeginShaping   Remembers the grabbed angle and the outline before the drag.
    //   ShapeTowards   Moves the grabbed edge to the cursor, neighbors follow softly.
    //   EdgeWeight     How much a shape point follows the grab, 1 at it, 0 beyond EDGE_FALLOFF.
    //   RaiseHeight    Raises or flattens the membrane at one height handle, neighbors follow softly.
    //   HeightHandlePosition  Where a height handle floats: above the upper membrane at its angle.
    //   HeightHandleAt        The height handle under a pixel, -1 for none.
    //   UpdateHeightHandles   Shows the handles with empty hands, the active one bigger.
    //   PickColor      Recolors the draft, null goes back to the stage color.
    //   ShowColor      Paints preview, ghost and the selected swatch in the draft color.
    //

    private const float EDGE_INNER = 0.75f;          // band around the edge, as share of the outline radius
    private const float EDGE_OUTER = 1.35f;
    private const float EDGE_FALLOFF = 1.2f;         // radians: neighbors this far away do not follow any more
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

    private const float HEIGHT_PER_PIXEL = 0.006f;   // height factor per pixel of vertical mouse drag
    private const float HEIGHT_HANDLE_LIFT = 0.7f;   // handles sit over the upper membrane at this lift
    private const float HEIGHT_HANDLE_GAP = 0.1f;    // floating above the surface
    private const float HANDLE_PICK_PIXELS = 16f;
    private int _heightDrag = -1;
    private int _heightHover = -1;
    private float _handleTime;
    private readonly List<MeshInstance3D> _heightHandles = new();
    private readonly StandardMaterial3D _handleMaterial = new()
    {
        AlbedoColor              = new Color(1f, 0.95f, 0.8f),
        EmissionEnabled          = true,
        Emission                 = new Color(1f, 0.85f, 0.55f),
        EmissionEnergyMultiplier = 1.2f,
    };

    /// <summary>Raises (or with a negative change flattens) the membrane at one height handle, free of charge.</summary>
    /// <param name="index">(Required) Handle, 0 forward, counting like CellDesign.Heights.</param>
    /// <param name="change">(Required) Change of the height factor at the handle, neighbors get EdgeWeight of it.</param>
    /// <example><code>editor.RaiseHeight(4, 0.5f);   // a hump at the back</code></example>
    public void RaiseHeight(int index, float change)
    {
        if (index < 0 || index >= CellDesign.SHAPE_POINTS)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {CellDesign.SHAPE_POINTS - 1}, got '{index}'.");
        }
        float grab    = Mathf.Tau * index / CellDesign.SHAPE_POINTS;
        var   heights = new float[CellDesign.SHAPE_POINTS];
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] = Draft.Heights[i] + change * EdgeWeight(Mathf.Tau * i / heights.Length, grab);
        }
        Draft.SetHeights(heights);
    }

    private Vector3 HeightHandlePosition(int index)
    {
        float angle = Mathf.Tau * index / CellDesign.SHAPE_POINTS;
        return Draft.SurfacePoint(angle, HEIGHT_HANDLE_LIFT) + Vector3.Up * HEIGHT_HANDLE_GAP;
    }

    private int HeightHandleAt(Vector2 pixel)
    {
        if (null != Selected || !_root.Visible)
        {
            return -1;
        }
        int   closest  = -1;
        float bestDist = HANDLE_PICK_PIXELS;
        for (int i = 0; i < CellDesign.SHAPE_POINTS; i++)
        {
            Vector3 position = HeightHandlePosition(i);
            if (_camera.IsPositionBehind(position))
            {
                continue;
            }
            float distance = _camera.UnprojectPosition(position).DistanceTo(pixel);
            if (distance < bestDist)
            {
                bestDist = distance;
                closest  = i;
            }
        }
        return closest;
    }

    private void UpdateHeightHandles(float delta)
    {
        _handleTime += delta;
        while (_heightHandles.Count < CellDesign.SHAPE_POINTS)
        {
            MeshInstance3D handle = CellVisual3D.AddMesh(_effects, CellVisual3D.SphereMesh(true), _handleMaterial, Vector3.Zero, Vector3.One * 0.06f);
            handle.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _heightHandles.Add(handle);
        }
        bool isShown = null == Selected && null == _ghostKind && _root.Visible;
        for (int i = 0; i < _heightHandles.Count; i++)
        {
            _heightHandles[i].Visible = isShown;
            if (!isShown)
            {
                continue;
            }
            bool  isActive = i == _heightDrag || i == _heightHover;
            float size     = isActive ? 0.09f : 0.06f + 0.008f * Mathf.Sin(_handleTime * 3f + i);
            _heightHandles[i].Position = HeightHandlePosition(i);
            _heightHandles[i].Scale    = Vector3.One * size;
        }
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
            EditorStyle.ApplySwatch(swatch, swatchColor ?? _player.StageColor, swatchColor == Draft.Color);
        }
    }

    // ===========================================================================
    // ==============================   FEEDBACK   ===============================
    // ===========================================================================
    //
    //   PartPoint   Where a placed part sits on the cell, for the ring burst.
    //   Celebrate   Punches the cell to a scale and sends a ring out from the part.
    //   _Process    Turns the dotted rim of the petri dish, updates the ghost (DRAG AND GHOST),
    //               the gizmo (PREVIEW INPUT) and the height handles (SHAPE AND COLOR).
    //

    private const float BURST_SECONDS = 0.45f;
    private const float DISH_TURN_SPEED = 0.12f;     // radians per second

    /// <summary>Where a placed part sits on the cell: on the membrane for rim parts, above an inner part.</summary>
    private Vector3 PartPoint(PlacedPart part)
    {
        if (Placement.Rim == part.Info.Placement)
        {
            return Draft.SurfacePoint(part.Angle, part.Lift);
        }
        return Plane3D.To3D(part.Position, Mathf.Max(SurfaceHeight(part.Position), 0.05f));
    }

    private void Celebrate(Vector3 position, Color color, float punch)
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
        MeshInstance3D ring = CellVisual3D.AddMesh(_effects, CellVisual3D.TorusMesh(), material, position, Vector3.One * 0.1f);
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
        UpdateGizmo();
        UpdateHeightHandles((float)delta);
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
                TryPlace(_ghostKind.Value, _ghostPoint, _ghostLift);
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
            title.ZIndex = 1;                                  // above the floating parts reaching up
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
        EditorStyle.ApplyFloating(button, EditorStyle.DIET_COLORS[info.Diet]);

        // The part floats free: bigger than its spot, reaching above it and under the texts, bobbing
        var icon = new PartIcon(info.Kind, _player.BodyColor, TILE_ICON_PIXELS, isPartOnly: true)
        {
            Position = new Vector2((TILE_SIZE.X - TILE_ICON_PIXELS) * 0.5f, TILE_ICON_TOP),
        };
        button.AddChild(icon);
        EditorStyle.Bob(icon, TILE_BOB_PIXELS, _tiles.Count * 0.23f);

        // Name and cost at the bottom, drawn above every icon (also the ones of the next row)
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End, ZIndex = 1 };
        stack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 2);
        stack.AddThemeConstantOverride("separation", -2);
        button.AddChild(stack);
        Label name = NewLabel(14);
        Label cost = NewLabel(12);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        cost.HorizontalAlignment = HorizontalAlignment.Center;
        cost.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[info.Diet].Lightened(0.3f));
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
    private const int TILE_ICON_PIXELS = 124;           // wider than the spot: the part reaches over its edges
    private const float TILE_ICON_TOP = -22f;           // and above it, under the texts of the row above
    private const float TILE_BOB_PIXELS = 4f;
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
        foreach (Color? swatchColor in new Color?[] { null }.Concat(EditorStyle.BODY_SWATCHES.Select(color => (Color?)color)))
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(30, 30), FocusMode = Control.FocusModeEnum.None };
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

        // Ghost and hover dot of DRAG AND GHOST, both hidden until a part hovers
        _ghostBody = new CellBody { Radius = 1f, IsPartsOnly = true };
        viewport.AddChild(_ghostBody);
        _hoverDot = CellVisual3D.AddMesh(_effects, CellVisual3D.SphereMesh(true), _hoverDotMaterial, Vector3.Zero, Vector3.One * 0.07f);
        _hoverDot.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _hoverDot.Visible    = false;

        // Gizmo on the picked part (PREVIEW INPUT)
        _gizmo = new PartGizmo { Camera = _camera };
        viewport.AddChild(_gizmo);
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
