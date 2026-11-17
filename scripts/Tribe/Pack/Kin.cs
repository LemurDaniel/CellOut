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
    //   Toughen      Another build: the lone beast, a small prey animal.
    //   Pace         Its pace instead of the pace of its body plan.
    //   Watch        Stands and looks at a point for a while.
    //   Calm         Calmed by a courtship, it starts no fight for a while.
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

    private const float WARRIOR_SIZE = 1.12f;        // a warrior is a little bigger
    private const float WARRIOR_SHADE = 0.25f;       // and darker than a gatherer

    /// <summary>Whether the kin was trained in a warrior hut: more health, harder blows.</summary>
    public bool IsWarrior { get; private set; }

    /// <summary>Multiplier of the damage it deals in a fight or to a building.</summary>
    public float DamageScale => IsWarrior ? Village.WarriorDamage : _damageScale;

    private float _damageScale = 1f;

    /// <summary>Another build than a common kin: a lone beast with many times the health and the bite, a small prey animal with a fraction.</summary>
    /// <param name="healthScale">(Required) Its health in healths of a kin (KinHealth), above 0.</param>
    /// <param name="damageScale">(Required) Its damage in damages of a kin, 0 for an animal that never bites.</param>
    /// <example><code>beast.Toughen(4f, 2.5f);   // the lone beast</code></example>
    public void Toughen(float healthScale, float damageScale)
    {
        if (healthScale <= 0f || damageScale < 0f)
        {
            throw new System.ArgumentOutOfRangeException(nameof(healthScale), $"healthScale must be above 0 and damageScale 0 or more, got '{healthScale}' and '{damageScale}'.");
        }
        MaxHp        = Village.KinHealth * healthScale;
        Hp           = MaxHp;
        _damageScale = damageScale;
    }

    /// <summary>(Optional) Its pace in plane units per second instead of the pace of its body plan, e.g. prey that runs nearly as fast as the player. Defaults to none.</summary>
    public float? Pace { get; set; }

    private Vector2 _watchPoint;
    private float _watchLeft;

    /// <summary>Stands still and looks at a point for a while instead of roaming, e.g. at the leader of another pack that comes close.</summary>
    /// <param name="point">(Required) Plane point to look at.</param>
    /// <param name="seconds">(Required) How long, a running watch is replaced.</param>
    public void Watch(Vector2 point, float seconds)
    {
        _watchPoint = point;
        _watchLeft  = seconds;
    }

    private float _calmLeft;

    /// <summary>Calmed by a courtship: it does not start a fight until the time ran out (Tribe/Rivals/PackManners.cs).</summary>
    public bool IsCalm => _calmLeft > 0f;

    /// <summary>Calms the animal for a while: it lets go of whom it attacks and looks at who calmed it.</summary>
    /// <param name="by">(Required) Plane point of who calmed it.</param>
    /// <param name="seconds">(Required) How long, a running calm is only extended.</param>
    public void Calm(Vector2 by, float seconds)
    {
        _calmLeft    = Mathf.Max(_calmLeft, seconds);
        AttackTarget = null;
        Watch(by, seconds);
    }

    /// <summary>Turns the kin into a warrior: WarriorHealth times the health, WarriorDamage times the damage, bigger and darker.</summary>
    public void MakeWarrior()
    {
        IsWarrior = true;
        MaxHp     = Village.KinHealth * Village.WarriorHealth;
        Hp        = MaxHp;
        AdultSize = AdultSize * WARRIOR_SIZE;
        Radius    = AdultSize;
        BodyColor = BodyColor.Darkened(WARRIOR_SHADE);
        Creature.SetColor(BodyColor);
    }

    /// <summary>A newborn: PupSize of the adult size, it grows up over PupGrowSeconds.</summary>
    public void BornSmall()
    {
        Radius    = AdultSize * Tune.PupSize;
        _growLeft = Tune.PupGrowSeconds;
    }

    private bool _isCatchingUp;                      // just called: runs at full pace until it reached the leader

    /// <summary>(Optional) Its place in the formation of the pack while it follows, set every frame by the PackKeeper. Defaults to none: it trails behind the leader.</summary>
    public Vector2? FollowSpot { get; set; }

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
    //   OrderRaze      Walk to an enemy building and tear it down (the RivalKeeper counts the damage).
    //   RazeTarget     The building torn down, null when none.
    //   IsIdle         No order, no chopping, no target: free for gathering or fighting.
    //   IsLeaving      A trader or raider on its way home, freed once it arrives.
    //   GoHome         Walks home and leaves (traders, raiders).
    //   Hp / MaxHp     Hit points, a grown kin has KinHealth.
    //   Hurt           Takes damage, true when it died (and is freed).
    //   Heal           Health back up to the maximum.
    //

    private Vector2? _chopTrunk;
    private float _chopTimer;

    public CellBody AttackTarget { get; private set; }

    public bool IsChopping => null != _chopTrunk && null == OrderTarget;

    /// <summary>Ordered to a trunk: on its way or chopping there.</summary>
    public bool HasChopOrder => null != _chopTrunk;

    public Hut RazeTarget { get; private set; }

    public bool IsIdle => null == OrderTarget && null == _chopTrunk && !IsInstanceValid(AttackTarget) && !IsInstanceValid(RazeTarget);

    public bool IsLeaving { get; private set; }

    public float Hp { get; private set; } = 30f;
    public float MaxHp { get; private set; } = 30f;

    /// <summary>Walks to a plane point, any chopping or attacking stops.</summary>
    /// <param name="point">(Required) Plane point.</param>
    public void OrderMove(Vector2 point)
    {
        _chopTrunk   = null;
        AttackTarget = null;
        RazeTarget   = null;
        OrderTarget  = point;
    }

    /// <summary>Walks to a trunk and chops there: every ChopSeconds ChopTick reports a load of wood.</summary>
    /// <param name="trunk">(Required) Plane point of the trunk.</param>
    /// <param name="trunkRadius">(Required) Radius of the trunk, it stands just outside it.</param>
    public void OrderChop(Vector2 trunk, float trunkRadius)
    {
        Vector2 away = (GlobalPosition - trunk).LengthSquared() > 0.001f ? (GlobalPosition - trunk).Normalized() : Vector2.Right;
        AttackTarget = null;
        RazeTarget   = null;
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
        RazeTarget   = null;
        AttackTarget = target;
    }

    /// <summary>Walks to an enemy building and tears it down, the damage is counted by the RivalKeeper while it stands there.</summary>
    /// <param name="building">(Required) A building of another faction.</param>
    public void OrderRaze(Hut building)
    {
        Vector2 away = (GlobalPosition - building.Position).LengthSquared() > 0.001f ? (GlobalPosition - building.Position).Normalized() : Vector2.Right;
        _chopTrunk   = null;
        AttackTarget = null;
        RazeTarget   = building;
        OrderTarget  = building.Position + away * (building.BlockRadius + Radius * 0.9f);     // right at its wall, it blocks the way
    }

    /// <summary>Walks home and leaves the island once it arrives (traders and raiders).</summary>
    public void GoHome()
    {
        OrderMove(Home);
        IsLeaving = true;
    }

    /// <summary>Heals up to its maximum health (healing moss of the leader).</summary>
    /// <param name="amount">(Required) Hit points, below 0 counts as 0.</param>
    public void Heal(float amount)
    {
        Hp = Mathf.Min(MaxHp, Hp + Mathf.Max(amount, 0f));
    }

    /// <summary>Takes damage, at zero health it is freed and leaves its fallen body behind (Fallen.cs).</summary>
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
        // Its body stays for a moment: it tips over and sinks together (a look only, no list knows it)
        AddSibling(Fallen.Of(this));
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
    private const float SPOT_REACH = 0.5f;           // share of its size: this close to its place in the formation it stands

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

        _calmLeft = Mathf.Max(0f, _calmLeft - dt);
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
        if (null != RazeTarget && (!IsInstanceValid(RazeTarget) || RazeTarget.IsQueuedForDeletion()))
        {
            RazeTarget = null;
        }
        bool     isFree = Vector2.Zero == desired && null == AttackTarget && null == OrderTarget;
        Vector2? work   = _chopTrunk ?? RazeTarget?.Position;
        if (isFree && work is Vector2 workplace)
        {
            // Chopping or razing: faces its work, the TribeCommander counts the loads (ChopTick), the RivalKeeper the blows
            Facing = Mathf.LerpAngle(Facing, (workplace - GlobalPosition).Angle(), Mathf.Clamp(dt * 4f, 0f, 1f));
        }
        else if (isFree && KinMode.Following == Mode && hasLeader)
        {
            // Its place in the formation of the pack (FollowSpot), without one it trails behind the leader
            Vector2 toLeader = (FollowSpot ?? leader.GlobalPosition) - GlobalPosition;
            float   gap      = toLeader.Length() - (FollowSpot.HasValue ? size * SPOT_REACH : Tune.FollowDistance * leader.BodySize());
            if (gap > 0f)
            {
                // Just called it runs at full pace, settled in it eases up to its place
                desired = toLeader.Normalized() * (_isCatchingUp ? 1f : Mathf.Clamp(gap / size, 0.3f, 1f));
            }
            else
            {
                // Waiting in place: in the formation it looks where the leader looks, else at the leader instead of into the woods
                _isCatchingUp = false;
                Facing = Mathf.LerpAngle(Facing, FollowSpot.HasValue ? leader.Facing : toLeader.Angle(), Mathf.Clamp(dt * 3f, 0f, 1f));
            }
        }
        else if (isFree && KinMode.Wild == Mode && hasLeader
            && GlobalPosition.DistanceTo(leader.GlobalPosition) <= Tune.CallRange * leader.BodySize())
        {
            // In call range: stands still and turns to the leader, the player sees it is close enough
            Facing = Mathf.LerpAngle(Facing, (leader.GlobalPosition - GlobalPosition).Angle(), Mathf.Clamp(dt * 4f, 0f, 1f));
        }
        else if (isFree && _watchLeft > 0f)
        {
            // Watching: stands and looks at what caught its eye (Watch)
            _watchLeft -= dt;
            Facing = Mathf.LerpAngle(Facing, (_watchPoint - GlobalPosition).Angle(), Mathf.Clamp(dt * 4f, 0f, 1f));
        }
        else if (isFree && KinMode.Following != Mode && !IsLeaving)
        {
            desired = Roam(dt, size, coast);
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

        float speed = (Pace ?? SwimSpeed(size) * _landSpeed) * StunFactor;
        Velocity = Velocity.Lerp(desired * speed, Mathf.Clamp(dt * EASE * _acceleration, 0f, 1f));
        Vector2 next = Position + Velocity * dt;
        if (coast.IsOnLand(next))
        {
            Position = coast.PushOutOfObstacles(next, size * TRUNK_CONTACT);
        }
        else
        {
            Velocity = Vector2.Zero;
        }
        IsOnLand   = true;
        GroundLift = coast.SurfaceLift(Position, groundUnit);
    }

    /// <summary>A slow walk to a random point near home, a new one every few seconds or once arrived.</summary>
    private Vector2 Roam(float dt, float size, Coast coast)
    {
        _roamTimer -= dt;
        if (_roamTimer <= 0f || Position.DistanceTo(_roamTarget) < size * 0.5f)
        {
            // Never a spot inside a building: the home of a city lies inside its headquarters
            _roamTarget = coast.PushOutOfObstacles(Home + Vector2.FromAngle(Rand.Angle()) * Tune.WanderRadius * size * Rand.Range(0.2f, 1f), size);
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
