// NOTE:
//   Keeps the neighbor tribes of the player's tribe: settles them when the tribe is
//   founded (one on the same island, the others on other islands), lets friendly ones
//   send traders (food and wood for the stock, better with a market, relation up, more
//   with a totem) and hostile ones send raids (raiders hurt the huts, less with palisade
//   and watchtower, villagers fight back), resolves every fight between factions and the
//   gifts of the player. It does not process on its own: Main calls Update after the
//   tribe. The rival tribes are its children.
//
//   var rivals = new RivalKeeper();
//   rivals.Setup(session, pack, tribe);
//   rivals.RaidStarted  += OnRaidStarted;
//   rivals.TradeArrived += OnTradeArrived;
//   AddChild(rivals);
//   rivals.Update(dt);                       // in _PhysicsProcess, after the tribe

//
//   Chapters:
//     SETTINGS   The session, the pack, the tribe and the signals.
//     SETTLE     Where the rival tribes live and where they enter the player's island.
//     RELATE     Traders, raids, gifts and the relation drift, per physics frame.
//     FIGHT      Fights between factions and raiders at the huts.
//     SAVE       The neighbors as save data and back.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class RivalKeeper : Node2D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Changed        A relation or the stock changed, the panel refreshes.
    //   RaidStarted    Raiders of a tribe set out, Main shows the banner.
    //   TradeArrived   A trader delivered food and wood, Main shows the banner.
    //   TribeDefeated  A tribe lost its last building, its stock is looted, Main shows the banner.
    //   TributePaid    A tribe that respects the player handed over food and wood.
    //   PackGave       A foreign pack brings fruit (friendly) or fruit and meat (tribute) to the nest.
    //   KinKilled      An enemy died in a fight: meat in the pack stage, food for a warlike tribe.
    //   Rivals         The neighbor tribes, empty before the player's tribe.
    //   Setup          The session, the pack and the tribe, before AddChild.
    //

    [Signal] public delegate void ChangedEventHandler();
    [Signal] public delegate void RaidStartedEventHandler(string nameKey);
    [Signal] public delegate void TradeArrivedEventHandler(string nameKey, int food, int wood);
    [Signal] public delegate void TribeDefeatedEventHandler(string nameKey, int food, int wood);
    [Signal] public delegate void TributePaidEventHandler(string nameKey, int food, int wood);
    [Signal] public delegate void PackGaveEventHandler(RivalTribe rival, bool isTribute);
    [Signal] public delegate void KinKilledEventHandler(Vector2 position, float size);

    /// <summary>Every tunable number of the rivals from data/balance.json (tribe.rivals).</summary>
    private static RivalsBalance Tune => Balance.Current.Tribe.Rivals;

    /// <summary>Kin damage, engage range and gifts from data/balance.json (tribe.village).</summary>
    private static VillageBalance Village => Balance.Current.Tribe.Village;

    private WorldSession _session;
    private PackKeeper _pack;
    private TribeCommander _tribe;
    private readonly List<RivalTribe> _rivals = new();

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;

    public IReadOnlyList<RivalTribe> Rivals => _rivals;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="pack">(Required) The pack: the player's villagers and the fire.</param>
    /// <param name="tribe">(Required) The tribe: stock, huts, defense.</param>
    public void Setup(WorldSession session, PackKeeper pack, TribeCommander tribe)
    {
        _session = session;
        _pack    = pack;
        _tribe   = tribe;
    }

    // ===========================================================================
    // ===============================   SETTLE   ================================
    // ===========================================================================
    //
    //   SettlePacks  The foreign packs around the nest, each of another species (pack stage).
    //   Settle       The packs become tribes, further tribes on the next islands up to Count.
    //   AddRival     One rival of a species, a pack at its den or a tribe at its fire.
    //   HomeOf       A home spot: on the own island away from the other homes, or the center of another island.
    //   EntryFor     Where a tribe enters the player's island: its fire on the same island, else the shore towards it.
    //   TintFor      A clearly different hue per tribe.
    //

    private const float HUE_STEP = 0.21f;            // around the hue wheel per rival
    private const int HOME_TRIES = 24;
    private const float SHORE_STEP = 2f;             // Units per step when walking out to the shore
    private const float SHORE_MARGIN = 4f;           // Units back from the water for an entry point

    /// <summary>Species of the foreign packs and tribes, creature templates that walk on land.</summary>
    private static readonly string[] SPECIES = { "TRICERATOPS", "ELEPHANT", "GIRAFFE", "CROCODILE", "CHAMELEON", "FROG", "TREX", "PARROT" };

    private const float PACK_DISTANCE = 0.55f;       // share of SameIslandDistance between the nest and a foreign den

    /*
      The foreign packs of the pack stage, once when the nest is built:
      - PacksOnIsland dens on grass around the nest, every pack of another species.
      - Neutral at first (relation 0, no respect), they roam around their den.
      Founding the tribe turns them into the neighbor tribes (Settle), relation and respect stay.
    */
    /// <summary>The foreign packs around the nest, once the nest stands.</summary>
    /// <example><code>rivals.SettlePacks();   // Society, when the nest is built</code></example>
    public void SettlePacks()
    {
        if (_rivals.Count > 0 || null == Coast || null == _pack.Nest)
        {
            return;
        }
        float   size = Player.BodySize();
        Vector2 nest = _pack.Nest.Position;
        var     kinds = new List<string>(SPECIES);
        for (int i = 0; i < Balance.Current.Tribe.Ways.PacksOnIsland; i++)
        {
            Vector2? den = HomeOf(0, nest, PACK_DISTANCE);
            if (den is not Vector2 spot)
            {
                continue;
            }
            string species = kinds[Rand.Index(kinds.Count)];
            kinds.Remove(species);
            AddRival(spot, nest, species, relation: 0f, size * Rand.Range(1f - Balance.Current.Tribe.Ways.PackSizeSpread, 1f + Balance.Current.Tribe.Ways.PackSizeSpread), isSettled: false);
        }
        Log.Verbose("RivalKeeper", $"{_rivals.Count} foreign pack(s) live around the nest.");
        EmitSignal(SignalName.Changed);
    }

    /*
      The neighbor tribes, once when the player's tribe is founded:
      - The foreign packs that are left light a fire at their den and build huts.
      - Further tribes settle on the next islands up to Count.
      - The path of the player shifts every start relation: StartShift up for the
        pacifist, down for the warlike.
    */
    /// <summary>Turns the foreign packs into tribes and settles the rest, once, when the player's tribe exists.</summary>
    /// <param name="path">(Required) The path of the player's tribe.</param>
    /// <example><code>rivals.Settle(TribePath.Pacifist);   // when the stage turns to the tribe</code></example>
    public void Settle(TribePath path)
    {
        if (null == Coast || null == _pack.Nest || _rivals.Exists(rival => rival.IsSettled))
        {
            return;
        }
        float shift = path switch
        {
            TribePath.Pacifist => Balance.Current.Tribe.Ways.StartShift,
            TribePath.Warlike  => -Balance.Current.Tribe.Ways.StartShift,
            _                  => 0f,
        };
        foreach (RivalTribe pack in _rivals)
        {
            pack.Settle();
            pack.Shift(shift);
        }

        // Further tribes on the next islands, the first is friendly, the second hostile, the rest in between
        float   size = Player.BodySize();
        Vector2 fire = _pack.Nest.Position;
        var     kinds = new List<string>(SPECIES);
        kinds.RemoveAll(kind => _rivals.Exists(rival => rival.Species == kind));
        for (int island = 1; _rivals.Count < Tune.Count && island < Coast.Islands.Count; island++)
        {
            Vector2? home = HomeOf(island, fire);
            if (home is not Vector2 spot)
            {
                continue;
            }
            float  relation = _rivals.Count switch { 0 => 0.5f, 1 => -0.5f, _ => Rand.Range(-0.2f, 0.2f) };
            string species  = kinds.Count > 0 ? kinds[Rand.Index(kinds.Count)] : SPECIES[Rand.Index(SPECIES.Length)];
            kinds.Remove(species);
            AddRival(spot, fire, species, relation + shift, size, isSettled: true);
        }
        Log.Verbose("RivalKeeper", $"{_rivals.Count} rival tribe(s) settled on the path {path}.");
        EmitSignal(SignalName.Changed);
    }

    /// <summary>One rival of a species at a home spot, a foreign pack at its den or a tribe at its fire.</summary>
    private void AddRival(Vector2 home, Vector2 ownHome, string species, float relation, float size, bool isSettled)
    {
        int            index    = _rivals.Count;
        CreatureDesign bodyPlan = CreatureTemplates.Get(species).Build();
        var rival = new RivalTribe { Species = species };
        rival.AttachCoast(Coast);
        rival.Setup(index + 1, $"RIVAL_NAME_{index + 1}", home, EntryFor(home, ownHome), bodyPlan, bodyPlan.Color ?? TintFor(index), relation, size, isSettled);
        _rivals.Add(rival);
        AddChild(rival);
    }

    /// <summary>A home spot: on the own island (island 0, at a share of SameIslandDistance, away from the other homes) or the center of another island, grass only.</summary>
    private Vector2? HomeOf(int island, Vector2 fire, float share = 1f)
    {
        float grass = Balance.Current.Tribe.Pack.NestDepth * Coast.Unit;
        if (island > 0 && island < Coast.Islands.Count)
        {
            Vector2 center = Coast.Islands[island].Center;
            return Coast.LandDepth(center) >= grass ? center : null;
        }
        for (int attempt = 0; attempt < HOME_TRIES; attempt++)
        {
            Vector2 spot     = fire + Vector2.FromAngle(Rand.Angle()) * Tune.SameIslandDistance * share * Coast.Unit * Rand.Range(0.8f, 1.1f);
            bool    isCrowded = _rivals.Exists(rival => rival.Fire.DistanceTo(spot) < Tune.SameIslandDistance * share * Coast.Unit * 0.6f);
            if (Coast.LandDepth(spot) >= grass && !isCrowded)
            {
                return spot;
            }
        }
        return null;
    }

    /// <summary>The entry point: the rival fire itself on the same island, else the shore of the player's island towards the rival.</summary>
    private Vector2 EntryFor(Vector2 rivalFire, Vector2 ownFire)
    {
        // Same island: walking from fire to fire never crosses water
        Vector2 direction = (rivalFire - ownFire).Normalized();
        Vector2 point     = ownFire;
        float   step      = SHORE_STEP * Coast.Unit;
        for (float walked = 0f; walked < rivalFire.DistanceTo(ownFire); walked += step)
        {
            Vector2 next = point + direction * step;
            if (!Coast.IsOnLand(next))
            {
                return point - direction * Mathf.Min(SHORE_MARGIN * Coast.Unit, walked);
            }
            point = next;
        }
        return rivalFire;
    }

    private Color TintFor(int index)
    {
        Color tint = Player.BodyColor;
        tint.H = Mathf.Wrap(tint.H + HUE_STEP * (index + 1), 0f, 1f);
        return tint;
    }

    // ===========================================================================
    // ===============================   RELATE   ================================
    // ===========================================================================
    //
    //   Update        Rivals update, gifts, tribute, traders and raids set out and arrive, fights resolve, relations and respect drift.
    //   PayTribute    A respectful pack brings fruit and meat, a respectful tribe food and wood.
    //   SendTrade     A friendly tribe sends a trader every TradeInterval.
    //   SendRaid      A hostile tribe sends raiders every RaidInterval towards the nearest hut.
    //   TraderReach   How close to the fire (or the walls of its headquarters) a trader hands over.
    //   Deliver       A trader at the fire hands over food and wood and walks home.
    //   Gift          The player gives food to a tribe, its relation rises.
    //   ForceRaid     Cheat and test: a raid of a tribe right now.
    //

    private readonly Dictionary<RivalTribe, float> _tradeTimer = new();
    private readonly Dictionary<RivalTribe, float> _raidTimer = new();
    private readonly Dictionary<Kin, float> _raidLeft = new();     // seconds a raider keeps raiding

    /// <summary>One physics frame of the rivals, nothing before they settled.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        if (0 == _rivals.Count || null == _pack.Nest)
        {
            return;
        }
        float size = Player.BodySize();
        foreach (RivalTribe rival in _rivals)
        {
            rival.Update(dt, size);

            // A foreign pack whose last animal died (hunted, adopted) is gone for good
            if (!rival.IsSettled && 0 == rival.Kin.Count && !rival.IsDefeated)
            {
                rival.Defeat();
                EmitSignal(SignalName.Changed);
            }
            if (rival.IsDefeated)
            {
                continue;
            }

            // Relations drift towards neutral and respect fades, so nothing lasts without deeds
            float drift = Mathf.Min(Mathf.Abs(rival.Relation), Tune.RelationDrift * dt);
            rival.Shift(rival.Relation > 0f ? -drift : drift);
            rival.ShiftRespect(-Balance.Current.Tribe.Ways.RespectDrift * dt);

            // Friendly packs bring fruit, respectful ones (packs and tribes) pay tribute
            _giftTimer[rival]    = _giftTimer.GetValueOrDefault(rival, Balance.Current.Tribe.Ways.GiftInterval) - dt;
            _tributeTimer[rival] = _tributeTimer.GetValueOrDefault(rival, Balance.Current.Tribe.Ways.TributeInterval) - dt;
            if (!rival.IsSettled && rival.IsFriendly && _giftTimer[rival] <= 0f)
            {
                _giftTimer[rival] = Balance.Current.Tribe.Ways.GiftInterval;
                EmitSignal(SignalName.PackGave, rival, false);
            }
            if (rival.IsRespectful && _tributeTimer[rival] <= 0f)
            {
                _tributeTimer[rival] = Balance.Current.Tribe.Ways.TributeInterval;
                PayTribute(rival);
            }
            if (!rival.IsSettled)
            {
                continue;     // a pack neither trades nor raids, it only roams around its den
            }

            _tradeTimer[rival] = _tradeTimer.GetValueOrDefault(rival, Tune.TradeInterval * 0.5f) - dt;
            _raidTimer[rival]  = _raidTimer.GetValueOrDefault(rival, Tune.RaidInterval * 0.5f) - dt;
            if (rival.IsFriendly && _tradeTimer[rival] <= 0f)
            {
                _tradeTimer[rival] = Tune.TradeInterval;
                rival.SendTrader(_pack.Nest.Position);
            }
            // A tribe that respects the player never raids it
            if (rival.IsHostile && !rival.IsRespectful && _raidTimer[rival] <= 0f)
            {
                _raidTimer[rival] = Tune.RaidInterval;
                SendRaid(rival);
            }

            // Traders at the fire deliver, raiders raid for RaidSeconds then leave
            foreach (Kin kin in rival.Kin)
            {
                if (KinRole.Trader == kin.Role && !kin.IsLeaving && kin.Position.DistanceTo(_pack.Nest.Position) <= TraderReach(size, kin))
                {
                    Deliver(rival, kin);
                }
                if (KinRole.Raider == kin.Role && !kin.IsLeaving)
                {
                    _raidLeft[kin] = _raidLeft.GetValueOrDefault(kin, Tune.RaidSeconds) - dt;
                    if (_raidLeft[kin] <= 0f)
                    {
                        kin.GoHome();
                        _raidLeft.Remove(kin);
                    }
                }
            }
        }
        ResolveFights(dt, size);
        ResolveRazing(dt, size);
    }

    /// <summary>Raiders set out towards the nearest hut of the player, or its fire.</summary>
    private void SendRaid(RivalTribe rival)
    {
        Vector2 target   = _pack.Nest.Position;
        float   distance = float.PositiveInfinity;
        foreach (Hut hut in _tribe.Huts)
        {
            float d = hut.Position.DistanceTo(rival.Entry);
            if (d < distance)
            {
                target   = hut.Position;
                distance = d;
            }
        }
        rival.SendRaiders(target, Tune.Raiders);
        EmitSignal(SignalName.RaidStarted, rival.NameKey);
    }

    /// <summary>How close to the fire a trader hands over: the nest range, or the walls of a headquarters that stands on the fire.</summary>
    private float TraderReach(float size, Kin trader)
    {
        float nest = size * Balance.Current.Tribe.Pack.NestRange;
        return null == _tribe.Hall ? nest : Mathf.Max(nest, _tribe.Hall.ContactRadius + trader.Radius);
    }

    /// <summary>The trader hands over food and wood (more with a market), the relation rises (more with a totem), then it walks home.</summary>
    private void Deliver(RivalTribe rival, Kin trader)
    {
        int food = Mathf.RoundToInt(Tune.TradeFood * (1f + _tribe.TradeBonus) * _tribe.Bonuses.Trade);
        int wood = Mathf.RoundToInt(Tune.TradeWood * (1f + _tribe.TradeBonus) * _tribe.Bonuses.Trade);
        _tribe.Economy.AddFood(food);
        _tribe.Economy.AddWood(wood);
        rival.Shift(Tune.RelationPerTrade + _tribe.RelationBonus);
        trader.GoHome();
        Log.Verbose("RivalKeeper", $"Trader of faction {rival.Faction} delivered {food} food and {wood} wood, relation {rival.Relation:F2}.");
        EmitSignal(SignalName.TradeArrived, rival.NameKey, food, wood);
        EmitSignal(SignalName.Changed);
    }

    private readonly Dictionary<RivalTribe, float> _giftTimer = new();
    private readonly Dictionary<RivalTribe, float> _tributeTimer = new();

    /*
      Tribute of a rival that respects the player:
      - A foreign pack drops fruit and meat at the nest (PackGave, the encounter spawns it).
      - A tribe hands over food and wood like a trade, but its relation does not rise.
    */
    private void PayTribute(RivalTribe rival)
    {
        if (!rival.IsSettled)
        {
            EmitSignal(SignalName.PackGave, rival, true);
            return;
        }
        int food = Mathf.RoundToInt(Tune.TradeFood * _tribe.Bonuses.Trade);
        int wood = Mathf.RoundToInt(Tune.TradeWood * _tribe.Bonuses.Trade);
        _tribe.Economy.AddFood(food);
        _tribe.Economy.AddWood(wood);
        Log.Verbose("RivalKeeper", $"Faction {rival.Faction} pays tribute: {food} food, {wood} wood.");
        EmitSignal(SignalName.TributePaid, rival.NameKey, food, wood);
        EmitSignal(SignalName.Changed);
    }

    /// <summary>A gift of GiftFood food to a tribe, its relation rises by RelationPerGift.</summary>
    /// <param name="rival">(Required) The tribe.</param>
    /// <returns>True when the stock paid for it.</returns>
    public bool Gift(RivalTribe rival)
    {
        if (!_tribe.Economy.Spend(Village.GiftFood, 0))
        {
            return false;
        }
        rival.Shift(Tune.RelationPerGift);
        Log.Verbose("RivalKeeper", $"Gift to faction {rival.Faction}, relation {rival.Relation:F2}.");
        EmitSignal(SignalName.Changed);
        return true;
    }

    /// <summary>Cheat and test: a raid of a tribe right now, whatever its relation.</summary>
    /// <param name="rival">(Required) The tribe.</param>
    public void ForceRaid(RivalTribe rival)
    {
        _raidTimer[rival] = Tune.RaidInterval;
        SendRaid(rival);
    }

    // ===========================================================================
    // ================================   FIGHT   ================================
    // ===========================================================================
    //
    //   ResolveFights   Idle villagers engage enemies near the village, hostile bodies in contact
    //                   hurt each other, raiders at a hut hurt it, kills cost relation.
    //   ResolveRazing   Kin of the player at an enemy building tear it down, its villagers defend,
    //                   a tribe without buildings is defeated and looted.
    //   HutAt           The rival building under a plane point (right click orders).
    //   IsHostile       Whether two kin fight: different factions and one of them is a raider or attacks the other.
    //   EnemyAt         The enemy kin nearest to a plane point within a radius (right click orders).
    //   ReportKill      A rival died by the player's side: relation down, KinKilled for its meat.
    //

    private const float CONTACT_SHARE = 1.15f;       // bodies this close (in sums of sizes) fight
    private const float PLAYER_DAMAGE = 2f;          // the leader fights with this many times the kin damage

    private void ResolveFights(float dt, float size)
    {
        var enemies = new List<Kin>();
        foreach (RivalTribe rival in _rivals)
        {
            enemies.AddRange(rival.Kin);
        }
        var own = new List<Kin>();
        // Every member of the player's pack fights, the villagers of the tribe and the animals of the pack before it
        foreach (Kin kin in _pack.Kin)
        {
            if (KinMode.Wild != kin.Mode)
            {
                own.Add(kin);
            }
        }

        // Idle villagers run to raiders and to enemies that attack the tribe
        foreach (Kin villager in own)
        {
            if (!villager.IsIdle)
            {
                continue;
            }
            Kin   nearest  = null;
            float distance = size * Village.EngageRange;
            foreach (Kin enemy in enemies)
            {
                bool isThreat = KinRole.Raider == enemy.Role || (enemy.AttackTarget is Kin target && 0 == target.Faction) || enemy.AttackTarget == Player;
                float d = enemy.Position.DistanceTo(villager.Position);
                if (isThreat && d < distance)
                {
                    nearest  = enemy;
                    distance = d;
                }
            }
            if (null != nearest)
            {
                villager.OrderAttack(nearest);
            }
        }

        // Kin against kin, the player against enemies
        float damage = Village.KinDamage * dt;
        foreach (Kin enemy in enemies)
        {
            if (enemy.IsQueuedForDeletion())
            {
                continue;
            }
            foreach (Kin villager in own)
            {
                if (villager.IsQueuedForDeletion() || !IsHostile(villager, enemy))
                {
                    continue;
                }
                if (villager.Position.DistanceTo(enemy.Position) <= (villager.Radius + enemy.Radius) * CONTACT_SHARE)
                {
                    villager.Hurt(damage * enemy.DamageScale);
                    if (enemy.Hurt(damage * villager.DamageScale * _tribe.Bonuses.Damage))
                    {
                        OnEnemyKilled(enemy);
                        break;
                    }
                }
            }
            if (enemy.IsQueuedForDeletion())
            {
                continue;
            }
            bool isPlayerFight = enemy.AttackTarget == Player || KinRole.Raider == enemy.Role && !Player.IsDead;
            if (isPlayerFight && Player.GlobalPosition.DistanceTo(enemy.Position) <= (size + enemy.Radius) * CONTACT_SHARE)
            {
                Player.Digest(damage, enemy.Position);
                if (enemy.Hurt(damage * PLAYER_DAMAGE * _tribe.Bonuses.Damage))
                {
                    OnEnemyKilled(enemy);
                    continue;
                }
            }

            // Raiders at a hut hurt it, the defense of the village softens the blows
            if (KinRole.Raider == enemy.Role && !enemy.IsLeaving && null == enemy.AttackTarget)
            {
                float softened = damage / Village.KinDamage * Tune.RaidDamage / (1f + _tribe.Defense / Tune.DefenseScale) / _tribe.Bonuses.BuildingHealth;
                foreach (Hut hut in new List<Hut>(_tribe.Huts))
                {
                    if (hut.Position.DistanceTo(enemy.Position) <= hut.ContactRadius + enemy.Radius)
                    {
                        if (hut.Hurt(softened))
                        {
                            EmitSignal(SignalName.Changed);
                        }
                        break;
                    }
                }
                // Its target hut is gone: on to the next one
                if (null == enemy.OrderTarget && !enemy.IsLeaving && _tribe.Huts.Count > 0)
                {
                    Hut next = _tribe.Huts[Rand.Index(_tribe.Huts.Count)];
                    if (next.Position.DistanceTo(enemy.Position) > next.ContactRadius + enemy.Radius)
                    {
                        enemy.OrderMove(next.Position);
                    }
                }
            }
        }
    }

    /*
      For every kin of the player that stands at the rival building it was ordered to raze:
      > The building takes RazeDamage per second, more from a warrior.
      > Idle villagers of that tribe within EngageRange run to defend it.
      > A building torn down costs RelationPerRaze, the last one defeats the tribe: its
        people leave and its stock is looted.
    */
    private void ResolveRazing(float dt, float size)
    {
        foreach (Kin kin in _pack.Kin)
        {
            if (kin.RazeTarget is not Hut building || !IsInstanceValid(building) || building.IsQueuedForDeletion())
            {
                continue;
            }
            RivalTribe rival = _rivals.Find(tribe => tribe.Faction == building.Faction);
            if (null == rival || kin.Position.DistanceTo(building.Position) > building.ContactRadius + kin.Radius)
            {
                continue;
            }
            foreach (Kin defender in rival.Kin)
            {
                if (KinRole.Villager == defender.Role && defender.IsIdle && defender.Position.DistanceTo(kin.Position) <= size * Village.EngageRange)
                {
                    defender.OrderAttack(kin);
                }
            }
            if (!building.Hurt(Tune.RazeDamage * kin.DamageScale * _tribe.Bonuses.Damage * dt))
            {
                continue;
            }
            rival.Shift(-Tune.RelationPerRaze);
            if (0 == rival.Huts.Count && !rival.IsDefeated)
            {
                rival.Defeat();
                _tribe.Economy.AddFood(Tune.DefeatFood);
                _tribe.Economy.AddWood(Tune.DefeatWood);
                EmitSignal(SignalName.TribeDefeated, rival.NameKey, Tune.DefeatFood, Tune.DefeatWood);
            }
            EmitSignal(SignalName.Changed);
        }
    }

    /// <summary>The rival building whose footprint lies under a plane point, null when none.</summary>
    /// <param name="point">(Required) Plane point under the cursor.</param>
    public Hut HutAt(Vector2 point)
    {
        foreach (RivalTribe rival in _rivals)
        {
            foreach (Hut hut in rival.Huts)
            {
                if (!hut.IsQueuedForDeletion() && hut.Position.DistanceTo(point) <= hut.ContactRadius)
                {
                    return hut;
                }
            }
        }
        return null;
    }

    /// <summary>Two kin fight when their factions differ and one is a raider or attacks the other.</summary>
    private static bool IsHostile(Kin a, Kin b)
    {
        return a.Faction != b.Faction
            && (KinRole.Raider == a.Role || KinRole.Raider == b.Role || a.AttackTarget == b || b.AttackTarget == a);
    }

    /// <summary>A kill by the player's tribe costs relation with the victim's tribe.</summary>
    private void OnEnemyKilled(Kin enemy)
    {
        ReportKill(enemy);
    }

    /// <summary>A rival animal or villager died by the player's side: its people like the player less, its body is meat or food (KinKilled).</summary>
    /// <param name="enemy">(Required) The kin that died, already freed.</param>
    public void ReportKill(Kin enemy)
    {
        foreach (RivalTribe rival in _rivals)
        {
            if (rival.Faction == enemy.Faction)
            {
                rival.Shift(-Tune.RelationPerKill);
            }
        }
        EmitSignal(SignalName.KinKilled, enemy.GlobalPosition, enemy.AdultSize);
        EmitSignal(SignalName.Changed);
    }

    /// <summary>The enemy kin nearest to a plane point within a radius, null when none.</summary>
    /// <param name="point">(Required) Plane point under the cursor.</param>
    /// <param name="radius">(Required) Pick radius on the plane.</param>
    public Kin EnemyAt(Vector2 point, float radius)
    {
        Kin   hit      = null;
        float distance = radius;
        foreach (RivalTribe rival in _rivals)
        {
            foreach (Kin kin in rival.Kin)
            {
                float d = kin.Position.DistanceTo(point);
                if (d < distance)
                {
                    hit      = kin;
                    distance = d;
                }
            }
        }
        return hit;
    }

    // ===========================================================================
    // =================================   SAVE   ================================
    // ===========================================================================
    //
    //   BuildHalls  The civilization reaches the neighbors: a headquarters on every fire.
    //   ForgetGone  Every neighbor drops its freed animals, first in a frame.
    //   Save      Relation, respect and defeat of every neighbor in order.
    //   Restore   The neighbors of a save: the packs settle around the nest again (new dens),
    //             from the tribe on they become tribes, then every one takes its saved standing;
    //             a defeated one loses its buildings again.
    //

    /// <summary>The neighbors as plain save data (Core/Save/SaveGame.cs).</summary>
    public List<SaveRival> Save()
    {
        return _rivals.Select(rival => new SaveRival(rival.Relation, rival.Respect, rival.IsDefeated)).ToList();
    }

    /// <summary>Every neighbor forgets its animals that were freed since the last frame; Society calls it first in a frame.</summary>
    public void ForgetGone()
    {
        foreach (RivalTribe rival in _rivals)
        {
            rival.ForgetGone();
        }
    }

    /// <summary>The player founded the city: every settled neighbor raises its own headquarters on its fire and sails from now on.</summary>
    public void BuildHalls()
    {
        foreach (RivalTribe rival in _rivals)
        {
            rival.BuildHall();
            rival.HasShips = true;      // from now on its traders and raiders land by ship
        }
        EmitSignal(SignalName.Changed);
    }

    /// <summary>Takes over the neighbors of a save, the pack (nest) and the tribe are restored already.</summary>
    /// <param name="saved">(Required) The saved neighbors in order.</param>
    public void Restore(IReadOnlyList<SaveRival> saved)
    {
        SettlePacks();
        if (Player.IsTribal)
        {
            Settle(_tribe.Path);
        }
        for (int i = 0; i < _rivals.Count && i < saved.Count; i++)
        {
            RivalTribe rival = _rivals[i];
            rival.Shift(saved[i].Relation - rival.Relation);
            rival.ShiftRespect(saved[i].Respect - rival.Respect);
            if (saved[i].IsDefeated && !rival.IsDefeated)
            {
                foreach (Hut hut in rival.Huts.ToList())
                {
                    hut.Hurt(hut.MaxHp);
                }
                rival.Defeat();
            }
        }
        if (Player.Stage >= Player.CIVILIZATION_STAGE)
        {
            BuildHalls();     // the civilization reached the islands before the save
        }
        EmitSignal(SignalName.Changed);
        Log.Verbose("RivalKeeper", $"Neighbors restored: {_rivals.Count} ({saved.Count} saved).");
    }
}