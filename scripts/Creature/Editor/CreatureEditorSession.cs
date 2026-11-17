// NOTE:
//   Shared state of one sitting in the creature editor: the draft design, the points
//   left, the phase (type choice or sculpting), the modes (symmetry, spray can) and the
//   selection (palette part, tab, picked part or vertebra). The CreatureEditor creates
//   it once and hands it to every component and view: they read it, every change goes
//   through the methods here. No node, so it needs no scene tree.
//
//   Four events tell the editor what to do after a change:
//   > PaletteChanged    The offered parts or their color changed, the tiles are rebuilt.
//   > Changed           Every widget has to follow (CreatureEditor.Refresh).
//   > SelectionChanged  Only the palette selection changed: info card and tiles follow.
//   > EndPulled         The body grew or shrank at the nose or tail end.
//   Changes of the looks only (pose, size, girth, lift) raise none: the stats follow
//   Draft.Version, the editor updates the one widget that shows them.
//
//   var session = new CreatureEditorSession();
//   session.Changed += Refresh;
//   session.Begin(player.Creature, isChoosingType: false, () => player.EvolutionPoints);
//   session.TryPlace(CreaturePartKind.Eye, 0.8f, 0, out int cost);

//
//   Chapters:
//     STATE             Events, draft, points, phase, modes and the selection.
//     CHOOSE AND PAINT  Body type, palette tab and part, colors, pattern, symmetry, spray can.
//     PLACE PARTS       Prices, placing on body or limb, removing with refund.
//     POSE PARTS        Picks a placed part, shifts, turns and sizes it.
//     PUSH VERTEBRAE    Picks a vertebra, pushes and scales it, grows or shrinks the spine.
//

using System.Linq;
using Godot;

namespace CellOut;

/// <summary>What the creature editor shows: parts and shape, paint, motion, or the clay of an own part.</summary>
public enum CreatureEditorMode { Build, Paint, Animate, Sculpt }

public sealed class CreatureEditorSession
{
    // ===========================================================================
    // ================================   STATE   ================================
    // ===========================================================================
    //
    //   PaletteChanged  Raised before Changed when the tiles have to be rebuilt.
    //   Changed         Raised after a change every widget has to show.
    //   SelectionChanged  Raised when a part is picked from the palette or let go.
    //   EndPulled       Raised before Changed when an end grew or shrank (isNose, isGrowing).
    //   Draft           The design being edited, a copy until Done.
    //   Points          Evolution points left: the budget minus what was spent.
    //   IsChoosingType  The first shaping still waits for fish or marine mammal.
    //   IsSymmetric     New side parts come as mirrored pairs.
    //   IsSpraying      The spray can is on.
    //   Selected        The palette part a click on the body places.
    //   Category        The shown palette tab.
    //   PickedPart      The placed part carrying the gizmo, -1 for none.
    //   GizmoFlank      Flank of the picked pair the gizmo sits on, see GizmoFlankFor.
    //   PickedVertebra  The vertebra carrying the gizmo, -1 for none.
    //   DraftColor      Color of the draft, the coral default when none is picked.
    //   SprayColor      Paint color of the spray can, its own choice (SetSprayColor), apart from the pattern color.
    //   Begin           Starts a sitting on a copy of a design.
    //

    public event System.Action PaletteChanged;
    public event System.Action Changed;
    public event System.Action SelectionChanged;
    public event System.Action<bool, bool> EndPulled;

    private System.Func<int> _budget;                // points of the player plus the bonus of the first shaping
    private int _spent;

    public CreatureDesign Draft { get; private set; } = CreatureDesign.Blob();
    public bool IsChoosingType { get; private set; }
    public bool IsSymmetric { get; private set; } = true;

    /// <summary>
    /// Stretches the picked vertebra along one axis (gizmo cubes): X the length of the body
    /// there, Y the height of its ring, Z the width of its ring. Free of charge, looks only.
    /// </summary>
    /// <param name="axis">(Required) Vector3.Right, Vector3.Up or Vector3.Back.</param>
    /// <param name="factor">(Required) Above 1 bigger, below 1 smaller, clamped by the spine.</param>
    /// <returns>False when no vertebra is picked.</returns>
    /// <example><code>session.StretchPickedVertebra(Vector3.Up, 0.8f);   // flatter</code></example>
    public bool StretchPickedVertebra(Vector3 axis, float factor)
    {
        if (PickedVertebra < 0)
        {
            return false;
        }
        int index = PickedVertebra;
        if (Vector3.Right == axis)
        {
            Sculpted.StretchVertebra(index, factor);
        }
        else if (Vector3.Up == axis)
        {
            Sculpted.SetHeight(index, Sculpted.Heights[index] * factor);
        }
        else
        {
            Sculpted.SetWidth(index, Sculpted.Widths[index] * factor);
        }
        return true;
    }

    /// <summary>Point symmetry of new parts: they stand this many times around the spine (2..CreatureDesign.MAX_RADIAL), 0 while the axis symmetry or none is on.</summary>
    public int RadialCount { get; private set; }
    public bool IsSpraying { get; private set; }

    /// <summary>The mode of the editor: build (parts and shape), paint (colors, skin, pattern, spray can) or animate (how body and parts move).</summary>
    public CreatureEditorMode Mode { get; private set; }

