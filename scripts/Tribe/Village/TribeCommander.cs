// NOTE:
//   The village of the tribe: the player is its chieftain and walks like the land animal
//   (no strategy view, decision of the user 2026-11-17), the villagers work on their own:
//   idle ones gather the fruit nearby, up to half of them chop the trees around the fire,
//   gathered fruit and chopped wood fill the stock (TribeEconomy). The stock pays for
//   buildings next to the fire: one blueprint per function (DesignOf, edited in the
//   BuildingEditor), dwellings raise villagers, the others help the village while they
//   stand. It does not process on its own: Society calls Update after the pack.
//
//   var tribe = new TribeCommander();
//   tribe.Setup(session, pack);
//   AddChild(tribe);
//   tribe.Update(dt);                       // in _PhysicsProcess
//   tribe.TryBuildAt(BuildingKind.Dwelling, spot);   // the BuildingPlacer, where the chieftain stands

//
//   Chapters:
//     SETTINGS   The session, the pack, the path, the stock and the blueprints.
//     WORK       Villagers gather fruit and chop trees on their own.
//     VILLAGE    Gathering, chopping, buildings and the villagers they raise, per physics frame.
//     SAVE       The tribe as save data and back.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class TribeCommander : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Changed         Stock or huts changed, the panel refreshes.
    //   Economy         Food, wood and huts of the tribe (TribeEconomy).
    //   DesignOf        The blueprint new buildings of a function are built from (BuildingEditor).
    //   TakeDesigns     Takes over the blueprints of a finished sitting in the editor.
    //   IsActive        From the tribe stage on, with a fire to build around.
    //   Fire            Where the fire burns (or the headquarters stands).
    //   Path            Pacifist, territorial or warlike: the goal of the tribe (RivalDiplomacy).
    //   Defense         Defense of the village: the standing watchtowers.
    //   TradeBonus      Share more every trader brings: the standing markets.
    //   RelationBonus   Extra relation per trade: the standing totems.
    //   Setup           The session and the pack, before AddChild.
    //   Bonuses         What the techs of the city do, read here and by the RivalKeeper.
    //   OnKinAte        A villager ate land food: gathered for the stock, more with a storehouse.
    //   TakeMeal        A meal for the stock: of a villager, or shared by the chieftain; meat is worth more.
    //

    [Signal] public delegate void ChangedEventHandler();

    /// <summary>Hut distance, gathering range, chopping and food per fruit from data/balance.json (tribe.village).</summary>
    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    public TribeEconomy Economy { get; } = new();

    /// <summary>What the techs of the city do (Civ/CivKeeper.cs, Society hands them in): food per fruit, farms, damage, building health, trade.</summary>
    public CivBonuses Bonuses { get; set; } = CivBonuses.NONE;

    private readonly Dictionary<BuildingKind, BuildingDesign> _designs = new();

    /// <summary>The blueprint of a function, the first template until the player designs one.</summary>
    public BuildingDesign DesignOf(BuildingKind kind)
    {
        if (!_designs.TryGetValue(kind, out BuildingDesign design))
        {
            design         = BuildingTemplates.Default(kind);
            _designs[kind] = design;
        }
        return design;
    }

    /// <summary>Takes over a blueprint per function, standing buildings keep their look.</summary>
    /// <param name="designOf">(Required) The new blueprint of a function, copied.</param>
    /// <example><code>editor.Applied += session => tribe.TakeDesigns(session.Result);</code></example>
    public void TakeDesigns(System.Func<BuildingKind, BuildingDesign> designOf)
    {
        foreach (BuildingKind kind in System.Enum.GetValues<BuildingKind>())
        {
            DesignOf(kind).CopyFrom(designOf(kind));
        }
        EmitSignal(SignalName.Changed);
    }

    private WorldSession _session;
    private PackKeeper _pack;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;
    private Node2D FoodLayer => _session.FoodLayer;

    public bool IsActive => Player.IsTribal && null != _pack.Nest;

    /// <summary>Plane point of the fire (of the headquarters on it in a city).</summary>
    public Vector2 Fire => _pack.Nest.Position;

    /// <summary>The path of the tribe, locked when it was founded (Society): its goal and what a fallen enemy brings.</summary>
    public TribePath Path { get; set; } = TribePath.Warlike;

    public float Defense => Sum(hut => hut.Design.Defense);

    public float TradeBonus => Sum(hut => hut.Design.TradeBonus);

    public float RelationBonus => Sum(hut => hut.Design.RelationBonus);

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, food layer and coast.</param>
    /// <param name="pack">(Required) The pack whose kin are the villagers.</param>
    public void Setup(WorldSession session, PackKeeper pack)
    {
        _session = session;
        _pack    = pack;
        _pack.KinAte += OnKinAte;
    }

    public override void _Ready()
    {
        TopLevel = true;
    }

    /// <summary>Gathered: every piece a villager eats fills the stock from the tribe on (TakeMeal).</summary>
    private void OnKinAte(Vector2 position, float size, bool isMeat)
    {
        TakeMeal(isMeat);
    }

    /*
      A meal for the stock, from the tribe on:
      - A fruit brings FoodPerFruit, more with every storehouse and with pottery.
      - A piece of meat brings FoodPerMeat on top: the hunt of the chieftain and its retinue feeds the village.
      The chieftain shares what it eats itself (Main wires ContactResolver.PlayerAteOnLand here).
    */
    /// <summary>A villager or the chieftain ate a piece of land food: it fills the stock of the tribe.</summary>
    /// <param name="isMeat">(Required) Meat instead of fruit, it is worth FoodPerMeat more.</param>
    /// <example><code>contacts.PlayerAteOnLand += isMeat => tribe.TakeMeal(isMeat);</code></example>
    public void TakeMeal(bool isMeat)
    {
        if (!IsActive)
        {
            return;
        }
        Economy.AddFood(Tune.FoodPerFruit + (isMeat ? Tune.FoodPerMeat : 0) + (int)Sum(hut => hut.Design.FoodPerFruit) + Bonuses.FoodPerFruit);
        EmitSignal(SignalName.Changed);
    }

    private float Sum(System.Func<Hut, float> value)
    {
        float sum = 0f;
        foreach (Hut hut in _huts)
        {
            sum += value(hut);
        }
        return sum;
    }

    // ===========================================================================
    // ================================   WORK   =================================
    // ===========================================================================
    //
    //   Villagers   Every kin that works in the village (not in the retinue of the chieftain).
    //   Work        Idle villagers gather the fruit nearby, else up to half of them chop the
    //               nearest tree around the fire.
    //   NearestTree The trunk closest to a point within a range, null when none.
    //

    private const float CHOPPER_SHARE = 0.5f;        // at most this share of the villagers chops at once
    private const float CHOP_REACH = 1.2f;           // build radii around the fire where trees are chopped

    /// <summary>Every kin that works in the village: villagers, not the retinue that follows the chieftain.</summary>
    public List<Kin> Villagers()
    {
        var villagers = new List<Kin>();
        foreach (Kin kin in _pack.Kin)
        {
            if (!GodotObject.IsInstanceValid(kin))
            {
                continue;       // freed since the last physics frame, read here outside of it
            }
            if (KinMode.Villager == kin.Mode && !kin.IsQueuedForDeletion())
            {
                villagers.Add(kin);
            }
        }
        return villagers;
    }

    /*
      The villagers work on their own, the chieftain leads by example (wish of the user 2026-11-17,
      no strategy view any more):
      > Fruit within GatherRange: walk there, the pack lets it eat on touch, it fills the stock.
      > Otherwise, while fewer than CHOPPER_SHARE of the villagers chop: chop the nearest tree
        around the fire, every ChopSeconds a load of wood.
      > Otherwise it roams around the fire.
    */
    private void Work(float size)
    {
        List<Kin> villagers = Villagers();
        int choppers = villagers.Count(kin => kin.HasChopOrder);
        foreach (Kin kin in villagers)
        {
            if (!kin.IsIdle)
            {
                continue;
            }
            Food  nearest  = null;
            float distance = size * Tune.GatherRange;
            foreach (Node node in FoodLayer.GetChildren())
            {
                if (node is Food { Habitat: FoodHabitat.Land } food && !food.IsEngulfed && !food.IsQueuedForDeletion())
                {
                    float d = food.Position.DistanceTo(kin.Position);
                    if (d < distance)
                    {
                        nearest  = food;
                        distance = d;
                    }
                }
            }
            if (null != nearest)
            {
                kin.OrderMove(nearest.Position);
                continue;
            }
            if (choppers < villagers.Count * CHOPPER_SHARE && NearestTree(kin.Position, Tune.BuildRadius * Coast.Unit * CHOP_REACH) is Coast.Tree tree)
            {
                kin.OrderChop(tree.Position, tree.TrunkRadius);
                choppers++;
            }
        }
    }

    /// <summary>The trunk closest to a point within a range, null when none.</summary>
    /// <param name="point">(Required) Plane point to search around.</param>
    /// <param name="range">(Required) Search radius on the plane.</param>
    private Coast.Tree? NearestTree(Vector2 point, float range)
    {
        if (null == Coast)
        {
            return null;
        }
        float cell   = TreeGrid.TREE_CELL * Coast.Unit;
        int   reach  = Mathf.CeilToInt(range / cell);
        var   center = new Vector2I(Mathf.FloorToInt(point.X / cell), Mathf.FloorToInt(point.Y / cell));
        Coast.Tree? hit      = null;
        float       distance = range;
        for (int x = -reach; x <= reach; x++)
        {
            for (int y = -reach; y <= reach; y++)
            {
                foreach (Coast.Tree tree in Coast.TreesIn(center + new Vector2I(x, y)))
                {
                    float d = tree.Position.DistanceTo(point);
                    if (d < distance)
                    {
                        hit      = tree;
                        distance = d;
                    }
                }
            }
        }
        return hit;
    }

    // ===========================================================================
    // ===============================   VILLAGE   ===============================
    // ===========================================================================
    //
    //   Huts          Every standing building of the player's tribe.
    //   Update        Villagers work, choppers fill the stock, huts raise villagers and stay on the
    //                 ground, the fire shows the stock.
    //   CanBuildAt    Whether a building may stand at a spot.
    //   TryBuildAt    Pays a building of a function and places it where the player chose.
    //   TryBuild      The same without a chosen spot: the next free slot around the fire.
    //   TryBuildHall  The headquarters of the city on the fire (Civ/CivKeeper.cs).
    //   Hall          The headquarters, HasHall whether it stands.
    //   PutUp         Puts up a paid building.
    //

    private readonly List<Hut> _huts = new();

    public IReadOnlyList<Hut> Huts => _huts;

    /// <summary>One physics frame of the village, nothing before the tribe.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        if (!IsActive)
        {
            return;
        }
        float size = Player.BodySize();
        Work(size);

        // The orchard: fruit also falls around the fire and stays there while the chieftain is away (Spawner, ContactResolver)
        _session.Orchard = (_pack.Nest.Position, size * Tune.GatherRange);

        // Choppers at their trunk fill the wood stock
        foreach (Kin kin in _pack.Kin)
        {
            if (kin.ChopTick(dt))
            {
                Economy.AddWood(Tune.WoodPerChop);
                EmitSignal(SignalName.Changed);
            }
        }

        // Fields of the city (Farming): every villager brings food without any fruit
        _farmFood += Villagers().Count * Bonuses.FarmFoodPerMinute * dt / SECONDS_PER_MINUTE;
        if (_farmFood >= 1f)
        {
            Economy.AddFood((int)_farmFood);
            _farmFood -= (int)_farmFood;
            EmitSignal(SignalName.Changed);
        }

        // The fire burns as high as the stock pays for a dwelling: a glimmer means gather, a roaring fire means build
        BuildingDesign dwelling = DesignOf(BuildingKind.Dwelling);
        float foodShare = (float)Economy.Food / Mathf.Max(dwelling.FoodCost, 1);
        float woodShare = (float)Economy.Wood / Mathf.Max(dwelling.WoodCost, 1);
        _pack.Nest.Glow = Mathf.Clamp(Mathf.Min(foodShare, woodShare), 0.2f, 1f);

        _huts.RemoveAll(hut => !IsInstanceValid(hut) || hut.IsQueuedForDeletion());
        foreach (Hut hut in _huts)
        {
            hut.GroundHeight = Coast.GroundHeight(hut.Position) * size;
            if (hut.Raise(dt))
            {
                _pack.SpawnVillager(hut.Position, isWarrior: hut.Design.Warriors > 0);
                EmitSignal(SignalName.Changed);
            }
        }
    }

    private const int HUT_TRIES = 12;
    private const float WALL_SHARE = 0.5f;           // share of BuildSpacing a new building keeps to the walls of a big one
    private const float SECONDS_PER_MINUTE = 60f;

    private float _farmFood;                         // food of the fields not yet whole
    private const int HUTS_PER_RING = 6;             // slots around the fire before the next ring starts
    private static readonly float HUT_ANGLE = Mathf.Tau / HUTS_PER_RING;

    /*
      A building may stand where all of this holds:
      > The tribe exists and the spot is on land.
      > It lies within BuildRadius of the fire, but not on the fire itself.
      > It keeps BuildSpacing to every standing building, and half of it to the walls of a big one.
    */
    /// <summary>Whether a building may stand at a spot: on land, within BuildRadius of the fire, BuildSpacing clear of the fire and of other buildings.</summary>
    /// <param name="spot">(Required) Plane point of its center.</param>
    public bool CanBuildAt(Vector2 spot)
    {
        if (!IsActive || !Coast.IsOnLand(spot))
        {
            return false;
        }
        float unit     = Coast.Unit;
        float fromFire = spot.DistanceTo(_pack.Nest.Position);
        if (fromFire > Tune.BuildRadius * unit || fromFire < Tune.BuildSpacing * unit)
        {
            return false;
        }
        foreach (Hut hut in _huts)
        {
            // Clear of its middle and of its walls: a big headquarters reaches further than the spacing
            float clear = Mathf.Max(Tune.BuildSpacing * unit, hut.BlockRadius + Tune.BuildSpacing * unit * WALL_SHARE);
            if (!hut.IsQueuedForDeletion() && hut.Position.DistanceTo(spot) < clear)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Pays a building from the stock and places it where the player chose.</summary>
    /// <param name="kind">(Required) The function, built from its blueprint (DesignOf).</param>
    /// <param name="spot">(Required) Plane point of its center, see CanBuildAt.</param>
    /// <param name="turn">(Optional) Turn around the vertical axis in radians. Defaults to a random one.</param>
    /// <returns>True when it was built: the spot is allowed and the stock pays for it.</returns>
    /// <example><code>if (tribe.CanBuildAt(spot)) tribe.TryBuildAt(BuildingKind.Market, spot);</code></example>
    public bool TryBuildAt(BuildingKind kind, Vector2 spot, float? turn = null)
    {
        BuildingDesign design = DesignOf(kind);
        if (!CanBuildAt(spot) || !Economy.BuildHut(design))
        {
            return false;
        }
        PutUp(design, spot, turn);
        return true;
    }

    /// <summary>The headquarters of the city where nest and fire stood: pays the hall from the stock, the fire gives way.</summary>
    /// <returns>True when it was built: a tribe without a hall whose stock pays for it.</returns>
    public bool TryBuildHall()
    {
        BuildingDesign design = DesignOf(BuildingKind.Hall);
        if (!IsActive || HasHall || !Economy.BuildHut(design))
        {
            return false;
        }
        float share = Hut.FitShare(design, Coast.Unit * Hut.SCALE, _pack.Nest.Position, _huts, Coast.Unit);     // squeezed between the buildings around the old fire
        PutUp(design, _pack.Nest.Position, turn: 0f, share);
        _pack.Nest.GiveWayToHall();
        return true;
    }

    /// <summary>The headquarters of the city, null while none stands.</summary>
    public Hut Hall => _huts.FirstOrDefault(hut => IsInstanceValid(hut) && BuildingKind.Hall == hut.Design.Kind);

    public bool HasHall => null != Hall;

    /// <summary>A paid building goes up: on a flat foundation at the height of its middle, where the preview stood.</summary>
    private Hut PutUp(BuildingDesign design, Vector2 spot, float? turn, float share = 1f)
    {
        var hut = new Hut { Position = spot, Size = Coast.Unit * Hut.SCALE * share, GroundHeight = Coast.GroundHeight(spot) * Player.BodySize(), Turn = turn };
        hut.Setup(design, faction: 0);
        Coast.AddPad(spot, hut.FoundationRadius);
        hut.Destroyed += () => EmitSignal(SignalName.Changed);
        hut.BlockOn(Coast);
        _huts.Add(hut);
        AddChild(hut);
        EmitSignal(SignalName.Changed);
        Log.Verbose("TribeCommander", $"{design.Kind} built at {spot} (building {Economy.Huts}), {Economy.Food} food and {Economy.Wood} wood left.");
        return hut;
    }

    /// <summary>Builds without a chosen spot: the next free slot on a ring HutDistance around the fire (tests, the graphics reel).</summary>
    /// <param name="kind">(Required) The function, built from its blueprint (DesignOf).</param>
    /// <returns>True when it was built.</returns>
    /// <example><code>if (!tribe.TryBuild(BuildingKind.Market)) ...   // not enough food or wood, or no free slot</code></example>
    public bool TryBuild(BuildingKind kind)
    {
        if (!IsActive || !Economy.CanAfford(DesignOf(kind)))
        {
            return false;
        }
        Vector2 fire = _pack.Nest.Position;
        for (int i = 0; i < HUT_TRIES; i++)
        {
            // Building n takes slot n around the fire, every HUTS_PER_RING a ring further out, a try skips to the next slot
            int     slot  = Economy.Huts + i;
            float   angle = HUT_ANGLE * slot + 0.5f * HUT_ANGLE * (slot / HUTS_PER_RING);
            Vector2 spot  = fire + Vector2.FromAngle(angle) * Tune.HutDistance * Coast.Unit * (1f + 0.6f * (slot / HUTS_PER_RING));
            if (TryBuildAt(kind, Coast.PushOutOfTrees(spot, Coast.Unit * Hut.SCALE)))
            {
                return true;
            }
        }
        return false;
    }

    // ===========================================================================
    // =================================   SAVE   ================================
    // ===========================================================================
    //
    //   Save      Path, stock, every blueprint and every standing building of the player.
    //   Restore   The tribe of a save: no costs, the foundations come back with the coast.
    //

    /// <summary>The tribe as plain save data (Core/Save/SaveGame.cs).</summary>
    public SaveTribe Save()
    {
        return new SaveTribe
        {
            Path    = Path.ToString(),
            Food    = Economy.Food,
            Wood    = Economy.Wood,
            Built   = Economy.Huts,
            Designs = System.Enum.GetValues<BuildingKind>().Select(kind => SaveData.Building(DesignOf(kind))).ToList(),
            Huts    = _huts.Where(IsInstanceValid).Select(hut => new SaveHut(SaveData.Building(hut.Design), SaveData.Of(hut.Position), hut.Turn ?? 0f)).ToList(),
        };
    }

    /// <summary>Takes over the tribe of a save, the pack (nest, villagers) and the coast are restored already.</summary>
    /// <param name="save">(Required) The saved tribe.</param>
    public void Restore(SaveTribe save)
    {
        Path = System.Enum.Parse<TribePath>(save.Path);
        Economy.Restore(save.Food, save.Wood, save.Built);
        foreach (SaveBuildingDesign design in save.Designs)
        {
            BuildingDesign loaded = SaveData.ToBuilding(design);
            DesignOf(loaded.Kind).CopyFrom(loaded);
        }
        // The headquarters last: it is squeezed between the others as when it was built
        foreach (SaveHut saved in save.Huts.OrderBy(saved => SaveData.ToBuilding(saved.Design).Kind == BuildingKind.Hall ? 1 : 0))
        {
            Vector2        spot   = SaveData.ToVector2(saved.Position);
            BuildingDesign design = SaveData.ToBuilding(saved.Design);
            float          share  = BuildingKind.Hall == design.Kind ? Hut.FitShare(design, Coast.Unit * Hut.SCALE, spot, _huts, Coast.Unit) : 1f;
            var            hut    = new Hut { Position = spot, Size = Coast.Unit * Hut.SCALE * share, GroundHeight = Coast.GroundHeight(spot) * Player.BodySize(), Turn = saved.Turn };
            hut.Setup(design, faction: 0);
            hut.BlockOn(Coast);
            hut.Destroyed += () => EmitSignal(SignalName.Changed);
            _huts.Add(hut);
            AddChild(hut);
        }
        if (HasHall)
        {
            _pack.Nest?.GiveWayToHall();    // the headquarters of a city stands on the old fire
        }
        EmitSignal(SignalName.Changed);
        Log.Verbose("TribeCommander", $"Tribe restored: {Path}, {_huts.Count} building(s), {Economy.Food} food, {Economy.Wood} wood.");
    }
}