// NOTE:
//   The clay geometry of an organism: the body lofted along the spine of its design
//   with the opening of its mouth part cut into the head (Design/MouthShapes.cs),
//   the tube of a sculpted limb through its joints and the fillet where a part grows
//   out of the skin. Builds meshes only, the materials (shaders/clay.gdshader) come
//   from CreatureVisual3D.
//
//   ArrayMesh body = CreatureClay.BuildBodyMesh(design);
//   ArrayMesh tube = CreatureClay.LimbMesh(new[] { Vector3.Zero, knee, ankle });

//
//   Chapters:
//     LOFT THE CLAY   Body mesh, limb tube and fillet.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public static class CreatureClay
{
    // ===========================================================================
    // ============================   LOFT THE CLAY   ============================
    // ===========================================================================
    //
    //   BuildBodyMesh   The lofted clay body: elliptic rings along the spine, the mouth cut into the head.
    //   CarveMouth      Moves one vertex for the mouth opening: cavity, lips and lower jaw.
    //   LimbMesh        The tube mesh along a smoothed joint path.
    //   AddFillet       A soft clay ball where a part grows out of the body.
    //

    private const int RINGS = 144;                  // rings along the body: fine enough for a smooth silhouette and for the steps of the plate skin
    private const int SEGMENTS = 40;
    private const float LIFT_RANGE = 2f;            // spine heights up to this are carried in the vertex color (clay.gdshaderinc, LIFT_RANGE)
    private const float RING_TILT = 0.6f;           // share of the spine slope the rings tilt by: full tilt folds the inside of a bend

    /// <summary>
    /// One elliptic ring per step along X from the tail tip to the nose tip, the radius
    /// from CreatureDesign.RadiusAt. The steps bunch up at both tips (cosine spacing),
    /// where the profile bends fastest. The tip rings collapse to a point.
    /// Quad winding: A (ring i), C (next segment), B (ring i + 1) is clockwise seen
    /// from outside, the front face in Godot.
    /// </summary>
    /// <param name="design">(Required) The design with at least MIN_VERTEBRAE girths.</param>
    /// <returns>The body mesh in unit space.</returns>
    public static ArrayMesh BuildBodyMesh(CreatureDesign design)
    {
        MouthShape mouth   = MouthShapes.For(design);
        var        surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= RINGS; i++)
        {
            float u      = 0.5f - 0.5f * Mathf.Cos(Mathf.Pi * i / RINGS);
            float x      = Mathf.Lerp(design.TailTipX, design.NoseTipX, u);
            float radius = 0 == i || RINGS == i ? 0f : design.RadiusAt(x);
            // The ring sits on the bent spine and tilts with it: its up axis is perpendicular to the slope
            Vector3 center = design.CenterAt(x);
            float   height = design.HeightScaleAt(x);
            float   width  = design.WidthScaleAt(x);
            Vector3 up     = new Vector3(-design.SlopeAt(x) * RING_TILT, 1f, 0f).Normalized();
            for (int j = 0; j < SEGMENTS; j++)
            {
                float   angle  = Mathf.Tau * j / SEGMENTS;
                Vector3 vertex = center + up * (Mathf.Sin(angle) * radius * height) + Vector3.Back * (Mathf.Cos(angle) * radius * width);
                // The shader draws the mouth line per pixel from this mark, sharper than the mesh could cut it
                // In UV2: primitive meshes sharing the clay (fillets, knuckles) bring their own UV, but no UV2
                surface.SetUV2(null == mouth ? Vector2.Zero : CarveMouth(design, mouth, center, ref vertex));
                // Height of the spine there, for the shader: back and belly are measured from the spine, not from the
                // origin (a lifted neck keeps its pattern). Red counts up, green down, white (meshes without colors) is 0
                surface.SetColor(new Color(1f - Mathf.Clamp(center.Y, 0f, LIFT_RANGE) / LIFT_RANGE, 1f - Mathf.Clamp(-center.Y, 0f, LIFT_RANGE) / LIFT_RANGE, 1f));
                surface.AddVertex(vertex);
            }
        }
        for (int i = 0; i < RINGS; i++)
        {
            for (int j = 0; j < SEGMENTS; j++)
            {
                int a = i * SEGMENTS + j;
                int b = (i + 1) * SEGMENTS + j;
                int c = i * SEGMENTS + (j + 1) % SEGMENTS;
                int d = (i + 1) * SEGMENTS + (j + 1) % SEGMENTS;
                surface.AddIndex(a);
                surface.AddIndex(c);
                surface.AddIndex(b);
                surface.AddIndex(b);
                surface.AddIndex(c);
                surface.AddIndex(d);
            }
        }
        surface.GenerateNormals();
        return surface.Commit();
    }

    /*
      The mouth is cut into the clay itself (Design/MouthShapes.cs), from the corners to
      the nose tip. s runs 0 at the corners .. 1 at the nose tip:
      > Mouth line:  at the level of the shape at the nose, climbing by its smile towards the corners
      > Opening:     a band around the line, closed at the corners, as wide as the gape at the nose
      > Inside it:   the vertex sinks towards the spine and back into the head: the cavity
      > Around it:   the skin bulges out a little: the lips
      > Below it:    the lower jaw is pushed forward (jut), or back under the snout
      All measures are heights of the head at the mouth corners.
    */
    /// <summary>Moves a vertex of the body loft for the mouth opening.</summary>
    /// <param name="center">(Required) Center of the ring the vertex belongs to.</param>
    /// <param name="vertex">The vertex in unit space, moved in place.</param>
    /// <returns>
    /// Where the vertex sat before it moved, for the shader (clay.gdshaderinc, mouth_mark):
    /// x = s (0 behind the corners), y = how far below the mouth line in heights of the head.
    /// </returns>
    private static Vector2 CarveMouth(CreatureDesign design, MouthShape mouth, Vector3 center, ref Vector3 vertex)
    {
        float cornerX = mouth.CornerX(design);
        if (vertex.X <= cornerX)
        {
            return Vector2.Zero;
        }
        float head  = design.TopAt(cornerX);
        float s     = Mathf.Clamp((vertex.X - cornerX) / Mathf.Max(design.NoseTipX - cornerX, 0.001f), 0f, 1f);
        float lineY = design.LiftAt(cornerX) + (mouth.Level + mouth.Smile * (1f - s) * (1f - s)) * head;
        float below = lineY - vertex.Y;
        var   mark  = new Vector2(s, below / Mathf.Max(head, 0.001f));

        // Lower jaw: everything below the line shifts, the most at the nose
        vertex.X += mouth.Jut * head * Mathf.SmoothStep(0f, 1f, s) * Mathf.Clamp(below / Mathf.Max(head * 0.6f, 0.001f), 0f, 1f);

        float   gape    = Mathf.Max(mouth.Gape * head * Mathf.SmoothStep(0f, 0.3f, s), head * MouthShape.MIN_GAPE);
        float   off     = Mathf.Abs(below) / gape;
        Vector3 outward = new Vector3(0f, vertex.Y - center.Y, vertex.Z - center.Z);
        outward = outward.LengthSquared() < 1e-8f ? Vector3.Zero : outward.Normalized();
        if (off >= 1f)
        {
            // Lips: a soft bulge just outside the opening, gone two gapes away
            float lip = Mathf.Clamp(2f - off, 0f, 1f) * Mathf.SmoothStep(0f, 0.2f, s);
            vertex += outward * mouth.Lip * head * lip;
            return mark;
        }
        // A closed line is only drawn (shader), sinking single vertices would fray it: the cavity grows with the gape
        float inside = (1f - off * off) * Mathf.SmoothStep(0f, 0.15f, s) * Mathf.Clamp(mouth.Gape / 0.06f, 0f, 1f);
        vertex -= outward * mouth.Depth * head * inside * 0.6f;
        vertex.X -= mouth.Depth * head * inside * s;
        return mark;
    }

    /*
      Tube along a path: the path is smoothed (Catmull-Rom, LIMB_STEPS per segment), every
      sample gets a ring of LIMB_SEGMENTS points around its tangent. The ring frame is
      carried along by parallel transport, so the tube does not twist. (tangent, normal,
      binormal) is right handed like (X, Y, Z) of the body loft, so the same winding faces out.
    */
    public const float LIMB_ROOT_RADIUS = 0.14f;             // chunky on purpose, cartoon monsters
    public const float LIMB_TIP_RADIUS = 0.08f;
    public const float TENTACLE_TIP_RADIUS = 0.022f;         // a tentacle runs out into a point
    public const float TENTACLE_END_RADIUS = 0.05f;          // unless a club or a hand sits on its tip
    private const int LIMB_STEPS = 10;
    private const int LIMB_SEGMENTS = 18;

    /// <summary>The clay tube of a sculpted limb, LIMB_ROOT_RADIUS thick at the shoulder, LIMB_TIP_RADIUS at the tip.</summary>
    /// <param name="path">(Required) The shoulder and every joint, in limb space.</param>
    /// <param name="tipRadius">(Optional) Radius at the tip. Defaults to LIMB_TIP_RADIUS.</param>
    /// <param name="rootRadius">(Optional) Radius at the shoulder. Defaults to LIMB_ROOT_RADIUS.</param>
    /// <param name="widths">(Optional) Thickness factor per point of the path, blended along the tube. Defaults to 1 everywhere.</param>
    /// <returns>The tube mesh in limb space.</returns>
    public static ArrayMesh LimbMesh(IReadOnlyList<Vector3> path, float tipRadius = LIMB_TIP_RADIUS, float rootRadius = LIMB_ROOT_RADIUS, IReadOnlyList<float> widths = null)
    {
        // Smoothed samples, thick at the shoulder, thin at the tip
        var samples = new List<Vector3>();
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 before = path[Mathf.Max(i - 1, 0)];
            Vector3 after  = path[Mathf.Min(i + 2, path.Count - 1)];
            for (int k = 0; k < LIMB_STEPS; k++)
            {
                samples.Add(path[i].CubicInterpolate(path[i + 1], before, after, (float)k / LIMB_STEPS));
            }
        }
        samples.Add(path[^1]);

        var     surface = new SurfaceTool();
        Vector3 normal  = Vector3.Zero;
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 tangent = (samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(i - 1, 0)]).Normalized();
            if (Vector3.Zero == normal)
            {
                normal = Mathf.Abs(tangent.Dot(Vector3.Up)) > 0.9f ? Vector3.Right : Vector3.Up;
            }
            normal = (normal - tangent * normal.Dot(tangent)).Normalized();   // parallel transport
            Vector3 binormal = tangent.Cross(normal);
            // The factor of the two path points around this sample, blended smoothly between them
            float   along    = (float)i / LIMB_STEPS;
            int     point    = Mathf.Min(Mathf.FloorToInt(along), path.Count - 2);
            float   width    = null == widths ? 1f : Mathf.Lerp(WidthOf(widths, point), WidthOf(widths, point + 1), Mathf.SmoothStep(0f, 1f, along - point));
            float   radius   = Mathf.Lerp(rootRadius, tipRadius, (float)i / (samples.Count - 1)) * width;
            for (int j = 0; j < LIMB_SEGMENTS; j++)
            {
                float angle = Mathf.Tau * j / LIMB_SEGMENTS;
                surface.AddVertex(samples[i] + (normal * Mathf.Sin(angle) + binormal * Mathf.Cos(angle)) * radius);
            }
        }
        for (int i = 0; i < samples.Count - 1; i++)
        {
            for (int j = 0; j < LIMB_SEGMENTS; j++)
            {
                int a = i * LIMB_SEGMENTS + j;
                int b = (i + 1) * LIMB_SEGMENTS + j;
                int c = i * LIMB_SEGMENTS + (j + 1) % LIMB_SEGMENTS;
                int d = (i + 1) * LIMB_SEGMENTS + (j + 1) % LIMB_SEGMENTS;
                surface.AddIndex(a);
                surface.AddIndex(c);
                surface.AddIndex(b);
                surface.AddIndex(b);
                surface.AddIndex(c);
                surface.AddIndex(d);
            }
        }
        surface.GenerateNormals();
        return surface.Commit();
    }

    private static float WidthOf(IReadOnlyList<float> widths, int point)
    {
        return point >= 0 && point < widths.Count ? widths[point] : 1f;
    }

    /*
      Fillet: a flat swelling of plain clay where a limb, wing or stalk grows out of the
      body, half sunk into it, so the part swells out of the skin instead of sticking in
      like a pipe. Flattened along the local Z of the part node (away from the body), so
      it reads as skin, not as a ball. A child of the part's own node: it follows side,
      pose and size. Sizes as share of the part length, tuned on renders.
    */
    public const float WING_FILLET = 0.08f;
    public const float STALK_FILLET = 0.24f;
    public const float LIMB_FILLET = 1.6f;           // of the root radius of the tube
    private const float FILLET_FLATTEN = 0.55f;

    /// <summary>Adds a fillet to the node of a part.</summary>
    /// <param name="parent">(Required) The node of the part, the fillet flattens along its Z.</param>
    /// <param name="position">(Required) Where the part leaves the body, in the space of the parent.</param>
    /// <param name="radius">(Required) Radius of the swelling.</param>
    /// <param name="clay">(Required) Plain clay without pattern (the knuckle clay of CreatureVisual3D).</param>
    public static void AddFillet(Node3D parent, Vector3 position, float radius, Material clay)
    {
        MeshInstance3D fillet = CellVisual3D.AddMesh(parent, CellVisual3D.SphereMesh(), clay, position, new Vector3(radius, radius, radius * FILLET_FLATTEN));
        fillet.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }
}
