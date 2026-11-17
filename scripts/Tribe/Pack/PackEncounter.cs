// NOTE:
//   The encounters of the pack, and later of the chieftain and its retinue, with the
//   foreign packs and tribes (wish of the user 2026-11-17): three ways to meet a stranger,
//   mirroring the diet, keys 1 to 3 (or the way slots of the HUD):
//     1 Court (herbivore)  bow to it, its pack likes the player more; friendly packs bring
//                          fruit, a trusting animal joins the pack (a mixed pack).
//     2 Display (omnivore) rear up with the whole pack nearby: more body than theirs wins
//                          respect, the losers duck and walk back to their den; a respectful
//                          pack pays tribute in fruit and meat. Nobody dies.
//     3 Hunt (carnivore)   lunge and bite, the pack joins the fight; a kill leaves meat.
//   Every way works, the one that fits the diet works better (TribeWays.Strength), and its
//   food pushes the diet further: fruit from courting, meat from hunting, both from tribute.
//   Feedback comes from the bodies (bowing, rearing up, ducking, retreating), no rings.
//   Society owns it, Update runs after the pack, HandleInput before it.
//
//   var encounter = new PackEncounter();
//   encounter.Setup(session, pack, rivals);
//   encounter.BannerRequested += Banner;
//   AddChild(encounter);
//   encounter.Update(dt);

