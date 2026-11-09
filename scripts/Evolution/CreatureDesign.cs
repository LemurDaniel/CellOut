// NOTE:
//   The body plan of an organism: a spine of vertebrae, each with its own girth, and
//   the parts placed along it. Positions on the body are a share T of the spine,
//   0 = tail end, 1 = nose. Unit space like every body: +X forward, Y up, the contact
//   circle of the body has radius 1. Also answers the geometry questions of the
//   creature (profile radius, inside test), so visual, editor and stats agree.
//   Edited by the CreatureEditor on a copy, committed to the Player when it closes.
//
//   var design = CreatureDesign.Blob();
//   design.Type = CreatureType.Fish;
//   design.TryPlace(CreaturePartKind.CaudalFin, 0f, paidCost: 2);

//
//   Chapters:
//     PLACED PART       One creature part with its spine share and the points paid.
//     PAINT PATTERN     The patterns the second color is painted in, and spray paint dots.
//     CREATURE DESIGN   Spine, parts, geometry, look, copies and the ready-made body plans.
//

using System.Collections.Generic;
using System.Linq;
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

// ===========================================================================
// ============================   PAINT PATTERN   ============================
// ===========================================================================
//
//   CreaturePattern   How the second color lies on the back and flanks (shaders/clay.gdshaderinc).
//   PaintSplat        One dot of spray paint, stored relative to the spine so it follows reshaping.
//

/// <summary>Paint of the second color: none, stripes across, round spots, big patches or a dark saddle.</summary>
public enum CreaturePattern { None, Stripes, Spots, Patches, Saddle }

/// <summary>One dot of spray paint on the body.</summary>
/// <param name="T">Spine share of its center, 0 tail .. 1 nose (outside on the caps).</param>
/// <param name="Angle">Angle around the ring: 0 right flank (+Z), Pi/2 back, -Pi/2 belly.</param>
/// <param name="Radius">Size of the dot in unit space.</param>
/// <param name="Color">Paint color.</param>
/// <param name="Stroke">Stroke the dot belongs to, undo removes the last stroke.</param>
public readonly record struct PaintSplat(float T, float Angle, float Radius, Color Color, int Stroke);

public sealed class CreatureDesign
{
    // ===========================================================================
    // ===========================   CREATURE DESIGN   ===========================
    // ===========================================================================
    //
    //   Spine        Body type, position, girth and height per vertebra, adding vertebrae in the middle or at the ends.
    //   Parts        Placement rules, sides, picking, removal, sizes, poses and counts.
    //   Geometry     Length, profile radius, spine height and slope, cross section, volume and the inside test.
    //   Look         Custom body color, the paint pattern and its color, spray paint.
    //   Copies       Copy from another design.
    //   Body plans   The clay blob, the starter bodies, the type examples and the NPC creatures.
    //

    // ---------------------------------------------------------------------------
    // Spine
    //   Body type, position, girth and height per vertebra, adding vertebrae in the middle or at the ends.
    // ---------------------------------------------------------------------------

    public const int MIN_VERTEBRAE = 4;
    public const int MAX_VERTEBRAE = 12;
    public const float MIN_GIRTH = 0.25f;
    public const float MAX_GIRTH = 1f;
    public const float MAX_LIFT = 1.1f;             // how far a vertebra can be pulled above or below the line
    private const float MAX_LIFT_STEP = 0.32f;      // height step between two neighbors, the spine stays a smooth curve

    private readonly List<float> _girths = new();
    private readonly List<float> _lifts = new();
    private readonly List<float> _xs = new();       // position of every vertebra along the body, tail first, rising

    /// <summary>Fish swim side to side, marine mammals up and down.</summary>
    public CreatureType Type { get; set; } = CreatureType.Fish;

    /// <summary>Girth per vertebra, tail first, MIN_GIRTH..MAX_GIRTH.</summary>
    public IReadOnlyList<float> Girths => _girths;

    /// <summary>Counts every change, the 3D visual rebuilds on it.</summary>
    public int Version { get; private set; }

    public float MeanGirth => _girths.Count > 0 ? _girths.Average() : 0f;

    /// <summary>Sets the girth of one vertebra, clamped.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="girth">(Required) New girth, clamped to MIN_GIRTH..MAX_GIRTH.</param>
    public void SetGirth(int index, float girth)
    {
        if (index < 0 || index >= _girths.Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {_girths.Count - 1}, got '{index}'.");
        }
        _girths[index] = Mathf.Clamp(girth, MIN_GIRTH, MAX_GIRTH);
        Version++;
    }

    /// <summary>Height of every vertebra above the straight line, tail first, -MAX_LIFT..MAX_LIFT.</summary>
    public IReadOnlyList<float> Lifts => _lifts;

    /*
      Bending the spine: the dragged vertebra takes the new height, its neighbors follow
      outwards as far as needed so no step between two of them exceeds MAX_LIFT_STEP.
      Pulling the nose up raises a neck, dropping the tail end lets the tail hang.
    */
    /// <summary>Lifts one vertebra, the neighbors follow smoothly.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="lift">(Required) Height above the straight line, clamped to -MAX_LIFT..MAX_LIFT.</param>
    /// <example><code>design.SetLift(design.Girths.Count - 1, 0.6f);   // a neck</code></example>
    public void SetLift(int index, float lift)
    {
        if (index < 0 || index >= _lifts.Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {_lifts.Count - 1}, got '{index}'.");
        }
        _lifts[index] = Mathf.Clamp(lift, -MAX_LIFT, MAX_LIFT);
        for (int i = index + 1; i < _lifts.Count; i++)
        {
            _lifts[i] = Mathf.Clamp(_lifts[i], _lifts[i - 1] - MAX_LIFT_STEP, _lifts[i - 1] + MAX_LIFT_STEP);
        }
        for (int i = index - 1; i >= 0; i--)
        {
            _lifts[i] = Mathf.Clamp(_lifts[i], _lifts[i + 1] - MAX_LIFT_STEP, _lifts[i + 1] + MAX_LIFT_STEP);
        }
        Version++;
    }

    /*
      Vertebrae can be pushed along the body: a short neck and a long trunk. Each one keeps
      MIN_GAP to its neighbors, inner ones stay between them, the ends at most MAX_GAP away.
      The body re-centers only when vertebrae are added or removed (Recenter), so the gizmo
      stays on the dragged vertebra.
    */
    public const float MIN_GAP = SPACING * 0.45f;
    public const float MAX_GAP = SPACING * 1.8f;

