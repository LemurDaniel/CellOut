// NOTE:
//   Creature editor of the organism stage, opened by Main after the evolve cutscene
//   (first shaping) and later with E. Full screen, so there is room for every part
//   category: the panel slides in from the left and covers the paused game.
//   First shaping: two cards ask what the clay becomes, fish or marine mammal (the
//   type stays for good), the clay turns into its streamlined starter body. It sits on a
//   sculptor's turntable: a click on the glowing handle above a vertebra picks it and
//   puts the gizmo on it: the red arrow pushes it along the body (the bigger end handles
//   pull a new vertebra out or push one in), the green arrow lifts it (a neck, a hump, a
//   hanging tail), the golden ring scales its girth; the wheel over a handle sets the
//   girth too, + and - add or remove vertebrae in the middle. A sculpted limb (Limbs tab) shows
//   joint handles once picked, dragging them pulls arms, legs or tentacles out. Feet and
//   hands are the ends of limbs: dropped on a limb they stick on its tip (the limb counts
//   towards the price), dropped on the body they bring a fitting limb along. A limb (or a
//   foot or hand on a limb that has its end) dropped on a limb grows from its nearest joint
//   as a branch; removing a limb takes its branches along.
//   Paint: a pattern in a second color, and the spray can for own patterns: with it on,
//   left drag over the body sprays the second color (mirrored with symmetry), the wheel
//   sets the nozzle, undo takes back the last stroke, wipe clears all spray paint. Tabs on top pick a category (fins,
//   weapons, eyes, feet, hands, brain, lungs, wings), its parts are dragged from the
//   tiles onto the body (or a tile is selected and the body clicked). Tail, nose and
//   skull take one part each. Side parts come in mirrored pairs, with the symmetry
//   switch off as a single half on the flank they were dropped on (half the points,
//   half the effect). Left drag elsewhere turns the view, the wheel zooms, right click
//   on a part removes and refunds it. A dropped or clicked part gets a gizmo: arrows
//   shift it, rings turn it, its mirror half follows; a slider below sets its size,
//   the wheel over it does the same (all looks only). A column on the right shows the
//   traits of the draft live (CreatureStats): weight, what the legs carry, speed in
//   the water and on land, and whether the wings could lift it. "Done" hands the body
//   plan to the player.
//
//   This class is the coordinator: it keeps the public API, creates the session
//   (CreatureEditorSession), the frame, the stage components (Stage/) and the views
//   (Views/), wires their events, routes the input of the stage and refreshes the views.
//   The rules live in the session, placing and removing parts in the CreaturePartPlacer.
//
//   editor.Open(player, isFirstShaping: true);   // later: Closed signal