    public bool IsPainting => CreatureEditorMode.Paint == Mode;

    /// <summary>Whether the clay on the turntable is sculpted by its handles: in the build mode (the body) and in the sculpt mode (an own part).</summary>
    public bool HasHandles => CreatureEditorMode.Build == Mode || CreatureEditorMode.Sculpt == Mode;

    /// <summary>Sculpt mode: the id of the own shape on the turntable, 0 for none.</summary>
    public int PickedShape { get; private set; }

    /// <summary>The clay the vertebra handles work on: the own shape in the sculpt mode, else the body of the draft.</summary>
    public CreatureDesign Sculpted => CreatureEditorMode.Sculpt == Mode && Draft.ShapeOf(PickedShape) is CustomShape shape ? shape.Form : Draft;

    /// <summary>Puts an own shape on the turntable of the sculpt mode. Raises Changed.</summary>
    public void PickShape(int id)
    {
        PickedShape    = null != Draft.ShapeOf(id) ? id : 0;
        PickedVertebra = -1;
        Changed?.Invoke();
    }

    /// <summary>Adds a fresh own shape and puts it on the turntable. Raises Changed.</summary>
    public void AddShape()
    {
        PickShape(Draft.AddShape().Id);
    }

    /// <summary>Deletes the own shape on the turntable with every part made of it, the next one takes its place. Raises PaletteChanged and Changed.</summary>
    public void RemovePickedShape()
    {
        if (!Draft.RemoveShape(PickedShape))
        {
            return;
        }
        PickedPart = -1;
        PaletteChanged?.Invoke();
        PickShape(Draft.Shapes.Count > 0 ? Draft.Shapes[^1].Id : 0);
    }

    /// <summary>Puts a part made of the own shape on the animal (a pair on the flanks, free of charge), picks it and returns to the build mode.</summary>
    /// <returns>False without a shape or when no spot of the spine took it.</returns>
    public bool PlacePickedShape()
    {
        foreach (float t in new[] { 0.5f, 0.6f, 0.4f, 0.7f, 0.3f, 0.8f, 0.2f })
        {
            if (Draft.PlaceShape(PickedShape, t))
            {
                SetMode(CreatureEditorMode.Build);
                PickPart(Draft.Parts.Count - 1, 1);
                return true;
            }
        }
        return false;
    }

    /// <summary>Animation mode: the pace the animal moves at on the turntable, 0 standing .. 1 full speed.</summary>
    public float PreviewSpeed { get; private set; } = 0.6f;

    /// <summary>Animation mode: true shows the walk on land, false the swimming in the water.</summary>
    public bool PreviewsLand { get; private set; }

    /// <summary>Sets pace and place of the preview of the animation mode. Raises Changed.</summary>
    public void SetPreview(float speed, bool isOnLand)
    {
        PreviewSpeed = Mathf.Clamp(speed, 0f, 1f);
        PreviewsLand = isOnLand;
        Changed?.Invoke();
    }

    /// <summary>Sets how the picked part moves on its own (PartMotion). Raises Changed.</summary>
    /// <returns>False when no part is picked.</returns>
    public bool SetPickedMotion(PartMotion motion)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        Draft.SetPartMotion(PickedPart, motion);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Sets the strength of one channel of the model animation of the picked part (CreatureDesign.SetPartChannel). Raises Changed.</summary>
    /// <returns>False when no part is picked.</returns>
    public bool SetPickedChannel(string channel, float amount)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        Draft.SetPartChannel(PickedPart, channel, amount);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Switches the own motion of every part on or off at once, their pace and swing stay. Raises Changed.</summary>
    public void SetAllMotion(bool isOn)
    {
        for (int i = 0; i < Draft.Parts.Count; i++)
        {
            Draft.SetPartMotion(i, Draft.Parts[i].Movement with { IsOn = isOn });
        }
        Changed?.Invoke();
    }

    /// <summary>Sets the motion of the whole body: swim wave, its pace, the breathing mouth. Raises Changed.</summary>
    public void SetBodyMotion(float waveAmount, float waveSpeed, bool isBreathing)
    {
        Draft.SetBodyMotion(waveAmount, waveSpeed, isBreathing);
        Changed?.Invoke();
    }

