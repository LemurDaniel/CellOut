// NOTE:
//   The low wall around a settlement (wish of the user 2026-11-17): field stones with
//   sharpened stakes on a ring a little inside the edge of the foundation
//   (Settlement.WallRadius), open at the gates (Settlement.GateAngles), where two posts
//   carry a crossbeam and a way leaves the settlement. It only marks the edge: low enough
//   to step over, it blocks nobody. The player's tribe and every neighbor tribe have one.
//   Models: assets/models/village/village.wall.glb and village.wallgate.glb
//   (tools/blender/models/village.py).
//
//   var wall = new SettlementWall();
//   wall.Setup(coast, fire);
//   AddChild(wall);
//   wall.Update(player.BodySize());   // every physics frame

//
//   Chapters:
//     WALL   The pieces on their ring and their place on the ground.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class SettlementWall : Node3D
{
    // ===========================================================================
    // =================================   WALL   ================================
    // ===========================================================================
    //
    //   Setup    The coast and the fire, before AddChild.
    //   _Ready   Builds the ring.
    //   Build    A gate at every gate angle, the arcs between closed with wall pieces.
    //   Update   Keeps every piece on the ground, pieces in the water stay hidden; once the
    //            ground of the settlement shows the streets of a city, the wall is rebuilt
    //            from the city models (tools/blender/models/modern_guard.py).
    //

    private const string WALL_MODEL = "res://assets/models/village/village.wall.glb";
    private const string GATE_MODEL = "res://assets/models/village/village.wallgate.glb";
    private const string CITY_WALL_MODEL = "res://assets/models/village/village.citywall.glb";
    private const string CITY_GATE_MODEL = "res://assets/models/village/village.citygate.glb";
    private const float PIECE_LENGTH = 2.6f;        // model units along the wall, WALL_LENGTH in tools/blender/models/village.py
    private static readonly Color WOOD_COLOR = new(0.72f, 0.62f, 0.4f);

    private readonly List<(Node3D Piece, Vector2 Spot)> _pieces = new();
    private Coast _coast;
    private Vector2 _fire;
    private bool _isCity;                            // built from the city models: low concrete wall with railing, gate pillars with lamps

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="coast">(Required) The coast: ground height and land.</param>
    /// <param name="fire">(Required) Plane point of the fire the wall runs around.</param>
    public void Setup(Coast coast, Vector2 fire)
    {
        _coast = coast;
        _fire  = fire;
    }

    /*
      The ring:
      - one gate piece at every gate angle,
      - between two gates as many wall pieces as fit, stretched along the wall so they close up exactly.
      A piece runs along its local Z, +X looks out of the settlement.
    */
    public override void _Ready()
    {
        TopLevel = true;
        Build();
    }

    /// <summary>Sets every piece of the ring anew, in stone and stakes or as the wall of a city.</summary>
    private void Build()
    {
        foreach ((Node3D piece, Vector2 _) in _pieces)
        {
            piece.QueueFree();
        }
        _pieces.Clear();
        string wall   = _isCity ? CITY_WALL_MODEL : WALL_MODEL;
        string opening = _isCity ? CITY_GATE_MODEL : GATE_MODEL;
        float   unit   = _coast.Unit;
        float   radius = Settlement.WallRadius(unit);
        float   scale  = unit * Hut.SCALE;
        float   gate   = PIECE_LENGTH * scale / radius;          // angle one piece covers
        float[] gates  = Settlement.GateAngles();
        for (int i = 0; i < gates.Length; i++)
        {
            Add(opening, gates[i], radius, scale, stretch: 1f);

            float from  = gates[i] + gate * 0.5f;
            float to    = Mathf.PosMod(gates[(i + 1) % gates.Length] - gates[i], Mathf.Tau) + gates[i] - gate * 0.5f;
            int   count = Mathf.Max(Mathf.RoundToInt((to - from) / gate), 1);
            float step  = (to - from) / count;
            for (int k = 0; k < count; k++)
            {
                Add(wall, from + step * (k + 0.5f), radius, scale, stretch: step / gate);
            }
        }
        Log.Verbose("SettlementWall", $"Wall of {_pieces.Count} piece(s) around {_fire} (city: {_isCity}).");
    }

    private void Add(string model, float angle, float radius, float scale, float stretch)
    {
        Node3D piece = PartModels.New(model, WOOD_COLOR);
        piece.Rotation = new Vector3(0f, -angle, 0f);            // a plane angle turns the other way round the upright axis
        piece.Scale    = new Vector3(scale, scale, scale * stretch);
        AddChild(piece);
        _pieces.Add((piece, _fire + Vector2.FromAngle(angle) * radius));
    }

    /// <summary>One physics frame: every piece sits on the ground, which scales with the player and drops with the planet.</summary>
    /// <param name="playerSize">(Required) Player.BodySize(), the ground height scales with it.</param>
    public void Update(float playerSize)
    {
        // The ground of the settlement turned into streets (or a save brought a city back): the wall follows
        bool isCity = CoastTerrain.PadStyle.City == _coast.PadStyleAt(_fire) && ResourceLoader.Exists(CITY_WALL_MODEL) && ResourceLoader.Exists(CITY_GATE_MODEL);
        if (isCity != _isCity)
        {
            _isCity = isCity;
            Build();
        }
        foreach ((Node3D piece, Vector2 spot) in _pieces)
        {
            piece.Visible        = _coast.IsOnLand(spot);
            piece.GlobalPosition = Plane3D.To3D(spot, _coast.GroundHeight(spot) * playerSize - Plane3D.Drop(spot));
        }
    }
}
