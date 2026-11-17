// NOTE:
//   Brings the coast of the organism stage into the world and keeps it around the
//   player: spawns the archipelago once, right when the organism swims (or by cheat),
//   lets it follow the view and tells the player where land is. The coast becomes a child
//   of the keeper and is known to everyone through the session. It does not process on
//   its own: Main calls Update at the start of every physics frame.
//
//   var coast = new CoastKeeper();
//   coast.Setup(session, followCamera);
//   AddChild(coast);
//   coast.Update(isSpawnAllowed: !IsEvolving);   // in _PhysicsProcess

//
//   Chapters:
//     SETTINGS     The session and the camera.
//     KEEP COAST   The update per frame and the spawn.
//

using Godot;

namespace CellOut;

public partial class CoastKeeper : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Setup   The session and the camera (view radius, draw reach), before AddChild.
    //

    private WorldSession _session;
    private FollowCamera _camera;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) The player, receives the coast once it spawned.</param>
    /// <param name="camera">(Required) The camera, its view decides how far the coast is drawn.</param>
    public void Setup(WorldSession session, FollowCamera camera)
    {
        _session = session;
        _camera  = camera;
    }

    // ===========================================================================
    // =============================   KEEP COAST   ==============================
    // ===========================================================================
    //
    //   Update       Spawns the coast once, keeps it around the player, tells the player where land is.
    //   SpawnCoast   An archipelago ahead of the player, its first shore far off or right in front.
    //   RestoreCoast The archipelago of a save.
    //

    private const float TRUNK_CONTACT = 0.5f;        // the body touches a trunk this many sizes from its center

    /// <summary>One physics frame of the coast: the islands show up as soon as the organism swims (decision of the user 2026-11-17, no sea-only phase).</summary>
    /// <param name="isSpawnAllowed">(Required) False while the evolution still runs, the coast waits for the swimming organism.</param>
    public void Update(bool isSpawnAllowed)
    {
        if (null == Coast && Player.IsOrganism && isSpawnAllowed)
        {
            SpawnCoast(isNear: false);
        }
        if (null == Coast)
        {
            return;
        }
        float size = Player.BodySize();
        Coast.Follow(Player.GlobalPosition, size, _camera.ViewRadius(), _camera.Camera.Far, _camera.View);
        Player.IsOnLand   = Coast.IsOnLand(Player.GlobalPosition);
        Player.GroundLift = Player.IsOnLand ? Coast.SurfaceLift(Player.GlobalPosition, size) : 0f;

        // Trunks block the way, the body slides around them
        if (Player.IsOnLand)
        {
            Player.Position = Coast.PushOutOfTrees(Player.Position, size * TRUNK_CONTACT);
        }
    }

    /// <summary>The archipelago of a save instead of a new one (Game/Flow/SaveKeeper.cs).</summary>
    /// <param name="save">(Required) The saved world.</param>
    public Coast RestoreCoast(SaveWorld save)
    {
        if (null != Coast)
        {
            return Coast;
        }
        Coast coast = Coast.FromSave(save);
        _session.AttachCoast(coast);
        AddChild(coast);
        coast.Follow(Player.GlobalPosition, Player.BodySize(), _camera.ViewRadius());
        Log.Verbose("CoastKeeper", $"Coast restored with {save.Islands.Count} islands and {save.Pads.Count} foundations.");
        return coast;
    }

    /// <summary>An archipelago ahead of the player (its facing), the landing island first.</summary>
    /// <param name="isNear">(Required) Right in front (cheat) instead of just outside the view.</param>
    /// <returns>The coast, also when it already existed.</returns>
    public Coast SpawnCoast(bool isNear)
    {
        if (null != Coast)
        {
            return Coast;
        }
        float   size      = Player.BodySize();
        Vector2 direction = Vector2.FromAngle(Player.Facing);
        float   distance  = isNear ? size * 4f : _camera.ViewRadius() * 1.1f;
        Coast coast = Coast.Scatter(Player.GlobalPosition, direction, distance, size, GD.Randi());
        _session.AttachCoast(coast);
        AddChild(coast);
        coast.Follow(Player.GlobalPosition, size, _camera.ViewRadius());
        Log.Verbose("CoastKeeper", $"Coast spawned {distance:F0} ahead (near: {isNear}).");
        return coast;
    }
}