    /// <summary>Position of every vertebra along the body (X), tail first.</summary>
    public IReadOnlyList<float> Positions => _xs;

    /// <summary>Moves one vertebra along the body, clamped between its neighbors.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="x">(Required) Wanted X in unit space.</param>
    /// <returns>The X the vertebra took.</returns>
    /// <example><code>design.SetVertebraX(2, design.Positions[2] + 0.1f);</code></example>
    public float SetVertebraX(int index, float x)
    {
        if (index < 0 || index >= _xs.Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {_xs.Count - 1}, got '{index}'.");
        }
        // Between the neighbors with MIN_GAP, an end at most MAX_GAP from its only neighbor
        float low  = 0 == index ? _xs[1] - MAX_GAP : _xs[index - 1] + MIN_GAP;
        float high = _xs.Count - 1 == index ? _xs[^2] + MAX_GAP : _xs[index + 1] - MIN_GAP;
        _xs[index] = Mathf.Clamp(x, low, high);
        Version++;
        return _xs[index];
    }

    /// <summary>Shifts every vertebra so the spine is centered on the origin again.</summary>
    private void Recenter()
    {
        float center = (_xs[0] + _xs[^1]) * 0.5f;
        for (int i = 0; i < _xs.Count; i++)
        {
            _xs[i] -= center;
        }
    }

    /// <summary>Inserts a vertebra in the middle, as thick and high as its neighbors. Parts keep their share.</summary>
    /// <returns>False at MAX_VERTEBRAE.</returns>
    public bool AddVertebra()
    {
        if (_girths.Count >= MAX_VERTEBRAE)
        {
            return false;
        }
        // The halves move apart by half a spacing each, the new vertebra sits in the middle
        int middle = _girths.Count / 2;
        _girths.Insert(middle, (_girths[middle - 1] + _girths[middle]) * 0.5f);
        _lifts.Insert(middle, (_lifts[middle - 1] + _lifts[middle]) * 0.5f);
        for (int i = 0; i < _xs.Count; i++)
        {
            _xs[i] += i < middle ? -SPACING * 0.5f : SPACING * 0.5f;
        }
        _xs.Insert(middle, (_xs[middle - 1] + _xs[middle]) * 0.5f);
        Version++;
        return true;
    }

    /// <summary>Removes the middle vertebra.</summary>
    /// <returns>False at MIN_VERTEBRAE.</returns>
    public bool RemoveVertebra()
    {
        if (_girths.Count <= MIN_VERTEBRAE)
        {
            return false;
        }
        int middle = _girths.Count / 2;
        _girths.RemoveAt(middle);
        _lifts.RemoveAt(middle);
        _xs.RemoveAt(middle);
        for (int i = 0; i < _xs.Count; i++)
        {
            _xs[i] += i < middle ? SPACING * 0.5f : -SPACING * 0.5f;
        }
        Version++;
        return true;
    }

    /*
      Pulling an end out grows the body there: a new vertebra continues the end (slightly
      thinner, the height continues the curve). The parts are remapped by bone,
      s = T * (count - 1), so they keep their place on the body:
      > Nose grows (neck): parts in the front share (T >= HEAD_SHARE, eyes) ride with the
        head, s' = s + 1; all others stay on their bone, s' = s.
      > Tail grows: parts in the rear share (T <= TAIL_SHARE) stay with the tail end,
        s' = s; all others move with their bone, s' = s + 1.
      Shrinking runs the same with -1. Tail, head and skull parts keep 0 or 1.
    */
    private const float HEAD_SHARE = 0.7f;
    private const float TAIL_SHARE = 0.3f;
    /// <summary>Adds a vertebra at the nose or the tail end.</summary>
    /// <param name="isNose">(Required) True grows the front (a neck), false the tail.</param>
    /// <returns>False at MAX_VERTEBRAE.</returns>
    /// <example><code>design.GrowEnd(isNose: true);</code></example>
    public bool GrowEnd(bool isNose)
    {
        if (_girths.Count >= MAX_VERTEBRAE)
        {
            return false;
        }
        int   count = _girths.Count;
        int   end   = isNose ? count - 1 : 0;
        int   next  = isNose ? count - 2 : 1;
        float girth = Mathf.Clamp(_girths[end] * 0.92f, MIN_GIRTH, MAX_GIRTH);
        float lift  = Mathf.Clamp(_lifts[end] + (_lifts[end] - _lifts[next]), -MAX_LIFT, MAX_LIFT);
        _girths.Insert(isNose ? count : 0, girth);
        _lifts.Insert(isNose ? count : 0, lift);
        _xs.Insert(isNose ? count : 0, isNose ? _xs[^1] + SPACING : _xs[0] - SPACING);
        Recenter();
        RemapParts(count, count + 1, isNose, 1);
        Version++;
        return true;
    }

    /// <summary>Removes the vertebra at the nose or the tail end.</summary>
    /// <param name="isNose">(Required) True shortens the front, false the tail.</param>
    /// <returns>False at MIN_VERTEBRAE.</returns>
    public bool ShrinkEnd(bool isNose)
    {
        if (_girths.Count <= MIN_VERTEBRAE)
        {
            return false;
        }
        int count = _girths.Count;
        _girths.RemoveAt(isNose ? count - 1 : 0);
        _lifts.RemoveAt(isNose ? count - 1 : 0);
        _xs.RemoveAt(isNose ? count - 1 : 0);
        Recenter();
        RemapParts(count, count - 1, isNose, -1);
        Version++;
        return true;
    }

    /// <summary>Keeps the free parts in place when the spine grows or shrinks at an end, see GrowEnd.</summary>
    /// <param name="change">(Required) 1 for a new vertebra, -1 for a removed one.</param>
    private void RemapParts(int countBefore, int countAfter, bool isNose, int change)
    {
        for (int i = 0; i < _parts.Count; i++)
        {
            CreaturePart part = _parts[i];
            if (part.Info.IsSingleSlot)
            {
                continue;
            }
            bool  isMoving = isNose ? part.T >= HEAD_SHARE : part.T > TAIL_SHARE;
            float bone     = part.T * (countBefore - 1) + (isMoving ? change : 0);
            _parts[i] = part with { T = Mathf.Clamp(bone / (countAfter - 1), MIN_FREE_T, MAX_FREE_T) };
        }
    }

