// NOTE:
//   A conspecific on the islands: the player's body plan in a shade of its color,
//   walking on land only. Wild it roams around its home, called it follows the leader,
//   at the nest it stays, in a tribe it is a villager that takes orders: walk somewhere
//   (CellBody.OrderTarget), chop a tree, attack an enemy. Every kin belongs to a faction
//   (0 the player's tribe, else a rival tribe) and has hit points, rival kin may come as
//   traders or raiders. Pups are born small and grow up. It does not steer on its own:
//   the PackKeeper (or the RivalTribe) calls Steer once per physics frame with the coast.
//
//   var kin = new Kin { Position = spot };
//   kin.Setup(player.Creature, player.BodySize(), tint, home: spot);
//   AddChild(kin);
//   kin.Steer(dt, coast, pack, leader, player.BodySize(), isPackFollowing: true);

//
//   Chapters:
//     SETUP    Body plan, size, color, speed, faction and the mode of life.
//     ORDERS   Walk, chop, attack, go home, and the hit points.
//     STEER    Attack, orders, following, roaming, separation and the walk on land.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public enum KinMode { Wild, Following, Staying, Villager }

public enum KinRole { Villager, Trader, Raider }

public partial class Kin : CellBody
{
    // ===========================================================================
    // ================================   SETUP   ================================
    // ===========================================================================
    //
    //   Mode         Wild, following the leader, staying at the nest or a villager of a tribe.
    //   Role         Villager, or a trader or raider sent by a rival tribe.
    //   Faction      0 the player's tribe, else the rival tribe it belongs to.
    //   Home         Plane point it roams around while it is not following.
    //   AdultSize    Body size when grown up, a pup grows towards it.
    //   IsPup        Still growing.
    //   Setup        Body plan of the player with a tint, size, speed and home, before AddChild.
    //   BornSmall    Makes it a pup: PupSize of the adult size, grown up after PupGrowSeconds.
    //   Follow       Follows a leader.
    //   Stay         Roams around a home point.
    //   JoinTribe    A villager of a tribe: roams around the fire until ordered.
    //

    /// <summary>Follow distance, roaming radius and pup growth from data/balance.json (tribe.pack).</summary>
    private static PackBalance Tune => Balance.Current.Tribe.Pack;

    /// <summary>Health, damage and chopping from data/balance.json (tribe.village).</summary>
    private static VillageBalance Village => Balance.Current.Tribe.Village;

    public KinMode Mode { get; private set; } = KinMode.Wild;
    public KinRole Role { get; set; } = KinRole.Villager;
    public int Faction { get; set; }
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
        MaxHp     = Village.KinHealth;
        Hp        = MaxHp;

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

    private bool _isCatchingUp;                      // just called: runs at full pace until it reached the leader

    /// <summary>Follows the leader from now on, running to catch up first.</summary>
    public void Follow()
    {
        Mode          = KinMode.Following;
        _isCatchingUp = true;
        Log.Verbose("Kin", "Called: follows the leader.");
    }

    /// <summary>Roams around a point from now on (the nest).</summary>
    /// <param name="home">(Required) Plane point to stay around.</param>
    public void Stay(Vector2 home)
    {
        Mode = KinMode.Staying;
        Home = home;
    }

    /// <summary>A villager of a tribe: roams around the fire until the tribe orders it somewhere.</summary>
    /// <param name="fire">(Required) Plane point of the fire.</param>
    public void JoinTribe(Vector2 fire)
    {
        Mode = KinMode.Villager;
        Home = fire;
    }

    // ===========================================================================
    // ===============================   ORDERS   ================================
    // ===========================================================================
    //
    //   OrderMove      Walk to a point, drops chopping and attacking.
    //   OrderChop      Walk to a trunk and chop there (IsChopping once arrived, ChopTick per load).
    //   OrderAttack    Pursue an enemy body until it is gone.
    //   AttackTarget   The enemy pursued, null when none.
    //   IsIdle         No order, no chopping, no target: free for gathering or fighting.
    //   IsLeaving      A trader or raider on its way home, freed once it arrives.
    //   GoHome         Walks home and leaves (traders, raiders).
    //   Hp / MaxHp     Hit points, a grown kin has KinHealth.
    //   Hurt           Takes damage, true when it died (and is freed).
    //

