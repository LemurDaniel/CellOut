// NOTE:
//   Puts parts on the clay of the creature editor and takes them off again: on the
//   body at a spine share or a point, as an end on the tip of a limb or as a branch
//   from one of its joints. It pays through the session, hands the gizmo to the new
//   part, lets go of the palette tile and cheers on the stage (ring burst). A refused
//   part is noted in the verbose log. It also knows what a click or a drop on the clay
//   means. It reads no input on its own: the CreatureEditor calls ClickAt, the
//   CreatureDropGhost reports the drop.
//
//   var placer = new CreaturePartPlacer();
//   placer.Setup(session, stage);
//   ghost.Dropped += placer.DropAt;
//   AddChild(placer);
//   placer.TryPlace(CreaturePartKind.Eye, 0.8f);

//
//   Chapters:
//     PLACE AND REMOVE   Paid placing on body and limbs, removing with refund, the pick.
//     CLICK AND DROP     What a click or a dropped tile on the clay does.
//

using Godot;

namespace CellOut;

public partial class CreaturePartPlacer : Node
{
    // ===========================================================================
    // ==========================   PLACE AND REMOVE   ===========================
    // ===========================================================================
    //
    //   Setup        Takes session and stage.
    //   TryPlace     Places a part at a spine share if affordable and free.
    //   PlaceAt      Places a part at a point of the clay, the flank from the point.
    //   AttachEnd    Sticks a foot or hand on the tip of a placed sculpted limb.
    //   PlaceOnLimb  Grows a limb (or a foot or hand with its limb) from a joint of a placed limb.
    //   RemoveAt     Removes the closest part and refunds it.
    //   RemovePart   Removes one placed part and refunds it.
    //   PickPart     Picks a placed part for gizmo and size slider, -1 for none.
    //

    private const string LOG_TAG = "CreatureEditor";   // one tag for the whole editor, as before the split into components
    private static readonly Color REMOVE_COLOR = new(0.75f, 0.85f, 0.9f);

    private CreatureEditorSession _session;
    private CreatureStage _stage;

    /// <summary>Call once before the first part is placed.</summary>
    /// <param name="session">(Required) The sitting: draft, points and selection.</param>
    /// <param name="stage">(Required) The stage, for the facing flank, what lies under a pixel and the ring burst.</param>
    public void Setup(CreatureEditorSession session, CreatureStage stage)
    {
        _session = session;
        _stage   = stage;
    }

    /// <summary>Places a part if the body type may use it, it is affordable and its spot is free.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Share of the spine, 0 tail .. 1 nose.</param>
    /// <param name="side">(Optional) 0 a pair, -1 left or 1 right for a single half of a paired part. Defaults to 0.</param>
    /// <returns>True when the part was placed and paid.</returns>
    /// <example><code>placer.TryPlace(CreaturePartKind.Hands, 0.6f, side: 1)   // one right hand, half price</code></example>
    public bool TryPlace(CreaturePartKind kind, float t, int side = 0)
    {
        CreatureDesign draft = _session.Draft;
        if (!_session.TryPlace(kind, t, side, out int cost))
        {
            if (_session.IsChoosingType || cost > _session.Points)
            {
                Log.Verbose(LOG_TAG, $"{kind} costs {cost} EP, {_session.Points} left (choosing: {_session.IsChoosingType}) -> not placed.");
            }
            return false;
        }
        CreaturePart placed = draft.Parts[^1];
        PickPart(draft.Parts.Count - 1, placed.Side);   // the new part gets the gizmo
        _session.Select(null);                          // deselected, so the gizmo has focus until the next tile
        _stage.Celebrate(_session.PointOf(placed), CreatureEditorUi.TypeAccent(draft.Type), 1.08f);
        return true;
    }

    /// <summary>Places a part where a point of the clay was hit; with symmetry off a side part goes to that flank only.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="hit">(Required) Point on or in the clay, unit space.</param>
    /// <returns>True when the part was placed and paid.</returns>
    public bool PlaceAt(CreaturePartKind kind, Vector3 hit)
    {
        return TryPlace(kind, _session.Draft.TAt(hit.X), _session.SideAt(hit));
    }

    /// <summary>
    /// Sticks a foot or hand on the tip of a placed bare sculpted limb. It costs the full
    /// price of the end minus what the limb cost, at least 1 point.
    /// </summary>
    /// <param name="limbIndex">(Required) Index in Draft.Parts, a sculpted limb without an end.</param>
    /// <param name="end">(Required) A foot or hand kind.</param>
    /// <returns>False when the part is no bare limb, the end is no foot or hand or the points do not reach.</returns>
    /// <example><code>placer.AttachEnd(limb, CreaturePartKind.Claws);</code></example>
    public bool AttachEnd(int limbIndex, CreaturePartKind end)
    {
        CreatureDesign draft = _session.Draft;
        if (!_session.AttachEnd(limbIndex, end, out int cost))
        {
            // A price was asked: limb and end fit, the points did not reach
            if (cost >= 0)
            {
                Log.Verbose(LOG_TAG, $"{end} on limb {limbIndex} costs {cost} EP, {_session.Points} left -> not attached.");
            }
            return false;
        }
        PickPart(limbIndex, draft.Parts[limbIndex].Side);
        _stage.Celebrate(_session.PointOf(draft.Parts[limbIndex]), CreatureEditorUi.TypeAccent(draft.Type), 1.06f);
        return true;
    }

