// NOTE:
//   The galley of the civilization as one node (assets/models/civ/civ.ship.glb from
//   tools/blender/models/ships.py): sail, shields and pennant in a color, placed on the
//   waterline of a plane point, turned to a facing and rocking on the waves. Used by the
//   chieftain's ship (Civ/ShipKeeper.cs) and the ships of the neighbors that land on the
//   player's island (Tribe/Rivals/RivalTribe.cs).
//
//   Node3D ship = ShipModel.New(color);
//   AddChild(ship);
//   ShipModel.Place(ship, point, facing, Player.BodySize(), time);   // every frame

//
//   Chapters:
//     SHIP   The model in a color and its place on the sea.
//

using Godot;

namespace CellOut;

public static class ShipModel
{
    // ===========================================================================
    // =================================   SHIP   ================================
    // ===========================================================================
    //
    //   New     The model, top level in world space, sail and shields in a color.
    //   Place   On the waterline under a plane point, turned to a facing, rocking with the time.
    //

    private const string MODEL = "res://assets/models/civ/civ.ship.glb";
    private const float SCALE = 2.6f;                // model units (about 2.6 long) per body size: a chieftain sits inside the hull
    private const float LIFT = 0.12f;                // body sizes the waterline of the model lies below the sea plane
    private const float ROCK_SPEED = 1.7f;           // radians per second of the rocking
    private const float ROCK_ROLL = 0.06f;
    private const float ROCK_PITCH = 0.035f;

    /// <summary>A new ship, top level in world space.</summary>
    /// <param name="color">(Required) Color of sail, shields, pennant and canopy, the trim a little darker.</param>
    public static Node3D New(Color color)
    {
        var ship = new Node3D { TopLevel = true };
        ship.AddChild(PartModels.New(MODEL, color));
        return ship;
    }

    /// <summary>Puts a ship on the sea: on the waterline under a plane point, turned to a facing, rocking.</summary>
    /// <param name="ship">(Required) A ship of New.</param>
    /// <param name="point">(Required) Plane point of its middle.</param>
    /// <param name="facing">(Required) Facing angle on the plane, the bow points that way.</param>
    /// <param name="size">(Required) Body size of the player, the ship scales with it.</param>
    /// <param name="time">(Required) Seconds, for the rocking; a different start per ship looks less mechanical.</param>
    public static void Place(Node3D ship, Vector2 point, float facing, float size, float time)
    {
        ship.GlobalPosition = Plane3D.To3D(point, -LIFT * size - Plane3D.Drop(point));
        ship.Rotation       = new Vector3(Mathf.Sin(time * ROCK_SPEED * 0.7f) * ROCK_PITCH, Plane3D.FacingToYaw(facing), Mathf.Sin(time * ROCK_SPEED) * ROCK_ROLL);
        ship.Scale          = Vector3.One * size * SCALE;
    }
}