    // ---------------------------------------------------------------------------
    // Parts
    //   Placement rules, sides, picking, removal, sizes, poses and counts.
    // ---------------------------------------------------------------------------

    private const float MIN_FREE_T = 0.1f;          // back and flank parts stay off the very ends
    private const float MAX_FREE_T = 0.92f;
    private const float MIN_PART_GAP = 0.14f;       // share of the spine between two parts on the same mount
    private const float REMOVE_DISTANCE = 0.18f;

    private readonly List<CreaturePart> _parts = new();
    private int _lastId;                             // ids of placed parts, never reused in a design

    public IReadOnlyList<CreaturePart> Parts => _parts;

    /// <summary>The side a part really takes: only paired parts can be a single half.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="side">(Required) Wanted side, 0 pair, -1 left, 1 right.</param>
    public static int SideFor(CreaturePartKind kind, int side)
    {
        return CreatureCatalog.Get(kind).IsPaired ? System.Math.Sign(side) : 0;
    }

    /// <summary>Where a click on the body would place the part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Clicked share of the spine, 0 tail .. 1 nose.</param>
    /// <param name="snapped">The snapped share on success.</param>
    /// <param name="side">(Optional) 0 pair, -1 or 1 a single half of a paired part. Defaults to 0.</param>
    /// <returns>False when the body type cannot use the part or its spot is taken.</returns>
    public bool TrySnap(CreaturePartKind kind, float t, out float snapped, int side = 0)
    {
        CreaturePartInfo info = CreatureCatalog.Get(kind);
        snapped = 0f;
        if (!info.IsFor(Type))
        {
            return false;
        }

        switch (info.Mount)
        {
            // Tail, nose and skull: one part each
            case CreatureMount.Tail:
                return !_parts.Any(part => CreatureMount.Tail == part.Info.Mount);
            case CreatureMount.Head:
            case CreatureMount.Skull:
                snapped = 1f;
                return !_parts.Any(part => info.Mount == part.Info.Mount);
            // Back, flanks and inside: anywhere between the ends with a gap to parts on the same mount and side
            default:
                float spot     = Mathf.Clamp(t, MIN_FREE_T, MAX_FREE_T);
                int   resolved = SideFor(kind, side);
                snapped = spot;
                return !_parts.Any(part => 0 == part.ParentId && info.Mount == part.Info.Mount
                    && Mathf.Abs(part.T - spot) < MIN_PART_GAP
                    && IsSideShared(resolved, part.Side));
        }
    }

    /// <summary>A pair covers both flanks, two single halves only clash on the same flank.</summary>
    private static bool IsSideShared(int side, int otherSide)
    {
        return 0 == side || 0 == otherSide || side == otherSide;
    }

    /// <summary>Snaps and adds the part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Clicked share of the spine.</param>
    /// <param name="paidCost">(Required) Points paid, refunded on removal.</param>
    /// <param name="side">(Optional) 0 pair, -1 or 1 a single half of a paired part. Defaults to 0.</param>
    /// <returns>True when the part was placed.</returns>
    /// <example><code>design.TryPlace(CreaturePartKind.Eye, 0.85f, paidCost: 1)</code></example>
    public bool TryPlace(CreaturePartKind kind, float t, int paidCost, int side = 0)
    {
        if (!TrySnap(kind, t, out float snapped, side))
        {
            return false;
        }
        IReadOnlyList<Vector3> joints = DefaultJoints(kind)?.ToArray();
        var part = new CreaturePart(kind, snapped, paidCost, Side: SideFor(kind, side), Joints: joints, Id: ++_lastId);
        if (CreatureCatalog.Get(kind).IsLeg)
        {
            part = part with { Joints = StretchToGround(part) };
        }
        _parts.Add(part);
        Version++;
        return true;
    }

    /*
      Branches: a sculpted limb (or a foot or hand, it brings its limb) grows from a joint
      of a placed limb instead of the body. It takes the side of its parent, its own joints
      start at that joint. It ignores the gaps on the body (TrySnap) and goes with its
      parent: RemovePart takes every branch along.
    */
    /// <summary>Places a limb on a joint of a placed limb (a branch).</summary>
    /// <param name="kind">(Required) A sculpted limb, a foot or a hand.</param>
    /// <param name="parentIndex">(Required) Index in Parts of a limb with joints.</param>
    /// <param name="joint">(Required) Joint of the parent, 0 = first joint .. Joints.Count - 1 = tip.</param>
    /// <param name="paidCost">(Required) Points paid, refunded on removal.</param>
    /// <returns>False when the kind grows no limb or the parent has no such joint.</returns>
    /// <example><code>design.TryPlaceOnLimb(CreaturePartKind.Tentacle, arm, 1, paidCost: 2);   // a tentacle from the elbow</code></example>
    public bool TryPlaceOnLimb(CreaturePartKind kind, int parentIndex, int joint, int paidCost)
    {
        if (null == DefaultJoints(kind) || parentIndex < 0 || parentIndex >= _parts.Count)
        {
            return false;
        }
        CreaturePart parent = _parts[parentIndex];
        if (null == parent.Joints || joint < 0 || joint >= parent.Joints.Count)
        {
            return false;
        }
        var part = new CreaturePart(kind, parent.T, paidCost, Side: parent.Side, Joints: DefaultJoints(kind).ToArray(),
            Id: ++_lastId, ParentId: parent.Id, ParentJoint: joint);
        if (CreatureCatalog.Get(kind).IsLeg)
        {
            part = part with { Joints = StretchToGround(part) };
        }
        _parts.Add(part);
        Version++;
        return true;
    }

    /// <summary>Index in Parts of the part with an id, -1 when it is gone.</summary>
    public int IndexOfId(int id)
    {
        return 0 == id ? -1 : _parts.FindIndex(part => id == part.Id);
    }

    /// <summary>Points paid for a part and every branch growing from it, the refund of RemovePart.</summary>
    public int BranchCost(int index)
    {
        return BranchIndices(index).Sum(i => _parts[i].PaidCost);
    }

