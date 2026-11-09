// NOTE:
//   The design of a cell: every placed part with its position in unit space
//   (cell radius 1, +X forward). Rim parts snap onto the membrane anywhere around and
//   up or down it (angle plus Lift), inner parts stay inside. It also holds the look:
//   a custom color, the outline (eight radii around the cell that the editor stretches
//   and squashes) and eight heights at the same angles that the editor raises and
//   flattens. Every cell of a divided cluster
//   carries the same design. Edited by the CellEditor on a copy, committed to the Player
//   when the editor closes.
//
//   var design = new CellDesign();
//   design.TryPlace(PartKind.Spike, new Vector2(1f, 0f), paidCost: 1);   // spike pointing forward

//
//   Chapters:
//     PLACED PART    One part with position and the points paid for it.
//     CELL DESIGN    Placement rules, removal, lean and copies.
//     LOOK           Custom color, the stretched or squashed outline and the heights.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// =============================   PLACED PART   =============================
// ===========================================================================
//
//   PlacedPart   Kind, unit position, the points paid (refunded on removal) and the
//                pose set with the gizmo of the editor.
//

/// <summary>One placed part. Angle 0 is forward, the angle grows clockwise on screen.</summary>
/// <param name="Offset">Shift of the model in 3D unit space of the cell (gizmo arrows). Looks only.</param>
/// <param name="Rotation">Euler turn in radians around the center of the model (gizmo rings). Looks only.</param>
/// <param name="Lift">Height of a rim part on the membrane, the sine of its latitude: 0 equator, 1 top, -1 bottom. Inner parts keep 0.</param>
public readonly record struct PlacedPart(
    PartKind Kind,
    Vector2 Position,
    int PaidCost,
    Vector3 Offset = default,
    Vector3 Rotation = default,
    float Lift = 0f)
{
    public float Angle => Position.Angle();
    public PartInfo Info => PartCatalog.Get(Kind);

    /// <summary>Direction of a rim part on the unit sphere (the membrane before flattening and stretching, see CellDesign.SurfacePoint).</summary>
    public Vector3 Direction => CellDesign.RimDirection(Angle, Lift);
}

public sealed class CellDesign
{
    // ===========================================================================
    // =============================   CELL DESIGN   =============================
    // ===========================================================================
    //
    //   Parts        Every placed part, Version counts each change for the 3D rebuild.
    //   TrySnap      Where a click would place a part, slid to the closest free spot, false when none is near.
    //   TrySnapRim   Closest free rim spot around a direction on the membrane sphere.
    //   TrySnapInner Closest free inner spot around a point.
    //   TryPlace     Snaps and adds the part.
    //   RemoveNear   Removes the part closest to a click and returns it for the refund.
    //   RemovePart   Removes one part by index and returns it for the refund.
    //   SetPartPose  Shift and turn of one part (gizmo), looks only.
    //   Lean         Cost weighted diet lean of all parts, 0 without parts.
    //   Count        Number of parts of one kind.
    //   CopyFrom     Replaces every part with the parts of another design.
    //   ForCritter   A random design for an NPC cell, parts by kind, more with the stage.
    //   PlaceRandom  One part at a random spot that suits it.
    //

    private const float INNER_MAX_RADIUS = 0.6f;     // inner parts stay well inside the membrane
    private const float MIN_INNER_GAP = 0.28f;
    private const float MIN_RIM_GAP = 0.35f;         // radians between two rim parts on the membrane sphere
    private const float REMOVE_DISTANCE = 0.35f;
    private const float CENTER_DEAD_ZONE = 0.05f;    // a rim click right in the center has no direction
    public const float MAX_RIM_LIFT = 1f;            // the poles count too: a part there points straight up or down

    private readonly List<PlacedPart> _parts = new();

    public IReadOnlyList<PlacedPart> Parts => _parts;
    public int Version { get; private set; }

