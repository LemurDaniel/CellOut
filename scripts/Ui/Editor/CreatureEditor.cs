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
//   editor.Open(player, isFirstShaping: true);   // later: Closed signal

//
//   Chapters:
//     STATE AND API   Signal, draft, points and the calls Main and the smoke test use.
//     SLIDE           Slides the panel in and out and fades the grey-out.
//     PREVIEW INPUT   Handles, gizmo, placing, removing, orbit and zoom on the 3D clay.
//     DRAG AND GHOST  A tile dragged over the clay shows its part where it would land.
//     REFRESH       Phase, title, points, tabs, stats, info card and tiles.
//     BUILD UI        Grey-out, panel, type cards, tabs, palette, 3D turntable, stats and footer.
//

using System.Collections.Generic;
using System.Linq;
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
    public CreatureCategory Category { get; private set; } = CreatureCategory.Fins;
    public int PickedPart { get; private set; } = -1;
    public bool IsSymmetric { get; private set; } = true;
    public int Points=> null == _player ? 0 : _player.EvolutionPoints + _bonus - _spent;
    public Color DraftColor => Draft.Color ?? Player.ORGANISM_COLOR;

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
        PickedVertebra  = -1;
        _gizmoDrag      = GizmoHandle.None;
        IsSpraying      = false;
        _lastSpray      = null;
        _sprayHover     = null;
        Selected        = null;
        PickedPart      = -1;
        Category        = CreatureCategory.Fins;
        IsChoosingType  = isFirstShaping;
        Draft           = new CreatureDesign();
        Draft.CopyFrom(isFirstShaping ? CreatureDesign.Blob() : player.Creature);

        _preview.Design    = Draft;
        _preview.BodyColor = DraftColor;
        ResetCamera();

        _root.Visible = true;
        _lastPoints   = -1;
        Log.Verbose("CreatureEditor", $"Opened (first shaping: {isFirstShaping}) with {Points} EP.");
        BuildTypeCards();
        BuildPalette();
        Refresh();
        SlideTo(1f);
    }

    /// <summary>Sets the body type of the first shaping, the clay becomes its starter body (CreatureDesign.Starter).</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    public void ChooseType(CreatureType type)
    {
        if (!IsChoosingType)
        {
            return;
        }
        // The clay becomes the streamlined starter body of the type, the picked color stays
        Color? color = Draft.Color;
        Draft.CopyFrom(CreatureDesign.Starter(type));
        Draft.SetColor(color);
        IsChoosingType = false;
        Log.Verbose("CreatureEditor", $"Type chosen: {type}.");
        BuildPalette();
        Refresh();
        Celebrate(Vector3.Zero, TypeAccent(type), 1.12f);
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
        Category = category;
        _hovered = null;
        Log.Verbose("CreatureEditor", $"Tab {category}.");
        BuildPalette();
        Refresh();
    }

    /// <summary>Picks a part, null clears the selection.</summary>
    public void Select(CreaturePartKind? kind)
    {
        Selected = kind;
        UpdateInfo();
        UpdateTiles();
    }

    /// <summary>Recolors the draft, committed with Done like the parts. Tiles rebuild in the new color.</summary>
    /// <param name="color">(Required) The new color, null for the coral default, alpha below 1 for translucent.</param>
    /// <example><code>editor.PickColor(EditorStyle.TRANSLUCENT_COLOR);</code></example>
    public void PickColor(Color? color)
    {
        Draft.SetColor(color);
        _preview.BodyColor = DraftColor;
        Log.Verbose("CreatureEditor", $"Color {(null == color ? "default" : color.Value.ToHtml())}.");
        BuildPalette();
        Refresh();
        EditorStyle.Punch(_swatchRow, 1.05f);
    }

    /// <summary>Paints the draft with a pattern in its second color, CreaturePattern.None for a plain body.</summary>
    /// <example><code>editor.PickPattern(CreaturePattern.Stripes);</code></example>
    public void PickPattern(CreaturePattern pattern)
    {
        Draft.SetPattern(pattern, Draft.PatternColor);
        Log.Verbose("CreatureEditor", $"Pattern {pattern}.");
        Refresh();
        EditorStyle.Punch(_patternRow, 1.05f);
    }

    // Spray can: on/off, nozzle size and the stroke a drag belongs to
    public bool IsSpraying { get; private set; }
    public float SprayRadius { get; private set; } = SPRAY_RADIUS;
    private const float SPRAY_RADIUS = 0.12f;
    private const float SPRAY_STEP = 0.02f;          // nozzle change per wheel notch
    private const float SPRAY_SPACING = 0.35f;       // share of the nozzle a drag moves before the next dot
    private const float MIRROR_EPSILON = 0.03f;      // dots this close to the midline get no mirror twin
    private int _sprayStroke;
    private Vector3? _lastSpray;
    private Vector3? _sprayHover;

    /// <summary>Paint color of the spray can: the second color, else a dark shade of the body.</summary>
    public Color SprayColor => Draft.PatternColor ?? DraftColor.Darkened(0.45f);

    /// <summary>Switches the spray can: on, left drag over the body paints, the wheel sets the nozzle.</summary>
    /// <param name="isSpraying">(Required) True to paint.</param>
    public void SetSpraying(bool isSpraying)
    {
        IsSpraying = isSpraying;
        _lastSpray  = null;
        _sprayHover = null;
        if (isSpraying)
        {
            PickPart(-1);
            Select(null);
        }
        Log.Verbose("CreatureEditor", $"Spray can {(isSpraying ? "on" : "off")}.");
        Refresh();
    }

    /// <summary>Sprays one dot of the spray color, mirrored on the other flank while symmetry is on. Free of charge.</summary>
    /// <param name="hit">(Required) Point on the body, unit space.</param>
    /// <param name="stroke">(Optional) Stroke id, 0 starts a new stroke. Defaults to 0.</param>
    /// <returns>False when the body holds CreatureDesign.MAX_SPLATS dots already.</returns>
    /// <example><code>editor.SprayAt(new Vector3(0f, 0f, 0.4f));</code></example>
    public bool SprayAt(Vector3 hit, int stroke = 0)
    {
        int  id        = 0 == stroke ? Draft.NewStroke() : stroke;
        bool isSprayed = Draft.AddSplat(hit, SprayRadius, SprayColor, id);
        if (isSprayed && IsSymmetric && Mathf.Abs(hit.Z) > MIRROR_EPSILON)
        {
            Draft.AddSplat(new Vector3(hit.X, hit.Y, -hit.Z), SprayRadius, SprayColor, id);
        }
        return isSprayed;
    }

    /// <summary>Takes back the last spray stroke.</summary>
    public bool UndoSpray()
    {
        bool isUndone = Draft.UndoStroke();
        Refresh();
        return isUndone;
    }

    /// <summary>Wipes all spray paint.</summary>
    public void ClearSpray()
    {
        Draft.ClearPaint();
        Refresh();
    }

    /// <summary>Second color of the paint, null for a dark shade of the body color.</summary>
    public void PickPatternColor(Color? color)
    {
        Draft.SetPattern(Draft.Pattern, color);
        Refresh();
    }

    /// <summary>Switches the symmetry: on, side parts are placed as mirrored pairs, off as single halves. Placed parts keep theirs.</summary>
    /// <param name="isSymmetric">(Required) True for pairs.</param>
    /// <example><code>editor.SetSymmetric(false);   // the next eye goes on one flank only</code></example>
    public void SetSymmetric(bool isSymmetric)
    {
        IsSymmetric = isSymmetric;
        Log.Verbose("CreatureEditor", $"Symmetry {(isSymmetric ? "on" : "off")}.");
        Refresh();
    }

    /// <summary>Points a part costs with the current symmetry: a single half of a pair costs half, rounded up.</summary>
    public int CostOf(CreaturePartInfo info)
    {
        return info.CostFor(IsSymmetric ? 0 : 1);
    }

    /// <summary>Places a part if the body type may use it, it is affordable and its spot is free.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Share of the spine, 0 tail .. 1 nose.</param>
    /// <param name="side">(Optional) 0 a pair, -1 left or 1 right for a single half of a paired part. Defaults to 0.</param>
    /// <returns>True when the part was placed and paid.</returns>
    /// <example><code>editor.TryPlace(CreaturePartKind.Hands, 0.6f, side: 1)   // one right hand, half price</code></example>
    public bool TryPlace(CreaturePartKind kind, float t, int side = 0)
    {
        CreaturePartInfo info = CreatureCatalog.Get(kind);
        int              cost = info.CostFor(CreatureDesign.SideFor(kind, side));
        if (IsChoosingType || cost > Points)
        {
            Log.Verbose("CreatureEditor", $"{kind} costs {cost} EP, {Points} left (choosing: {IsChoosingType}) -> not placed.");
            return false;
        }
        if (!Draft.TryPlace(kind, t, cost, side))
        {
            return false;
        }
        _spent += cost;
        CreaturePart placed = Draft.Parts[^1];
        PickPart(Draft.Parts.Count - 1, placed.Side);   // the new part gets the gizmo
        Select(null);                                   // deselected, so the gizmo has focus until the next tile
        Celebrate(PartPoint(placed), TypeAccent(Draft.Type), 1.08f);
        return true;
    }

    /// <summary>Places a part where a point of the clay was hit; with symmetry off a side part goes to that flank only.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="hit">(Required) Point on or in the clay, unit space.</param>
    /// <returns>True when the part was placed and paid.</returns>
    public bool PlaceAt(CreaturePartKind kind, Vector3 hit)
    {
        return TryPlace(kind, Draft.TAt(hit.X), SideAt(hit));
    }

    /// <summary>The side a part dropped at a point takes: 0 with symmetry, else the flank of the point.</summary>
    private int SideAt(Vector3 hit)
    {
        return IsSymmetric ? 0 : (hit.Z < 0f ? -1 : 1);
    }

    /// <summary>
    /// Sticks a foot or hand on the tip of a placed bare sculpted limb. It costs the full
    /// price of the end minus what the limb cost, at least 1 point.
    /// </summary>
    /// <param name="limbIndex">(Required) Index in Draft.Parts, a sculpted limb without an end.</param>
    /// <param name="end">(Required) A foot or hand kind.</param>
    /// <returns>False when the part is no bare limb, the end is no foot or hand or the points do not reach.</returns>
    /// <example><code>editor.AttachEnd(limb, CreaturePartKind.Claws);</code></example>
    public bool AttachEnd(int limbIndex, CreaturePartKind end)
    {
        if (limbIndex < 0 || limbIndex >= Draft.Parts.Count || !Draft.Parts[limbIndex].Info.IsSculpted || !CreatureCatalog.Get(end).IsEnd)
        {
            return false;
        }
        CreaturePart limb = Draft.Parts[limbIndex];
        int          cost = Mathf.Max(1, CreatureCatalog.Get(end).CostFor(limb.Side) - limb.PaidCost);
        if (cost > Points || !Draft.AttachEnd(limbIndex, end, cost))
        {
            Log.Verbose("CreatureEditor", $"{end} on limb {limbIndex} costs {cost} EP, {Points} left -> not attached.");
            return false;
        }
        _spent += cost;
        PickPart(limbIndex, limb.Side);
        Celebrate(PartPoint(Draft.Parts[limbIndex]), TypeAccent(Draft.Type), 1.06f);
        return true;
    }

    /// <summary>Grows a limb (or a foot or hand with its limb) from a joint of a placed limb, at the price of the part.</summary>
    /// <param name="kind">(Required) A sculpted limb, a foot or a hand.</param>
    /// <param name="parentIndex">(Required) Index in Draft.Parts of a limb with joints.</param>
    /// <param name="joint">(Required) Joint of the parent, 0 = first .. tip.</param>
    /// <returns>False when the kind grows no limb, the parent has no such joint or the points do not reach.</returns>
    /// <example><code>editor.PlaceOnLimb(CreaturePartKind.Tentacle, arm, 1);</code></example>
    public bool PlaceOnLimb(CreaturePartKind kind, int parentIndex, int joint)
    {
        if (parentIndex < 0 || parentIndex >= Draft.Parts.Count)
        {
            return false;
        }
        int side = Draft.Parts[parentIndex].Side;
        int cost = CreatureCatalog.Get(kind).CostFor(side);
        if (IsChoosingType || cost > Points || !Draft.TryPlaceOnLimb(kind, parentIndex, joint, cost))
        {
            Log.Verbose("CreatureEditor", $"{kind} on limb {parentIndex} joint {joint} costs {cost} EP, {Points} left -> not placed.");
            return false;
        }
        _spent += cost;
        PickPart(Draft.Parts.Count - 1, side);
        Celebrate(PartPoint(Draft.Parts[^1]), TypeAccent(Draft.Type), 1.06f);
        return true;
    }

    /*
      A part dropped or clicked onto a placed limb (anything with joints):
      > A foot or hand on a bare sculpted limb sticks on its tip (AttachEnd).
      > A sculpted limb, or a foot or hand on a limb that has its end, grows as a branch
        from the joint closest to the cursor on screen (PlaceOnLimb).
      > Anything else, or no limb under the cursor: false, the caller places it on the body.
    */
    private bool TryGrowOnLimb(CreaturePartKind kind, Vector2 pixel)
    {
        CreaturePartInfo info  = CreatureCatalog.Get(kind);
        int              index = PartAtPixel(pixel, out int flank);
        if (index < 0 || null == Draft.Parts[index].Joints || !(info.IsSculpted || info.IsEnd))
        {
            return false;
        }
        if (info.IsEnd && Draft.Parts[index].Info.IsSculpted)
        {
            return AttachEnd(index, kind);
        }
        return PlaceOnLimb(kind, index, NearestJoint(index, flank, pixel));
    }

    /// <summary>The joint of a placed limb closest to a pixel on screen, the tip when none can be seen.</summary>
    private int NearestJoint(int index, int flank, Vector2 pixel)
    {
        IReadOnlyList<Vector3> joints = Draft.Parts[index].Joints;
        int                    nearest = joints.Count - 1;
        if (!_preview.TryGetLimbFrame(index, 0 == flank ? 1 : flank, out Transform3D frame)
            && !_preview.TryGetLimbFrame(index, -flank, out frame))
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

    /// <summary>Removes the part closest to a spine share and refunds what was paid for it.</summary>
    /// <returns>True when a part was removed.</returns>
    public bool RemoveAt(float t)
    {
        int index = Draft.IndexNear(t);
        return index >= 0 && RemovePart(index);
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
        int          refund  = Draft.BranchCost(index);     // the part and every branch growing from it
        CreaturePart removed = Draft.RemovePart(index);
        _spent    -= refund;
        PickedPart = -1;                             // indices moved
        Celebrate(PartPoint(removed), new Color(0.75f, 0.85f, 0.9f), 0.95f);
        Refresh();
        return true;
    }

    /// <summary>Picks a placed part for gizmo and size slider.</summary>
    /// <param name="index">(Required) Index in Draft.Parts, -1 clears the pick.</param>
    /// <param name="flank">(Optional) Flank of a pair the gizmo sits on, 0 = the one facing the camera. Defaults to 0.</param>
    public void PickPart(int index, int flank = 0)
    {
        PickedPart  = index >= 0 && index < Draft.Parts.Count ? index : -1;
        if (PickedPart >= 0)
        {
            PickedVertebra = -1;                     // one gizmo: a part or a vertebra
        }
        _gizmoFlank = PickedPart < 0 ? 0 : GizmoFlankFor(Draft.Parts[PickedPart], flank);
        Refresh();
    }

    /*
      The gizmo of a pair sits on one half, the other half mirrors what it does:
      > Midline part: flank 0, nothing is mirrored.
      > Single half: its own flank.
      > Pair: the wanted flank, without one the flank facing the camera.
    */
    private int GizmoFlankFor(CreaturePart part, int flank)
    {
        if (!part.Info.IsPaired)
        {
            return 0;
        }
        if (0 != part.Side)
        {
            return part.Side;
        }
        if (0 != flank)
        {
            return flank;
        }
        return _camera.Position.Z < 0f ? -1 : 1;
    }

    /// <summary>Shifts the picked part, its mirror half follows mirrored. Clamped to CreatureDesign.MAX_PART_OFFSET.</summary>
    /// <param name="delta">(Required) Shift in unit space as seen on the flank of the gizmo.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.PickPart(0); editor.MovePickedPart(new Vector3(0f, 0.1f, 0f));</code></example>
    public bool MovePickedPart(Vector3 delta)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        CreaturePart part   = Draft.Parts[PickedPart];
        Vector3      stored = _gizmoFlank < 0 ? new Vector3(delta.X, delta.Y, -delta.Z) : delta;
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
    /// <example><code>editor.TurnPickedPart(Vector3.Up, 0.5f);</code></example>
    public bool TurnPickedPart(Vector3 axis, float angle)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        CreaturePart part  = Draft.Parts[PickedPart];
        Basis        delta = _gizmoFlank < 0 ? new Basis(new Vector3(axis.X, axis.Y, -axis.Z), -angle) : new Basis(axis, angle);
        Basis        turned = delta * Basis.FromEuler(part.Rotation);
        Draft.SetPartPose(PickedPart, part.Offset, turned.Orthonormalized().GetEuler());
        return true;
    }

    /// <summary>Grows or shrinks the picked part, clamped by the design. Only the look changes.</summary>
    /// <param name="size">(Required) 1 = normal, CreatureDesign.MIN_PART_SIZE..MAX_PART_SIZE.</param>
    /// <returns>False when no part is picked.</returns>
    /// <example><code>editor.PickPart(0); editor.SetPartSize(1.3f);</code></example>
    public bool SetPartSize(float size)
    {
        if (PickedPart < 0)
        {
            return false;
        }
        Draft.SetPartSize(PickedPart, size);
        UpdateSizeRow();
        return true;
    }

    /// <summary>Sets the girth of one vertebra, the handles follow every frame.</summary>
    public void SetGirth(int index, float girth)
    {
        Draft.SetGirth(index, girth);
    }

    /// <summary>Lifts one vertebra (bends the spine), the neighbors follow smoothly. Free of charge.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="lift">(Required) Height above the straight line, clamped by the design.</param>
    /// <example><code>editor.SetLift(editor.Draft.Girths.Count - 1, 0.6f);   // a neck</code></example>
    public void SetLift(int index, float lift)
    {
        Draft.SetLift(index, lift);
    }

    /// <summary>The vertebra carrying the gizmo, -1 for none.</summary>
    public int PickedVertebra { get; private set; } = -1;

    /// <summary>Picks a vertebra for the gizmo, a picked part lets go.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end, -1 for none.</param>
    public void PickVertebra(int index)
    {
        PickedVertebra = index >= 0 && index < Draft.Girths.Count ? index : -1;
        if (PickedVertebra >= 0)
        {
            PickedPart = -1;
            Refresh();
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

    /// <summary>Pushes the picked vertebra, free of charge.</summary>
    /// <param name="delta">(Required) Shift in unit space, X along the body, Y up, Z ignored (the body stays symmetric).</param>
    /// <returns>False when no vertebra is picked.</returns>
    /// <example><code>editor.PickVertebra(0); editor.MovePickedVertebra(new Vector3(-0.5f, 0f, 0f));   // a longer tail</code></example>
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
        SetGirth(PickedVertebra, Draft.Girths[PickedVertebra] * factor);
        return true;
    }

    /// <summary>Grows (true) or shrinks (false) the body at the nose or tail end by one vertebra. Free of charge.</summary>
    /// <param name="isNose">(Required) True for the front end, false for the tail.</param>
    /// <param name="isGrowing">(Required) True adds a vertebra, false removes one.</param>
    /// <returns>False at the vertebra limits.</returns>
    /// <example><code>editor.PullEnd(isNose: true, isGrowing: true);   // the neck gets longer</code></example>
    public bool PullEnd(bool isNose, bool isGrowing)
    {
        bool isChanged = isGrowing ? Draft.GrowEnd(isNose) : Draft.ShrinkEnd(isNose);
        if (!isChanged)
        {
            return false;
        }
        Log.Verbose("CreatureEditor", $"{(isNose ? "Nose" : "Tail")} {(isGrowing ? "pulled out" : "pushed in")}, {Draft.Girths.Count} vertebrae.");
        Celebrate(HandlePosition(isNose ? Draft.Girths.Count - 1 : 0), TypeAccent(Draft.Type), isGrowing ? 1.04f : 0.97f);
        Refresh();
        return true;
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
    //   OnPreviewInput   Left drag on the gizmo shifts or turns the picked part, on a joint handle
    //                    sculpts the picked limb, on a vertebra handle bends the spine (the end handles
    //                    also pull the body longer or push it shorter, Shift sets the girth instead),
    //                    a click places the selected part or picks a placed one, left drag elsewhere
    //                    rotates, right click removes, the wheel over a vertebra handle sets its girth,
    //                    over the picked part its size, otherwise it zooms.
    //   DragJoint        Moves a joint of the picked limb in the plane facing the camera.
    //   _Process         Turns the turntable, updates changed stats, moves handles, hover marker and gizmo.
    //   ResetCamera      Side view from slightly above, forward to the right.
    //   UpdateCamera     Orbit position from yaw, pitch and zoom.
    //   HandleAt         The vertebra handle under a pixel, -1 for none.
    //   HandlePosition   Where the handle of a vertebra floats above the back.
    //   JointAt          The joint handle of the picked limb under a pixel, -1 for none.
    //   JointPosition    Where a joint of the picked limb sits (the flank of the gizmo).
    //   PartAtPixel      The placed part under a pixel and the flank hit, -1 for none.
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
    private const float GIRTH_WHEEL_STEP = 0.06f;      // per wheel notch over a vertebra handle
    private const float HANDLE_LIFT = 0.16f;          // above the back
    private const float HANDLE_PICK_PIXELS = 16f;
    private const int RAY_STEPS = 240;
    private const float TURNTABLE_SPEED = 0.15f;
    private const float SIZE_STEP = 0.1f;            // per wheel notch over the picked part

    private float _yaw;
    private float _pitch;
    private float _zoom = 1f;
    private bool _isLeftDown;
    private bool _isOrbiting;
    private Vector2 _leftDownAt;
    private int _hoverHandle = -1;
    private int _dragJoint = -1;
    private int _hoverJoint = -1;
    private Vector3? _hoverPoint;
    private GizmoHandle _gizmoDrag = GizmoHandle.None;
    private bool _isGizmoOnVertebra;
    private int _gizmoFlank;                         // flank of the picked pair the gizmo sits on, see GizmoFlankFor
    private float _time;                             // drives the breathing handles and the marker

    private void OnPreviewInput(InputEvent @event)
    {
        if (IsChoosingType)
        {
            return;
        }
        if (IsSpraying && OnSprayInput(@event))
        {
            return;
        }
        switch (@event)
        {
            // Wheel over the picked part: size, anywhere else: zoom
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                float notch = MouseButton.WheelUp == wheel.ButtonIndex ? 1f : -1f;
                int   girthAt = HandleAt(wheel.Position);
                if (girthAt >= 0)
                {
                    SetGirth(girthAt, Draft.Girths[girthAt] + notch * GIRTH_WHEEL_STEP);
                    break;
                }
                if (PickedPart >= 0 && PickedPart == PartAtPixel(wheel.Position, out _))
                {
                    SetPartSize(Draft.Parts[PickedPart].Size + notch * SIZE_STEP);
                    break;
                }
                _zoom = Mathf.Clamp(_zoom * (notch > 0f ? 1f / ZOOM_STEP : ZOOM_STEP), MIN_ZOOM, MAX_ZOOM);
                UpdateCamera();
                break;

            // Right click: remove the part under the cursor
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right:
                RemovePart(PartAtPixel(right.Position, out _));
                break;

            // Left press: the gizmo shifts or turns, a joint sculpts the limb, a vertebra handle
            // bends the spine, otherwise maybe a click, maybe a rotation
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.Pressed:
                _gizmoDrag    = _gizmo.HandleAt(left.Position);
                _gizmo.Active = _gizmoDrag;
                bool isGizmo  = GizmoHandle.None != _gizmoDrag;
                _dragJoint    = isGizmo ? -1 : JointAt(left.Position);
                int handle    = isGizmo || _dragJoint >= 0 ? -1 : HandleAt(left.Position);
                if (handle >= 0)
                {
                    PickVertebra(handle);
                }
                _isLeftDown   = !isGizmo && _dragJoint < 0 && handle < 0;
                _isOrbiting   = false;
                _pull         = 0f;
                _leftDownAt   = left.Position;
                break;

            // Left release: a click without rotation places the selected palette part, without one it picks a placed part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_isLeftDown && !_isOrbiting)
                {
                    if (null != Selected && TryGrowOnLimb(Selected.Value, left.Position))
                    {
                        // The part sits on the clicked limb now (an end on its tip or a branch)
                    }
                    else if (null != Selected && RayToBody(left.Position) is Vector3 placeHit)
                    {
                        PlaceAt(Selected.Value, placeHit);
                    }
                    else if (null == Selected)
                    {
                        int index = PartAtPixel(left.Position, out int flank);
                        PickVertebra(-1);
                        PickPart(index, flank);
                    }
                }
                _dragJoint    = -1;
                _gizmoDrag    = GizmoHandle.None;
                _gizmo.Active = GizmoHandle.None;
                _isLeftDown   = false;
                _isOrbiting   = false;
                break;

            // Gizmo drag: arrows shift along their axis, rings turn around it
            // Gizmo on a vertebra: arrows push it, the golden ring scales it
            case InputEventMouseMotion spine when GizmoHandle.None != _gizmoDrag && PickedVertebra >= 0:
                if (GizmoHandle.Scale == _gizmoDrag)
                {
                    ScalePickedVertebra(_gizmo.ScaleFactor(spine.Position - spine.Relative, spine.Position));
                }
                else
                {
                    MovePickedVertebra(_gizmo.MoveDelta(_gizmoDrag, spine.Relative));
                }
                break;

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

            // Joint drag: the picked limb follows the cursor
            case InputEventMouseMotion limb when _dragJoint >= 0:
                DragJoint(limb.Position);
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

            // Hover: gizmo and handles light up, a selected part shows where it would go
            case InputEventMouseMotion hover:
                _gizmo.Hovered = _gizmo.HandleAt(hover.Position);
                _hoverJoint    = GizmoHandle.None == _gizmo.Hovered ? JointAt(hover.Position) : -1;
                _hoverHandle   = GizmoHandle.None == _gizmo.Hovered && _hoverJoint < 0 ? HandleAt(hover.Position) : -1;
                _hoverPoint    = null != Selected && _hoverHandle < 0 && _hoverJoint < 0 && GizmoHandle.None == _gizmo.Hovered ? RayToBody(hover.Position) : null;
                break;
        }
    }

    /*
      Spray can input, the rest of OnPreviewInput only sees what it does not take:
      > Wheel over the body: nozzle size.
      > Left press on the body: a new stroke, the first dot.
      > Motion with the stroke on: a dot every SPRAY_SPACING of the nozzle; hover moves the marker.
      > Left release: the stroke ends.
      A press beside the body falls through and turns the view as usual.
    */
    private bool OnSprayInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel when RayToBody(wheel.Position) is not null:
                float notch = MouseButton.WheelUp == wheel.ButtonIndex ? 1f : -1f;
                SprayRadius = Mathf.Clamp(SprayRadius + notch * SPRAY_STEP, CreatureDesign.MIN_SPLAT_RADIUS, CreatureDesign.MAX_SPLAT_RADIUS);
                return true;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press when RayToBody(press.Position) is Vector3 start:
                _sprayStroke = Draft.NewStroke();
                _lastSpray   = start;
                SprayAt(start, _sprayStroke);
                return true;

            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } when null != _lastSpray:
                _lastSpray = null;
                Refresh();                           // undo and wipe light up
                return true;

            case InputEventMouseMotion motion:
                _sprayHover = RayToBody(motion.Position);
                if (null == _lastSpray || _sprayHover is not Vector3 at)
                {
                    return null != _lastSpray;
                }
                if (at.DistanceTo(_lastSpray.Value) >= SprayRadius * SPRAY_SPACING)
                {
                    SprayAt(at, _sprayStroke);
                    _lastSpray = at;
                }
                return true;
        }
        return false;
    }

    /// <summary>
    /// The joint stays at its depth seen from the camera and follows the cursor in that
    /// plane; the world point goes back into limb space through the frame of the limb.
    /// </summary>
    private void DragJoint(Vector2 pixel)
    {
        if (!TryGetLimbFrame(out Transform3D frame))
        {
            return;
        }
        Vector3 world = frame * Draft.Parts[PickedPart].Joints[_dragJoint];
        float   depth = (world - _camera.GlobalPosition).Dot(-_camera.GlobalBasis.Z);
        Vector3 moved = _camera.ProjectPosition(pixel, depth);
        Draft.SetLimbJoint(PickedPart, _dragJoint, frame.AffineInverse() * moved);
    }

    /// <summary>Frame of the picked limb on the flank of the gizmo, false when no limb is picked.</summary>
    private bool TryGetLimbFrame(out Transform3D frame)
    {
        frame = Transform3D.Identity;
        bool isLimb = !IsChoosingType && PickedPart >= 0 && PickedPart < Draft.Parts.Count && null != Draft.Parts[PickedPart].Joints;
        return isLimb && _preview.TryGetLimbFrame(PickedPart, _gizmoFlank, out frame);
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

        // Stats follow every change, also the handle drags that skip Refresh
        if (Draft.Version != _statsVersion)
        {
            UpdateStats();
        }

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
            bool  isActive = i == PickedVertebra || i == _hoverHandle;
            bool  isEnd    = 0 == i || Draft.Girths.Count - 1 == i;   // the ends can be pulled out, they stand out
            float size     = (isActive ? 0.09f : 0.06f + 0.008f * Mathf.Sin(_time * 3f + i)) * (isEnd ? 1.3f : 1f);
            _handles[i].Position = HandlePosition(i);
            _handles[i].Scale    = Vector3.One * size;
        }

        // Hover marker where the selected part would land
        // Marker: the spray nozzle where the paint lands (as big as the nozzle), else where a selected part would land
        if (IsSpraying && _sprayHover is Vector3 nozzle)
        {
            _marker.Visible  = true;
            _marker.Position = nozzle;
            _marker.Scale    = Vector3.One * SprayRadius * 0.45f;
        }
        else
        {
            _marker.Visible = null != _hoverPoint;
            if (_hoverPoint is Vector3 point)
            {
                _marker.Position = point;
                _marker.Scale    = Vector3.One * (0.07f + 0.015f * Mathf.Sin(_time * 8f));
            }
        }

        // Joint handles of the picked limb, on the flank of the gizmo
        bool hasLimb    = TryGetLimbFrame(out Transform3D limbFrame);
        int  jointCount = hasLimb ? Draft.Parts[PickedPart].Joints.Count : 0;
        while (_jointHandles.Count < jointCount)
        {
            MeshInstance3D joint = CellVisual3D.AddMesh(_previewRoot, CellVisual3D.SphereMesh(true), _jointMaterial, Vector3.Zero, Vector3.One * 0.06f);
            joint.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _jointHandles.Add(joint);
        }
        for (int i = 0; i < _jointHandles.Count; i++)
        {
            _jointHandles[i].Visible = i < jointCount;
            if (i < jointCount)
            {
                bool isActive = i == _dragJoint || i == _hoverJoint;
                _jointHandles[i].GlobalPosition = limbFrame * Draft.Parts[PickedPart].Joints[i];
                _jointHandles[i].Scale          = Vector3.One * (isActive ? 0.085f : 0.06f + 0.006f * Mathf.Sin(_time * 4f + i));
            }
        }

        // Gizmo on the picked vertebra (in the spine, push and scale) or on the anchor of the picked part
        bool    isVertebra = !IsChoosingType && PickedVertebra >= 0 && PickedVertebra < Draft.Girths.Count;
        bool    isPicked   = !IsChoosingType && PickedPart >= 0 && PickedPart < Draft.Parts.Count;
        Vector3 anchor     = Vector3.Zero;
        if (isVertebra)
        {
            float x = Draft.Positions[PickedVertebra];
            anchor = _preview.ToGlobal(new Vector3(x, Draft.LiftAt(x), 0f));
        }
        _gizmo.Visible = isVertebra || (isPicked && _preview.TryGetPartAnchor(PickedPart, _gizmoFlank, out anchor));
        if (_gizmo.Visible)
        {
            _gizmo.GlobalPosition = anchor;
            if (isVertebra != _isGizmoOnVertebra)
            {
                _isGizmoOnVertebra = isVertebra;
                if (isVertebra)
                {
                    _gizmo.ShowHandles(GizmoHandle.MoveX, GizmoHandle.MoveY, GizmoHandle.Scale);
                }
                else
                {
                    _gizmo.ShowHandles(PartGizmo.PART_HANDLES);
                }
            }
        }

        // Ghost of DRAG AND GHOST: goes once the dragged part leaves the clay
        if (null != _ghostKind && !_previewContainer.GetGlobalRect().HasPoint(_previewContainer.GetGlobalMousePosition()))
        {
            HideGhost();
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
        return new Vector3(x, Draft.LiftAt(x) + Draft.TopAt(x) + HANDLE_LIFT, 0f);
    }

    private int JointAt(Vector2 pixel)
    {
        if (!TryGetLimbFrame(out Transform3D frame))
        {
            return -1;
        }
        IReadOnlyList<Vector3> joints   = Draft.Parts[PickedPart].Joints;
        int                    closest  = -1;
        float                  bestDist = HANDLE_PICK_PIXELS;
        for (int i = 0; i < joints.Count; i++)
        {
            Vector3 position = frame * joints[i];
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

    private int PartAtPixel(Vector2 pixel, out int flank)
    {
        return _preview.PartAt(_camera.ProjectRayOrigin(pixel), _camera.ProjectRayNormal(pixel), out flank);
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
    // ===========================   DRAG AND GHOST   ============================
    // ===========================================================================
    //
    //   GetTileDrag   Starts dragging a tile: its part hangs at the cursor.
    //   CanDropPart   Over the clay: shows the ghost where the part would land.
    //   DropPart      Places the dragged part where the ghost shows.
    //   PreviewPart   The part alone on a copy of the body (parts only) at its snapped spot,
    //                 hidden when it does not fit there.
    //   HideGhost     Removes the ghost, the cursor icon shows again.
    //
    //   While the ghost shows, the cursor icon hides, so the part seems to snap onto the
    //   clay. The drag ends in _Notification (REFRESH).
    //

    private const int DRAG_ICON_PIXELS = 120;
    private const float GHOST_ALPHA = 0.6f;

    private Control _dragIcon;
    private CreaturePartInfo _dragged;
    private CreaturePartKind? _ghostKind;
    private Vector3 _ghostHit;
    private CreatureVisual3D _ghost;

    /// <summary>The part whose ghost shows on the clay, null when none does.</summary>
    public CreaturePartKind? GhostKind => _ghostKind;

    /// <summary>Unaffordable parts cannot be dragged.</summary>
    private Variant GetTileDrag(CreaturePartInfo info)
    {
        if (IsChoosingType || CostOf(info) > Points)
        {
            return default;
        }

        // The cursor sits in the middle of the floating part
        _dragIcon = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        Control icon = NewCreatureIcon(IconDesign(info), DraftColor, DRAG_ICON_PIXELS, isTurning: false, isPartsOnly: true);
        icon.Position = -Vector2.One * DRAG_ICON_PIXELS * 0.5f;
        _dragIcon.AddChild(icon);
        _palette.SetDragPreview(_dragIcon);
        _dragged = info;
        UpdateInfo();
        return (int)info.Kind;
    }

    private bool CanDropPart(Vector2 position, Variant data)
    {
        if (Variant.Type.Int != data.VariantType || IsChoosingType)
        {
            return false;
        }
        var kind = (CreaturePartKind)data.AsInt32();
        if (RayToBody(position) is Vector3 hit)
        {
            PreviewPart(kind, hit);
        }
        else
        {
            HideGhost();
        }
        return true;
    }

    private void DropPart(Vector2 position, Variant data)
    {
        var      kind = (CreaturePartKind)data.AsInt32();
        Vector3? hit  = RayToBody(position) ?? (null != _ghostKind ? _ghostHit : null);
        HideGhost();
        if (TryGrowOnLimb(kind, position))
        {
            return;
        }
        if (null == hit || !PlaceAt(kind, hit.Value))
        {
            Log.Verbose("CreatureEditor", $"{kind} dropped where it does not fit -> not placed.");
        }
    }

    /// <summary>Shows the ghost of a part where a drop at a point of the clay would place it.</summary>
    /// <param name="kind">(Required) The dragged part.</param>
    /// <param name="hit">(Required) Point on the clay, unit space.</param>
    /// <returns>True when the part fits there and its ghost shows.</returns>
    public bool PreviewPart(CreaturePartKind kind, Vector3 hit)
    {
        int side = SideAt(hit);
        if (!Draft.TrySnap(kind, Draft.TAt(hit.X), out float snapped, side))
        {
            HideGhost();
            return false;
        }

        // Ghost: the body without its parts, drawn parts only, with just this part
        var design = new CreatureDesign();
        design.CopyFrom(Draft, isPartsKept: false);
        design.TryPlace(kind, snapped, 0, side);
        _ghost.Design    = design;
        _ghost.BodyColor = new Color(DraftColor, GHOST_ALPHA);
        _ghostKind       = kind;
        _ghostHit        = hit;
        _hoverPoint      = hit;
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
        _ghostKind    = null;
        _ghost.Design = null;                        // a visual without design hides itself
        _hoverPoint   = null;
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh         Phase (cards or sculpting), title, points, buttons, tabs, stats, info card and tiles.
    //   UpdateTabs      Name and part count of every tab, the shown one pressed.
    //   UpdateStats     Traits of the draft (CreatureStats) as values and bars.
    //   UpdateSizeRow   Size slider of the picked part.
    //   UpdateInfo      Name and description of the dragged, hovered or selected part, else of the body type.
    //   _Notification   Re-translates after a locale change, cleans up after a drag.
    //   BuildTypeCards  Two cards with a live example creature each, first shaping only.
    //   BuildPalette    One icon tile per part of the shown tab the body type may use, popping in.
    //   UpdateTiles     Name, cost, weight, pressed and affordable state of every tile.
    //

    private const float TILE_HOVER_SCALE = 1.06f;
    private const float TILE_POP_DELAY = 0.12f;
    private const float TILE_POP_STAGGER = 0.03f;

    private sealed record PaletteTile(CreaturePartInfo Info, Button Button, Control Icon, Label Name, Label Cost);

    private readonly List<PaletteTile> _tiles = new();
    private CreaturePartInfo _hovered;
    private int _lastPoints = -1;
    private int _statsVersion = -1;

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
        _symmetryButton.Text      = Tr(IsSymmetric ? "CREATURE_SYMMETRY_ON" : "CREATURE_SYMMETRY_OFF");
        _sprayButton.Text         = Tr("CREATURE_SPRAY");
        _undoSprayButton.Text     = Tr("CREATURE_SPRAY_UNDO");
        _clearSprayButton.Text    = Tr("CREATURE_SPRAY_CLEAR");
        _sprayButton.SetPressedNoSignal(IsSpraying);
        EditorStyle.ApplyButton(_sprayButton, IsSpraying ? TypeAccent(Draft.Type) : EditorStyle.RIM_COLOR);
        _undoSprayButton.Visible   = IsSpraying;
        _clearSprayButton.Visible  = IsSpraying;
        _undoSprayButton.Disabled  = 0 == Draft.Splats.Count;
        _clearSprayButton.Disabled = 0 == Draft.Splats.Count;
        _symmetryButton.SetPressedNoSignal(IsSymmetric);
        EditorStyle.ApplyButton(_symmetryButton, IsSymmetric ? TypeAccent(Draft.Type) : EditorStyle.RIM_COLOR);
        _cards.Visible            = IsChoosingType;
        _sculpt.Visible           = !IsChoosingType;
        _tabStrip.Visible         = !IsChoosingType;
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
        foreach ((CreaturePattern pattern, Button button) in _patternButtons)
        {
            button.Text = Tr($"CREATURE_PATTERN_{pattern}".ToUpperInvariant());
            EditorStyle.ApplyButton(button, pattern == Draft.Pattern ? TypeAccent(Draft.Type) : EditorStyle.RIM_COLOR);
        }
        foreach ((Color? paintColor, Button swatch) in _patternSwatches)
        {
            swatch.Visible = CreaturePattern.None != Draft.Pattern || IsSpraying;
            EditorStyle.ApplySwatch(swatch, paintColor ?? DraftColor.Darkened(0.45f), paintColor == Draft.PatternColor);
        }
        foreach ((Color? swatchColor, Button swatch) in _swatches)
        {
            EditorStyle.ApplySwatch(swatch, swatchColor ?? Player.ORGANISM_COLOR, swatchColor == Draft.Color);
        }

        UpdateTabs();
        UpdateStats();
        UpdateSizeRow();
        UpdateInfo();
        UpdateTiles();
    }

    /// <summary>Slider and percent of the picked part, hidden without one.</summary>
    private void UpdateSizeRow()
    {
        bool isShown = !IsChoosingType && PickedPart >= 0 && PickedPart < Draft.Parts.Count;
        _sizeRow.Visible = isShown;
        if (!isShown)
        {
            return;
        }
        float size = Draft.Parts[PickedPart].Size;
        _sizeLabel.Text = Tr("CREATURE_PART_SIZE");
        _sizeValue.Text = string.Format(Tr("CREATURE_UNIT_PERCENT"), Mathf.RoundToInt(size * 100f));
        _sizeSlider.SetValueNoSignal(size);
    }

    private static string TypeKey(CreatureType type, string suffix)
    {
        return $"CREATURE_TYPE_{type}_{suffix}".ToUpperInvariant();
    }

    private static string CategoryKey(CreatureCategory category)
    {
        return $"CREATURE_CATEGORY_{category}".ToUpperInvariant();
    }

    /// <summary>"Eyes" or "Eyes · 2" once parts of the tab sit on the body.</summary>
    private void UpdateTabs()
    {
        foreach ((CreatureCategory category, Button tab) in _tabButtons)
        {
            int placed = 0;
            foreach (CreaturePart part in Draft.Parts)
            {
                placed += category == part.Info.Category ? 1 : 0;
            }
            tab.Text = placed > 0 ? $"{Tr(CategoryKey(category))} · {placed}" : Tr(CategoryKey(category));
            tab.SetPressedNoSignal(category == Category);
            tab.CustomMinimumSize = new Vector2(0f, category == Category ? TAB_ACTIVE_HEIGHT : TAB_HEIGHT);
        }
    }

    /*
      Bars are filled up to a share of a generous maximum, so a normal body sits in the
      middle and growth stays visible. Weight and carry share one scale (MAX_WEIGHT_BAR).
      The carry bar turns orange when the legs carry less than the body weighs, the
      flight bar fills towards lift = weight and turns green there.
    */
    private const float MAX_WEIGHT_BAR = 30f;
    private const float MAX_SPEED_BAR = 2f;
    private const float MAX_LAND_BAR = 1.2f;
    private const float MAX_AGILITY_BAR = 3f;
    private const float MAX_HEALTH_BAR = 2.5f;
    private const float MAX_RANGE_BAR = 2.5f;
    private const int MAX_EP_BAR = 4;
    private static readonly Color GOOD_COLOR = new(0.45f, 0.92f, 0.6f);
    private static readonly Color WARN_COLOR = new(1f, 0.68f, 0.3f);

    private void UpdateStats()
    {
        _statsVersion = Draft.Version;
        if (IsChoosingType)
        {
            return;
        }
        CreatureStats stats  = CreatureStats.From(Draft);
        Color         accent = TypeAccent(Draft.Type);
        string Kg(float kg) => string.Format(Tr("CREATURE_UNIT_KG"), kg.ToString("0.0"));
        string Percent(float share) => string.Format(Tr("CREATURE_UNIT_PERCENT"), Mathf.RoundToInt(share * 100f));

        // Weight and what the legs carry
        _statsTitle.Text = Tr("CREATURE_STATS_TITLE");
        SetStat(_weightRow, Kg(stats.Weight), stats.Weight / MAX_WEIGHT_BAR, accent);
        bool isCarried = stats.Carry >= stats.Weight;
        SetStat(_carryRow, stats.Carry > 0f ? Kg(stats.Carry) : Tr("CREATURE_CARRY_NONE"), stats.Carry / MAX_WEIGHT_BAR, isCarried ? GOOD_COLOR : WARN_COLOR);

        // Movement and body
        float range = (Player.BASE_MAGNET_RANGE + stats.MagnetBonus) / Player.BASE_MAGNET_RANGE;
        int   ep    = Player.EP_PER_LEVEL + stats.EpPerLevelBonus;
        SetStat(_swimRow, Percent(stats.SwimSpeed), stats.SwimSpeed / MAX_SPEED_BAR, accent);
        SetStat(_landRow, Percent(stats.LandSpeed), stats.LandSpeed / MAX_LAND_BAR, stats.Carry > 0f && isCarried ? GOOD_COLOR : WARN_COLOR);
        SetStat(_agilityRow, Percent(stats.Acceleration), stats.Acceleration / MAX_AGILITY_BAR, accent);
        SetStat(_healthRow, Percent(stats.Health), stats.Health / MAX_HEALTH_BAR, accent);
        SetStat(_rangeRow, Percent(range), range / MAX_RANGE_BAR, accent);
        SetStat(_epRow, ep.ToString(), (float)ep / MAX_EP_BAR, accent);

        // Flight: not possible yet, the bar shows how close the wings come
        string flight = stats.Lift <= 0f
            ? Tr("CREATURE_FLIGHT_NONE")
            : stats.CanFly ? Tr("CREATURE_FLIGHT_READY") : string.Format(Tr("CREATURE_FLIGHT_HEAVY"), Mathf.RoundToInt(stats.FlightRatio * 100f));
        SetStat(_flightRow, flight, stats.FlightRatio, stats.CanFly ? GOOD_COLOR : WARN_COLOR);
    }

    private void SetStat(StatRow row, string value, float share, Color color)
    {
        row.Name.Text        = Tr(row.Key);
        row.Value.Text       = value;
        row.Fill.AnchorRight = Mathf.Clamp(share, 0f, 1f);
        row.Fill.Color       = color;
    }

    private void UpdateInfo()
    {
        // Dragged tile first, then the hovered one, then the palette selection, then the picked part on the body
        CreaturePartInfo info = _dragged
            ?? _hovered
            ?? (null != Selected ? CreatureCatalog.Get(Selected.Value) : null)
            ?? (PickedPart >= 0 && PickedPart < Draft.Parts.Count ? Draft.Parts[PickedPart].Info : null);
        if (IsChoosingType)
        {
            _infoName.Text = "";
            _infoText.Text = "";
            return;
        }
        if (IsSpraying)
        {
            _infoName.Text = Tr("CREATURE_SPRAY");
            _infoText.Text = Tr("CREATURE_SPRAY_HINT");
            _infoName.AddThemeColorOverride("font_color", SprayColor.Lightened(0.3f));
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

        // Drag over (dropped or cancelled): ghost and drag info go, see DRAG AND GHOST
        if (NotificationDragEnd == what && null != _dragged)
        {
            // Godot does not always deliver the drop over the SubViewportContainer, the ghost knows the spot
            if (null != _ghostKind && !GetViewport().GuiIsDragSuccessful())
            {
                PlaceAt(_ghostKind.Value, _ghostHit);
            }
            _dragged  = null;
            _dragIcon = null;
            HideGhost();
            UpdateInfo();
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
            Control icon = NewCreatureIcon(CreatureDesign.Example(type), DraftColor, 220, isTurning: true);
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

    /// <summary>Rebuilt on every Open, type choice and tab switch: type and tab decide which parts are offered.</summary>
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
        foreach (CreaturePartInfo info in CreatureCatalog.ForCategory(Draft.Type, Category))
        {
            var button = new Button
            {
                CustomMinimumSize = TILE_SIZE,
                ToggleMode        = true,
                ButtonGroup       = group,
                FocusMode         = Control.FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };
            EditorStyle.ApplyFloating(button, accent);
            _palette.AddChild(button);

            // The part floats free: bigger than its spot, reaching above it and under the texts, bobbing
            Control icon = NewCreatureIcon(IconDesign(info), DraftColor, TILE_ICON_PIXELS, isTurning: false, isPartsOnly: true);
            icon.Position = new Vector2((TILE_SIZE.X - TILE_ICON_PIXELS) * 0.5f, TILE_ICON_TOP);
            button.AddChild(icon);
            EditorStyle.Bob(icon, TILE_BOB_PIXELS, _tiles.Count * 0.23f);

            // Name and cost at the bottom, drawn above every icon (also the ones of the next row)
            var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End, ZIndex = 1 };
            stack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 2);
            stack.AddThemeConstantOverride("separation", -2);
            button.AddChild(stack);
            Label name = NewLabel(16);
            Label cost = NewLabel(13);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            cost.HorizontalAlignment = HorizontalAlignment.Center;
            cost.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
            stack.AddChild(name);
            stack.AddChild(cost);

            // Switching tiles unpresses the old one, that must not clear the new selection
            CreaturePartKind kind = info.Kind;
            button.Toggled += isPressed =>
            {
                if (isPressed) Select(kind);
                else if (kind == Selected) Select(null);
            };
            button.SetDragForwarding(Callable.From<Vector2, Variant>(_ => GetTileDrag(info)), new Callable(), new Callable());
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

    /// <summary>The clay blob of the draft's type that carries only this part: eyes near the nose, arms in front, legs behind.</summary>
    private CreatureDesign IconDesign(CreaturePartInfo info)
    {
        CreatureDesign design = CreatureDesign.Blob();
        design.Type = Draft.Type;
        float t = info.Category switch
        {
            CreatureCategory.Eyes  => 0.85f,
            CreatureCategory.Hands => 0.72f,
            CreatureCategory.Feet  => 0.4f,
            CreatureCategory.Limbs => 0.5f,
            _                      => 0.55f,
        };
        design.TryPlace(info.Kind, t, 0);
        return design;
    }

    private void UpdateTiles()
    {
        foreach (PaletteTile tile in _tiles)
        {
            int  cost        = CostOf(tile.Info);
            bool isAvailable = cost <= Points;
            tile.Name.Text       = Tr(tile.Info.NameKey);
            tile.Cost.Text       = $"{cost} EP ·{string.Format(Tr("CREATURE_UNIT_KG"), tile.Info.Weight.ToString("0.0"))}";
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
    //   BuildPanel        Grey-out over the game, the full screen panel with header, tabs, cards,
    //                     sculpt area (palette, turntable, stats) and footer.
    //   BuildTabs         One index card tab per part category.
    //   BuildStats        The column of trait rows on the right.
    //   BuildFooter       Info card, size slider of the picked part, points badge and the done button.
    //   BuildPreview      SubViewport with orbit camera, turntable, the clay, handles, marker, drag ghost,
    //                     gizmo, the vertebra buttons and the symmetry switch.
    //   NewStatRow        Name, value and a bar for one trait.
    //   NewCreatureIcon   A live 3D creature (cards) or only its part (tiles) in its own small viewport.
    //   NewLabel          Label with outline, translated here.
    //   NewPillButton     Small pill button without focus.
    //   SetAlpha          Fades a canvas item.
    //

    private const float PANEL_SHARE = 1f;              // full screen, the membrane edge sits just off screen
    private const float EDGE_PIXELS = 40f;
    private const int PANEL_MARGIN = 24;
    private const int TILE_ICON_PIXELS = 168;           // wider than the spot: the part reaches over its edges
    private const float TILE_ICON_TOP = -30f;           // and above it, under the texts of the row above
    private const float TILE_BOB_PIXELS = 5f;
    private const float CARD_CAMERA_DISTANCE = 3.6f;
    private const float TILE_CAMERA_DISTANCE = 2.9f;
    private const int PALETTE_COLUMNS = 2;
    private const float STATS_WIDTH = 290f;
    private const float TURNTABLE_RADIUS = 1.7f;
    private static readonly Vector2 TILE_SIZE = new(150, 140);
    private const float TAB_HEIGHT = 38f;
    private const float TAB_ACTIVE_HEIGHT = 46f;
    private static readonly Shader GREYOUT_SHADER = GD.Load<Shader>("res://shaders/greyout.gdshader");
    private static readonly Shader PANEL_SHADER = GD.Load<Shader>("res://shaders/editor_panel.gdshader");

    private sealed record StatRow(string Key, Label Name, Label Value, ColorRect Fill);

    private Control _root;
    private ShaderMaterial _greyout;
    private ShaderMaterial _panelBackground;
    private Control _panel;
    private Label _titleLabel;
    private VBoxContainer _tabStrip;
    private HBoxContainer _tabs;
    private readonly List<(CreatureCategory Category, Button Tab)> _tabButtons = new();
    private HBoxContainer _cards;
    private HBoxContainer _sculpt;
    private GridContainer _palette;
    private Label _statsTitle;
    private StatRow _weightRow;
    private StatRow _carryRow;
    private StatRow _swimRow;
    private StatRow _landRow;
    private StatRow _agilityRow;
    private StatRow _healthRow;
    private StatRow _rangeRow;
    private StatRow _epRow;
    private StatRow _flightRow;
    private PanelContainer _pointsBadge;
    private Label _pointsLabel;
    private Label _infoName;
    private Label _infoText;
    private Button _doneButton;
    private Button _addButton;
    private Button _removeButton;
    private HBoxContainer _swatchRow;
    private readonly List<(Color? Color, Button Swatch)> _swatches = new();
    private HBoxContainer _patternRow;
    private HBoxContainer _paintRow;
    private readonly List<(CreaturePattern Pattern, Button Button)> _patternButtons = new();
    private readonly List<(Color? Color, Button Swatch)> _patternSwatches = new();
    private Camera3D _camera;
    private Node3D _previewRoot;
    private Node3D _turntable;
    private CreatureVisual3D _preview;
    private MeshInstance3D _marker;
    private PartGizmo _gizmo;
    private SubViewportContainer _previewContainer;
    private Button _symmetryButton;
    private Button _sprayButton;
    private Button _undoSprayButton;
    private Button _clearSprayButton;
    private HBoxContainer _sizeRow;
    private Label _sizeLabel;
    private HSlider _sizeSlider;
    private Label _sizeValue;
    private readonly List<MeshInstance3D> _handles = new();
    private readonly List<MeshInstance3D> _jointHandles = new();
    private readonly StandardMaterial3D _jointMaterial = new()
    {
        AlbedoColor              = new Color(0.75f, 1f, 0.95f),
        EmissionEnabled          = true,
        Emission                 = new Color(0.35f, 0.95f, 0.85f),
        EmissionEnergyMultiplier = 1.3f,
        NoDepthTest              = true,             // joints inside the clay stay grabbable
        RenderPriority           = 5,
    };
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

        _titleLabel = NewLabel(38);
        GameFonts.UseDisplay(_titleLabel);
        layout.AddChild(_titleLabel);

        // Category tabs as index card tabs on a baseline, hidden during the type choice
        _tabStrip = new VBoxContainer();
        _tabStrip.AddThemeConstantOverride("separation", 0);
        layout.AddChild(_tabStrip);
        _tabs = new HBoxContainer { CustomMinimumSize = new Vector2(0f, TAB_ACTIVE_HEIGHT) };
        _tabs.AddThemeConstantOverride("separation", 4);
        _tabStrip.AddChild(_tabs);
        _tabStrip.AddChild(new ColorRect
        {
            Color             = new Color(EditorStyle.RIM_COLOR, 0.8f),
            CustomMinimumSize = new Vector2(0f, 2f),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        });
        BuildTabs();

        // Type cards of the first shaping
        _cards = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _cards.AddThemeConstantOverride("separation", 18);
        layout.AddChild(_cards);

        // Sculpting: tile palette left, turntable in the middle, stats right
        _sculpt = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _sculpt.AddThemeConstantOverride("separation", 16);
        layout.AddChild(_sculpt);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize    = new Vector2(PALETTE_COLUMNS * (TILE_SIZE.X + 10) + 28, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _sculpt.AddChild(scroll);
        var paletteMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            paletteMargin.AddThemeConstantOverride(side, 8);
        }
        scroll.AddChild(paletteMargin);
        _palette = new GridContainer { Columns = PALETTE_COLUMNS, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _palette.AddThemeConstantOverride("h_separation", 10);
        _palette.AddThemeConstantOverride("v_separation", 10);
        paletteMargin.AddChild(_palette);
        BuildPreview(_sculpt);
        BuildStats(_sculpt);

        BuildFooter(layout);
    }

    private void BuildTabs()
    {
        var group = new ButtonGroup();
        foreach (CreatureCategory category in System.Enum.GetValues<CreatureCategory>())
        {
            // Tabs stand on the baseline, the pressed one grows taller in UpdateTabs (no hover scale, it would lift off)
            Button tab = NewPillButton(new Vector2(0, TAB_HEIGHT), 18);
            tab.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            tab.ToggleMode        = true;
            tab.ButtonGroup       = group;
            EditorStyle.ApplyTab(tab, EditorStyle.RIM_COLOR);
            tab.Pressed += () => ShowCategory(category);
            _tabs.AddChild(tab);
            _tabButtons.Add((category, tab));
        }
    }

    private void BuildStats(Container parent)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(STATS_WIDTH, 0f),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        StyleBoxFlat style = EditorStyle.Pill(new Color(0.04f, 0.09f, 0.13f, 0.82f), new Color(EditorStyle.RIM_COLOR, 0.45f));
        style.SetContentMarginAll(16);
        card.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(card);

        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 10);
        card.AddChild(rows);
        _statsTitle = NewLabel(24);
        GameFonts.UseDisplay(_statsTitle);
        rows.AddChild(_statsTitle);

        // Weight first, it decides walking now and flying later
        _weightRow  = NewStatRow(rows, "CREATURE_STAT_WEIGHT");
        _carryRow   = NewStatRow(rows, "CREATURE_STAT_CARRY");
        _landRow    = NewStatRow(rows, "CREATURE_STAT_LAND");
        _flightRow  = NewStatRow(rows, "CREATURE_STAT_FLIGHT");
        rows.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
        _swimRow    = NewStatRow(rows, "CREATURE_STAT_SWIM");
        _agilityRow = NewStatRow(rows, "CREATURE_STAT_TURN");
        _healthRow  = NewStatRow(rows, "CREATURE_STAT_HEALTH");
        _rangeRow   = NewStatRow(rows, "CREATURE_STAT_RANGE");
        _epRow      = NewStatRow(rows, "CREATURE_STAT_EP");
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

        // Size slider of the picked part, only shown while one is picked
        _sizeRow = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, Visible = false };
        _sizeRow.AddThemeConstantOverride("separation", 10);
        footer.AddChild(_sizeRow);
        _sizeLabel  = NewLabel(17);
        _sizeValue  = NewLabel(17);
        _sizeValue.CustomMinimumSize = new Vector2(64f, 0f);
        _sizeSlider = new HSlider
        {
            MinValue          = CreatureDesign.MIN_PART_SIZE,
            MaxValue          = CreatureDesign.MAX_PART_SIZE,
            Step              = 0.05,
            Value             = 1.0,
            CustomMinimumSize = new Vector2(180f, 28f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode         = Control.FocusModeEnum.None,
        };
        _sizeSlider.ValueChanged += value => SetPartSize((float)value);
        _sizeRow.AddChild(_sizeLabel);
        _sizeRow.AddChild(_sizeSlider);
        _sizeRow.AddChild(_sizeValue);

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
        container.SetDragForwarding(new Callable(),
            Callable.From<Vector2, Variant, bool>(CanDropPart),
            Callable.From<Vector2, Variant>(DropPart));
        parent.AddChild(container);
        _previewContainer = container;

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

        // Ghost of a dragged part (DRAG AND GHOST): parts only, on the same swim wave as the clay
        _ghost = new CreatureVisual3D { Knead = CreatureVisual3D.EDITOR_KNEAD, SwimScale = 0.3f, Seed = 7f, IsPartsOnly = true, Visible = false };
        _previewRoot.AddChild(_ghost);

        // Gizmo on the picked part
        _gizmo = new PartGizmo { Camera = _camera };
        _previewRoot.AddChild(_gizmo);

        // + and - for vertebrae and the symmetry switch, bottom left over the turntable
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
        _symmetryButton            = NewPillButton(new Vector2(0, 40), 16);
        _symmetryButton.ToggleMode = true;
        _symmetryButton.Toggled   += SetSymmetric;
        buttons.AddChild(_symmetryButton);

        // Spray can, undo and wipe as a compact column at the left edge (the tail end, the head stays free);
        // undo and wipe only show while the spray can is on (Refresh)
        var sprayRow = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        sprayRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft, Control.LayoutPresetMode.Minsize, 10);
        sprayRow.GrowVertical = Control.GrowDirection.Both;
        sprayRow.AddThemeConstantOverride("separation", 8);
        container.AddChild(sprayRow);
        _sprayButton            = NewPillButton(new Vector2(0, 34), 14);
        _sprayButton.ToggleMode = true;
        _sprayButton.Toggled   += SetSpraying;
        _undoSprayButton        = NewPillButton(new Vector2(0, 34), 14);
        _undoSprayButton.Pressed += () => UndoSpray();
        _clearSprayButton       = NewPillButton(new Vector2(0, 34), 14);
        _clearSprayButton.Pressed += ClearSpray;
        foreach (Button button in new[] { _sprayButton, _undoSprayButton, _clearSprayButton })
        {
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            sprayRow.AddChild(button);
        }

        // Color swatches, top center over the turntable: coral default first, translucent last
        _swatchRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _swatchRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 10);
        _swatchRow.GrowHorizontal = Control.GrowDirection.Both;
        _swatchRow.AddThemeConstantOverride("separation", 8);
        container.AddChild(_swatchRow);
        var swatchColors = new List<Color?> { null };
        foreach (Color color in EditorStyle.BODY_SWATCHES)
        {
            swatchColors.Add(color);
        }
        foreach (Color? swatchColor in swatchColors)
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(30, 30), FocusMode = Control.FocusModeEnum.None };
            swatch.Pressed      += () => PickColor(swatchColor);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            _swatchRow.AddChild(swatch);
            _swatches.Add((swatchColor, swatch));
        }

        // Paint rows below: one pill per pattern, under it the second color (opaque swatches, dark shade first)
        _patternRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _patternRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 10);
        _patternRow.OffsetTop     += PATTERN_ROW_OFFSET;
        _patternRow.OffsetBottom  += PATTERN_ROW_OFFSET;
        _patternRow.GrowHorizontal = Control.GrowDirection.Both;
        _patternRow.AddThemeConstantOverride("separation", 6);
        container.AddChild(_patternRow);
        foreach (CreaturePattern pattern in System.Enum.GetValues<CreaturePattern>())
        {
            Button pill = NewPillButton(new Vector2(0, 30), 14);
            pill.Pressed += () => PickPattern(pattern);
            _patternRow.AddChild(pill);
            _patternButtons.Add((pattern, pill));
        }
        _paintRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _paintRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 10);
        _paintRow.OffsetTop     += PAINT_ROW_OFFSET;
        _paintRow.OffsetBottom  += PAINT_ROW_OFFSET;
        _paintRow.GrowHorizontal = Control.GrowDirection.Both;
        _paintRow.AddThemeConstantOverride("separation", 6);
        container.AddChild(_paintRow);
        var paintColors = new List<Color?> { null };
        foreach (Color color in EditorStyle.BODY_SWATCHES.Where(color => color.A >= 0.99f))
        {
            paintColors.Add(color);
        }
        foreach (Color? paintColor in paintColors)
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(24, 24), FocusMode = Control.FocusModeEnum.None, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            swatch.Pressed      += () => PickPatternColor(paintColor);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            _paintRow.AddChild(swatch);
            _patternSwatches.Add((paintColor, swatch));
        }
    }

    private const float PATTERN_ROW_OFFSET = 44f;    // below the color swatches
    private const float PAINT_ROW_OFFSET = 82f;      // second color below the pattern pills

    /// <summary>Name left, value right, a thin bar below that UpdateStats fills.</summary>
    /// <param name="parent">(Required) The stats column.</param>
    /// <param name="key">(Required) Translation key of the trait name.</param>
    private static StatRow NewStatRow(Container parent, string key)
    {
        var row = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 3);
        parent.AddChild(row);

        var   line  = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        Label name  = NewLabel(15);
        Label value = NewLabel(15);
        name.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        line.AddChild(name);
        line.AddChild(value);
        row.AddChild(line);

        // Bar: dark track, the fill grows from the left via its right anchor
        var track = new ColorRect { Color = new Color(1f, 1f, 1f, 0.08f), CustomMinimumSize = new Vector2(0f, 7f), MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(track);
        var fill = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        fill.AnchorRight = 0f;
        track.AddChild(fill);
        return new StatRow(key, name, value, fill);
    }

    /// <summary>A live creature in its own small studio, seen from the front flank.</summary>
    /// <param name="design">(Required) The creature shown.</param>
    /// <param name="color">(Required) Body color.</param>
    /// <param name="pixels">(Required) Edge length of the square icon.</param>
    /// <param name="isTurning">(Required) Turns slowly on the spot (type cards).</param>
    /// <param name="isPartsOnly">(Optional) Only the parts without the body, the camera frames them. Defaults to false.</param>
    private static Control NewCreatureIcon(CreatureDesign design, Color color, int pixels, bool isTurning, bool isPartsOnly = false)
    {
        var container = new SubViewportContainer
        {
            Stretch           = true,
            CustomMinimumSize = new Vector2(pixels, pixels),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        SubViewport viewport = PartIcon.NewStudio(new Vector2I(pixels, pixels));
        container.AddChild(viewport);

        var visual = new CreatureVisual3D { Design = design, BodyColor = color, Knead = CreatureVisual3D.EDITOR_KNEAD, IsPartsOnly = isPartsOnly };
        viewport.AddChild(visual);
        if (isTurning)
        {
            visual.Rotation = new Vector3(0f, -0.5f, 0f);
            Tween spin = visual.CreateTween().SetLoops().SetPauseMode(Tween.TweenPauseMode.Process);
            spin.TweenProperty(visual, "rotation:y", 0.5f, 3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            spin.TweenProperty(visual, "rotation:y", -0.5f, 3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        // Cards look at the whole creature, a parts-only tile lets the visual frame its part
        Vector3 offset = new Vector3(0.35f, 0.55f, 1f).Normalized() * (isTurning ? CARD_CAMERA_DISTANCE : TILE_CAMERA_DISTANCE);
        var camera = new Camera3D { Fov = 40f, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), offset);
        viewport.AddChild(camera);
        if (isPartsOnly)
        {
            visual.FitCamera = camera;
        }
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
