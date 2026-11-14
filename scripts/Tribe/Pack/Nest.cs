// NOTE:
//   The nest of the pack on the plane: a mossy mound with a hollow, woven from twigs, the
//   spawn point of the pack. Once the tribe is founded a fire burns in the middle
//   (LightFire): a stone ring with logs, flickering flames and a warm light. Logic in 2D,
//   nest and fire pit are generated models (tools/blender/models/village.py, loaded by
//   Core/PartModels.cs), the flames are glowing primitives. The PackKeeper keeps
//   GroundHeight up to date (the ground scales with the player).
//
//   var nest = new Nest { Position = spot, Size = player.BodySize(), GroundHeight = ground };
//   AddChild(nest);
//   nest.LightFire();   // when the tribe is founded

//
//   Chapters:
//     NEST   Mound, sticks, grass, the fire and the placement on the ground.
//

using Godot;

namespace CellOut;

public partial class Nest : Node2D
{
    // ===========================================================================
    // =================================   NEST   ================================
    // ===========================================================================
    //
    //   Size           (Required) Player.BodySize() when built, the nest scales with it.
    //   GroundHeight   (Required) Ground below in world units, kept up to date by the PackKeeper.
    //   IsLit          Whether the fire burns.
    //   Glow           0..1 how high the fire burns, the tribe sets it from its stock.
    //   _Ready         The nest model, scaled to the player.
    //   LightFire      The fire pit model, flames and the light of the tribe.
    //   _Process       Sits on the ground, the flames flicker.
    //

    public float Size { get; set; } = 14f;
    public float GroundHeight { get; set; }
    public bool IsLit { get; private set; }

    /// <summary>(Optional) 0 a glimmer .. 1 a roaring fire, the tribe sets it from its stock. Defaults to 1.</summary>
    public float Glow { get; set; } = 1f;

    private const float GLOW_MIN_LIGHT = 0.8f;       // light energy at Glow 0, up to GLOW_MIN_LIGHT + GLOW_LIGHT_RANGE
    private const float GLOW_LIGHT_RANGE = 1.2f;
    private const float GLOW_MIN_FLAME = 0.6f;       // flame height at Glow 0, up to GLOW_MIN_FLAME + GLOW_FLAME_RANGE
    private const float GLOW_FLAME_RANGE = 0.6f;

    private const string NEST_MODEL = "res://assets/models/village/village.nest.glb";
    private const string FIRE_MODEL = "res://assets/models/village/village.fire.glb";
    private const float NEST_SCALE = 3.4f;           // model width (1 unit) in Size
    private const float FIRE_SCALE = 1.5f;
    private static readonly Color EARTH_COLOR = new(0.42f, 0.3f, 0.18f);
    private static readonly Color FLAME_COLOR = new(1f, 0.55f, 0.15f);
    private static readonly Color FLAME_CORE = new(1f, 0.9f, 0.5f);

    private Node3D _visual;
    private Node3D _fire;
    private OmniLight3D _light;
    private float _time;

    private Node3D _flames;

    public override void _Ready()
    {
        _visual = new Node3D { Basis = new Basis(Vector3.Up, Rand.Angle()) };
        AddChild(_visual);
        Node3D nest = PartModels.New(NEST_MODEL, EARTH_COLOR);
        nest.Scale = Vector3.One * NEST_SCALE;
        _visual.AddChild(nest);
        Place();
        Log.Verbose("Nest", $"Built at {Position}, size {Size:F1}.");
    }

    /// <summary>The fire of the tribe in the hollow: the fire pit model, two flickering flames and a warm light.</summary>
    public void LightFire()
    {
        if (IsLit)
        {
            return;
        }
        IsLit = true;
        _fire = new Node3D { Position = new Vector3(0f, NEST_SCALE * 0.1f, 0f) };
        _visual.AddChild(_fire);
        Node3D pit = PartModels.New(FIRE_MODEL, EARTH_COLOR);
        pit.Scale = Vector3.One * FIRE_SCALE;
        _fire.AddChild(pit);

        // Flames over the embers, scaled by the flicker, a core and an outer tongue
        _flames = new Node3D { Position = new Vector3(0f, FIRE_SCALE * 0.06f, 0f) };
        _fire.AddChild(_flames);
        CellVisual3D.AddMesh(_flames, CellVisual3D.ConeMesh(), CellVisual3D.Material(FLAME_COLOR, 1.8f, 0.6f), new Vector3(0f, 0.3f, 0f), new Vector3(0.32f, 0.6f, 0.32f));
        CellVisual3D.AddMesh(_flames, CellVisual3D.ConeMesh(), CellVisual3D.Material(FLAME_CORE, 2.5f, 0.6f), new Vector3(0f, 0.25f, 0f), new Vector3(0.17f, 0.42f, 0.17f));
        _light = new OmniLight3D
        {
            LightColor    = FLAME_COLOR,
            LightEnergy   = 1.6f,
            OmniRange     = 6f,
            Position      = new Vector3(0f, 0.8f, 0f),
            ShadowEnabled = false,
        };
        _fire.AddChild(_light);
        Log.Verbose("Nest", "The fire burns.");
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        Place();
        if (null != _flames)
        {
            float flicker = 1f + Mathf.Sin(_time * 9f) * 0.08f + Mathf.Sin(_time * 23f) * 0.05f;
            float glow    = Mathf.Clamp(Glow, 0f, 1f);
            _flames.Scale      = new Vector3(1f, (GLOW_MIN_FLAME + GLOW_FLAME_RANGE * glow) * flicker, 1f);
            _light.LightEnergy = (GLOW_MIN_LIGHT + GLOW_LIGHT_RANGE * glow) * flicker;
        }
    }

    /// <summary>On the ground at its plane point, scaled with the player, dropped with the planet.</summary>
    private void Place()
    {
        _visual.GlobalPosition = Plane3D.To3D(GlobalPosition, GroundHeight - Plane3D.Drop(GlobalPosition));
        _visual.Scale          = Vector3.One * Size;
    }
}
