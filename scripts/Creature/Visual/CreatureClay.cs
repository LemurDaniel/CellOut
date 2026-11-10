// NOTE:
//   The clay geometry of an organism: the body lofted along the spine of its design,
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
    //   BuildBodyMesh   The lofted clay body: elliptic rings along the spine.
    //   LimbMesh        The tube mesh along a smoothed joint path.
    //   AddFillet       A soft clay ball where a part grows out of the body.
    //

    private const int RINGS = 96;                   // rings along the body, fine enough for a smooth silhouette
    private const int SEGMENTS = 40;
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
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= RINGS; i++)
        {
            float u      = 0.5f - 0.5f * Mathf.Cos(Mathf.Pi * i / RINGS);
            float x      = Mathf.Lerp(design.TailTipX, design.NoseTipX, u);
            float radius = 0 == i || RINGS == i ? 0f : design.RadiusAt(x);
            // The ring sits on the bent spine and tilts with it: its up axis is perpendicular to the slope
            var     center = new Vector3(x, design.LiftAt(x), 0f);
            Vector3 up     = new Vector3(-design.SlopeAt(x) * RING_TILT, 1f, 0f).Normalized();
            for (int j = 0; j < SEGMENTS; j++)
            {
                float angle = Mathf.Tau * j / SEGMENTS;
                surface.AddVertex(center + up * (Mathf.Sin(angle) * radius * design.HeightScale) + Vector3.Back * (Mathf.Cos(angle) * radius * design.WidthScale));
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
      Tube along a path: the path is smoothed (Catmull-Rom, LIMB_STEPS per segment), every
      sample gets a ring of LIMB_SEGMENTS points around its tangent. The ring frame is
      carried along by parallel transport, so the tube does not twist. (tangent, normal,
      binormal) is right handed like (X, Y, Z) of the body loft, so the same winding faces out.
    */
    public const float LIMB_ROOT_RADIUS = 0.14f;             // chunky on purpose, cartoon monsters
    public const float LIMB_TIP_RADIUS = 0.08f;
    private const int LIMB_STEPS = 10;
    private const int LIMB_SEGMENTS = 18;

    /// <summary>The clay tube of a sculpted limb, LIMB_ROOT_RADIUS thick at the shoulder, LIMB_TIP_RADIUS at the tip.</summary>
    /// <param name="path">(Required) The shoulder and every joint, in limb space.</param>
    /// <returns>The tube mesh in limb space.</returns>
    public static ArrayMesh LimbMesh(IReadOnlyList<Vector3> path)
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
            float   radius   = Mathf.Lerp(LIMB_ROOT_RADIUS, LIMB_TIP_RADIUS, (float)i / (samples.Count - 1));
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
