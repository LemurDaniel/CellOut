// NOTE:
//   The building blocks of the village designer: what a block is (its tab in the palette,
//   the cells it fills on the grid, its default paint) and the paints a block can take.
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
//   BlockKind       Every block of the designer.
//   BlockCategory   Tab of the palette: rooms and frames, roofs, decoration.
//   BlockInfo       One block: tab, cells it fills (width along X, height, depth along Z), default paint.
//

public enum BlockKind
{
    // Rooms and frames
    RoundRoom, SquareRoom, RoundTower, SquareTower, Stilts, Platform, Palisade,
    // Roofs
    ConeRoof, PyramidRoof, GableRoof, SmallConeRoof, SmallPyramidRoof, Awning,
    // Decoration
    Door, Totem, Horns, Stall, Crate, Basket, Banner, Torch, Ladder,
}

public enum BlockCategory { Frame, Roof, Decor }

public readonly record struct BlockInfo(BlockKind Kind, BlockCategory Category, int Width, int Height, int Depth, int Paint);

public static class BlockCatalog
{
    // ===========================================================================
    // ===============================   CATALOG   ===============================
    // ===========================================================================
    //
    //   DEFAULT_PAINT   Paint index that means "the block's own default".
    //   PAINTS          The paints a block can take, by index.
    //   BLOCKS          Every block in palette order.
    //   Get             The entry of a block.
    //   Footprint       Width and depth of a block after quarter turns.
    //   PaintOf         The color of a block with a paint index.
    //

    public const int DEFAULT_PAINT = -1;

    // Paint indices, used as defaults in BLOCKS
    private const int CLAY = 0;
    private const int WATTLE = 1;
    private const int STONE = 2;
    private const int WOOD = 3;
    private const int GRASS = 4;
    private const int REED = 5;
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
        new BlockInfo(BlockKind.RoundRoom,        BlockCategory.Frame, 4, 2, 4, CLAY),
        new BlockInfo(BlockKind.SquareRoom,       BlockCategory.Frame, 4, 2, 4, CLAY),
        new BlockInfo(BlockKind.RoundTower,       BlockCategory.Frame, 2, 2, 2, STONE),
        new BlockInfo(BlockKind.SquareTower,      BlockCategory.Frame, 2, 2, 2, WATTLE),
        new BlockInfo(BlockKind.Stilts,           BlockCategory.Frame, 2, 2, 2, WOOD),
        new BlockInfo(BlockKind.Platform,         BlockCategory.Frame, 4, 1, 4, WOOD),
        new BlockInfo(BlockKind.Palisade,         BlockCategory.Frame, 4, 2, 1, WOOD),
        new BlockInfo(BlockKind.ConeRoof,         BlockCategory.Roof,  4, 2, 4, GRASS),
        new BlockInfo(BlockKind.PyramidRoof,      BlockCategory.Roof,  4, 2, 4, REED),
        new BlockInfo(BlockKind.GableRoof,        BlockCategory.Roof,  4, 2, 4, SHINGLE),
        new BlockInfo(BlockKind.SmallConeRoof,    BlockCategory.Roof,  2, 2, 2, GRASS),
        new BlockInfo(BlockKind.SmallPyramidRoof, BlockCategory.Roof,  2, 2, 2, SHINGLE),
        new BlockInfo(BlockKind.Awning,           BlockCategory.Roof,  2, 2, 2, RED),
        new BlockInfo(BlockKind.Door,             BlockCategory.Decor, 1, 2, 2, WOOD),
        new BlockInfo(BlockKind.Totem,            BlockCategory.Decor, 2, 2, 2, OCHRE),
        new BlockInfo(BlockKind.Horns,            BlockCategory.Decor, 2, 1, 2, BONE),
        new BlockInfo(BlockKind.Stall,            BlockCategory.Decor, 2, 1, 2, WOOD),
        new BlockInfo(BlockKind.Crate,            BlockCategory.Decor, 1, 1, 1, WOOD),
        new BlockInfo(BlockKind.Basket,           BlockCategory.Decor, 1, 1, 1, REED),
        new BlockInfo(BlockKind.Banner,           BlockCategory.Decor, 1, 3, 1, BLUE),
        new BlockInfo(BlockKind.Torch,            BlockCategory.Decor, 1, 2, 1, WOOD),
        new BlockInfo(BlockKind.Ladder,           BlockCategory.Decor, 1, 2, 1, WOOD),
    };

    /// <summary>The entry of a block. BLOCKS lists the kinds in enum order.</summary>
    public static BlockInfo Get(BlockKind kind)
    {
        return BLOCKS[(int)kind];
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
