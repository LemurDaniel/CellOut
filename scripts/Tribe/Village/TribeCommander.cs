// NOTE:
//   The strategy layer of the tribe: from the tribe stage on the player commands the
//   villagers (every Kin of the pack and the own body) from the strategy view of the
//   camera. Left click or a drag box selects, right click orders the selection to a point.
//   Idle villagers gather the fruit nearby, gathered fruit fills the stock
//   (TribeEconomy), the stock pays for huts next to the fire, and every hut raises a
//   villager. Selection rings are its children in world space. It does not process on
//   its own: Main calls Update after the pack and routes the mouse to HandleInput.
//
//   var tribe = new TribeCommander();
//   tribe.Setup(session, pack, followCamera);
//   AddChild(tribe);
//   tribe.Update(dt);                       // in _PhysicsProcess
//   if (tribe.HandleInput(@event)) GetViewport().SetInputAsHandled();
//   tribe.TryBuildHut();                    // HUD button

//
//   Chapters:
//     SETTINGS   The session, the pack, the camera and the stock.
//     SELECT     Click and drag box select units, right click orders them.
//     VILLAGE    Gathering, huts and the villagers they raise, per physics frame.
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
    //   Changed    Stock or huts changed, the panel refreshes.
    //   Economy    Food and huts of the tribe (TribeEconomy).
    //   IsActive   From the tribe stage on, with a fire to build around.
    //   Setup      The session, the pack and the camera, before AddChild.
    //   OnKinAte   A villager ate land food: gathered for the stock.
    //

    [Signal] public delegate void ChangedEventHandler();

    /// <summary>Hut distance, gathering range and food per fruit from data/balance.json (tribe.village).</summary>
    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    public TribeEconomy Economy { get; } = new();

    private WorldSession _session;
    private PackKeeper _pack;
    private FollowCamera _camera;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;
    private Node2D FoodLayer => _session.FoodLayer;

    public bool IsActive => Player.IsTribal && null != _pack.Nest;

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

    public override void _Ready()
    {
        TopLevel = true;
    }

    /// <summary>Gathered: every piece a villager eats fills the stock from the tribe on.</summary>
    private void OnKinAte(Vector2 position, float size, Color color)
    {
        if (!IsActive)
        {
            return;
        }
        Economy.AddFood(Tune.FoodPerFruit);
        EmitSignal(SignalName.Changed);
    }

    // ===========================================================================
    // ===============================   SELECT   ================================
    // ===========================================================================
    //
    //   Selected      The commanded units right now.
    //   Units         Every villager and the player.
    //   HandleInput   Left press starts a box, release selects (click: the nearest unit), right click orders.
    //   SelectAt      The unit under the cursor, Shift adds to the selection.
    //   SelectBox     Every unit whose screen position lies in the box.
    //   Order         The selection walks to the point, spread on a small ring.
    //   DrawRings     A ring under every selected unit, once per frame.
    //

    private const float PICK_RADIUS = 1.6f;          // body sizes around a unit a click still hits
    private const float BOX_MIN = 6f;                // pixels: smaller drags count as a click
    private const float ORDER_SPREAD = 1.2f;         // body sizes between ordered units
    private const float RING_LIFT = 0.08f;           // ring height above the ground in body sizes
    private static readonly Color RING_COLOR = new(0.95f, 0.9f, 0.4f);

    private readonly List<CellBody> _selected = new();
    private readonly Dictionary<CellBody, MeshInstance3D> _rings = new();
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

    /// <summary>Takes the mouse part of an input event from the tribe on.</summary>
    /// <param name="event">(Required) The event Main received in _UnhandledInput.</param>
    /// <returns>True when the tribe used the event.</returns>
    public bool HandleInput(InputEvent @event)
    {
        if (!IsActive || @event is not InputEventMouseButton button)
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

    /// <summary>Orders the selection to a plane point, several units spread on a ring around it.</summary>
    /// <param name="point">(Required) Plane point to walk to.</param>
    public void Order(Vector2 point)
    {
        _selected.RemoveAll(unit => !IsInstanceValid(unit) || unit.IsQueuedForDeletion());
        for (int i = 0; i < _selected.Count; i++)
        {
            float   spread = 1 == _selected.Count ? 0f : _selected[i].BodySize() * ORDER_SPREAD;
            Vector2 target = point + Vector2.FromAngle(Mathf.Tau * i / _selected.Count) * spread;
            _selected[i].OrderTarget = null != Coast && !Coast.IsOnLand(target) ? point : target;
        }
        Log.Verbose("TribeCommander", $"{_selected.Count} unit(s) ordered to {point}.");
    }

    /// <summary>A ring under every selected unit, rings of unselected units go.</summary>
    private void DrawRings(float playerSize)
    {
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
    //   Huts          Every hut built so far.
    //   Update        Idle villagers gather, huts raise villagers and stay on the ground, rings follow.
    //   Gather        An idle villager walks to the nearest fruit within GatherRange.
    //   TryBuildHut   Pays a hut and places it next to the fire on land.
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
        foreach (Hut hut in _huts)
        {
            hut.GroundHeight = Coast.GroundHeight(hut.Position) * size;
            if (hut.Raise(dt))
            {
                _pack.SpawnVillager(hut.Position);
                EmitSignal(SignalName.Changed);
            }
        }
        DrawRings(size);
    }

    /// <summary>Every villager without an order walks to the nearest fruit within GatherRange, the pack lets it eat on touch.</summary>
    private void Gather(float size)
    {
        foreach (Kin kin in _pack.Kin)
        {
            if (KinMode.Villager != kin.Mode || null != kin.OrderTarget)
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
                kin.OrderTarget = nearest.Position;
            }
        }
    }

    private const int HUT_TRIES = 12;
    private const int HUTS_PER_RING = 6;             // huts around the fire before the next ring starts
    private static readonly float HUT_ANGLE = Mathf.Tau / HUTS_PER_RING;

    /// <summary>Pays a hut from the stock and places it HutDistance from the fire, on land and clear of trunks.</summary>
    /// <returns>True when a hut was built.</returns>
    /// <example><code>if (!tribe.TryBuildHut()) ...   // not enough food</code></example>
    public bool TryBuildHut()
    {
        if (!IsActive || !Economy.CanBuildHut)
        {
            return false;
        }
        float   unit = Coast.Unit;
        Vector2 fire = _pack.Nest.Position;
        for (int i = 0; i < HUT_TRIES; i++)
        {
            // Hut n takes slot n around the fire, every HUTS_PER_RING a ring further out, a try skips to the next slot
            int     slot  = _huts.Count + i;
            float   angle = HUT_ANGLE * slot + 0.5f * HUT_ANGLE * (slot / HUTS_PER_RING);
            Vector2 spot  = fire + Vector2.FromAngle(angle) * Tune.HutDistance * unit * (1f + 0.6f * (slot / HUTS_PER_RING));
            if (!Coast.IsOnLand(spot))
            {
                continue;
            }
            spot = Coast.PushOutOfTrees(spot, unit * 1.5f);
            Economy.BuildHut();
            var hut = new Hut { Position = spot, Size = unit, GroundHeight = Coast.GroundHeight(spot) * Player.BodySize() };
            _huts.Add(hut);
            AddChild(hut);
            EmitSignal(SignalName.Changed);
            Log.Verbose("TribeCommander", $"Hut {_huts.Count} built, {Economy.Food} food left.");
            return true;
        }
        return false;
    }
}
