// NOTE:
//   The drawn forests of the coast (Creature/Coast.cs), a component of it: the trees of
//   World/TreeGrid.cs as generated primeval tree models (Core/TreeModels.cs), one
//   multimesh per species and chunk. Chunks around the player come and go.
//
//   var forest = new CoastForest();
//   forest.Setup(terrain, trees);
//   AddChild(forest);
//   forest.Follow(playerPosition, playerSize);   // every frame

//
//   Chapters:
//     FOLLOW   Adds chunks entering TREE_REACH, frees chunks leaving it.
//     CHUNKS   One node per chunk with a multimesh per species.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CoastForest : Node3D
{
    // ===========================================================================
    // ===============================   FOLLOW   ================================
    // ===========================================================================
    //
    //   Setup    (Required) terrain: ground height and the unit of the coast.
    //            (Required) trees: the seeded trees per grid cell.
    //   Follow   Chunks around the player come and go, all rebuild when the player grew.
    //

    private const int TREE_CHUNK = 6;                // cells per chunk side
    private const float TREE_REACH = 260f;           // chunks this far stay loaded, in Unit
    private const float TREE_RELOAD = 10f;           // chunks update after the player went this far, in Unit

    private readonly Dictionary<Vector2I, Node3D> _treeChunks = new();
    private CoastTerrain _terrain;
    private TreeGrid _trees;
    private Vector2 _treesCenter = new(float.NaN, float.NaN);
    private float _treesSize;

    /// <summary>Hands over the terrain and its trees, called once before AddChild.</summary>
    /// <param name="terrain">(Required) The terrain of the coast.</param>
    /// <param name="trees">(Required) The seeded trees of that terrain.</param>
    public void Setup(CoastTerrain terrain, TreeGrid trees)
    {
        _terrain = terrain;
        _trees   = trees;
    }

    /*
      > The player grew noticeably: every chunk rebuilds (the ground heights scale with the size)
      > The player went TREE_RELOAD Units: chunks within TREE_REACH come, the others go
    */
    /// <summary>Keeps the tree chunks around the player, called every frame.</summary>
    /// <param name="playerPosition">(Required) Plane position of the player.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), the ground heights scale with it.</param>
    public void Follow(Vector2 playerPosition, float playerSize)
    {
        float unit = _terrain.Unit;
        if (Mathf.Abs(playerSize / Mathf.Max(_treesSize, 0.001f) - 1f) > 0.05f)
        {
            foreach (Node3D chunk in _treeChunks.Values)
            {
                chunk.QueueFree();
            }
            _treeChunks.Clear();
            _treesSize   = playerSize;
            _treesCenter = new Vector2(float.NaN, float.NaN);
        }
        if (!float.IsNaN(_treesCenter.X) && playerPosition.DistanceTo(_treesCenter) < TREE_RELOAD * unit)
        {
            return;
        }
        _treesCenter = playerPosition;

        float chunkSize = TreeGrid.TREE_CELL * TREE_CHUNK * unit;
        float reach     = TREE_REACH * unit;
        var   wanted    = new HashSet<Vector2I>();
        int   x0        = Mathf.FloorToInt((playerPosition.X - reach) / chunkSize);
        int   x1        = Mathf.FloorToInt((playerPosition.X + reach) / chunkSize);
        int   y0        = Mathf.FloorToInt((playerPosition.Y - reach) / chunkSize);
        int   y1        = Mathf.FloorToInt((playerPosition.Y + reach) / chunkSize);
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                Vector2 middle = (new Vector2(x, y) + new Vector2(0.5f, 0.5f)) * chunkSize;
                if (middle.DistanceTo(playerPosition) < reach + chunkSize * 0.71f)
                {
                    wanted.Add(new Vector2I(x, y));
                }
            }
        }
        foreach (Vector2I gone in _treeChunks.Keys.Where(chunk => !wanted.Contains(chunk)).ToList())
        {
            _treeChunks[gone].QueueFree();
            _treeChunks.Remove(gone);
        }
        foreach (Vector2I chunk in wanted.Where(chunk => !_treeChunks.ContainsKey(chunk)).ToList())
        {
            _treeChunks[chunk] = BuildTreeChunk(chunk, playerSize);
        }
    }

    // ===========================================================================
    // ===============================   CHUNKS   ================================
    // ===========================================================================
    //
    //   TREE_VISIBLE     How far each species is drawn.
    //   BuildTreeChunk   One node per chunk with a multimesh per species.
    //

    private static readonly float[] TREE_VISIBLE = { 140f, 260f, 260f, 300f, 500f };  // per species, in Unit

    private Node3D BuildTreeChunk(Vector2I chunk, float playerSize)
    {
        var node       = new Node3D();
        var transforms = new List<Transform3D>[TreeModels.COUNT];
        var tints      = new List<Color>[TreeModels.COUNT];
        for (int i = 0; i < TreeModels.COUNT; i++)
        {
            transforms[i] = new List<Transform3D>();
            tints[i]      = new List<Color>();
        }

        // Every tree of the chunk, a little sunk into the ground so roots sit on slopes
        for (int x = 0; x < TREE_CHUNK; x++)
        {
            for (int y = 0; y < TREE_CHUNK; y++)
            {
                foreach (Coast.Tree tree in _trees.TreesIn(chunk * TREE_CHUNK + new Vector2I(x, y)))
                {
                    float ground = _terrain.GroundHeight(tree.Position) * playerSize - tree.Height * 0.03f;
                    Basis basis  = new Basis(Vector3.Up, tree.Yaw).Scaled(Vector3.One * tree.Height);
                    transforms[(int)tree.Species].Add(new Transform3D(basis, Plane3D.To3D(tree.Position, ground)));
                    tints[(int)tree.Species].Add(tree.Tint);
                }
            }
        }

        // One multimesh per species, culled per chunk, small trees vanish earlier
        for (int i = 0; i < TreeModels.COUNT; i++)
        {
            if (0 == transforms[i].Count)
            {
                continue;
            }
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors       = true,
                Mesh            = TreeModels.MeshOf((TreeSpecies)i),
                InstanceCount   = transforms[i].Count,
            };
            for (int k = 0; k < transforms[i].Count; k++)
            {
                multimesh.SetInstanceTransform(k, transforms[i][k]);
                multimesh.SetInstanceColor(k, tints[i][k]);
            }
            node.AddChild(new MultiMeshInstance3D { Multimesh = multimesh, VisibilityRangeEnd = TREE_VISIBLE[i] * _terrain.Unit });
        }
        AddChild(node);
        return node;
    }
}
