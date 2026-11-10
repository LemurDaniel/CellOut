// NOTE:
//   One part placed on the body plan of an organism (CreatureDesign.Parts): its kind,
//   its share of the spine, the points paid and how the editor posed it. A value, never
//   changed in place: the design replaces it (part with { ... }).
//
//   var eye = new CreaturePart(CreaturePartKind.Eye, 0.85f, PaidCost: 1);
//   float weight = eye.Info.Weight * eye.Share;

//
//   Chapters:
//     PLACED PART   One creature part with its spine share and the points paid.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// =============================   PLACED PART   =============================
// ===========================================================================
//
//   CreaturePart   Kind, share of the spine (0 tail .. 1 nose), the points paid, size,
//                  side and the pose set with the gizmo of the editor.
//

/// <summary>One placed creature part. Paired parts (CreaturePartInfo.IsPaired) stand for a mirrored pair unless Side is set.</summary>
/// <param name="Size">Visual size, 1 = normal, MIN_PART_SIZE..MAX_PART_SIZE. Looks only, the stats ignore it.</param>
/// <param name="Side">0 for a pair or a midline part, 1 right (+Z) or -1 left for a single half of a paired part.</param>
/// <param name="Offset">Shift of the right half (or the whole midline part) in unit space, the left half mirrors it. Looks only.</param>
/// <param name="Rotation">Euler turn in radians around the anchor of the right half, the left half mirrors it. Looks only.</param>
/// <param name="Joints">Sculpted limbs only: joint points from the shoulder outwards, in the space of the right
/// limb (shoulder at the origin, +Z away from the body). Null for every other part. Never changed in place.</param>
/// <param name="Id">Stable id of the part in its design (indices shift on removal), 0 before it is placed.</param>
/// <param name="ParentId">A branch limb: id of the limb it grows from, 0 for a part on the body.</param>
/// <param name="ParentJoint">A branch limb: the joint of the parent it grows from (0 = first joint).</param>
public readonly record struct CreaturePart(
    CreaturePartKind Kind,
    float T,
    int PaidCost,
    float Size = 1f,
    int Side = 0,
    Vector3 Offset = default,
    Vector3 Rotation = default,
    IReadOnlyList<Vector3> Joints = null,
    int Id = 0,
    int ParentId = 0,
    int ParentJoint = 0)
{
    public CreaturePartInfo Info => CreatureCatalog.Get(Kind);

    /// <summary>1 for a pair or a midline part, 0.5 for a single half: weight and effects scale with it.</summary>
    public float Share => 0 == Side ? 1f : 0.5f;
}
