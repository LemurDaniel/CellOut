// NOTE:
//   How the members of the pack move around their leader while they follow: every member
//   has its place on the flanks of the leader (never straight behind it, the third person
//   camera sits there and a tall member would hide the leader), and in the pack stage a
//   member with nothing else to do walks off to food close by. A component of the
//   PackKeeper, which calls FormUp and Forage once per physics frame before it steers the
//   kin; the meal itself (and its share for the leader) is resolved by the PackKeeper.
//
//   var formation = new PackFormation();
//   formation.Setup(session);
//   AddChild(formation);
//   formation.Forage(kin);        // pack stage only
//   formation.FormUp(dt, kin);

//
//   Chapters:
//     SETTINGS    The session.
//     FORMATION   The places on the flanks and the walk to food.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class PackFormation : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Setup   The session, before AddChild.
    //

    private static PackBalance Tune => Balance.Current.Tribe.Pack;

    private WorldSession _session;

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and food layer.</param>
    public void Setup(WorldSession session)
    {
        _session = session;
    }

    // ===========================================================================
    // ==============================   FORMATION   ==============================
    // ===========================================================================
    //
    //   FormUp   Every follower gets its place on the flanks of the leader.
    //   Forage   Following members walk off to food nearby.
    //

    private const float FLANK_ANGLE = 1.65f;         // radians from the heading to the first place beside the leader, just behind its shoulder
    private const float FLANK_STEP = 0.52f;          // radians further back per place
    private const int FLANK_PLACES = 2;              // places per side and row, the gap straight behind the leader stays free for the camera
    private const float ROW_GAP = 1.4f;              // body sizes between two rows
    private const float HEADING_SPEED = 1.6f;        // how fast the formation turns with the leader, per second

    private float _heading;                          // where the formation looks, eased behind the facing of the leader

    /*
      The pack runs beside its leader, not behind it: every member that follows gets a place
      on the flanks, alternating left and right, FLANK_PLACES per side, further rows further
      out. The formation turns with the leader while it walks, eased, so a quick turn does
      not whirl the whole pack around.
    */
    /// <summary>Gives every following kin its place on the flanks of the leader (Kin.FollowSpot), everyone else none.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="pack">(Required) Every kin of the player.</param>
    public void FormUp(float dt, IReadOnlyList<Kin> pack)
    {
        float size = Player.BodySize();
        if (Player.Velocity.Length() > Player.CurrentSpeed() * 0.2f)
        {
            _heading = Mathf.LerpAngle(_heading, Player.Facing, Mathf.Clamp(dt * HEADING_SPEED, 0f, 1f));
        }
        int place = 0;
        foreach (Kin kin in pack)
        {
            if (KinMode.Following != kin.Mode)
            {
                kin.FollowSpot = null;
                continue;
            }
            int   side   = 0 == place % 2 ? 1 : -1;
            int   rank   = place / 2;
            float angle  = FLANK_ANGLE + (rank % FLANK_PLACES) * FLANK_STEP;
            float radius = (Tune.FollowDistance + (rank / FLANK_PLACES) * ROW_GAP) * size + kin.Radius - size;
            kin.FollowSpot = Player.GlobalPosition + Vector2.FromAngle(_heading + side * angle) * radius;
            place++;
        }
    }

    /*
      The pack eats together (pack stage): a grown member that follows and has nothing else
      to do walks off to land food within ForageRange of it, as long as no other member is
      on its way there. What it eats feeds the leader too (PackFoodShare, PackKeeper.Update).
    */
    /// <summary>Sends idle followers to land food close to them.</summary>
    /// <param name="pack">(Required) Every kin of the player.</param>
    public void Forage(IReadOnlyList<Kin> pack)
    {
        float size  = Player.BodySize();
        float range = size * Tune.ForageRange;
        foreach (Kin kin in pack)
        {
            if (KinMode.Following != kin.Mode || kin.IsPup || !kin.IsIdle || kin.Position.DistanceTo(Player.GlobalPosition) > range * 2f)
            {
                continue;
            }
            Food  nearest  = null;
            float distance = range;
            foreach (Node node in _session.FoodLayer.GetChildren())
            {
                if (node is not Food { Habitat: FoodHabitat.Land } food || food.IsEngulfed || food.IsQueuedForDeletion())
                {
                    continue;
                }
                float d = food.Position.DistanceTo(kin.Position);
                if (d < distance && !IsClaimed(food, pack, size))
                {
                    nearest  = food;
                    distance = d;
                }
            }
            if (null != nearest)
            {
                kin.OrderMove(nearest.Position);
            }
        }
    }

    /// <summary>Whether a member is on its way to a piece of food already.</summary>
    private static bool IsClaimed(Food food, IReadOnlyList<Kin> pack, float size)
    {
        foreach (Kin other in pack)
        {
            if (other.OrderTarget is Vector2 target && target.DistanceTo(food.Position) < size * 0.5f)
            {
                return true;
            }
        }
        return false;
    }
}
