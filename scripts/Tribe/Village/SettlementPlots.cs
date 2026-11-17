// NOTE:
//   Shows the free building plots of the player's settlement: on every plot of the ground
//   plan (Settlement.cs) that is still free stands a staked out square (pegs and rope, in a
//   city cones and barrier tape: village.cityplot.glb,
//   assets/models/village/village.plot.glb), so the player sees where a building can go.
//   A plot that gets a building, or lies too close to the shore, shows nothing. It also
//   owns the low wall around the settlement (SettlementWall.cs). Society owns it, Update
//   runs after the tribe.
//
//   var plots = new SettlementPlots();
//   plots.Setup(session, tribe);
//   AddChild(plots);
//   plots.Update();

//
//   Chapters:
//     PLOTS   The markers and their update per physics frame.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class SettlementPlots : Node3D
{
    // ===========================================================================
    // ================================   PLOTS   ================================
    // ===========================================================================
    //
    //   Setup    The session and the tribe, before AddChild.
    //   Update   One marker per plot, shown while the plot is free, kept on the ground.
    //

    private const string PLOT_MODEL = "res://assets/models/village/village.plot.glb";
    private const string CITY_PLOT_MODEL = "res://assets/models/village/village.cityplot.glb";
    private static readonly Color ROPE_COLOR = new(0.72f, 0.62f, 0.4f);

    private readonly List<Node3D> _markers = new();
    private WorldSession _session;
    private TribeCommander _tribe;
    private SettlementWall _wall;                    // the low wall around the settlement, built with the first update of a tribe
    private bool _isCity;                            // the markers are the cones and tape of a city

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="tribe">(Required) The tribe: its plots and which are free.</param>
    public void Setup(WorldSession session, TribeCommander tribe)
    {
        _session = session;
        _tribe   = tribe;
    }

    public override void _Ready()
    {
        TopLevel = true;
    }

    /// <summary>One physics frame: every free plot shows its marker on the ground, scaled like the buildings.</summary>
    public void Update()
    {
        if (!_tribe.IsActive || null == _session.Coast)
        {
            _markers.ForEach(marker => marker.Visible = false);
            return;
        }
        Coast coast = _session.Coast;
        float size  = _session.Player.BodySize();
        if (null == _wall)
        {
            _wall = new SettlementWall();
            _wall.Setup(coast, _tribe.Fire);
            AddChild(_wall);
        }
        _wall.Update(size);
        // A city stakes its plots out with cones and barrier tape instead of pegs and rope
        bool isCity = _tribe.HasHall && ResourceLoader.Exists(CITY_PLOT_MODEL);
        if (isCity != _isCity)
        {
            _isCity = isCity;
            _markers.ForEach(marker => marker.QueueFree());
            _markers.Clear();
        }
        IReadOnlyList<SettlementSlot> slots = _tribe.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= _markers.Count)
            {
                Node3D marker = PartModels.New(_isCity ? CITY_PLOT_MODEL : PLOT_MODEL, ROPE_COLOR);
                AddChild(marker);
                _markers.Add(marker);
            }
            Vector2 spot = slots[i].Spot;
            _markers[i].Visible        = _tribe.IsFree(slots[i]);
            _markers[i].GlobalPosition = Plane3D.To3D(spot, coast.GroundHeight(spot) * size - Plane3D.Drop(spot));
            _markers[i].Rotation       = new Vector3(0f, slots[i].Turn, 0f);
            _markers[i].Scale          = Vector3.One * coast.Unit * Hut.SCALE;
        }
    }
}
