// NOTE:
//   The wake of swimming cells: every cell that moves sheds a pair of counter turning
//   swirls from its flanks behind it (soft discs that widen, curl and fade, like the
//   vortex street behind a flagellum) and a few tiny bubbles that rise and wobble.
//   Emission follows the distance swum, so a fast cell leaves a dense trail and a
//   resting one none. Only in the cell stages, the sea of the organism has its own look.
//   World/FollowCamera.cs creates it and calls Follow once per frame.
//
//   var wake = new SwimWake();
//   wake.Setup(session);
//   AddChild(wake);
//   wake.Follow(dt, viewRadius, palette.Light, isActive: !player.IsOrganism);

//
//   Chapters:
//     BUILD WAKE    Swirl and bubble multimeshes, the particle pool.
//     FOLLOW CELLS  Emits behind every swimming cell, moves, curls and fades the particles.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class SwimWake : Node3D
{
    private const int MAX_SWIRLS = 420;
    private const int MAX_BUBBLES = 360;
    private const int TEXTURE_SIZE = 64;

    // ===========================================================================
    // =============================   BUILD WAKE   ==============================
    // ===========================================================================
    //
    //   Setup    The session with player and critter layer, before AddChild.
    //   _Ready   1. Swirls    Soft additive discs facing the camera.
    //            2. Bubbles   Discs with a bright rim, a bubble seen from above.
    //

    /// <summary>One particle of the wake on the plane, Y is its height.</summary>
    private struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public float Life;
        public float From;      // size at the birth
        public float To;        // size at the end
        public float Alpha;
        public float Curl;      // radians per second the velocity turns, the sign is the side
    }

    private WorldSession _session;
    private MultiMesh _swirlMesh;
    private MultiMesh _bubbleMesh;
    private readonly Particle[] _swirls = new Particle[MAX_SWIRLS];
    private readonly Particle[] _bubbles = new Particle[MAX_BUBBLES];
    private int _nextSwirl;
    private int _nextBubble;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and critter layer, every swimming cell leaves a wake.</param>
    public void Setup(WorldSession session)
    {
        _session = session;
    }

    public override void _Ready()
    {
        // ---------------------------------------------------------------------------
        // Swirls
        //   Soft additive discs facing the camera.
        // ---------------------------------------------------------------------------

        var soft = new Gradient { Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) }, Offsets = new[] { 0f, 1f } };
        _swirlMesh = AddLayer(soft, MAX_SWIRLS, BaseMaterial3D.BlendModeEnum.Add);

        // ---------------------------------------------------------------------------
        // Bubbles
        //   Discs with a bright rim, a bubble seen from above.
        // ---------------------------------------------------------------------------

        var ring = new Gradient
        {
            Colors = new[]
            {
                new Color(1f, 1f, 1f, 0.12f),
                new Color(1f, 1f, 1f, 0.08f),
                new Color(1f, 1f, 1f, 0.95f),
                new Color(1f, 1f, 1f, 0f),
            },
            Offsets = new[] { 0f, 0.6f, 0.8f, 1f },
        };
        _bubbleMesh = AddLayer(ring, MAX_BUBBLES, BaseMaterial3D.BlendModeEnum.Mix);
    }

    /// <summary>A multimesh of camera facing quads with a radial texture and vertex colors.</summary>
    private MultiMesh AddLayer(Gradient gradient, int count, BaseMaterial3D.BlendModeEnum blend)
    {
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1f, 0.5f),
            Width    = TEXTURE_SIZE,
            Height   = TEXTURE_SIZE,
        };
        var material = new StandardMaterial3D
        {
            ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode              = blend,
            BillboardMode          = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale     = true,
            AlbedoTexture          = texture,
            VertexColorUseAsAlbedo = true,
        };
        var mesh = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            Mesh                 = new QuadMesh { Size = Vector2.One },
            InstanceCount        = count,
            VisibleInstanceCount = 0,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh        = mesh,
            MaterialOverride = material,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return mesh;
    }

    // ===========================================================================
    // ============================   FOLLOW CELLS   =============================
    // ===========================================================================
    //
    //   Follow   1. Emit     Sheds swirls and bubbles behind every cell for the distance it swam.
    //            2. Move     Drags, curls, lifts and ages every particle.
    //            3. Draw     Writes size and fading color into both multimeshes.
    //

    private const float SWIRL_STEP = 0.35f;       // one swirl pair per this many body sizes swum
    private const float BUBBLE_CHANCE = 0.8f;     // chance of a bubble per swirl pair
    private const float MIN_SPEED_SHARE = 0.12f;  // slower than this share of the swim speed leaves no wake
    private const float VIEW_MARGIN = 1.2f;       // cells this many view radii away still shed a wake
    private const float SWIRL_LIFE = 1.5f;
    private const float BUBBLE_LIFE = 2.2f;
    private const float DRAG = 2.2f;              // velocity share lost per second, the water is thick at this scale
    private const float CURL = 2.4f;              // radians per second a swirl turns

    private Dictionary<ulong, float> _swum = new();    // distance swum since the last swirl pair, per cell
    private float _time;

    /// <summary>Emits, moves and draws the wake, called every frame.</summary>
    /// <param name="dt">(Required) Frame time, 0 only redraws.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, farther cells shed nothing.</param>
    /// <param name="light">(Required) Light color of the water, the wake shimmers in it.</param>
    /// <param name="isActive">(Required) Sheds new particles, false lets the old ones fade.</param>
    public void Follow(float dt, float viewRadius, Color light, bool isActive)
    {
        _time += dt;

        // ---------------------------------------------------------------------------
        // Emit
        //   Sheds swirls and bubbles behind every cell for the distance it swam.
        // ---------------------------------------------------------------------------

        var swum = new Dictionary<ulong, float>();
        if (isActive && dt > 0f)
        {
            Player player = _session.Player;
            if (!player.IsHidden)
            {
                EmitBehind(player, dt, swum);
            }
            foreach (Node node in _session.CritterLayer.GetChildren())
            {
                if (node is Critter critter && !critter.IsEngulfed && !critter.IsQueuedForDeletion()
                    && critter.Position.DistanceTo(player.GlobalPosition) < viewRadius * VIEW_MARGIN)
                {
                    EmitBehind(critter, dt, swum);
                }
            }
        }
        _swum = swum;

        // ---------------------------------------------------------------------------
        // Move
        //   Drags, curls, lifts and ages every particle.
        // ---------------------------------------------------------------------------

        Move(_swirls, dt);
        Move(_bubbles, dt);

        // ---------------------------------------------------------------------------
        // Draw
        //   Writes size and fading color into both multimeshes.
        // ---------------------------------------------------------------------------

        Draw(_swirlMesh, _swirls, light, isBubble: false);
        Draw(_bubbleMesh, _bubbles, light, isBubble: true);
    }

    /*
      Behind one cell:
      - Distance swum adds up per cell (instance id), a resting cell sheds nothing.
      - Every SWIRL_STEP body sizes: a swirl on each flank, pushed back and out and
        turning against each other, and maybe a bubble from the tail.
    */
    private void EmitBehind(CellBody body, float dt, Dictionary<ulong, float> swum)
    {
        float   size  = body.BodySize();
        float   speed = body.Velocity.Length();
        ulong   id    = body.GetInstanceId();
        float   total = _swum.GetValueOrDefault(id);
        if (speed < CellBody.SwimSpeed(size) * MIN_SPEED_SHARE)
        {
            swum[id] = total;
            return;
        }

        total += speed * dt;
        Vector2 forward = body.Velocity / speed;
        Vector2 side    = forward.Orthogonal();
        float   drop    = Plane3D.Drop(body.GlobalPosition) + body.Depth;
        while (total >= size * SWIRL_STEP)
        {
            total -= size * SWIRL_STEP;
            Vector2 tail = body.GlobalPosition - forward * size * 0.85f;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 flank = tail + side * sign * size * 0.45f;
                Vector2 push  = (-forward * 0.35f + side * sign * 0.3f) * speed;
                _swirls[_nextSwirl] = new Particle
                {
                    Position = Plane3D.To3D(flank, -drop + Rand.Range(-0.15f, 0.1f) * size),
                    Velocity = new Vector3(push.X, 0f, push.Y),
                    Life     = SWIRL_LIFE * Rand.Range(0.8f, 1.2f),
                    From     = size * 0.35f,
                    To       = size * Rand.Range(0.9f, 1.3f),
                    Alpha    = Rand.Range(0.09f, 0.15f),
                    Curl     = CURL * sign,
                };
                _nextSwirl = (_nextSwirl + 1) % MAX_SWIRLS;
            }
            if (Rand.Chance(BUBBLE_CHANCE))
            {
                Vector2 spot  = tail + Vector2.FromAngle(Rand.Angle()) * size * 0.3f;
                Vector2 drift = -forward * speed * 0.2f;
                _bubbles[_nextBubble] = new Particle
                {
                    Position = Plane3D.To3D(spot, -drop + size * 0.2f),
                    Velocity = new Vector3(drift.X, size * Rand.Range(0.3f, 0.6f), drift.Y),
                    Life     = BUBBLE_LIFE * Rand.Range(0.7f, 1.2f),
                    From     = size * Rand.Range(0.07f, 0.13f),
                    To       = size * Rand.Range(0.14f, 0.24f),
                    Alpha    = Rand.Range(0.35f, 0.6f),
                    Curl     = Rand.Range(-3f, 3f),     // the phase of its wobble
                };
                _nextBubble = (_nextBubble + 1) % MAX_BUBBLES;
            }
        }
        swum[id] = total;
    }

    /// <summary>Ages, drags and curls the particles, the curl turns the velocity on the plane.</summary>
    private static void Move(Particle[] particles, float dt)
    {
        float drag = Mathf.Exp(-DRAG * dt);
        for (int i = 0; i < particles.Length; i++)
        {
            ref Particle p = ref particles[i];
            if (p.Age >= p.Life)
            {
                continue;
            }
            p.Age += dt;
            var flat = new Vector2(p.Velocity.X, p.Velocity.Z).Rotated(p.Curl * dt) * drag;
            p.Velocity = new Vector3(flat.X, p.Velocity.Y * Mathf.Exp(-DRAG * 0.3f * dt), flat.Y);
            p.Position += p.Velocity * dt;
        }
    }

    /// <summary>Living particles into the multimesh: they grow, fade in fast and out slowly, bubbles wobble.</summary>
    private void Draw(MultiMesh mesh, Particle[] particles, Color light, bool isBubble)
    {
        int count = 0;
        for (int i = 0; i < particles.Length; i++)
        {
            Particle p = particles[i];
            if (p.Age >= p.Life)
            {
                continue;
            }
            float   t      = p.Age / p.Life;
            float   fade   = Mathf.Min(t * 8f, 1f) * (1f - t) * (1f - t);
            float   size   = Mathf.Lerp(p.From, p.To, Mathf.Sqrt(t));
            Vector3 wobble = isBubble ? new Vector3(Mathf.Sin(_time * 7f + p.Curl), 0f, Mathf.Cos(_time * 5f + p.Curl)) * p.From * 0.4f : Vector3.Zero;
            mesh.SetInstanceTransform(count, new Transform3D(Basis.FromScale(Vector3.One * size), p.Position + wobble));
            mesh.SetInstanceColor(count, new Color(light.Lightened(isBubble ? 0.5f : 0.2f), p.Alpha * fade));
            count++;
        }
        mesh.VisibleInstanceCount = count;
    }
}
