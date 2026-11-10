// NOTE:
//   Geometry of the cell membrane as the editor needs it: which point of the stretched
//   membrane a ray hits, where a placed part or a height handle sits and how a pull on
//   the edge or on a height handle spreads to the neighboring shape points. Pure math
//   on a CellDesign, no node and no engine call, so it runs in a unit test.
//
//   Vector2 unit = MembraneGeometry.ToUnit(camera.ProjectRayOrigin(pixel), camera.ProjectRayNormal(pixel));
//   float[] radii = MembraneGeometry.StretchedShape(shapeBefore, grabAngle, grabRadius, unit);

//
//   Chapters:
//     CAST RAYS         A camera ray against the membrane and the plane.
//     LOCATE ON CELL    Where a placed part and a height handle sit.
//     SCULPT MEMBRANE   The edge band and how a pull spreads to the neighbors.
//

using Godot;

namespace CellOut;

public static class MembraneGeometry
{
    // ===========================================================================
    // ==============================   CAST RAYS   ==============================
    // ===========================================================================
    //
    //   ToUnit          A ray to unit space by a hit on the round membrane,
    //                   outside it against the plane.
    //   TryHitSurface   The point of the stretched membrane a ray hits with its height (lift), false on a miss.
    //   RayEllipsoid    First hit of a ray on the membrane ellipsoid of one outline radius and height.
    //

    private const int SURFACE_RAY_STEPS = 3;         // refinements of the outline radius, the stretch changes slowly

