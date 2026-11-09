// NOTE:
//   Creature editor of the organism stage, opened by Main after the evolve cutscene
//   (first shaping) and later with E. Looks like the CellEditor: the panel slides in
//   from the left over the greyed-out, paused game.
//   First shaping: two cards ask what the clay becomes, fish or marine mammal (the
//   type stays for good). Then the clay blob sits on a sculptor's turntable: a glowing
//   handle above every vertebra is dragged up and down to make the body thicker or
//   thinner, + and - add or remove vertebrae, parts from the palette are clicked onto
//   the body (tail and nose take one part each, side parts come in pairs). Left drag
//   elsewhere turns the view, the wheel zooms, right click removes the closest part
//   and refunds it. "Done" hands the body plan to the player.
//
//   editor.Open(player, isFirstShaping: true);   // later: Closed signal

//
//   Chapters:
//     STATE AND API   Signal, draft, points and the calls Main and the smoke test use.
//     SLIDE           Slides the panel in and out and fades the grey-out.
//     PREVIEW INPUT   Handles, placing, removing, orbit and zoom on the 3D clay.
//     REFRESH         Phase, title, points, info card and tiles.
//     BUILD UI        Grey-out, panel, type cards, palette, 3D turntable and footer.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreatureEditor : CanvasLayer
{
    // ===========================================================================
    // ============================   STATE AND API   ============================
    // ===========================================================================
    //
    //   IsOpen          Whether the editor is shown (false right on Finish).
    //   IsChoosingType  The first shaping still waits for fish or marine mammal.
    //   Points          Evolution points left: the player's, the organism bonus, minus what was spent.
    //   Open            Copies the clay blob (first shaping) or the creature into a draft.
    //   ChooseType      Fish or marine mammal, the clay takes its shape and the palette opens.
    //   Select          Picks a part from the palette.
    //   TryPlace        Places a part at a spine share if affordable and free.
    //   RemoveAt        Removes the closest part and refunds it.
    //   SetGirth        Thickness of one vertebra, free of charge.
    //   AddVertebra     One more vertebra in the middle, free of charge.
    //   RemoveVertebra  One vertebra less, free of charge.
    //   Finish          Hands the draft to the player and slides out.
    //

    [Signal] public delegate void ClosedEventHandler();

    private Player _player;
    private int _spent;
    private int _bonus;
    private bool _isFirstShaping;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool IsChoosingType { get; private set; }
    public CreatureDesign Draft { get; private set; } = CreatureDesign.Blob();
    public CreaturePartKind? Selected { get; private set; }
    public int Points => null == _player ? 0 : _player.EvolutionPoints + _bonus - _spent;

    /// <summary>Shows the editor for the player, the game must already be paused.</summary>
    /// <param name="player">(Required) The player whose body plan and points are edited.</param>
    /// <param name="isFirstShaping">(Required) True right after the evolve cutscene: type choice, clay blob, bonus points.</param>
    /// <example><code>editor.Open(player, isFirstShaping: true);</code></example>
    public void Open(Player player, bool isFirstShaping)
    {
        _player         = player;
        _spent          = 0;
        _isFirstShaping = isFirstShaping;
        _bonus          = isFirstShaping ? Player.ORGANISM_BONUS_EP : 0;
        _isOpen         = true;
        _hovered        = null;
        _dragHandle     = -1;
        Selected        = null;
        IsChoosingType  = isFirstShaping;
        Draft           = new CreatureDesign();
        Draft.CopyFrom(isFirstShaping ? CreatureDesign.Blob() : player.Creature);

        _preview.Design    = Draft;
        _preview.BodyColor = player.BodyColor;
        ResetCamera();

        _root.Visible = true;
        _lastPoints   = -1;
        Log.Verbose("CreatureEditor", $"Opened (first shaping: {isFirstShaping}) with {Points} EP.");
        BuildTypeCards();
        BuildPalette();
        Refresh();
        SlideTo(1f);
    }

    /// <summary>Sets the body type of the first shaping, the clay takes its shape.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    public void ChooseType(CreatureType type)
    {
        if (!IsChoosingType)
        {
            return;
        }
        Draft.Type     = type;
        IsChoosingType = false;
        Log.Verbose("CreatureEditor", $"Type chosen: {type}.");
        BuildPalette();
        Refresh();
        Celebrate(Vector3.Zero, TypeAccent(type), 1.12f);
    }

    /// <summary>Picks a part, null clears the selection.</summary>
    public void Select(CreaturePartKind? kind)
    {
        Selected = kind;
        UpdateInfo();
        UpdateTiles();
    }

    /// <summary>Places a part if the body type may use it, it is affordable and its spot is free.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Share of the spine, 0 tail .. 1 nose.</param>
    /// <returns>True when the part was placed and paid.</returns>
    public bool TryPlace(CreaturePartKind kind, float t)
    {
        CreaturePartInfo info = CreatureCatalog.Get(kind);
        if (IsChoosingType || info.Cost > Points)
        {
            Log.Verbose("CreatureEditor", $"{kind} costs {info.Cost} EP, {Points} left (choosing: {IsChoosingType}) -> not placed.");
            return false;
        }
        if (!Draft.TryPlace(kind, t, info.Cost))
        {
            return false;
        }
        _spent += info.Cost;
        CreaturePart placed = Draft.Parts[^1];
        Celebrate(PartPoint(placed), TypeAccent(Draft.Type), 1.08f);
        Refresh();
        return true;
    }

    /// <summary>Removes the part closest to a spine share and refunds what was paid for it.</summary>
    /// <returns>True when a part was removed.</returns>
    public bool RemoveAt(float t)
    {
        CreaturePart? removed = Draft.RemoveNear(t);
        if (null == removed)
        {
            return false;
        }
        _spent -= removed.Value.PaidCost;
        Celebrate(PartPoint(removed.Value), new Color(0.75f, 0.85f, 0.9f), 0.95f);
        Refresh();
        return true;
    }

    /// <summary>Sets the girth of one vertebra, the handles follow every frame.</summary>
    public void SetGirth(int index, float girth)
    {
        Draft.SetGirth(index, girth);
    }

    public bool AddVertebra()
    {
        bool isAdded = Draft.AddVertebra();
        if (isAdded)
        {
            Celebrate(Vector3.Zero, TypeAccent(Draft.Type), 1.05f);
            Refresh();
        }
        return isAdded;
    }

    public bool RemoveVertebra()
    {
        bool isRemoved = Draft.RemoveVertebra();
        if (isRemoved)
        {
            Celebrate(Vector3.Zero, TypeAccent(Draft.Type), 0.95f);
            Refresh();
        }
        return isRemoved;
    }

    /// <summary>Hands the draft and the points left to the player, the game resumes while the panel slides out.</summary>
    public void Finish()
    {
        if (IsChoosingType)
        {
            return;
        }
        if (_isFirstShaping)
        {
            _player.BecomeOrganism(Draft, Points);
        }
        else
        {
            _player.CommitCreature(Draft, Points);
        }
        _isOpen = false;
        SlideTo(0f);
        EmitSignal(SignalName.Closed);
    }

    /// <summary>Accent of a body type: ocean blue for fish, lagoon teal for marine mammals.</summary>
    private static Color TypeAccent(CreatureType type)
    {
        return CreatureType.Fish == type ? new Color(0.45f, 0.72f, 1f) : new Color(0.45f, 0.92f, 0.82f);
    }

    /// <summary>Where a part sits in the preview, for the ring burst.</summary>
    private Vector3 PartPoint(CreaturePart part)
    {
        float x = CreatureMount.Tail == part.Info.Mount ? Draft.TailTipX : Draft.XAt(part.T);
        float y = CreatureMount.Top == part.Info.Mount ? Draft.TopAt(x) : 0f;
        return new Vector3(x, y, 0f);
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
        if (target <= 0f)
        {
            _slideTween.TweenCallback(Callable.From(() => _root.Visible = false));
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
    //   OnPreviewInput   Left drag on a handle sculpts, a click places the selected part,
    //                    left drag elsewhere rotates, right click removes, the wheel zooms.
    //   _Process         Turns the turntable, moves handles and the hover marker.
    //   ResetCamera      Side view from slightly above, forward to the right.
    //   UpdateCamera     Orbit position from yaw, pitch and zoom.
    //   HandleAt         The vertebra handle under a pixel, -1 for none.
    //   HandlePosition   Where the handle of a vertebra floats above the back.
    //   RayToBody        First point of the body under a pixel (ray marching).
    //   Celebrate        Punches the clay and sends a ring out from a point.
    //

    private const float CAMERA_DISTANCE = 4.6f;
    private const float CAMERA_FOV = 40f;
    private const float START_PITCH = 0.35f;
    private const float MIN_PITCH = -1.2f;
    private const float MAX_PITCH = 1.45f;
    private const float ORBIT_SPEED = 0.008f;
    private const float DRAG_THRESHOLD = 5f;
    private const float ZOOM_STEP = 1.1f;
    private const float MIN_ZOOM = 0.55f;
    private const float MAX_ZOOM = 1.8f;
    private const float GIRTH_PER_PIXEL = 0.004f;
    private const float HANDLE_LIFT = 0.16f;          // above the back
    private const float HANDLE_PICK_PIXELS = 16f;
    private const int RAY_STEPS = 240;
    private const float TURNTABLE_SPEED = 0.15f;

    private float _yaw;
    private float _pitch;
    private float _zoom = 1f;
    private bool _isLeftDown;
    private bool _isOrbiting;
    private Vector2 _leftDownAt;
    private int _dragHandle = -1;
    private int _hoverHandle = -1;
    private Vector3? _hoverPoint;
    private float _time;                             // drives the breathing handles and the marker

    private void OnPreviewInput(InputEvent @event)
    {
        if (IsChoosingType)
        {
            return;
        }
        switch (@event)
        {
            // Wheel: zoom
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                _zoom = Mathf.Clamp(_zoom * (MouseButton.WheelUp == wheel.ButtonIndex ? 1f / ZOOM_STEP : ZOOM_STEP), MIN_ZOOM, MAX_ZOOM);
                UpdateCamera();
                break;

            // Right click: remove the part closest to the clicked spot
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right:
                if (RayToBody(right.Position) is Vector3 removeHit)
                {
                    RemoveAt(Draft.TAt(removeHit.X));
                }
                break;

            // Left press: a handle starts sculpting, otherwise maybe a click, maybe a rotation
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.Pressed:
                _dragHandle = HandleAt(left.Position);
                _isLeftDown = _dragHandle < 0;
                _isOrbiting = false;
                _leftDownAt = left.Position;
                break;

            // Left release: a click without rotation places the selected part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_isLeftDown && !_isOrbiting && null != Selected && RayToBody(left.Position) is Vector3 placeHit)
                {
                    TryPlace(Selected.Value, Draft.TAt(placeHit.X));
                }
                if (_dragHandle >= 0)
                {
                    Celebrate(HandlePosition(_dragHandle), TypeAccent(Draft.Type), 1.03f);
                }
                _dragHandle = -1;
                _isLeftDown = false;
                _isOrbiting = false;
                break;

            // Handle drag: up makes the vertebra thicker
            case InputEventMouseMotion sculpt when _dragHandle >= 0:
                SetGirth(_dragHandle, Draft.Girths[_dragHandle] - sculpt.Relative.Y * GIRTH_PER_PIXEL);
                break;

            // Left drag: rotate once past the threshold
            case InputEventMouseMotion motion when _isLeftDown:
                _isOrbiting = _isOrbiting || motion.Position.DistanceTo(_leftDownAt) > DRAG_THRESHOLD;
                if (_isOrbiting)
                {
                    _yaw  -= motion.Relative.X * ORBIT_SPEED;
                    _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * ORBIT_SPEED, MIN_PITCH, MAX_PITCH);
                    UpdateCamera();
                }
                break;

            // Hover: handles light up, a selected part shows where it would go
            case InputEventMouseMotion hover:
                _hoverHandle = HandleAt(hover.Position);
                _hoverPoint  = null != Selected && _hoverHandle < 0 ? RayToBody(hover.Position) : null;
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (!_root.Visible)
        {
            return;
        }
        float dt = (float)delta;
        _time += dt;
        _turntable.RotateY(dt * TURNTABLE_SPEED);

        // Handles: one per vertebra above the back, hidden while the type is open
        while (_handles.Count < Draft.Girths.Count)
        {
            MeshInstance3D handle = CellVisual3D.AddMesh(_previewRoot, CellVisual3D.SphereMesh(true), _handleMaterial, Vector3.Zero, Vector3.One * 0.06f);
            handle.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _handles.Add(handle);
        }
        for (int i = 0; i < _handles.Count; i++)
        {
            bool isShown = !IsChoosingType && i < Draft.Girths.Count;
            _handles[i].Visible = isShown;
            if (!isShown)
            {
                continue;
            }
            bool  isActive = i == _dragHandle || i == _hoverHandle;
            float size     = isActive ? 0.09f : 0.06f + 0.008f * Mathf.Sin(_time * 3f + i);
            _handles[i].Position = HandlePosition(i);
            _handles[i].Scale    = Vector3.One * size;
        }

        // Hover marker where the selected part would land
        _marker.Visible = null != _hoverPoint;
        if (_hoverPoint is Vector3 point)
        {
            _marker.Position = point;
            _marker.Scale    = Vector3.One * (0.07f + 0.015f * Mathf.Sin(_time * 8f));
        }
    }

    private void ResetCamera()
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

    private int HandleAt(Vector2 pixel)
    {
        int   closest  = -1;
        float bestDist = HANDLE_PICK_PIXELS;
        for (int i = 0; i < Draft.Girths.Count; i++)
        {
            Vector3 position = HandlePosition(i);
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

    private Vector3 HandlePosition(int index)
    {
        float x = Draft.VertebraX(index);
        return new Vector3(x, Draft.TopAt(x) + HANDLE_LIFT, 0f);
    }

    /// <summary>
    /// The lofted body has no simple formula for a ray hit, so the ray is walked in
    /// small steps through the box around the camera target until the first step lands
    /// inside (CreatureDesign.Contains). Precise enough to pick a spot on the spine.
    /// </summary>
    /// <returns>The first point inside the body, null when the ray misses it.</returns>
    private Vector3? RayToBody(Vector2 pixel)
    {
        Vector3 origin    = _camera.ProjectRayOrigin(pixel);
        Vector3 direction = _camera.ProjectRayNormal(pixel);
        float   reach     = CAMERA_DISTANCE * _zoom * 2f;
        float   step      = reach / RAY_STEPS;
        for (int i = 0; i < RAY_STEPS; i++)
        {
            Vector3 point = origin + direction * (i * step);
            if (Draft.Contains(point))
            {
                return point;
            }
        }
        return null;
    }

    private const float BURST_SECONDS = 0.45f;

    private void Celebrate(Vector3 point, Color color, float punch)
    {
        // Clay punch: out and elastic back
        Tween bounce = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        bounce.TweenProperty(_preview, "scale", Vector3.One * punch, 0.08f);
        bounce.TweenProperty(_preview, "scale", Vector3.One, 0.35f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

        // Ring burst at the point, its own material so the fade does not touch other rings
        var material = new StandardMaterial3D
        {
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor  = color,
        };
        MeshInstance3D ring = CellVisual3D.AddMesh(_previewRoot, CellVisual3D.TorusMesh(), material, point, Vector3.One * 0.1f);
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Tween burst = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        burst.TweenMethod(Callable.From<float>(t =>
        {
            ring.Scale           = new Vector3(1f, 0.4f, 1f) * Mathf.Lerp(0.1f, 0.8f, t);
            material.AlbedoColor = new Color(color, 1f - t);
        }), 0f, 1f, BURST_SECONDS).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        burst.TweenCallback(Callable.From(ring.QueueFree));
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh         Phase (cards or sculpting), title, points, buttons, info card and tiles.
    //   UpdateInfo      Name and description of the hovered or selected part, else of the body type.
    //   _Notification   Re-translates after a locale change.
    //   BuildTypeCards  Two cards with a live example creature each, first shaping only.
    //   BuildPalette    One icon tile per part the body type may use, popping in.
    //   UpdateTiles     Name, cost, pressed and affordable state of every tile.
    //

    private const float TILE_HOVER_SCALE = 1.06f;
    private const float TILE_POP_DELAY = 0.12f;
    private const float TILE_POP_STAGGER = 0.03f;

    private sealed record PaletteTile(CreaturePartInfo Info, Button Button, Control Icon, Label Name, Label Cost);

    private readonly List<PaletteTile> _tiles = new();
    private CreaturePartInfo _hovered;
    private int _lastPoints = -1;

    /// <summary>Updates every widget.</summary>
    public void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }
        _titleLabel.Text          = Tr(IsChoosingType ? "CREATURE_CHOOSE_TITLE" : "CREATURE_EDITOR_TITLE");
        _pointsLabel.Text         = string.Format(Tr("EDITOR_POINTS"), Points);
        _doneButton.Text          = Tr("EDITOR_DONE");
        _addButton.Text           = Tr("CREATURE_ADD_VERTEBRA");
        _removeButton.Text        = Tr("CREATURE_REMOVE_VERTEBRA");
        _cards.Visible            = IsChoosingType;
        _sculpt.Visible           = !IsChoosingType;
        _doneButton.Visible       = !IsChoosingType;
        _addButton.Disabled       = Draft.Girths.Count >= CreatureDesign.MAX_VERTEBRAE;
        _removeButton.Disabled    = Draft.Girths.Count <= CreatureDesign.MIN_VERTEBRAE;
        foreach ((CreatureType type, Label name, Label text) in _cardTexts)
        {
            name.Text = Tr(TypeKey(type, "NAME"));
            text.Text = Tr(TypeKey(type, "DESC"));
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
        EditorStyle.ApplyButton(_doneButton, TypeAccent(Draft.Type));

        UpdateInfo();
        UpdateTiles();
    }

    private static string TypeKey(CreatureType type, string suffix)
    {
        return $"CREATURE_TYPE_{type}_{suffix}".ToUpperInvariant();
    }

    private void UpdateInfo()
    {
        CreaturePartInfo info = _hovered ?? (null == Selected ? null : CreatureCatalog.Get(Selected.Value));
        if (IsChoosingType)
        {
            _infoName.Text = "";
            _infoText.Text = "";
            return;
        }
        if (null == info)
        {
            _infoName.Text = Tr(TypeKey(Draft.Type, "NAME"));
            _infoText.Text = Tr(TypeKey(Draft.Type, "DESC"));
            _infoName.AddThemeColorOverride("font_color", TypeAccent(Draft.Type));
            return;
        }
        _infoName.Text = Tr(info.NameKey);
        _infoText.Text = Tr(info.DescriptionKey);
        _infoName.AddThemeColorOverride("font_color", TypeAccent(Draft.Type).Lightened(0.2f));
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _root)
        {
            Refresh();
        }
    }

    private readonly List<(CreatureType Type, Label Name, Label Text)> _cardTexts = new();

    /// <summary>Rebuilt on every Open, the example creatures take the player color.</summary>
    private void BuildTypeCards()
    {
        foreach (Node child in _cards.GetChildren())
        {
            _cards.RemoveChild(child);
            child.QueueFree();
        }
        _cardTexts.Clear();
        if (!IsChoosingType)
        {
            return;
        }

        int index = 0;
        foreach (CreatureType type in new[] { CreatureType.Fish, CreatureType.MarineMammal })
        {
            Color accent = TypeAccent(type);
            var card = new Button
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical   = Control.SizeFlags.ExpandFill,
                FocusMode           = Control.FocusModeEnum.None,
                AutoTranslateMode   = AutoTranslateModeEnum.Disabled,
            };
            EditorStyle.ApplyTile(card, accent);
            _cards.AddChild(card);

            var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            stack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 14);
            stack.AddThemeConstantOverride("separation", 6);
            card.AddChild(stack);
            Control icon = NewCreatureIcon(CreatureDesign.Example(type), _player.BodyColor, 220, isTurning: true);
            icon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            stack.AddChild(icon);
            Label name = NewLabel(30);
            GameFonts.UseDisplay(name);
            name.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
            Label text = NewLabel(15);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            foreach (Label label in new[] { name, text })
            {
                label.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(label);
            }
            _cardTexts.Add((type, name, text));

            card.Pressed      += () => ChooseType(type);
            card.MouseEntered += () => EditorStyle.ScaleTo(card, 1.03f);
            card.MouseExited  += () => EditorStyle.ScaleTo(card, 1f);
            EditorStyle.PopIn(card, TILE_POP_DELAY + index * 0.12f);
            index++;
        }
    }

    /// <summary>Rebuilt on every Open and type choice: the type decides which parts are offered.</summary>
    private void BuildPalette()
    {
        foreach (Node child in _palette.GetChildren())
        {
            _palette.RemoveChild(child);
            child.QueueFree();
        }
        _tiles.Clear();
        if (IsChoosingType)
        {
            return;
        }

        var   group  = new ButtonGroup { AllowUnpress = true };
        Color accent = TypeAccent(Draft.Type);
        foreach (CreaturePartInfo info in CreatureCatalog.ForType(Draft.Type))
        {
            var button = new Button
            {
                CustomMinimumSize = TILE_SIZE,
                ToggleMode        = true,
                ButtonGroup       = group,
                FocusMode         = Control.FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };
            EditorStyle.ApplyTile(button, accent);
            _palette.AddChild(button);

            // Icon, name and cost stacked, they let every click through to the button
            var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            stack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 4);
            stack.AddThemeConstantOverride("separation", 0);
            button.AddChild(stack);
            Control icon = NewCreatureIcon(IconDesign(info), _player.BodyColor, TILE_ICON_PIXELS, isTurning: false);
            icon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            Label name = NewLabel(12);
            Label cost = NewLabel(11);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            cost.HorizontalAlignment = HorizontalAlignment.Center;
            cost.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
            stack.AddChild(icon);
            stack.AddChild(name);
            stack.AddChild(cost);

            // Switching tiles unpresses the old one, that must not clear the new selection
            CreaturePartKind kind = info.Kind;
            button.Toggled += isPressed =>
            {
                if (isPressed) Select(kind);
                else if (kind == Selected) Select(null);
            };
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
            EditorStyle.PopIn(button, TILE_POP_DELAY + _tiles.Count * TILE_POP_STAGGER);
            _tiles.Add(new PaletteTile(info, button, icon, name, cost));
        }
    }

    /// <summary>A small slim body of the draft's type that carries only this part.</summary>
    private CreatureDesign IconDesign(CreaturePartInfo info)
    {
        CreatureDesign design = CreatureDesign.Blob();
        design.Type = Draft.Type;
        float t = CreaturePartKind.Eye == info.Kind ? 0.85f : 0.55f;
        design.TryPlace(info.Kind, t, 0);
        return design;
    }

    private void UpdateTiles()
    {
        foreach (PaletteTile tile in _tiles)
        {
            bool isAvailable = tile.Info.Cost <= Points;
            tile.Name.Text       = Tr(tile.Info.NameKey);
            tile.Cost.Text       = $"{tile.Info.Cost} EP";
            tile.Button.Disabled = !isAvailable;
            tile.Button.SetPressedNoSignal(tile.Info.Kind == Selected);
            SetAlpha(tile.Icon, isAvailable ? 1f : 0.3f);
            SetAlpha(tile.Name, isAvailable ? 1f : 0.45f);
        }
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   _Ready            Keeps running while paused, builds everything hidden and closed.
    //   BuildPanel        Grey-out over the game, the panel with header, cards, sculpt area and footer.
    //   BuildFooter       Info card, points badge and the done button.
    //   BuildPreview      SubViewport with orbit camera, turntable, the clay, handles and marker.
    //   NewCreatureIcon   A live 3D creature in its own small viewport (cards and tiles).
    //   NewLabel          Label with outline, translated here.
    //   NewPillButton     Small pill button without focus.
    //   SetAlpha          Fades a canvas item.
    //

    private const float PANEL_SHARE = 0.65f;
    private const float EDGE_PIXELS = 40f;
    private const int PANEL_MARGIN = 20;
    private const int TILE_ICON_PIXELS = 64;
    private const float TURNTABLE_RADIUS = 1.7f;
    private static readonly Vector2 TILE_SIZE = new(108, 104);
    private static readonly Shader GREYOUT_SHADER = GD.Load<Shader>("res://shaders/greyout.gdshader");
    private static readonly Shader PANEL_SHADER = GD.Load<Shader>("res://shaders/editor_panel.gdshader");

    private Control _root;
    private ShaderMaterial _greyout;
    private ShaderMaterial _panelBackground;
    private Control _panel;
    private Label _titleLabel;
    private HBoxContainer _cards;
    private HBoxContainer _sculpt;
    private VBoxContainer _palette;
    private PanelContainer _pointsBadge;
    private Label _pointsLabel;
    private Label _infoName;
    private Label _infoText;
    private Button _doneButton;
    private Button _addButton;
    private Button _removeButton;
    private Camera3D _camera;
    private Node3D _previewRoot;
    private Node3D _turntable;
    private CreatureVisual3D _preview;
    private MeshInstance3D _marker;
    private readonly List<MeshInstance3D> _handles = new();
    private readonly StandardMaterial3D _handleMaterial = new()
    {
        AlbedoColor              = new Color(1f, 0.95f, 0.8f),
        EmissionEnabled          = true,
        Emission                 = new Color(1f, 0.85f, 0.55f),
        EmissionEnergyMultiplier = 1.2f,
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 20;
        BuildPanel();
        _root.Visible = false;
        ApplySlide(0f);
        GetViewport().SizeChanged += () => ApplySlide(_slide);
    }

    private void BuildPanel()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // Grey-out over the paused game (the clay blob of the cutscene breathes behind it)
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

        _titleLabel = NewLabel(34);
        GameFonts.UseDisplay(_titleLabel);
        layout.AddChild(_titleLabel);

        // Type cards of the first shaping
        _cards = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _cards.AddThemeConstantOverride("separation", 18);
        layout.AddChild(_cards);

        // Sculpting: tile palette left, turntable right
        _sculpt = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _sculpt.AddThemeConstantOverride("separation", 12);
        layout.AddChild(_sculpt);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize    = new Vector2(TILE_SIZE.X + 28, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _sculpt.AddChild(scroll);
        var paletteMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            paletteMargin.AddThemeConstantOverride(side, 6);
        }
        scroll.AddChild(paletteMargin);
        _palette = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _palette.AddThemeConstantOverride("separation", 8);
        paletteMargin.AddChild(_palette);
        BuildPreview(_sculpt);

        BuildFooter(layout);
    }

    private void BuildFooter(Container parent)
    {
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 14);
        parent.AddChild(footer);

        // Info card: name in the type color, description below, fixed height so nothing jumps
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

        _doneButton = NewPillButton(new Vector2(150, 48), 20);
        _doneButton.Pressed      += Finish;
        _doneButton.MouseEntered += () => EditorStyle.ScaleTo(_doneButton, 1.06f);
        _doneButton.MouseExited  += () => EditorStyle.ScaleTo(_doneButton, 1f);
        footer.AddChild(_doneButton);
    }

    private void BuildPreview(Container parent)
    {
        var container = new SubViewportContainer
        {
            Stretch             = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ExpandFill,
            MouseFilter         = Control.MouseFilterEnum.Stop,
        };
        container.GuiInput += OnPreviewInput;
        parent.AddChild(container);

        SubViewport viewport = PartIcon.NewStudio(new Vector2I(440, 440));
        container.AddChild(viewport);
        _camera = new Camera3D { Fov = CAMERA_FOV, Current = true };
        viewport.AddChild(_camera);
        _previewRoot = new Node3D();
        viewport.AddChild(_previewRoot);

        // Sculptor's turntable: a slowly turning slab with a lighter rim and a few studs
        _turntable = new Node3D { Position = new Vector3(0f, -0.95f, 0f) };
        _previewRoot.AddChild(_turntable);
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
        _preview = new CreatureVisual3D { Design = Draft, Knead = CreatureVisual3D.EDITOR_KNEAD, SwimScale = 0.3f, Seed = 7f };
        _previewRoot.AddChild(_preview);

        // Marker where a selected part would land
        _marker = CellVisual3D.AddMesh(_previewRoot, CellVisual3D.SphereMesh(true), _handleMaterial, Vector3.Zero, Vector3.One * 0.07f);
        _marker.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _marker.Visible    = false;

        // + and - for vertebrae, bottom left over the turntable
        var buttons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        buttons.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 10);
        buttons.GrowVertical = Control.GrowDirection.Begin;
        buttons.AddThemeConstantOverride("separation", 10);
        container.AddChild(buttons);
        _addButton    = NewPillButton(new Vector2(0, 40), 16);
        _removeButton = NewPillButton(new Vector2(0, 40), 16);
        _addButton.Pressed    += () => AddVertebra();
        _removeButton.Pressed += () => RemoveVertebra();
        foreach (Button button in new[] { _removeButton, _addButton })
        {
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            buttons.AddChild(button);
        }
    }

    /// <summary>A live creature in its own small studio, seen from the front flank.</summary>
    /// <param name="design">(Required) The creature shown.</param>
    /// <param name="color">(Required) Body color.</param>
    /// <param name="pixels">(Required) Edge length of the square icon.</param>
    /// <param name="isTurning">(Required) Turns slowly on the spot (type cards).</param>
    private static Control NewCreatureIcon(CreatureDesign design, Color color, int pixels, bool isTurning)
    {
        var container = new SubViewportContainer
        {
            Stretch           = true,
            CustomMinimumSize = new Vector2(pixels, pixels),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        SubViewport viewport = PartIcon.NewStudio(new Vector2I(pixels, pixels));
        container.AddChild(viewport);

        var visual = new CreatureVisual3D { Design = design, BodyColor = color, Knead = CreatureVisual3D.EDITOR_KNEAD };
        viewport.AddChild(visual);
        if (isTurning)
        {
            visual.Rotation = new Vector3(0f, -0.5f, 0f);
            Tween spin = visual.CreateTween().SetLoops().SetPauseMode(Tween.TweenPauseMode.Process);
            spin.TweenProperty(visual, "rotation:y", 0.5f, 3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            spin.TweenProperty(visual, "rotation:y", -0.5f, 3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        Vector3 offset = new Vector3(0.35f, 0.55f, 1f).Normalized() * 3.6f;
        var camera = new Camera3D { Fov = 40f, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), offset);
        viewport.AddChild(camera);
        return container;
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }

    private static Button NewPillButton(Vector2 minimumSize, int fontSize)
    {
        var button = new Button
        {
            CustomMinimumSize = minimumSize,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        return button;
    }

    private static void SetAlpha(CanvasItem item, float alpha)
    {
        Color modulate = item.Modulate;
        modulate.A    = alpha;
        item.Modulate = modulate;
    }
}
