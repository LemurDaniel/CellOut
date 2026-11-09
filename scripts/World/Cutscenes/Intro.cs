// NOTE:
//   Start animation: the camera sinks from the menu view down to the player while
//   molecules of the primordial soup swirl together, a membrane bubble closes around
//   them and the protocell is born. Runs while the game is paused, Main shows the real
//   player when Finished fires and frees the intro.
//
//   var intro = new Intro { Camera = camera, CameraEnd = gameplayPose, Center = spawn, Size = player.BodySize(), BodyColor = player.BodyColor };
//   AddChild(intro);
//   intro.Finished += OnIntroFinished;

//
//   Chapters:
//     SETTINGS        Inputs from Main and the timeline.
//     BUILD           Molecules around the spawn and the hidden membrane bubble.
//     PLAY TIMELINE   Camera flight, molecule swirl, membrane closing, birth.
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
    //     0.0 - 3.4   camera flies from its start pose to CameraEnd
    //     0.0 - 2.4   molecules swirl inwards
    //     1.8 - 2.8   the membrane bubble grows and wobbles
    //     2.4 - 3.2   molecules inside shrink away, the bubble glows
    //     3.4         Finished
    //

    [Signal] public delegate void CameraMovedEventHandler(float distance);
    [Signal] public delegate void FinishedEventHandler();

    public Camera3D Camera { get; set; }
    public Transform3D CameraEnd { get; set; }
    public Vector3 Center { get; set; }
    public float Size { get; set; } = 14f;
    public Color BodyColor { get; set; } = new(0.55f, 0.85f, 0.95f);

    private const float DURATION = 3.4f;
    private const float SWIRL_END = 2.4f;
    private const float MEMBRANE_START = 1.8f;
    private const float MEMBRANE_END = 2.8f;
    private const float ABSORB_END = 3.2f;
    private const int MOLECULES = 56;
    private static readonly Shader MEMBRANE_SHADER = GD.Load<Shader>("res://shaders/membrane.gdshader");
    private static readonly Color[] MOLECULE_COLORS =
    {
        new(0.95f, 0.5f, 0.7f),     // RNA pink
        new(0.95f, 0.85f, 0.4f),    // lipid yellow
        new(0.55f, 0.75f, 1f),      // amino blue
        new(0.6f, 0.95f, 0.7f),     // sugar green
    };

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready   Keeps running while paused, scatters the molecules, hides the bubble.
    //

    private sealed record Molecule(MeshInstance3D Mesh, float Angle, float Radius, float Height, float Spin, float Scale);

    private readonly List<Molecule> _molecules = new();
    private MeshInstance3D _membrane;
    private ShaderMaterial _membraneMaterial;
    private Transform3D _cameraStart;
    private float _time;
    private bool _isFinished;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Position    = Center;
        _cameraStart = Camera.GlobalTransform;

        // Molecules on a wide, flat ring around the spawn
        for (int i = 0; i < MOLECULES; i++)
        {
            Color color = MOLECULE_COLORS[i % MOLECULE_COLORS.Length];
            float scale = Rand.Range(0.07f, 0.16f);
            MeshInstance3D mesh = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), CellVisual3D.Material(color, 0.6f),
                Vector3.Zero, Vector3.One * scale * Size);
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _molecules.Add(new Molecule(mesh, Rand.Angle(), Rand.Range(2.5f, 7f), Rand.Range(-0.4f, 0.8f), Rand.Range(0.8f, 1.6f), scale));
        }

        // Membrane bubble, the same shader and look as the protocell
        _membraneMaterial = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        _membraneMaterial.SetShaderParameter("body_color", new Color(BodyColor, 0.8f));
        _membraneMaterial.SetShaderParameter("seed", Rand.Range(0f, 100f));
        _membraneMaterial.SetShaderParameter("core_alpha", 0.2f);
        _membrane = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(), _membraneMaterial, Vector3.Zero, Vector3.One * 0.001f);
        _membrane.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        UpdateTimeline();
    }

    // ===========================================================================
    // ============================   PLAY TIMELINE   ============================
    // ===========================================================================
    //
    //   1. Camera      Eases from the start pose into CameraEnd, reports the distance.
    //   2. Molecules   Spiral inwards, faster the closer they get, then shrink away inside.
    //   3. Membrane    Grows with overshoot, wobbles and flashes once it is closed.
    //   4. Birth       Emits Finished once.
    //

    public override void _Process(double delta)
    {
        _time += (float)delta;
        UpdateTimeline();
    }

    private void UpdateTimeline()
    {
        // ---------------------------------------------------------------------------
        // Camera
        //   Eases from the start pose into CameraEnd.
        // ---------------------------------------------------------------------------

        float flight = EaseInOut(Mathf.Clamp(_time / DURATION, 0f, 1f));
        Camera.GlobalTransform = _cameraStart.InterpolateWith(CameraEnd, flight);
        EmitSignal(SignalName.CameraMoved, Camera.GlobalPosition.DistanceTo(Center));

        // ---------------------------------------------------------------------------
        // Molecules
        //   Spiral inwards, faster the closer they get, then shrink away inside.
        // ---------------------------------------------------------------------------

        float swirl  = Mathf.Clamp(_time / SWIRL_END, 0f, 1f);
        float pull   = swirl * swirl * (3f - 2f * swirl);
        float absorb = Mathf.Clamp((_time - SWIRL_END) / (ABSORB_END - SWIRL_END), 0f, 1f);
        foreach (Molecule molecule in _molecules)
        {
            float radius = Mathf.Lerp(molecule.Radius, 0.45f, pull);
            float angle  = molecule.Angle + _time * molecule.Spin * (1f + 2.5f * pull);
            float height = Mathf.Lerp(molecule.Height, 0.05f, pull) + Mathf.Sin(_time * 3f + molecule.Angle) * 0.08f;
            molecule.Mesh.Position = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius) * Size;
            molecule.Mesh.Scale    = Vector3.One * Mathf.Max(molecule.Scale * (1f - absorb), 0.001f) * Size;
        }

        // ---------------------------------------------------------------------------
        // Membrane
        //   Grows with overshoot, wobbles and flashes once it is closed.
        // ---------------------------------------------------------------------------

        float grow   = Mathf.Clamp((_time - MEMBRANE_START) / (MEMBRANE_END - MEMBRANE_START), 0f, 1f);
        float wobble = 1f + 0.06f * Mathf.Sin(_time * 14f) * (1f - absorb);
        float bubble = Mathf.Max(BackOut(grow) * wobble, 0.001f) * Size;
        _membrane.Scale = new Vector3(bubble, bubble * CellVisual3D.FLATTEN, bubble);
        _membraneMaterial.SetShaderParameter("flash", Mathf.Max(0f, 1f - Mathf.Abs(_time - MEMBRANE_END) * 3f) * 0.6f);

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

    private static float EaseInOut(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    /// <summary>Overshoots to about 1.1 and settles at 1.</summary>
    private static float BackOut(float t)
    {
        const float OVERSHOOT = 1.70158f;
        float u = t - 1f;
        return 1f + (OVERSHOOT + 1f) * u * u * u + OVERSHOOT * u * u;
    }
}
