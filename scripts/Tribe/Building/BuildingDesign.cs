// NOTE:
//   The blueprint of one village building as plain rules: a function (BuildingKind) and
//   blocks on a grid. The function alone decides what the building costs and does
//   (data/balance.json, tribe.buildings), the blocks are its look and free of charge.
//   Blocks drop from above: a block lands on the ground or on the highest block below
//   its footprint, so everything stacks and nothing floats; removing a block lets the
//   ones above fall. Pure logic without the engine: the BuildingEditor edits it, the
//   TribeCommander builds from it, BuildingVisual3D shows it, tests/unit checks it.
//
//   var design = new BuildingDesign(BuildingKind.Dwelling);
//   design.TryPlace(BlockKind.RoundRoom, x: 2, z: 2);
//   design.TryPlace(BlockKind.ConeRoof, x: 2, z: 2);     // lands on the room
//   int food = design.FoodCost;                          // of the function, not of the blocks

//
//   Chapters:
//     TYPES     Building functions and a placed block.
//     BLOCKS    The grid, dropping, removing and settling.
//     EFFECTS   Costs and effects of the function.
//

using System.Collections.Generic;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   BuildingKind   The function: dwelling (villagers), storehouse (food per fruit), market
//                  (trade), totem (relation), watchtower (defense), barracks (the warrior hut: warriors).
//   PlacedBlock    A block on the grid: the cell of its lowest corner, quarter turns, paint and pattern.
//

public enum BuildingKind { Dwelling, Storehouse, Market, Totem, Watchtower, Barracks }

public readonly record struct PlacedBlock(BlockKind Kind, int X, int Y, int Z, int Turns, int Paint, int Pattern = 0);

public sealed class BuildingDesign
{
    // ===========================================================================
    // ===============================   BLOCKS   ================================
    // ===========================================================================
    //
    //   GRID_SIZE     Cells along X and Z.
    //   GRID_HEIGHT   Cells upward.
    //   MAX_BLOCKS    Blocks a design holds at most.
    //   Kind          The function of the building.
    //   Blocks        The placed blocks.
    //   Height        Cells from the ground to the top of the highest block.
    //   RestHeight    Where a block would land, null when it does not fit.
    //   TryPlace      Drops a block onto the grid.
    //   BlockAt       The block that fills a cell, -1 for none.
    //   Repaint       Gives a placed block another paint and pattern.
    //   RemoveAt      Removes a block, the ones above fall.
    //   Clear         Removes every block.
    //   CopyFrom      Takes over function and blocks of another design.
    //

    public const int GRID_SIZE = 12;
    public const int GRID_HEIGHT = 16;
    public const int MAX_BLOCKS = 120;

    private readonly List<PlacedBlock> _blocks = new();

    public BuildingKind Kind { get; private set; }

    public IReadOnlyList<PlacedBlock> Blocks => _blocks;

    /// <summary>An empty blueprint of a function.</summary>
    /// <param name="kind">(Required) The function of the building.</param>
    public BuildingDesign(BuildingKind kind)
    {
        Kind = kind;
    }

    public int Height
    {
        get
        {
            int height = 0;
            foreach (PlacedBlock block in _blocks)
            {
                height = System.Math.Max(height, block.Y + BlockCatalog.Get(block.Kind).Height);
            }
            return height;
        }
    }

    /// <summary>The height a block would land on: the ground or the top of the highest block below its footprint.</summary>
    /// <param name="kind">(Required) The block.</param>
    /// <param name="x">(Required) Cell of its lowest corner along X.</param>
    /// <param name="z">(Required) Cell of its lowest corner along Z.</param>
    /// <param name="turns">(Optional) Quarter turns. Defaults to 0.</param>
    /// <returns>The cell height of its bottom, null when it leaves the grid or would stick out on top.</returns>
    public int? RestHeight(BlockKind kind, int x, int z, int turns = 0)
    {
        return RestHeight(_blocks, kind, x, z, turns);
    }

    private static int? RestHeight(List<PlacedBlock> blocks, BlockKind kind, int x, int z, int turns)
    {
        (int width, int depth) = BlockCatalog.Footprint(kind, turns);
        if (x < 0 || z < 0 || x + width > GRID_SIZE || z + depth > GRID_SIZE)
        {
            return null;
        }
        int rest = 0;
        foreach (PlacedBlock block in blocks)
        {
            (int otherWidth, int otherDepth) = BlockCatalog.Footprint(block.Kind, block.Turns);
            bool isBelow = x < block.X + otherWidth && block.X < x + width
                && z < block.Z + otherDepth && block.Z < z + depth;
            if (isBelow)
            {
                rest = System.Math.Max(rest, block.Y + BlockCatalog.Get(block.Kind).Height);
            }
        }
        return rest + BlockCatalog.Get(kind).Height <= GRID_HEIGHT ? rest : null;
    }