    /// <summary>Direction on the unit sphere for a rim angle and a lift (sine of the latitude).</summary>
    /// <param name="angle">(Required) Rim angle in radians, 0 forward.</param>
    /// <param name="lift">(Required) -1 bottom .. 0 equator .. 1 top.</param>
    /// <example><code>CellDesign.RimDirection(0f, 0f)   // (1, 0, 0), forward on the equator</code></example>
    public static Vector3 RimDirection(float angle, float lift)
    {
        float around = Mathf.Sqrt(Mathf.Max(0f, 1f - lift * lift));
        return new Vector3(Mathf.Cos(angle) * around, lift, Mathf.Sin(angle) * around);
    }

    /// <summary>Where a click would place the part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="point">(Required) Click position in unit space, seen from above.</param>
    /// <param name="snapped">The snapped position on success.</param>
    /// <param name="snappedLift">The snapped lift on success, 0 for inner parts.</param>
    /// <param name="lift">(Optional) Height of the click on the membrane, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>False when no free spot is near the click (all taken within the slide range).</returns>
    public bool TrySnap(PartKind kind, Vector2 point, out Vector2 snapped, out float snappedLift, float lift = 0f)
    {
        snapped     = Vector2.Zero;
        snappedLift = 0f;
        if (Placement.Rim != PartCatalog.Get(kind).Placement)
        {
            return TrySnapInner(point, out snapped);
        }

        // A rim click on the equator right in the center has no direction (a lifted one points up or down)
        if (Mathf.IsZeroApprox(lift) && point.Length() < CENTER_DEAD_ZONE)
        {
            return false;
        }
        return TrySnapRim(RimDirection(point.Angle(), Mathf.Clamp(lift, -MAX_RIM_LIFT, MAX_RIM_LIFT)), out snapped, out snappedLift);
    }

    private const float RIM_SLIDE = 0.8f;            // radians a rim part slides at most to the next free spot
    private const float RIM_SLIDE_STEP = 0.06f;
    private const float INNER_SLIDE = 0.45f;         // unit distance an inner part slides at most
    private const float INNER_SLIDE_STEP = 0.04f;
    private const int SLIDE_DIRECTIONS = 16;         // candidates per ring around the click

    /*
      Sliding to the next free spot, so a click close to another part still places:
      > The clicked spot is free
          - Take it.
      > It is taken
          - Try rings of growing distance around the click, SLIDE_DIRECTIONS candidates
            each, and take the first free one: the closest free spot wins.
          - Beyond RIM_SLIDE (INNER_SLIDE) give up, the part does not fit there.
      Rim parts walk on the sphere (great circle away from the click), inner parts in the plane.
    */
    private bool TrySnapRim(Vector3 target, out Vector2 snapped, out float snappedLift)
    {
        // Two tangents at the click, the up tangent degenerates at the poles
        Vector3 side = Vector3.Up.Cross(target);
        side = side.LengthSquared() < 0.0001f ? Vector3.Back : side.Normalized();
        Vector3 up = target.Cross(side);

        for (float distance = 0f; distance <= RIM_SLIDE; distance += RIM_SLIDE_STEP)
        {
            int count = 0f == distance ? 1 : SLIDE_DIRECTIONS;
            for (int i = 0; i < count; i++)
            {
                float   turn      = Mathf.Tau * i / SLIDE_DIRECTIONS;
                Vector3 candidate = target * Mathf.Cos(distance) + (side * Mathf.Cos(turn) + up * Mathf.Sin(turn)) * Mathf.Sin(distance);
                float   angle     = Mathf.Atan2(candidate.Z, candidate.X);
                float   lift      = Mathf.Clamp(candidate.Y, -MAX_RIM_LIFT, MAX_RIM_LIFT);
                Vector3 direction = RimDirection(angle, lift);
                bool    isTaken   = _parts.Any(part => Placement.Rim == part.Info.Placement
                    && part.Direction.AngleTo(direction) < MIN_RIM_GAP);
                if (!isTaken)
                {
                    snapped     = Vector2.FromAngle(angle);
                    snappedLift = lift;
                    return true;
                }
            }
        }
        snapped     = Vector2.Zero;
        snappedLift = 0f;
        return false;
    }

