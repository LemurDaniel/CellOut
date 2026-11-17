// NOTE:
//   A neighbor: in the pack stage a foreign pack of another species around its den, from
//   the tribe on a fire with huts and villagers somewhere on the islands (Settle turns the
//   den into a village). It has its own body plan and tint, a relation to the player from
//   hostile (-1) to friendly (+1) and a respect (0..1) the player wins by displays. A friendly tribe sends traders, a hostile one raiders (both are its kin with a
//   Role), the RivalKeeper decides when and resolves what they do. Fire, huts and kin
//   are its children, it steers its kin once per physics frame when the keeper calls
//   Update.
//
//   var rival = new RivalTribe();
//   rival.Setup(faction: 1, nameKey: "RIVAL_NAME_1", fire, entry, design, tint, relation: 0.5f, size, isSettled: true);
//   pack.Setup(...same..., isSettled: false);   // a foreign pack at its den, later pack.Settle()
//   AddChild(rival);
//   rival.Update(dt, coast, playerSize);

//
//   Chapters:
//     SETTINGS   Faction, name, fire, entry point, relation and respect.
//     VILLAGE    The den of a pack or fire, huts and villagers of a tribe.
//     KIN        Villagers, traders and raiders: spawning, steering and leaving.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class RivalTribe : Node2D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Faction      Its number, 1 and up.
    //   NameKey      Translation key of its name.
    //   Fire         Plane point of its fire.
    //   Entry        Plane point where its traders and raiders enter the player's island.
    //   Relation     -1 hostile .. 1 friendly towards the player's tribe.
    //   IsFriendly   Relation at FriendlyAt or above: it trades.
    //   IsHostile    Relation at HostileAt or below: it raids.
    //   Setup        Everything before AddChild.
    //   Shift        Moves the relation, clamped.
    //   Respect      0 .. 1, won by displays, it drifts back (RivalKeeper).
    //   IsRespectful Respect at RespectAt or above: tribute, never a raid.
    //   ShiftRespect Moves the respect, clamped.
    //

    /// <summary>Village size and relation thresholds from data/balance.json (tribe.rivals).</summary>
    private static RivalsBalance Tune => Balance.Current.Tribe.Rivals;

    public int Faction { get; private set; } = 1;
    public string NameKey { get; private set; } = "RIVAL_NAME_1";

    /// <summary>Key of its creature template (data/creatures), its body plan.</summary>
    public string Species { get; init; } = "";
    public Vector2 Fire { get; private set; }
    public Vector2 Entry { get; private set; }
    public float Relation { get; private set; }

    /// <summary>Color of its kin, also its color on the map.</summary>
    public Color Tint => _tint;

    public bool IsFriendly => Relation >= Tune.FriendlyAt;
    public bool IsHostile => Relation <= Tune.HostileAt;

    private CreatureDesign _bodyPlan;
    private Color _tint;
    private float _size;

    /// <summary>Sets up the tribe, call before AddChild.</summary>
    /// <param name="faction">(Required) Its number, 1 and up.</param>
    /// <param name="nameKey">(Required) Translation key of its name.</param>
    /// <param name="fire">(Required) Plane point of its fire, on land.</param>
    /// <param name="entry">(Required) Where its traders and raiders enter the player's island.</param>
    /// <param name="bodyPlan">(Required) Body plan of its kin (the player's), copied per kin.</param>
    /// <param name="tint">(Required) Color of its kin.</param>
    /// <param name="relation">(Required) Starting relation, -1..1.</param>
    /// <param name="size">(Required) Player.BodySize(), its kin are grown.</param>
    /// <param name="isSettled">(Optional) A tribe with fire and huts, false: a foreign pack at its den. Defaults to true.</param>
    public void Setup(int faction, string nameKey, Vector2 fire, Vector2 entry, CreatureDesign bodyPlan, Color tint, float relation, float size, bool isSettled = true)
    {
        IsSettled = isSettled;
        Faction   = faction;
        NameKey   = nameKey;
        Fire      = fire;
        Entry     = entry;
        _bodyPlan = bodyPlan;
        _tint     = tint;
        Relation  = Mathf.Clamp(relation, -1f, 1f);
        _size     = size;
    }

    /// <summary>Moves the relation, clamped to -1..1.</summary>
    /// <param name="amount">(Required) Positive towards friendly.</param>
    public void Shift(float amount)
    {
        Relation = Mathf.Clamp(Relation + amount, -1f, 1f);
    }

    public float Respect { get; private set; }

    public bool IsRespectful => Respect >= Balance.Current.Tribe.Ways.RespectAt;

    /// <summary>Moves the respect, clamped to 0..1.</summary>
    /// <param name="amount">(Required) Positive: it bows to the player.</param>
    public void ShiftRespect(float amount)
    {
        Respect = Mathf.Clamp(Respect + amount, 0f, 1f);
    }

    // ===========================================================================
    // ===============================   VILLAGE   ===============================
    // ===========================================================================
    //
    //   Nest         Its den, the fire once it is a tribe.
    //   IsSettled    A tribe with fire and huts, false while it is a foreign pack at its den.
    //   Huts         Its buildings still standing.
    //   IsDefeated   It lost its last building (or every animal of the pack): no trades, no raids, its people left.
    //   Defeat       Marks it defeated and sends everyone away.
    //   _Ready       The den with the animals of the pack, or right away the village.
    //   Settle       The pack becomes a tribe: the fire is lit, huts on a ring, villagers up to Villagers.
    //   Release      Hands one animal over to the player's pack (courted until it joined).
    //

    public Nest Nest { get; private set; }

    private static readonly BuildingKind[] SIGNATURE_KINDS = { BuildingKind.Totem, BuildingKind.Watchtower, BuildingKind.Market, BuildingKind.Barracks };

    private readonly List<Hut> _huts = new();

    public IReadOnlyList<Hut> Huts => _huts;

    public bool IsDefeated { get; private set; }

    /// <summary>The tribe gives up: every kin walks home and leaves, nothing sets out any more.</summary>
    public void Defeat()
    {
        IsDefeated = true;
        foreach (Kin kin in _kin)
        {
            kin.GoHome();
        }
        Log.Verbose("RivalTribe", $"Faction {Faction} is defeated.");
    }

    private Coast _coast;

    /// <summary>Hands in the coast, before AddChild: huts and kin need the ground and the land.</summary>
    /// <param name="coast">(Required) The coast.</param>
    public void AttachCoast(Coast coast)
    {
        _coast = coast;
    }

    public bool IsSettled { get; private set; }

    public override void _Ready()
    {
        Nest = new Nest { Position = Fire, Size = _size, GroundHeight = _coast.GroundHeight(Fire) * _size };
        AddChild(Nest);
        if (IsSettled)
        {
            BuildVillage();
            return;
        }

        // A foreign pack: its animals roam around the den
        for (int i = 0; i < Balance.Current.Tribe.Ways.PackSize; i++)
        {
            Kin animal = SpawnKin(Fire + Vector2.FromAngle(Rand.Angle()) * _size * 3f, KinRole.Villager);
            animal.JoinTribe(Fire);
        }
        Log.Verbose("RivalTribe", $"Foreign pack {Faction} lives at its den {Fire} with {_kin.Count} animal(s).");
    }

    /// <summary>The foreign pack becomes a tribe: its den turns into a fire with huts, villagers up to Villagers.</summary>
    public void Settle()
    {
        if (IsSettled || IsDefeated)
        {
            return;
        }
        IsSettled = true;
        BuildVillage();
    }

    /// <summary>Fire, huts on a ring and villagers around the fire, the animals of the pack count as villagers.</summary>
    private void BuildVillage()
    {
        Nest.LightFire();
        float unit = _coast.Unit;
        for (int i = 0; i < Tune.Huts; i++)
        {
            float   angle = Mathf.Tau * i / Tune.Huts + 0.4f;
            Vector2 spot  = Fire + Vector2.FromAngle(angle) * Balance.Current.Tribe.Village.HutDistance * unit;
            if (!_coast.IsOnLand(spot))
            {
                continue;
            }
            spot = _coast.PushOutOfTrees(spot, unit * Hut.SCALE);
            var hut = new Hut { Position = spot, Size = unit * Hut.SCALE, GroundHeight = _coast.GroundHeight(spot) * _size };
            // Dwellings first, the last building is what the tribe is known for; every one from a random template
            BuildingKind kind = i < Tune.Huts - 1 ? BuildingKind.Dwelling : SIGNATURE_KINDS[Faction % SIGNATURE_KINDS.Length];
            hut.Setup(BuildingTemplates.Build(kind, Rand.Index(BuildingTemplates.Count(kind))), Faction);
            _coast.AddPad(spot, hut.FoundationRadius);
            hut.Destroyed += () => _huts.Remove(hut);
            _huts.Add(hut);
            AddChild(hut);
        }
        for (int i = _kin.Count; i < Tune.Villagers; i++)
        {
            Kin villager = SpawnKin(Fire + Vector2.FromAngle(Rand.Angle()) * _size * 3f, KinRole.Villager);
            villager.JoinTribe(Fire);
        }
        Log.Verbose("RivalTribe", $"Faction {Faction} settled at {Fire} with {_huts.Count} hut(s), relation {Relation:F2}.");
    }

    /// <summary>One animal leaves the foreign pack for the player's pack, the caller adds it there.</summary>
    /// <param name="animal">(Required) A kin of this pack.</param>
    /// <returns>True when it belonged to this pack and left it.</returns>
    public bool Release(Kin animal)
    {
        if (!_kin.Remove(animal))
        {
            return false;
        }
        RemoveChild(animal);
        return true;
    }

    // ===========================================================================
    // =================================   KIN   =================================
    // ===========================================================================
    //
    //   Kin           Every kin of the tribe: villagers at home, traders and raiders abroad.
    //   SpawnKin      One grown kin of the tribe at a spot with a role.
    //   SendTrader    A trader walks from the entry point to the player's fire.
    //   SendRaiders   Raiders walk from the entry point towards the player's village.
    //   Update        Steers every kin, frees the ones that left, keeps fire and huts on the ground.
    //

    private readonly List<Kin> _kin = new();

    public IReadOnlyList<Kin> Kin => _kin;

    /// <summary>One grown kin of this tribe.</summary>
    /// <param name="spot">(Required) Plane point, moved out of trunks.</param>
    /// <param name="role">(Required) Villager, trader or raider.</param>
    public Kin SpawnKin(Vector2 spot, KinRole role)
    {
        var kin = new Kin { Position = _coast.PushOutOfTrees(spot, _size), Role = role, Faction = Faction };
        kin.Setup(_bodyPlan, _size, _tint, home: KinRole.Villager == role ? Fire : Entry);
        _kin.Add(kin);
        AddChild(kin);
        return kin;
    }

    /// <summary>A trader enters at the entry point and walks to a target (the player's fire).</summary>
    /// <param name="target">(Required) Plane point of the player's fire.</param>
    public Kin SendTrader(Vector2 target)
    {
        Kin trader = SpawnKin(Entry, KinRole.Trader);
        trader.JoinTribe(Entry);
        trader.OrderMove(target);
        Log.Verbose("RivalTribe", $"Faction {Faction} sends a trader to {target}.");
        return trader;
    }

    /// <summary>Raiders enter at the entry point and walk towards a target (a hut or the fire).</summary>
    /// <param name="target">(Required) Plane point to walk to.</param>
    /// <param name="count">(Required) Raiders sent.</param>
    public List<Kin> SendRaiders(Vector2 target, int count)
    {
        var raiders = new List<Kin>();
        for (int i = 0; i < count; i++)
        {
            Kin raider = SpawnKin(Entry + Vector2.FromAngle(Rand.Angle()) * _size * 2f, KinRole.Raider);
            raider.JoinTribe(Entry);
            raider.OrderMove(target);
            raiders.Add(raider);
        }
        Log.Verbose("RivalTribe", $"Faction {Faction} raids with {count} towards {target}.");
        return raiders;
    }

    /// <summary>One physics frame of the tribe.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), the ground scales with it.</param>
    public void Update(float dt, float playerSize)
    {
        _kin.RemoveAll(kin => !IsInstanceValid(kin) || kin.IsQueuedForDeletion());
        foreach (Kin kin in _kin)
        {
            kin.Steer(dt, _coast, _kin, null, playerSize, isPackFollowing: false);
            // A trader or raider that walked home leaves the world
            if (kin.IsLeaving && null == kin.OrderTarget)
            {
                kin.QueueFree();
            }
        }
        Nest.Size         = playerSize;
        Nest.GroundHeight = _coast.GroundHeight(Fire) * playerSize;
        foreach (Hut hut in _huts)
        {
            hut.GroundHeight = _coast.GroundHeight(hut.Position) * playerSize;
        }
    }
}