    private readonly CreatureDraftHistory _history = new();
    private int _shapeStamp;                         // sum of the versions of the own shapes, see Checkpoint

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    /// <summary>Switches the mode. Leaving the paint mode puts the spray can down, leaving the build mode lets go of the palette selection and the picked vertebra. Raises Changed.</summary>
    public void SetMode(CreatureEditorMode mode)
    {
        Mode       = mode;
        IsSpraying = IsSpraying && IsPainting;
        PickedVertebra = -1;                         // the handles belong to another clay now
        if (CreatureEditorMode.Build != mode)
        {
            Selected = null;
        }
        if (CreatureEditorMode.Sculpt == mode)
        {
            PickedPart = -1;
            if (null == Draft.ShapeOf(PickedShape))
            {
                PickedShape = Draft.Shapes.Count > 0 ? Draft.Shapes[^1].Id : Draft.AddShape().Id;
            }
        }
        SelectionChanged?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Stores the draft as an undo step when it changed, called while the draft is at rest (no drag running).</summary>
    public void Checkpoint()
    {
        // A shape is a design of its own: its changes count on the draft, so visual and history follow
        int shapes = Draft.Shapes.Sum(shape => shape.Form.Version);
        if (shapes != _shapeStamp)
        {
            _shapeStamp = shapes;
            Draft.Touch();
        }
        if (!IsChoosingType && _history.Checkpoint(Draft, _spent))
        {
            Changed?.Invoke();                       // the undo button wakes up
        }
    }

    /// <summary>Takes back the last change of the draft, points included. Raises PaletteChanged and Changed.</summary>
    /// <returns>False when there is nothing to take back.</returns>
    public bool Undo()
    {
        return Restore(_history.Undo(Draft, ref _spent));
    }

    /// <summary>Repeats the change taken back last. Raises PaletteChanged and Changed.</summary>
    /// <returns>False when there is nothing to repeat.</returns>
    public bool Redo()
    {
        return Restore(_history.Redo(Draft, ref _spent));
    }

    /// <summary>After a step of the history: indices may have moved, so nothing stays picked.</summary>
    private bool Restore(bool isRestored)
    {
        if (!isRestored)
        {
            return false;
        }
        PickedPart     = -1;
        PickedVertebra = -1;
        GizmoFlank     = 0;
        PaletteChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }
    public CreaturePartKind? Selected { get; private set; }
    public CreatureCategory Category { get; private set; } = CreatureCategory.Fins;
    public int PickedPart { get; private set; } = -1;
    public int GizmoFlank { get; private set; }
    public int PickedVertebra { get; private set; } = -1;
    public int Points => null == _budget ? 0 : _budget() - _spent;
    public Color DraftColor => Draft.Color ?? Player.ORGANISM_COLOR;

    /// <summary>Paint color of the spray can: the second color, else a dark shade of the body.</summary>
    public Color SprayColor => _sprayColor ?? DraftColor.Darkened(0.45f);

    /// <summary>The color picked for the spray can, null while it sprays a dark shade of the body. Its own choice, apart from the pattern color.</summary>
    public Color? PickedSprayColor => _sprayColor;

    private Color? _sprayColor;

    /// <summary>Picks the color of the spray can, null for a dark shade of the body. Raises Changed.</summary>
    public void SetSprayColor(Color? color)
    {
        _sprayColor = color;
        Changed?.Invoke();
    }

    /// <summary>
    /// Starts a sitting: nothing spent, nothing selected, the first tab, the spray can off.
    /// The symmetry switch stays as it was. Raises no event, the editor rebuilds and
    /// refreshes once its stage shows the new draft.
    /// </summary>
    /// <param name="source">(Required) The design the draft is copied from, the clay blob or the creature of the player.</param>
    /// <param name="isChoosingType">(Required) True for the first shaping: the template cards come first.</param>
    /// <param name="budget">(Required) Points available in total, read on every access so points earned meanwhile count.</param>
    /// <example><code>session.Begin(CreatureDesign.Blob(), isChoosingType: true, () => player.EvolutionPoints + bonus);</code></example>
    public void Begin(CreatureDesign source, bool isChoosingType, System.Func<int> budget)
    {
        _budget        = budget;
        _spent         = 0;
        PickedVertebra = -1;
        IsSpraying     = false;
        Selected       = null;
        PickedPart     = -1;
        Category       = CreatureCategory.Fins;
        IsChoosingType = isChoosingType;
        Mode           = CreatureEditorMode.Build;
        PickedShape    = 0;
        _shapeStamp    = source.Shapes.Sum(shape => shape.Form.Version);
        Draft          = new CreatureDesign();
        Draft.CopyFrom(source);
        _history.Reset(Draft, _spent);
    }

    // ===========================================================================
    // ==========================   CHOOSE AND PAINT   ===========================
    // ===========================================================================
    //
    //   ChooseType       The plain starter body of a type, a shortcut for its template.
    //   ChooseTemplate   The clay becomes a template of the library, its parts are paid.
    //   ShowCategory     Switches the palette tab.
    //   Select           Picks a part from the palette.
    //   SetColor         Recolors the draft (null = coral default).
    //   SetPosture       Posture of the whole body in degrees.
    //   SetHover         Hover height above the ground on land.
    //   SetZoneColor     Color of a zone: belly, head, limbs or tail end (null = none).
    //   SetSkin          Surface of the skin: smooth, scales or warts.
    //   SetPickedPartColor   A color of its own for the picked part.
    //   CyclePickedLimbLook  The next tube look for the picked limb.
    //   SetPatternScale, SetPatternWraps   Fineness of the pattern, all around the body.
    //   SetMouthLine, SetMouthPlace        The face: with or without a mouth, its height and length.
    //   ShapePickedVertebra  Keel, round or boxy for the upper and lower half of a ring.
    //   SetPattern       Paints a pattern in the second color.
    //   SetPatternColor  Second color of the paint (null = a dark shade of the body).
    //   SetSymmetric     Axis symmetry: new side parts as pairs or as single halves.
    //   SetRadial        Point symmetry: new parts stand in a ring around the spine.
    //   SetSpraying      Spray can on or off, on it lets go of part and palette selection.
    //   UndoSpray        Takes back the last spray stroke.
    //   ClearSpray       Wipes all spray paint.
    //

    /// <summary>The plain starter body of a type, a shortcut for its template (CreatureTemplates.Plain).</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <returns>False when the choice is made already.</returns>
    public bool ChooseType(CreatureType type)
    {
        return ChooseTemplate(CreatureTemplates.Plain(type));
    }

    /*
      The first shaping: the clay becomes a template of the library.
      > The choice is made already: refused
      > Its parts cost more points than are left: refused, the cards stay
      > Otherwise: the draft becomes the template, its parts are paid like parts placed
        by hand (removing one refunds it). A template without a color of its own keeps
        the color the clay had
    */
    /// <summary>Turns the clay of the first shaping into a template of the library and pays its parts.</summary>
    /// <param name="template">(Required) A template of CreatureTemplates.All.</param>
    /// <returns>False when the choice is made already or the points do not reach.</returns>
    /// <example><code>session.ChooseTemplate(CreatureTemplates.All[4]);   // the shark, 4 EP</code></example>
    public bool ChooseTemplate(CreatureTemplate template)
    {
        if (!IsChoosingType)
        {
            return false;
        }
        CreatureDesign body = template.Build();
        int            cost = CreatureTemplates.CostOf(body);
        if (cost > Points)
        {
            Log.Verbose("CreatureEditorSession", $"Template {template.Key} costs {cost} EP, only {Points} left.");
            return false;
        }
        Color? color = Draft.Color;
        Draft.CopyFrom(body);
        if (null == body.Color)
        {
            Draft.SetColor(color);
        }
        _spent        += cost;
        IsChoosingType = false;
        _history.Reset(Draft, _spent);               // the choice of the template is not taken back
        PaletteChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Switches the palette to the parts of one category, the selection stays.</summary>
    /// <param name="category">(Required) The tab to show.</param>
    /// <returns>False when the tab shows already.</returns>
    public bool ShowCategory(CreatureCategory category)
    {
        if (category == Category)
        {
            return false;
        }
        Category = category;
        PaletteChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Picks a part, null clears the selection. Raises SelectionChanged: only info card and tiles show it.</summary>
    public void Select(CreaturePartKind? kind)
    {
        Selected = kind;
        SelectionChanged?.Invoke();
    }

    /// <summary>Recolors the draft, the tiles rebuild in the new color.</summary>
    /// <param name="color">(Required) The new color, null for the coral default, alpha below 1 for translucent.</param>
    public void SetColor(Color? color)
    {
        Draft.SetColor(color);
        PaletteChanged?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Sets the posture of the draft in degrees (0 level, 90 upright). Raises Changed.</summary>
    public void SetPosture(float degrees)
    {
        Draft.SetPitch(Mathf.DegToRad(degrees));
        Changed?.Invoke();
    }

    /// <summary>Sets how high the draft floats above the ground on land, in body sizes. Raises Changed.</summary>
    public void SetHover(float hover)
    {
        Draft.SetHover(hover);
        Changed?.Invoke();
    }

    /// <summary>Picks the color of a zone of the draft (belly, head, limbs, tail end), null takes it away. Raises Changed.</summary>
    public void SetZoneColor(CreatureZone zone, Color? color)
    {
        Draft.SetZoneColor(zone, color);
        Changed?.Invoke();
    }

    /// <summary>Gives the picked part a color of its own, null takes it away. Raises Changed.</summary>
    /// <returns>False when no part is picked.</returns>
    public bool SetPickedPartColor(Color? color)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        Draft.SetPartColor(PickedPart, color);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Paints the picked limb in a second color from one joint outwards (CreatureDesign.SetLimbTip), null takes it away. Raises Changed.</summary>
    /// <returns>False when the picked part has no joints.</returns>
    public bool SetPickedTipColor(Color? color, int from)
    {
        if (PickedPart < 0 || !Draft.SetLimbTip(PickedPart, color, from))
        {
            return false;
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>The look of the tube of the picked limb, foot or hand becomes the next limb kind (plain, tentacle, feeler ..). Free of charge. Raises Changed.</summary>
    /// <returns>False when the picked part has no joints.</returns>
    public bool CyclePickedLimbLook()
    {
        if (PickedPart < 0 || null == Draft.Parts[PickedPart].Joints)
        {
            return false;
        }
        var looks = System.Enum.GetValues<CreaturePartKind>().Where(kind => CreatureCatalog.Get(kind).IsSculpted).ToList();
        int next  = (looks.IndexOf(Draft.Parts[PickedPart].LimbStyle) + 1) % looks.Count;
        Draft.SetLimbKind(PickedPart, looks[next]);
        Changed?.Invoke();
        return true;
    }

    /// <summary>How fine the paint pattern is (CreatureLook.SetPatternScale). Raises Changed.</summary>
    public void SetPatternScale(float scale)
    {
        Draft.SetPatternScale(scale);
        Changed?.Invoke();
    }

    /// <summary>Lets the pattern run all around the body instead of only over the back. Raises Changed.</summary>
    public void SetPatternWraps(bool isWrapping)
    {
        Draft.SetPatternWraps(isWrapping);
        Changed?.Invoke();
    }

    /// <summary>Shows or hides the mouth line and opening of the face. Raises Changed.</summary>
    public void SetMouthLine(bool hasMouthLine)
    {
        Draft.SetMouthLine(hasMouthLine);
        Changed?.Invoke();
    }

    /// <summary>Moves and sizes the mouth opening on the head (CreatureDesign.SetMouthPlace). Raises Changed.</summary>
    public void SetMouthPlace(float level, float length)
    {
        Draft.SetMouthPlace(level, length);
        Changed?.Invoke();
    }

    /// <summary>Shapes the ring of the picked vertebra: the upper half (crown) and the lower half (belly), -1 a keel .. 1 boxy. Raises Changed.</summary>
    /// <returns>False when no vertebra is picked.</returns>
    public bool ShapePickedVertebra(float crown, float belly)
    {
        if (PickedVertebra < 0)
        {
            return false;
        }
        Sculpted.SetCrown(PickedVertebra, crown);
        Sculpted.SetBelly(PickedVertebra, belly);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Picks the surface of the skin of the draft. Raises Changed.</summary>
    public void SetSkin(CreatureSkin skin)
    {
        Draft.SetSkin(skin);
        Changed?.Invoke();
    }

    /// <summary>Paints the draft with a pattern in its second color, CreaturePattern.None for a plain body.</summary>
    public void SetPattern(CreaturePattern pattern)
    {
        Draft.SetPattern(pattern, Draft.PatternColor);
        Changed?.Invoke();
    }

    /// <summary>Second color of the paint, null for a dark shade of the body color.</summary>
    public void SetPatternColor(Color? color)
    {
        Draft.SetPattern(Draft.Pattern, color);
        Changed?.Invoke();
    }

    /// <summary>Switches the symmetry: on, side parts are placed as mirrored pairs, off as single halves. Placed parts keep theirs.</summary>
    /// <param name="isSymmetric">(Required) True for pairs.</param>
    public void SetSymmetric(bool isSymmetric)
    {
        IsSymmetric = isSymmetric;
        RadialCount = 0;                             // the two symmetries exclude each other
        Changed?.Invoke();
    }

    /// <summary>Switches the point symmetry: new parts stand count times around the spine. It turns the axis symmetry off. Placed parts keep theirs.</summary>
    /// <param name="count">(Required) 2..CreatureDesign.MAX_RADIAL, anything below 2 turns it off.</param>
    public void SetRadial(int count)
    {
        RadialCount = count < 2 ? 0 : Mathf.Min(count, CreatureDesign.MAX_RADIAL);
        IsSymmetric = RadialCount < 2 && IsSymmetric;
        Changed?.Invoke();
    }

    /// <summary>Switches the spray can. On, the picked part and the palette selection let go: the body is painted now.</summary>
    /// <param name="isSpraying">(Required) True to paint.</param>
    public void SetSpraying(bool isSpraying)
    {
        IsSpraying = isSpraying;
        if (isSpraying)
        {
            PickedPart = -1;
            GizmoFlank = 0;
            Selected   = null;
        }
        Changed?.Invoke();
    }

    /// <summary>Takes back the last spray stroke.</summary>
    /// <returns>False when there was no stroke.</returns>
    public bool UndoSpray()
    {
        bool isUndone = Draft.UndoStroke();
        Changed?.Invoke();
        return isUndone;
    }

    /// <summary>Wipes all spray paint.</summary>
    public void ClearSpray()
    {
        Draft.ClearPaint();
        Changed?.Invoke();
    }

    // ===========================================================================
    // =============================   PLACE PARTS   =============================
    // ===========================================================================
    //
    //   IsOpen       Whether a part may be built yet, locked parts are refused.
    //   CostOf       Points a part costs right now (a single half costs half).
    //   SideAt       The side a part dropped at a point takes.
    //   TryPlace     Places a part at a spine share if affordable and free.
    //   AttachEnd    Sticks a foot or hand on the tip of a placed sculpted limb.
    //   PlaceOnLimb  Grows a limb (or a foot or hand with its limb) from a joint of a placed limb.
    //   RemovePart   Removes one placed part with its branches and refunds them.
    //   PointOf      Where a part sits on the body.
    //
    //   The placing calls only pay and place. They raise no event: the CreaturePartPlacer
    //   picks the new part right after, and that pick raises Changed.
    //

    /// <summary>Whether a part may be built yet (Creature/Design/PartUnlocks.cs), set by the editor; every part by default.</summary>
    public System.Func<CreaturePartKind, bool> IsOpen { get; set; } = _ => true;

    /// <summary>Points a part costs with the current symmetry: a single half of a pair costs half, rounded up.</summary>
    public int CostOf(CreaturePartInfo info)
    {
        bool isRing = RadialCount >= 2 && CreatureDesign.CanBeRadial(info.Kind);
        return info.CostFor(IsSymmetric ? 0 : 1) * (isRing ? RadialCount : 1);
    }

    /// <summary>The side a part dropped at a point takes: 0 with symmetry, else the flank of the point.</summary>
    public int SideAt(Vector3 hit)
    {
        return IsSymmetric ? 0 : (hit.Z < 0f ? -1 : 1);
    }

    /// <summary>Places a part if the body type may use it, it is affordable and its spot is free.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Share of the spine, 0 tail .. 1 nose.</param>
    /// <param name="side">(Required) 0 a pair, -1 left or 1 right for a single half of a paired part.</param>
    /// <param name="cost">(Required) Receives the price asked, also when the part was not placed.</param>
    /// <returns>True when the part was placed and paid, it is the last of Draft.Parts then.</returns>
    /// <example><code>session.TryPlace(CreaturePartKind.Hands, 0.6f, 1, out int cost)   // one right hand, half price</code></example>
    public bool TryPlace(CreaturePartKind kind, float t, int side, out int cost)
    {
        cost = 0;
        if (!IsOpen(kind))
        {
            return false;
        }
        // Point symmetry: a ring of single halves around the spine, each at its price
        bool isRing = RadialCount >= 2 && CreatureDesign.CanBeRadial(kind);
        side = isRing && CreatureCatalog.Get(kind).IsPaired ? 1 : side;
        cost = CreatureCatalog.Get(kind).CostFor(CreatureDesign.SideFor(kind, side)) * (isRing ? RadialCount : 1);
        if (IsChoosingType || cost > Points || !Draft.TryPlace(kind, t, cost, side))
        {
            return false;
        }
        if (isRing)
        {
            Draft.SetPartRadial(Draft.Parts.Count - 1, RadialCount);
        }
        _spent += cost;
        return true;
    }

    /// <summary>
    /// Sticks a foot or hand on the tip of a placed bare sculpted limb. It costs the full
    /// price of the end minus what the limb cost, at least 1 point.
    /// </summary>
    /// <param name="limbIndex">(Required) Index in Draft.Parts, a sculpted limb without an end.</param>
    /// <param name="end">(Required) A foot or hand kind.</param>
    /// <param name="cost">(Required) Receives the price asked, -1 when the part is no bare limb or the end no foot or hand.</param>
    /// <returns>False when the part is no bare limb, the end is no foot or hand or the points do not reach.</returns>
    public bool AttachEnd(int limbIndex, CreaturePartKind end, out int cost)
    {
        cost = -1;
        if (limbIndex < 0 || limbIndex >= Draft.Parts.Count || !Draft.Parts[limbIndex].Info.IsSculpted || !CreatureCatalog.Get(end).IsEnd)
        {
            return false;
        }
        CreaturePart limb = Draft.Parts[limbIndex];
        cost = Mathf.Max(1, CreatureCatalog.Get(end).CostFor(limb.Side) - limb.PaidCost);
        if (cost > Points || !Draft.AttachEnd(limbIndex, end, cost))
        {
            return false;
        }
        _spent += cost;
        return true;
    }

    /// <summary>Grows a limb (or a foot or hand with its limb) from a joint of a placed limb, at the price of the part.</summary>
    /// <param name="kind">(Required) A sculpted limb, a foot or a hand.</param>
    /// <param name="parentIndex">(Required) Index in Draft.Parts of a limb with joints.</param>
    /// <param name="joint">(Required) Joint of the parent, 0 = first .. tip.</param>
    /// <param name="cost">(Required) Receives the price asked, -1 when there is no such parent.</param>
    /// <returns>False when the kind grows no limb, the parent has no such joint or the points do not reach.</returns>
    public bool PlaceOnLimb(CreaturePartKind kind, int parentIndex, int joint, out int cost)
    {
        cost = -1;
        if (parentIndex < 0 || parentIndex >= Draft.Parts.Count || !IsOpen(kind))
        {
            return false;
        }
        cost = CreatureCatalog.Get(kind).CostFor(Draft.Parts[parentIndex].Side);
        if (IsChoosingType || cost > Points || !Draft.TryPlaceOnLimb(kind, parentIndex, joint, cost))
        {
            return false;
        }
        _spent += cost;
        return true;
    }

    /// <summary>Removes one placed part and refunds what was paid for it. Raises Changed.</summary>
    /// <param name="index">(Required) Index in Draft.Parts.</param>
    /// <param name="removed">(Required) Receives the removed part.</param>
    /// <returns>False for an index outside Draft.Parts.</returns>
    public bool RemovePart(int index, out CreaturePart removed)
    {
        removed = default;
        if (index < 0 || index >= Draft.Parts.Count)
        {
            return false;
        }
        int refund = Draft.BranchCost(index);        // the part and every branch growing from it
        removed    = Draft.RemovePart(index);
        _spent    -= refund;
        PickedPart = -1;                             // indices moved
        Changed?.Invoke();
        return true;
    }

    /// <summary>Where a part sits on the body, unit space, for the ring burst.</summary>
    public Vector3 PointOf(CreaturePart part)
    {
        float x = CreatureMount.Tail == part.Info.Mount ? Draft.TailTipX : Draft.XAt(part.T);
        float y = CreatureMount.Top == part.Info.Mount ? Draft.TopAt(x) : 0f;
        return new Vector3(x, y, 0f);
    }

    // ===========================================================================
    // =============================   POSE PARTS   ==============================
    // ===========================================================================
    //
    //   PickPart        Picks a placed part for gizmo and size slider, -1 for none.
    //   GizmoFlankFor   The flank of a part its gizmo sits on.
    //   MovePickedPart  Shifts the picked part (gizmo arrows), free of charge, looks only.
    //   TurnPickedPart  Turns the picked part (gizmo rings), free of charge, looks only.
    //   SetPartSize     Size of the picked part, free of charge, looks only.
    //   StretchPickedPart  Stretches the picked part along one axis (gizmo cubes), free of charge, looks only.
    //

    /// <summary>Picks a placed part for gizmo and size slider. Raises Changed.</summary>
    /// <param name="index">(Required) Index in Draft.Parts, -1 clears the pick.</param>
    /// <param name="flank">(Required) Flank of a pair the gizmo sits on, -1 or 1. The caller passes the flank facing the camera when none is wanted.</param>
    public void PickPart(int index, int flank)
    {
        PickedPart = index >= 0 && index < Draft.Parts.Count ? index : -1;
        if (PickedPart >= 0)
        {
            PickedVertebra = -1;                     // one gizmo: a part or a vertebra
        }
        GizmoFlank = PickedPart < 0 ? 0 : GizmoFlankFor(Draft.Parts[PickedPart], flank);
        Changed?.Invoke();
    }

    /*
      The gizmo of a pair sits on one half, the other half mirrors what it does:
      > Midline part: flank 0, nothing is mirrored.
      > Single half: its own flank.
      > Pair: the wanted flank.
    */
    private static int GizmoFlankFor(CreaturePart part, int flank)
    {
        if (!part.Info.IsPaired)
        {
            return 0;
        }
        if (0 != part.Side)
        {
            return part.Side;
        }
        return flank;
    }

    /// <summary>Shifts the picked part, its mirror half follows mirrored. Clamped to CreatureDesign.MAX_PART_OFFSET.</summary>
    /// <param name="delta">(Required) Shift in unit space as seen on the flank of the gizmo.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>session.MovePickedPart(new Vector3(0f, 0.1f, 0f));</code></example>
    public bool MovePickedPart(Vector3 delta)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        CreaturePart part   = Draft.Parts[PickedPart];
        Vector3      stored = GizmoFlank < 0 ? new Vector3(delta.X, delta.Y, -delta.Z) : delta;
        Draft.SetPartPose(PickedPart, part.Offset + stored, part.Rotation);
        return true;
    }

    /// <summary>
    /// Turns the picked part around a world axis. The pose stores the right half, so a
    /// turn on the left flank is mirrored first: axis Z flipped and the angle negated.
    /// </summary>
    /// <param name="axis">(Required) Normalized world axis.</param>
    /// <param name="angle">(Required) Radians, right hand rule.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>session.TurnPickedPart(Vector3.Up, 0.5f);</code></example>
    public bool TurnPickedPart(Vector3 axis, float angle)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        CreaturePart part  = Draft.Parts[PickedPart];
        Basis        delta = GizmoFlank < 0 ? new Basis(new Vector3(axis.X, axis.Y, -axis.Z), -angle) : new Basis(axis, angle);
        Basis        turned = delta * Basis.FromEuler(part.Rotation);
        Draft.SetPartPose(PickedPart, part.Offset, turned.Orthonormalized().GetEuler());
        return true;
    }

    /// <summary>Grows or shrinks the picked part, clamped by the design. Only the look changes.</summary>
    /// <param name="size">(Required) 1 = normal, CreatureDesign.MIN_PART_SIZE..MAX_PART_SIZE.</param>
    /// <returns>False when no part is picked.</returns>
    public bool SetPartSize(float size)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        Draft.SetPartSize(PickedPart, size);
        return true;
    }

    /// <summary>
    /// Stretches the picked part along one body axis, clamped by the design. Both halves of a
    /// pair follow. The stretch is stored along the own axes of the part (before its turn), so
    /// the body axis is turned back into the part first: a part turned by a quarter grows along
    /// the axis that points that way now, a part standing askew shares the growth between its axes.
    /// </summary>
    /// <param name="axis">(Required) Normalized: Vector3.Right (along the body), Vector3.Up or Vector3.Back (out of the flank).</param>
    /// <param name="factor">(Required) Above 1 longer, below 1 shorter, multiplied onto the stretch of that axis.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>session.StretchPickedPart(Vector3.Right, 1.2f);</code></example>
    public bool StretchPickedPart(Vector3 axis, float factor)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        CreaturePart part  = Draft.Parts[PickedPart];
        Vector3      right = GizmoFlank < 0 ? new Vector3(axis.X, axis.Y, -axis.Z) : axis;       // the pose stores the right half
        Vector3      own   = Basis.FromEuler(part.Rotation).Inverse() * right;
        Vector3      grown = Vector3.One + own * own * (factor - 1f);                           // squares: the shares sum to 1
        Draft.SetPartStretch(PickedPart, part.Proportion * grown);
        return true;
    }

    // ===========================================================================
    // ===========================   PUSH VERTEBRAE   ============================
    // ===========================================================================
    //
    //   PickVertebra         Picks a vertebra for the gizmo, -1 for none.
    //   ForgetPull           A new drag starts, the gathered end overshoot is dropped.
    //   MovePickedVertebra   Pushes the picked vertebra along the body (X) and lifts it (Y), ends grow or shrink.
    //   ScalePickedVertebra  Scales the girth of the picked vertebra.
    //   StretchPickedVertebra  Stretches the picked vertebra per axis: length of the body there, height and width of its ring.
    //   PullEnd              One vertebra more or less at the nose or tail end, free of charge.
    //   AddVertebra          One more vertebra in the middle, free of charge.
    //   RemoveVertebra       One vertebra less, free of charge.
    //

    /// <summary>Picks a vertebra for the gizmo, a picked part lets go. Raises Changed when one is picked.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end, -1 for none.</param>
    public void PickVertebra(int index)
    {
        PickedVertebra = index >= 0 && index < Sculpted.Girths.Count ? index : -1;
        if (PickedVertebra >= 0)
        {
            PickedPart = -1;
            Changed?.Invoke();
        }
    }

    /*
      Pushing the picked vertebra:
      > Y lifts it (the neighbors follow smoothly, see CreatureDesign.SetLift).
      > X moves it along the body, clamped between its neighbors.
      > An end vertebra pushed past its clamp gathers the overshoot: outwards past
        PULL_STEP a new vertebra grows there and takes the gizmo, inwards one goes.
    */
    private const float PULL_STEP = CreatureDesign.SPACING * 0.6f;
    private float _pull;                             // end overshoot gathered since the last vertebra grew or went

    /// <summary>Drops the gathered end overshoot, called when a new drag starts.</summary>
    public void ForgetPull()
    {
        _pull = 0f;
    }

    /// <summary>Pushes the picked vertebra, free of charge.</summary>
    /// <param name="delta">(Required) Shift in unit space, X along the body, Y up, Z ignored (the body stays symmetric).</param>
    /// <returns>False when no vertebra is picked.</returns>
    /// <example><code>session.PickVertebra(0); session.MovePickedVertebra(new Vector3(-0.5f, 0f, 0f));   // a longer tail</code></example>
    public bool MovePickedVertebra(Vector3 delta)
    {
        if (PickedVertebra < 0)
        {
            return false;
        }
        int index = PickedVertebra;
        if (!Mathf.IsZeroApprox(delta.Y))
        {
            Sculpted.SetLift(index, Sculpted.Lifts[index] + delta.Y);
        }
        if (!Mathf.IsZeroApprox(delta.Z))
        {
            Sculpted.SetSway(index, Sculpted.Sways[index] + delta.Z);
        }
        if (Mathf.IsZeroApprox(delta.X))
        {
            return true;
        }
        float wanted = Sculpted.Positions[index] + delta.X;
        float taken  = Sculpted.SetVertebraX(index, wanted);

        // Ends: the overshoot past the clamp grows or shrinks the body
        bool isNose = Sculpted.Girths.Count - 1 == index;
        bool isTail = 0 == index;
        if (!isNose && !isTail)
        {
            return true;
        }
        _pull += (wanted - taken) * (isNose ? 1f : -1f);
        if (_pull > PULL_STEP && PullEnd(isNose, isGrowing: true))
        {
            _pull = 0f;
        }
        else if (_pull < -PULL_STEP && PullEnd(isNose, isGrowing: false))
        {
            _pull = 0f;
        }
        PickedVertebra = isNose ? Sculpted.Girths.Count - 1 : 0;
        return true;
    }

    /// <summary>Scales the girth of the picked vertebra, free of charge.</summary>
    /// <param name="factor">(Required) Above 1 thicker, below 1 thinner; the girth is clamped by the design.</param>
    /// <returns>False when no vertebra is picked.</returns>
    public bool ScalePickedVertebra(float factor)
    {
        if (PickedVertebra < 0)
        {
            return false;
        }
        Sculpted.SetGirth(PickedVertebra, Sculpted.Girths[PickedVertebra] * factor);
        return true;
    }

    /// <summary>Grows (true) or shrinks (false) the body at the nose or tail end by one vertebra. Raises EndPulled and Changed.</summary>
    /// <param name="isNose">(Required) True for the front end, false for the tail.</param>
    /// <param name="isGrowing">(Required) True adds a vertebra, false removes one.</param>
    /// <returns>False at the vertebra limits.</returns>
    /// <example><code>session.PullEnd(isNose: true, isGrowing: true);   // the neck gets longer</code></example>
    public bool PullEnd(bool isNose, bool isGrowing)
    {
        bool isChanged = isGrowing ? Sculpted.GrowEnd(isNose) : Sculpted.ShrinkEnd(isNose);
        if (!isChanged)
        {
            return false;
        }
        EndPulled?.Invoke(isNose, isGrowing);
        Changed?.Invoke();
        return true;
    }

    /// <summary>One more vertebra in the middle. Raises Changed when one was added.</summary>
    /// <returns>False at CreatureDesign.MAX_VERTEBRAE.</returns>
    public bool AddVertebra()
    {
        bool isAdded = Sculpted.AddVertebra();
        if (isAdded)
        {
            Changed?.Invoke();
        }
        return isAdded;
    }

    /// <summary>One vertebra less. Raises Changed when one was removed.</summary>
    /// <returns>False at CreatureDesign.MIN_VERTEBRAE.</returns>
    public bool RemoveVertebra()
    {
        bool isRemoved = Sculpted.RemoveVertebra();
        if (isRemoved)
        {
            Changed?.Invoke();
        }
        return isRemoved;
    }
}
