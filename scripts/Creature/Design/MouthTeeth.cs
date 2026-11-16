// NOTE:
//   Where the teeth of a mouth stand: on the edges of the opening the body mesh cuts for
//   its mouth kind (Design/MouthShapes.cs, Visual/CreatureClay.cs). The spots are computed
//   from the body itself, so the teeth sit on the lips of every head a player shapes, not
//   only on the head a model was built for. Pure placement, no nodes: the meshes are
//   built by Visual/CreatureParts3D.cs.
//
//   foreach (Tooth tooth in MouthTeeth.Place(design, shape)) { ... }

//
//   Chapters:
//     TYPES        One tooth and the rows of a mouth.
//     PLACE TEETH  The teeth along both lips of a carved opening.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   TeethRows   Which lips carry teeth.
//   Tooth       One tooth: root on the lip, direction, lip tangent and size.
//

public enum TeethRows { Both, Upper, Lower }

/// <summary>One tooth in unit space of the body.</summary>
/// <param name="Root">Where it grows out of the lip.</param>
/// <param name="Direction">Normalized, from the root to the tip (into the opening).</param>
/// <param name="Along">Normalized direction of the lip at the root, the tooth is wide along it.</param>
/// <param name="Length">Root to tip.</param>
/// <param name="Width">Width at the root along the lip.</param>
public readonly record struct Tooth(Vector3 Root, Vector3 Direction, Vector3 Along, float Length, float Width);

public static class MouthTeeth
{
    // ===========================================================================
    // =============================   PLACE TEETH   =============================
    // ===========================================================================
    //
    //   Place     The teeth of a mouth shape on a body, both flanks.
    //   LipPoint  A point on the upper or lower lip of the opening.
    //

    private const float FIRST_SHARE = 0.14f;         // no teeth right in the corners, the opening is closed there
    private const int LIP_SAMPLES = 48;              // points of the lip line the teeth are spread along
    private const float INSET = 0.94f;               // the roots sit a little inside the lips
    private const float MAX_REACH = 1.7f;            // a tooth is at most this many half openings long: the rows interlock, they do not pierce the other lip
    private const float EDGE_LIMIT = 0.97f;          // above or below this share of the body height there is no skin to stand on

    /*
      The teeth stand evenly along the lip line from the corners round to the nose, on both flanks:
      > Upper row:  on the upper lip, pointing down
      > Lower row:  on the lower lip, pointing up, half a step shifted so the rows interlock
      Evenly by the length of the lip, not by X: a blunt snout carries teeth across its front.
      A spot where either lip leaves the body (a mouth cut lower than the belly) gets no tooth.
      Every second tooth is shorter by the raggedness of the shape.
    */
    /// <summary>The teeth of a mouth shape on a body.</summary>
    /// <param name="design">(Required) The body plan.</param>
    /// <param name="shape">(Required) The opening of its mouth kind, with Teeth per row and flank.</param>
    /// <returns>Every tooth of both flanks, empty when the shape has no teeth.</returns>
    public static List<Tooth> Place(CreatureDesign design, MouthShape shape)
    {
        var teeth = new List<Tooth>();
        if (shape.Teeth <= 0)
        {
            return teeth;
        }
        float head = design.TopAt(shape.CornerX(design));
        foreach (bool isUpper in new[] { true, false })
        {
            if ((isUpper && TeethRows.Lower == shape.TeethRows) || (!isUpper && TeethRows.Upper == shape.TeethRows))
            {
                continue;
            }
            // The lip as a polyline on the right flank, only where both lips lie on the body
            var   points = new List<(Vector3 Point, float Gape)>();
            var   lengths = new List<float>();
            float total  = 0f;
            for (int k = 0; k <= LIP_SAMPLES; k++)
            {
                float s = Mathf.Lerp(FIRST_SHARE, 1f, (float)k / LIP_SAMPLES);
                if (!LipPoint(design, shape, s, isUpper, out Vector3 point, out float gape) || !LipPoint(design, shape, s, !isUpper, out _, out _))
                {
                    continue;
                }
                total += 0 == points.Count ? 0f : points[^1].Point.DistanceTo(point);
                points.Add((point, gape));
                lengths.Add(total);
            }
            if (points.Count < 2 || total <= 0f)
            {
                continue;
            }

            // Evenly along the lip by its length, so a blunt snout gets teeth across its front too
            for (int i = 0; i < shape.Teeth; i++)
            {
                float at    = total * (i + (isUpper ? 0.25f : 0.75f)) / shape.Teeth;
                int   after = Mathf.Clamp(lengths.FindIndex(length => length >= at), 1, points.Count - 1);
                float share = Mathf.InverseLerp(lengths[after - 1], lengths[after], at);
                Vector3 root  = points[after - 1].Point.Lerp(points[after].Point, share);
                Vector3 along = (points[after].Point - points[after - 1].Point).Normalized();
                float   gape  = Mathf.Lerp(points[after - 1].Gape, points[after].Gape, share);
                float ragged = 0 == i % 2 ? 1f : 1f - shape.ToothRagged;
                float length = Mathf.Min(shape.ToothLength * head * ragged, gape * MAX_REACH);
                float width  = shape.ToothWidth * head;
                foreach (float side in new[] { 1f, -1f })
                {
                    // Mirrored about the spine, which may lie beside the middle (sway)
                    var     mirror = new Vector3(1f, 1f, side);
                    Vector3 sway   = Vector3.Back * design.SwayAt(root.X);
                    teeth.Add(new Tooth((root - sway) * mirror + sway, isUpper ? Vector3.Down : Vector3.Up, along * mirror, length, width));
                }
            }
        }
        return teeth;
    }

    /// <summary>
    /// A point on a lip of the opening on the right flank: the skin of the uncarved body at the
    /// height of the lip, the lower lip shifted by the jut like the mesh shifts it.
    /// </summary>
    /// <param name="s">(Required) 0 at the mouth corners .. 1 at the nose tip.</param>
    /// <param name="point">The point in unit space.</param>
    /// <param name="gape">Half the opening there in unit space.</param>
    /// <returns>False where the lip runs above the back or below the belly.</returns>
    private static bool LipPoint(CreatureDesign design, MouthShape shape, float s, bool isUpper, out Vector3 point, out float gape)
    {
        float cornerX = shape.CornerX(design);
        float head    = design.TopAt(cornerX);
        float x       = Mathf.Lerp(cornerX, design.NoseTipX, s);
        float lineY   = design.LiftAt(cornerX) + (shape.Level + shape.Smile * (1f - s) * (1f - s)) * head;
        gape = Mathf.Max(shape.Gape * head * Mathf.SmoothStep(0f, 0.3f, s), head * MouthShape.MIN_GAPE);

        float y      = lineY + (isUpper ? gape : -gape);
        float height = design.TopAt(x);
        float across = (y - design.LiftAt(x)) / Mathf.Max(height, 0.0001f);
        point = Vector3.Zero;
        if (Mathf.Abs(across) >= EDGE_LIMIT)
        {
            return false;
        }
        float z   = design.SwayAt(x) + design.FlankAt(x) * Mathf.Sqrt(1f - across * across) * INSET;
        float jut = isUpper ? 0f : shape.Jut * head * Mathf.SmoothStep(0f, 1f, s) * Mathf.Clamp(gape / Mathf.Max(head * 0.6f, 0.001f), 0f, 1f);
        point = new Vector3(x + jut, y, z);
        return true;
    }
}
