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

using Godot;

namespace CellOut;

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
    //   SprayColor      Paint color of the spray can.
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
    public bool IsSpraying { get; private set; }
    public CreaturePartKind? Selected { get; private set; }
    public CreatureCategory Category { get; private set; } = CreatureCategory.Fins;
    public int PickedPart { get; private set; } = -1;
    public int GizmoFlank { get; private set; }
    public int PickedVertebra { get; private set; } = -1;
    public int Points => null == _budget ? 0 : _budget() - _spent;
    public Color DraftColor => Draft.Color ?? Player.ORGANISM_COLOR;

    /// <summary>Paint color of the spray can: the second color, else a dark shade of the body.</summary>
    public Color SprayColor => Draft.PatternColor ?? DraftColor.Darkened(0.45f);

    /// <summary>
    /// Starts a sitting: nothing spent, nothing selected, the first tab, the spray can off.
    /// The symmetry switch stays as it was. Raises no event, the editor rebuilds and
    /// refreshes once its stage shows the new draft.
    /// </summary>
    /// <param name="source">(Required) The design the draft is copied from, the clay blob or the creature of the player.</param>
    /// <param name="isChoosingType">(Required) True for the first shaping: the type cards come first.</param>
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
        Draft          = new CreatureDesign();
        Draft.CopyFrom(source);
    }

    // ===========================================================================
    // ==========================   CHOOSE AND PAINT   ===========================
    // ===========================================================================
    //
    //   ChooseType       Fish or marine mammal, the clay takes its starter shape.
    //   ShowCategory     Switches the palette tab.
    //   Select           Picks a part from the palette.
    //   SetColor         Recolors the draft (null = coral default).
    //   SetPattern       Paints a pattern in the second color.
    //   SetPatternColor  Second color of the paint (null = a dark shade of the body).
    //   SetSymmetric     Symmetry switch: new side parts as pairs or as single halves.
    //   SetSpraying      Spray can on or off, on it lets go of part and palette selection.
    //   UndoSpray        Takes back the last spray stroke.
    //   ClearSpray       Wipes all spray paint.
    //

    /// <summary>Sets the body type of the first shaping, the clay becomes its starter body (CreatureDesign.Starter).</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <returns>False when the type is chosen already.</returns>
    public bool ChooseType(CreatureType type)
    {
        if (!IsChoosingType)
        {
            return false;
        }
        // The clay becomes the streamlined starter body of the type, the picked color stays
        Color? color = Draft.Color;
        Draft.CopyFrom(CreatureDesign.Starter(type));
        Draft.SetColor(color);
        IsChoosingType = false;
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

    /// <summary>Points a part costs with the current symmetry: a single half of a pair costs half, rounded up.</summary>
    public int CostOf(CreaturePartInfo info)
    {
        return info.CostFor(IsSymmetric ? 0 : 1);
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
        cost = CreatureCatalog.Get(kind).CostFor(CreatureDesign.SideFor(kind, side));
        if (IsChoosingType || cost > Points || !Draft.TryPlace(kind, t, cost, side))
        {
            return false;
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
        if (parentIndex < 0 || parentIndex >= Draft.Parts.Count)
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

    // ===========================================================================
    // ===========================   PUSH VERTEBRAE   ============================
    // ===========================================================================
    //
    //   PickVertebra         Picks a vertebra for the gizmo, -1 for none.
    //   ForgetPull           A new drag starts, the gathered end overshoot is dropped.
    //   MovePickedVertebra   Pushes the picked vertebra along the body (X) and lifts it (Y), ends grow or shrink.
    //   ScalePickedVertebra  Scales the girth of the picked vertebra.
    //   PullEnd              One vertebra more or less at the nose or tail end, free of charge.
    //   AddVertebra          One more vertebra in the middle, free of charge.
    //   RemoveVertebra       One vertebra less, free of charge.
    //

    /// <summary>Picks a vertebra for the gizmo, a picked part lets go. Raises Changed when one is picked.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end, -1 for none.</param>
    public void PickVertebra(int index)
    {
        PickedVertebra = index >= 0 && index < Draft.Girths.Count ? index : -1;
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
            Draft.SetLift(index, Draft.Lifts[index] + delta.Y);
        }
        if (Mathf.IsZeroApprox(delta.X))
        {
            return true;
        }
        float wanted = Draft.Positions[index] + delta.X;
        float taken  = Draft.SetVertebraX(index, wanted);

        // Ends: the overshoot past the clamp grows or shrinks the body
        bool isNose = Draft.Girths.Count - 1 == index;
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
        PickedVertebra = isNose ? Draft.Girths.Count - 1 : 0;
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
        Draft.SetGirth(PickedVertebra, Draft.Girths[PickedVertebra] * factor);
        return true;
    }

    /// <summary>Grows (true) or shrinks (false) the body at the nose or tail end by one vertebra. Raises EndPulled and Changed.</summary>
    /// <param name="isNose">(Required) True for the front end, false for the tail.</param>
    /// <param name="isGrowing">(Required) True adds a vertebra, false removes one.</param>
    /// <returns>False at the vertebra limits.</returns>
    /// <example><code>session.PullEnd(isNose: true, isGrowing: true);   // the neck gets longer</code></example>
    public bool PullEnd(bool isNose, bool isGrowing)
    {
        bool isChanged = isGrowing ? Draft.GrowEnd(isNose) : Draft.ShrinkEnd(isNose);
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
        bool isAdded = Draft.AddVertebra();
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
        bool isRemoved = Draft.RemoveVertebra();
        if (isRemoved)
        {
            Changed?.Invoke();
        }
        return isRemoved;
    }
}