    /// <summary>The part and all branches below it (children first found by parent id, any depth).</summary>
    private List<int> BranchIndices(int index)
    {
        var indices = new List<int> { index };
        for (int k = 0; k < indices.Count; k++)
        {
            int id = _parts[indices[k]].Id;
            for (int i = 0; i < _parts.Count; i++)
            {
                if (0 != id && id == _parts[i].ParentId && !indices.Contains(i))
                {
                    indices.Add(i);
                }
            }
        }
        return indices;
    }

    /*
      A fresh leg reaches the ground wherever it grows: on a lifted part of the body (a
      neck, a hump) its joints stretch down, under a low belly they shrink. Y of every
      joint scales by the same factor, so the shape stays; the tip lands LEG_TIP_Y below
      the body center (the foot sole touches the coast with the body lifted on land).
    */
    private const float LEG_TIP_Y = -0.7f;
    private const float MIN_LEG_STRETCH = 0.5f;
    private const float MAX_LEG_STRETCH = 2.5f;

    private IReadOnlyList<Vector3> StretchToGround(CreaturePart part)
    {
        float   rootY   = LimbRoot(part).Y;
        float   tipY    = part.Joints[^1].Y;
        float   stretch = tipY < -0.01f ? Mathf.Clamp((LEG_TIP_Y - rootY) / tipY, MIN_LEG_STRETCH, MAX_LEG_STRETCH) : 1f;
        return part.Joints.Select(joint => new Vector3(joint.X, joint.Y * stretch, joint.Z)).ToArray();
    }

    /*
      Sculpted limbs: a chain of joints pulled out of the flank, one kind per joint count.
      Each segment stays MIN..MAX_LIMB_SEGMENT long.

        shoulder (flank) --o-- knee --o-- ankle --o tip
      - StubLimb (1):  a short nub sticking out sideways, pulled down a straight pole leg
      - BentLimb (2):  one knee, hangs to the ground like a leg
      - Limb (3):      knee and ankle, hangs to the ground like a leg
      - Tentacle (4):  curls out and down, the tip bends back up
      - Feet:          a leg of three joints, the foot model on the tip touches the ground
                       of the coast with the body lifted (CreatureVisual3D.LAND_LIFT)
      - Hands:         an arm of three joints reaching forward, held above the ground
      A fresh leg-like limb already carries on land; pulled up and forward it becomes an arm.
    */
    // Before DEFAULT_JOINTS: static fields initialize in order, it reads them
    private static readonly Vector3[] LEG_JOINTS = { new(0.04f, -0.12f, 0.26f), new(0.1f, -0.36f, 0.34f), new(0.12f, -0.6f, 0.34f) };
    private static readonly Vector3[] ARM_JOINTS = { new(0.1f, -0.08f, 0.26f), new(0.26f, -0.18f, 0.34f), new(0.42f, -0.16f, 0.34f) };
    private static readonly IReadOnlyDictionary<CreaturePartKind, Vector3[]> DEFAULT_JOINTS = new Dictionary<CreaturePartKind, Vector3[]>
    {
        [CreaturePartKind.StubLimb] = new Vector3[] { new(0.08f, -0.18f, 0.34f) },
        [CreaturePartKind.BentLimb] = new Vector3[] { new(0.06f, -0.34f, 0.32f), new(0.16f, -0.82f, 0.38f) },
        [CreaturePartKind.Limb]     = new Vector3[] { new(0.04f, -0.12f, 0.28f), new(0.12f, -0.46f, 0.42f), new(0.2f, -0.82f, 0.42f) },
        [CreaturePartKind.Tentacle] = new Vector3[] { new(0.06f, -0.16f, 0.3f), new(0.2f, -0.42f, 0.48f), new(0.36f, -0.66f, 0.46f), new(0.52f, -0.6f, 0.3f) },
        [CreaturePartKind.Legs]       = LEG_JOINTS,
        [CreaturePartKind.StubbyLegs] = LEG_JOINTS,
        [CreaturePartKind.ClawLegs]   = LEG_JOINTS,
        [CreaturePartKind.CrabLegs]   = LEG_JOINTS,
        [CreaturePartKind.Hands]      = ARM_JOINTS,
        [CreaturePartKind.Claws]      = ARM_JOINTS,
    };

    /// <summary>
    /// Sticks a foot or a hand on the tip of a placed sculpted limb: the part becomes that
    /// end kind and keeps joints, spot, side and pose, the points add up.
    /// </summary>
    /// <param name="index">(Required) Index in Parts, a sculpted limb without an end.</param>
    /// <param name="end">(Required) A foot or hand kind (CreaturePartInfo.IsEnd).</param>
    /// <param name="paidCost">(Required) Points paid for the end, refunded with the limb.</param>
    /// <returns>False when the part is no bare sculpted limb or the end is no foot or hand.</returns>
    /// <example><code>design.AttachEnd(limb, CreaturePartKind.Legs, paidCost: 1);   // a paw on the limb</code></example>
    public bool AttachEnd(int index, CreaturePartKind end, int paidCost)
    {
        if (index < 0 || index >= _parts.Count || !_parts[index].Info.IsSculpted || !CreatureCatalog.Get(end).IsEnd)
        {
            return false;
        }
        _parts[index] = _parts[index] with { Kind = end, PaidCost = _parts[index].PaidCost + paidCost };
        Version++;
        return true;
    }

    /// <summary>The joints a sculpted limb starts with, null for every other part.</summary>
    /// <example><code>CreatureDesign.DefaultJoints(CreaturePartKind.Tentacle).Count   // 4</code></example>
    public static IReadOnlyList<Vector3> DefaultJoints(CreaturePartKind kind)
    {
        return DEFAULT_JOINTS.TryGetValue(kind, out Vector3[] joints) ? joints : null;
    }
    public const float MIN_LIMB_SEGMENT = 0.08f;
    public const float MAX_LIMB_SEGMENT = 0.75f;
    private const float WALK_REACH = 0.1f;          // a limb tip this close above the ground counts as a foot