//
//   Chapters:
//     SETTINGS    The session, pack, rivals, banners and the target.
//     ENCOUNTER   Keys, cooldowns, the lunge and the gifts of the packs.
//     WAYS        Court, display and hunt.
//     FOOD        Fruit and meat on the ground.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class PackEncounter : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   BannerRequested   A banner is due: title key, text key and its arguments.
    //   Setup             The session, the pack and the rivals, before AddChild.
    //   IsActive          The player leads a pack or the tribe and lives.
    //   Target            The closest foreign animal or villager within EncounterRange, null when none.
    //   TargetPack        Its pack.
    //   NameOf            The species of a foreign pack as a translation key.
    //

    public event System.Action<string, string, object[]> BannerRequested;

    private static WaysBalance Tune => Balance.Current.Tribe.Ways;

    private WorldSession _session;
    private PackKeeper _pack;
    private RivalKeeper _rivals;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, food layer and coast.</param>
    /// <param name="pack">(Required) The player's pack: members, nest, adoption.</param>
    /// <param name="rivals">(Required) The foreign packs: relation, respect, kills.</param>
    public void Setup(WorldSession session, PackKeeper pack, RivalKeeper rivals)
    {
        _session = session;
        _pack    = pack;
        _rivals  = rivals;
    }

    public override void _Ready()
    {
        _rivals.PackGave += OnPackGave;
    }

    /// <summary>The player leads a pack or is the chieftain of a tribe, on land, alive.</summary>
    public bool IsActive => null != Coast && Player.IsPackLeader && !Player.IsDead;

    public Kin Target { get; private set; }

    public RivalTribe TargetPack { get; private set; }

    /// <summary>The species of a foreign pack as a translation key (its creature template).</summary>
    public static string NameOf(RivalTribe pack)
    {
        return $"CREATURE_TEMPLATE_{pack.Species}";
    }

    // ===========================================================================
    // ==============================   ENCOUNTER   ==============================
    // ===========================================================================
    //
    //   HandleInput   Keys 1, 2 and 3: court, display, hunt the target, or the KeyRouter (the fans of the HUD) takes them.
    //   Use           One way on the target, false while it recharges or out of reach.
    //   CooldownOf    Seconds left until a way works again.
    //   CooldownShare 1 right after use .. 0 ready, for the HUD.
    //   Update        Cooldowns and the target (the lunge carries the player in CreatureAbilityResolver).
    //

    private static readonly Key[] WAY_KEYS = { Key.Key1, Key.Key2, Key.Key3 };

    private readonly Dictionary<PackWay, float> _cooldowns = new();

    /// <summary>Keys 1, 2 and 3 court, display and hunt while the player leads a pack.</summary>
    /// <returns>True when a way key was pressed (also when it did not work right now).</returns>
    public bool HandleInput(InputEvent @event)
    {
        if (!IsActive || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return false;
        }
        int index = System.Array.IndexOf(WAY_KEYS, key.PhysicalKeycode);
        if (index < 0)
        {
            return false;
        }
        // The HUD opens the fan of the way first (AbilitySlots), else the way acts at once
        if (KeyRouter?.Invoke((PackWay)index) != true)
        {
            Use((PackWay)index);
        }
        return true;
    }

    /// <summary>(Optional) Takes the keys 1, 2 and 3 first, e.g. the HUD opening the fan of the way; true when it did. Defaults to none.</summary>
    public System.Func<PackWay, bool> KeyRouter { get; set; }

    /// <summary>One way on the current target.</summary>
    /// <param name="way">(Required) Court, display or hunt.</param>
    /// <returns>True when it happened, false without target, out of reach or while it recharges.</returns>
    /// <example><code>encounter.Use(PackWay.Display);   // what key 2 does</code></example>
    public bool Use(PackWay way)
    {
        if (!IsActive || CooldownOf(way) > 0f)
        {
            return false;
        }
        bool isDone = way switch
        {
            _ when null == Target => PackWay.Hunt == way && Raze(),
            PackWay.Court         => Court(Target, TargetPack),
            PackWay.Display       => Display(Target, TargetPack),
            _                     => Hunt(Target, TargetPack) || Raze(),
        };
        if (isDone)
        {
            _cooldowns[way] = way switch
            {
                PackWay.Court   => Tune.CourtCooldown,
                PackWay.Display => Tune.DisplayCooldown,
                _               => Tune.HuntCooldown,
            };
            Log.Verbose("PackEncounter", $"{way} on the {TargetPack?.Species ?? "village"}: relation {TargetPack?.Relation ?? 0f:F2}, respect {TargetPack?.Respect ?? 0f:F2}.");
        }
        return isDone;
    }

    /// <summary>1 right after a way was used .. 0 ready, for the slots of the HUD.</summary>
    public float CooldownShare(PackWay way)
    {
        float total = way switch
        {
            PackWay.Court   => Tune.CourtCooldown,
            PackWay.Display => Tune.DisplayCooldown,
            _               => Tune.HuntCooldown,
        };
        return total > 0f ? Mathf.Clamp(CooldownOf(way) / total, 0f, 1f) : 0f;
    }

    /// <summary>Seconds left until a way works again, 0 when ready.</summary>
    public float CooldownOf(PackWay way)
    {
        return _cooldowns.GetValueOrDefault(way);
    }


    /// <summary>One physics frame: cooldowns run down, the closest stranger becomes the target.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        foreach (PackWay way in new List<PackWay>(_cooldowns.Keys))
        {
            _cooldowns[way] = Mathf.Max(0f, _cooldowns[way] - dt);
        }
        if (!IsActive)
        {
            Target     = null;
            TargetPack = null;
            return;
        }
        (Target, TargetPack) = Closest(Player.BodySize() * Tune.EncounterRange);
    }

    /// <summary>The closest animal of a foreign pack (not yet a tribe) within a range.</summary>
    private (Kin, RivalTribe) Closest(float range)
    {
        Kin        closest  = null;
        RivalTribe pack     = null;
        float      distance = range;
        foreach (RivalTribe rival in _rivals.Rivals)
        {
            if (rival.IsDefeated)
            {
                continue;
            }
            foreach (Kin animal in rival.Kin)
            {
                if (!IsInstanceValid(animal) || animal.IsQueuedForDeletion())
                {
                    continue;
                }
                float d = animal.GlobalPosition.DistanceTo(Player.GlobalPosition) - animal.Radius;
                if (d < distance)
                {
                    closest  = animal;
                    pack     = rival;
                    distance = d;
                }
            }
        }
        return (closest, pack);
    }

    // ===========================================================================
    // ================================   WAYS   =================================
    // ===========================================================================
    //
    //   Court     Bow at close range: relation up, a hostile pack snaps back, a trusting animal joins.
    //   Display   The pack rears up: more body than theirs wins respect, the losers duck and retreat.
    //   Hunt      Lunge and bite, the pack joins in, the strangers fight back, a kill leaves meat.
    //   Raze      In the tribe without prey in reach: the members nearby tear down the closest foreign hut.
    //   Gap       Distance between the membranes of the player and an animal.
    //   Ours      The grown members of the player's pack within a range of a point.
    //

    private const float BOW = -0.35f;              // radians of a bow, the body language of a courtship
    private const float REAR_UP = 0.55f;           // radians of rearing up in a display
    private const float DUCK = -0.3f;              // the losers of a display duck
    private const float COURT_POSE_SECONDS = 1.4f;
    private const float ADOPT_COST = 0.3f;         // relation the pack loses when one of them leaves for the player

    /// <summary>Bow at close range: the stranger bows back and its pack likes the player more, a hostile one snaps.</summary>
    private bool Court(Kin animal, RivalTribe pack)
    {
        if (Gap(animal) > Player.BodySize() * Tune.CourtRange)
        {
            return false;
        }
        Player.Strike(BOW, COURT_POSE_SECONDS);
        animal.Facing = (Player.GlobalPosition - animal.GlobalPosition).Angle();

        // A hostile pack does not trust a bow yet: it snaps, the courtship counts half
        float strength = TribeWays.Strength(PackWay.Court, Player.Diet);
        if (pack.IsHostile)
        {
            Player.TakeDamage(Tune.CourtSnap, animal.GlobalPosition);
            pack.Shift(Tune.CourtRelation * strength * 0.5f);
            return true;
        }
        animal.Strike(BOW, COURT_POSE_SECONDS);
        pack.Shift(Tune.CourtRelation * strength);

        // A trusting animal leaves its pack and joins the player's: a mixed pack
        if (pack.Relation >= Tune.AdoptAt && pack.Release(animal))
        {
            _pack.Adopt(animal);
            pack.Shift(-ADOPT_COST);
            BannerRequested?.Invoke("BANNER_ADOPT_TITLE", "BANNER_ADOPT_TEXT", new object[] { Tr(NameOf(pack)) });
        }
        return true;
    }

    /*
      The display contest:
      - Ours:   the player and its grown members within DisplayRange of the player, their
                sizes added, times the strength of the way for the diet.
      - Theirs: the animals of the pack within DisplayRange of the target, sizes added.
      > Ours bigger:  respect + DisplayWin, the player's side rears up, theirs ducks and
                      walks back to its den. Reaching RespectAt shows a banner.
      > Otherwise:    respect - DisplayLose, theirs rears up, the player's side ducks.
    */
    private bool Display(Kin animal, RivalTribe pack)
    {
        float range  = Player.BodySize() * Tune.DisplayRange;
        var   ours   = Ours(Player.GlobalPosition, range);
        var   theirs = new List<Kin>();
        foreach (Kin other in pack.Kin)
        {
            if (!GodotObject.IsInstanceValid(other))
            {
                continue;       // freed since the last physics frame, read here outside of it
            }
            if (other.GlobalPosition.DistanceTo(animal.GlobalPosition) <= range)
            {
                theirs.Add(other);
            }
        }
        float ourBody   = Player.BodySize();
        float theirBody = 0f;
        ours.ForEach(member => ourBody += member.Radius);
        theirs.ForEach(other => theirBody += other.Radius);
        bool isWon = TribeWays.DisplayWins(ourBody * TribeWays.Strength(PackWay.Display, Player.Diet), theirBody);

        float winner = REAR_UP;
        float loser  = DUCK;
        Player.Strike(isWon ? winner : loser, Tune.DisplaySeconds);
        ours.ForEach(member => member.Strike(isWon ? winner : loser, Tune.DisplaySeconds));
        foreach (Kin other in theirs)
        {
            other.Strike(isWon ? loser : winner, Tune.DisplaySeconds);
            if (isWon)
            {
                other.OrderMove(pack.Fire);
            }
        }

        bool wasRespectful = pack.IsRespectful;
        pack.ShiftRespect(isWon ? Tune.DisplayWin : -Tune.DisplayLose);
        if (!wasRespectful && pack.IsRespectful)
        {
            BannerRequested?.Invoke("BANNER_RESPECT_TITLE", "BANNER_RESPECT_TEXT", new object[] { Tr(NameOf(pack)) });
        }
        return true;
    }

    /// <summary>Lunge and bite: the pack joins the fight, the strangers nearby fight back, a kill leaves meat (RivalKeeper.KinKilled).</summary>
    private bool Hunt(Kin animal, RivalTribe pack)
    {
        float size = Player.BodySize();
        if (Gap(animal) > size * Tune.HuntRange)
        {
            return false;
        }
        Vector2 toPrey = animal.GlobalPosition - Player.GlobalPosition;
        Player.Push  += toPrey.Normalized() * Player.CurrentSpeed() * Tune.HuntLunge;
        Player.Facing = toPrey.Angle();
        pack.Shift(-Tune.HuntRelation);

        // The members follow the leader into the fight, the strangers nearby fight back
        foreach (Kin member in Ours(Player.GlobalPosition, size * Tune.DisplayRange))
        {
            member.OrderAttack(animal);
        }
        foreach (Kin other in pack.Kin)
        {
            if (!GodotObject.IsInstanceValid(other))
            {
                continue;       // freed since the last physics frame, read here outside of it
            }
            if (other.GlobalPosition.DistanceTo(animal.GlobalPosition) <= size * Tune.DisplayRange)
            {
                other.OrderAttack(Player);
            }
        }
        if (animal.Hurt(Tune.HuntDamage * TribeWays.Strength(PackWay.Hunt, Player.Diet)))
        {
            _rivals.ReportKill(animal);
        }
        return true;
    }

    private const float RAZE_RANGE = 4f;           // body sizes from the chieftain to the wall of a foreign hut

    /// <summary>Hunt without prey in reach, in the tribe: the members nearby tear down the closest foreign hut (RivalKeeper counts the blows).</summary>
    private bool Raze()
    {
        float size    = Player.BodySize();
        Hut   closest = null;
        float reach   = size * RAZE_RANGE;
        foreach (RivalTribe rival in _rivals.Rivals)
        {
            if (!rival.IsSettled || rival.IsDefeated)
            {
                continue;
            }
            foreach (Hut hut in rival.Huts)
            {
                float d = hut.Position.DistanceTo(Player.GlobalPosition) - hut.BlockRadius;
                if (!hut.IsQueuedForDeletion() && d < reach)
                {
                    closest = hut;
                    reach   = d;
                }
            }
        }
        List<Kin> members = Ours(Player.GlobalPosition, size * Tune.DisplayRange);
        if (null == closest || 0 == members.Count)
        {
            return false;
        }
        members.ForEach(member => member.OrderRaze(closest));
        Player.Strike(REAR_UP, Tune.DisplaySeconds);
        return true;
    }

    private float Gap(Kin animal)
    {
        return animal.GlobalPosition.DistanceTo(Player.GlobalPosition) - animal.Radius - Player.BodySize();
    }

    /// <summary>The grown members of the player's pack within a range of a point.</summary>
    private List<Kin> Ours(Vector2 point, float range)
    {
        var members = new List<Kin>();
        foreach (Kin kin in _pack.Kin)
        {
            if (!GodotObject.IsInstanceValid(kin))
            {
                continue;       // freed since the last physics frame, read here outside of it
            }
            if (KinMode.Wild != kin.Mode && !kin.IsPup && !kin.IsQueuedForDeletion() && kin.GlobalPosition.DistanceTo(point) <= range)
            {
                members.Add(kin);
            }
        }
        return members;
    }

    // ===========================================================================
    // ================================   FOOD   =================================
    // ===========================================================================
    //
    //   OnPackGave   A friendly pack brings fruit to the nest, a respectful one fruit and meat (tribute).
    //   DropMeat     The meat of an animal killed in the pack stage, where it fell.
    //   DropFood     One piece of land food on the ground.
    //

    private const float LAND_FOOD_LOOK = 2.6f;      // like the land food of the Spawner
    private const float MEAT_SHARE = 0.3f;          // size of a meat chunk against the animal

    private void OnPackGave(RivalTribe pack, bool isTribute)
    {
        if (null == _pack.Nest || !IsActive)
        {
            return;
        }
        float   size = Player.BodySize();
        Vector2 spot = _pack.Nest.Position + Vector2.FromAngle(Rand.Angle()) * size * 2f;
        DropFood(spot, size * 0.25f, isMeat: false);
        if (isTribute)
        {
            DropFood(spot + Vector2.FromAngle(Rand.Angle()) * size, size * 0.3f, isMeat: true);
        }
    }

    /// <summary>The meat of an animal killed in the pack stage, where it fell.</summary>
    /// <param name="position">(Required) Where it died.</param>
    /// <param name="size">(Required) Its body size.</param>
    public void DropMeat(Vector2 position, float size)
    {
        if (!IsActive)
        {
            return;
        }
        DropFood(position, size * MEAT_SHARE, isMeat: true);
    }

    private void DropFood(Vector2 spot, float size, bool isMeat)
    {
        if (!Coast.IsOnLand(spot))
        {
            return;
        }
        var food = new Food
        {
            Habitat      = FoodHabitat.Land,
            IsMeat       = isMeat,
            GroundHeight = Coast.GroundHeight(spot) * Player.BodySize(),
            LookScale    = LAND_FOOD_LOOK,
            Position     = spot,
        };
        food.Setup(size, isMeat ? FoodKind.Amino : FoodKind.Sugar);
        food.Value *= Balance.Current.Diet.PlantFoodValue;
        _session.FoodLayer.AddChild(food);
    }
}