    private bool TrySnapInner(Vector2 point, out Vector2 snapped)
    {
        for (float distance = 0f; distance <= INNER_SLIDE; distance += INNER_SLIDE_STEP)
        {
            int count = 0f == distance ? 1 : SLIDE_DIRECTIONS;
            for (int i = 0; i < count; i++)
            {
                Vector2 candidate = (point + Vector2.FromAngle(Mathf.Tau * i / SLIDE_DIRECTIONS) * distance).LimitLength(INNER_MAX_RADIUS);
                bool    isTaken   = _parts.Any(part => Placement.Inner == part.Info.Placement
                    && part.Position.DistanceTo(candidate) < MIN_INNER_GAP);
                if (!isTaken)
                {
                    snapped = candidate;
                    return true;
                }
            }
        }
        snapped = Vector2.Zero;
        return false;
    }

    /// <summary>Snaps and adds a part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="point">(Required) Click position in unit space, seen from above.</param>
    /// <param name="paidCost">(Required) Points paid, refunded on removal. 0 for free starter cells.</param>
    /// <param name="lift">(Optional) Height on the membrane for rim parts, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>True when the part was placed.</returns>
    /// <example><code>design.TryPlace(PartKind.Spike, new Vector2(1f, 0f), paidCost: 1, lift: 0.6f)   // spike forward, on the upper side</code></example>
    public bool TryPlace(PartKind kind, Vector2 point, int paidCost, float lift = 0f)
    {
        if (!TrySnap(kind, point, out Vector2 snapped, out float snappedLift, lift))
        {
            return false;
        }
        _parts.Add(new PlacedPart(kind, snapped, paidCost, Lift: snappedLift));
        Version++;
        return true;
    }

    /// <summary>Removes the part closest to a click.</summary>
    /// <param name="point">(Required) Click position in unit space.</param>
    /// <returns>The removed part, or null when nothing is close enough.</returns>
    public PlacedPart? RemoveNear(Vector2 point)
    {
        PlacedPart? closest = null;
        float bestDistance = float.PositiveInfinity;
        foreach (PlacedPart part in _parts)
        {
            float distance = part.Position.DistanceTo(point);
            if (distance < REMOVE_DISTANCE && distance < bestDistance)
            {
                closest      = part;
                bestDistance = distance;
            }
        }
        if (null == closest)
        {
            return null;
        }
        _parts.Remove(closest.Value);
        Version++;
        return closest;
    }

    /// <summary>Removes one placed part.</summary>
    /// <param name="index">(Required) Index in Parts.</param>
    /// <returns>The removed part for the refund.</returns>
    public PlacedPart RemovePart(int index)
    {
        if (index < 0 || index >= _parts.Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {_parts.Count - 1}, got '{index}'.");
        }
        PlacedPart removed = _parts[index];
        _parts.RemoveAt(index);
        Version++;
        return removed;
    }

    public const float MAX_PART_OFFSET = 0.8f;       // unit space, the gizmo cannot pull a part off the cell

    /// <summary>Sets shift and turn of one placed part (gizmo of the editor), the shift clamped to MAX_PART_OFFSET.</summary>
    /// <param name="index">(Required) Index in Parts.</param>
    /// <param name="offset">(Required) Shift in 3D unit space of the cell.</param>
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

    /// <summary>Cost weighted lean of all parts: -1 only herbivore parts .. 1 only carnivore parts.</summary>
    public float Lean()
    {
        float weight = _parts.Sum(part => part.Info.Cost);
        if (weight <= 0f)
        {
            return 0f;
        }
        return _parts.Sum(part => part.Info.Lean * part.Info.Cost) / weight;
    }

