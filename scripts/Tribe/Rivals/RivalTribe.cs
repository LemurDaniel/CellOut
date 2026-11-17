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
    //   BuildHall    The headquarters on its fire once the player founded the city.
    //   PutUp        One building from a random template.
    //   Release      Hands one animal over to the player's pack (courted until it joined).
    //   IsLoner      A lone beast instead of a pack, it never settles.
    //   Presence     Bodies one of its animals counts for in a display.
    //   Released     Animals that left for the player's pack.
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

        // A foreign pack: its animals roam around the den, a loner is one beast with the health and bite of several
        WaysBalance ways = Balance.Current.Tribe.Ways;
        for (int i = 0; i < (IsLoner ? 1 : ways.PackSize); i++)
        {
            Kin animal = SpawnKin(Fire + Vector2.FromAngle(Rand.Angle()) * _size * 3f, KinRole.Villager);
            animal.JoinTribe(Fire);
            if (IsLoner)
            {
                animal.Toughen(ways.LonerHealth, ways.LonerDamage);
            }
        }
        Log.Verbose("RivalTribe", $"Foreign pack {Faction} lives at its den {Fire} with {_kin.Count} animal(s) (loner: {IsLoner}).");
    }

    /// <summary>(Optional) A lone beast instead of a pack: one big animal at its lair that never settles (Tribe/Rivals/RivalKeeper.cs, LONERS). Defaults to false.</summary>
    public bool IsLoner { get; init; }

    /// <summary>Bodies of its own size one of its animals counts for in a display: a lone beast impresses like several.</summary>
    public float Presence => IsLoner ? Balance.Current.Tribe.Ways.LonerPresence : 1f;

    /// <summary>Animals that left this pack for the player's (Release), a pack lets only AdoptPerPack go.</summary>
    public int Released { get; private set; }

    /// <summary>The foreign pack becomes a tribe: it moves to its new home, lights a fire there and builds huts, villagers up to Villagers. A loner stays what it is.</summary>
    /// <param name="home">(Optional) Plane point of the new fire, far enough from the player's settlement; its animals walk there. Defaults to its den.</param>
    /// <param name="entry">(Optional) Where its traders and raiders enter the player's island from the new home. Defaults to the old entry.</param>
    public void Settle(Vector2? home = null, Vector2? entry = null)
    {
        if (IsSettled || IsDefeated || IsLoner)
        {
            return;
        }
        if (home is Vector2 spot)
        {
            Fire          = spot;
            Entry         = entry ?? spot;
            Nest.Position = spot;
            foreach (Kin animal in _kin)
            {
                animal.JoinTribe(spot);
            }
        }
        IsSettled = true;
        BuildVillage();
    }

    /// <summary>The civilization reached the islands: the tribe raises its own headquarters on its fire.</summary>
    public void BuildHall()
    {
        if (!IsSettled || IsDefeated)
        {
            return;
        }
        _coast.StylePad(Fire, CoastTerrain.PadStyle.City);       // its trodden ways become streets
        if (_huts.Exists(hut => IsInstanceValid(hut) && BuildingKind.Hall == hut.Design.Kind))
        {
            return;
        }
        PutUp(BuildingKind.Hall, Fire, isSqueezed: true);
        Nest.GiveWayToHall();
        Log.Verbose("RivalTribe", $"Faction {Faction} raised its headquarters at {Fire}.");
    }

    /// <summary>A building of a function from a random template at a spot: leveled ground, it blocks the way; squeezed it shrinks to fit between the others.</summary>
    private void PutUp(BuildingKind kind, Vector2 spot, bool isSqueezed = false, float? turn = null)
    {
        BuildingDesign design = BuildingTemplates.Build(kind, Rand.Index(BuildingTemplates.Count(kind)));
        float          share  = isSqueezed ? Hut.FitShare(design, _coast.Unit * Hut.SCALE, spot, _huts, _coast.Unit) : 1f;
        var hut = new Hut { Position = spot, Size = _coast.Unit * Hut.SCALE * share, GroundHeight = _coast.GroundHeight(spot) * _size, Turn = turn };
        hut.Setup(design, Faction);
        hut.Destroyed += () => _huts.Remove(hut);
        hut.BlockOn(_coast);
        _huts.Add(hut);
        AddChild(hut);
    }

    private SettlementWall _wall;

    /// <summary>Fire, the foundation with its ways and wall, huts on a ring and villagers around the fire, the animals of the pack count as villagers.</summary>
    private void BuildVillage()
    {
        Nest.LightFire();
        // The same ground plan as the player's settlement (Tribe/Village/Settlement.cs): one foundation, the buildings
        // on every other plot of the inner ring, their fronts to the fire
        float unit = _coast.Unit;
        if (!_coast.HasPadAt(Fire))
        {
            _coast.AddPad(Fire, Settlement.Radius(unit));
        }
        _coast.StylePad(Fire, CoastTerrain.PadStyle.Village);
        _wall = new SettlementWall();
        _wall.Setup(_coast, Fire);
        AddChild(_wall);
        IReadOnlyList<SettlementSlot> slots = Settlement.SlotsAround(Fire, unit);
        int built = 0;
        for (int i = 0; i < slots.Count && built < Tune.Huts; i += 2)
        {
            if (_coast.LandDepth(slots[i].Spot) < Balance.Current.Tribe.Village.SlotDepth * unit)
            {
                continue;
            }
            // Dwellings first, the last building is what the tribe is known for
            BuildingKind kind = built < Tune.Huts - 1 ? BuildingKind.Dwelling : SIGNATURE_KINDS[Faction % SIGNATURE_KINDS.Length];
            PutUp(kind, slots[i].Spot, turn: slots[i].Turn);
            built++;
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
        Released++;
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
    //   ForgetGone    Drops animals freed since the last frame from Kin.
    //   Update        Steers every kin, frees the ones that left, keeps fire, huts and a landed ship in place.
    //   HomeReach     How close a leaving kin must come to a home that lies inside a building.
    //   HasShips      The tribe sails (from the player's city on): its people land by ship.
    //   LandShip      A ship off the entry while traders or raiders from over the sea are ashore.
    //   UpdateLanding The landed ship rocks, it is gone with its people.
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
        LandShip();
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
        LandShip();
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
    /// <summary>Forgets the animals that are gone (freed at the end of the last frame), before anyone reads Kin this frame.</summary>
    public void ForgetGone()
    {
        _kin.RemoveAll(kin => !IsInstanceValid(kin));
    }

    public void Update(float dt, float playerSize)
    {
        _kin.RemoveAll(kin => !IsInstanceValid(kin) || kin.IsQueuedForDeletion());
        foreach (Kin kin in _kin)
        {
            kin.Steer(dt, _coast, _kin, null, playerSize, isPackFollowing: false);
            // A trader or raider that walked home leaves the world. Home may lie inside a building (the
            // headquarters stands on the fire), then the wall is as far as it gets
            if (kin.IsLeaving && (null == kin.OrderTarget || kin.Position.DistanceTo(kin.Home) <= HomeReach(kin)))
            {
                kin.QueueFree();
            }
        }
        Nest.Size         = playerSize;
        Nest.GroundHeight = _coast.GroundHeight(Fire) * playerSize;
        _wall?.Update(playerSize);
        foreach (Hut hut in _huts)
        {
            hut.GroundHeight = _coast.GroundHeight(hut.Position) * playerSize;
        }
        UpdateLanding(dt, playerSize);
    }

    private const float WALL_REACH = 1.5f;           // radii of a kin from the wall of a building that stands on its home

    /// <summary>How close a kin must come to its home to count as arrived: the wall of a building that stands on it, else 0 (the order ends on its own).</summary>
    private float HomeReach(Kin kin)
    {
        foreach (Hut hut in _huts)
        {
            if (IsInstanceValid(hut) && hut.Position.DistanceTo(kin.Home) < hut.BlockRadius)
            {
                return hut.ContactRadius + kin.Radius * WALL_REACH;
            }
        }
        return 0f;
    }

    /// <summary>(Optional) The tribe sails like the player's city: traders and raiders from over the sea land with a ship. Defaults to false.</summary>
    public bool HasShips { get; set; }

    private const float LANDING_OUT = 3.5f;          // body sizes off the shore the ship lies
    private const int LANDING_STEPS = 8;

    private Node3D _landing;
    private Vector2 _landingPoint;
    private float _landingFacing;
    private float _landingTime;

    /*
      A ship lands only for a tribe that sails and lives over the sea (its entry is a shore,
      not its own fire). It lies off the entry, its bow towards the land, in the first spot
      out there that is water.
    */
    private void LandShip()
    {
        if (!HasShips || Entry.IsEqualApprox(Fire))
        {
            return;
        }
        Vector2 seaward = (Fire - Entry).Normalized();
        _landingPoint = Entry + seaward * _size * LANDING_OUT;
        for (int i = 0; i < LANDING_STEPS && _coast.IsOnLand(_landingPoint); i++)
        {
            _landingPoint += seaward * _size;
        }
        _landingFacing = (Entry - _landingPoint).Angle();
        if (null == _landing)
        {
            _landing = ShipModel.New(_tint);
            AddChild(_landing);
        }
        _landing.Visible = true;
        Log.Verbose("RivalTribe", $"Faction {Faction} lands a ship at {_landingPoint}.");
    }

    /// <summary>The landed ship rocks while traders or raiders of the tribe are on the player's island, it sails off once they are gone.</summary>
    private void UpdateLanding(float dt, float playerSize)
    {
        if (null == _landing || !_landing.Visible)
        {
            return;
        }
        _landingTime += dt;
        ShipModel.Place(_landing, _landingPoint, _landingFacing, playerSize, _landingTime + Faction);
        bool isAshore = _kin.Exists(kin => KinRole.Villager != kin.Role);
        _landing.Visible = isAshore;
    }
}
