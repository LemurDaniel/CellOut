// NOTE:
//   The lipid vesicle that ends the RNA prelude (Prelude/RnaWorld.cs): a wobbling
//   membrane bubble (shaders/membrane.gdshader) that pops up once the goal is reached.
//   RnaWorld decides when and where it appears and what happens when the strand swims in.
//
//   var vesicle = new RnaVesicle();
//   vesicle.Setup(size);
//   AddChild(vesicle);
//   vesicle.Appear(spot, time);        // once, the goal is reached
//   vesicle.Animate(time);             // every frame
//   if (vesicle.Contains(head)) { ... }

//
//   Chapters:
//     VESICLE   The vesicle popping up, its wobble and whether a point is inside.
//

using Godot;

namespace CellOut;

public partial class RnaVesicle : Node3D
{
    // ===========================================================================
    // ===============================   VESICLE   ===============================
    // ===========================================================================
    //
    //   Setup         (Required) size: body size of the player, the vesicle scales with it.
    //   HasAppeared   Whether the vesicle exists.
    //   Spot          Plane position of the vesicle.
    //   Appear        Lets the vesicle pop up at a point.
    //   Animate       Grows it in and lets it wobble and flash.
    //   Contains      Whether a plane point is inside the vesicle.
    //

    private const float VESICLE_RADIUS = 1.3f;       // in body sizes
    private static readonly Color VESICLE_COLOR = new(1f, 0.9f, 0.55f);
    private static readonly Shader MEMBRANE_SHADER = GD.Load<Shader>("res://shaders/membrane.gdshader");

    private float _size;
    private MeshInstance3D _mesh;
    private ShaderMaterial _material;
    private float _appearedAt;

    public bool HasAppeared => null != _mesh;
    public Vector2 Spot { get; private set; }

    /// <summary>Hands over the scale, called once before AddChild.</summary>
    /// <param name="size">(Required) Body size of the player.</param>
    public void Setup(float size)
    {
        _size = size;
    }

    /// <summary>Lets the vesicle pop up, it grows in from that moment.</summary>
    /// <param name="spot">(Required) Plane position of the vesicle.</param>
    /// <param name="time">(Required) Time of the prelude in seconds.</param>
    public void Appear(Vector2 spot, float time)
    {
        Spot        = spot;
        _appearedAt = time;
        _material   = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        _material.SetShaderParameter("body_color", new Color(VESICLE_COLOR, 0.6f));
        _material.SetShaderParameter("seed", Rand.Range(0f, 100f));
        _material.SetShaderParameter("core_alpha", 0.15f);
        _mesh = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(), _material, Plane3D.To3D(spot), Vector3.One * 0.001f);
        _mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    /// <summary>Grows the vesicle in and lets it wobble and flash, nothing before Appear.</summary>
    /// <param name="time">(Required) Time of the prelude in seconds.</param>
    public void Animate(float time)
    {
        if (!HasAppeared)
        {
            return;
        }
        float grow = Ease.BackOut(Ease.Window(time, _appearedAt, _appearedAt + 0.8f));
        float size = VESICLE_RADIUS * _size * Mathf.Max(grow, 0.001f) * (1f + 0.05f * Mathf.Sin(time * 3f));
        _mesh.Scale = new Vector3(size, size * CellVisual3D.FLATTEN, size);
        _material.SetShaderParameter("flash", 0.25f + 0.25f * Mathf.Sin(time * 4f));
    }

    /// <summary>Whether a plane point is inside the vesicle.</summary>
    /// <param name="point">(Required) Plane point, e.g. the head of the player strand.</param>
    /// <returns>False before Appear.</returns>
    public bool Contains(Vector2 point)
    {
        return HasAppeared && point.DistanceTo(Spot) <= VESICLE_RADIUS * _size;
    }
}
