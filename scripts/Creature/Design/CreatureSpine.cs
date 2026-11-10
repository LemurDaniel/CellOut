// NOTE:
//   The spine of a body plan: one vertebra after the other, each with its own girth,
//   height and position along the body, and the curves through them (profile radius,
//   spine height and slope). Unit space: +X forward, Y up, tail first.
//   Pure logic without engine calls, owned by a CreatureDesign that counts the changes
//   and keeps its parts in place when an end grows.
//
//   var spine = new CreatureSpine();
//   spine.Reset(new[] { 0.62f, 0.9f, 1f, 0.9f, 0.62f });
//   spine.SetLift(spine.Count - 1, 0.6f);          // a neck
//   float radius = spine.RadiusAt(0f);             // 0.62, the thickest vertebra

//
//   Chapters:
//     SHAPE THE SPINE     Vertebra values, bending, pushing, adding and removing vertebrae.
//     MEASURE THE SPINE   Length, shares, profile radius, height, slope, volume and the inside test.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public sealed class CreatureSpine
{
    // ===========================================================================
    // ===========================   SHAPE THE SPINE   ===========================
    // ===========================================================================
    //
    //   Vertebrae   Girth, height and position per vertebra, a fresh straight spine, copies.
    //   Girth       Sets the girth of one vertebra.
    //   Bending     Lifts one vertebra, the neighbors follow smoothly.
    //   Pushing     Moves one vertebra along the body between its neighbors.
    //   Middle      Adds or removes the middle vertebra.
    //   Ends        Adds or removes a vertebra at the nose or the tail end.
    //

    // ---------------------------------------------------------------------------
    // Vertebrae
    //   Girth, height and position per vertebra, a fresh straight spine, copies.
    // ---------------------------------------------------------------------------

    public const int MIN_VERTEBRAE = 4;
    public const int MAX_VERTEBRAE = 12;
    public const float MIN_GIRTH = 0.25f;
    public const float MAX_GIRTH = 1f;
    public const float MAX_LIFT = 1.1f;             // how far a vertebra can be pulled above or below the line
    private const float MAX_LIFT_STEP = 0.32f;      // height step between two neighbors, the spine stays a smooth curve
    public const float SPACING = 0.36f;             // distance between two vertebrae

    private readonly List<float> _girths = new();
    private readonly List<float> _lifts = new();
    private readonly List<float> _xs = new();       // position of every vertebra along the body, tail first, rising

    /// <summary>Number of vertebrae.</summary>
    public int Count => _girths.Count;

    /// <summary>Girth per vertebra, tail first, MIN_GIRTH..MAX_GIRTH.</summary>
    public IReadOnlyList<float> Girths => _girths;

    /// <summary>Height of every vertebra above the straight line, tail first, -MAX_LIFT..MAX_LIFT.</summary>
    public IReadOnlyList<float> Lifts => _lifts;

    /// <summary>Position of every vertebra along the body (X), tail first.</summary>
    public IReadOnlyList<float> Positions => _xs;

    public float MeanGirth => _girths.Count > 0 ? _girths.Average() : 0f;

    /// <summary>Replaces the spine with a straight one: the given girths, evenly spaced and centered.</summary>
    /// <param name="girths">(Required) Girth per vertebra, tail first, taken as they are.</param>
    /// <example><code>spine.Reset(new[] { 0.62f, 0.9f, 1f, 0.9f, 0.62f });   // the clay blob</code></example>
    public void Reset(IReadOnlyList<float> girths)
    {
        _girths.Clear();
        _lifts.Clear();
        _xs.Clear();
        _girths.AddRange(girths);
        _lifts.AddRange(new float[girths.Count]);   // a straight spine
        for (int i = 0; i < girths.Count; i++)
        {
            _xs.Add((i - (girths.Count - 1) * 0.5f) * SPACING);   // evenly spaced, centered
        }
    }

    /// <summary>Replaces every vertebra with those of another spine.</summary>
    /// <param name="other">(Required) The spine copied.</param>
    public void CopyFrom(CreatureSpine other)
    {
        _girths.Clear();
        _girths.AddRange(other._girths);
        _lifts.Clear();
        _lifts.AddRange(other._lifts);
        _xs.Clear();
        _xs.AddRange(other._xs);
    }

    // ---------------------------------------------------------------------------
    // Girth
    //   Sets the girth of one vertebra.
    // ---------------------------------------------------------------------------

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
    }

    // ---------------------------------------------------------------------------
    // Bending
    //   Lifts one vertebra, the neighbors follow smoothly.
    // ---------------------------------------------------------------------------

    /*
      Bending the spine: the dragged vertebra takes the new height, its neighbors follow
      outwards as far as needed so no step between two of them exceeds MAX_LIFT_STEP.
      Pulling the nose up raises a neck, dropping the tail end lets the tail hang.
    */
    /// <summary>Lifts one vertebra, the neighbors follow smoothly.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="lift">(Required) Height above the straight line, clamped to -MAX_LIFT..MAX_LIFT.</param>
    /// <example><code>spine.SetLift(spine.Count - 1, 0.6f);   // a neck</code></example>
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
    }

    // ---------------------------------------------------------------------------
    // Pushing
    //   Moves one vertebra along the body between its neighbors.
    // ---------------------------------------------------------------------------

    /*
      Vertebrae can be pushed along the body: a short neck and a long trunk. Each one keeps
      MIN_GAP to its neighbors, inner ones stay between them, the ends at most MAX_GAP away.
      The body re-centers only when vertebrae are added or removed (Recenter), so the gizmo
      stays on the dragged vertebra.
    */
    public const float MIN_GAP = SPACING * 0.45f;
    public const float MAX_GAP = SPACING * 1.8f;

    /// <summary>Moves one vertebra along the body, clamped between its neighbors.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    /// <param name="x">(Required) Wanted X in unit space.</param>
    /// <returns>The X the vertebra took.</returns>
    /// <example><code>spine.SetVertebraX(2, spine.Positions[2] + 0.1f);</code></example>
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

    // ---------------------------------------------------------------------------
    // Middle
    //   Adds or removes the middle vertebra.
    // ---------------------------------------------------------------------------

    /// <summary>Inserts a vertebra in the middle, as thick and high as its neighbors.</summary>
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
        return true;
    }

    // ---------------------------------------------------------------------------
    // Ends
    //   Adds or removes a vertebra at the nose or the tail end.
    // ---------------------------------------------------------------------------

    /*
      Pulling an end out grows the body there: a new vertebra continues the end (slightly
      thinner, the height continues the curve). The parts of the design are remapped by
      bone so they keep their place on the body (PartPlacement.ShareAfterSpineChange).
    */
    /// <summary>Adds a vertebra at the nose or the tail end.</summary>
    /// <param name="isNose">(Required) True grows the front (a neck), false the tail.</param>
    /// <returns>False at MAX_VERTEBRAE.</returns>
    /// <example><code>spine.GrowEnd(isNose: true);</code></example>
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
        return true;
    }

    // ===========================================================================
    // ==========================   MEASURE THE SPINE   ==========================
    // ===========================================================================
    //
    //   Extent   Length of the vertebra span, the ends and the tips of the caps.
    //   Shares   Spine share and bone number of an X and back.
    //   Curves   Profile radius, spine height and slope at an X.
    //   Body     Volume of the lofted body and the inside test.
    //

    /*
      The body is a loft along X: one ring per spine point, the ring radius follows a
      smooth curve (Catmull-Rom) through the vertebra girths. Behind the last vertebra
      the tail cap tapers off over TAIL_CAP, in front of the first the nose closes as a
      quarter ellipse. A ring is an ellipse, taller than wide for a fish, round and
      slightly flat for a marine mammal (CreatureDesign.HeightScale and WidthScale).
      The vertebrae sit at their own X (Positions, SPACING apart when fresh). The curves
      run over the bone number s (0 tail .. count - 1 nose); s of an X comes from the
      positions piecewise linear, outside on the caps it continues with the end gap.

        tail tip   vertebrae (about SPACING apart)    nose tip
           <====(  o    o    o   o  o  )====>
    */

    // ---------------------------------------------------------------------------
    // Extent
    //   Length of the vertebra span, the ends and the tips of the caps.
    // ---------------------------------------------------------------------------

    public const float MAX_RADIUS = 0.62f;          // profile radius of girth 1
    private const float TAIL_CAP = 0.3f;
    private const float MIN_NOSE_CAP = 0.12f;
    private const float MIN_PROFILE = 0.04f;

    /// <summary>Length of the vertebra span, the caps come on top.</summary>
    public float Length => _xs[^1] - _xs[0];

    public float HalfLength => Length * 0.5f;

    /// <summary>X of the last vertebra at the tail.</summary>
    public float StartX => _xs[0];

    /// <summary>X of the first vertebra at the nose.</summary>
    public float EndX => _xs[^1];

    public float TailTipX => _xs[0] - TAIL_CAP;

    public float NoseTipX => _xs[^1] + NoseCap;

    private float NoseCap => Mathf.Max(MIN_NOSE_CAP, _girths[^1] * MAX_RADIUS);

    // ---------------------------------------------------------------------------
    // Shares
    //   Spine share and bone number of an X and back.
    // ---------------------------------------------------------------------------

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

    // ---------------------------------------------------------------------------
    // Curves
    //   Profile radius, spine height and slope at an X.
    // ---------------------------------------------------------------------------

    /// <summary>Profile radius at an X, 0 beyond the tips.</summary>
    /// <example><code>spine.RadiusAt(0f)  // 0.62 for the clay blob, the thickest vertebra</code></example>
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
    /// <example><code>spine.LiftAt(spine.NoseTipX)   // the height of the head</code></example>
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

    // ---------------------------------------------------------------------------
    // Body
    //   Volume of the lofted body and the inside test.
    // ---------------------------------------------------------------------------

    private const int VOLUME_STEPS = 64;

    /// <summary>Volume of the lofted body in unit space: elliptic slices from tail tip to nose tip.</summary>
    /// <param name="heightScale">(Required) Height of a ring per profile radius.</param>
    /// <param name="widthScale">(Required) Width of a ring per profile radius.</param>
    /// <example><code>spine.Volume(1f, 0.72f)   // about 1.3 for the clay blob of a fish</code></example>
    public float Volume(float heightScale, float widthScale)
    {
        float step   = (NoseTipX - TailTipX) / VOLUME_STEPS;
        float volume = 0f;
        for (int i = 0; i < VOLUME_STEPS; i++)
        {
            float radius = RadiusAt(TailTipX + (i + 0.5f) * step);
            volume += Mathf.Pi * radius * radius * heightScale * widthScale * step;
        }
        return volume;
    }

    /// <summary>Whether a point in unit space lies inside the body (the swim wave and the ring tilt ignored).</summary>
    /// <param name="point">(Required) Point in unit space.</param>
    /// <param name="heightScale">(Required) Height of a ring per profile radius.</param>
    /// <param name="widthScale">(Required) Width of a ring per profile radius.</param>
    public bool Contains(Vector3 point, float heightScale, float widthScale)
    {
        float radius = RadiusAt(point.X);
        if (radius <= 0f)
        {
            return false;
        }
        float y = (point.Y - LiftAt(point.X)) / (radius * heightScale);
        float z = point.Z / (radius * widthScale);
        return y * y + z * z <= 1f;
    }
}
