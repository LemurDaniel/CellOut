// NOTE:
//   A conspecific of the player on the islands: the same body plan (Creature), a shade
//   of its color, walking on land only. Wild it roams around its home, called it follows
//   the leader, at the nest it stays, in the tribe it is a villager that takes orders
//   (CellBody.OrderTarget). Pups are born small and grow up. It does not steer on its
//   own: the PackKeeper calls Steer once per physics frame with the coast, so it never
//   leaves the land and knows the ground below it.
//
//   var kin = new Kin { Position = spot };
//   kin.Setup(player.Creature, player.BodySize(), tint, home: spot);
//   AddChild(kin);
//   kin.Steer(dt, coast, pack, leader, player.BodySize());   // in the pack keeper

//
//   Chapters:
//     SETUP   Body plan, size, color, speed and the mode of life.
//     STEER   Orders, following, roaming, separation and the walk on land.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public enum KinMode { Wild, Following, Staying, Villager }

public partial class Kin : CellBody
{
    // ===========================================================================
    // ================================   SETUP   ================================
    // ===========================================================================
    //
    //   Mode         Wild, following the leader, staying at the nest or a villager of the tribe.
    //   Home         Plane point it roams around while it is not following.
    //   AdultSize    Body size when grown up, a pup grows towards it.
    //   IsPup        Still growing.
    //   Setup        Body plan of the player with a tint, size, speed and home, before AddChild.
    //   BornSmall    Makes it a pup: PupSize of the adult size, grown up after PupGrowSeconds.
    //   Follow       Follows a leader.
    //   Stay         Roams around a home point.
    //   JoinTribe    A villager of the tribe: roams around the fire until ordered.
    //

    /// <summary>Follow distance, roaming radius and pup growth from data/balance.json (tribe.pack).</summary>
    private static PackBalance Tune => Balance.Current.Tribe.Pack;

    public KinMode Mode { get; private set; } = KinMode.Wild;
    public Vector2 Home { get; private set; }
    public float AdultSize { get; private set; } = 14f;
    public bool IsPup => _growLeft > 0f;

    private float _growLeft;
    private float _landSpeed = Balance.Current.Creature.Land.CrawlSpeed;
    private float _acceleration = 1f;

    /// <summary>Sets body plan, size, color and home.</summary>
    /// <param name="design">(Required) The body plan of the player, copied.</param>
    /// <param name="size">(Required) Adult body size on the plane.</param>
    /// <param name="tint">(Required) Body color, a shade of the leader's.</param>
    /// <param name="home">(Required) Plane point it roams around.</param>
    public void Setup(CreatureDesign design, float size, Color tint, Vector2 home)
    {
        if (null == design)
        {
            throw new System.ArgumentNullException(nameof(design), "design must be the body plan of the player.");
        }
        Creature = new CreatureDesign();
        Creature.CopyFrom(design);
        Creature.SetColor(tint);
        BodyColor = tint;
        AdultSize = size;
        Radius    = size;
        Home      = home;
        IsOnLand  = true;

        CreatureStats stats = CreatureStats.From(Creature);
        _landSpeed    = stats.LandSpeed;
        _acceleration = stats.Acceleration;
    }

    /// <summary>A newborn: PupSize of the adult size, it grows up over PupGrowSeconds.</summary>
    public void BornSmall()
    {
        Radius    = AdultSize * Tune.PupSize;
        _growLeft = Tune.PupGrowSeconds;
    }

    /// <summary>Follows the leader from now on.</summary>
    public void Follow()
    {
        Mode = KinMode.Following;
        Log.Verbose("Kin", "Called: follows the leader.");
    }

    /// <summary>Roams around a point from now on (the nest).</summary>
    /// <param name="home">(Required) Plane point to stay around.</param>
    public void Stay(Vector2 home)
    {
        Mode = KinMode.Staying;
        Home = home;
    }

    /// <summary>A villager of the tribe: roams around the fire until the tribe orders it somewhere.</summary>
    /// <param name="fire">(Required) Plane point of the fire.</param>
    public void JoinTribe(Vector2 fire)
    {
        Mode = KinMode.Villager;
        Home = fire;
    }

    // ===========================================================================
    // ================================   STEER   ================================
    // ===========================================================================
    //
    //   1. Grow       A pup grows towards the adult size.
    //   2. Decide     An order wins, then following the leader, else roaming around home.
    //   3. Separate   Keeps apart from the other kin and the leader.
    //   4. Walk       Eases the velocity, never steps into the sea or into a trunk, reads the ground.
    //

