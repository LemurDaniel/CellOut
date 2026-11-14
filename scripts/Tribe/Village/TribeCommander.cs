// NOTE:
//   The strategy layer of the tribe: from the tribe stage on the player commands the
//   villagers (every Kin of the pack and the own body) from the strategy view of the
//   camera. Left click or a drag box selects, right click orders the selection: onto a
//   tree it chops wood, onto an enemy it attacks, onto an enemy building it tears it down, else it walks there. Idle villagers
//   gather the fruit nearby, gathered fruit and chopped wood fill the stock
//   (TribeEconomy), the stock pays for buildings next to the fire: one blueprint per
//   function (DesignOf, edited in the BuildingEditor), dwellings raise villagers, the
//   others help the village while they stand. Selection rings are its children in world space. It does not process on its own: Main calls Update after
//   the pack and routes the mouse to HandleInput.
//
//   var tribe = new TribeCommander();
//   tribe.Setup(session, pack, followCamera);
//   tribe.AttachRivals(rivals);              // right clicks on enemies
//   AddChild(tribe);
//   tribe.Update(dt);                       // in _PhysicsProcess
//   if (tribe.HandleInput(@event)) GetViewport().SetInputAsHandled();
//   tribe.TryBuildAt(BuildingKind.Dwelling, spot);   // the BuildingPlacer, where the player clicked

//
//   Chapters:
//     SETTINGS   The session, the pack, the camera, the rivals, the stock and the blueprints.
//     SELECT     Click and drag box select units, right click orders them.
//     VILLAGE    Gathering, chopping, buildings and the villagers they raise, per physics frame.
//

