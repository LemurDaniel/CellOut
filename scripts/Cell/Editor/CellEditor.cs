// NOTE:
//   Cell editor, opened by Main on E or the HUD button (never by itself) while the game
//   is paused. The panel slides in from the left (background: shaders/editor_panel.gdshader,
//   a wobbling membrane edge), the paused game on the right is greyed out
//   (shaders/greyout.gdshader). The palette shows one live 3D icon tile per part
//   (PartIcon), tinted per diet, locked ones dimmed. Dragging a tile hangs the bare
//   part at the cursor; over the rotatable 3D cell it snaps as a ghost to where it
//   would go and is placed on release. A click on a tile selects it, hovering the cell
//   shows the ghost, a click places it. Dragging the membrane edge stretches or squashes
//   the cell right there (free form, no mirroring), the swatches above the cell recolor it. Left drag
//   elsewhere rotates, the wheel zooms, right click removes the part under the cursor
//   (else the closest one) and refunds it. A placed or clicked part gets a gizmo
//   (PartGizmo): arrows shift it, rings turn it, looks only. The size slider in the
//   footer and the wheel over the picked part scale it. "Done" commits the draft
//   and slides the panel out.
//
//   This class is the coordinator: it keeps the public API, creates the components,
//   wires their events and routes the mouse input of the preview to them in a fixed
//   order. The state they share is the CellEditSession.
//     Panel/CellEditorPanel     Grey-out, sliding panel, header, footer, diet tint.
//     Panel/PartPalette         Icon tiles per diet, hover and drag of a tile.
//     Preview/CellPreview       Swatches, 3D cell in its petri dish, camera, feedback.
//     Preview/PartGhost         Ghost and hover dot of the part in hand.
//     Preview/PartPoser         Gizmo on the picked part.
//     Preview/MembraneSculptor  Edge stretching and the height handles.
//     Preview/MembraneGeometry  The math behind rays, handles and sculpting.
//   Looks and tweens: EditorStyle.
//
//   editor.Open(player);   // later: Closed signal