    /// <summary>Drops a block onto the grid, it lands on the ground or on what stands below.</summary>
    /// <param name="kind">(Required) The block.</param>
    /// <param name="x">(Required) Cell of its lowest corner along X, 0..GRID_SIZE.</param>
    /// <param name="z">(Required) Cell of its lowest corner along Z, 0..GRID_SIZE.</param>
    /// <param name="turns">(Optional) Quarter turns around the vertical axis. Defaults to 0.</param>
    /// <param name="paint">(Optional) Index in BlockCatalog.PAINTS. Defaults to the paint of the block.</param>
    /// <param name="pattern">(Optional) 0..BlockCatalog.PATTERN_COUNT, 0 is plain. Defaults to 0.</param>
    /// <returns>False when it leaves the grid, sticks out on top or the design is full.</returns>
    /// <example><code>design.TryPlace(BlockKind.Palisade, 2, 0, turns: 1, paint: 3);</code></example>
    public bool TryPlace(BlockKind kind, int x, int z, int turns = 0, int paint = BlockCatalog.DEFAULT_PAINT, int pattern = 0)
    {
        if (_blocks.Count >= MAX_BLOCKS || RestHeight(kind, x, z, turns) is not int y)
        {
            return false;
        }
        _blocks.Add(new PlacedBlock(kind, x, y, z, turns & 3, paint, System.Math.Clamp(pattern, 0, BlockCatalog.PATTERN_COUNT - 1)));
        return true;
    }

    /// <summary>Gives a placed block another paint and pattern.</summary>
    /// <param name="index">(Required) Index in Blocks.</param>
    /// <param name="paint">(Required) Index in BlockCatalog.PAINTS or BlockCatalog.DEFAULT_PAINT.</param>
    /// <param name="pattern">(Required) 0..BlockCatalog.PATTERN_COUNT, 0 is plain.</param>
    /// <returns>False for an index outside Blocks.</returns>
    public bool Repaint(int index, int paint, int pattern)
    {
        if (index < 0 || index >= _blocks.Count)
        {
            return false;
        }
        _blocks[index] = _blocks[index] with { Paint = paint, Pattern = System.Math.Clamp(pattern, 0, BlockCatalog.PATTERN_COUNT - 1) };
        return true;
    }

    /// <summary>The block that fills a cell.</summary>
    /// <returns>Index in Blocks, -1 for an empty cell.</returns>
    public int BlockAt(int x, int y, int z)
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            PlacedBlock block = _blocks[i];
            (int width, int depth) = BlockCatalog.Footprint(block.Kind, block.Turns);
            bool isInside = x >= block.X && x < block.X + width
                && z >= block.Z && z < block.Z + depth
                && y >= block.Y && y < block.Y + BlockCatalog.Get(block.Kind).Height;
            if (isInside)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Removes a block, every block that stood on it falls to its new rest.</summary>
    /// <param name="index">(Required) Index in Blocks.</param>
    /// <returns>False for an index outside Blocks.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _blocks.Count)
        {
            return false;
        }
        _blocks.RemoveAt(index);
        Settle();
        return true;
    }

    /// <summary>Lets every block fall to its rest, lowest first, the order in Blocks stays.</summary>
    private void Settle()
    {
        var order = new List<int>();
        for (int i = 0; i < _blocks.Count; i++)
        {
            order.Add(i);
        }
        order.Sort((a, b) => _blocks[a].Y != _blocks[b].Y ? _blocks[a].Y.CompareTo(_blocks[b].Y) : a.CompareTo(b));

        var settled = new List<PlacedBlock>();
        foreach (int index in order)
        {
            PlacedBlock block = _blocks[index];
            // A block that stood in the grid always finds a rest at or below its old height
            int rest = RestHeight(settled, block.Kind, block.X, block.Z, block.Turns) ?? block.Y;
            _blocks[index] = block with { Y = rest };
            settled.Add(_blocks[index]);
        }
    }

    public void Clear()
    {
        _blocks.Clear();
    }

    /// <summary>Takes over function and blocks of another design.</summary>
    /// <param name="other">(Required) The design copied.</param>
    public void CopyFrom(BuildingDesign other)
    {
        Kind = other.Kind;
        _blocks.Clear();
        _blocks.AddRange(other._blocks);
    }

    // ===========================================================================
    // ===============================   EFFECTS   ===============================
    // ===========================================================================
    //
    //   FoodCost        Food the building costs.
    //   WoodCost        Wood the building costs.
    //   Health          Hit points against raids.
    //   Villagers       Villagers it raises (dwelling).
    //   Warriors        Warriors it trains (warrior hut).
    //   Defense         Defense it adds to the village (watchtower).
    //   TradeBonus      Share more a trader brings (market).
    //   RelationBonus   Extra relation per trade (totem).
    //   FoodPerFruit    Extra food per gathered fruit while it stands (storehouse).
    //

    /// <summary>Costs and effects of the function from data/balance.json (tribe.buildings).</summary>
    private BuildingBalance Tune => Balance.Current.Tribe.Buildings[Kind];

    public int FoodCost => Tune.Food;

    public int WoodCost => Tune.Wood;

    public float Health => Tune.Health;

    public int Villagers => Tune.Villagers;

    public int Warriors => Tune.Warriors;

    public float Defense => Tune.Defense;

    public float TradeBonus => Tune.TradeBonus;

    public float RelationBonus => Tune.RelationBonus;

    public int FoodPerFruit => Tune.FoodPerFruit;
}
