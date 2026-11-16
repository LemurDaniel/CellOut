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
//   PartMotion     How a part moves on its own: on or off, pace, swing, offset in the cycle, blink.
//   CreaturePart   Kind, share of the spine (0 tail .. 1 nose), the points paid, size, stretch per axis,
//                  side and the pose set with the gizmo of the editor.
//

/// <summary>
/// The own motion of one part: the loop of its model (a fluttering fin, a chewing jaw), the swing of a limb,
/// the blink of an eye. Looks only.
/// </summary>
/// <param name="IsOn">False: the part stands still in its rest pose.</param>
/// <param name="Speed">Factor on the pace of its motion, MIN_SPEED..MAX_SPEED.</param>
/// <param name="Amount">Limbs: factor on how far they swing, 0..MAX_AMOUNT. Model loops ignore it.</param>
/// <param name="Phase">Where in its cycle the part starts, 0..1: two fins half a beat apart.</param>
/// <param name="IsBlinking">Eyes: false keeps them open for good.</param>
public readonly record struct PartMotion(bool IsOn, float Speed, float Amount, float Phase, bool IsBlinking)
{
    public const float MIN_SPEED = 0.1f;
    public const float MAX_SPEED = 3f;
    public const float MAX_AMOUNT = 2.5f;

    /// <summary>The motion of a part nobody touched: on, plain pace and swing, no offset, blinking.</summary>
    public static readonly PartMotion REST = new(true, 1f, 1f, 0f, true);

    /// <summary>The same motion with every value inside its range.</summary>
    public PartMotion Clamped()
    {
        return this with
        {
            Speed  = Mathf.Clamp(Speed, MIN_SPEED, MAX_SPEED),
            Amount = Mathf.Clamp(Amount, 0f, MAX_AMOUNT),
            Phase  = Mathf.PosMod(Phase, 1f),
        };
    }
}

/// <summary>One placed creature part. Paired parts (CreaturePartInfo.IsPaired) stand for a mirrored pair unless Side is set.</summary>
/// <param name="Size">Visual size, 1 = normal, MIN_PART_SIZE..MAX_PART_SIZE. Looks only, the stats ignore it.</param>
/// <param name="Stretch">Extra factor per axis on top of Size (x along the body, y up, z out of the flank, before the turn),
/// null = unstretched. Read it through Proportion. MIN_PART_STRETCH..MAX_PART_STRETCH per axis. Looks only.</param>
/// <param name="Widths">Sculpted limbs only: thickness factor per point of the path (shoulder, then every joint), null = the plain taper of the limb kind. Never changed in place.</param>
/// <param name="IsCentered">A limb that grows from the middle of the body instead of a flank (a neck, a trunk, a tail), or a part on a limb joint that sits on the axis of the limb instead of beside it: one piece, no mirror half.</param>
/// <param name="Radial">Point symmetry: the part stands this many times around the spine, evenly spread (2 to CreatureDesign.MAX_RADIAL), 0 for none. A paired kind is a single half then (Side 1).</param>
/// <param name="LimbKind">A foot or hand on a sculpted limb: the limb kind it was stuck on (its tube keeps that look), else null.</param>
/// <param name="Motion">How the part moves on its own (its model loop, the swing of a limb, the blink of an eye), null = as built. Read it through Movement. Looks only.</param>
/// <param name="Channels">Strength per channel of the model animation (a bone or node it moves, see Core/ModelChannels.cs): 0 off, 1 as built,
/// channels left out are 1; null = all as built. Never changed in place. Looks only.</param>
/// <param name="TipColor">Sculpted limbs: a second plain color from the joint TipFrom outwards, foot or hand included (white lower legs), null = none. Looks only.</param>
/// <param name="TipFrom">The joint the TipColor begins at: 1 the first joint .. Joints.Count the tip.</param>
/// <param name="ShapeId">A CustomPart: the id of the shape the player sculpted for it (CreatureDesign.Shapes), 0 for every other kind.</param>
/// <param name="Tint">A color of its own for this part instead of the body color (its skin, fins and limb tube), null = the color of the body. Looks only.</param>
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
    int ParentJoint = 0,
    Vector3? Stretch = null,
    CreaturePartKind? LimbKind = null,
    int Radial = 0,
    IReadOnlyList<float> Widths = null,
    bool IsCentered = false,
    Color? Tint = null,
    PartMotion? Motion = null,
    IReadOnlyDictionary<string, float> Channels = null,
    Color? TipColor = null,
    int TipFrom = 0,
    int ShapeId = 0)
{
    /// <summary>Thickness factor of a limb at one point of its path: 0 the shoulder, 1 the first joint .. Joints.Count the tip. 1 without Widths.</summary>
    public float WidthAt(int point)
    {
        return null != Widths && point >= 0 && point < Widths.Count ? Widths[point] : 1f;
    }

    /// <summary>The look of the limb tube: the limb kind a foot or hand was stuck on, else the kind itself.</summary>
    public CreaturePartKind LimbStyle => LimbKind ?? Kind;

    /// <summary>How the part moves on its own, PartMotion.REST (everything on, plain pace) without a setting.</summary>
    public PartMotion Movement => Motion ?? PartMotion.REST;

    /// <summary>Strength of one channel of the model animation, 1 without a setting.</summary>
    public float ChannelAmount(string channel)
    {
        return null != Channels && Channels.TryGetValue(channel, out float amount) ? amount : 1f;
    }

    /// <summary>The stretch per axis, (1, 1, 1) for an unstretched part.</summary>
    public Vector3 Proportion => Stretch ?? Vector3.One;

    public CreaturePartInfo Info => CreatureCatalog.Get(Kind);

    /// <summary>How many times the part stands around the spine: Radial for point symmetry, else 1.</summary>
    public int Copies => Radial >= 2 ? Radial : 1;

    /// <summary>1 for a pair or a midline part, 0.5 for a single half, times the copies of point symmetry: weight and effects scale with it.</summary>
    public float Share => (0 == Side ? 1f : 0.5f) * Copies;
}
