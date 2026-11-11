// NOTE:
//   Shared state of one cell editor session: the player, the draft of its design, the
//   points spent, the part selected in the palette and the placed part picked for the
//   gizmo. CellEditor creates one and hands it to every component (Setup). They read it
//   and reshape the draft through CellDesign, selection, pick and payment only change
//   through the methods here. No node, it only reads the Player.
//
//   var session = new CellEditSession();
//   session.Begin(player);
//   bool isPaid = session.TryPlace(kind, new Vector2(-1f, 0f));

//
//   Chapters:
//     SESSION STATE      Player, draft, selection, pick and the points left.
//     PLACE AND REMOVE   Pays for a part, refunds a removed one, commits the draft.
//

using Godot;

namespace CellOut;

public sealed class CellEditSession
{
    // ===========================================================================
    // ============================   SESSION STATE   ============================
    // ===========================================================================
    //
    //   Points       Evolution points left: the player's points minus what this session spent.
    //   Begin        Copies the player's design into a fresh draft, nothing spent or selected.
    //   Select       Picks a part from the palette, null for empty hands.
    //   PickPart     Picks a placed part for the gizmo, -1 for none.
    //   SetPartSize  Size of the picked part, free of charge, looks only.
    //   IsUnlocked  Whether stage and diet of the player offer a part.
    //   IsAvailable  Unlocked and affordable with the points left.
    //

    private int _spent;

    /// <summary>The player being edited, null before the first Begin.</summary>
    public Player Player { get; private set; }

    /// <summary>The design being edited, a copy of the player's until Commit.</summary>
    public CellDesign Draft { get; private set; } = new();

    /// <summary>The part in hand (palette tile), null for empty hands.</summary>
    public PartKind? Selected { get; private set; }

    /// <summary>Index in Draft.Parts of the part with the gizmo, -1 for none.</summary>
    public int PickedPart { get; private set; } = -1;

    public int Points => null == Player ? 0 : Player.EvolutionPoints - _spent;

    /// <summary>Starts a session for the player.</summary>
    /// <param name="player">(Required) The player whose design and points are edited.</param>
    public void Begin(Player player)
    {
        Player     = player;
        _spent     = 0;
        Selected   = null;
        PickedPart = -1;
        Draft      = new CellDesign();
        Draft.CopyFrom(player.Design);
    }

    /// <summary>Picks a part from the palette.</summary>
    /// <param name="kind">(Required) The part, null clears the selection.</param>
    public void Select(PartKind? kind)
    {
        Selected = kind;
    }

    /// <summary>Picks a placed part for the gizmo.</summary>
    /// <param name="index">(Required) Index in Draft.Parts, anything outside clears the pick.</param>
    public void PickPart(int index)
    {
        PickedPart = index >= 0 && index < Draft.Parts.Count ? index : -1;
    }

    /// <summary>Sets the size of the picked part.</summary>
    /// <param name="size">(Required) 1 = normal, CellDesign.MIN_PART_SIZE..MAX_PART_SIZE.</param>
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

    /// <summary>Whether stage and diet of the player offer the part.</summary>
    public bool IsUnlocked(PartInfo info)
    {
        return PartCatalog.IsUnlocked(info, Player.Stage, Player.Diet);
    }

    /// <summary>Whether the part is unlocked and the points left pay for it.</summary>
    public bool IsAvailable(PartInfo info)
    {
        return IsUnlocked(info) && info.Cost <= Points;
    }

    // ===========================================================================
    // ==========================   PLACE AND REMOVE   ===========================
    // ===========================================================================
    //
    //   TryPlace     Places a part if unlocked, affordable and free, pays and picks it.
    //   RemoveNear   Removes the part closest to a unit position and refunds it.
    //   RemovePart   Removes one placed part and refunds it.
    //   Refund       Gives the paid points back and clears the pick.
    //   Commit       Hands the draft and the points left to the player.
    //

    /// <summary>Places a part if it is unlocked, affordable and fits there. The new part gets the pick.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="point">(Required) Position in unit space (core radius 1, +X forward).</param>
    /// <param name="lift">(Optional) Height on the membrane for rim parts, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>True when the part was placed and paid.</returns>
    public bool TryPlace(PartKind kind, Vector2 point, float lift = 0f)
    {
        PartInfo info = PartCatalog.Get(kind);
        if (!IsUnlocked(info))
        {
            Log.Verbose("CellEditor", $"{kind} is locked at diet {Player.Diet:F2} -> not placed.");
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
        return true;
    }

    /// <summary>Removes the part closest to a unit position and refunds what was paid for it.</summary>
    /// <returns>The removed part, null when there was none.</returns>
    public PlacedPart? RemoveNear(Vector2 point)
    {
        PlacedPart? removed = Draft.RemoveNear(point);
        if (null == removed)
        {
            return null;
        }
        Refund(removed.Value);
        return removed;
    }

    /// <summary>Removes one placed part and refunds what was paid for it.</summary>
    /// <param name="index">(Required) Index in Draft.Parts.</param>
    /// <returns>The removed part, null for an index outside Draft.Parts.</returns>
    public PlacedPart? RemovePart(int index)
    {
        if (index < 0 || index >= Draft.Parts.Count)
        {
            return null;
        }
        PlacedPart removed = Draft.RemovePart(index);
        Refund(removed);
        return removed;
    }

    private void Refund(PlacedPart removed)
    {
        _spent    -= removed.PaidCost;
        PickedPart = -1;                             // indices moved
    }

    /// <summary>Commits the draft and the points left to the player.</summary>
    public void Commit()
    {
        Player.CommitDesign(Draft, Points);
    }
}