    /// <summary>Grows a limb (or a foot or hand with its limb) from a joint of a placed limb, at the price of the part.</summary>
    /// <param name="kind">(Required) A sculpted limb, a foot or a hand.</param>
    /// <param name="parentIndex">(Required) Index in Draft.Parts of a limb with joints.</param>
    /// <param name="joint">(Required) Joint of the parent, 0 = first .. tip.</param>
    /// <returns>False when the kind grows no limb, the parent has no such joint or the points do not reach.</returns>
    /// <example><code>placer.PlaceOnLimb(CreaturePartKind.Tentacle, arm, 1);</code></example>
    public bool PlaceOnLimb(CreaturePartKind kind, int parentIndex, int joint)
    {
        CreatureDesign draft = _session.Draft;
        if (!_session.PlaceOnLimb(kind, parentIndex, joint, out int cost))
        {
            // A price was asked: the parent exists, the part did not fit or the points did not reach
            if (cost >= 0)
            {
                Log.Verbose(LOG_TAG, $"{kind} on limb {parentIndex} joint {joint} costs {cost} EP, {_session.Points} left -> not placed.");
            }
            return false;
        }
        PickPart(draft.Parts.Count - 1, draft.Parts[parentIndex].Side);
        _stage.Celebrate(_session.PointOf(draft.Parts[^1]), CreatureEditorUi.TypeAccent(draft.Type), 1.06f);
        return true;
    }

    /// <summary>Removes the part closest to a spine share and refunds what was paid for it.</summary>
    /// <returns>True when a part was removed.</returns>
    public bool RemoveAt(float t)
    {
        int index = _session.Draft.IndexNear(t);
        return index >= 0 && RemovePart(index);
    }

    /// <summary>Removes one placed part and refunds what was paid for it.</summary>
    /// <param name="index">(Required) Index in Draft.Parts.</param>
    /// <returns>False for an index outside Draft.Parts.</returns>
    public bool RemovePart(int index)
    {
        if (!_session.RemovePart(index, out CreaturePart removed))
        {
            return false;
        }
        _stage.Celebrate(_session.PointOf(removed), REMOVE_COLOR, 0.95f);
        return true;
    }

    /// <summary>Picks a placed part for gizmo and size slider.</summary>
    /// <param name="index">(Required) Index in Draft.Parts, -1 clears the pick.</param>
    /// <param name="flank">(Optional) Flank of a pair the gizmo sits on, 0 = the one facing the camera. Defaults to 0.</param>
    public void PickPart(int index, int flank = 0)
    {
        _session.PickPart(index, 0 != flank ? flank : _stage.FacingFlank);
    }

    // ===========================================================================
    // ===========================   CLICK AND DROP   ============================
    // ===========================================================================
    //
    //   ClickAt         A click on free space places the selected part or picks a placed one.
    //   DropAt          A dragged tile was let go over the stage.
    //   TryGrowOnLimb   A part dropped or clicked onto a placed limb sticks on its tip or branches.
    //

    /// <summary>A click without rotation: places the selected palette part, without one it picks a placed part.</summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    public void ClickAt(Vector2 pixel)
    {
        // Nothing selected: the click picks the part under it, or lets go of part and vertebra
        if (null == _session.Selected)
        {
            int index = _stage.PartAt(pixel, out int flank);
            _session.PickVertebra(-1);
            PickPart(index, flank);
            return;
        }

        // On a limb the part sticks on its tip or grows as a branch, else it goes on the body
        CreaturePartKind kind = _session.Selected.Value;
        if (TryGrowOnLimb(kind, pixel))
        {
            return;
        }
        if (_stage.RayToBody(pixel) is Vector3 placeHit)
        {
            PlaceAt(kind, placeHit);
        }
    }

    /// <summary>Places a dropped tile: on the limb under the cursor, else on the body where the ghost showed.</summary>
    /// <param name="kind">(Required) The dragged part.</param>
    /// <param name="pixel">(Required) Position of the drop in the stage.</param>
    /// <param name="hit">(Required) Point of the clay under the drop or of the last ghost, null beside the body.</param>
    public void DropAt(CreaturePartKind kind, Vector2 pixel, Vector3? hit)
    {
        if (TryGrowOnLimb(kind, pixel))
        {
            return;
        }
        if (null == hit || !PlaceAt(kind, hit.Value))
        {
            Log.Verbose(LOG_TAG, $"{kind} dropped where it does not fit -> not placed.");
        }
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
        CreatureDesign   draft = _session.Draft;
        CreaturePartInfo info  = CreatureCatalog.Get(kind);
        int              index = _stage.PartAt(pixel, out int flank);
        if (index < 0 || null == draft.Parts[index].Joints || !(info.IsSculpted || info.IsEnd))
        {
            return false;
        }
        if (info.IsEnd && draft.Parts[index].Info.IsSculpted)
        {
            return AttachEnd(index, kind);
        }
        return PlaceOnLimb(kind, index, _stage.NearestJoint(index, flank, pixel));
    }
}
