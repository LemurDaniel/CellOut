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
    //   _Ready   Builds a gate at every gate angle and closes the arcs between with wall pieces.
    //   Update   Keeps every piece on the ground, pieces in the water stay hidden.
    //

    private const string WALL_MODEL = "res://assets/models/village/village.wall.glb";
    private const string GATE_MODEL = "res://assets/models/village/village.wallgate.glb";
    private const float PIECE_LENGTH = 2.6f;         // model units along the wall, WALL_LENGTH in tools/blender/models/village.py
    private static readonly Color WOOD_COLOR = new(0.72f, 0.62f, 0.4f);

    private readonly List<(Node3D Piece, Vector2 Spot)> _pieces = new();
    private Coast _coast;
    private Vector2 _fire;

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
        float   unit   = _coast.Unit;
        float   radius = Settlement.WallRadius(unit);
        float   scale  = unit * Hut.SCALE;
        float   gate   = PIECE_LENGTH * scale / radius;          // angle one piece covers
        float[] gates  = Settlement.GateAngles();
        for (int i = 0; i < gates.Length; i++)
        {
            Add(GATE_MODEL, gates[i], radius, scale, stretch: 1f);

            float from  = gates[i] + gate * 0.5f;
            float to    = Mathf.PosMod(gates[(i + 1) % gates.Length] - gates[i], Mathf.Tau) + gates[i] - gate * 0.5f;
            int   count = Mathf.Max(Mathf.RoundToInt((to - from) / gate), 1);
            float step  = (to - from) / count;
            for (int k = 0; k < count; k++)
            {
                Add(WALL_MODEL, from + step * (k + 0.5f), radius, scale, stretch: step / gate);
            }
        }
        Log.Verbose("SettlementWall", $"Wall of {_pieces.Count} piece(s) around {_fire}.");
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
        foreach ((Node3D piece, Vector2 spot) in _pieces)
        {
            piece.Visible        = _coast.IsOnLand(spot);
            piece.GlobalPosition = Plane3D.To3D(spot, _coast.GroundHeight(spot) * playerSize - Plane3D.Drop(spot));
        }
    }
}
