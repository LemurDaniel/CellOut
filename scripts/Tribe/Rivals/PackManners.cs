// NOTE:
//   How the foreign packs and the lone beast behave towards the player's pack, by their
//   mood. Everything is body language and deeds, no texts (rule of the project):
//     Any mood     an animal the leader comes close to stops and looks at it.
//     Hostile      the pack defends its den: it attacks whoever of the player's pack comes
//                  close and chases it up to LeashRange, then walks home. A courtship calms
//                  one animal for a moment (Kin.Calm), a won display sends all of them home.
//     Friendly     the animals bow when the leader arrives, and one of them carries the
//                  fruit of the pack to the leader in person (RivalKeeper.PackGave).
//     Respectful   they duck when the leader arrives, one carries the tribute to it.
//   Only packs that did not settle yet (the pack stage) and the lone beast, the villagers
//   of a tribe have their own life (RivalKeeper). Society owns it, Update runs after the
//   rivals and the lone beast.
//
//   var manners = new PackManners();
//   manners.Setup(session, pack, rivals, encounter);
//   AddChild(manners);
//   manners.Update(dt);

//
//   Chapters:
//     SETTINGS   The session, the pack, the rivals and the encounters.
//     MOODS      Attention, the defense of the den and the greeting, per physics frame.
//     CARRIERS   Gifts and tribute carried to the leader.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class PackManners : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Setup   The session, the pack, the rivals and the encounters, before AddChild.
    //

    private static WaysBalance Tune => Balance.Current.Tribe.Ways;

    private WorldSession _session;
    private PackKeeper _pack;
    private RivalKeeper _rivals;
    private PackEncounter _encounter;

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="pack">(Required) The player's pack: whom a hostile animal attacks.</param>
    /// <param name="rivals">(Required) The foreign packs and the lone beast, and their gifts (PackGave).</param>
    /// <param name="encounter">(Required) The encounters: they drop the food a carrier brings.</param>
    public void Setup(WorldSession session, PackKeeper pack, RivalKeeper rivals, PackEncounter encounter)
    {
        _session   = session;
        _pack      = pack;
        _rivals    = rivals;
        _encounter = encounter;
    }

    public override void _Ready()
    {
        _rivals.PackGave += OnPackGave;
    }

    // ===========================================================================
    // ================================   MOODS   ================================
    // ===========================================================================
    //
    //   Update      Per pack: defend the den or look at the leader, greet it, then the carriers.
    //   Defend      A hostile animal attacks the closest of the player's pack, or gives up the chase.
    //   VictimOf    The closest of the player's pack around an animal: the leader or a member.
    //   Greet       Friendly animals bow, respectful ones duck, once per visit.
    //

    private const float WATCH_SECONDS = 0.6f;        // an animal keeps looking this long after the leader left its sight
    private const float GREET_POSE_SECONDS = 1.3f;
    private const float BOW = -0.35f;                // the poses of PackEncounter: a bow, a duck
    private const float DUCK = -0.3f;

    private readonly Dictionary<RivalTribe, float> _greetTimer = new();

    /*
      One physics frame, for every pack that did not settle and is not gone, while the
      player leads a pack on land:
      > Hostile and not respectful: every animal that is not calmed defends (Defend).
      > Otherwise: an animal that still chases somebody (it was hunted) gives up beyond the
        leash; an idle one within EncounterRange of the leader stops and looks at it.
      > The first animal the leader comes close to greets it by the mood of its pack (Greet).
    */
    /// <summary>One physics frame of the manners.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        if (null == _session.Coast || !Player.IsPackLeader || Player.IsDead)
        {
            return;
        }
        float size  = Player.BodySize();
        float sight = size * Tune.EncounterRange;
        foreach (RivalTribe pack in _rivals.Everyone)
        {
            if (pack.IsSettled || pack.IsDefeated)
            {
                continue;
            }
            _greetTimer[pack] = _greetTimer.GetValueOrDefault(pack) - dt;
            bool isDefending = pack.IsHostile && !pack.IsRespectful && Player.IsOnLand;
            bool isLeaderFar = Player.GlobalPosition.DistanceTo(pack.Fire) > size * Tune.LeashRange;
            bool isVisited   = false;
            foreach (Kin animal in pack.Kin)
            {
                if (!IsInstanceValid(animal) || animal.IsQueuedForDeletion() || _carriers.ContainsKey(animal))
                {
                    continue;
                }
                float gap = animal.GlobalPosition.DistanceTo(Player.GlobalPosition) - animal.Radius;
                if (isDefending && !animal.IsCalm)
                {
                    Defend(animal, pack, size, sight);
                    continue;
                }
                if (null != animal.AttackTarget)
                {
                    // Hunted, it fights back: beyond the leash it lets go and walks home
                    if (isLeaderFar && gap > sight * 2f)
                    {
                        animal.OrderMove(pack.Fire);
                    }
                    continue;
                }
                if (gap < sight && animal.IsIdle)
                {
                    animal.Watch(Player.GlobalPosition, WATCH_SECONDS);
                    isVisited = true;
                }
            }
            if (isVisited && _greetTimer[pack] <= 0f)
            {
                Greet(pack, sight);
            }
        }
        UpdateCarriers(dt, size);
    }

    /*
      A hostile animal that is not calmed:
      > Without a victim: it attacks the closest of the player's pack within its sight, or
        the leader as soon as it steps into the territory around the den.
      > With a victim: it gives up once the victim is beyond the leash from the den and
        out of twice its sight, and walks home.
    */
    private void Defend(Kin animal, RivalTribe pack, float size, float sight)
    {
        if (null == animal.AttackTarget)
        {
            bool isIntruded = Player.GlobalPosition.DistanceTo(pack.Fire) < size * Tune.TerritoryRange;
            CellBody victim = VictimOf(animal, sight) ?? (isIntruded ? Player : null);
            if (null != victim)
            {
                animal.OrderAttack(victim);
                Log.Verbose("PackManners", $"A hostile {pack.Species} attacks.");
            }
            return;
        }
        Vector2 target   = animal.AttackTarget.GlobalPosition;
        bool    isBeyond = target.DistanceTo(pack.Fire) > size * Tune.LeashRange && target.DistanceTo(animal.GlobalPosition) > sight * 2f;
        if (isBeyond)
        {
            animal.OrderMove(pack.Fire);
        }
    }

    /// <summary>The closest of the player's pack within a range of an animal: the leader or one of its members, null when none.</summary>
    private CellBody VictimOf(Kin animal, float range)
    {
        CellBody victim   = null;
        float    distance = range;
        float    toLeader = animal.GlobalPosition.DistanceTo(Player.GlobalPosition);
        if (toLeader < distance)
        {
            victim   = Player;
            distance = toLeader;
        }
        foreach (Kin member in _pack.Kin)
        {
            if (!IsInstanceValid(member) || member.IsQueuedForDeletion() || KinMode.Wild == member.Mode)
            {
                continue;
            }
            float d = animal.GlobalPosition.DistanceTo(member.GlobalPosition);
            if (d < distance)
            {
                victim   = member;
                distance = d;
            }
        }
        return victim;
    }

    /// <summary>The animals in sight of the leader greet it: friendly ones bow, respectful ones duck; a neutral pack only looks.</summary>
    private void Greet(RivalTribe pack, float sight)
    {
        if (!pack.IsFriendly && !pack.IsRespectful)
        {
            return;
        }
        _greetTimer[pack] = Tune.GreetCooldown;
        foreach (Kin animal in pack.Kin)
        {
            if (IsInstanceValid(animal) && animal.GlobalPosition.DistanceTo(Player.GlobalPosition) - animal.Radius < sight)
            {
                animal.Strike(pack.IsRespectful ? DUCK : BOW, GREET_POSE_SECONDS);
            }
        }
    }

    // ===========================================================================
    // ==============================   CARRIERS   ===============================
    // ===========================================================================
    //
    //   OnPackGave       A pack gives: its animal closest to the leader sets out with the gift.
    //   UpdateCarriers   Every carrier walks to the leader, lays the food at its feet and walks home.
    //   HandOver         Fruit for a gift, fruit and meat for a tribute, with a bow or a duck.
    //

    private const float HAND_OVER_GAP = 1.6f;        // body sizes between leader and carrier at the handover
    private const float CARRY_SECONDS = 40f;         // a carrier that cannot reach the leader lays the food down where it stands
    private const float GIFT_SIZE = 0.25f;           // fruit of a gift against the player size
    private const float TRIBUTE_SIZE = 0.3f;         // meat of a tribute against the player size

    private readonly Dictionary<Kin, (RivalTribe Pack, bool IsTribute, float Left)> _carriers = new();

    /// <summary>A friendly pack gives fruit, a respectful one tribute: the idle animal closest to the leader carries it over.</summary>
    private void OnPackGave(RivalTribe pack, bool isTribute)
    {
        if (!Player.IsPackLeader || Player.IsDead || pack.IsDefeated)
        {
            return;
        }
        Kin   carrier  = null;
        float distance = float.PositiveInfinity;
        foreach (Kin animal in pack.Kin)
        {
            if (!IsInstanceValid(animal) || animal.IsQueuedForDeletion() || !animal.IsIdle || _carriers.ContainsKey(animal))
            {
                continue;
            }
            float d = animal.GlobalPosition.DistanceTo(Player.GlobalPosition);
            if (d < distance)
            {
                carrier  = animal;
                distance = d;
            }
        }
        if (null == carrier)
        {
            return;
        }
        _carriers[carrier] = (pack, isTribute, CARRY_SECONDS);
        Log.Verbose("PackManners", $"A {pack.Species} sets out with a {(isTribute ? "tribute" : "gift")}.");
    }

    private void UpdateCarriers(float dt, float size)
    {
        foreach (Kin carrier in new List<Kin>(_carriers.Keys))
        {
            if (!IsInstanceValid(carrier) || carrier.IsQueuedForDeletion())
            {
                _carriers.Remove(carrier);
                continue;
            }
            (RivalTribe pack, bool isTribute, float left) = _carriers[carrier];
            float gap = carrier.GlobalPosition.DistanceTo(Player.GlobalPosition) - carrier.Radius - size;
            if (gap <= size * HAND_OVER_GAP || left <= 0f || null != carrier.AttackTarget)
            {
                HandOver(carrier, pack, isTribute, size);
                _carriers.Remove(carrier);
                continue;
            }
            _carriers[carrier] = (pack, isTribute, left - dt);
            carrier.OrderMove(Player.GlobalPosition);
        }
    }

    /// <summary>The carrier lays the food between itself and the leader, shows its mood and walks home.</summary>
    private void HandOver(Kin carrier, RivalTribe pack, bool isTribute, float size)
    {
        Vector2 toLeader = (Player.GlobalPosition - carrier.GlobalPosition).Normalized();
        Vector2 spot     = carrier.GlobalPosition + toLeader * (carrier.Radius + size * 0.4f);
        _encounter.DropFood(spot, size * GIFT_SIZE, isMeat: false);
        if (isTribute)
        {
            _encounter.DropFood(spot + toLeader.Orthogonal() * size * 0.6f, size * TRIBUTE_SIZE, isMeat: true);
        }
        // It stops, shows its mood and then roams home on its own (its home is the den)
        carrier.OrderTarget = null;
        carrier.Facing      = toLeader.Angle();
        carrier.Strike(isTribute ? DUCK : BOW, GREET_POSE_SECONDS);
        carrier.Watch(Player.GlobalPosition, GREET_POSE_SECONDS);
        Log.Verbose("PackManners", $"A {pack.Species} hands over its {(isTribute ? "tribute" : "gift")}.");
    }
}