//
//   Chapters:
//     STATE AND API     Signal, session and the calls Main and the smoke test use.
//     ROUTE INPUT       Mouse input on the 3D cell and dropped parts, offered to the components in order.
//     REFRESH           Feeds the views, the per frame updates, locale change and drag end.
//     WIRE COMPONENTS   Creates panel, palette, preview and its components and connects them.
//

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
    //   SetPartSize     Size of the picked part (slider, wheel), free of charge, looks only.
    //   GhostKind      The part whose ghost shows, null when none.
    //   PreviewPart     The part at its snapped spot while dragging, hidden when it does not fit there.
    //   RaiseHeight     Raises or flattens the membrane at one height handle, neighbors follow softly.
    //   PickColor       Recolors the draft, null goes back to the stage color.
    //   Finish     Commits the draft and slides out.
    //

    [Signal] public delegate void ClosedEventHandler();

    private readonly CellEditSession _session = new();
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public CellDesign Draft => _session.Draft;
    public PartKind? Selected => _session.Selected;
    public int PickedPart => _session.PickedPart;
    public int Points => _session.Points;

    /// <summary>The part whose ghost shows on the cell, null when none does.</summary>
    public PartKind? GhostKind => _ghost?.Kind;

    /// <summary>Shows the editor for the player, the game must already be paused.</summary>
    /// <param name="player">(Required) The player whose design and points are edited.</param>
    public void Open(Player player)
    {
        _session.Begin(player);
        _isOpen = true;
        _poser.Reset();

        _preview.ShowCell();
        ShowColor();
        _preview.ResetCamera();

        _panel.Open();
        Log.Verbose("CellEditor", $"Opened at stage {player.Stage} with {Points} EP.");
        _palette.Rebuild();
        Refresh();
        _panel.SlideTo(1f);
    }

    /// <summary>Picks a part, null clears the selection. Only info card and tile states change.</summary>
    public void Select(PartKind? kind)
    {
        _session.Select(kind);
        UpdateInfo();
        UpdateSize();
        _palette.UpdateTiles();
    }

    /// <summary>Places a part if it is unlocked, affordable and fits there.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="point">(Required) Position in unit space (core radius 1, +X forward).</param>
    /// <param name="lift">(Optional) Height on the membrane for rim parts, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>True when the part was placed and paid.</returns>
    public bool TryPlace(PartKind kind, Vector2 point, float lift = 0f)
    {
        if (!_session.TryPlace(kind, point, lift))
        {
            return false;
        }
        _ghost.HideGhost();
        _preview.Celebrate(MembraneGeometry.PartPoint(Draft, Draft.Parts[^1]), EditorStyle.DIET_COLORS[PartCatalog.Get(kind).Diet], 1.1f);
        Select(null);                                // deselected, so the gizmo has focus until the next tile
        Refresh();
        return true;
    }

    /// <summary>Removes the part closest to a unit position and refunds what was paid for it.</summary>
    /// <returns>True when a part was removed.</returns>
    public bool RemoveAt(Vector2 point)
    {
        return ShowRemoved(_session.RemoveNear(point));
    }

    /// <summary>Removes one placed part and refunds what was paid for it.</summary>
    /// <param name="index">(Required) Index in Draft.Parts.</param>
    /// <returns>False for an index outside Draft.Parts.</returns>
    public bool RemovePart(int index)
    {
        return ShowRemoved(_session.RemovePart(index));
    }

    /// <summary>Ring burst where the removed part sat and fresh points, false when nothing was removed.</summary>
    private bool ShowRemoved(PlacedPart? removed)
    {
        if (null == removed)
        {
            return false;
        }
        _preview.Celebrate(MembraneGeometry.PartPoint(Draft, removed.Value), new Color(0.75f, 0.85f, 0.9f), 0.93f);
        Refresh();
        return true;
    }

    /// <summary>Picks a placed part for the gizmo.</summary>
    /// <param name="index">(Required) Index in Draft.Parts, -1 clears the pick.</param>
    public void PickPart(int index)
    {
        _session.PickPart(index);
        UpdateSize();
    }

    /// <summary>Shifts the picked part, clamped to CellDesign.MAX_PART_OFFSET.</summary>
    /// <param name="delta">(Required) Shift in the preview world, turned and scaled into the unit space of the cell.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.PickPart(0); editor.MovePickedPart(new Vector3(0f, 0.1f, 0f));</code></example>
    public bool MovePickedPart(Vector3 delta)
    {
        return _poser.Move(delta);
    }

    /// <summary>Turns the picked part around a world axis of the preview.</summary>
    /// <param name="axis">(Required) Normalized world axis.</param>
    /// <param name="angle">(Required) Radians, right hand rule.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.TurnPickedPart(Vector3.Up, 0.5f);</code></example>
    public bool TurnPickedPart(Vector3 axis, float angle)
    {
        return _poser.Turn(axis, angle);
    }

    /// <summary>Sets the size of the picked part, free of charge.</summary>
    /// <param name="size">(Required) 1 = normal, CellDesign.MIN_PART_SIZE..MAX_PART_SIZE.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.PickPart(0); editor.SetPartSize(1.3f);</code></example>
    public bool SetPartSize(float size)
    {
        if (!_session.SetPartSize(size))
        {
            return false;
        }
        UpdateSize();
        return true;
    }

    /// <summary>Shows the ghost of a part where it would snap, used while dragging, hovering and by the smoke test.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="point">(Required) Position in unit space (core radius 1, +X forward).</param>
    /// <param name="lift">(Optional) Height on the membrane for rim parts, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>True when the part fits there and its ghost shows, false when the ghost is hidden.</returns>
    public bool PreviewPart(PartKind kind, Vector2 point, float lift = 0f)
    {
        return _ghost.Preview(kind, point, lift);
    }

    /// <summary>Raises (or with a negative change flattens) the membrane at one height handle, free of charge.</summary>
    /// <param name="index">(Required) Handle, 0 forward, counting like CellDesign.Heights.</param>
    /// <param name="change">(Required) Change of the height factor at the handle, neighbors get EdgeWeight of it.</param>
    /// <example><code>editor.RaiseHeight(4, 0.5f);   // a hump at the back</code></example>
    public void RaiseHeight(int index, float change)
    {
        _sculptor.RaiseHeight(index, change);
    }

    /// <summary>Recolors the draft, committed with Done like the parts.</summary>
    /// <param name="color">(Required) The new color, null for the color of the stage.</param>
    public void PickColor(Color? color)
    {
        Draft.SetColor(color);
        ShowColor();
        _preview.Punch(1.02f);
    }

    /// <summary>Commits the draft and the points left to the player, the game resumes while the panel slides out.</summary>
    public void Finish()
    {
        _session.Commit();
        _isOpen = false;
        _panel.SlideTo(0f);
        EmitSignal(SignalName.Closed);
    }

    // ===========================================================================
    // =============================   ROUTE INPUT   =============================
    // ===========================================================================
    //
    //   OnPreviewInput   The wheel sizes the picked part under it, else zooms, right click
    //                    removes, then the gizmo (PartPoser) and
    //                    the height handles (MembraneSculptor) get the event. What is left:
    //                    a left drag rotates (on the edge: stretches), a left click places
    //                    the selected part or picks a placed one, hovering with a selected
    //                    part shows where it would go.
    //   ClickCell        Left click on the cell: places the selected part (where its dot
    //                    shows) or picks a placed one.
    //   OnPartDropped    Places the dragged part where it was released.
    //

    private const float DRAG_THRESHOLD = 5f;             // pixels before a click becomes a rotation
    private const float PLACE_DRAG_THRESHOLD = 14f;      // with a part in hand a shaky click still places
    private const float SIZE_STEP = 0.1f;                // part size per wheel notch

    private void OnPreviewInput(InputEvent @event)
    {
        switch (@event)
        {
            // Wheel over the picked part (empty hands): size, anywhere else: zoom
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                bool isUp = MouseButton.WheelUp == wheel.ButtonIndex;
                if (null == Selected && PickedPart >= 0 && PickedPart == _preview.PartAtPixel(wheel.Position))
                {
                    SetPartSize(Draft.Parts[PickedPart].Size + (isUp ? SIZE_STEP : -SIZE_STEP));
                    return;
                }
                _preview.Zoom(isUp);
                return;

            // Right click: remove the part under the cursor, else the closest one
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right:
                if (!RemovePart(_preview.PartAtPixel(right.Position)))
                {
                    RemoveAt(_preview.ToUnit(right.Position));
                }
                return;
        }

        // Left drag on the gizmo: shift or turn the picked part
        if (_poser.Handle(@event))
        {
            return;
        }

        // Left drag on a height handle with empty hands: raise or flatten the membrane there
        if (_sculptor.HandleHeight(@event))
        {
            return;
        }

        switch (@event)
        {
            // Left press: maybe a click, maybe the start of a rotation
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.Pressed:
                _preview.BeginPress(left.Position);
                // Pressed on the membrane edge without a part in hand: stretch instead of rotate
                if (_sculptor.TryBeginShaping(left.Position))
                {
                    _ghost.HideGhost();
                }
                break;

            // Left release: a click without rotation places the selected part, without one it picks a placed part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_preview.IsPressed && !_preview.IsOrbiting && !_sculptor.IsShaping)
                {
                    ClickCell(left.Position);
                }
                _preview.EndPress();
                _sculptor.EndShaping();
                break;

            // Edge drag: stretch or squash
            case InputEventMouseMotion shaping when _preview.IsPressed && _sculptor.IsShaping:
                _sculptor.ShapeTowards(shaping.Position);
                break;

            // Left drag: rotate once past the threshold
            case InputEventMouseMotion motion when _preview.IsPressed:
                if (_preview.TrackOrbit(motion.Position, null == Selected ? DRAG_THRESHOLD : PLACE_DRAG_THRESHOLD))
                {
                    _ghost.HideGhost();
                    _ghost.HideDot();
                    _preview.Orbit(motion.Relative);
                }
                break;

            // Hover with a selected part: a dot on the membrane right under the cursor
            case InputEventMouseMotion hover when null != Selected:
                _ghost.ShowDot(Selected.Value, hover.Position);
                break;

            // Hover over a height handle or the edge with empty hands: the cursor offers to raise or stretch
            case InputEventMouseMotion edgeHover:
                _ghost.HideDot();
                _preview.SetCursor(_sculptor.HoverAt(edgeHover.Position));
                break;
        }
    }

    private void ClickCell(Vector2 pixel)
    {
        if (null == Selected)
        {
            PickPart(_preview.PartAtPixel(pixel));
            return;
        }

        // The dot shows where the part goes: the hit on the membrane, a miss places nothing.
        // A placed part deselects the tile (gizmo gets the focus), a failed one keeps the dot
        PartKind kind     = Selected.Value;
        bool     isPlaced = _preview.TryHitSurface(pixel, out Vector2 spot, out float lift, out _)
            && TryPlace(kind, spot, lift);
        if (isPlaced)
        {
            _ghost.HideDot();
        }
        else
        {
            _ghost.ShowDot(kind, pixel);
        }
    }

    private void OnPartDropped(Vector2 pixel, PartKind kind)
    {
        _ghost.HideGhost();
        _ghost.HideDot();
        if (!_preview.TryHitSurface(pixel, out Vector2 spot, out float lift, out _) || !TryPlace(kind, spot, lift))
        {
            Log.Verbose("CellEditor", $"{kind} dropped where it does not fit -> not placed.");
        }
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh        Updates title, points, diet preview, info card and tiles.
    //   UpdateInfo     Name and description of the dragged, hovered or selected part, empty otherwise.
    //   UpdateSize     Size slider of the picked part, hidden without one or with a part in hand.
    //   ShowColor     Paints preview, ghost and the selected swatch in the draft color.
    //   _Process       Turns the dish, updates the ghost, the gizmo and the height handles, in this order.
    //   _Notification  Re-translates after a locale change, cleans up after a drag.
    //

    /// <summary>Updates every widget, also when new points arrive while the editor is open.</summary>
    public void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }
        Player player = _session.Player;
        _panel.Refresh(Points, player.Diet, player.PreviewDiet(Draft));
        _palette.UpdateHeaders();
        UpdateInfo();
        UpdateSize();
        _palette.UpdateTiles();
    }

    private void UpdateInfo()
    {
        PartInfo info = _palette.Focused ?? (null == Selected ? null : PartCatalog.Get(Selected.Value));
        _panel.ShowInfo(info, null == info || _session.IsUnlocked(info));
    }

    private void UpdateSize()
    {
        // Like the gizmo: a palette part in hand hides the slider
        bool isPicked = null == Selected && PickedPart >= 0 && PickedPart < Draft.Parts.Count;
        _panel.ShowSize(isPicked ? Draft.Parts[PickedPart].Size : null);
    }

    private void ShowColor()
    {
        Color stageColor = _session.Player.StageColor;
        Color color      = Draft.Color ?? stageColor;
        _preview.ShowColor(color, stageColor, Draft.Color);
        _ghost.SetColor(color);
    }

    public override void _Process(double delta)
    {
        _preview.TurnDish((float)delta);
        _ghost.UpdateHover((float)delta, null != Selected || null != _palette.Dragged);
        _poser.UpdateGizmo();
        _sculptor.UpdateHandles((float)delta, null != _ghost.Kind);
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what)
        {
            Refresh();
        }

        // Drag over (dropped or cancelled): ghost and drag info go, see PartPalette (DRAG TILE)
        if (NotificationDragEnd == what && null != _palette?.Dragged)
        {
            // Godot does not always deliver the drop over the SubViewportContainer, the ghost knows the spot
            if (null != _ghost.Kind && !GetViewport().GuiIsDragSuccessful())
            {
                TryPlace(_ghost.Kind.Value, _ghost.Point, _ghost.Lift);
            }
            _palette.EndDrag();
            _ghost.HideGhost();
            UpdateInfo();
        }
    }

    // ===========================================================================
    // ===========================   WIRE COMPONENTS   ===========================
    // ===========================================================================
    //
    //   _Ready   Keeps running while paused, builds everything hidden and closed:
    //            the two views inside the panel, then the components in the 3D world
    //            of the preview, each with the session and its events connected.
    //

    private CellEditorPanel _panel;
    private PartPalette _palette;
    private CellPreview _preview;
    private PartGhost _ghost;
    private PartPoser _poser;
    private MembraneSculptor _sculptor;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 20;

        // Views: palette left, preview right, framed by the panel (it builds them when it enters the tree)
        _palette = new PartPalette();
        _palette.Setup(_session);
        _palette.SelectRequested += Select;
        _palette.FocusChanged    += UpdateInfo;

        _preview = new CellPreview();
        _preview.Setup(_session);
        _preview.PreviewInput += OnPreviewInput;
        _preview.PartDropped  += OnPartDropped;
        _preview.ColorPicked  += PickColor;

        _panel = new CellEditorPanel();
        _panel.Setup(_palette, _preview);
        _panel.DonePressed     += Finish;
        _panel.PartSizeChanged += size => SetPartSize(size);
        _panel.SlidOut         += _preview.ClearCell;
        _panel.AccentChanged   += _preview.ApplyAccent;
        AddChild(_panel);

        // Components in the 3D world of the preview: ghost and hover dot, gizmo, height handles
        _ghost = new PartGhost();
        _ghost.Setup(_session, _preview);
        _ghost.DragIconToggled += _palette.SetDragIconVisible;
        _preview.PartHovered   += _ghost.PreviewAtPixel;
        _preview.AddToWorld(_ghost);

        _poser = new PartPoser();
        _poser.Setup(_session, _preview);
        _preview.AddToWorld(_poser);

        _sculptor = new MembraneSculptor();
        _sculptor.Setup(_session, _preview);
        _preview.AddToWorld(_sculptor);
    }
}
