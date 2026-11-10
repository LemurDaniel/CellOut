// NOTE:
//   Short "plop" effect when something is eaten or pops: a growing ring and
//   six droplets that fade out. Frees itself after DURATION seconds.
//
//   var splash = new Splash { Position = Plane3D.To3D(point), MaxRadius = r, Color = color };

//
//   Chapters:
//     PLOP EFFECT   Builds ring and droplets, grows and fades them.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Splash : Node3D
{
    // ===========================================================================
    // =============================   PLOP EFFECT   =============================
    // ===========================================================================
    //
    //   1. Build    One shared fading material for the ring and six droplets.
    //   2. Animate  Grows the ring, throws the droplets out and up, frees itself at the end.
    //

    private const float DURATION = 0.45f;
    private const int DROPLETS = 6;

    /// <summary>(Optional) Color of the effect. Defaults to white.</summary>
    public Color Color { get; set; } = Colors.White;

    /// <summary>(Optional) Final radius of the ring. Defaults to 20.</summary>
    public float MaxRadius { get; set; } = 20f;

    private float _life;
    private StandardMaterial3D _material;
    private MeshInstance3D _ring;
    private readonly List<MeshInstance3D> _droplets = new();

    // ---------------------------------------------------------------------------
    // Build
    //   One shared fading material for the ring and six droplets.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor  = Color,
        };
        _ring = CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), _material, Vector3.Zero, Vector3.One);
        _ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        for (int i = 0; i < DROPLETS; i++)
        {
            MeshInstance3D droplet = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), _material, Vector3.Zero, Vector3.One);
            droplet.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _droplets.Add(droplet);
        }
        UpdateEffect();
    }

    // ---------------------------------------------------------------------------
    // Animate
    //   Grows the ring, throws the droplets out and up, frees itself at the end.
    // ---------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        _life += (float)delta;
        if (_life >= DURATION)
        {
            QueueFree();
            return;
        }
        UpdateEffect();
    }

    private void UpdateEffect()
    {
        float t     = _life / DURATION;
        Color faded = Color;
        faded.A               = 1f - t;
        _material.AlbedoColor = faded;

        float ringRadius = MaxRadius * (0.3f + t);
        _ring.Scale = new Vector3(ringRadius, ringRadius * 0.6f, ringRadius);
        for (int i = 0; i < _droplets.Count; i++)
        {
            Vector3 direction = Vector3.Right.Rotated(Vector3.Up, i * Mathf.Tau / DROPLETS + t);
            _droplets[i].Position = direction * MaxRadius * (0.4f + t * 1.1f) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * MaxRadius * 0.5f;
            _droplets[i].Scale    = Vector3.One * Mathf.Max(MaxRadius * 0.08f * (1f - t), 0.001f);
        }
    }
}
