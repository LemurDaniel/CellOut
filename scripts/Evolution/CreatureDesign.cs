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
//     CREATURE DESIGN   Spine, parts, geometry, copies and the ready-made body plans.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// =============================   PLACED PART   =============================
// ===========================================================================
//
//   CreaturePart   Kind, share of the spine (0 tail .. 1 nose) and the points paid.
//

/// <summary>One placed creature part. Side parts stand for a mirrored pair.</summary>
public readonly record struct CreaturePart(CreaturePartKind Kind, float T, int PaidCost)
{
    public CreaturePartInfo Info => CreatureCatalog.Get(Kind);
}

public sealed class CreatureDesign
{
    // ===========================================================================
    // ===========================   CREATURE DESIGN   ===========================
    // ===========================================================================
    //
    //   Spine        Body type, girth per vertebra, adding and removing vertebrae.
    //   Parts        Placement rules, removal and counts.
    //   Geometry     Length, profile radius, cross section and the inside test.
    //   Copies       Copy from another design.
    //   Body plans   The clay blob, the type examples and the NPC creatures.
    //

    // ---------------------------------------------------------------------------
    // Spine
    //   Body type, girth per vertebra, adding and removing vertebrae.
    // ---------------------------------------------------------------------------

    public const int MIN_VERTEBRAE = 4;
    public const int MAX_VERTEBRAE = 9;
    public const float MIN_GIRTH = 0.25f;
    public const float MAX_GIRTH = 1f;

    private readonly List<float> _girths = new();

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

    /// <summary>Inserts a vertebra in the middle, as thick as its neighbors. Parts keep their share.</summary>
    /// <returns>False at MAX_VERTEBRAE.</returns>
    public bool AddVertebra()
    {
        if (_girths.Count >= MAX_VERTEBRAE)
        {
            return false;
        }
        int middle = _girths.Count / 2;
        _girths.Insert(middle, (_girths[middle - 1] + _girths[middle]) * 0.5f);
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
        _girths.RemoveAt(_girths.Count / 2);
        Version++;
        return true;
    }

    // ---------------------------------------------------------------------------
    // Parts
    //   Placement rules, removal and counts.
    // ---------------------------------------------------------------------------

    private const float MIN_FREE_T = 0.1f;          // back and flank parts stay off the very ends
    private const float MAX_FREE_T = 0.92f;
    private const float MIN_PART_GAP = 0.14f;       // share of the spine between two parts on the same mount
    private const float REMOVE_DISTANCE = 0.18f;

    private readonly List<CreaturePart> _parts = new();

    public IReadOnlyList<CreaturePart> Parts => _parts;

    /// <summary>Where a click on the body would place the part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Clicked share of the spine, 0 tail .. 1 nose.</param>
    /// <param name="snapped">The snapped share on success.</param>
    /// <returns>False when the body type cannot use the part or its spot is taken.</returns>
    public bool TrySnap(CreaturePartKind kind, float t, out float snapped)
    {
        CreaturePartInfo info = CreatureCatalog.Get(kind);
        snapped = 0f;
        if (!info.IsFor(Type))
        {
            return false;
        }

        switch (info.Mount)
        {
            // Tail and nose: one part each
            case CreatureMount.Tail:
                return !_parts.Any(part => CreatureMount.Tail == part.Info.Mount);
            case CreatureMount.Head:
                snapped = 1f;
                return !_parts.Any(part => CreatureMount.Head == part.Info.Mount);
            // Back, flanks and inside: anywhere between the ends with a gap to parts on the same mount
            default:
                float spot = Mathf.Clamp(t, MIN_FREE_T, MAX_FREE_T);
                snapped = spot;
                return !_parts.Any(part => info.Mount == part.Info.Mount && Mathf.Abs(part.T - spot) < MIN_PART_GAP);
        }
    }

    /// <summary>Snaps and adds the part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="t">(Required) Clicked share of the spine.</param>
    /// <param name="paidCost">(Required) Points paid, refunded on removal.</param>
    /// <returns>True when the part was placed.</returns>
    /// <example><code>design.TryPlace(CreaturePartKind.Eye, 0.85f, paidCost: 1)</code></example>
    public bool TryPlace(CreaturePartKind kind, float t, int paidCost)
    {
        if (!TrySnap(kind, t, out float snapped))
        {
            return false;
        }
        _parts.Add(new CreaturePart(kind, snapped, paidCost));
        Version++;
        return true;
    }

    /// <summary>Removes the part closest to a spine share.</summary>
    /// <param name="t">(Required) Clicked share of the spine.</param>
    /// <returns>The removed part for the refund, null when none was close enough.</returns>
    public CreaturePart? RemoveNear(float t)
    {
        if (0 == _parts.Count)
        {
            return null;
        }
        CreaturePart closest = _parts.OrderBy(part => Mathf.Abs(part.T - t)).First();
        if (Mathf.Abs(closest.T - t) > REMOVE_DISTANCE)
        {
            return null;
        }
        _parts.Remove(closest);
        Version++;
        return closest;
    }

    /// <summary>Number of placed parts of one kind.</summary>
    public int Count(CreaturePartKind kind)
    {
        return _parts.Count(part => kind == part.Kind);
    }

    /// <summary>Pairs of legs, they carry the body on land.</summary>
    public int LegPairs => Count(CreaturePartKind.Legs);

    /// <summary>A marine mammal breathes air from the start, a fish needs a lung.</summary>
    public bool BreathesAir => CreatureType.MarineMammal == Type || Count(CreaturePartKind.Lung) > 0;

