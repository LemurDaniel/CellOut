// NOTE:
//   The building blocks of the village builder by era and group of its palette, what a
//   block is (its era and group in the palette, the cells it fills on the grid, its default paint)
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
//   BlockKind    Every block of the builder (the order is the order of BLOCKS, new kinds go to the end;
//                saves name blocks, so a kind may be removed: the antique column, steps, temple roof,
//                obelisk and fountain were).
//   BlockEra     Top section of the palette (wish of the user 2026-11-17): the village of the tribe, the modern
//                town, later space travel (no blocks yet, its tab is locked). Every era has its own groups.
//   BlockGroup   Group of the palette inside an era: walls, roofs, frames, defense, trade, decoration.
//   BlockInfo    One block: era, group, cells it fills (width along X, height, depth along Z), default paint.
//

public enum BlockKind
{
    RoundRoom, SquareRoom, RoundTower,      // walls
    ConeRoof, GableRoof, SmallConeRoof,     // roofs
    Stilts, Platform, Ladder,               // frames
    Palisade, Torch, Banner,                // defense
    Stall, Awning, Crate,                   // trade
    Door, Totem, Horns,                     // decoration
    StoneRoom, SmallRoom,                   // more walls
    DomeRoof, HipRoof, FlatRoof,            // more roofs
    Stairs, Railing,                        // more frames
    Gate, StoneWall, Spikes, Fence,         // more defense
    Barrel, Pots, DryingRack, Cart,         // more trade
    Window, Well, Firepit,                  // more decoration
    BrickRoom, GlassFloor, RoofDeck, Entrance,          // modern: the town of the last stage
    Antenna, Smokestack, StreetLamp, Car,
    SmallBrickRoom, TileRoof, SolarPanels, Balcony, FireEscape,     // more modern: home, guard, trade and street
    ChainFence, Barrier,
    Kiosk, VendingMachine, Container, Bench, TrafficLight,
}

public enum BlockEra { Village, Modern, Space }

public enum BlockGroup { Wall, Roof, Frame, Defense, Trade, Decor }

