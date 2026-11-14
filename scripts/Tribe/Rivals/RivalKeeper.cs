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
//

using System.Collections.Generic;
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
    //   Rivals         The neighbor tribes, empty before the player's tribe.
    //   Setup          The session, the pack and the tribe, before AddChild.
    //

    [Signal] public delegate void ChangedEventHandler();
    [Signal] public delegate void RaidStartedEventHandler(string nameKey);
    [Signal] public delegate void TradeArrivedEventHandler(string nameKey, int food, int wood);
    [Signal] public delegate void TribeDefeatedEventHandler(string nameKey, int food, int wood);

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
    //   Settle       Count rival tribes: the first on the player's island, the others on the next islands.
    //   HomeOf       A fire spot for rival i: far across the own island, or the center of another island.
    //   EntryFor     Where a tribe enters the player's island: its fire on the same island, else the shore towards it.
    //   TintFor      A clearly different hue per tribe.
    //

    private const float HUE_STEP = 0.21f;            // around the hue wheel per rival
    private const int HOME_TRIES = 24;
    private const float SHORE_STEP = 2f;             // Units per step when walking out to the shore
    private const float SHORE_MARGIN = 4f;           // Units back from the water for an entry point

    /// <summary>Settles the rival tribes once, when the player's tribe exists.</summary>
    /// <example><code>rivals.Settle();   // in Main when the stage turns to the tribe</code></example>
    public void Settle()
    {
        if (_rivals.Count > 0 || null == Coast || null == _pack.Nest)
        {
            return;
        }
        float   size = Player.BodySize();
        Vector2 fire = _pack.Nest.Position;
        for (int i = 0; i < Tune.Count; i++)
        {
            Vector2? home = HomeOf(i, fire);
            if (home is not Vector2 spot)
            {
                continue;
            }
            // The first neighbor is friendly, the second hostile, the rest are somewhere in between
            float relation = i switch { 0 => 0.5f, 1 => -0.5f, _ => Rand.Range(-0.2f, 0.2f) };
            var rival  = new RivalTribe();
            rival.AttachCoast(Coast);
            rival.Setup(i + 1, $"RIVAL_NAME_{i + 1}", spot, EntryFor(spot, fire), Player.Creature, TintFor(i), relation, size);
            _rivals.Add(rival);
            AddChild(rival);
        }
        Log.Verbose("RivalKeeper", $"{_rivals.Count} rival tribe(s) settled.");
        EmitSignal(SignalName.Changed);
    }

    /// <summary>A fire spot for rival i: across the own island (i = 0) or the center of island i, grass only.</summary>
    private Vector2? HomeOf(int index, Vector2 fire)
    {
        float grass = Balance.Current.Tribe.Pack.NestDepth * Coast.Unit;
        if (index > 0 && index < Coast.Islands.Count)
        {
            Vector2 center = Coast.Islands[index].Center;
            return Coast.LandDepth(center) >= grass ? center : null;
        }
        for (int attempt = 0; attempt < HOME_TRIES; attempt++)
        {
            Vector2 spot = fire + Vector2.FromAngle(Rand.Angle()) * Tune.SameIslandDistance * Coast.Unit * Rand.Range(0.8f, 1.1f);
            if (Coast.LandDepth(spot) >= grass)
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
    //   Update        Rivals update, traders and raids set out and arrive, fights resolve, relations drift.
    //   SendTrade     A friendly tribe sends a trader every TradeInterval.
    //   SendRaid      A hostile tribe sends raiders every RaidInterval towards the nearest hut.
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
            if (rival.IsDefeated)
            {
                continue;
            }

            // Relations drift towards neutral, so no war and no friendship lasts without deeds
            float drift = Mathf.Min(Mathf.Abs(rival.Relation), Tune.RelationDrift * dt);
            rival.Shift(rival.Relation > 0f ? -drift : drift);

            _tradeTimer[rival] = _tradeTimer.GetValueOrDefault(rival, Tune.TradeInterval * 0.5f) - dt;
            _raidTimer[rival]  = _raidTimer.GetValueOrDefault(rival, Tune.RaidInterval * 0.5f) - dt;
            if (rival.IsFriendly && _tradeTimer[rival] <= 0f)
            {
                _tradeTimer[rival] = Tune.TradeInterval;
                rival.SendTrader(_pack.Nest.Position);
            }
            if (rival.IsHostile && _raidTimer[rival] <= 0f)
            {
                _raidTimer[rival] = Tune.RaidInterval;
                SendRaid(rival);
            }

            // Traders at the fire deliver, raiders raid for RaidSeconds then leave
            foreach (Kin kin in rival.Kin)
            {
                if (KinRole.Trader == kin.Role && !kin.IsLeaving && kin.Position.DistanceTo(_pack.Nest.Position) <= size * Balance.Current.Tribe.Pack.NestRange)
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

    /// <summary>The trader hands over food and wood (more with a market), the relation rises (more with a totem), then it walks home.</summary>
    private void Deliver(RivalTribe rival, Kin trader)
    {
        int food = Mathf.RoundToInt(Tune.TradeFood * (1f + _tribe.TradeBonus));
        int wood = Mathf.RoundToInt(Tune.TradeWood * (1f + _tribe.TradeBonus));
        _tribe.Economy.AddFood(food);
        _tribe.Economy.AddWood(wood);
        rival.Shift(Tune.RelationPerTrade + _tribe.RelationBonus);
        trader.GoHome();
        Log.Verbose("RivalKeeper", $"Trader of faction {rival.Faction} delivered {food} food and {wood} wood, relation {rival.Relation:F2}.");
        EmitSignal(SignalName.TradeArrived, rival.NameKey, food, wood);
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
    //

    private const float CONTACT_SHARE = 1.15f;       // bodies this close (in sums of sizes) fight
    private const float HUT_REACH = 1.3f;            // raiders this close to a hut (in hut radii) hurt it
    private const float PLAYER_DAMAGE = 2f;          // the leader fights with this many times the kin damage

    private void ResolveFights(float dt, float size)
    {
        var enemies = new List<Kin>();
        foreach (RivalTribe rival in _rivals)
        {
            enemies.AddRange(rival.Kin);
        }
        var own = new List<Kin>();
        foreach (Kin kin in _pack.Kin)
        {
            if (KinMode.Villager == kin.Mode)
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
                    if (enemy.Hurt(damage * villager.DamageScale))
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
                if (enemy.Hurt(damage * PLAYER_DAMAGE))
                {
                    OnEnemyKilled(enemy);
                    continue;
                }
            }

            // Raiders at a hut hurt it, the defense of the village softens the blows
            if (KinRole.Raider == enemy.Role && !enemy.IsLeaving && null == enemy.AttackTarget)
            {
                float softened = damage / Village.KinDamage * Tune.RaidDamage / (1f + _tribe.Defense / Tune.DefenseScale);
                foreach (Hut hut in new List<Hut>(_tribe.Huts))
                {
                    if (hut.Position.DistanceTo(enemy.Position) <= hut.Size * Hut.RADIUS * HUT_REACH + enemy.Radius)
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
                    if (next.Position.DistanceTo(enemy.Position) > next.Size * Hut.RADIUS * HUT_REACH + enemy.Radius)
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
            if (null == rival || kin.Position.DistanceTo(building.Position) > building.Size * Hut.RADIUS * HUT_REACH + kin.Radius)
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
            if (!building.Hurt(Tune.RazeDamage * kin.DamageScale * dt))
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
                if (!hut.IsQueuedForDeletion() && hut.Position.DistanceTo(point) <= hut.Size * Hut.RADIUS * HUT_REACH)
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
        foreach (RivalTribe rival in _rivals)
        {
            if (rival.Faction == enemy.Faction)
            {
                rival.Shift(-Tune.RelationPerKill);
            }
        }
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
}
