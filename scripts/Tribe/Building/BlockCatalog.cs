// NOTE:
//   The building blocks of the village builder: six groups of three blocks each, what a
//   block is (its group in the palette, the cells it fills on the grid, its default paint)
//   and the paints a block can take.
//   Blocks are looks only, a building gets its costs and effects from its function
//   (BuildingKind, data/balance.json tribe.buildings). Pure data without the engine.
//
//   BlockInfo info = BlockCatalog.Get(BlockKind.ConeRoof);
//   (int width, int depth) = BlockCatalog.Footprint(BlockKind.Palisade, turns: 1);
//   Color paint = BlockCatalog.PaintOf(BlockKind.Door, BlockCatalog.DEFAULT_PAINT);

//
//   Chapters:
//     TYPES     Block kinds, palette tabs and the entry of one block.
//     CATALOG   The table of every block and the paints.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   BlockKind    Every block of the builder, three per group.
//   BlockGroup   Group of the palette: walls, roofs, frames, defense, trade, decoration.
//   BlockInfo    One block: group, cells it fills (width along X, height, depth along Z), default paint.
//

public enum BlockKind
{
    RoundRoom, SquareRoom, RoundTower,      // walls
    ConeRoof, GableRoof, SmallConeRoof,     // roofs
    Stilts, Platform, Ladder,               // frames
    Palisade, Torch, Banner,                // defense
    Stall, Awning, Crate,                   // trade
    Door, Totem, Horns,                     // decoration
}

public enum BlockGroup { Wall, Roof, Frame, Defense, Trade, Decor }

public readonly record struct BlockInfo(BlockKind Kind, BlockGroup Group, int Width, int Height, int Depth, int Paint);

public static class BlockCatalog
{
    // ===========================================================================
    // ===============================   CATALOG   ===============================
    // ===========================================================================
    //
    //   DEFAULT_PAINT   Paint index that means "the block's own default".
    //   PATTERN_COUNT   Patterns a block can carry: 0 plain, then bands, stripes, zigzag, dots, checks.
    //   PAINTS          The paints a block can take, by index.
    //   BLOCKS          Every block in palette order.
    //   Get             The entry of a block.
    //   In              The blocks of a group.
    //   Footprint       Width and depth of a block after quarter turns.
    //   PaintOf         The color of a block with a paint index.
    //

    public const int DEFAULT_PAINT = -1;
    public const int PATTERN_COUNT = 6;

    // Paint indices, used as defaults in BLOCKS
    private const int CLAY = 0;
    private const int STONE = 2;
    private const int WOOD = 3;
    private const int GRASS = 4;
    private const int SHINGLE = 6;
    private const int RED = 7;
    private const int BLUE = 8;
    private const int BONE = 9;
    private const int OCHRE = 10;

    public static readonly IReadOnlyList<Color> PAINTS = new[]
    {
        new Color(0.72f, 0.55f, 0.38f),     // clay
        new Color(0.55f, 0.42f, 0.25f),     // wattle
        new Color(0.56f, 0.56f, 0.58f),     // stone
        new Color(0.42f, 0.3f, 0.17f),      // wood
        new Color(0.62f, 0.56f, 0.28f),     // dry grass
        new Color(0.8f, 0.72f, 0.44f),      // reed
        new Color(0.45f, 0.26f, 0.18f),     // shingle
        new Color(0.85f, 0.35f, 0.3f),      // red cloth
        new Color(0.25f, 0.55f, 0.75f),     // blue cloth
        new Color(0.9f, 0.88f, 0.8f),       // bone
        new Color(0.9f, 0.6f, 0.2f),        // ochre
    };

    public static readonly IReadOnlyList<BlockInfo> BLOCKS = new[]
    {
        new BlockInfo(BlockKind.RoundRoom,     BlockGroup.Wall,    4, 2, 4, CLAY),
        new BlockInfo(BlockKind.SquareRoom,    BlockGroup.Wall,    4, 2, 4, CLAY),
        new BlockInfo(BlockKind.RoundTower,    BlockGroup.Wall,    2, 2, 2, STONE),
        new BlockInfo(BlockKind.ConeRoof,      BlockGroup.Roof,    4, 2, 4, GRASS),
        new BlockInfo(BlockKind.GableRoof,     BlockGroup.Roof,    4, 2, 4, SHINGLE),
        new BlockInfo(BlockKind.SmallConeRoof, BlockGroup.Roof,    2, 2, 2, GRASS),
        new BlockInfo(BlockKind.Stilts,        BlockGroup.Frame,   2, 2, 2, WOOD),
        new BlockInfo(BlockKind.Platform,      BlockGroup.Frame,   4, 1, 4, WOOD),
        new BlockInfo(BlockKind.Ladder,        BlockGroup.Frame,   1, 2, 1, WOOD),
        new BlockInfo(BlockKind.Palisade,      BlockGroup.Defense, 4, 2, 1, WOOD),
        new BlockInfo(BlockKind.Torch,         BlockGroup.Defense, 1, 2, 1, WOOD),
        new BlockInfo(BlockKind.Banner,        BlockGroup.Defense, 1, 3, 1, BLUE),
        new BlockInfo(BlockKind.Stall,         BlockGroup.Trade,   2, 1, 2, WOOD),
        new BlockInfo(BlockKind.Awning,        BlockGroup.Trade,   2, 2, 2, RED),
        new BlockInfo(BlockKind.Crate,         BlockGroup.Trade,   1, 1, 1, WOOD),
        new BlockInfo(BlockKind.Door,          BlockGroup.Decor,   1, 2, 2, WOOD),
        new BlockInfo(BlockKind.Totem,         BlockGroup.Decor,   2, 2, 2, OCHRE),
        new BlockInfo(BlockKind.Horns,         BlockGroup.Decor,   2, 1, 2, BONE),
    };

    /// <summary>The entry of a block. BLOCKS lists the kinds in enum order.</summary>
    public static BlockInfo Get(BlockKind kind)
    {
        return BLOCKS[(int)kind];
    }

    /// <summary>The blocks of a group in palette order.</summary>
    public static IEnumerable<BlockInfo> In(BlockGroup group)
    {
        foreach (BlockInfo info in BLOCKS)
        {
            if (group == info.Group)
            {
                yield return info;
            }
        }
    }

    /// <summary>Cells a block covers on the ground after quarter turns: an odd turn swaps width and depth.</summary>
    /// <param name="kind">(Required) The block.</param>
    /// <param name="turns">(Required) Quarter turns around the vertical axis, any integer.</param>
    /// <returns>Cells along X and along Z.</returns>
    public static (int Width, int Depth) Footprint(BlockKind kind, int turns)
    {
        BlockInfo info = Get(kind);
        return 0 == (turns & 1) ? (info.Width, info.Depth) : (info.Depth, info.Width);
    }

    /// <summary>The color of a block for a paint index.</summary>
    /// <param name="kind">(Required) The block, its default paint answers DEFAULT_PAINT.</param>
    /// <param name="paint">(Required) Index in PAINTS or DEFAULT_PAINT.</param>
    public static Color PaintOf(BlockKind kind, int paint)
    {
        int index = paint >= 0 && paint < PAINTS.Count ? paint : Get(kind).Paint;
        return PAINTS[index];
    }
}
