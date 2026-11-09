// NOTE:
//   The design of a cell: every placed part with its position in unit space
//   (cell radius 1, +X forward). Rim parts snap onto the membrane, inner parts stay
//   inside. It also holds the look: a custom color and the outline, eight radii around
//   the cell that the editor stretches and squashes. Every cell of a divided cluster
//   carries the same design. Edited by the CellEditor on a copy, committed to the Player
//   when the editor closes.
//
//   var design = new CellDesign();
//   design.TryPlace(PartKind.Spike, new Vector2(1f, 0f), paidCost: 1);   // spike pointing forward

//
//   Chapters:
//     PLACED PART    One part with position and the points paid for it.
//     CELL DESIGN    Placement rules, removal, lean and copies.
//     LOOK           Custom color and the stretched or squashed outline.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// =============================   PLACED PART   =============================
// ===========================================================================
//
//   PlacedPart   Kind, unit position and the points paid (refunded on removal).
//

/// <summary>One placed part. Angle 0 is forward, the angle grows clockwise on screen.</summary>
public readonly record struct PlacedPart(PartKind Kind, Vector2 Position, int PaidCost)
{
    public float Angle => Position.Angle();
    public PartInfo Info => PartCatalog.Get(Kind);
}

public sealed class CellDesign
{
    // ===========================================================================
    // =============================   CELL DESIGN   =============================
    // ===========================================================================
    //
    //   Parts        Every placed part, Version counts each change for the 3D rebuild.
    //   TrySnap      Where a click would place a part, or false when it does not fit.
    //   TryPlace     Snaps and adds the part.
    //   RemoveNear   Removes the part closest to a click and returns it for the refund.
    //   Lean         Cost weighted diet lean of all parts, 0 without parts.
    //   Count        Number of parts of one kind.
    //   CopyFrom     Replaces every part with the parts of another design.
    //

    private const float INNER_MAX_RADIUS = 0.6f;     // inner parts stay well inside the membrane
    private const float MIN_INNER_GAP = 0.28f;
    private const float MIN_RIM_GAP = 0.35f;         // radians between two rim parts
    private const float REMOVE_DISTANCE = 0.35f;
    private const float CENTER_DEAD_ZONE = 0.05f;    // a rim click right in the center has no direction

    private readonly List<PlacedPart> _parts = new();

    public IReadOnlyList<PlacedPart> Parts => _parts;
    public int Version { get; private set; }

    /// <summary>Where a click would place the part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="point">(Required) Click position in unit space.</param>
    /// <param name="snapped">The snapped position on success.</param>
    /// <returns>False when the part does not fit there (too close to another part, no free slot).</returns>
    public bool TrySnap(PartKind kind, Vector2 point, out Vector2 snapped)
    {
        snapped = Vector2.Zero;
        switch (PartCatalog.Get(kind).Placement)
        {
            // Rim: onto the membrane in the clicked direction, keeping a gap to other rim parts
            case Placement.Rim:
                if (point.Length() < CENTER_DEAD_ZONE)
                {
                    return false;
                }
                float angle = point.Angle();
                bool isRimTaken = _parts.Any(part => Placement.Rim == part.Info.Placement
                    && Mathf.Abs(Mathf.Wrap(part.Angle - angle, -Mathf.Pi, Mathf.Pi)) < MIN_RIM_GAP);
                snapped = Vector2.FromAngle(angle);
                return !isRimTaken;

            // Inner: clamped inside the membrane, keeping a gap to other inner parts
            default:
                Vector2 inner = point.LimitLength(INNER_MAX_RADIUS);
                bool isInnerTaken = _parts.Any(part => Placement.Inner == part.Info.Placement
                    && part.Position.DistanceTo(inner) < MIN_INNER_GAP);
                snapped = inner;
                return !isInnerTaken;
        }
    }

    /// <summary>Snaps and adds a part.</summary>
    /// <param name="kind">(Required) The part to place.</param>
    /// <param name="point">(Required) Click position in unit space.</param>
    /// <param name="paidCost">(Required) Points paid, refunded on removal. 0 for free starter cells.</param>
    /// <returns>True when the part was placed.</returns>
    public bool TryPlace(PartKind kind, Vector2 point, int paidCost)
    {
        if (!TrySnap(kind, point, out Vector2 snapped))
        {
            return false;
        }
        _parts.Add(new PlacedPart(kind, snapped, paidCost));
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

    /// <summary>Replaces every part, the color and the outline with those of another design.</summary>
    /// <param name="other">(Required) The design to copy.</param>
    public void CopyFrom(CellDesign other)
    {
        _parts.Clear();
        _parts.AddRange(other._parts);
        Color = other.Color;
        other._shape.CopyTo(_shape, 0);
        Version++;
    }

    // ===========================================================================
    // ================================   LOOK   =================================
    // ===========================================================================
    //
    //   Color         Custom membrane color, null keeps the color of the stage.
    //   SetColor      Picks a color or goes back to the stage color.
    //   Shape         Radii of the outline at SHAPE_POINTS evenly spaced angles, 1 = round.
    //   SetShape      Replaces the outline, every radius clamped to MIN_SHAPE..MAX_SHAPE.
    //   ShapeRadius   Radius of the outline at any angle, smoothly interpolated.
    //

    public const int SHAPE_POINTS = 8;               // one radius every 45°, starting forward (+X)
    public const float MIN_SHAPE = 0.7f;
    public const float MAX_SHAPE = 1.5f;

    private readonly float[] _shape = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    public Color? Color { get; private set; }
    public IReadOnlyList<float> Shape => _shape;

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
        if (SHAPE_POINTS != radii.Count)
        {
            throw new System.ArgumentException($"radii must hold {SHAPE_POINTS} values, got {radii.Count}.", nameof(radii));
        }
        for (int i = 0; i < SHAPE_POINTS; i++)
        {
            _shape[i] = Mathf.Clamp(radii[i], MIN_SHAPE, MAX_SHAPE);
        }
        Version++;
    }

    /// <summary>
    /// Periodic Catmull-Rom spline through the shape radii: it passes exactly through each
    /// radius at its angle and runs smoothly in between, so a stretched cell has no corners.
    /// shaders/membrane.gdshader does the same on the GPU.
    /// </summary>
    /// <param name="angle">(Required) Angle in radians, 0 forward, like PlacedPart.Angle.</param>
    /// <example><code>design.ShapeRadius(0f)  // radius of the front</code></example>
    public float ShapeRadius(float angle)
    {
        float t  = Mathf.PosMod(angle / Mathf.Tau, 1f) * SHAPE_POINTS;
        int   i  = Mathf.FloorToInt(t);
        float f  = t - i;
        float p0 = _shape[(i - 1 + SHAPE_POINTS) % SHAPE_POINTS];
        float p1 = _shape[i % SHAPE_POINTS];
        float p2 = _shape[(i + 1) % SHAPE_POINTS];
        float p3 = _shape[(i + 2) % SHAPE_POINTS];
        float radius = 0.5f * (2f * p1 + (p2 - p0) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (3f * p1 - p0 - 3f * p2 + p3) * f * f * f);
        return Mathf.Clamp(radius, MIN_SHAPE * 0.9f, MAX_SHAPE * 1.05f);
    }
}