    /// <summary>Moves one joint of a sculpted limb, the joints further out follow by the same shift.</summary>
    /// <param name="index">(Required) Index in Parts, a Limb.</param>
    /// <param name="joint">(Required) Joint from the shoulder outwards, 0..Joints.Count - 1.</param>
    /// <param name="point">(Required) New position in the space of the right limb, the segment length is clamped.</param>
    /// <example><code>design.SetLimbJoint(0, 2, new Vector3(0.3f, 0.2f, 0.4f));   // the tip reaches forward like an arm</code></example>
    public void SetLimbJoint(int index, int joint, Vector3 point)
    {
        if (index < 0 || index >= _parts.Count || null == _parts[index].Joints)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must point at a placed sculpted limb, got '{index}'.");
        }
        Vector3[] joints = _parts[index].Joints.ToArray();
        if (joint < 0 || joint >= joints.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(joint), $"joint must be between 0 and {joints.Length - 1}, got '{joint}'.");
        }
        Vector3 previous = 0 == joint ? Vector3.Zero : joints[joint - 1];
        Vector3 segment  = point - previous;
        float   length   = Mathf.Clamp(segment.Length(), MIN_LIMB_SEGMENT, MAX_LIMB_SEGMENT);
        Vector3 moved    = previous + (segment.LengthSquared() > 1e-8f ? segment.Normalized() : Vector3.Back) * length;
        Vector3 shift    = moved - joints[joint];
        for (int i = joint; i < joints.Length; i++)
        {
            joints[i] += shift;
        }
        _parts[index] = _parts[index] with { Joints = joints };
        Version++;
    }

    /// <summary>The shoulder of the right limb in unit space: on the lower flank at the spine share, a branch at its parent's joint.</summary>
    public Vector3 LimbRoot(CreaturePart part)
    {
        // A branch: the parent's joint, with its shift and size (the turn of the parent ignored)
        int parentIndex = IndexOfId(part.ParentId);
        if (parentIndex >= 0)
        {
            CreaturePart parent = _parts[parentIndex];
            return LimbRoot(parent) + parent.Offset + parent.Joints[Mathf.Clamp(part.ParentJoint, 0, parent.Joints.Count - 1)] * parent.Size;
        }
        float x = XAt(part.T);
        return new Vector3(x, LiftAt(x) - TopAt(x) * 0.2f, FlankAt(x) * 0.85f);
    }

    /// <summary>
    /// Whether a sculpted limb reaches the ground of the coast (Coast.GROUND_HEIGHT below the
    /// body center) with its tip: then it walks and carries. Size and shift count, the turn not.
    /// </summary>
    public bool IsWalkingLimb(CreaturePart part)
    {
        if (null == part.Joints || 0 == part.Joints.Count)
        {
            return false;
        }
        float tipY = LimbRoot(part).Y + part.Offset.Y + part.Joints[^1].Y * part.Size;
        return tipY <= Coast.GROUND_HEIGHT + WALK_REACH;
    }

    /// <summary>Index of the part closest to a spine share.</summary>
    /// <param name="t">(Required) Clicked share of the spine.</param>
    /// <returns>The index in Parts, -1 when none is close enough.</returns>
    public int IndexNear(float t)
    {
        int   closest  = -1;
        float bestDist = REMOVE_DISTANCE;
        for (int i = 0; i < _parts.Count; i++)
        {
            float distance = Mathf.Abs(_parts[i].T - t);
            // Strictly closer wins, so ties keep the first part like before; the limit itself still counts
            if (distance < bestDist || (closest < 0 && distance <= bestDist))
            {
                bestDist = distance;
                closest  = i;
            }
        }
        return closest;
    }

    /// <summary>Removes the part closest to a spine share.</summary>
    /// <param name="t">(Required) Clicked share of the spine.</param>
    /// <returns>The removed part for the refund, null when none was close enough.</returns>
    public CreaturePart? RemoveNear(float t)
    {
        int index = IndexNear(t);
        return index < 0 ? null : RemovePart(index);
    }

    /// <summary>Removes one placed part and every branch limb growing from it (BranchCost is the refund).</summary>
    /// <param name="index">(Required) Index in Parts.</param>
    /// <returns>The removed part for the refund.</returns>
    public CreaturePart RemovePart(int index)
    {
        if (index < 0 || index >= _parts.Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {_parts.Count - 1}, got '{index}'.");
        }
        // The part and every branch growing from it, from the back so the indices stay valid
        CreaturePart removed = _parts[index];
        foreach (int i in BranchIndices(index).OrderByDescending(i => i))
        {
            _parts.RemoveAt(i);
        }
        Version++;
        return removed;
    }

    public const float MAX_PART_OFFSET = 0.8f;      // unit space, the gizmo cannot pull a part off the body

    /// <summary>Sets shift and turn of one placed part (gizmo of the editor), the shift clamped to MAX_PART_OFFSET.</summary>
    /// <param name="index">(Required) Index in Parts.</param>
    /// <param name="offset">(Required) Shift of the right half or the midline part in unit space.</param>
    /// <param name="rotation">(Required) Euler turn in radians.</param>
    /// <example><code>design.SetPartPose(0, new Vector3(0f, 0.1f, 0f), Vector3.Zero);</code></example>
    public void SetPartPose(int index, Vector3 offset, Vector3 rotation)
    {
        if (index < 0 || index >= _parts.Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {_parts.Count - 1}, got '{index}'.");
        }
        _parts[index] = _parts[index] with { Offset = offset.LimitLength(MAX_PART_OFFSET), Rotation = rotation };
        Version++;
    }

    public const float MIN_PART_SIZE = 0.6f;
    public const float MAX_PART_SIZE = 3f;

    /// <summary>Sets the visual size of one placed part, clamped.</summary>
    /// <param name="index">(Required) Index in Parts.</param>
    /// <param name="size">(Required) 1 = normal, clamped to MIN_PART_SIZE..MAX_PART_SIZE.</param>
    /// <example><code>design.SetPartSize(0, 1.3f);</code></example>
    public void SetPartSize(int index, float size)
    {
        if (index < 0 || index >= _parts.Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {_parts.Count - 1}, got '{index}'.");
        }
        _parts[index] = _parts[index] with { Size = Mathf.Clamp(size, MIN_PART_SIZE, MAX_PART_SIZE) };
        Version++;
    }

    /// <summary>Number of placed parts of one kind.</summary>
    public int Count(CreaturePartKind kind)
    {
        return _parts.Count(part => kind == part.Kind);
    }

    /// <summary>Pairs of limbs that reach the ground (bare or with a foot), they carry the body on land.</summary>
    public int LegPairs => _parts.Count(IsWalkingLimb);

    /// <summary>A marine mammal breathes air from the start, a fish needs a lung.</summary>
    public bool BreathesAir => CreatureType.MarineMammal == Type
        || Count(CreaturePartKind.Lung) > 0
        || Count(CreaturePartKind.BigLung) > 0;

    // ---------------------------------------------------------------------------
    // Geometry
    //   Length, profile radius, spine height and slope, cross section, volume and the inside test.
    // ---------------------------------------------------------------------------

    /*
      The body is a loft along X: one ring per spine point, the ring radius follows a
      smooth curve (Catmull-Rom) through the vertebra girths. Behind the last vertebra
      the tail cap tapers off over TAIL_CAP, in front of the first the nose closes as a
      quarter ellipse. A ring is an ellipse, taller than wide for a fish, round and
      slightly flat for a marine mammal.
      The vertebrae sit at their own X (Positions, SPACING apart when fresh). The curves
      run over the bone number s (0 tail .. count - 1 nose); s of an X comes from the
      positions piecewise linear, outside on the caps it continues with the end gap.

        tail tip   vertebrae (about SPACING apart)    nose tip
           <====(  o    o    o   o  o  )====>
    */
    public const float SPACING = 0.36f;             // distance between two vertebrae
    public const float MAX_RADIUS = 0.62f;          // profile radius of girth 1
    private const float TAIL_CAP = 0.3f;
    private const float MIN_NOSE_CAP = 0.12f;
    private const float MIN_PROFILE = 0.04f;

    /// <summary>Length of the vertebra span, the caps come on top.</summary>
    public float Length => _xs[^1] - _xs[0];

    public float HalfLength => Length * 0.5f;

    /// <summary>X of the last vertebra at the tail.</summary>
    public float SpineStartX => _xs[0];

    /// <summary>X of the first vertebra at the nose.</summary>
    public float SpineEndX => _xs[^1];

    public float TailTipX => _xs[0] - TAIL_CAP;

    public float NoseTipX => _xs[^1] + NoseCap;

    private float NoseCap => Mathf.Max(MIN_NOSE_CAP, _girths[^1] * MAX_RADIUS);

    /// <summary>Height of a ring per profile radius.</summary>
    public float HeightScale => CreatureType.Fish == Type ? 1f : 0.88f;

    /// <summary>Width of a ring per profile radius.</summary>
    public float WidthScale => CreatureType.Fish == Type ? 0.72f : 0.95f;

    /// <summary>X of a spine share, 0 = last vertebra at the tail, 1 = first at the nose (outside on the caps).</summary>
    public float XAt(float t)
    {
        float s = t * (_xs.Count - 1);
        int   i = Mathf.Clamp(Mathf.FloorToInt(s), 0, _xs.Count - 2);
        return _xs[i] + (s - i) * (_xs[i + 1] - _xs[i]);
    }

    /// <summary>Spine share of an X, can leave 0..1 on the caps.</summary>
    public float TAt(float x)
    {
        return BoneAt(x) / (_xs.Count - 1);
    }

    /// <summary>Bone number of an X: 0 at the tail vertebra, count - 1 at the nose, continued on the caps.</summary>
    private float BoneAt(float x)
    {
        int i = 0;
        while (i < _xs.Count - 2 && x > _xs[i + 1])
        {
            i++;
        }
        return i + (x - _xs[i]) / Mathf.Max(_xs[i + 1] - _xs[i], 0.001f);
    }

    /// <summary>X of one vertebra.</summary>
    public float VertebraX(int index)
    {
        return _xs[index];
    }

    /// <summary>Profile radius at an X, 0 beyond the tips.</summary>
    /// <example><code>CreatureDesign.Blob().RadiusAt(0f)  // 0.62, the thickest vertebra</code></example>
    public float RadiusAt(float x)
    {
        // Tail cap: tapers to a quarter, the tip closes
        if (x < _xs[0])
        {
            float u = (_xs[0] - x) / TAIL_CAP;
            return u >= 1f ? 0f : _girths[0] * MAX_RADIUS * (1f - 0.75f * u);
        }
        // Nose cap: quarter ellipse
        if (x > _xs[^1])
        {
            float u = (x - _xs[^1]) / NoseCap;
            return u >= 1f ? 0f : _girths[^1] * MAX_RADIUS * Mathf.Sqrt(1f - u * u);
        }

        return Mathf.Max(MIN_PROFILE, SplineAt(_girths, x)) * MAX_RADIUS;
    }

    /// <summary>Height of the spine (center of the ring) at an X, the caps keep the height of their end vertebra.</summary>
    /// <example><code>design.LiftAt(design.NoseTipX)   // the height of the head</code></example>
    public float LiftAt(float x)
    {
        return SplineAt(_lifts, Mathf.Clamp(x, _xs[0], _xs[^1]));
    }

    /// <summary>Slope of the spine (dY / dX) at an X over a wide window, for the tilt of the rings (smooth, no kinks).</summary>
    public float SlopeAt(float x)
    {
        const float H = 0.12f;
        return (LiftAt(x + H) - LiftAt(x - H)) / (2f * H);
    }

    /// <summary>Catmull-Rom through one value per vertebra over the bone number, the ends repeat their neighbor.</summary>
    private float SplineAt(List<float> values, float x)
    {
        float s  = BoneAt(x);
        int   i  = Mathf.Clamp(Mathf.FloorToInt(s), 0, values.Count - 2);
        float f  = s - i;
        float p0 = values[Mathf.Max(i - 1, 0)];
        float p1 = values[i];
        float p2 = values[i + 1];
        float p3 = values[Mathf.Min(i + 2, values.Count - 1)];
        return 0.5f * (2f * p1
            + (-p0 + p2) * f
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f
            + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
    }

    /// <summary>Height of the back above the spine at an X.</summary>
    public float TopAt(float x)
    {
        return RadiusAt(x) * HeightScale;
    }

    /// <summary>Distance of the flank from the spine at an X.</summary>
    public float FlankAt(float x)
    {
        return RadiusAt(x) * WidthScale;
    }

    private const int VOLUME_STEPS = 64;

    /// <summary>Volume of the lofted body in unit space: elliptic slices from tail tip to nose tip.</summary>
    /// <example><code>CreatureDesign.Blob().Volume()   // about 1.3</code></example>
    public float Volume()
    {
        float step   = (NoseTipX - TailTipX) / VOLUME_STEPS;
        float volume = 0f;
        for (int i = 0; i < VOLUME_STEPS; i++)
        {
            float radius = RadiusAt(TailTipX + (i + 0.5f) * step);
            volume += Mathf.Pi * radius * radius * HeightScale * WidthScale * step;
        }
        return volume;
    }

    /// <summary>Whether a point in unit space lies inside the body (the swim wave and the ring tilt ignored).</summary>
    public bool Contains(Vector3 point)
    {
        float radius = RadiusAt(point.X);
        if (radius <= 0f)
        {
            return false;
        }
        float y = (point.Y - LiftAt(point.X)) / (radius * HeightScale);
        float z = point.Z / (radius * WidthScale);
        return y * y + z * z <= 1f;
    }

    // ---------------------------------------------------------------------------
    // Look
    //   Custom body color, the paint pattern and its color, spray paint.
    // ---------------------------------------------------------------------------

    /// <summary>Custom clay color, null keeps Player.ORGANISM_COLOR. Alpha below 1 is translucent like a cell.</summary>
    public Color? Color { get; private set; }

    /// <summary>Picks the body color, null goes back to the default.</summary>
    /// <example><code>design.SetColor(EditorStyle.TRANSLUCENT_COLOR);</code></example>
    public void SetColor(Color? color)
    {
        Color = color;
        Version++;
    }

    /// <summary>How the second color is painted on, CreaturePattern.None for a plain body.</summary>
    public CreaturePattern Pattern { get; private set; }

    /// <summary>Second color of the paint, null for a darker shade of the body color.</summary>
    public Color? PatternColor { get; private set; }

    /// <summary>Paints the body: a pattern in a second color, free of charge, looks only.</summary>
    /// <param name="pattern">(Required) The pattern, None for a plain body.</param>
    /// <param name="color">(Required) The second color, null for a darker shade of the body color.</param>
    /// <example><code>design.SetPattern(CreaturePattern.Stripes, new Color(0.2f, 0.15f, 0.3f));</code></example>
    public void SetPattern(CreaturePattern pattern, Color? color)
    {
        Pattern      = pattern;
        PatternColor = color;
        Version++;
    }

    /*
      Spray paint: dots stored by spine share and angle around the ring, so they stay on
      the skin when the body is reshaped. Only PaintVersion counts their changes: the
      visual then updates the shader dots, the meshes stay (spraying is fast).
      The shader holds MAX_SPLATS dots (shaders/clay.gdshaderinc), a full body sprays no more.
    */
    public const int MAX_SPLATS = 160;
    public const float MIN_SPLAT_RADIUS = 0.04f;
    public const float MAX_SPLAT_RADIUS = 0.35f;

    private readonly List<PaintSplat> _splats = new();
    private int _lastStroke;

    public IReadOnlyList<PaintSplat> Splats => _splats;

    /// <summary>Counts every spray change, the visual updates the paint on it.</summary>
    public int PaintVersion { get; private set; }

    /// <summary>A new stroke id, the dots of one drag share it.</summary>
    public int NewStroke()
    {
        return ++_lastStroke;
    }

    /// <summary>Sprays one dot where a point of the body was hit.</summary>
    /// <param name="point">(Required) Point on or in the body, unit space.</param>
    /// <param name="radius">(Required) Dot size, clamped to MIN..MAX_SPLAT_RADIUS.</param>
    /// <param name="color">(Required) Paint color.</param>
    /// <param name="stroke">(Required) Stroke id from NewStroke.</param>
    /// <returns>False when MAX_SPLATS dots are on the body already.</returns>
    /// <example><code>design.AddSplat(new Vector3(0f, 0f, 0.4f), 0.12f, Colors.Purple, design.NewStroke());</code></example>
    public bool AddSplat(Vector3 point, float radius, Color color, int stroke)
    {
        if (_splats.Count >= MAX_SPLATS)
        {
            return false;
        }
        float ring  = Mathf.Max(RadiusAt(point.X), 0.01f);
        float angle = Mathf.Atan2((point.Y - LiftAt(point.X)) / (ring * HeightScale), point.Z / (ring * WidthScale));
        _splats.Add(new PaintSplat(TAt(point.X), angle, Mathf.Clamp(radius, MIN_SPLAT_RADIUS, MAX_SPLAT_RADIUS), color, stroke));
        PaintVersion++;
        return true;
    }

    /// <summary>Removes the dots of the last stroke.</summary>
    /// <returns>False when there is no paint.</returns>
    public bool UndoStroke()
    {
        if (0 == _splats.Count)
        {
            return false;
        }
        int last = _splats.Max(splat => splat.Stroke);
        _splats.RemoveAll(splat => last == splat.Stroke);
        PaintVersion++;
        return true;
    }

    /// <summary>Wipes all spray paint.</summary>
    public void ClearPaint()
    {
        _splats.Clear();
        PaintVersion++;
    }

    /// <summary>Center of a dot on the current body, unit space (the ring tilt ignored).</summary>
    public Vector3 SplatPoint(PaintSplat splat)
    {
        float x    = XAt(splat.T);
        float ring = RadiusAt(x);
        return new Vector3(x, LiftAt(x) + Mathf.Sin(splat.Angle) * ring * HeightScale, Mathf.Cos(splat.Angle) * ring * WidthScale);
    }

    // ---------------------------------------------------------------------------
    // Copies
    //   Copy from another design.
    // ---------------------------------------------------------------------------

    /// <summary>Replaces type, spine, color and (optionally) the parts with those of another design.</summary>
    /// <param name="other">(Required) The design copied.</param>
    /// <param name="isPartsKept">(Optional) False copies only the body, the parts stay empty (drag ghost). Defaults to true.</param>
    public void CopyFrom(CreatureDesign other, bool isPartsKept = true)
    {
        Type  = other.Type;
        Color        = other.Color;
        Pattern      = other.Pattern;
        PatternColor = other.PatternColor;
        _splats.Clear();
        _splats.AddRange(other._splats);
        _lastStroke = other._lastStroke;
        PaintVersion++;
        _girths.Clear();
        _girths.AddRange(other._girths);
        _lifts.Clear();
        _lifts.AddRange(other._lifts);
        _xs.Clear();
        _xs.AddRange(other._xs);
        _parts.Clear();
        if (isPartsKept)
        {
            _parts.AddRange(other._parts);
            _lastId = other._lastId;
        }
        Version++;
    }

    // ---------------------------------------------------------------------------
    // Body plans
    //   The clay blob, the starter bodies, the type examples and the NPC creatures.
    // ---------------------------------------------------------------------------

    private const float NPC_GIRTH_JITTER = 0.08f;
    private static readonly Color ORCA_PATCH_COLOR = new(0.95f, 0.95f, 0.92f);

    /// <summary>The fresh clay blob the editor starts from: five round vertebrae, no parts.</summary>
    public static CreatureDesign Blob()
    {
        return FromGirths(CreatureType.Fish, 0.62f, 0.9f, 1f, 0.9f, 0.62f);
    }

    /*
      Starter body after the type choice, so the first creature already reads as an animal:
      - Fish:           slim tail stalk, deepest just behind the head, a blunt head
      - Marine mammal:  longer, a thick chest and a narrow tail stock like a dolphin
      Both get their tail (caudal fin or fluke) and a pair of eyes for free (paid 0, so
      removing them refunds nothing).
    */
    /// <summary>The body the clay takes once the type is chosen: streamlined, with tail and eyes.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <example><code>CreatureDesign.Starter(CreatureType.Fish).Parts.Count   // 2</code></example>
    public static CreatureDesign Starter(CreatureType type)
    {
        CreatureDesign design = CreatureType.Fish == type
            ? FromGirths(type, 0.3f, 0.48f, 0.7f, 0.86f, 0.84f, 0.62f)
            : FromGirths(type, 0.26f, 0.42f, 0.66f, 0.88f, 0.92f, 0.82f, 0.6f);
        design.TryPlace(CreatureType.Fish == type ? CreaturePartKind.CaudalFin : CreaturePartKind.Fluke, 0f, 0);
        design.TryPlace(CreaturePartKind.Eye, 0.86f, 0);
        return design;
    }

    /// <summary>A finished example of a body type, shown on the type choice tiles.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    public static CreatureDesign Example(CreatureType type)
    {
        CreatureDesign design = CreatureType.Fish == type
            ? FromGirths(type, 0.35f, 0.6f, 0.8f, 0.85f, 0.7f, 0.5f)
            : FromGirths(type, 0.3f, 0.55f, 0.8f, 0.95f, 0.9f, 0.75f, 0.6f);
        design.TryPlace(CreatureType.Fish == type ? CreaturePartKind.CaudalFin : CreaturePartKind.Fluke, 0f, 0);
        design.TryPlace(CreaturePartKind.DorsalFin, 0.5f, 0);
        design.TryPlace(CreaturePartKind.PectoralFin, 0.68f, 0);
        design.TryPlace(CreaturePartKind.Eye, 0.88f, 0);
        if (CreatureType.MarineMammal == type)
        {
            design.TryPlace(CreaturePartKind.Blowhole, 0.78f, 0);
        }
        return design;
    }

    /*
      NPC creatures per critter kind, the girths jitter a little:
      - Bacterium:  small slim fish, flees, striped
      - Amoeba:     round puffer with spines, spotted
      - Hunter:     shark (fish, dark saddle) or orca (marine mammal, white patches), with jaw
    */
    /// <summary>A body plan for an NPC creature of the organism stage.</summary>
    /// <param name="kind">(Required) The critter kind, sets the shape and the parts.</param>
    public static CreatureDesign ForCritter(CritterKind kind)
    {
        CreatureDesign design;
        switch (kind)
        {
            case CritterKind.Bacterium:
                design = FromGirths(CreatureType.Fish, 0.4f, 0.7f, 0.8f, 0.6f);
                design.TryPlace(CreaturePartKind.CaudalFin, 0f, 0);
                design.TryPlace(CreaturePartKind.Eye, 0.82f, 0);
                design.SetPattern(CreaturePattern.Stripes, null);
                break;
            case CritterKind.Amoeba:
                design = FromGirths(CreatureType.Fish, 0.6f, 0.95f, 1f, 0.95f, 0.7f);
                design.TryPlace(CreaturePartKind.CaudalFin, 0f, 0);
                design.TryPlace(CreaturePartKind.Spines, 0.5f, 0);
                design.TryPlace(CreaturePartKind.PectoralFin, 0.6f, 0);
                design.TryPlace(CreaturePartKind.Eye, 0.85f, 0);
                design.SetPattern(CreaturePattern.Spots, null);
                break;
            case CritterKind.Hunter:
                bool isShark = Rand.Chance(0.6f);
                design = isShark
                    ? FromGirths(CreatureType.Fish, 0.3f, 0.5f, 0.75f, 0.9f, 0.85f, 0.7f, 0.5f)
                    : FromGirths(CreatureType.MarineMammal, 0.35f, 0.6f, 0.85f, 0.95f, 0.95f, 0.8f, 0.6f);
                design.TryPlace(isShark ? CreaturePartKind.CaudalFin : CreaturePartKind.Fluke, 0f, 0);
                design.TryPlace(CreaturePartKind.DorsalFin, 0.55f, 0);
                design.TryPlace(CreaturePartKind.PectoralFin, 0.66f, 0);
                design.TryPlace(CreaturePartKind.Eye, 0.88f, 0);
                design.TryPlace(CreaturePartKind.Jaw, 1f, 0);
                if (!isShark)
                {
                    design.TryPlace(CreaturePartKind.Blowhole, 0.76f, 0);
                }
                design.SetPattern(isShark ? CreaturePattern.Saddle : CreaturePattern.Patches, isShark ? null : ORCA_PATCH_COLOR);
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be a known CritterKind, got '{kind}'.");
        }

        for (int i = 0; i < design._girths.Count; i++)
        {
            design.SetGirth(i, design._girths[i] + Rand.Range(-NPC_GIRTH_JITTER, NPC_GIRTH_JITTER));
        }
        return design;
    }

    private static CreatureDesign FromGirths(CreatureType type, params float[] girths)
    {
        var design = new CreatureDesign { Type = type };
        design._girths.AddRange(girths);
        design._lifts.AddRange(new float[girths.Length]);   // a straight spine
        for (int i = 0; i < girths.Length; i++)
        {
            design._xs.Add((i - (girths.Length - 1) * 0.5f) * SPACING);   // evenly spaced, centered
        }
        return design;
    }
}