    // ---------------------------------------------------------------------------
    // Geometry
    //   Length, profile radius, cross section and the inside test.
    // ---------------------------------------------------------------------------

    /*
      The body is a loft along X: one ring per spine point, the ring radius follows a
      smooth curve (Catmull-Rom) through the vertebra girths. Behind the last vertebra
      the tail cap tapers off over TAIL_CAP, in front of the first the nose closes as a
      quarter ellipse. A ring is an ellipse, taller than wide for a fish, round and
      slightly flat for a marine mammal.

        tail tip   vertebrae (SPACING apart)    nose tip
           <====(  o    o    o    o    o  )====>
    */
    public const float SPACING = 0.36f;             // distance between two vertebrae
    public const float MAX_RADIUS = 0.62f;          // profile radius of girth 1
    private const float TAIL_CAP = 0.3f;
    private const float MIN_NOSE_CAP = 0.12f;
    private const float MIN_PROFILE = 0.04f;

    /// <summary>Length of the vertebra span, the caps come on top.</summary>
    public float Length => (_girths.Count - 1) * SPACING;

    public float HalfLength => Length * 0.5f;

    public float TailTipX => -HalfLength - TAIL_CAP;

    public float NoseTipX => HalfLength + NoseCap;

    private float NoseCap => Mathf.Max(MIN_NOSE_CAP, _girths[^1] * MAX_RADIUS);

    /// <summary>Height of a ring per profile radius.</summary>
    public float HeightScale => CreatureType.Fish == Type ? 1f : 0.88f;

    /// <summary>Width of a ring per profile radius.</summary>
    public float WidthScale => CreatureType.Fish == Type ? 0.72f : 0.95f;

    /// <summary>X of a spine share, 0 = last vertebra at the tail, 1 = first at the nose.</summary>
    public float XAt(float t)
    {
        return -HalfLength + t * Length;
    }

    /// <summary>Spine share of an X, can leave 0..1 on the caps.</summary>
    public float TAt(float x)
    {
        return (x + HalfLength) / Mathf.Max(Length, 0.001f);
    }

    /// <summary>X of one vertebra.</summary>
    public float VertebraX(int index)
    {
        return -HalfLength + index * SPACING;
    }

    /// <summary>Profile radius at an X, 0 beyond the tips.</summary>
    /// <example><code>CreatureDesign.Blob().RadiusAt(0f)  // 0.62, the thickest vertebra</code></example>
    public float RadiusAt(float x)
    {
        float half = HalfLength;

        // Tail cap: tapers to a quarter, the tip closes
        if (x < -half)
        {
            float u = (-half - x) / TAIL_CAP;
            return u >= 1f ? 0f : _girths[0] * MAX_RADIUS * (1f - 0.75f * u);
        }
        // Nose cap: quarter ellipse
        if (x > half)
        {
            float u = (x - half) / NoseCap;
            return u >= 1f ? 0f : _girths[^1] * MAX_RADIUS * Mathf.Sqrt(1f - u * u);
        }

        // Catmull-Rom through the girths, the ends repeat their neighbor
        float s  = (x + half) / SPACING;
        int   i  = Mathf.Clamp(Mathf.FloorToInt(s), 0, _girths.Count - 2);
        float f  = s - i;
        float p0 = _girths[Mathf.Max(i - 1, 0)];
        float p1 = _girths[i];
        float p2 = _girths[i + 1];
        float p3 = _girths[Mathf.Min(i + 2, _girths.Count - 1)];
        float girth = 0.5f * (2f * p1
            + (-p0 + p2) * f
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f
            + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
        return Mathf.Max(MIN_PROFILE, girth) * MAX_RADIUS;
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

    /// <summary>Whether a point in unit space lies inside the body (the swim wave ignored).</summary>
    public bool Contains(Vector3 point)
    {
        float radius = RadiusAt(point.X);
        if (radius <= 0f)
        {
            return false;
        }
        float y = point.Y / (radius * HeightScale);
        float z = point.Z / (radius * WidthScale);
        return y * y + z * z <= 1f;
    }

    // ---------------------------------------------------------------------------
    // Copies
    //   Copy from another design.
    // ---------------------------------------------------------------------------

    /// <summary>Replaces type, spine and parts with those of another design.</summary>
    public void CopyFrom(CreatureDesign other)
    {
        Type = other.Type;
        _girths.Clear();
        _girths.AddRange(other._girths);
        _parts.Clear();
        _parts.AddRange(other._parts);
        Version++;
    }

    // ---------------------------------------------------------------------------
    // Body plans
    //   The clay blob, the type examples and the NPC creatures.
    // ---------------------------------------------------------------------------

    private const float NPC_GIRTH_JITTER = 0.08f;

    /// <summary>The fresh clay blob the editor starts from: five round vertebrae, no parts.</summary>
    public static CreatureDesign Blob()
    {
        return FromGirths(CreatureType.Fish, 0.62f, 0.9f, 1f, 0.9f, 0.62f);
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
      - Bacterium:  small slim fish, flees
      - Amoeba:     round puffer with spines
      - Hunter:     shark (fish) or orca (marine mammal), with jaw
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
                break;
            case CritterKind.Amoeba:
                design = FromGirths(CreatureType.Fish, 0.6f, 0.95f, 1f, 0.95f, 0.7f);
                design.TryPlace(CreaturePartKind.CaudalFin, 0f, 0);
                design.TryPlace(CreaturePartKind.Spines, 0.5f, 0);
                design.TryPlace(CreaturePartKind.PectoralFin, 0.6f, 0);
                design.TryPlace(CreaturePartKind.Eye, 0.85f, 0);
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
        return design;
    }
}