//
//   Chapters:
//     STATE AND API   Signal, session and the calls Main and the smoke test use.
//     ROUTE INPUT     Hands the input of the stage to its components in a fixed order.
//     REFRESH         Refreshes every view, follows the session and runs the stage every frame.
//     BUILD EDITOR    Creates frame, stage components and views and wires their events.
//

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
    //   ShowCategory    Switches the palette tab.
    //   DraftColor      Color of the draft, the coral default when none is picked.
    //   Select          Picks a part from the palette.
    //   PickColor       Recolors the draft (null = coral default), free of charge.
    //   PickPattern     Paints a pattern in the second color, free of charge.
    //   PickPatternColor  Second color of the paint (null = a dark shade of the body), free of charge.
    //   SetSymmetric    Symmetry switch: new side parts as pairs or as single halves.
    //   SetSpraying     Spray can on or off: left drag over the body paints instead of turning the view.
    //   SprayAt         One spray dot (and its mirror with symmetry) where the body was hit.
    //   UndoSpray       Takes back the last spray stroke.
    //   ClearSpray      Wipes all spray paint.
    //   CostOf          Points a part costs right now (a single half costs half).
    //   TryPlace        Places a part at a spine share if affordable and free.
    //   PlaceAt         Places a part at a point of the clay, the flank from the point.
    //   AttachEnd       Sticks a foot or hand on the tip of a placed sculpted limb.
    //   PlaceOnLimb     Grows a limb (or a foot or hand with its limb) from a joint of a placed limb.
    //   RemoveAt        Removes the closest part and refunds it.
    //   RemovePart      Removes one placed part and refunds it.
    //   PickPart        Picks a placed part for gizmo and size slider, -1 for none.
    //   MovePickedPart  Shifts the picked part (gizmo arrows), free of charge, looks only.
    //   TurnPickedPart  Turns the picked part (gizmo rings), free of charge, looks only.
    //   SetPartSize     Size of the picked part, free of charge, looks only.
    //   SetGirth        Thickness of one vertebra, free of charge.
    //   SetLift         Height of one vertebra (bends the spine), free of charge.
    //   PickVertebra    Picks a vertebra for the gizmo, -1 for none.
    //   MovePickedVertebra   Pushes the picked vertebra along the body (X) and lifts it (Y), ends grow or shrink.
    //   ScalePickedVertebra  Scales the girth of the picked vertebra.
    //   PullEnd         One vertebra more or less at the nose or tail end, free of charge.
    //   AddVertebra     One more vertebra in the middle, free of charge.
    //   RemoveVertebra  One vertebra less, free of charge.
    //   PreviewPart     Shows the ghost of a dragged part where it would land.
    //   Finish          Hands the draft to the player and slides out.
    //
    //   State and rules live in the session, placing parts in the CreaturePartPlacer. The
    //   calls here hand over and add the verbose log and the ring burst of a change.
    //

    [Signal] public delegate void ClosedEventHandler();

    private readonly CreatureEditorSession _session = new();
    private Player _player;
    private bool _isFirstShaping;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool IsChoosingType => _session.IsChoosingType;
    public CreatureDesign Draft => _session.Draft;
    public CreaturePartKind? Selected => _session.Selected;
    public CreatureCategory Category => _session.Category;
    public int PickedPart => _session.PickedPart;
    public bool IsSymmetric => _session.IsSymmetric;
    public int Points => _session.Points;
    public Color DraftColor => _session.DraftColor;

    /// <summary>Shows the editor for the player, the game must already be paused.</summary>
    /// <param name="player">(Required) The player whose body plan and points are edited.</param>
    /// <param name="isFirstShaping">(Required) True right after the evolve cutscene: type choice, clay blob, bonus points.</param>
    /// <example><code>editor.Open(player, isFirstShaping: true);</code></example>
    public void Open(Player player, bool isFirstShaping)
    {
        int bonus       = isFirstShaping ? Balance.Current.Player.OrganismBonusEp : 0;
        _player         = player;
        _isFirstShaping = isFirstShaping;
        _isOpen         = true;
        _palette.ClearHover();
        _handles.CancelGizmoDrag();
        _spray.Reset();
        _session.Begin(isFirstShaping ? CreatureDesign.Blob() : player.Creature, isFirstShaping, () => player.EvolutionPoints + bonus);

        _stage.ShowDraft();
        _stage.ResetCamera();

        _frame.Visible = true;
        _footer.ForgetPoints();
        Log.Verbose("CreatureEditor", $"Opened (first shaping: {isFirstShaping}) with {Points} EP.");
        _cards.Rebuild();
        _palette.Rebuild();
        Refresh();
        _frame.SlideTo(1f);
    }

    /// <summary>Sets the body type of the first shaping, the clay becomes its starter body (CreatureDesign.Starter).</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    public void ChooseType(CreatureType type)
    {
        if (!_session.ChooseType(type))
        {
            return;
        }
        Log.Verbose("CreatureEditor", $"Type chosen: {type}.");
        _stage.Celebrate(Vector3.Zero, CreatureEditorUi.TypeAccent(type), 1.12f);
    }

    /// <summary>Switches the palette to the parts of one category, the selection stays.</summary>
    /// <param name="category">(Required) The tab to show.</param>
    /// <example><code>editor.ShowCategory(CreatureCategory.Feet);</code></example>
    public void ShowCategory(CreatureCategory category)
    {
        if (category == Category)
        {
            return;
        }
        _palette.ClearHover();
        Log.Verbose("CreatureEditor", $"Tab {category}.");
        _session.ShowCategory(category);
    }

    /// <summary>Picks a part, null clears the selection.</summary>
    public void Select(CreaturePartKind? kind) => _session.Select(kind);

    /// <summary>Recolors the draft, committed with Done like the parts. Tiles rebuild in the new color.</summary>
    /// <param name="color">(Required) The new color, null for the coral default, alpha below 1 for translucent.</param>
    /// <example><code>editor.PickColor(EditorStyle.TRANSLUCENT_COLOR);</code></example>
    public void PickColor(Color? color)
    {
        Log.Verbose("CreatureEditor", $"Color {(null == color ? "default" : color.Value.ToHtml())}.");
        _session.SetColor(color);
        _stage.Recolor();
        _toolbar.PunchColors();
    }

    /// <summary>Paints the draft with a pattern in its second color, CreaturePattern.None for a plain body.</summary>
    /// <example><code>editor.PickPattern(CreaturePattern.Stripes);</code></example>
    public void PickPattern(CreaturePattern pattern)
    {
        Log.Verbose("CreatureEditor", $"Pattern {pattern}.");
        _session.SetPattern(pattern);
        _toolbar.PunchPatterns();
    }

    // Spray can: on/off, nozzle size and the paint color
    public bool IsSpraying => _session.IsSpraying;
    public float SprayRadius => _spray.Radius;

    /// <summary>Paint color of the spray can: the second color, else a dark shade of the body.</summary>
    public Color SprayColor => _session.SprayColor;

    /// <summary>Switches the spray can: on, left drag over the body paints, the wheel sets the nozzle.</summary>
    /// <param name="isSpraying">(Required) True to paint.</param>
    public void SetSpraying(bool isSpraying)
    {
        _spray.Reset();
        Log.Verbose("CreatureEditor", $"Spray can {(isSpraying ? "on" : "off")}.");
        _session.SetSpraying(isSpraying);
    }

    /// <summary>Sprays one dot of the spray color, mirrored on the other flank while symmetry is on. Free of charge.</summary>
    /// <inheritdoc cref="CreatureSprayCan.SprayAt"/>
    public bool SprayAt(Vector3 hit, int stroke = 0) => _spray.SprayAt(hit, stroke);

    /// <summary>Takes back the last spray stroke.</summary>
    public bool UndoSpray() => _session.UndoSpray();

    /// <summary>Wipes all spray paint.</summary>
    public void ClearSpray() => _session.ClearSpray();

    /// <summary>Second color of the paint, null for a dark shade of the body color.</summary>
    public void PickPatternColor(Color? color) => _session.SetPatternColor(color);

    /// <summary>Switches the symmetry: on, side parts are placed as mirrored pairs, off as single halves. Placed parts keep theirs.</summary>
    /// <param name="isSymmetric">(Required) True for pairs.</param>
    /// <example><code>editor.SetSymmetric(false);   // the next eye goes on one flank only</code></example>
    public void SetSymmetric(bool isSymmetric)
    {
        Log.Verbose("CreatureEditor", $"Symmetry {(isSymmetric ? "on" : "off")}.");
        _session.SetSymmetric(isSymmetric);
    }

    /// <summary>Points a part costs with the current symmetry: a single half of a pair costs half, rounded up.</summary>
    public int CostOf(CreaturePartInfo info) => _session.CostOf(info);

    /// <summary>Places a part if the body type may use it, it is affordable and its spot is free.</summary>
    /// <inheritdoc cref="CreaturePartPlacer.TryPlace"/>
    /// <example><code>editor.TryPlace(CreaturePartKind.Hands, 0.6f, side: 1)   // one right hand, half price</code></example>
    public bool TryPlace(CreaturePartKind kind, float t, int side = 0) => _placer.TryPlace(kind, t, side);

    /// <summary>Places a part where a point of the clay was hit; with symmetry off a side part goes to that flank only.</summary>
    /// <inheritdoc cref="CreaturePartPlacer.PlaceAt"/>
    public bool PlaceAt(CreaturePartKind kind, Vector3 hit) => _placer.PlaceAt(kind, hit);

    /// <summary>Sticks a foot or hand on the tip of a placed bare sculpted limb, at the price of the end minus the limb.</summary>
    /// <inheritdoc cref="CreaturePartPlacer.AttachEnd"/>
    /// <example><code>editor.AttachEnd(limb, CreaturePartKind.Claws);</code></example>
    public bool AttachEnd(int limbIndex, CreaturePartKind end) => _placer.AttachEnd(limbIndex, end);

    /// <summary>Grows a limb (or a foot or hand with its limb) from a joint of a placed limb, at the price of the part.</summary>
    /// <inheritdoc cref="CreaturePartPlacer.PlaceOnLimb"/>
    /// <example><code>editor.PlaceOnLimb(CreaturePartKind.Tentacle, arm, 1);</code></example>
    public bool PlaceOnLimb(CreaturePartKind kind, int parentIndex, int joint) => _placer.PlaceOnLimb(kind, parentIndex, joint);

    /// <summary>Removes the part closest to a spine share and refunds what was paid for it.</summary>
    /// <returns>True when a part was removed.</returns>
    public bool RemoveAt(float t) => _placer.RemoveAt(t);

    /// <summary>Removes one placed part and refunds what was paid for it.</summary>
    /// <inheritdoc cref="CreaturePartPlacer.RemovePart"/>
    public bool RemovePart(int index) => _placer.RemovePart(index);

    /// <summary>Picks a placed part for gizmo and size slider.</summary>
    /// <inheritdoc cref="CreaturePartPlacer.PickPart"/>
    public void PickPart(int index, int flank = 0) => _placer.PickPart(index, flank);

    /// <summary>Shifts the picked part, its mirror half follows mirrored. Clamped to CreatureDesign.MAX_PART_OFFSET.</summary>
    /// <inheritdoc cref="CreatureEditorSession.MovePickedPart"/>
    /// <example><code>editor.PickPart(0); editor.MovePickedPart(new Vector3(0f, 0.1f, 0f));</code></example>
    public bool MovePickedPart(Vector3 delta) => _session.MovePickedPart(delta);

    /// <summary>Turns the picked part around a world axis, its mirror half follows mirrored.</summary>
    /// <inheritdoc cref="CreatureEditorSession.TurnPickedPart"/>
    /// <example><code>editor.TurnPickedPart(Vector3.Up, 0.5f);</code></example>
    public bool TurnPickedPart(Vector3 axis, float angle) => _session.TurnPickedPart(axis, angle);

    /// <summary>Grows or shrinks the picked part, clamped by the design. Only the look changes.</summary>
    /// <param name="size">(Required) 1 = normal, CreatureDesign.MIN_PART_SIZE..MAX_PART_SIZE.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.PickPart(0); editor.SetPartSize(1.3f);</code></example>
    public bool SetPartSize(float size)
    {
        if (!_session.SetPartSize(size))
        {
            return false;
        }
        _footer.RefreshSize();
        return true;
    }

    /// <summary>Sets the girth of one vertebra, the handles follow every frame.</summary>
    public void SetGirth(int index, float girth) => Draft.SetGirth(index, girth);

    /// <summary>Lifts one vertebra (bends the spine), the neighbors follow smoothly. Free of charge.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="lift">(Required) Height above the straight line, clamped by the design.</param>
    /// <example><code>editor.SetLift(editor.Draft.Girths.Count - 1, 0.6f);   // a neck</code></example>
    public void SetLift(int index, float lift) => Draft.SetLift(index, lift);

    /// <summary>The vertebra carrying the gizmo, -1 for none.</summary>
    public int PickedVertebra => _session.PickedVertebra;

    /// <summary>Picks a vertebra for the gizmo, a picked part lets go.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end, -1 for none.</param>
    public void PickVertebra(int index) => _session.PickVertebra(index);

    /// <summary>Pushes the picked vertebra, free of charge.</summary>
    /// <inheritdoc cref="CreatureEditorSession.MovePickedVertebra"/>
    /// <example><code>editor.PickVertebra(0); editor.MovePickedVertebra(new Vector3(-0.5f, 0f, 0f));   // a longer tail</code></example>
    public bool MovePickedVertebra(Vector3 delta) => _session.MovePickedVertebra(delta);

    /// <summary>Scales the girth of the picked vertebra, free of charge.</summary>
    /// <inheritdoc cref="CreatureEditorSession.ScalePickedVertebra"/>
    public bool ScalePickedVertebra(float factor) => _session.ScalePickedVertebra(factor);

    /// <summary>Grows (true) or shrinks (false) the body at the nose or tail end by one vertebra. Free of charge.</summary>
    /// <inheritdoc cref="CreatureEditorSession.PullEnd"/>
    /// <example><code>editor.PullEnd(isNose: true, isGrowing: true);   // the neck gets longer</code></example>
    public bool PullEnd(bool isNose, bool isGrowing) => _session.PullEnd(isNose, isGrowing);

    public bool AddVertebra()
    {
        bool isAdded = _session.AddVertebra();
        if (isAdded)
        {
            _stage.Celebrate(Vector3.Zero, CreatureEditorUi.TypeAccent(Draft.Type), 1.05f);
        }
        return isAdded;
    }

    public bool RemoveVertebra()
    {
        bool isRemoved = _session.RemoveVertebra();
        if (isRemoved)
        {
            _stage.Celebrate(Vector3.Zero, CreatureEditorUi.TypeAccent(Draft.Type), 0.95f);
        }
        return isRemoved;
    }

    /// <summary>The part whose ghost shows on the clay, null when none does.</summary>
    public CreaturePartKind? GhostKind => _ghost.GhostKind;

    /// <summary>Shows the ghost of a part where a drop at a point of the clay would place it.</summary>
    /// <inheritdoc cref="CreatureDropGhost.PreviewPart"/>
    public bool PreviewPart(CreaturePartKind kind, Vector3 hit) => _ghost.PreviewPart(kind, hit);

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
        _frame.SlideTo(0f);
        EmitSignal(SignalName.Closed);
    }

    // ===========================================================================
    // =============================   ROUTE INPUT   =============================
    // ===========================================================================
    //
    //   OnStageInput     Left drag on the gizmo shifts or turns the picked part, on a joint handle
    //                    sculpts the picked limb, on a vertebra handle bends the spine (the end handles
    //                    also pull the body longer or push it shorter, Shift sets the girth instead),
    //                    a click places the selected part or picks a placed one, left drag elsewhere
    //                    rotates, right click removes, the wheel over a vertebra handle sets its girth,
    //                    over the picked part its size, otherwise it zooms.
    //   _Notification    Re-translates after a locale change, ends a drag.
    //
    //   Order per event: the spray can first while it is on, then the handles (gizmo, joints,
    //   vertebrae), then the parts on the clay (CreaturePartPlacer), at last the orbit camera
    //   of the stage.
    //

    private const float SIZE_STEP = 0.1f;            // per wheel notch over the picked part

    private void OnStageInput(InputEvent @event)
    {
        if (IsChoosingType)
        {
            return;
        }
        if (IsSpraying && _spray.Handle(@event))
        {
            return;
        }
        switch (@event)
        {
            // Wheel over a vertebra handle: girth, over the picked part: size, anywhere else: zoom
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                float notch = MouseButton.WheelUp == wheel.ButtonIndex ? 1f : -1f;
                if (_handles.ScrollGirth(wheel.Position, notch))
                {
                    break;
                }
                if (PickedPart >= 0 && PickedPart == _stage.PartAt(wheel.Position, out _))
                {
                    SetPartSize(Draft.Parts[PickedPart].Size + notch * SIZE_STEP);
                    break;
                }
                _stage.Zoom(notch);
                break;

            // Right click: remove the part under the cursor
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right:
                _placer.RemovePart(_stage.PartAt(right.Position, out _));
                break;

            // Left press: the gizmo shifts or turns, a joint sculpts the limb, a vertebra handle
            // bends the spine, otherwise maybe a click, maybe a rotation
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.Pressed:
                bool isTaken = _handles.Grab(left.Position);
                _stage.Press(left.Position, isFree: !isTaken);
                break;

            // Left release: a click without rotation places the selected palette part, without one it picks a placed part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_stage.Release())
                {
                    _placer.ClickAt(left.Position);
                }
                _handles.Release();
                break;

            // Gizmo or joint drag: the picked vertebra, part or limb follows
            case InputEventMouseMotion drag when _handles.IsDragging:
                _handles.Drag(drag);
                break;

            // Left drag: rotate once past the threshold
            case InputEventMouseMotion motion when _stage.IsPressed:
                _stage.Orbit(motion);
                break;

            // Hover: gizmo and handles light up, a selected part shows where it would go
            case InputEventMouseMotion hover:
                bool isOverHandle = _handles.Hover(hover.Position);
                _stage.ShowLanding(null != Selected && !isOverHandle ? _stage.RayToBody(hover.Position) : null);
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _frame)
        {
            Refresh();
        }

        // Drag over (dropped or cancelled): ghost and drag info go, see Stage/CreatureDropGhost.cs
        if (NotificationDragEnd == what && null != _ghost?.Dragged)
        {
            // Godot does not always deliver the drop over the SubViewportContainer, the ghost knows the spot
            if (null != _ghost.GhostKind && !GetViewport().GuiIsDragSuccessful())
            {
                PlaceAt(_ghost.GhostKind.Value, _ghost.GhostHit);
            }
            _ghost.EndDrag();
            RefreshInfo();
        }
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh              Phase (cards or sculpting), title, points, buttons, tabs, stats, info card and tiles.
    //   RefreshInfo          Name and description of the dragged, hovered or selected part, else of the body type.
    //   OnSelectionChanged   The palette selection changed: info card and tiles follow.
    //   OnEndPulled          The body grew or shrank at an end: log and ring burst at that end.
    //   _Process             Turns the turntable, updates changed stats, moves handles, marker and gizmo.
    //

    /// <summary>Updates every widget.</summary>
    public void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }
        _frame.Refresh();
        _toolbar.Refresh();
        _cards.Refresh();
        _footer.Refresh();
        _tabs.Refresh();
        _stats.Refresh();
        _footer.RefreshSize();
        RefreshInfo();
        _palette.RefreshTiles();
    }

    /// <summary>Dragged tile first, then the hovered one, the footer falls back to the selection of the session.</summary>
    private void RefreshInfo()
    {
        _footer.RefreshInfo(_ghost.Dragged ?? _palette.Hovered);
    }

    private void OnSelectionChanged()
    {
        RefreshInfo();
        _palette.RefreshTiles();
    }

    private void OnEndPulled(bool isNose, bool isGrowing)
    {
        Log.Verbose("CreatureEditor", $"{(isNose ? "Nose" : "Tail")} {(isGrowing ? "pulled out" : "pushed in")}, {Draft.Girths.Count} vertebrae.");
        _stage.Celebrate(_handles.HandlePosition(isNose ? Draft.Girths.Count - 1 : 0), CreatureEditorUi.TypeAccent(Draft.Type), isGrowing ? 1.04f : 0.97f);
    }

    public override void _Process(double delta)
    {
        if (!_frame.Visible)
        {
            return;
        }
        _stage.Advance((float)delta);

        // Stats follow every change, also the handle drags that skip Refresh
        _stats.Follow();

        // Vertebra handles, joint handles of the picked limb and the gizmo
        _handles.Update(_stage.Clock);

        // Marker: the spray nozzle where the paint lands (as big as the nozzle), else where a selected part would land
        _stage.UpdateMarker(IsSpraying ? _spray.Hover : null, _spray.Radius);

        // Ghost of a dragged part: goes once the part leaves the clay
        _ghost.HideOutside();
    }

    // ===========================================================================
    // ============================   BUILD EDITOR   =============================
    // ===========================================================================
    //
    //   _Ready        Keeps running while paused, builds everything hidden and closed, follows the session.
    //   BuildStage    The turntable stage with drag ghost, sculpt handles, part placer and spray can.
    //   BuildViews    Toolbar over the turntable, tabs, type cards, tile palette, stats column and footer.
    //

    private CreatureEditorFrame _frame;
    private CreatureStage _stage;
    private CreatureDropGhost _ghost;
    private CreatureSculptHandles _handles;
    private CreaturePartPlacer _placer;
    private CreatureSprayCan _spray;
    private CreatureToolbar _toolbar;
    private CreatureCategoryTabs _tabs;
    private CreatureTypeCards _cards;
    private CreaturePartPalette _palette;
    private CreatureStatsColumn _stats;
    private CreatureFooter _footer;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 20;

        // Frame: grey-out and the sliding panel, hidden and closed
        _frame = new CreatureEditorFrame();
        _frame.Setup(_session);
        AddChild(_frame);

        BuildStage();
        BuildViews();

        // Rows of the panel: tabs, type cards (first shaping), the workbench and the footer
        _frame.AddRow(_tabs);
        _frame.AddRow(_cards);
        _frame.AddWorkbench(_palette, _stage, _stats);
        _frame.AddRow(_footer);

        // The session tells when tiles are rebuilt, every widget follows, the selection changed or an end was pulled
        _session.PaletteChanged   += _palette.Rebuild;
        _session.Changed          += Refresh;
        _session.SelectionChanged += OnSelectionChanged;
        _session.EndPulled        += OnEndPulled;
    }

    private void BuildStage()
    {
        _stage = new CreatureStage();
        _stage.Setup(_session);
        _stage.GuiInput += OnStageInput;

        // Next to the clay: the ghost of a dragged part, then handles and gizmo
        _ghost = new CreatureDropGhost();
        _ghost.Setup(_session, _stage);
        _ghost.DragStarted += RefreshInfo;
        _stage.AddToScene(_ghost);

        _handles = new CreatureSculptHandles();
        _handles.Setup(_session, _stage);
        _stage.AddToScene(_handles);

        // Places what is clicked or dropped on the clay
        _placer = new CreaturePartPlacer();
        _placer.Setup(_session, _stage);
        _ghost.Dropped += _placer.DropAt;
        AddChild(_placer);

        _spray = new CreatureSprayCan();
        _spray.Setup(_session, _stage);
        _spray.StrokeEnded += Refresh;
        AddChild(_spray);
    }

    private void BuildViews()
    {
        // Buttons floating over the turntable
        _toolbar = new CreatureToolbar();
        _toolbar.Setup(_session);
        _toolbar.AddVertebraPressed    += () => AddVertebra();
        _toolbar.RemoveVertebraPressed += () => RemoveVertebra();
        _toolbar.SymmetryToggled       += SetSymmetric;
        _toolbar.SprayToggled          += SetSpraying;
        _toolbar.UndoSprayPressed      += () => UndoSpray();
        _toolbar.ClearSprayPressed     += ClearSpray;
        _toolbar.ColorPicked           += PickColor;
        _toolbar.PatternPicked         += PickPattern;
        _toolbar.PatternColorPicked    += PickPatternColor;
        _stage.AddChild(_toolbar);

        _tabs = new CreatureCategoryTabs();
        _tabs.Setup(_session);
        _tabs.CategoryPressed += ShowCategory;

        _cards = new CreatureTypeCards();
        _cards.Setup(_session);
        _cards.TypeChosen += ChooseType;

        _palette = new CreaturePartPalette();
        _palette.Setup(_session, _ghost.StartDrag);
        _palette.PartPicked   += Select;
        _palette.HoverChanged += RefreshInfo;

        _stats = new CreatureStatsColumn();
        _stats.Setup(_session);

        _footer = new CreatureFooter();
        _footer.Setup(_session);
        _footer.PartSizeChanged += size => SetPartSize(size);
        _footer.DonePressed     += Finish;
    }
}
