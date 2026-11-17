// NOTE:
//   Giant cells in the background of the cell stages: far bigger than the microscopic
//   player, they drift deep below the plane through the view and nothing touches them,
//   they live in their own layer (this node), not in the critter layer the contacts
//   read. The player grows, the giants rise with the band (DeepRule.Placement); once one
//   is small enough to meet (DeepRule.IsReadyToSurface) it rises to the plane and is
//   handed over as a normal cell (Surfaced), from then on it hunts and can be eaten.
//   From the organism on there are no giants. Spawner owns it and advances it.
//
//   var deep = new DeepKeeper();
//   deep.Setup(session, followCamera);
//   deep.Surfaced += critter => CritterLayer.AddChild(critter);
//   AddChild(deep);
//   deep.Advance(dt);   // once per physics frame

//
//   Chapters:
//     SETTINGS        The session, the camera and the hand over signal.
//     KEEP GIANTS     Spawning, placing, rising and handing over.
//

using Godot;

namespace CellOut;

public partial class DeepKeeper : Node2D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Surfaced   A giant reached the plane, the receiver adds it to the critter layer.
    //   Setup      The session and the camera (view radius), before AddChild.
    //   Clear      Removes every giant, after the evolution or a fresh start.
    //

    [Signal] public delegate void SurfacedEventHandler(Critter critter);

    private WorldSession _session;
    private FollowCamera _camera;

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) The player the giants are measured against.</param>
    /// <param name="camera">(Required) The camera, its view radius decides where giants spawn and leave.</param>
    public void Setup(WorldSession session, FollowCamera camera)
    {
        _session = session;
        _camera  = camera;
    }

    /// <summary>Removes every giant at once.</summary>
    public void Clear()
    {
        foreach (Node node in GetChildren())
        {
            node.QueueFree();
        }
        _rising.Clear();
    }

    // ===========================================================================
    // =============================   KEEP GIANTS   =============================
    // ===========================================================================
    //
    //   1. Stage      From the organism on the giants are gone.
    //   2. Spawn      Tops up to Count giants just outside the view, on a course through it.
    //   3. Place      Far ones leave, the others float in the band or rise, risen ones are handed over.
    //
    //   Advance runs the steps, SpawnGiant, HandOver and Pose below serve it.
    //

    private const float SPAWN_DISTANCE = 1.15f;    // view radii from the player to the edge of a new giant
    private const float COURSE_SPREAD = 0.6f;      // the course aims this many view radii beside the player
    private const float LEAVE_DISTANCE = 3.5f;     // view radii, farther giants are gone
    private const float GIANT_HUNTER_SHARE = 0.4f; // the rest are amoebas
    private const float SURFACE_CLEARANCE = 1.3f;  // a risen giant waits until the player is this far off its edge, in player sizes

    private readonly System.Collections.Generic.Dictionary<Critter, float> _rising = new();

    /// <summary>Spawns, places, raises and hands over the giants.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Advance(float dt)
    {
        // ---------------------------------------------------------------------------
        // Stage
        //   From the organism on the giants are gone.
        // ---------------------------------------------------------------------------

        if (Player.IsOrganism)
        {
            if (GetChildCount() > 0)
            {
                Clear();
            }
            return;
        }

        // ---------------------------------------------------------------------------
        // Spawn
        //   Tops up to Count giants just outside the view, on a course through it.
        // ---------------------------------------------------------------------------

        float playerSize = Player.BodySize();
        float viewRadius = _camera.ViewRadius();
        int   alive      = 0;
        foreach (Node node in GetChildren())
        {
            alive += node.IsQueuedForDeletion() ? 0 : 1;
        }
        if (alive < Balance.Current.Critter.Giants.Count)
        {
            SpawnGiant(playerSize, viewRadius);
        }

        // ---------------------------------------------------------------------------
        // Place
        //   Far ones leave, the others float in the band or rise, risen ones are handed over.
        // ---------------------------------------------------------------------------

        foreach (Node node in GetChildren())
        {
            if (node is not Critter giant || giant.IsQueuedForDeletion())
            {
                continue;
            }
            float size     = giant.BodySize();
            float distance = giant.Position.DistanceTo(Player.GlobalPosition);
            if (distance > viewRadius * LEAVE_DISTANCE + size)
            {
                _rising.Remove(giant);
                giant.QueueFree();
                continue;
            }

            // Still too big for the player: floats in the band
            if (!_rising.TryGetValue(giant, out float elapsed))
            {
                if (DeepRule.IsReadyToSurface(size, playerSize))
                {
                    _rising[giant] = 0f;
                    Log.Verbose("DeepKeeper", $"Giant of size {size:F0} rises, the player has size {playerSize:F0}.");
                }
                Pose(giant, DeepRule.Placement(size, playerSize));
                continue;
            }

            // On the way up
            elapsed += dt;
            _rising[giant] = elapsed;
            Pose(giant, DeepRule.Rise(size, playerSize, elapsed));
            bool isClear = distance > size + playerSize * SURFACE_CLEARANCE;
            if (DeepRule.HasRisen(elapsed) && isClear)
            {
                HandOver(giant);
            }
        }
    }

    /// <summary>A new giant at the edge of the view, its course crosses the view beside the player.</summary>
    private void SpawnGiant(float playerSize, float viewRadius)
    {
        float size  = DeepRule.SpawnSize(playerSize, GD.Randf());
        var   kind  = Rand.Chance(GIANT_HUNTER_SHARE) ? CritterKind.Hunter : CritterKind.Amoeba;
        var   giant = new Critter();
        giant.Setup(size, kind, playerSize, Player.Stage);

        Vector2 from   = Vector2.FromAngle(Rand.Angle());
        Vector2 aim    = Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * viewRadius * COURSE_SPREAD * GD.Randf();
        giant.Position = Player.GlobalPosition + from * (viewRadius * SPAWN_DISTANCE + size);
        giant.Drift    = (aim - giant.Position).Normalized();
        giant.Facing   = giant.Drift.Value.Angle();
        Pose(giant, DeepRule.Placement(size, playerSize));
        AddChild(giant);
        Log.Verbose("DeepKeeper", $"Giant {kind} of size {size:F0} drifts in, the player has size {playerSize:F0}.");
    }

    /// <summary>The giant swims on the plane now: it senses, hunts and grazes like every other cell.</summary>
    private void HandOver(Critter giant)
    {
        _rising.Remove(giant);
        giant.Drift       = null;
        giant.Depth       = 0f;
        giant.HeightScale = 1f;
        giant.Fade        = 0f;
        giant.Target      = Player;
        giant.FoodLayer   = _session.FoodLayer;
        giant.MatchSpeed(Player.BodySize());
        RemoveChild(giant);
        EmitSignal(SignalName.Surfaced, giant);
        Log.Verbose("DeepKeeper", $"Giant of size {giant.BodySize():F0} reached the plane and joins the game.");
    }

    /// <summary>Hands depth, squash and fade of a pose to the body, CellVisual3D draws them.</summary>
    private static void Pose(Critter giant, DeepPose pose)
    {
        giant.Depth       = pose.Depth;
        giant.HeightScale = pose.Squash;
        giant.Fade        = pose.Fade;
    }
}