    /// <summary>
    /// The core membrane is an ellipsoid with radius 1 and height FLATTEN. Scaling the
    /// ray's Y by 1 / FLATTEN turns it into the unit sphere, the first hit is the point
    /// under the cursor. A ray that misses the cell (rim parts) hits the plane y = 0.
    /// </summary>
    /// <param name="origin">(Required) Start of the ray in the preview world.</param>
    /// <param name="direction">(Required) Normalized direction of the ray.</param>
    /// <returns>The hit seen from above, in unit space.</returns>
    public static Vector2 ToUnit(Vector3 origin, Vector3 direction)
    {
        // Core membrane as unit sphere
        Vector3? hit = RayEllipsoid(origin, direction, 1f, CellVisual3D.FLATTEN);
        if (null != hit)
        {
            return new Vector2(hit.Value.X, hit.Value.Z);
        }

        // Plane y = 0, a ray parallel to it gets the center (places nothing on the rim)
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return Vector2.Zero;
        }
        Vector3 planeHit = origin + direction * (-origin.Y / direction.Y);
        return new Vector2(planeHit.X, planeHit.Z);
    }

    /*
      Like ToUnit, but against the real membrane: the ellipsoid stretched by the outline
      radius S and the membrane height H at the hit angle. Both depend on the hit, so the
      ray is cast against the ellipsoid of the last guess and refined SURFACE_RAY_STEPS
      times, starting round. The lift is the hit height in sphere units (-1 bottom ..
      1 top). A ray that misses the membrane hits nothing, like the creature editor.
    */
    /// <summary>The point of the membrane a ray hits.</summary>
    /// <param name="design">(Required) The design whose membrane is hit.</param>
    /// <param name="origin">(Required) Start of the ray in the preview world.</param>
    /// <param name="direction">(Required) Normalized direction of the ray.</param>
    /// <param name="point">The hit seen from above, in unit space.</param>
    /// <param name="lift">Height of the hit, the sine of its latitude.</param>
    /// <param name="surface">The hit in 3D, for the hover dot.</param>
    /// <returns>False when the ray misses the membrane.</returns>
    public static bool TryHitSurface(CellDesign design, Vector3 origin, Vector3 direction, out Vector2 point, out float lift, out Vector3 surface)
    {
        point   = Vector2.Zero;
        lift    = 0f;
        surface = Vector3.Zero;
        float   stretch = 1f;
        float   height  = CellVisual3D.FLATTEN;
        Vector3? hit    = null;
        for (int step = 0; step < SURFACE_RAY_STEPS; step++)
        {
            Vector3? next = RayEllipsoid(origin, direction, stretch, height);
            if (null == next)
            {
                break;
            }
            float angle = Mathf.Atan2(next.Value.Z, next.Value.X);
            hit     = next;
            stretch = design.ShapeRadius(angle);
            height  = design.MembraneHeight(angle, new Vector2(next.Value.X, next.Value.Z).Length() / stretch);
        }

        if (null == hit)
        {
            return false;
        }
        point   = new Vector2(hit.Value.X, hit.Value.Z);
        lift    = Mathf.Clamp(hit.Value.Y / height, -1f, 1f);
        surface = hit.Value;
        return true;
    }

    /// <summary>First hit of a ray on the ellipsoid with outline radius stretch and half height height, null on a miss.</summary>
    public static Vector3? RayEllipsoid(Vector3 origin, Vector3 direction, float stretch, float height)
    {
        var scale = new Vector3(1f / stretch, 1f / height, 1f / stretch);
        Vector3 o = origin * scale;
        Vector3 d = direction * scale;
        float a = d.Dot(d);
        float b = 2f * o.Dot(d);
        float c = o.Dot(o) - 1f;
        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            return null;
        }
        float t = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
        return t > 0f ? origin + direction * t : null;
    }

    // ===========================================================================
    // ===========================   LOCATE ON CELL   ============================
    // ===========================================================================
    //
    //   PartPoint             Where a placed part sits on the cell, for the ring burst.
    //   HeightHandlePosition  Where a height handle floats: above the upper membrane at its angle.
    //

    private const float HEIGHT_HANDLE_LIFT = 0.7f;   // handles sit over the upper membrane at this lift
    private const float HEIGHT_HANDLE_GAP = 0.1f;    // floating above the surface

    /// <summary>Where a placed part sits on the cell: on the membrane for rim parts, above an inner part.</summary>
    /// <param name="design">(Required) The design the part belongs (or belonged) to.</param>
    /// <param name="part">(Required) The placed part.</param>
    public static Vector3 PartPoint(CellDesign design, PlacedPart part)
    {
        if (Placement.Rim == part.Info.Placement)
        {
            return design.SurfacePoint(part.Angle, part.Lift);
        }
        return Plane3D.To3D(part.Position, Mathf.Max(design.TopHeight(part.Position), 0.05f));
    }

    /// <summary>Where a height handle floats above the membrane.</summary>
    /// <param name="design">(Required) The design whose membrane carries the handle.</param>
    /// <param name="index">(Required) Handle, 0 forward, counting like CellDesign.Heights.</param>
    public static Vector3 HeightHandlePosition(CellDesign design, int index)
    {
        float angle = Mathf.Tau * index / CellDesign.SHAPE_POINTS;
        return design.SurfacePoint(angle, HEIGHT_HANDLE_LIFT) + Vector3.Up * HEIGHT_HANDLE_GAP;
    }

    // ===========================================================================
    // ===========================   SCULPT MEMBRANE   ===========================
    // ===========================================================================
    //
    //   IsOnEdge         Whether a unit point lies on the band around the membrane edge.
    //   StretchedShape   The outline with the grabbed edge moved to the cursor, neighbors follow softly.
    //   RaisedHeights    The heights raised or flattened at one handle, neighbors follow softly.
    //   EdgeWeight       How much a shape point follows the grab, 1 at it, 0 beyond EDGE_FALLOFF.
    //

    private const float EDGE_INNER = 0.75f;          // band around the edge, as share of the outline radius
    private const float EDGE_OUTER = 1.35f;
    private const float EDGE_FALLOFF = 1.2f;         // radians: neighbors this far away do not follow any more

    /// <summary>Whether a unit point lies on the band around the membrane edge of a design.</summary>
    public static bool IsOnEdge(CellDesign design, Vector2 point)
    {
        float radius   = design.ShapeRadius(point.Angle());
        float distance = point.Length();
        return distance >= radius * EDGE_INNER && distance <= radius * EDGE_OUTER;
    }

    /// <summary>
    /// The edge under the cursor goes to the cursor's distance along the grabbed angle.
    /// Every shape point moves by that change times its EdgeWeight to the grab, so lumpy,
    /// lopsided cells are possible. CellDesign.SetShape clamps the radii.
    /// </summary>
    /// <param name="shapeBefore">(Required) The outline radii before the drag, CellDesign.SHAPE_POINTS of them.</param>
    /// <param name="grabAngle">(Required) Angle of the grabbed edge in radians.</param>
    /// <param name="grabRadius">(Required) Outline radius at the grab before the drag.</param>
    /// <param name="point">(Required) The cursor in unit space.</param>
    /// <returns>The new outline radii for CellDesign.SetShape.</returns>
    public static float[] StretchedShape(float[] shapeBefore, float grabAngle, float grabRadius, Vector2 point)
    {
        float target = Mathf.Clamp(point.Dot(Vector2.FromAngle(grabAngle)), CellDesign.MIN_SHAPE, CellDesign.MAX_SHAPE);
        float change = target - grabRadius;
        var   radii  = new float[CellDesign.SHAPE_POINTS];
        for (int i = 0; i < radii.Length; i++)
        {
            float angle  = Mathf.Tau * i / radii.Length;
            radii[i] = shapeBefore[i] + change * EdgeWeight(angle, grabAngle);
        }
        return radii;
    }

    /// <summary>The height factors of a design raised (or with a negative change flattened) at one handle.</summary>
    /// <param name="design">(Required) The design whose heights are the start.</param>
    /// <param name="index">(Required) Handle, 0 forward, counting like CellDesign.Heights.</param>
    /// <param name="change">(Required) Change of the height factor at the handle, neighbors get EdgeWeight of it.</param>
    /// <returns>The new height factors for CellDesign.SetHeights.</returns>
    public static float[] RaisedHeights(CellDesign design, int index, float change)
    {
        if (index < 0 || index >= CellDesign.SHAPE_POINTS)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be between 0 and {CellDesign.SHAPE_POINTS - 1}, got '{index}'.");
        }
        float grab    = Mathf.Tau * index / CellDesign.SHAPE_POINTS;
        var   heights = new float[CellDesign.SHAPE_POINTS];
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] = design.Heights[i] + change * EdgeWeight(Mathf.Tau * i / heights.Length, grab);
        }
        return heights;
    }

    /// <summary>How much a shape point follows a grab: 1 at the grab, easing to 0 at EDGE_FALLOFF radians.</summary>
    public static float EdgeWeight(float angle, float grab)
    {
        float distance = Mathf.Abs(Mathf.Wrap(angle - grab, -Mathf.Pi, Mathf.Pi));
        return distance >= EDGE_FALLOFF ? 0f : (Mathf.Cos(distance / EDGE_FALLOFF * Mathf.Pi) + 1f) * 0.5f;
    }
}