    public int Count(PartKind kind)
    {
        return _parts.Count(part => kind == part.Kind);
    }

    /// <summary>Replaces every part, the color, the outline and the heights with those of another design.</summary>
    /// <param name="other">(Required) The design to copy.</param>
    public void CopyFrom(CellDesign other)
    {
        _parts.Clear();
        _parts.AddRange(other._parts);
        Color = other.Color;
        other._shape.CopyTo(_shape, 0);
        other._heights.CopyTo(_heights, 0);
        Version++;
    }

    private const int MAX_CRITTER_EXTRAS = 3;       // extra parts of an NPC cell besides its signature part

    /*
      NPC cells carry the same parts as the player, picked by their kind:
      - Bacterium:  a flagellum at the back, extras: chloroplast, filter mouth, cilia, vacuole, flagellum
      - Amoeba:     cilia, extras: vacuole, eyespot, filter mouth, armor plate, flagellum, cilia
      - Hunter:     a spike in front, extras: jaw, toxin, spike, flagellum, eyespot, armor plate
      Extras: one per stage of the player (up to MAX_CRITTER_EXTRAS) plus a coin flip for one more.
      Organisms carry no cell parts, their creature body counts.
    */
    /// <summary>A random design for an NPC cell, its parts work like the player's (CellStats).</summary>
    /// <param name="kind">(Required) Sets the signature part and the pool of extras.</param>
    /// <param name="stage">(Required) Stage of the player, every stage adds an extra part.</param>
    /// <example><code>CellDesign.ForCritter(CritterKind.Hunter, 2).Count(PartKind.Spike)   // at least 1</code></example>
    public static CellDesign ForCritter(CritterKind kind, int stage)
    {
        var design = new CellDesign();
        if (stage >= Player.ORGANISM_STAGE)
        {
            return design;
        }

        (PartKind signature, PartKind[] extras) = kind switch
        {
            CritterKind.Bacterium => (PartKind.Flagellum, new[] { PartKind.Chloroplast, PartKind.FilterMouth, PartKind.Cilia, PartKind.Vacuole, PartKind.Flagellum }),
            CritterKind.Amoeba    => (PartKind.Cilia, new[] { PartKind.Vacuole, PartKind.Eyespot, PartKind.FilterMouth, PartKind.ArmorPlate, PartKind.Flagellum, PartKind.Cilia }),
            CritterKind.Hunter    => (PartKind.Spike, new[] { PartKind.Jaw, PartKind.Toxin, PartKind.Spike, PartKind.Flagellum, PartKind.Eyespot, PartKind.ArmorPlate }),
            _ => throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be a known CritterKind, got '{kind}'."),
        };
        design.PlaceRandom(signature);
        int count = Mathf.Min(stage, MAX_CRITTER_EXTRAS) + (Rand.Chance(0.5f) ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            design.PlaceRandom(extras[Rand.Index(extras.Length)]);
        }
        return design;
    }

    private const int PLACE_TRIES = 6;

    /// <summary>
    /// One part at a random spot that suits it: flagella at the back, spikes and jaws in
    /// front, other rim parts anywhere around, inner parts anywhere inside. Free, paid 0.
    /// </summary>
    private void PlaceRandom(PartKind kind)
    {
        for (int attempt = 0; attempt < PLACE_TRIES; attempt++)
        {
            if (Placement.Inner == PartCatalog.Get(kind).Placement)
            {
                if (TryPlace(kind, Vector2.FromAngle(Rand.Angle()) * Rand.Range(0f, INNER_MAX_RADIUS), 0))
                {
                    return;
                }
                continue;
            }
            float angle = kind switch
            {
                PartKind.Flagellum               => Mathf.Pi + Rand.Range(-0.6f, 0.6f),
                PartKind.Spike or PartKind.Jaw   => Rand.Range(-0.7f, 0.7f),
                _                                => Rand.Angle(),
            };
            if (TryPlace(kind, Vector2.FromAngle(angle), 0, Rand.Range(-0.2f, 0.5f)))
            {
                return;
            }
        }
    }

