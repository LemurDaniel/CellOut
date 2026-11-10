// NOTE:
//   Where the primeval trees of the coast (Creature/Coast.cs) stand: every grid cell on
//   land is seeded by its coordinates, so it holds the same trees every time it is asked.
//   Forests with clearings come from a broad noise, cycads stand on the dunes. Also the
//   collision with the trunks. No node: the Coast creates it, World/CoastForest.cs draws
//   its trees in chunks.
//
//   var grid = new TreeGrid(terrain);
//   foreach (Coast.Tree tree in grid.TreesIn(cell)) { ... }
//   player.Position = grid.PushOutOfTrees(player.Position, radius);

//
//   Chapters:
//     TREES   Seeded primeval trees in forests and clearings, trunks block the way.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public sealed class TreeGrid
{
    // ===========================================================================
    // ================================   TREES   ================================
    // ===========================================================================
    //
    //   TREE_CELL        Side of a grid cell, in Unit.
    //   TreeGrid         (Required) terrain: the islands and the unit the cells scale with.
    //   TreesIn          The trees of one grid cell, the same every time (seeded by the cell).
    //   PushOutOfTrees   A point moved out of every trunk it overlaps.
    //
    //   Heights in Unit (player sizes when the coast appeared), the ranges below times NATURE_SCALE:
    //     young 2..4, normal 5..10, giants 15..25 (scale trees, only deep in forests)
    //     -> about 6..75 body sizes, the animal walks below them like a dog in a forest
    //   Bands (d = LandDepth in Unit):
    //     2.5 .. 10   dunes: now and then a cycad
    //     10 ..       forests (broad noise) with clearings between them: broadleaf, giant,
    //                 horsetail, tree ferns, a fern undergrowth; single trees in the clearings
    //

    public const float TREE_CELL = 16f;              // in Unit

    private readonly FastNoiseLite _forest = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.018f, Seed = 23 };
    private readonly RandomNumberGenerator _treeRng = new();
    private readonly CoastTerrain _terrain;

    /// <summary>The trees of a terrain.</summary>
    /// <param name="terrain">(Required) The islands and the unit of the coast.</param>
    public TreeGrid(CoastTerrain terrain)
    {
        _terrain = terrain;
    }

    /*
      > Dunes: 22% for a cycad
      > Deeper inland: the forest noise sets the chance of a tree (8% in a clearing, up to
        90% deep in a forest), then the species:
          giant 8% (only deep in a forest), broadleaf 45%, horsetail 22%, tree fern 25%
        and in a forest 40% for a young fern of the undergrowth next to it
    */
    /// <summary>The trees of one grid cell: none, one or two.</summary>
    /// <param name="cell">(Required) Grid cell of TREE_CELL Units.</param>
    public List<Coast.Tree> TreesIn(Vector2I cell)
    {
        float unit  = _terrain.Unit;
        var   trees = new List<Coast.Tree>(2);
        _treeRng.Seed = (ulong)(uint)System.HashCode.Combine(cell.X, cell.Y, 57);
        Vector2 point = (new Vector2(cell.X, cell.Y) + new Vector2(_treeRng.RandfRange(0.15f, 0.85f), _treeRng.RandfRange(0.15f, 0.85f))) * TREE_CELL * unit;
        float   roll  = _treeRng.Randf();
        float   d     = _terrain.LandDepth(point) / unit;
        if (d < 2.5f)
        {
            return trees;
        }

        // Dunes: 22% for a cycad
        if (d < 10f)
        {
            if (roll < 0.22f)
            {
                trees.Add(NewTree(point, TreeSpecies.Cycad, _treeRng.RandfRange(4f, 8f)));
            }
            return trees;
        }

        // Deeper inland: forests and clearings
        Vector2 q      = point / unit;
        float   forest = Mathf.SmoothStep(-0.15f, 0.35f, _forest.GetNoise2D(q.X, q.Y));
        if (roll > 0.08f + 0.82f * forest)
        {
            return trees;
        }
        float species = _treeRng.Randf();
        bool  isYoung = _treeRng.Randf() < 0.3f;
        if (forest > 0.6f && species < 0.08f)
        {
            trees.Add(NewTree(point, TreeSpecies.Giant, _treeRng.RandfRange(15f, 25f)));
        }
        else if (species < 0.53f)
        {
            trees.Add(NewTree(point, TreeSpecies.Broadleaf, isYoung ? _treeRng.RandfRange(2f, 4f) : _treeRng.RandfRange(5f, 10f)));
        }
        else if (species < 0.75f)
        {
            trees.Add(NewTree(point, TreeSpecies.Horsetail, isYoung ? _treeRng.RandfRange(2.5f, 4f) : _treeRng.RandfRange(5f, 9f)));
        }
        else
        {
            trees.Add(NewTree(point, TreeSpecies.Fern, _treeRng.RandfRange(2f, 5f)));
        }

        // Undergrowth: a young fern next to it
        if (forest > 0.4f && _treeRng.Randf() < 0.4f)
        {
            Vector2 next = point + Vector2.FromAngle(_treeRng.Randf() * Mathf.Tau) * _treeRng.RandfRange(2f, 3f) * CoastTerrain.NATURE_SCALE * unit;
            if (_terrain.IsOnLand(next))
            {
                trees.Add(NewTree(next, TreeSpecies.Fern, _treeRng.RandfRange(1.5f, 3f)));
            }
        }
        return trees;
    }

    /// <summary>A tree with a random yaw and a slightly varied tint, base height in Unit (times NATURE_SCALE).</summary>
    private Coast.Tree NewTree(Vector2 point, TreeSpecies species, float height)
    {
        float light = _treeRng.RandfRange(0.85f, 1.1f);
        var   tint  = new Color(light * _treeRng.RandfRange(0.95f, 1.08f), light, light * _treeRng.RandfRange(0.9f, 1.02f));
        return new Coast.Tree(point, species, height * CoastTerrain.NATURE_SCALE * _terrain.Unit, _treeRng.Randf() * Mathf.Tau, tint);
    }

    /// <summary>A point pushed out of every trunk it overlaps, e.g. the walking player.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="radius">(Required) Radius of the body at the point.</param>
    /// <returns>The point itself when it touches no trunk.</returns>
    public Vector2 PushOutOfTrees(Vector2 point, float radius)
    {
        float unit   = _terrain.Unit;
        var   center = new Vector2I(Mathf.FloorToInt(point.X / (TREE_CELL * unit)), Mathf.FloorToInt(point.Y / (TREE_CELL * unit)));
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                foreach (Coast.Tree tree in TreesIn(center + new Vector2I(x, y)))
                {
                    Vector2 away  = point - tree.Position;
                    float   reach = tree.TrunkRadius + radius;
                    if (away.LengthSquared() >= reach * reach)
                    {
                        continue;
                    }
                    Vector2 outward = away.LengthSquared() > 0.0001f ? away.Normalized() : Vector2.Right;
                    point = tree.Position + outward * reach;
                }
            }
        }
        return point;
    }
}
