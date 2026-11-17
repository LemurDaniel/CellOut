// NOTE:
//   The look of the cell abilities that are not a lasting zone: short-lived particles
//   (the jet of a nozzle, the spray of a toxin gland, the swirl of a vortex, the glint
//   of a sunbathing sail, the shards of a broken plate) and jagged lightning bolts
//   between cells. No rings on the plane (rule in CLAUDE.md): everything flies, swirls
//   or flickers. AbilityResolver.cs owns it and calls Emit, Bolt and Advance.
//
//   var particles = new AbilityParticles();
//   AddChild(particles);
//   particles.Emit(position, velocity, size, color, life);
//   particles.Bolt(new[] { from, to }, thickness);
//   particles.Advance(dt);   // once per frame

//
//   Chapters:
//     BUILD     Particle and bolt multimeshes.
//     EMIT      New particles and bolts.
//     ADVANCE   Moves, fades and draws them.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class AbilityParticles : Node3D
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready   1. Particles   Soft additive discs facing the camera.
    //            2. Bolts       Thin glowing bars, one per segment of a bolt.
    //

    private const int MAX_PARTICLES = 700;
    private const int MAX_SEGMENTS = 240;
    private const int TEXTURE_SIZE = 32;

    private MultiMesh _particleMesh;
    private MultiMesh _boltMesh;

    public override void _Ready()
    {
        // ---------------------------------------------------------------------------
        // Particles
        //   Soft additive discs facing the camera.
        // ---------------------------------------------------------------------------

        var soft = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) }, Offsets = new[] { 0.1f, 1f } },
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1f, 0.5f),
            Width    = TEXTURE_SIZE,
            Height   = TEXTURE_SIZE,
        };
        _particleMesh = AddLayer(new QuadMesh { Size = Vector2.One }, MAX_PARTICLES, new StandardMaterial3D
        {
            ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode              = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode          = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale     = true,
            AlbedoTexture          = soft,
            VertexColorUseAsAlbedo = true,
        });

        // ---------------------------------------------------------------------------
        // Bolts
        //   Thin glowing bars, one per segment of a bolt.
        // ---------------------------------------------------------------------------

        _boltMesh = AddLayer(new BoxMesh { Size = Vector3.One }, MAX_SEGMENTS, new StandardMaterial3D
        {
            ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode              = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true,
        });
    }

    /// <summary>A multimesh with vertex colors, empty until Advance fills it.</summary>
    private MultiMesh AddLayer(Mesh mesh, int count, Material material)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            Mesh                 = mesh,
            InstanceCount        = count,
            VisibleInstanceCount = 0,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh        = multimesh,
            MaterialOverride = material,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return multimesh;
    }

    // ===========================================================================
    // ================================   EMIT   =================================
    // ===========================================================================
    //
    //   Emit   One particle: it drifts, slows in the thick water, grows and fades.
    //   Bolt   A jagged lightning bolt along a chain of points, it flickers until it fades.
    //

    private struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Size;
        public Color Color;
        public float Age;
        public float Life;
    }

    private struct Bolt3D
    {
        public Vector3[] Points;
        public float Thickness;
        public float Age;
    }

    private readonly Particle[] _particles = new Particle[MAX_PARTICLES];
    private int _next;
    private readonly List<Bolt3D> _bolts = new();

    /// <summary>One particle in the world.</summary>
    /// <param name="position">(Required) Start point in 3D.</param>
    /// <param name="velocity">(Required) Start velocity in world units per second.</param>
    /// <param name="size">(Required) Diameter at the start, it grows by half until it fades.</param>
    /// <param name="color">(Required) Color, alpha is the start opacity.</param>
    /// <param name="life">(Required) Seconds until it is gone.</param>
    public void Emit(Vector3 position, Vector3 velocity, float size, Color color, float life)
    {
        _particles[_next] = new Particle { Position = position, Velocity = velocity, Size = size, Color = color, Life = life };
        _next = (_next + 1) % MAX_PARTICLES;
    }

    private const float BOLT_LIFE = 0.28f;

    /// <summary>A lightning bolt along a chain of points (the cell, then every target), flickering until it fades.</summary>
    /// <param name="points">(Required) At least two points in 3D.</param>
    /// <param name="thickness">(Required) Width of the bolt in world units.</param>
    public void Bolt(IReadOnlyList<Vector3> points, float thickness)
    {
        if (points.Count < 2)
        {
            return;
        }
        _bolts.Add(new Bolt3D { Points = new List<Vector3>(points).ToArray(), Thickness = thickness });
    }

    // ===========================================================================
    // ===============================   ADVANCE   ===============================
    // ===========================================================================
    //
    //   Advance       1. Particles   Drift, drag, grow and fade.
    //                 2. Bolts       Every segment zig-zags anew each frame, the bolt fades.
    //   AddSegment    One glowing bar between two points.
    //

    private const float DRAG = 3f;
    private const int BOLT_KINKS = 6;             // zig-zag points between two cells
    private const float BOLT_JITTER = 0.18f;      // share of the segment length a kink may stray
    private static readonly Color BOLT_COLOR = new(0.65f, 0.85f, 1f);

    /// <summary>Moves, fades and draws every particle and bolt.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Advance(float dt)
    {
        // ---------------------------------------------------------------------------
        // Particles
        //   Drift, drag, grow and fade.
        // ---------------------------------------------------------------------------

        float drag  = Mathf.Exp(-DRAG * dt);
        int   count = 0;
        for (int i = 0; i < _particles.Length; i++)
        {
            ref Particle p = ref _particles[i];
            if (p.Age >= p.Life)
            {
                continue;
            }
            p.Age      += dt;
            p.Velocity *= drag;
            p.Position += p.Velocity * dt;
            float t    = Mathf.Clamp(p.Age / p.Life, 0f, 1f);
            float fade = Mathf.Min(t * 10f, 1f) * (1f - t);
            _particleMesh.SetInstanceTransform(count, new Transform3D(Basis.FromScale(Vector3.One * p.Size * (1f + t * 0.5f)), p.Position));
            _particleMesh.SetInstanceColor(count, new Color(p.Color, p.Color.A * fade));
            count++;
        }
        _particleMesh.VisibleInstanceCount = count;

        // ---------------------------------------------------------------------------
        // Bolts
        //   Every segment zig-zags anew each frame, the bolt fades.
        // ---------------------------------------------------------------------------

        int segments = 0;
        for (int b = _bolts.Count - 1; b >= 0; b--)
        {
            Bolt3D bolt = _bolts[b];
            bolt.Age += dt;
            if (bolt.Age >= BOLT_LIFE)
            {
                _bolts.RemoveAt(b);
                continue;
            }
            _bolts[b] = bolt;
            float alpha = 1f - bolt.Age / BOLT_LIFE;
            for (int i = 0; i + 1 < bolt.Points.Length; i++)
            {
                Vector3 from   = bolt.Points[i];
                Vector3 to     = bolt.Points[i + 1];
                Vector3 across = (to - from).Cross(Vector3.Up).Normalized();
                float   stray  = from.DistanceTo(to) * BOLT_JITTER;
                Vector3 last   = from;
                for (int k = 1; k <= BOLT_KINKS && segments < MAX_SEGMENTS; k++)
                {
                    Vector3 point = k == BOLT_KINKS
                        ? to
                        : from.Lerp(to, (float)k / BOLT_KINKS) + across * (float)GD.RandRange(-stray, stray) + Vector3.Up * (float)GD.RandRange(-stray, stray) * 0.4f;
                    AddSegment(segments++, last, point, bolt.Thickness, new Color(BOLT_COLOR, alpha));
                    last = point;
                }
            }
        }
        _boltMesh.VisibleInstanceCount = segments;
    }

    /// <summary>A unit box stretched from one point to the next, thin across.</summary>
    private void AddSegment(int index, Vector3 from, Vector3 to, float thickness, Color color)
    {
        Vector3 along  = to - from;
        float   length = Mathf.Max(along.Length(), 0.0001f);
        Basis   turn   = Basis.LookingAt(along / length, Mathf.Abs(along.Normalized().Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up);
        Basis   basis  = turn * Basis.FromScale(new Vector3(thickness, thickness, length));
        _boltMesh.SetInstanceTransform(index, new Transform3D(basis, (from + to) * 0.5f));
        _boltMesh.SetInstanceColor(index, color);
    }
}