    private Vector2? _chopTrunk;
    private float _chopTimer;

    public CellBody AttackTarget { get; private set; }

    public bool IsChopping => null != _chopTrunk && null == OrderTarget;

    public bool IsIdle => null == OrderTarget && null == _chopTrunk && !IsInstanceValid(AttackTarget);

    public bool IsLeaving { get; private set; }

    public float Hp { get; private set; } = 30f;
    public float MaxHp { get; private set; } = 30f;

    /// <summary>Walks to a plane point, any chopping or attacking stops.</summary>
    /// <param name="point">(Required) Plane point.</param>
    public void OrderMove(Vector2 point)
    {
        _chopTrunk   = null;
        AttackTarget = null;
        OrderTarget  = point;
    }

    /// <summary>Walks to a trunk and chops there: every ChopSeconds ChopTick reports a load of wood.</summary>
    /// <param name="trunk">(Required) Plane point of the trunk.</param>
    /// <param name="trunkRadius">(Required) Radius of the trunk, it stands just outside it.</param>
    public void OrderChop(Vector2 trunk, float trunkRadius)
    {
        Vector2 away = (GlobalPosition - trunk).LengthSquared() > 0.001f ? (GlobalPosition - trunk).Normalized() : Vector2.Right;
        AttackTarget = null;
        _chopTrunk   = trunk;
        _chopTimer   = Village.ChopSeconds;
        OrderTarget  = trunk + away * (trunkRadius + Radius * 0.9f);
    }

    /// <summary>Counts the chopping while the kin stands at its trunk.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <returns>True once per ChopSeconds, a load of wood for the stock.</returns>
    public bool ChopTick(float dt)
    {
        if (!IsChopping)
        {
            return false;
        }
        _chopTimer -= dt;
        if (_chopTimer > 0f)
        {
            return false;
        }
        _chopTimer = Village.ChopSeconds;
        return true;
    }

    /// <summary>Pursues an enemy body, the fight itself is resolved by the RivalKeeper on contact.</summary>
    /// <param name="target">(Required) The enemy, a kin of another faction or the player.</param>
    public void OrderAttack(CellBody target)
    {
        _chopTrunk   = null;
        OrderTarget  = null;
        AttackTarget = target;
    }

    /// <summary>Walks home and leaves the island once it arrives (traders and raiders).</summary>
    public void GoHome()
    {
        OrderMove(Home);
        IsLeaving = true;
    }

    /// <summary>Takes damage, pops out of the world at zero health.</summary>
    /// <param name="amount">(Required) Damage in hit points.</param>
    /// <returns>True when it died.</returns>
    public bool Hurt(float amount)
    {
        if (Hp <= 0f || IsQueuedForDeletion())
        {
            return false;
        }
        Hp       -= amount;
        HurtFlash = 1f;
        if (Hp > 0f)
        {
            return false;
        }
        Log.Verbose("Kin", $"A kin of faction {Faction} ({Role}) died.");
        QueueFree();
        return true;
    }

    // ===========================================================================
    // ================================   STEER   ================================
    // ===========================================================================
    //
    //   1. Grow       A pup grows towards the adult size, grown up it joins the following pack.
    //   2. Decide     An enemy is pursued, an order wins, then following the leader, a wild
    //                 kin in call range turns to the leader and waits, else roaming around home.
    //   3. Separate   Keeps apart from the other kin and the leader.
    //   4. Walk       Eases the velocity, never steps into the sea or into a trunk, reads the ground.
    //

    private const float ROAM_SPEED = 0.4f;          // share of the walking speed while roaming
    private const float SEPARATION_WEIGHT = 0.7f;
    private const float TRUNK_CONTACT = 0.5f;        // the body touches a trunk this many sizes from its center
    private const float CONTACT_SHARE = 1.05f;       // in a fight the bodies stop this close, in sums of their sizes
    private const float EASE = 3f;

    private Vector2 _roamTarget;
    private float _roamTimer;

