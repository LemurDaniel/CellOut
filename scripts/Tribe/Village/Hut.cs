// NOTE:
//   A hut of the tribe on the plane: a round wall of clay with a cone roof of grass,
//   built next to the fire. Every hut raises a villager after VillagerSeconds (Raise),
//   up to VillagersPerHut. Logic in 2D, rendered in 3D by primitives, the
//   TribeCommander keeps GroundHeight up to date (the ground scales with the player).
//
//   var hut = new Hut { Position = spot, Size = coast.Unit, GroundHeight = ground };
//   AddChild(hut);
//   if (hut.Raise(dt)) pack.SpawnVillager(hut.Position);   // in the commander

//
//   Chapters:
//     HUT   Wall, roof, door, the villagers it raises and the placement on the ground.
//

using Godot;

namespace CellOut;

public partial class Hut : Node2D
{
    // ===========================================================================
    // =================================   HUT   =================================
    // ===========================================================================
    //
    //   Size            (Required) Unit of the coast, the hut scales with it.
    //   GroundHeight    (Required) Ground below in world units, kept up to date by the commander.
    //   VillagersLeft   Villagers this hut still raises.
    //   _Ready          Wall, roof and door in unit size.
    //   Raise           Counts down to the next villager, true when one is born.
    //   _Process        Sits on the ground.
    //

    /// <summary>Villagers per hut and how long one takes, from data/balance.json (tribe.village).</summary>
    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    public float Size { get; set; } = 14f;
    public float GroundHeight { get; set; }
    public int VillagersLeft { get; private set; } = Tune.VillagersPerHut;

    private const float HUT_RADIUS = 1.4f;           // in Size
    private const float WALL_HEIGHT = 1.2f;
    private const float ROOF_HEIGHT = 1.3f;
    private static readonly Color CLAY_COLOR = new(0.72f, 0.55f, 0.38f);
    private static readonly Color ROOF_COLOR = new(0.62f, 0.56f, 0.28f);
    private static readonly Color DOOR_COLOR = new(0.2f, 0.14f, 0.08f);

    private Node3D _visual;
    private float _raiseTimer = Tune.VillagerSeconds;

    public override void _Ready()
    {
        _visual = new Node3D { Basis = new Basis(Vector3.Up, Rand.Angle()) };
        AddChild(_visual);

        // Round clay wall, grass roof on top, a dark door in the wall
        CellVisual3D.AddMesh(_visual, CellVisual3D.RodMesh(), CellVisual3D.Material(CLAY_COLOR, 0f, 0.95f), new Vector3(0f, WALL_HEIGHT * 0.5f, 0f), new Vector3(HUT_RADIUS, WALL_HEIGHT, HUT_RADIUS));
        CellVisual3D.AddMesh(_visual, CellVisual3D.ConeMesh(), CellVisual3D.Material(ROOF_COLOR, 0f, 1f), new Vector3(0f, WALL_HEIGHT + ROOF_HEIGHT * 0.5f, 0f), new Vector3(HUT_RADIUS * 1.25f, ROOF_HEIGHT, HUT_RADIUS * 1.25f));
        CellVisual3D.AddMesh(_visual, CellVisual3D.RodMesh(), CellVisual3D.Material(DOOR_COLOR, 0f, 1f), new Vector3(HUT_RADIUS * 0.98f, WALL_HEIGHT * 0.4f, 0f), new Vector3(0.45f, WALL_HEIGHT * 0.8f, 0.45f));
        Place();
        Log.Verbose("Hut", $"Built at {Position}.");
    }

    /// <summary>Counts down to the next villager.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <returns>True once a villager is born, at most VillagersPerHut times.</returns>
    public bool Raise(float dt)
    {
        if (VillagersLeft <= 0)
        {
            return false;
        }
        _raiseTimer -= dt;
        if (_raiseTimer > 0f)
        {
            return false;
        }
        _raiseTimer = Tune.VillagerSeconds;
        VillagersLeft--;
        return true;
    }

    public override void _Process(double delta)
    {
        Place();
    }

    /// <summary>On the ground at its plane point, scaled with the unit of the coast, dropped with the planet.</summary>
    private void Place()
    {
        _visual.GlobalPosition = Plane3D.To3D(GlobalPosition, GroundHeight - Plane3D.Drop(GlobalPosition));
        _visual.Scale          = Vector3.One * Size;
    }
}
