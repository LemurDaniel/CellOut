// NOTE:
//   The rules where a creature part may sit on a body plan: the side a part takes, the
//   snapped spot of a click, the part closest to a spine share, the branches growing
//   from a limb and where a part ends up when the spine grows at an end.
//   Pure functions over the parts of a design, without engine calls. CreatureDesign
//   owns the parts and asks here before it changes them.
//
//   if (PartPlacement.TrySnap(design.Parts, design.Type, CreaturePartKind.Eye, 0.85f, out float snapped))
//   {
//       // snapped is the share the eye would take
//   }

//
//   Chapters:
//     PLACE PARTS   Side, snapping, picking, branches and the remap of a growing spine.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public static class PartPlacement
{
    // ===========================================================================
    // =============================   PLACE PARTS   =============================
    // ===========================================================================
    //
    //   SideFor                 The side a part really takes.
    //   TrySnap                 Where a click on the body would place a part.
    //   IndexNear               The part closest to a spine share.
    //   BranchIndices           A part and every branch limb growing from it.
    //   ShareAfterSpineChange   The spine share a part takes when the spine grows or shrinks at an end.
    //

    public const float MIN_FREE_T = 0.1f;           // back and flank parts stay off the very ends
    public const float MAX_FREE_T = 0.92f;
    public const float MIN_PART_GAP = 0.14f;        // share of the spine between two parts on the same mount
    public const float REMOVE_DISTANCE = 0.18f;

    /// <summary>The side a part really takes: only paired parts can be a single half.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="side">(Required) Wanted side, 0 pair, -1 left, 1 right.</param>
    /// <example><code>PartPlacement.SideFor(CreaturePartKind.DorsalFin, 1)   // 0, a midline part</code></example>
    public static int SideFor(CreaturePartKind kind, int side)
    {
        return CreatureCatalog.Get(kind).IsPaired ? System.Math.Sign(side) : 0;
    }

    /// <summary>Where a click on the body would place the part.</summary>
    /// <param name="parts">(Required) The parts placed so far.</param>
    /// <param name="type">(Required) Body type of the design.</param>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Clicked share of the spine, 0 tail .. 1 nose.</param>
    /// <param name="snapped">The snapped share on success.</param>
    /// <param name="side">(Optional) 0 pair, -1 or 1 a single half of a paired part. Defaults to 0.</param>
    /// <returns>False when the body type cannot use the part or its spot is taken.</returns>
    public static bool TrySnap(IReadOnlyList<CreaturePart> parts, CreatureType type, CreaturePartKind kind, float t, out float snapped, int side = 0)
    {
        CreaturePartInfo info = CreatureCatalog.Get(kind);
        snapped = 0f;
        if (!info.IsFor(type))
        {
            return false;
        }

        switch (info.Mount)
        {
            // Tail, nose and skull: one part each
            case CreatureMount.Tail:
                return !parts.Any(part => CreatureMount.Tail == part.Info.Mount);
            case CreatureMount.Head:
            case CreatureMount.Skull:
                snapped = 1f;
                return !parts.Any(part => info.Mount == part.Info.Mount);
            // Back, flanks and inside: anywhere between the ends with a gap to parts on the same mount and side
            default:
                float spot     = Mathf.Clamp(t, MIN_FREE_T, MAX_FREE_T);
                int   resolved = SideFor(kind, side);
                snapped = spot;
                return !parts.Any(part => 0 == part.ParentId && info.Mount == part.Info.Mount
                    && Mathf.Abs(part.T - spot) < MIN_PART_GAP
                    && IsSideShared(resolved, part.Side));
        }
    }

    /// <summary>A pair covers both flanks, two single halves only clash on the same flank.</summary>
    private static bool IsSideShared(int side, int otherSide)
    {
        return 0 == side || 0 == otherSide || side == otherSide;
    }

    /// <summary>Index of the part closest to a spine share.</summary>
    /// <param name="parts">(Required) The placed parts.</param>
    /// <param name="t">(Required) Clicked share of the spine.</param>
    /// <returns>The index in parts, -1 when none is within REMOVE_DISTANCE.</returns>
    public static int IndexNear(IReadOnlyList<CreaturePart> parts, float t)
    {
        int   closest  = -1;
        float bestDist = REMOVE_DISTANCE;
        for (int i = 0; i < parts.Count; i++)
        {
            float distance = Mathf.Abs(parts[i].T - t);
            // Strictly closer wins, so ties keep the first part like before; the limit itself still counts
            if (distance < bestDist || (closest < 0 && distance <= bestDist))
            {
                bestDist = distance;
                closest  = i;
            }
        }
        return closest;
    }

    /// <summary>The part and all branches below it (children first found by parent id, any depth).</summary>
    /// <param name="parts">(Required) The placed parts.</param>
    /// <param name="index">(Required) Index in parts of the part the branches grow from.</param>
    /// <returns>The index itself first, then every branch.</returns>
    public static List<int> BranchIndices(IReadOnlyList<CreaturePart> parts, int index)
    {
        var indices = new List<int> { index };
        for (int k = 0; k < indices.Count; k++)
        {
            int id = parts[indices[k]].Id;
            for (int i = 0; i < parts.Count; i++)
            {
                if (0 != id && id == parts[i].ParentId && !indices.Contains(i))
                {
                    indices.Add(i);
                }
            }
        }
        return indices;
    }

    /*
      A spine that grows at an end (CreatureSpine.GrowEnd) remaps the parts by bone,
      s = T * (count - 1), so they keep their place on the body:
      > Nose grows (neck): parts in the front share (T >= HEAD_SHARE, eyes) ride with the
        head, s' = s + 1; all others stay on their bone, s' = s.
      > Tail grows: parts in the rear share (T <= TAIL_SHARE) stay with the tail end,
        s' = s; all others move with their bone, s' = s + 1.
      Shrinking runs the same with -1. Tail, head and skull parts keep 0 or 1.
    */
    private const float HEAD_SHARE = 0.7f;
    private const float TAIL_SHARE = 0.3f;

    /// <summary>The spine share that keeps a part in place when the spine grows or shrinks at an end.</summary>
    /// <param name="part">(Required) The placed part.</param>
    /// <param name="countBefore">(Required) Number of vertebrae before the change.</param>
    /// <param name="countAfter">(Required) Number of vertebrae after the change.</param>
    /// <param name="isNose">(Required) True when the front changed, false the tail.</param>
    /// <param name="change">(Required) 1 for a new vertebra, -1 for a removed one.</param>
    /// <returns>The new share, the old one for tail, head and skull parts.</returns>
    public static float ShareAfterSpineChange(CreaturePart part, int countBefore, int countAfter, bool isNose, int change)
    {
        if (part.Info.IsSingleSlot)
        {
            return part.T;
        }
        bool  isMoving = isNose ? part.T >= HEAD_SHARE : part.T > TAIL_SHARE;
        float bone     = part.T * (countBefore - 1) + (isMoving ? change : 0);
        return Mathf.Clamp(bone / (countAfter - 1), MIN_FREE_T, MAX_FREE_T);
    }
}
