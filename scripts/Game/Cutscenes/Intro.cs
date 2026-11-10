// NOTE:
//   Start animation, the origin of life in a few seconds: the camera spirals down from
//   the menu view into the primordial soup. Molecules drift together, link into short
//   chains and swirl inwards, lipids line up into a shell around them, the membrane
//   closes, an RNA strand coils up inside and two heartbeats bring the protocell to
//   life, while a soft warm light gathers around it. Runs while the game is paused, any
//   key or click skips it. Main shows the real player when Finished fires and frees the intro.
//
//   var intro = new Intro { Camera = camera, CameraEnd = gameplayPose, Center = spawn, Size = player.BodySize(), BodyColor = player.BodyColor };
//   AddChild(intro);
//   intro.Finished += OnIntroFinished;

//
//   Chapters:
//     SETTINGS        Inputs from Main and the timeline.
//     BUILD           Molecule chains, lipids, the hidden membrane and the warm light.
//     PLAY TIMELINE   Camera, chains, lipid shell, membrane, heartbeats, birth.
//     HELPERS         Rods between two points and the lipid shell.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Intro : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Camera      (Required) The world camera, flown by the intro.
    //   CameraEnd   (Required) Gameplay pose the flight ends in.
    //   Center      (Required) Spawn point of the player in 3D.
    //   Size        (Required) Body size of the player, everything scales with it.
    //   BodyColor   (Required) Membrane color of the protocell.
    //   CameraMoved Every frame of the flight with the camera distance to Center,
    //               Main sizes the sea floor with it.
    //   Finished    The protocell is born, Main takes over.
    //
    //   Timeline in seconds:
    //     0.0 - 6.0   camera spirals from its start pose down to CameraEnd
    //     0.6 - 2.6   molecules link into chains while they swirl inwards
    //     2.4 - 3.9   lipids drift onto a shell around the center
    //     2.8 - 3.8   the plain molecules sink into the center
    //     3.2 - 4.2   the RNA chains coil into one strand inside
    //     3.7 - 4.4   the membrane closes over the lipid shell
    //     4.5, 5.05   two heartbeats, the warm light swells with them
    //     5.2 - 5.8   the strand fades, the real protocell takes over
    //     6.0         Finished
    //

    [Signal] public delegate void CameraMovedEventHandler(float distance);
    [Signal] public delegate void FinishedEventHandler();

    public Camera3D Camera { get; set; }
    public Transform3D CameraEnd { get; set; }
    public Vector3 Center { get; set; }
    public float Size { get; set; } = 14f;
    public Color BodyColor { get; set; } = new(0.55f, 0.85f, 0.95f);

    private const float DURATION = 6f;
    private const float SKIP_GUARD = 0.4f;            // the click that started the game must not skip it
    private const float SPIRAL = 0.9f;                // radians the flight swings around the center
    private const float CHAIN_START = 0.6f;
    private const float CHAIN_END = 2.6f;
    private const float SWIRL_END = 3f;
    private const float SINK_START = 2.8f;
    private const float SINK_END = 3.8f;
    private const float LIPID_START = 2.4f;
    private const float LIPID_END = 3.9f;
    private const float COIL_START = 3.2f;
    private const float COIL_END = 4.2f;
    private const float MEMBRANE_START = 3.7f;
    private const float MEMBRANE_END = 4.4f;
    private static readonly float[] BEAT_TIMES = { 4.5f, 5.05f };
    private const float BEAT_SECONDS = 0.5f;
    private const float FADE_START = 5.2f;
    private const float FADE_END = 5.8f;
    private const int CHAINS = 16;
    private const int CHAIN_LENGTH = 4;
    private const int LIPIDS = 96;
    private const float BOND_LENGTH = 0.2f;           // in body sizes
    private const float RNA_LENGTH = 0.95f;           // the coiled strand along +X, in body sizes
    private const float RNA_RADIUS = 0.14f;
    private const float RNA_TURNS = 2.5f;
    private const int RNA_COLOR = 0;
    private const float WARM_ENERGY = 1.6f;           // the light around the forming cell at full strength
    private static readonly Shader MEMBRANE_SHADER = GD.Load<Shader>("res://shaders/membrane.gdshader");
    private static readonly Color WARM_COLOR = new(1f, 0.85f, 0.6f);
    private static readonly Color LIPID_COLOR = new(1f, 0.9f, 0.55f);
    private static readonly Color[] MOLECULE_COLORS =
    {
        new(0.95f, 0.5f, 0.7f),     // RNA pink, coils up inside
        new(0.95f, 0.85f, 0.4f),    // lipid yellow
        new(0.55f, 0.75f, 1f),      // amino blue
        new(0.6f, 0.95f, 0.7f),     // sugar green
    };

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready         Keeps running while paused, builds every part, shows the first frame.
    //   BuildChains    Molecules of four kinds in chains, each chain on its own orbit.
    //   BuildLipids    Lipid heads scattered wide, each with its spot on the shell.
    //   BuildMembrane  The membrane bubble (same shader as the protocell) and the warm light above it.
    //

    private sealed record Chain(int Kind, float Angle, float Radius, float Height, float Spin, float Delay,
        Vector3[] Loose, float[] Scales, MeshInstance3D[] Members, MeshInstance3D[] Bonds);
    private sealed record Lipid(MeshInstance3D Mesh, float Angle, float Radius, float Height, float Delay, Vector3 Target, Basis Facing);

    private readonly List<Chain> _chains = new();
    private readonly List<Lipid> _lipids = new();
    private OmniLight3D _warmLight;
    private MeshInstance3D _membrane;
    private ShaderMaterial _membraneMaterial;
    private Transform3D _cameraStart;
    private float _time;
    private bool _isFinished;

    public override void _Ready()
    {
        ProcessMode  = ProcessModeEnum.Always;
        Position     = Center;
        _cameraStart = Camera.GlobalTransform;
        BuildChains();
        BuildLipids();
        BuildMembrane();
        UpdateTimeline();
    }

    private void BuildChains()
    {
        for (int c = 0; c < CHAINS; c++)
        {
            int      kind     = c % MOLECULE_COLORS.Length;
            Material material = CellVisual3D.Material(MOLECULE_COLORS[kind], 0.25f, 0.35f);
            var      loose    = new Vector3[CHAIN_LENGTH];
            var      scales   = new float[CHAIN_LENGTH];
            var      members  = new MeshInstance3D[CHAIN_LENGTH];
            var      bonds    = new MeshInstance3D[CHAIN_LENGTH - 1];
            for (int k = 0; k < CHAIN_LENGTH; k++)
            {
                loose[k]   = new Vector3(Rand.Range(-1f, 1f), Rand.Range(-0.3f, 0.3f), Rand.Range(-1f, 1f)) * 0.9f;
                scales[k]  = Rand.Range(0.07f, 0.12f);
                members[k] = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), material, Vector3.Zero, Vector3.One * 0.001f);
                members[k].CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            for (int k = 0; k < bonds.Length; k++)
            {
                bonds[k] = CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), material, Vector3.Zero, Vector3.One * 0.001f);
                bonds[k].CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            _chains.Add(new Chain(kind, Rand.Angle(), Rand.Range(2.5f, 7f), Rand.Range(-0.3f, 0.8f), Rand.Range(0.8f, 1.5f), Rand.Range(0f, 0.6f),
                loose, scales, members, bonds));
        }
    }

    private void BuildLipids()
    {
        Material material = CellVisual3D.Material(LIPID_COLOR, 0.2f, 0.3f);
        for (int i = 0; i < LIPIDS; i++)
        {
            (Vector3 target, Basis facing) = ShellSpot(i);
            MeshInstance3D mesh = CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), material, Vector3.Zero, Vector3.One * 0.001f);
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _lipids.Add(new Lipid(mesh, Rand.Angle(), Rand.Range(3f, 8f), Rand.Range(-0.4f, 1f), Rand.Range(0f, 0.5f), target, facing));
        }
    }

    private void BuildMembrane()
    {
        _membraneMaterial = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        _membraneMaterial.SetShaderParameter("body_color", new Color(BodyColor, 0f));
        _membraneMaterial.SetShaderParameter("seed", Rand.Range(0f, 100f));
        _membraneMaterial.SetShaderParameter("core_alpha", 0.2f);
        _membrane = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(), _membraneMaterial, Vector3.Zero, Vector3.One * 0.001f);
        _membrane.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        _warmLight = new OmniLight3D
        {
            LightColor  = WARM_COLOR,
            OmniRange   = Size * 6f,
            LightEnergy = 0f,
            Position    = Vector3.Up * Size * 1.5f,
        };
        AddChild(_warmLight);
    }

    // ===========================================================================
    // ============================   PLAY TIMELINE   ============================
    // ===========================================================================
    //
    //   1. Camera      Spirals from the start pose into CameraEnd.
    //   2. Chains      Molecules link up, swirl inwards, sink or coil into the RNA strand.
    //   3. Lipids      Drift onto the shell, melt into the membrane once it closes.
    //   4. Membrane    Fades in over the shell and beats twice, the warm light swells with it.
    //   5. Birth       Emits Finished once.
    //
    //   Any key or click after SKIP_GUARD jumps to the end (_UnhandledInput).
    //

    public override void _Process(double delta)
    {
        _time += (float)delta;
        UpdateTimeline();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        bool isPress = @event is InputEventKey { Pressed: true, Echo: false } || @event is InputEventMouseButton { Pressed: true };
        if (!isPress || _time < SKIP_GUARD || _isFinished)
        {
            return;
        }
        GetViewport().SetInputAsHandled();
        Log.Verbose("Intro", $"Skipped at {_time:F1} s.");
        _time = DURATION;
        UpdateTimeline();
    }

    private void UpdateTimeline()
    {
        // ---------------------------------------------------------------------------
        // Camera
        //   Spirals from the start pose into CameraEnd.
        // ---------------------------------------------------------------------------

        // The swing is 0 at both ends, so the flight starts in the menu pose and ends exactly in CameraEnd
        float       flight = Ease.InOut(Ease.Window(_time, 0f, DURATION));
        float       swing  = SPIRAL * Mathf.Sin(flight * Mathf.Pi);
        Transform3D pivot  = Transform3D.Identity.Translated(Center) * new Transform3D(new Basis(Vector3.Up, swing), Vector3.Zero) * Transform3D.Identity.Translated(-Center);
        Camera.GlobalTransform = pivot * _cameraStart.InterpolateWith(CameraEnd, flight);
        EmitSignal(SignalName.CameraMoved, Camera.GlobalPosition.DistanceTo(Center));

        // ---------------------------------------------------------------------------
        // Chains
        //   Molecules link up, swirl inwards, sink or coil into the RNA strand.
        // ---------------------------------------------------------------------------

        float pull = Ease.Smooth(Ease.Window(_time, 0f, SWIRL_END));
        float sink = Ease.Smooth(Ease.Window(_time, SINK_START, SINK_END));
        float coil = Ease.Smooth(Ease.Window(_time, COIL_START, COIL_END));
        float fade = Ease.Window(_time, FADE_START, FADE_END);
        int   rna  = 0;     // index along the coiled strand, counts the RNA molecules
        foreach (Chain chain in _chains)
        {
            bool  isRna  = RNA_COLOR == chain.Kind;
            float bond   = Ease.Smooth(Ease.Window(_time, CHAIN_START + chain.Delay, CHAIN_END + chain.Delay * 0.5f));
            float angle  = chain.Angle + _time * chain.Spin * (1f + 2.5f * pull);
            float radius = Mathf.Lerp(chain.Radius, isRna ? 0.5f : 0.35f, pull);
            float height = Mathf.Lerp(chain.Height, 0.05f, pull) + Mathf.Sin(_time * 3f + chain.Angle) * 0.08f;
            var   anchor = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            var   along  = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));      // the orbit direction, the chain lines up along it

            var positions = new Vector3[CHAIN_LENGTH];
            for (int k = 0; k < CHAIN_LENGTH; k++)
            {
                Vector3 bonded   = along * (k - (CHAIN_LENGTH - 1) * 0.5f) * BOND_LENGTH;
                Vector3 swirling = anchor + chain.Loose[k].Rotated(Vector3.Up, -angle).Lerp(bonded, bond);
                float   scale    = chain.Scales[k] * Ease.Window(_time, 0f, 0.6f);

                // RNA coils into one strand along +X, the rest sinks into the center and is gone
                if (isRna)
                {
                    float   s      = (float)rna / (CHAINS / MOLECULE_COLORS.Length * CHAIN_LENGTH - 1);
                    float   twist  = s * Mathf.Tau * RNA_TURNS + _time * 2.5f;
                    Vector3 strand = new Vector3((s - 0.5f) * RNA_LENGTH, Mathf.Sin(twist) * RNA_RADIUS, Mathf.Cos(twist) * RNA_RADIUS);
                    positions[k] = swirling.Lerp(strand, coil);
                    scale       *= Mathf.Lerp(1f, 0.75f, coil) * (1f - fade);
                    rna++;
                }
                else
                {
                    positions[k] = swirling * (1f - sink * 0.8f);
                    scale       *= 1f - sink;
                }
                chain.Members[k].Position = positions[k] * Size;
                chain.Members[k].Scale    = Vector3.One * Mathf.Max(scale, 0.0005f) * Size;
            }

            // Bonds grow in with the linking and go with their molecules
            float thickness = 0.022f * bond * (isRna ? 1f - fade : 1f - sink);
            for (int k = 0; k < chain.Bonds.Length; k++)
            {
                chain.Bonds[k].Transform = Rod(positions[k] * Size, positions[k + 1] * Size, Mathf.Max(thickness, 0.0005f) * Size);
            }
        }

        // ---------------------------------------------------------------------------
        // Lipids
        //   Drift onto the shell, melt into the membrane once it closes.
        // ---------------------------------------------------------------------------

        float closing = Ease.Window(_time, MEMBRANE_START, MEMBRANE_END);
        foreach (Lipid lipid in _lipids)
        {
            // Drifting wide on their orbit, then a swirl onto their spot on the shell
            float   gather = Ease.InOut(Ease.Window(_time, LIPID_START + lipid.Delay, LIPID_END - 0.5f + lipid.Delay));
            float   angle  = lipid.Angle + _time * 0.6f;
            var     drift  = new Vector3(Mathf.Cos(angle) * lipid.Radius, lipid.Height, Mathf.Sin(angle) * lipid.Radius);
            Vector3 spot   = lipid.Target.Rotated(Vector3.Up, (1f - gather) * 1.8f);
            float   melt   = 1f - Ease.Smooth(closing);
            float   appear = Ease.Window(_time, 0.2f, 0.8f);
            Basis   facing = Basis.Identity.Slerp(lipid.Facing, gather);
            Vector3 scale  = new Vector3(0.035f, 0.11f, 0.035f) * Mathf.Max(appear * melt, 0.005f) * Size;
            lipid.Mesh.Transform = new Transform3D(facing * Basis.FromScale(scale), drift.Lerp(spot, gather) * Size);
        }

        // ---------------------------------------------------------------------------
        // Membrane
        //   Fades in over the shell and beats twice, the warm light swells with it.
        // ---------------------------------------------------------------------------

        float beat = 0f;
        foreach (float time in BEAT_TIMES)
        {
            beat = Mathf.Max(beat, Ease.Pulse(_time, time, BEAT_SECONDS));
        }
        float bubble = closing > 0f ? Mathf.Lerp(0.92f, 1f, Ease.BackOut(closing)) * (1f + 0.08f * beat) * Size : 0.001f;
        _membrane.Scale = new Vector3(bubble, bubble * CellVisual3D.FLATTEN, bubble);
        _membraneMaterial.SetShaderParameter("body_color", new Color(BodyColor, 0.8f * Ease.Smooth(closing)));

        // The light gathers with the molecules, swells at each beat and hands over softly
        float warmth = Ease.Smooth(Ease.Window(_time, 1f, MEMBRANE_END)) * (1f - Ease.Smooth(Ease.Window(_time, FADE_START, DURATION)));
        _warmLight.LightEnergy = WARM_ENERGY * warmth * (1f + 0.6f * beat);

        // ---------------------------------------------------------------------------
        // Birth
        //   Emits Finished once.
        // ---------------------------------------------------------------------------

        if (_time >= DURATION && !_isFinished)
        {
            _isFinished = true;
            Log.Verbose("Intro", "Protocell born -> handing over to Main.");
            EmitSignal(SignalName.Finished);
        }
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   Rod         Transform of the unit rod (RodMesh, along +Y) from one point to another, also used by RnaWorld.
    //   ShellSpot   Spot i of LIPIDS on the flattened membrane, evenly spread, and its facing outwards.
    //

    /// <summary>Transform that stretches the unit rod from one point to another.</summary>
    /// <param name="from">(Required) Start point, local.</param>
    /// <param name="to">(Required) End point, local.</param>
    /// <param name="radius">(Required) Rod radius.</param>
    public static Transform3D Rod(Vector3 from, Vector3 to, float radius)
    {
        Vector3 direction = to - from;
        float   length    = direction.Length();
        if (length < 0.0001f)
        {
            return new Transform3D(Basis.FromScale(Vector3.One * 0.0001f), from);
        }

        // The arc from +Y is undefined for a rod pointing straight down, a half turn does it
        Vector3 unit   = direction / length;
        Basis   facing = unit.Dot(Vector3.Up) < -0.9999f ? new Basis(Vector3.Right, Mathf.Pi) : new Basis(new Quaternion(Vector3.Up, unit));
        return new Transform3D(facing * Basis.FromScale(new Vector3(radius, length, radius)), (from + to) * 0.5f);
    }

    /// <summary>
    /// Fibonacci sphere: height 1 - 2 (i + 0.5) / n, one golden angle further around per
    /// spot, which spreads the spots evenly. Flattened like the membrane (FLATTEN).
    /// </summary>
    private static (Vector3 Spot, Basis Facing) ShellSpot(int index)
    {
        const float GOLDEN_ANGLE = 2.39996f;
        float   y      = 1f - 2f * (index + 0.5f) / LIPIDS;
        float   ring   = Mathf.Sqrt(1f - y * y);
        float   around = index * GOLDEN_ANGLE;
        var     normal = new Vector3(Mathf.Cos(around) * ring, y, Mathf.Sin(around) * ring);
        Vector3 spot   = new Vector3(normal.X, normal.Y * CellVisual3D.FLATTEN, normal.Z) * 0.98f;
        Basis   facing = normal.Dot(Vector3.Up) < -0.9999f ? new Basis(Vector3.Right, Mathf.Pi) : new Basis(new Quaternion(Vector3.Up, normal));
        return (spot, facing);
    }
}
