// NOTE:
//   The small animals of the islands: herds of prey (snails, ants, crabs, newts ...) graze
//   on the grass from the land animal on, so the land is alive and a hunter finds meat
//   without killing its neighbors. A herd appears outside the view like the wild kin and
//   roams around its spot. Prey runs from the leader and its pack once they come closer
//   than FleeRange, nearly as fast as the leader walks: it takes the lunge of the hunt
//   (key 3, PackEncounter.HuntPrey) or a pack that cuts off its way, the sea stops it.
//   A herd far behind the player moves on (it is freed), a new one appears ahead.
//   Prey is Kin of the faction PREY_FACTION with a fraction of the health and no bite.
//   Society owns it, Update runs after the pack.
//
//   var wildlife = new Wildlife();
//   wildlife.Setup(session, camera, pack);
//   wildlife.PreyKilled += (position, meat) => encounter.DropFood(position, meat, isMeat: true);
//   AddChild(wildlife);
//   wildlife.Update(dt);
//   Kin prey = wildlife.PreyNear(player.GlobalPosition, range);
//   wildlife.Bite(prey, damage);

//
//   Chapters:
//     SETTINGS   The session, the camera, the pack and the signal.
//     HERDS      Spawning and moving on.
//     LIVE       Fleeing, the bites of the pack and the hunt of the leader.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Wildlife : Node2D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   PreyKilled   A prey animal died where it stood: the spot and the size of the meat it leaves.
    //   Prey         Every prey animal alive.
    //   Setup        The session, the camera and the pack, before AddChild.
    //

    [Signal] public delegate void PreyKilledEventHandler(Vector2 position, float meatSize);

    public const int PREY_FACTION = -1;              // no pack and no tribe

    private static WildlifeBalance Tune => Balance.Current.Tribe.Wildlife;

    private WorldSession _session;
    private FollowCamera _camera;
    private PackKeeper _pack;
    private readonly List<Kin> _prey = new();

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;

    public IReadOnlyList<Kin> Prey => _prey;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="camera">(Required) The camera, herds appear just outside its view.</param>
    /// <param name="pack">(Required) The player's pack: prey runs from its members, their bites count.</param>
    public void Setup(WorldSession session, FollowCamera camera, PackKeeper pack)
    {
        _session = session;
        _camera  = camera;
        _pack    = pack;
    }

    // ===========================================================================
    // ================================   HERDS   ================================
    // ===========================================================================
    //
    //   SpawnHerd   A herd of one species on grass in a ring outside the view.
    //   MoveOn      Prey far behind the player leaves the world.
    //

    private const float SPAWN_NEAR = 1.1f;           // view radii
    private const float SPAWN_FAR = 1.7f;
    private const float GONE_AT = 3.2f;              // view radii from the player a prey animal moves on
    private const int SPAWN_TRIES = 12;
    private const float HERD_SPREAD = 2.2f;          // body sizes of the player the animals of a herd stand apart at most
    private const float SIZE_SPREAD = 0.2f;          // +-share of the prey size within a herd

    /// <summary>Species of the prey, creature templates of small animals.</summary>
    private static readonly string[] SPECIES = { "SNAIL", "ANT", "CRAB", "NEWT", "PENGUIN", "LOBSTER" };

    private float _herdCooldown;

    /// <summary>A herd of HerdSize animals of one species on grass in a ring just outside the view, none when the ring misses the land.</summary>
    private void SpawnHerd(float size)
    {
        float grass      = Balance.Current.Tribe.Pack.NestDepth * Coast.Unit;
        float viewRadius = _camera.ViewRadius();
        for (int attempt = 0; attempt < SPAWN_TRIES; attempt++)
        {
            Vector2 home = Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * viewRadius * Rand.Range(SPAWN_NEAR, SPAWN_FAR);
            if (Coast.LandDepth(home) < grass)
            {
                continue;
            }
            string         species  = SPECIES[Rand.Index(SPECIES.Length)];
            CreatureDesign bodyPlan = CreatureTemplates.Get(species).Build();
            Color          tint     = bodyPlan.Color ?? new Color(0.7f, 0.6f, 0.45f);
            for (int i = 0; i < Tune.HerdSize; i++)
            {
                Vector2 spot = home + Vector2.FromAngle(Rand.Angle()) * size * Rand.Range(0.4f, HERD_SPREAD);
                float   own  = size * Tune.PreySize * Rand.Range(1f - SIZE_SPREAD, 1f + SIZE_SPREAD);
                var animal = new Kin { Position = Coast.PushOutOfTrees(spot, own), Faction = PREY_FACTION };
                animal.Setup(bodyPlan, own, tint, home);
                animal.Toughen(Tune.PreyHealth, 0f);
                animal.JoinTribe(home);
                _prey.Add(animal);
                AddChild(animal);
            }
            Log.Verbose("Wildlife", $"A herd of {Tune.HerdSize} {species} grazes at {home}.");
            return;
        }
    }

    /// <summary>Prey far behind the player moves on: it is freed, a new herd appears ahead.</summary>
    private void MoveOn()
    {
        float gone = _camera.ViewRadius() * GONE_AT;
        foreach (Kin animal in _prey)
        {
            if (animal.GlobalPosition.DistanceTo(Player.GlobalPosition) > gone)
            {
                animal.QueueFree();
            }
        }
    }

    // ===========================================================================
    // =================================   LIVE   ================================
    // ===========================================================================
    //
    //   Update     Herds come and go, prey flees, the bites of the pack count.
    //   Flee       The direction away from the leader and the members close by, zero when calm.
    //   PreyNear   The closest prey animal around a point.
    //   Bite       The leader bites a prey animal.
    //

    private const float FLEE_STEP = 5f;              // body sizes of the player a fleeing animal runs ahead per order
    private const float CONTACT_SHARE = 1.15f;       // bodies this close (in sums of sizes) bite, like the fights of the RivalKeeper
    private const float HUNT_LEASH = 12f;            // body sizes from the leader a member gives up its prey

    /*
      One physics frame, from the land animal on:
      1. Herds    Up to Herds herds, a new one every HerdSeconds; prey far away moves on.
      2. Flee     Prey closer than FleeRange to the leader or a member runs away from them
                  at PreyPace, else it roams around the spot of its herd.
      3. Bites    A member that reached the prey it hunts bites it; a member far from the
                  leader gives its prey up.
    */
    /// <summary>One physics frame of the wildlife.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        _prey.RemoveAll(animal => !IsInstanceValid(animal) || animal.IsQueuedForDeletion());
        if (null == Coast || !Player.IsLandAnimal)
        {
            return;
        }
        float size = Player.BodySize();

        // Herds
        _herdCooldown -= dt;
        if (_herdCooldown <= 0f && _prey.Count <= (Tune.Herds - 1) * Tune.HerdSize)
        {
            _herdCooldown = Tune.HerdSeconds;
            SpawnHerd(size);
        }
        MoveOn();

        // Flee
        float pace = CellBody.SwimSpeed(size) * Balance.Current.Creature.Land.WalkSpeed * Tune.PreyPace;
        foreach (Kin animal in _prey)
        {
            Vector2 away = Flee(animal, size * Tune.FleeRange);
            animal.Pace  = pace;
            if (Vector2.Zero != away)
            {
                animal.OrderMove(animal.Position + away * size * FLEE_STEP);
            }
            animal.Steer(dt, Coast, _prey, leader: null, size, isPackFollowing: false);
        }

        // Bites
        foreach (Kin member in _pack.Kin)
        {
            if (!IsInstanceValid(member) || member.AttackTarget is not Kin { Faction: PREY_FACTION } prey)
            {
                continue;
            }
            if (member.GlobalPosition.DistanceTo(Player.GlobalPosition) > size * HUNT_LEASH)
            {
                member.OrderMove(member.Position);      // lets go of the prey, it follows the leader again
                continue;
            }
            if (member.Position.DistanceTo(prey.Position) <= (member.Radius + prey.Radius) * CONTACT_SHARE)
            {
                Bite(prey, Balance.Current.Tribe.Village.KinDamage * member.DamageScale * dt);
            }
        }
    }

    /// <summary>The direction away from the leader and the pack members within a range, weighted by how close they are; zero when nobody is.</summary>
    private Vector2 Flee(Kin animal, float range)
    {
        Vector2 away = Vector2.Zero;
        if (!Player.IsDead && Player.IsOnLand)
        {
            away += AwayFrom(animal, Player.GlobalPosition, range);
        }
        foreach (Kin member in _pack.Kin)
        {
            if (IsInstanceValid(member) && KinMode.Wild != member.Mode)
            {
                away += AwayFrom(animal, member.GlobalPosition, range);
            }
        }
        return away.LengthSquared() > 0.0001f ? away.Normalized() : Vector2.Zero;
    }

    private static Vector2 AwayFrom(Kin animal, Vector2 threat, float range)
    {
        Vector2 away     = animal.GlobalPosition - threat;
        float   distance = away.Length();
        return distance < range && distance > 0.001f ? away / distance * (1f - distance / range) : Vector2.Zero;
    }

    /// <summary>The closest prey animal within a range of a point, null when none.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="range">(Required) Search radius on the plane, measured to the skin of the animal.</param>
    /// <example><code>Kin prey = wildlife.PreyNear(player.GlobalPosition, player.BodySize() * 5f);</code></example>
    public Kin PreyNear(Vector2 point, float range)
    {
        Kin   nearest  = null;
        float distance = range;
        foreach (Kin animal in _prey)
        {
            if (!IsInstanceValid(animal) || animal.IsQueuedForDeletion())
            {
                continue;
            }
            float d = animal.GlobalPosition.DistanceTo(point) - animal.Radius;
            if (d < distance)
            {
                nearest  = animal;
                distance = d;
            }
        }
        return nearest;
    }

    /// <summary>A bite into a prey animal, a deadly one leaves its meat (PreyKilled).</summary>
    /// <param name="prey">(Required) A prey animal of this wildlife.</param>
    /// <param name="damage">(Required) Damage in hit points.</param>
    /// <returns>True when it died.</returns>
    public bool Bite(Kin prey, float damage)
    {
        if (!prey.Hurt(damage))
        {
            return false;
        }
        EmitSignal(SignalName.PreyKilled, prey.GlobalPosition, prey.AdultSize * Tune.PreyMeat);
        return true;
    }
}