    private const float ROAM_SPEED = 0.4f;          // share of the walking speed while roaming
    private const float SEPARATION_WEIGHT = 0.7f;
    private const float TRUNK_CONTACT = 0.5f;        // the body touches a trunk this many sizes from its center
    private const float EASE = 3f;

    private Vector2 _roamTarget;
    private float _roamTimer;

    /// <summary>One physics frame of the kin.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="coast">(Required) The coast, it keeps the kin on land and gives the ground height.</param>
    /// <param name="pack">(Required) Every kin, for the separation.</param>
    /// <param name="leader">(Required) The player, followed when called.</param>
    /// <param name="groundUnit">(Required) Player.BodySize(), the ground heights scale with it.</param>
    public void Steer(float dt, Coast coast, IReadOnlyList<Kin> pack, CellBody leader, float groundUnit)
    {
        // ---------------------------------------------------------------------------
        // Grow
        //   A pup grows towards the adult size.
        // ---------------------------------------------------------------------------

        if (_growLeft > 0f)
        {
            _growLeft = Mathf.Max(0f, _growLeft - dt);
            Radius    = Mathf.Lerp(AdultSize, AdultSize * Tune.PupSize, _growLeft / Tune.PupGrowSeconds);
        }

        // ---------------------------------------------------------------------------
        // Decide
        //   An order wins, then following the leader, else roaming around home.
        // ---------------------------------------------------------------------------

        float   size    = Radius;
        Vector2 desired = OrderDirection();
        if (Vector2.Zero == desired && KinMode.Following == Mode && IsInstanceValid(leader))
        {
            Vector2 toLeader = leader.GlobalPosition - GlobalPosition;
            float   gap      = toLeader.Length() - Tune.FollowDistance * leader.BodySize();
            if (gap > 0f)
            {
                desired = toLeader.Normalized() * Mathf.Clamp(gap / size, 0.3f, 1f);
            }
        }
        else if (Vector2.Zero == desired && KinMode.Following != Mode)
        {
            desired = Roam(dt, size);
        }

        // ---------------------------------------------------------------------------
        // Separate
        //   Keeps apart from the other kin and the leader.
        // ---------------------------------------------------------------------------

        Vector2 separate = Vector2.Zero;
        foreach (Kin other in pack)
        {
            if (other != this && !other.IsQueuedForDeletion())
            {
                separate += PushFrom(other.GlobalPosition, size + other.Radius);
            }
        }
        if (IsInstanceValid(leader))
        {
            separate += PushFrom(leader.GlobalPosition, size + leader.BodySize());
        }
        desired = (desired + separate * SEPARATION_WEIGHT).LimitLength(1f);

        // ---------------------------------------------------------------------------
        // Walk
        //   Eases the velocity, never steps into the sea or into a trunk, reads the ground.
        // ---------------------------------------------------------------------------

        float speed = SwimSpeed(size) * _landSpeed * StunFactor;
        Velocity = Velocity.Lerp(desired * speed, Mathf.Clamp(dt * EASE * _acceleration, 0f, 1f));
        Vector2 next = Position + Velocity * dt;
        if (coast.IsOnLand(next))
        {
            Position = coast.PushOutOfTrees(next, size * TRUNK_CONTACT);
        }
        else
        {
            Velocity = Vector2.Zero;
        }
        IsOnLand   = true;
        GroundLift = coast.SurfaceLift(Position, groundUnit);
    }

    /// <summary>A slow walk to a random point near home, a new one every few seconds or once arrived.</summary>
    private Vector2 Roam(float dt, float size)
    {
        _roamTimer -= dt;
        if (_roamTimer <= 0f || Position.DistanceTo(_roamTarget) < size * 0.5f)
        {
            _roamTarget = Home + Vector2.FromAngle(Rand.Angle()) * Tune.WanderRadius * size * Rand.Range(0.2f, 1f);
            _roamTimer  = Rand.Range(3f, 7f);
        }
        Vector2 toTarget = _roamTarget - Position;
        return toTarget.Length() < size * 0.5f ? Vector2.Zero : toTarget.Normalized() * ROAM_SPEED;
    }

    /// <summary>Push away from a body that overlaps this one, zero when apart.</summary>
    private Vector2 PushFrom(Vector2 other, float contact)
    {
        Vector2 away     = GlobalPosition - other;
        float   distance = away.Length();
        return distance < contact && distance > 0.001f ? away / distance * (1f - distance / contact) : Vector2.Zero;
    }
}