    /// <summary>One physics frame of the kin.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="coast">(Required) The coast, it keeps the kin on land and gives the ground height.</param>
    /// <param name="pack">(Required) Every kin of its group, for the separation.</param>
    /// <param name="leader">(Optional) The player, followed when called. Null for rival kin.</param>
    /// <param name="groundUnit">(Required) Player.BodySize(), the ground heights scale with it.</param>
    /// <param name="isPackFollowing">(Required) Whether the pack follows right now, a pup that grows up joins in.</param>
    public void Steer(float dt, Coast coast, IReadOnlyList<Kin> pack, CellBody leader, float groundUnit, bool isPackFollowing)
    {
        // ---------------------------------------------------------------------------
        // Grow
        //   A pup grows towards the adult size, grown up it joins the following pack.
        // ---------------------------------------------------------------------------

        if (_growLeft > 0f)
        {
            _growLeft = Mathf.Max(0f, _growLeft - dt);
            Radius    = Mathf.Lerp(AdultSize, AdultSize * Tune.PupSize, _growLeft / Tune.PupGrowSeconds);
            if (_growLeft <= 0f && KinMode.Staying == Mode && isPackFollowing)
            {
                Follow();
                Log.Verbose("Kin", "Grown up: leaves the nest and follows.");
            }
        }

        // ---------------------------------------------------------------------------
        // Decide
        //   An enemy is pursued, an order wins, then following the leader, a wild
        //   kin in call range turns to the leader and waits, else roaming around home.
        // ---------------------------------------------------------------------------

        float   size      = Radius;
        Vector2 desired   = Vector2.Zero;
        bool    hasLeader = IsInstanceValid(leader);
        if (null != AttackTarget && (!IsInstanceValid(AttackTarget) || AttackTarget.IsQueuedForDeletion()))
        {
            AttackTarget = null;
        }
        if (null != AttackTarget)
        {
            Vector2 toEnemy = AttackTarget.GlobalPosition - GlobalPosition;
            float   contact = (size + AttackTarget.BodySize()) * CONTACT_SHARE;
            if (toEnemy.Length() > contact)
            {
                desired = toEnemy.Normalized();
            }
            else
            {
                Facing = Mathf.LerpAngle(Facing, toEnemy.Angle(), Mathf.Clamp(dt * 6f, 0f, 1f));
            }
        }
        else
        {
            desired = OrderDirection();
        }
        bool isFree = Vector2.Zero == desired && null == AttackTarget && null == OrderTarget;
        if (isFree && null != _chopTrunk)
        {
            // Chopping: faces the trunk, the TribeCommander counts the loads (ChopTick)
            Facing = Mathf.LerpAngle(Facing, (_chopTrunk.Value - GlobalPosition).Angle(), Mathf.Clamp(dt * 4f, 0f, 1f));
        }
        else if (isFree && KinMode.Following == Mode && hasLeader)
        {
            Vector2 toLeader = leader.GlobalPosition - GlobalPosition;
            float   gap      = toLeader.Length() - Tune.FollowDistance * leader.BodySize();
            if (gap > 0f)
            {
                // Just called it runs at full pace, settled in it eases up behind the leader
                desired = toLeader.Normalized() * (_isCatchingUp ? 1f : Mathf.Clamp(gap / size, 0.3f, 1f));
            }
            else
            {
                // Waiting in place: looks at the leader instead of into the woods
                _isCatchingUp = false;
                Facing = Mathf.LerpAngle(Facing, toLeader.Angle(), Mathf.Clamp(dt * 3f, 0f, 1f));
            }
        }
        else if (isFree && KinMode.Wild == Mode && hasLeader
            && GlobalPosition.DistanceTo(leader.GlobalPosition) <= Tune.CallRange * leader.BodySize())
        {
            // In call range: stands still and turns to the leader, the player sees it is close enough
            Facing = Mathf.LerpAngle(Facing, (leader.GlobalPosition - GlobalPosition).Angle(), Mathf.Clamp(dt * 4f, 0f, 1f));
        }
        else if (isFree && KinMode.Following != Mode && !IsLeaving)
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
            if (other != this && IsInstanceValid(other) && !other.IsQueuedForDeletion())
            {
                separate += PushFrom(other.GlobalPosition, size + other.Radius);
            }
        }
        if (hasLeader && leader != AttackTarget)
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