    // ===========================================================================
    // ================================   LOOK   =================================
    // ===========================================================================
    //
    //   Color           Custom membrane color, null keeps the color of the stage.
    //   SetColor        Picks a color or goes back to the stage color.
    //   Shape           Radii of the outline at SHAPE_POINTS evenly spaced angles, 1 = round.
    //   SetShape        Replaces the outline, every radius clamped to MIN_SHAPE..MAX_SHAPE.
    //   ShapeRadius     Radius of the outline at any angle, smoothly interpolated.
    //   Heights         Height factors of the membrane at the same angles, 1 = FLATTEN.
    //   SetHeights      Replaces the heights, every factor clamped to MIN_HEIGHT..MAX_HEIGHT.
    //   HeightScale     Height factor at any angle, smoothly interpolated.
    //   MembraneHeight  Half height of the membrane above its center at an angle, in unit space.
    //   SurfacePoint    Point on the membrane for a rim angle and lift.
    //   TopHeight       Height of the membrane top above a unit point, 0 outside.
    //   Spline          The periodic Catmull-Rom both outline and heights run through.
    //

    public const int SHAPE_POINTS = 8;               // one radius every 45°, starting forward (+X)
    public const float MIN_SHAPE = 0.7f;
    public const float MAX_SHAPE = 1.5f;
    public const float MIN_HEIGHT = 0.5f;            // times CellVisual3D.FLATTEN: a pancake
    public const float MAX_HEIGHT = 1.8f;            // about a ball

    private readonly float[] _shape = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
    private readonly float[] _heights = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    public Color? Color { get; private set; }
    public IReadOnlyList<float> Shape => _shape;
    public IReadOnlyList<float> Heights => _heights;

    /// <summary>Picks the membrane color, null goes back to the color of the stage.</summary>
    public void SetColor(Color? color)
    {
        Color = color;
        Version++;
    }

    /// <summary>Replaces the outline.</summary>
    /// <param name="radii">(Required) SHAPE_POINTS radii, index 0 forward, counting like the part angles.</param>
    public void SetShape(IReadOnlyList<float> radii)
    {
        CopyClamped(radii, _shape, MIN_SHAPE, MAX_SHAPE, nameof(radii));
    }

    /// <summary>Replaces the heights of the membrane.</summary>
    /// <param name="heights">(Required) SHAPE_POINTS factors on CellVisual3D.FLATTEN, index 0 forward, like the radii.</param>
    /// <example><code>design.SetHeights(new[] { 0.6f, 0.8f, 1f, 1.5f, 1.8f, 1.5f, 1f, 0.8f })   // flat snout, high hump behind</code></example>
    public void SetHeights(IReadOnlyList<float> heights)
    {
        CopyClamped(heights, _heights, MIN_HEIGHT, MAX_HEIGHT, nameof(heights));
    }

    private void CopyClamped(IReadOnlyList<float> values, float[] target, float min, float max, string name)
    {
        if (SHAPE_POINTS != values.Count)
        {
            throw new System.ArgumentException($"{name} must hold {SHAPE_POINTS} values, got {values.Count}.", name);
        }
        for (int i = 0; i < SHAPE_POINTS; i++)
        {
            target[i] = Mathf.Clamp(values[i], min, max);
        }
        Version++;
    }

    /// <summary>Radius of the outline at an angle, see Spline.</summary>
    /// <param name="angle">(Required) Angle in radians, 0 forward, like PlacedPart.Angle.</param>
    /// <example><code>design.ShapeRadius(0f)  // radius of the front</code></example>
    public float ShapeRadius(float angle)
    {
        return Mathf.Clamp(Spline(_shape, angle), MIN_SHAPE * 0.9f, MAX_SHAPE * 1.05f);
    }

