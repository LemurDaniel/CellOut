// NOTE:
//   Short, calm animation for a stage advance: the camera glides in and swings a little
//   around the body, a soft light brightens over it and natural particles tell what
//   happens. Single cell: nutrients drift in and sink into the middle, the nucleus forms.
//   Multicellular: gas bubbles rise from the dividing cells. Land animal: pollen drifts
//   past in the wind. The camera returns to exactly where it started. Runs while the game
//   is paused, Main shows the stage banner and frees the cutscene when Finished fires.
//
//   var cutscene = new StageCutscene { Camera = camera, Center = center, Size = player.BodySize(), Stage = Player.SINGLE_STAGE };
//   AddChild(cutscene);
//   cutscene.Finished += OnStageCutsceneFinished;

//
//   Chapters:
//     SETTINGS        Inputs from Main, the particle looks per stage and the timeline.
//     BUILD           Particles of the stage and the soft light.
//     PLAY TIMELINE   Camera, particles, light, Finished.
//     HELPERS         The path of one particle per stage.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class StageCutscene : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Camera      (Required) The world camera, glided in and back out.
    //   Center      (Required) Center of the player body in 3D.
    //   Size        (Required) Body size of the player, everything scales with it.
    //   Stage       (Required) The stage just reached, picks the particles.
    //   Finished    Camera back, Main takes over.
    //
    //   Timeline in seconds:
    //     0.0 - 0.8   camera glides in, the light brightens
    //     0.1 - 1.9   the particles of the stage
    //     1.6 - 2.4   camera glides back to its start pose, the light fades
    //     2.4         Finished
    //

    [Signal] public delegate void FinishedEventHandler();

    public Camera3D Camera { get; set; }
    public Vector3 Center { get; set; }
    public float Size { get; set; } = 14f;
    public int Stage { get; set; } = Player.SINGLE_STAGE;

    private const float DURATION = 2.4f;
    private const float GLIDE_END = 0.8f;
    private const float RETURN_START = 1.6f;
    private const float GLIDE_DEPTH = 0.4f;           // share of the camera distance the glide covers
    private const float SWING = 0.3f;                 // radians the camera swings around the body
    private const float PARTICLES_START = 0.1f;
    private const float PARTICLES_END = 1.9f;
    private const int PARTICLES = 28;
    private const float LIGHT_ENERGY = 1.4f;
    private static readonly Color LIGHT_COLOR = new(1f, 0.92f, 0.78f);
    private static readonly Color[] NUTRIENT_COLORS = { new(0.95f, 0.72f, 0.3f), new(0.62f, 0.4f, 0.75f), new(0.6f, 0.85f, 0.5f) };
    private static readonly Color BUBBLE_COLOR = new(0.85f, 0.95f, 1f, 0.45f);
    private static readonly Color POLLEN_COLOR = new(1f, 0.92f, 0.55f);

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready     Keeps running while paused, builds the particles and the light.
    //   Particle   Material of particle i for the stage.
    //

    private sealed record Mote(MeshInstance3D Mesh, float Angle, float Radius, float Height, float Delay, float Scale);

    private readonly List<Mote> _motes = new();
    private OmniLight3D _light;
    private Transform3D _cameraStart;
    private float _time;
    private bool _isFinished;

    public override void _Ready()
    {
        ProcessMode  = ProcessModeEnum.Always;
        Position     = Center;
        _cameraStart = Camera.GlobalTransform;

        for (int i = 0; i < PARTICLES; i++)
        {
            MeshInstance3D mesh = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), Particle(i), Vector3.Zero, Vector3.One * 0.001f);
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _motes.Add(new Mote(mesh, Rand.Angle(), Rand.Range(0.2f, 1f), Rand.Range(0f, 1f), Rand.Range(0f, 0.6f), Rand.Range(0.035f, 0.07f)));
        }
        _light = new OmniLight3D
        {
            LightColor  = LIGHT_COLOR,
            OmniRange   = Size * 5f,
            LightEnergy = 0f,
            Position    = Vector3.Up * Size * 1.6f,
        };
        AddChild(_light);
        Log.Verbose("StageCutscene", $"Started for stage {Stage}, size {Size:F1}.");
        UpdateTimeline();
    }

    private Material Particle(int index)
    {
        return Stage switch
        {
            Player.MULTICELLULAR_STAGE => CellVisual3D.Material(BUBBLE_COLOR, 0f, 0.1f),
            Player.LAND_STAGE          => CellVisual3D.Material(POLLEN_COLOR, 0.15f, 0.8f),
            _                          => CellVisual3D.Material(NUTRIENT_COLORS[index % NUTRIENT_COLORS.Length], 0.15f),
        };
    }

    // ===========================================================================
    // ============================   PLAY TIMELINE   ============================
    // ===========================================================================
    //
    //   1. Camera      Glides towards the body with a small swing, ends exactly in its start pose.
    //   2. Particles   Nutrients sink in, bubbles rise or pollen drifts, depending on the stage.
    //   3. Light       Brightens with the glide and fades with the return.
    //   4. Done        Emits Finished once.
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
        //   Glides towards the body with a small swing, ends exactly in its start pose.
        // ---------------------------------------------------------------------------

        float       glide = Ease.InOut(Ease.Window(_time, 0f, GLIDE_END)) * (1f - Ease.InOut(Ease.Window(_time, RETURN_START, DURATION)));
        float       swing = SWING * Mathf.Sin(Ease.Window(_time, 0f, DURATION) * Mathf.Pi);
        Transform3D pivot = Transform3D.Identity.Translated(Center) * new Transform3D(new Basis(Vector3.Up, swing), Vector3.Zero) * Transform3D.Identity.Translated(-Center);
        Transform3D pose  = _cameraStart;
        pose.Origin = _cameraStart.Origin.Lerp(Center, GLIDE_DEPTH * glide);
        Camera.GlobalTransform = _time >= DURATION ? _cameraStart : pivot * pose;

        // ---------------------------------------------------------------------------
        // Particles
        //   Nutrients sink in, bubbles rise or pollen drifts, depending on the stage.
        // ---------------------------------------------------------------------------

        foreach (Mote mote in _motes)
        {
            float life = Ease.Window(_time, PARTICLES_START + mote.Delay, PARTICLES_END - 0.6f + mote.Delay);
            (Vector3 position, float size) = Stage switch
            {
                Player.MULTICELLULAR_STAGE => Bubble(mote, life),
                Player.LAND_STAGE          => Pollen(mote, life),
                _                          => Nutrient(mote, life),
            };
            mote.Mesh.Position = position * Size;
            mote.Mesh.Scale    = Vector3.One * Mathf.Max(size * mote.Scale, 0.0005f) * Size;
        }

        // ---------------------------------------------------------------------------
        // Light
        //   Brightens with the glide and fades with the return.
        // ---------------------------------------------------------------------------

        _light.LightEnergy = LIGHT_ENERGY * glide;

        // ---------------------------------------------------------------------------
        // Done
        //   Emits Finished once.
        // ---------------------------------------------------------------------------

        if (_time >= DURATION && !_isFinished)
        {
            _isFinished = true;
            Log.Verbose("StageCutscene", $"Stage {Stage} shown -> back to Main.");
            EmitSignal(SignalName.Finished);
        }
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   Nutrient   Single cell: drifts in on a slow spiral and shrinks into the middle.
    //   Bubble     Multicellular: rises from the cells, wobbles and pops at the top.
    //   Pollen     Land animal: drifts past in the wind, bobbing.
    //
    //   Each takes the progress 0..1 of its particle and returns the position (in body
    //   sizes) and the size factor.
    //

    /// <summary>Drifts in from around the cell on a slow spiral and shrinks into the middle.</summary>
    private static (Vector3 Position, float Size) Nutrient(Mote mote, float life)
    {
        float inward = Ease.InOut(life);
        float angle  = mote.Angle + life * 2f;
        float radius = Mathf.Lerp(1.6f + mote.Radius * 1.4f, 0.05f, inward);
        float height = Mathf.Lerp(0.2f + mote.Height * 0.6f, 0.05f, inward);
        float size   = Ease.Smooth(Mathf.Min(life * 5f, 1f)) * (1f - Ease.Smooth(Ease.Window(life, 0.75f, 1f)));
        return (new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius), size);
    }

    /// <summary>Rises from inside the cells, wobbles, grows a little and pops at the top.</summary>
    private static (Vector3 Position, float Size) Bubble(Mote mote, float life)
    {
        float radius = mote.Radius * 0.7f + Mathf.Sin(life * 9f + mote.Angle) * 0.06f;
        float height = 0.1f + life * (1.8f + mote.Height);
        float size   = (life > 0f ? 0.8f + life * 0.6f : 0f) * (life < 1f ? 1f : 0f);
        return (new Vector3(Mathf.Cos(mote.Angle) * radius, height, Mathf.Sin(mote.Angle) * radius), size);
    }

    /// <summary>Drifts past along +X in the wind, bobbing up and down.</summary>
    private static (Vector3 Position, float Size) Pollen(Mote mote, float life)
    {
        float across = Mathf.Lerp(-2.5f, 2.5f, life) + Mathf.Cos(mote.Angle) * 0.8f;
        float side   = Mathf.Sin(mote.Angle) * 2f * mote.Radius;
        float height = 0.4f + mote.Height * 1.2f + Mathf.Sin(life * 7f + mote.Angle) * 0.15f;
        float size   = Mathf.Sin(life * Mathf.Pi);
        return (new Vector3(across, height, side), size);
    }
}
