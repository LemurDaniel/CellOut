// NOTE:
//   The body plan of an organism: a spine of vertebrae, each with its own girth, and
//   the parts placed along it. Positions on the body are a share T of the spine,
//   0 = tail end, 1 = nose. Unit space like every body: +X forward, Y up, the contact
//   circle of the body has radius 1. Also answers the geometry questions of the
//   creature (profile radius, inside test), so visual, editor and stats agree.
//   Edited by the CreatureEditor on a copy, committed to the Player when it closes.
//   Coordinator of the classes in Design/: it owns the placed parts and counts every
//   change (Version), the spine (CreatureSpine), the look (CreatureLook), the placement
//   rules (PartPlacement), the limb joints (LimbJoints) and the ready-made plans
//   (BodyPlans) hold the detail logic.
//
//   var design = CreatureDesign.Blob();
//   design.Type = CreatureType.Fish;
//   design.TryPlace(CreaturePartKind.CaudalFin, 0f, paidCost: 2);

//
//   Chapters:
//     CREATURE DESIGN   Spine, parts, geometry, look, copies and the ready-made body plans.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public sealed class CreatureDesign
{
    // ===========================================================================
    // ===========================   CREATURE DESIGN   ===========================
    // ===========================================================================
    //
    //   Spine        Body type, position, girth and height per vertebra, adding vertebrae in the middle or at the ends.
    //   Parts        Placement, branches, limb joints, picking, removal, sizes, poses and counts.
    //   Geometry     Length, profile radius, spine height and slope, cross section, volume and the inside test.
    //   Look         Custom body color, the paint pattern and its color, spray paint.
    //   Copies       Copy from another design.
    //   Body plans   The clay blob, the starter bodies, the type examples and the NPC creatures.
    //

    // ---------------------------------------------------------------------------
    // Spine
    //   Body type, position, girth and height per vertebra, adding vertebrae in the middle or at the ends.
    // ---------------------------------------------------------------------------

    public const int MIN_VERTEBRAE = CreatureSpine.MIN_VERTEBRAE;
    public const int MAX_VERTEBRAE = CreatureSpine.MAX_VERTEBRAE;
    public const float MIN_GIRTH = CreatureSpine.MIN_GIRTH;
    public const float MAX_GIRTH = CreatureSpine.MAX_GIRTH;
    public const float MAX_LIFT = CreatureSpine.MAX_LIFT;
    public const float MIN_GAP = CreatureSpine.MIN_GAP;
    public const float MAX_GAP = CreatureSpine.MAX_GAP;
    public const float SPACING = CreatureSpine.SPACING;
    public const float MAX_RADIUS = CreatureSpine.MAX_RADIUS;

    private readonly CreatureSpine _spine = new();

    /// <summary>Fish swim side to side, marine mammals up and down.</summary>
    public CreatureType Type { get; set; } = CreatureType.Fish;

    /// <summary>Girth per vertebra, tail first, MIN_GIRTH..MAX_GIRTH.</summary>
    public IReadOnlyList<float> Girths => _spine.Girths;

    /// <summary>Height of every vertebra above the straight line, tail first, -MAX_LIFT..MAX_LIFT.</summary>
    public IReadOnlyList<float> Lifts => _spine.Lifts;

    /// <summary>Position of every vertebra along the body (X), tail first.</summary>
    public IReadOnlyList<float> Positions => _spine.Positions;

    /// <summary>Counts every change, the 3D visual rebuilds on it.</summary>
    public int Version { get; private set; }

    public float MeanGirth => _spine.MeanGirth;

    /// <summary>Sets the girth of one vertebra, clamped.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="girth">(Required) New girth, clamped to MIN_GIRTH..MAX_GIRTH.</param>
    public void SetGirth(int index, float girth)
    {
        _spine.SetGirth(index, girth);
        Version++;
    }

    /// <summary>Lifts one vertebra, the neighbors follow smoothly (CreatureSpine.SetLift).</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="lift">(Required) Height above the straight line, clamped to -MAX_LIFT..MAX_LIFT.</param>
    /// <example><code>design.SetLift(design.Girths.Count - 1, 0.6f);   // a neck</code></example>
    public void SetLift(int index, float lift)
    {
        _spine.SetLift(index, lift);
        Version++;
    }

    /// <summary>Moves one vertebra along the body, clamped between its neighbors (CreatureSpine.SetVertebraX).</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="x">(Required) Wanted X in unit space.</param>
    /// <returns>The X the vertebra took.</returns>
    /// <example><code>design.SetVertebraX(2, design.Positions[2] + 0.1f);</code></example>
    public float SetVertebraX(int index, float x)
    {
        float taken = _spine.SetVertebraX(index, x);
        Version++;
        return taken;
    }

    /// <summary>Inserts a vertebra in the middle, as thick and high as its neighbors. Parts keep their share.</summary>
    /// <returns>False at MAX_VERTEBRAE.</returns>
    public bool AddVertebra()
    {
        if (!_spine.AddVertebra())
        {
            return false;
        }
        Version++;
        return true;
    }

    /// <summary>Removes the middle vertebra.</summary>
    /// <returns>False at MIN_VERTEBRAE.</returns>
    public bool RemoveVertebra()
    {
        if (!_spine.RemoveVertebra())
        {
            return false;
        }
        Version++;
        return true;
    }

    /// <summary>Adds a vertebra at the nose or the tail end, the free parts keep their place on the body.</summary>
    /// <param name="isNose">(Required) True grows the front (a neck), false the tail.</param>
    /// <returns>False at MAX_VERTEBRAE.</returns>
    /// <example><code>design.GrowEnd(isNose: true);</code></example>
    public bool GrowEnd(bool isNose)
    {
        int count = _spine.Count;
        if (!_spine.GrowEnd(isNose))
        {
            return false;
        }
        RemapParts(count, count + 1, isNose, 1);
        Version++;
        return true;
    }

    /// <summary>Removes the vertebra at the nose or the tail end.</summary>
    /// <param name="isNose">(Required) True shortens the front, false the tail.</param>
    /// <returns>False at MIN_VERTEBRAE.</returns>
    public bool ShrinkEnd(bool isNose)
    {
        int count = _spine.Count;
        if (!_spine.ShrinkEnd(isNose))
        {
            return false;
        }
        RemapParts(count, count - 1, isNose, -1);
        Version++;
        return true;
    }

    /// <summary>Keeps the free parts in place when the spine grows or shrinks at an end (PartPlacement.ShareAfterSpineChange).</summary>
    /// <param name="change">(Required) 1 for a new vertebra, -1 for a removed one.</param>
    private void RemapParts(int countBefore, int countAfter, bool isNose, int change)
    {
        for (int i = 0; i < _parts.Count; i++)
        {
            _parts[i] = _parts[i] with { T = PartPlacement.ShareAfterSpineChange(_parts[i], countBefore, countAfter, isNose, change) };
        }
    }

    // ---------------------------------------------------------------------------
    // Parts
    //   Placement, branches, limb joints, picking, removal, sizes, poses and counts.
    // ---------------------------------------------------------------------------

    private readonly List<CreaturePart> _parts = new();
    private int _lastId;                             // ids of placed parts, never reused in a design

    public IReadOnlyList<CreaturePart> Parts => _parts;

    /// <summary>The side a part really takes: only paired parts can be a single half.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="side">(Required) Wanted side, 0 pair, -1 left, 1 right.</param>
    public static int SideFor(CreaturePartKind kind, int side)
    {
        return PartPlacement.SideFor(kind, side);
    }

    /// <summary>Where a click on the body would place the part (PartPlacement.TrySnap).</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Clicked share of the spine, 0 tail .. 1 nose.</param>
    /// <param name="snapped">The snapped share on success.</param>
    /// <param name="side">(Optional) 0 pair, -1 or 1 a single half of a paired part. Defaults to 0.</param>
    /// <returns>False when the body type cannot use the part or its spot is taken.</returns>
    public bool TrySnap(CreaturePartKind kind, float t, out float snapped, int side = 0)
    {
        return PartPlacement.TrySnap(_parts, Type, kind, t, out snapped, side);
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
            part = part with { Joints = LimbJoints.StretchToGround(part.Joints, LimbRoot(part).Y) };
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
            part = part with { Joints = LimbJoints.StretchToGround(part.Joints, LimbRoot(part).Y) };
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
        return PartPlacement.BranchIndices(_parts, index).Sum(i => _parts[i].PaidCost);
    }

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

    public const float MIN_LIMB_SEGMENT = LimbJoints.MIN_LIMB_SEGMENT;
    public const float MAX_LIMB_SEGMENT = LimbJoints.MAX_LIMB_SEGMENT;

    /// <summary>The joints a sculpted limb starts with, null for every other part (LimbJoints.DefaultFor).</summary>
    /// <example><code>CreatureDesign.DefaultJoints(CreaturePartKind.Tentacle).Count   // 4</code></example>
    public static IReadOnlyList<Vector3> DefaultJoints(CreaturePartKind kind)
    {
        return LimbJoints.DefaultFor(kind);
    }

    /// <summary>Moves one joint of a sculpted limb, the joints further out follow by the same shift (LimbJoints.Move).</summary>
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
        _parts[index] = _parts[index] with { Joints = LimbJoints.Move(_parts[index].Joints, joint, point) };
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
        return tipY <= Coast.GROUND_HEIGHT + LimbJoints.WALK_REACH;
    }

    /// <summary>Index of the part closest to a spine share.</summary>
    /// <param name="t">(Required) Clicked share of the spine.</param>
    /// <returns>The index in Parts, -1 when none is close enough.</returns>
    public int IndexNear(float t)
    {
        return PartPlacement.IndexNear(_parts, t);
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
        foreach (int i in PartPlacement.BranchIndices(_parts, index).OrderByDescending(i => i))
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

    // The curves of the loft are explained in Design/CreatureSpine.cs, chapter MEASURE THE SPINE.
    // The spine knows the profile radius, the body type makes an ellipse of every ring.

    /// <summary>Length of the vertebra span, the caps come on top.</summary>
    public float Length => _spine.Length;

    public float HalfLength => _spine.HalfLength;

    /// <summary>X of the last vertebra at the tail.</summary>
    public float SpineStartX => _spine.StartX;

    /// <summary>X of the first vertebra at the nose.</summary>
    public float SpineEndX => _spine.EndX;

    public float TailTipX => _spine.TailTipX;

    public float NoseTipX => _spine.NoseTipX;

    /// <summary>Height of a ring per profile radius.</summary>
    public float HeightScale => CreatureType.Fish == Type ? 1f : 0.88f;

    /// <summary>Width of a ring per profile radius.</summary>
    public float WidthScale => CreatureType.Fish == Type ? 0.72f : 0.95f;

    /// <summary>X of a spine share, 0 = last vertebra at the tail, 1 = first at the nose (outside on the caps).</summary>
    public float XAt(float t)
    {
        return _spine.XAt(t);
    }

    /// <summary>Spine share of an X, can leave 0..1 on the caps.</summary>
    public float TAt(float x)
    {
        return _spine.TAt(x);
    }

    /// <summary>X of one vertebra.</summary>
    public float VertebraX(int index)
    {
        return _spine.VertebraX(index);
    }

    /// <summary>Profile radius at an X, 0 beyond the tips.</summary>
    /// <example><code>CreatureDesign.Blob().RadiusAt(0f)  // 0.62, the thickest vertebra</code></example>
    public float RadiusAt(float x)
    {
        return _spine.RadiusAt(x);
    }

    /// <summary>Height of the spine (center of the ring) at an X, the caps keep the height of their end vertebra.</summary>
    /// <example><code>design.LiftAt(design.NoseTipX)   // the height of the head</code></example>
    public float LiftAt(float x)
    {
        return _spine.LiftAt(x);
    }

    /// <summary>Slope of the spine (dY / dX) at an X over a wide window, for the tilt of the rings (smooth, no kinks).</summary>
    public float SlopeAt(float x)
    {
        return _spine.SlopeAt(x);
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

    /// <summary>Volume of the lofted body in unit space: elliptic slices from tail tip to nose tip.</summary>
    /// <example><code>CreatureDesign.Blob().Volume()   // about 1.3</code></example>
    public float Volume()
    {
        return _spine.Volume(HeightScale, WidthScale);
    }

    /// <summary>Whether a point in unit space lies inside the body (the swim wave and the ring tilt ignored).</summary>
    public bool Contains(Vector3 point)
    {
        return _spine.Contains(point, HeightScale, WidthScale);
    }

    // ---------------------------------------------------------------------------
    // Look
    //   Custom body color, the paint pattern and its color, spray paint.
    // ---------------------------------------------------------------------------

    public const int MAX_SPLATS = CreatureLook.MAX_SPLATS;
    public const float MIN_SPLAT_RADIUS = CreatureLook.MIN_SPLAT_RADIUS;
    public const float MAX_SPLAT_RADIUS = CreatureLook.MAX_SPLAT_RADIUS;

    private readonly CreatureLook _look = new();

    /// <summary>Custom clay color, null keeps Player.ORGANISM_COLOR. Alpha below 1 is translucent like a cell.</summary>
    public Color? Color => _look.Color;

    /// <summary>Picks the body color, null goes back to the default.</summary>
    /// <example><code>design.SetColor(EditorStyle.TRANSLUCENT_COLOR);</code></example>
    public void SetColor(Color? color)
    {
        _look.SetColor(color);
        Version++;
    }

    /// <summary>How the second color is painted on, CreaturePattern.None for a plain body.</summary>
    public CreaturePattern Pattern => _look.Pattern;

    /// <summary>Second color of the paint, null for a darker shade of the body color.</summary>
    public Color? PatternColor => _look.PatternColor;

    /// <summary>Paints the body: a pattern in a second color, free of charge, looks only.</summary>
    /// <param name="pattern">(Required) The pattern, None for a plain body.</param>
    /// <param name="color">(Required) The second color, null for a darker shade of the body color.</param>
    /// <example><code>design.SetPattern(CreaturePattern.Stripes, new Color(0.2f, 0.15f, 0.3f));</code></example>
    public void SetPattern(CreaturePattern pattern, Color? color)
    {
        _look.SetPattern(pattern, color);
        Version++;
    }

    // Spray paint (Design/CreatureLook.cs, section Spray paint): only PaintVersion counts its changes
    public IReadOnlyList<PaintSplat> Splats => _look.Splats;

    /// <summary>Counts every spray change, the visual updates the paint on it.</summary>
    public int PaintVersion => _look.PaintVersion;

    /// <summary>A new stroke id, the dots of one drag share it.</summary>
    public int NewStroke()
    {
        return _look.NewStroke();
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
        float ring  = Mathf.Max(RadiusAt(point.X), 0.01f);
        float angle = Mathf.Atan2((point.Y - LiftAt(point.X)) / (ring * HeightScale), point.Z / (ring * WidthScale));
        return _look.AddSplat(TAt(point.X), angle, radius, color, stroke);
    }

    /// <summary>Removes the dots of the last stroke.</summary>
    /// <returns>False when there is no paint.</returns>
    public bool UndoStroke()
    {
        return _look.UndoStroke();
    }

    /// <summary>Wipes all spray paint.</summary>
    public void ClearPaint()
    {
        _look.ClearPaint();
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
        Type = other.Type;
        _look.CopyFrom(other._look);
        _spine.CopyFrom(other._spine);
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

    /// <summary>A design with a straight spine of the given girths, evenly spaced and centered, no parts.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <param name="girths">(Required) Girth per vertebra, tail first.</param>
    /// <example><code>CreatureDesign.FromGirths(CreatureType.Fish, 0.4f, 0.7f, 0.8f, 0.6f).Girths.Count   // 4</code></example>
    public static CreatureDesign FromGirths(CreatureType type, params float[] girths)
    {
        var design = new CreatureDesign { Type = type };
        design._spine.Reset(girths);
        return design;
    }

    /// <summary>The fresh clay blob the editor starts from: five round vertebrae, no parts (BodyPlans.Blob).</summary>
    public static CreatureDesign Blob()
    {
        return BodyPlans.Blob();
    }

    /// <summary>The body the clay takes once the type is chosen: streamlined, with tail and eyes (BodyPlans.Starter).</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <example><code>CreatureDesign.Starter(CreatureType.Fish).Parts.Count   // 2</code></example>
    public static CreatureDesign Starter(CreatureType type)
    {
        return BodyPlans.Starter(type);
    }

    /// <summary>A finished example of a body type, shown on the type choice tiles (BodyPlans.Example).</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    public static CreatureDesign Example(CreatureType type)
    {
        return BodyPlans.Example(type);
    }

    /// <summary>A body plan for an NPC creature of the organism stage (BodyPlans.ForCritter).</summary>
    /// <param name="kind">(Required) The critter kind, sets the shape and the parts.</param>
    public static CreatureDesign ForCritter(CritterKind kind)
    {
        return BodyPlans.ForCritter(kind);
    }
}