public readonly record struct BlockInfo(BlockKind Kind, BlockEra Era, BlockGroup Group, int Width, int Height, int Depth, int Paint);

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
    //   In              The blocks of a group, of a group of an era, or of an era.
    //   Footprint       Width and depth of a block after quarter turns.
    //   PaintOf         The color of a block with a paint index.
    //

    public const int DEFAULT_PAINT = -1;
    public const int PATTERN_COUNT = 6;

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
    private const int BRICK = 11;
    private const int CONCRETE = 12;

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
        new Color(0.72f, 0.36f, 0.27f),     // brick
        new Color(0.8f, 0.8f, 0.78f),       // concrete
    };

    public static readonly IReadOnlyList<BlockInfo> BLOCKS = new[]
    {
        new BlockInfo(BlockKind.RoundRoom,     BlockEra.Village, BlockGroup.Wall,    4, 2, 4, CLAY),
        new BlockInfo(BlockKind.SquareRoom,    BlockEra.Village, BlockGroup.Wall,    4, 2, 4, CLAY),
        new BlockInfo(BlockKind.RoundTower,    BlockEra.Village, BlockGroup.Wall,    2, 2, 2, STONE),
        new BlockInfo(BlockKind.ConeRoof,      BlockEra.Village, BlockGroup.Roof,    4, 2, 4, GRASS),
        new BlockInfo(BlockKind.GableRoof,     BlockEra.Village, BlockGroup.Roof,    4, 2, 4, SHINGLE),
        new BlockInfo(BlockKind.SmallConeRoof, BlockEra.Village, BlockGroup.Roof,    2, 2, 2, GRASS),
        new BlockInfo(BlockKind.Stilts,        BlockEra.Village, BlockGroup.Frame,   2, 2, 2, WOOD),
        new BlockInfo(BlockKind.Platform,      BlockEra.Village, BlockGroup.Frame,   4, 1, 4, WOOD),
        new BlockInfo(BlockKind.Ladder,        BlockEra.Village, BlockGroup.Frame,   1, 2, 1, WOOD),
        new BlockInfo(BlockKind.Palisade,      BlockEra.Village, BlockGroup.Defense, 4, 2, 1, WOOD),
        new BlockInfo(BlockKind.Torch,         BlockEra.Village, BlockGroup.Defense, 1, 2, 1, WOOD),
        new BlockInfo(BlockKind.Banner,        BlockEra.Village, BlockGroup.Defense, 1, 3, 1, BLUE),
        new BlockInfo(BlockKind.Stall,         BlockEra.Village, BlockGroup.Trade,   2, 1, 2, WOOD),
        new BlockInfo(BlockKind.Awning,        BlockEra.Village, BlockGroup.Trade,   2, 2, 2, RED),
        new BlockInfo(BlockKind.Crate,         BlockEra.Village, BlockGroup.Trade,   1, 1, 1, WOOD),
        new BlockInfo(BlockKind.Door,          BlockEra.Village, BlockGroup.Decor,   1, 2, 2, WOOD),
        new BlockInfo(BlockKind.Totem,         BlockEra.Village, BlockGroup.Decor,   2, 2, 2, OCHRE),
        new BlockInfo(BlockKind.Horns,         BlockEra.Village, BlockGroup.Decor,   2, 1, 2, BONE),
        new BlockInfo(BlockKind.StoneRoom,     BlockEra.Village, BlockGroup.Wall,    4, 2, 4, STONE),
        new BlockInfo(BlockKind.SmallRoom,     BlockEra.Village, BlockGroup.Wall,    2, 2, 2, CLAY),
        new BlockInfo(BlockKind.DomeRoof,      BlockEra.Village, BlockGroup.Roof,    4, 2, 4, REED),
        new BlockInfo(BlockKind.HipRoof,       BlockEra.Village, BlockGroup.Roof,    4, 2, 4, SHINGLE),
        new BlockInfo(BlockKind.FlatRoof,      BlockEra.Village, BlockGroup.Roof,    4, 1, 4, CLAY),
        new BlockInfo(BlockKind.Stairs,        BlockEra.Village, BlockGroup.Frame,   2, 2, 2, WOOD),
        new BlockInfo(BlockKind.Railing,       BlockEra.Village, BlockGroup.Frame,   4, 1, 1, WOOD),
        new BlockInfo(BlockKind.Gate,          BlockEra.Village, BlockGroup.Defense, 4, 3, 1, WOOD),
        new BlockInfo(BlockKind.StoneWall,     BlockEra.Village, BlockGroup.Defense, 4, 2, 1, STONE),
        new BlockInfo(BlockKind.Spikes,        BlockEra.Village, BlockGroup.Defense, 2, 1, 1, WOOD),
        new BlockInfo(BlockKind.Fence,         BlockEra.Village, BlockGroup.Defense, 4, 1, 1, WATTLE),
        new BlockInfo(BlockKind.Barrel,        BlockEra.Village, BlockGroup.Trade,   1, 1, 1, WOOD),
        new BlockInfo(BlockKind.Pots,          BlockEra.Village, BlockGroup.Trade,   1, 1, 1, OCHRE),
        new BlockInfo(BlockKind.DryingRack,    BlockEra.Village, BlockGroup.Trade,   2, 2, 1, WOOD),
        new BlockInfo(BlockKind.Cart,          BlockEra.Village, BlockGroup.Trade,   2, 1, 2, WOOD),
        new BlockInfo(BlockKind.Window,        BlockEra.Village, BlockGroup.Decor,   1, 2, 2, WOOD),
        new BlockInfo(BlockKind.Well,          BlockEra.Village, BlockGroup.Decor,   2, 2, 2, SHINGLE),
        new BlockInfo(BlockKind.Firepit,       BlockEra.Village, BlockGroup.Decor,   2, 1, 2, STONE),
        new BlockInfo(BlockKind.BrickRoom,     BlockEra.Modern,  BlockGroup.Wall,    4, 2, 4, BRICK),
        new BlockInfo(BlockKind.GlassFloor,    BlockEra.Modern,  BlockGroup.Wall,    4, 2, 4, CONCRETE),
        new BlockInfo(BlockKind.RoofDeck,      BlockEra.Modern,  BlockGroup.Roof,    4, 1, 4, CONCRETE),
        new BlockInfo(BlockKind.Entrance,      BlockEra.Modern,  BlockGroup.Decor,   1, 2, 2, BLUE),
        new BlockInfo(BlockKind.Antenna,       BlockEra.Modern,  BlockGroup.Roof,    1, 4, 1, BONE),
        new BlockInfo(BlockKind.Smokestack,    BlockEra.Modern,  BlockGroup.Roof,    2, 4, 2, BRICK),
        new BlockInfo(BlockKind.StreetLamp,    BlockEra.Modern,  BlockGroup.Decor,   1, 3, 1, RED),
        new BlockInfo(BlockKind.Car,           BlockEra.Modern,  BlockGroup.Decor,   2, 1, 4, RED),
        new BlockInfo(BlockKind.SmallBrickRoom, BlockEra.Modern, BlockGroup.Wall,    2, 2, 2, BRICK),
        new BlockInfo(BlockKind.TileRoof,      BlockEra.Modern,  BlockGroup.Roof,    4, 2, 4, RED),
        new BlockInfo(BlockKind.SolarPanels,   BlockEra.Modern,  BlockGroup.Roof,    2, 1, 2, CONCRETE),
        new BlockInfo(BlockKind.Balcony,       BlockEra.Modern,  BlockGroup.Frame,   1, 1, 2, CONCRETE),
        new BlockInfo(BlockKind.FireEscape,    BlockEra.Modern,  BlockGroup.Frame,   1, 2, 2, STONE),
        new BlockInfo(BlockKind.ChainFence,    BlockEra.Modern,  BlockGroup.Defense, 4, 2, 1, OCHRE),
        new BlockInfo(BlockKind.Barrier,       BlockEra.Modern,  BlockGroup.Defense, 2, 1, 1, RED),
        new BlockInfo(BlockKind.Kiosk,         BlockEra.Modern,  BlockGroup.Trade,   2, 2, 2, RED),
        new BlockInfo(BlockKind.VendingMachine, BlockEra.Modern, BlockGroup.Trade,   1, 2, 1, BLUE),
        new BlockInfo(BlockKind.Container,     BlockEra.Modern,  BlockGroup.Trade,   2, 2, 4, OCHRE),
        new BlockInfo(BlockKind.Bench,         BlockEra.Modern,  BlockGroup.Decor,   1, 1, 2, WOOD),
        new BlockInfo(BlockKind.TrafficLight,  BlockEra.Modern,  BlockGroup.Decor,   1, 3, 1, OCHRE),
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

    /// <summary>The blocks of a group of an era in palette order.</summary>
    /// <example><code>BlockCatalog.In(BlockEra.Modern, BlockGroup.Roof)   // roof deck, antenna, smokestack</code></example>
    public static IEnumerable<BlockInfo> In(BlockEra era, BlockGroup group)
    {
        foreach (BlockInfo info in BLOCKS)
        {
            if (era == info.Era && group == info.Group)
            {
                yield return info;
            }
        }
    }

    /// <summary>The blocks of an era in palette order, none for an era that is not built yet.</summary>
    public static IEnumerable<BlockInfo> In(BlockEra era)
    {
        foreach (BlockInfo info in BLOCKS)
        {
            if (era == info.Era)
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