    /// <summary>Height factor of the membrane at an angle, see Spline.</summary>
    /// <param name="angle">(Required) Angle in radians, 0 forward.</param>
    public float HeightScale(float angle)
    {
        return Mathf.Clamp(Spline(_heights, angle), MIN_HEIGHT * 0.9f, MAX_HEIGHT * 1.05f);
    }

    /*
      The membrane is the unit sphere stretched by the outline radius S around and by
      the membrane height H up and down. A rim part at angle a and lift l (sine of its
      latitude) sits at
        p = (cos a * h * S, l * H, sin a * h * S),   h = sqrt(1 - l^2)
      At the poles every angle meets, so H(a) alone would tear the membrane into a crater
      with ridges. H therefore blends from the mean height M at the poles to H(a) at the
      equator, smoothly over the share h around the axis:
        H = FLATTEN * (M + (HeightScale(a) - M) * smoothstep(0, 1, h))
      CellVisual3D, shaders/membrane.gdshader, the CellEditor raycast and the ghost
      marker all use this one surface.
    */
    /// <summary>Half height of the membrane at an angle, in unit space, blended to the mean towards the poles.</summary>
    /// <param name="angle">(Required) Angle in radians, 0 forward.</param>
    /// <param name="around">(Optional) Share of the point around the axis, 0 at the poles .. 1 at the equator. Defaults to 1.</param>
    /// <example><code>design.MembraneHeight(Mathf.Pi)   // height of the back at the outline</code></example>
    public float MembraneHeight(float angle, float around = 1f)
    {
        float mean  = _heights.Average();
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(around, 0f, 1f));
        return CellVisual3D.FLATTEN * (mean + (HeightScale(angle) - mean) * blend);
    }

    /// <summary>Point on the membrane for a rim angle and lift.</summary>
    /// <param name="angle">(Required) Rim angle in radians, 0 forward.</param>
    /// <param name="lift">(Required) -1 bottom .. 0 equator .. 1 top.</param>
    /// <example><code>design.SurfacePoint(0f, 0f)   // front of the outline, (ShapeRadius(0), 0, 0)</code></example>
    public Vector3 SurfacePoint(float angle, float lift)
    {
        Vector3 direction = RimDirection(angle, lift);
        float   around    = Mathf.Sqrt(Mathf.Max(0f, 1f - lift * lift));
        float   stretch   = ShapeRadius(angle);
        return new Vector3(direction.X * stretch, direction.Y * MembraneHeight(angle, around), direction.Z * stretch);
    }

    /// <summary>Height of the membrane top above a unit point: H * sqrt(1 - (d / S)^2) inside the outline, 0 outside.</summary>
    /// <param name="point">(Required) Unit point seen from above.</param>
    public float TopHeight(Vector2 point)
    {
        float angle = point.Angle();
        float share = point.Length() / ShapeRadius(angle);
        return share < 1f ? MembraneHeight(angle, share) * Mathf.Sqrt(1f - share * share) : 0f;
    }

    /// <summary>
    /// Periodic Catmull-Rom spline through SHAPE_POINTS values: it passes exactly through each
    /// value at its angle and runs smoothly in between, so a stretched cell has no corners.
    /// shaders/membrane.gdshader does the same on the GPU.
    /// </summary>
    private static float Spline(float[] points, float angle)
    {
        float t  = Mathf.PosMod(angle / Mathf.Tau, 1f) * SHAPE_POINTS;
        int   i  = Mathf.FloorToInt(t);
        float f  = t - i;
        float p0 = points[(i - 1 + SHAPE_POINTS) % SHAPE_POINTS];
        float p1 = points[i % SHAPE_POINTS];
        float p2 = points[(i + 1) % SHAPE_POINTS];
        float p3 = points[(i + 2) % SHAPE_POINTS];
        return 0.5f * (2f * p1 + (p2 - p0) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (3f * p1 - p0 - 3f * p2 + p3) * f * f * f);
    }
}
