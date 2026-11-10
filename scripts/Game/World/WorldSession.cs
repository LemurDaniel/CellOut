// NOTE:
//   The shared state of the world: the player, the two layers everything swims in and
//   the coast once it showed up. Main creates it and hands it to every component of
//   World/ and Flow/, so none of them has to reach up to Main. No engine calls in here.
//
//   var session = new WorldSession(player, foodLayer, critterLayer);
//   spawner.Setup(session, followCamera);

//
//   Chapters:
//     WORLD SESSION   Player, layers and the coast.
//

using Godot;

namespace CellOut;

public sealed class WorldSession
{
    // ===========================================================================
    // ============================   WORLD SESSION   ============================
    // ===========================================================================
    //
    //   Player         The player, target of camera, contacts and spawning.
    //   FoodLayer      Holds every piece of food.
    //   CritterLayer   Holds every cell and sea creature.
    //   Coast          The coast of the organism stage, null until it showed up.
    //   AttachCoast    Makes the spawned coast known to everyone (CoastKeeper).
    //

    public Player Player { get; }
    public Node2D FoodLayer { get; }
    public Node2D CritterLayer { get; }

    /// <summary>The coast of the organism stage, null before it showed up.</summary>
    public Coast Coast { get; private set; }

    /// <param name="player">(Required) The player.</param>
    /// <param name="foodLayer">(Required) Layer of the food.</param>
    /// <param name="critterLayer">(Required) Layer of the cells and sea creatures.</param>
    public WorldSession(Player player, Node2D foodLayer, Node2D critterLayer)
    {
        Player       = player;
        FoodLayer    = foodLayer;
        CritterLayer = critterLayer;
    }

    /// <summary>Makes the coast known to camera, contacts and spawning. Called once by CoastKeeper.</summary>
    /// <param name="coast">(Required) The coast just spawned.</param>
    public void AttachCoast(Coast coast)
    {
        Coast = coast;
    }
}
