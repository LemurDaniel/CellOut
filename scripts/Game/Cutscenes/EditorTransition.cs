// NOTE:
//   Short animation before an editor opens: the camera dives into the body as if it
//   slipped through the membrane, the screen fills with the body color, a few blurry
//   bits of cytoplasm drift by. Finished fires when the screen is covered, Main opens
//   the editor underneath, the camera is back in its start pose and the color fades out
//   over the editor. The transition frees itself after the fade. Runs while the game is paused.
//
//   var transition = new EditorTransition { Camera = camera, Center = center, Size = player.BodySize(), Tint = player.BodyColor };
//   AddChild(transition);
//   transition.Finished += OpenEditorNow;

//
//   Chapters:
//     SETTINGS        Inputs from Main and the timeline.
//     BUILD           Cytoplasm bits and the color overlay.
//     PLAY TIMELINE   Camera dive, drifting bits, the overlay in and out.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class EditorTransition : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Camera      (Required) The world camera, dived in and put back when the screen is covered.
    //   Center      (Required) Center of the player body in 3D.
    //   Size        (Required) Body size of the player, everything scales with it.
    //   Tint        (Optional) Body color, the screen fills with it. Defaults to a soft cyan.
    //   Finished    The screen is covered, Main opens the editor now.
    //
    //   Timeline in seconds:
    //     0.0 - 0.8   camera dives into the body, cytoplasm bits drift towards it
    //     0.45 - 0.8  the screen fills with the body color
    //     0.8         Finished, camera back in its start pose, bits gone
    //     0.8 - 1.2   the color fades out over the editor, then the transition frees itself
    //

    [Signal] public delegate void FinishedEventHandler();

    public Camera3D Camera { get; set; }
    public Vector3 Center { get; set; }
    public float Size { get; set; } = 14f;
    public Color Tint { get; set; } = new(0.55f, 0.85f, 0.95f);

    private const float COVERED = 0.8f;
    private const float FADE_END = 1.2f;
    private const float FILL_START = 0.45f;
    private const float DIVE_DEPTH = 0.92f;           // share of the camera distance, ends just inside the membrane
    private const int BITS = 14;
    private const int OVERLAY_LAYER = 50;             // above the editors (layer 20)
    private static readonly Color[] BIT_COLORS = { new(0.95f, 0.72f, 0.3f, 0.7f), new(0.62f, 0.4f, 0.75f, 0.7f), new(1f, 1f, 1f, 0.35f) };

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready   Keeps running while paused, builds the bits and the overlay.
    //

    private sealed record Bit(MeshInstance3D Mesh, Vector3 Offset, float Scale);

    private readonly List<Bit> _bits = new();
    private ColorRect _overlay;
    private Transform3D _cameraStart;
    private float _time;
    private bool _isFinished;

    public override void _Ready()
    {
        ProcessMode  = ProcessModeEnum.Always;
        Position     = Center;
        _cameraStart = Camera.GlobalTransform;

        // Organelle-like bits around the path of the camera, inside the body
        for (int i = 0; i < BITS; i++)
        {
            Material       material = CellVisual3D.Material(BIT_COLORS[i % BIT_COLORS.Length], 0.1f);
            MeshInstance3D mesh     = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), material, Vector3.Zero, Vector3.One * 0.001f);
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var offset = new Vector3(Rand.Range(-0.7f, 0.7f), Rand.Range(0f, 0.5f), Rand.Range(-0.7f, 0.7f));
            _bits.Add(new Bit(mesh, offset, Rand.Range(0.05f, 0.12f)));
        }

        // Full screen color above the editor, clicks go through to it while it fades
        var layer = new CanvasLayer { Layer = OVERLAY_LAYER };
        AddChild(layer);
        _overlay = new ColorRect { Color = new Color(Tint.Darkened(0.35f), 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_overlay);
        UpdateTimeline();
    }

    // ===========================================================================
    // ============================   PLAY TIMELINE   ============================
    // ===========================================================================
    //
    //   1. Camera    Dives into the body until the screen is covered.
    //   2. Bits      Grow in and drift slowly, the camera passes between them.
    //   3. Overlay   Fills, at full Finished and camera back, then fades and frees.
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
        //   Dives into the body until the screen is covered.
        // ---------------------------------------------------------------------------

        float dive = Ease.InOut(Ease.Window(_time, 0f, COVERED));
        if (!_isFinished)
        {
            Transform3D pose = _cameraStart;
            pose.Origin = _cameraStart.Origin.Lerp(Center, DIVE_DEPTH * dive);
            Camera.GlobalTransform = pose;
        }

        // ---------------------------------------------------------------------------
        // Bits
        //   Grow in and drift slowly, the camera passes between them.
        // ---------------------------------------------------------------------------

        float grow = Ease.Smooth(Ease.Window(_time, 0.15f, 0.5f));
        for (int i = 0; i < _bits.Count; i++)
        {
            Bit     bit   = _bits[i];
            Vector3 drift = new Vector3(Mathf.Sin(_time * 1.3f + i), Mathf.Sin(_time * 0.9f + i * 2f) * 0.5f, Mathf.Cos(_time * 1.1f + i)) * 0.05f;
            bit.Mesh.Position = (bit.Offset + drift) * Size;
            bit.Mesh.Scale    = Vector3.One * Mathf.Max(bit.Scale * grow, 0.0005f) * Size;
        }

        // ---------------------------------------------------------------------------
        // Overlay
        //   Fills, at full Finished and camera back, then fades and frees.
        // ---------------------------------------------------------------------------

        float fill    = Ease.Smooth(Ease.Window(_time, FILL_START, COVERED));
        float fadeOut = Ease.Smooth(Ease.Window(_time, COVERED, FADE_END));
        _overlay.Color = new Color(_overlay.Color, fill * (1f - fadeOut));

        if (_time >= COVERED && !_isFinished)
        {
            _isFinished            = true;
            Camera.GlobalTransform = _cameraStart;
            foreach (Bit bit in _bits)
            {
                bit.Mesh.Visible = false;
            }
            Log.Verbose("EditorTransition", "Screen covered -> editor opens.");
            EmitSignal(SignalName.Finished);
        }
        if (_time >= FADE_END)
        {
            QueueFree();
        }
    }
}