using System.Collections.Generic;
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
    //   Defense         Defense of the village: the standing watchtowers.
    //   TradeBonus      Share more every trader brings: the standing markets.
    //   RelationBonus   Extra relation per trade: the standing totems.
    //   Setup           The session, the pack and the camera, before AddChild.
    //   AttachRivals    The rival tribes, so a right click on an enemy orders an attack.
    //   OnKinAte        A villager ate land food: gathered for the stock, more with a storehouse.
    //

    [Signal] public delegate void ChangedEventHandler();

    /// <summary>Hut distance, gathering range, chopping and food per fruit from data/balance.json (tribe.village).</summary>
    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    public TribeEconomy Economy { get; } = new();

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
    private FollowCamera _camera;
    private RivalKeeper _rivals;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;
    private Node2D FoodLayer => _session.FoodLayer;

    public bool IsActive => Player.IsTribal && null != _pack.Nest;

    public float Defense => Sum(hut => hut.Design.Defense);

    public float TradeBonus => Sum(hut => hut.Design.TradeBonus);

    public float RelationBonus => Sum(hut => hut.Design.RelationBonus);

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, food layer and coast.</param>
    /// <param name="pack">(Required) The pack whose kin are the villagers.</param>
    /// <param name="camera">(Required) The camera that projects the units for the drag box.</param>
    public void Setup(WorldSession session, PackKeeper pack, FollowCamera camera)
    {
        _session = session;
        _pack    = pack;
        _camera  = camera;
        _pack.KinAte += OnKinAte;
    }

    /// <summary>The rival tribes, a right click on one of their kin orders an attack.</summary>
    /// <param name="rivals">(Required) The rival keeper.</param>
    public void AttachRivals(RivalKeeper rivals)
    {
        _rivals = rivals;
    }

    public override void _Ready()
    {
        TopLevel = true;
    }

    /// <summary>Gathered: every piece a villager eats fills the stock from the tribe on, storehouses add to it.</summary>
    private void OnKinAte(Vector2 position, float size, Color color)
    {
        if (!IsActive)
        {
            return;
        }
        Economy.AddFood(Tune.FoodPerFruit + (int)Sum(hut => hut.Design.FoodPerFruit));
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
    // ===============================   SELECT   ================================
    // ===========================================================================
    //
    //   Selected      The commanded units right now.
    //   Units         Every villager and the player.
    //   HandleInput   Left press starts a box, release selects (click: the nearest unit), right click orders, Tab selects all.
    //   SelectAt      The unit under the cursor, Shift adds to the selection.
    //   SelectBox     Every unit whose screen position lies in the box.
    //   Order         The selection attacks an enemy, razes an enemy building, chops a tree or walks to the point (spread on a ring).
    //   TreeAt        The tree whose trunk lies under a plane point, null when none.
    //   DrawRings     A ring under every selected unit and a white one under the player, once per frame.
    //

    private const float PICK_RADIUS = 1.6f;          // body sizes around a unit a click still hits
    private const float TREE_PICK = 2.5f;            // trunk radii around a trunk a click still hits
    private const float BOX_MIN = 6f;                // pixels: smaller drags count as a click
    private const float ORDER_SPREAD = 1.2f;         // body sizes between ordered units
    private const float RING_LIFT = 0.08f;           // ring height above the ground in body sizes
    private static readonly Color RING_COLOR = new(0.95f, 0.9f, 0.4f);
    private static readonly Color PLAYER_RING_COLOR = new(1f, 1f, 1f);

    private readonly List<CellBody> _selected = new();
    private readonly Dictionary<CellBody, MeshInstance3D> _rings = new();
    private MeshInstance3D _playerRing;
    private Vector2 _boxStart;
    private bool _isBoxing;

    public IReadOnlyList<CellBody> Selected => _selected;

    /// <summary>Every villager and the player.</summary>
    public List<CellBody> Units()
    {
        var units = new List<CellBody> { Player };
        foreach (Kin kin in _pack.Kin)
        {
            if (KinMode.Villager == kin.Mode && !kin.IsQueuedForDeletion())
            {
                units.Add(kin);
            }
        }
        return units;
    }

    /// <summary>Takes the mouse part of an input event from the tribe on, Tab selects every unit.</summary>
    /// <param name="event">(Required) The event Main received in _UnhandledInput.</param>
    /// <returns>True when the tribe used the event.</returns>
    public bool HandleInput(InputEvent @event)
    {
        if (!IsActive)
        {
            return false;
        }
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab })
        {
            _selected.Clear();
            _selected.AddRange(Units());
            Log.Verbose("TribeCommander", $"Tab: all {_selected.Count} unit(s) selected.");
            return true;
        }
        if (@event is not InputEventMouseButton button)
        {
            return false;
        }
        Vector2 mouse = GetViewport().GetMousePosition();
        if (MouseButton.Left == button.ButtonIndex && button.Pressed)
        {
            _boxStart = mouse;
            _isBoxing = true;
            return true;
        }
        if (MouseButton.Left == button.ButtonIndex && !button.Pressed && _isBoxing)
        {
            _isBoxing = false;
            bool isAdding = button.ShiftPressed;
            if (mouse.DistanceTo(_boxStart) < BOX_MIN)
            {
                SelectAt(Plane3D.MouseOnPlane(GetViewport(), Player.GlobalPosition), isAdding);
            }
            else
            {
                SelectBox(new Rect2(_boxStart, Vector2.Zero).Expand(mouse), isAdding);
            }
            return true;
        }
        if (MouseButton.Right == button.ButtonIndex && button.Pressed)
        {
            Order(Plane3D.MouseOnPlane(GetViewport(), Player.GlobalPosition));
            return true;
        }
        return false;
    }

    /// <summary>Selects the unit nearest to a plane point within its pick radius, nothing clears the selection.</summary>
    /// <param name="point">(Required) Plane point under the cursor.</param>
    /// <param name="isAdding">(Required) Shift held: adds to the selection instead of replacing it.</param>
    public void SelectAt(Vector2 point, bool isAdding)
    {
        CellBody hit      = null;
        float    distance = float.PositiveInfinity;
        foreach (CellBody unit in Units())
        {
            float d = unit.GlobalPosition.DistanceTo(point);
            if (d < unit.BodySize() * PICK_RADIUS && d < distance)
            {
                hit      = unit;
                distance = d;
            }
        }
        if (!isAdding)
        {
            _selected.Clear();
        }
        if (null != hit && !_selected.Contains(hit))
        {
            _selected.Add(hit);
        }
        Log.Verbose("TribeCommander", $"Selected {_selected.Count} unit(s).");
    }

    /// <summary>Selects every unit whose projected position lies inside a screen box.</summary>
    /// <param name="box">(Required) Screen rectangle of the drag.</param>
    /// <param name="isAdding">(Required) Shift held: adds to the selection.</param>
    public void SelectBox(Rect2 box, bool isAdding)
    {
        if (!isAdding)
        {
            _selected.Clear();
        }
        Camera3D camera = _camera.Camera;
        foreach (CellBody unit in Units())
        {
            Vector3 world = Plane3D.To3D(unit.GlobalPosition, unit.GroundLift - Plane3D.Drop(unit.GlobalPosition));
            if (!camera.IsPositionBehind(world) && box.HasPoint(camera.UnprojectPosition(world)) && !_selected.Contains(unit))
            {
                _selected.Add(unit);
            }
        }
        Log.Verbose("TribeCommander", $"Box selected {_selected.Count} unit(s).");
    }

    /*
      A right click on the plane, for the selection:
      > An enemy kin under the cursor: every selected kin attacks it (the player walks there).
      > An enemy building under the cursor: every selected kin tears it down.
      > A trunk under the cursor: every selected kin chops it.
      > Otherwise: everyone walks there, several units spread on a ring, never into the sea.
    */
    /// <summary>Orders the selection: attack, chop or walk.</summary>
    /// <param name="point">(Required) Plane point under the cursor.</param>
    public void Order(Vector2 point)
    {
        _selected.RemoveAll(unit => !IsInstanceValid(unit) || unit.IsQueuedForDeletion());
        if (0 == _selected.Count)
        {
            return;
        }
        float size  = Player.BodySize();
        Kin   enemy = _rivals?.EnemyAt(point, size * PICK_RADIUS);
        if (null != enemy)
        {
            foreach (CellBody unit in _selected)
            {
                if (unit is Kin kin)
                {
                    kin.OrderAttack(enemy);
                }
                else
                {
                    unit.OrderTarget = enemy.Position;
                }
            }
            Log.Verbose("TribeCommander", $"{_selected.Count} unit(s) attack a kin of faction {enemy.Faction}.");
            return;
        }
        if (_rivals?.HutAt(point) is Hut building)
        {
            foreach (CellBody unit in _selected)
            {
                if (unit is Kin kin)
                {
                    kin.OrderRaze(building);
                }
                else
                {
                    unit.OrderTarget = building.Position;
                }
            }
            Log.Verbose("TribeCommander", $"{_selected.Count} unit(s) raze a {building.Design.Kind} of faction {building.Faction}.");
            return;
        }
        if (TreeAt(point) is Coast.Tree tree)
        {
            foreach (CellBody unit in _selected)
            {
                if (unit is Kin kin)
                {
                    kin.OrderChop(tree.Position, tree.TrunkRadius);
                }
            }
            Log.Verbose("TribeCommander", $"{_selected.Count} unit(s) chop a {tree.Species}.");
            return;
        }
        for (int i = 0; i < _selected.Count; i++)
        {
            float   spread = 1 == _selected.Count ? 0f : _selected[i].BodySize() * ORDER_SPREAD;
            Vector2 target = point + Vector2.FromAngle(Mathf.Tau * i / _selected.Count) * spread;
            target = null != Coast && !Coast.IsOnLand(target) ? point : target;
            if (_selected[i] is Kin kin)
            {
                kin.OrderMove(target);
            }
            else
            {
                _selected[i].OrderTarget = target;
            }
        }
        Log.Verbose("TribeCommander", $"{_selected.Count} unit(s) ordered to {point}.");
    }

    /// <summary>The tree whose trunk lies under a plane point (within TREE_PICK trunk radii), null when none.</summary>
    private Coast.Tree? TreeAt(Vector2 point)
    {
        if (null == Coast)
        {
            return null;
        }
        float cell   = TreeGrid.TREE_CELL * Coast.Unit;
        var   center = new Vector2I(Mathf.FloorToInt(point.X / cell), Mathf.FloorToInt(point.Y / cell));
        Coast.Tree? hit      = null;
        float       distance = float.PositiveInfinity;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                foreach (Coast.Tree tree in Coast.TreesIn(center + new Vector2I(x, y)))
                {
                    float d = tree.Position.DistanceTo(point);
                    if (d < tree.TrunkRadius * TREE_PICK && d < distance)
                    {
                        hit      = tree;
                        distance = d;
                    }
                }
            }
        }
        return hit;
    }

    /// <summary>A ring under every selected unit, rings of unselected units go, the player always carries a white one.</summary>
    private void DrawRings(float playerSize)
    {
        // The player: from above, among same colored kin, it needs a marker of its own
        if (null == _playerRing)
        {
            _playerRing = CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), CellVisual3D.Material(PLAYER_RING_COLOR, 1.2f, 0.5f), Vector3.Zero, Vector3.One);
            _playerRing.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        float playerGround = null == Coast ? 0f : Coast.GroundHeight(Player.GlobalPosition) * playerSize;
        _playerRing.GlobalPosition = Plane3D.To3D(Player.GlobalPosition, playerGround + playerSize * RING_LIFT * 1.5f - Plane3D.Drop(Player.GlobalPosition));
        _playerRing.Scale          = new Vector3(playerSize * 1.1f, playerSize * 0.1f, playerSize * 1.1f);

        foreach (CellBody unit in new List<CellBody>(_rings.Keys))
        {
            if (!_selected.Contains(unit) || !IsInstanceValid(unit))
            {
                _rings[unit].QueueFree();
                _rings.Remove(unit);
            }
        }
        foreach (CellBody unit in _selected)
        {
            if (!IsInstanceValid(unit))
            {
                continue;
            }
            if (!_rings.TryGetValue(unit, out MeshInstance3D ring))
            {
                ring = CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), CellVisual3D.Material(RING_COLOR, 0.8f, 0.5f), Vector3.Zero, Vector3.One);
                ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _rings[unit] = ring;
            }
            float size   = unit.BodySize();
            float ground = null == Coast ? 0f : Coast.GroundHeight(unit.GlobalPosition) * playerSize;
            ring.GlobalPosition = Plane3D.To3D(unit.GlobalPosition, ground + size * RING_LIFT - Plane3D.Drop(unit.GlobalPosition));
            ring.Scale          = new Vector3(size * 1.3f, size * 0.15f, size * 1.3f);
        }
    }

    // ===========================================================================
    // ===============================   VILLAGE   ===============================
    // ===========================================================================
    //
    //   Huts          Every standing building of the player's tribe.
    //   Update        Idle villagers gather, choppers chop, huts raise villagers and stay on the
    //                 ground, the fire shows the stock, rings follow.
    //   Gather        An idle villager walks to the nearest fruit within GatherRange.
    //   CanBuildAt    Whether a building may stand at a spot.
    //   TryBuildAt    Pays a building of a function and places it where the player chose.
    //   TryBuild      The same without a chosen spot: the next free slot around the fire.
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
        Gather(size);

        // Choppers at their trunk fill the wood stock
        foreach (Kin kin in _pack.Kin)
        {
            if (kin.ChopTick(dt))
            {
                Economy.AddWood(Tune.WoodPerChop);
                EmitSignal(SignalName.Changed);
            }
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
        DrawRings(size);
    }

    /// <summary>Every idle villager walks to the nearest fruit within GatherRange, the pack lets it eat on touch.</summary>
    private void Gather(float size)
    {
        foreach (Kin kin in _pack.Kin)
        {
            if (KinMode.Villager != kin.Mode || !kin.IsIdle)
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
            }
        }
    }

    private const int HUT_TRIES = 12;
    private const int HUTS_PER_RING = 6;             // slots around the fire before the next ring starts
    private static readonly float HUT_ANGLE = Mathf.Tau / HUTS_PER_RING;

    /*
      A building may stand where all of this holds:
      > The tribe exists and the spot is on land.
      > It lies within BuildRadius of the fire, but not on the fire itself.
      > It keeps BuildSpacing to every standing building.
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
            if (!hut.IsQueuedForDeletion() && hut.Position.DistanceTo(spot) < Tune.BuildSpacing * unit)
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
        var hut = new Hut { Position = spot, Size = Coast.Unit * Hut.SCALE, GroundHeight = Coast.GroundHeight(spot) * Player.BodySize(), Turn = turn };
        hut.Setup(design, faction: 0);
        hut.Destroyed += () => EmitSignal(SignalName.Changed);
        _huts.Add(hut);
        AddChild(hut);
        EmitSignal(SignalName.Changed);
        Log.Verbose("TribeCommander", $"{kind} built at {spot} (building {Economy.Huts}), {Economy.Food} food and {Economy.Wood} wood left.");
        return true;
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
}