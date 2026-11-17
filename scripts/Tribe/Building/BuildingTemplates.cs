// NOTE:
//   Ready-made layouts for every building function, for players who do not want to
//   design: the editor offers them as buttons, a new game starts with the first one of
//   each function and the rival tribes pick theirs at random. A template is a list of
//   drops (block, cell, turns, paint) replayed onto an empty BuildingDesign, in order:
//   a block listed later lands on the ones listed before. The cells of a template are
//   written for a plot of TEMPLATE_SIZE cells and moved to the middle of the grid. Pure
//   data without the engine.
//
//   BuildingDesign hut = BuildingTemplates.Default(BuildingKind.Dwelling);
//   BuildingDesign tower = BuildingTemplates.Build(BuildingKind.Watchtower, 1);
//   string key = BuildingTemplates.NameKey(BuildingKind.Market, 0);   // "TEMPLATE_BAZAAR"

//
//   Chapters:
//     TEMPLATES   The layouts per function and how they become designs.
//

using System.Collections.Generic;

namespace CellOut;

public static class BuildingTemplates
{
    // ===========================================================================
    // ==============================   TEMPLATES   ==============================
    // ===========================================================================
    //
    //   TEMPLATE_SIZE   Cells along X and Z the drops below are written for.
    //   Count     Templates of a function.
    //   NameKey   Translation key of a template.
    //   Build     A new design from a template.
    //   Default   The first template of a function.
    //

    public const int TEMPLATE_SIZE = 8;

    private readonly record struct Drop(BlockKind Kind, int X, int Z, int Turns = 0, int Paint = BlockCatalog.DEFAULT_PAINT);

    private sealed record Template(string NameKey, Drop[] Drops);

    // Paint indices of BlockCatalog.PAINTS used below
    private const int STONE = 2;
    private const int SHINGLE = 6;
    private const int RED = 7;
    private const int BLUE = 8;
    private const int BONE = 9;
    private const int OCHRE = 10;

    private static readonly IReadOnlyDictionary<BuildingKind, Template[]> TEMPLATES = new Dictionary<BuildingKind, Template[]>
    {
        [BuildingKind.Dwelling] = new[]
        {
            new Template("TEMPLATE_ROUND_HUT", new[]
            {
                new Drop(BlockKind.RoundRoom, 2, 2),
                new Drop(BlockKind.ConeRoof, 2, 2),
                new Drop(BlockKind.Door, 6, 3),
            }),
            new Template("TEMPLATE_LONGHOUSE", new[]
            {
                new Drop(BlockKind.SquareRoom, 2, 2),
                new Drop(BlockKind.GableRoof, 2, 2),
                new Drop(BlockKind.Door, 6, 3),
                new Drop(BlockKind.Crate, 6, 1),
                new Drop(BlockKind.Torch, 6, 6),
            }),
            new Template("TEMPLATE_STILT_HOUSE", new[]
            {
                new Drop(BlockKind.Stilts, 2, 2),
                new Drop(BlockKind.Stilts, 4, 2),
                new Drop(BlockKind.Stilts, 2, 4),
                new Drop(BlockKind.Stilts, 4, 4),
                new Drop(BlockKind.Platform, 2, 2),
                new Drop(BlockKind.RoundRoom, 2, 2),
                new Drop(BlockKind.ConeRoof, 2, 2),
                new Drop(BlockKind.Ladder, 6, 3),
            }),
        },
        [BuildingKind.Storehouse] = new[]
        {
            new Template("TEMPLATE_GRANARY", new[]
            {
                new Drop(BlockKind.Stilts, 2, 2),
                new Drop(BlockKind.Stilts, 4, 2),
                new Drop(BlockKind.Stilts, 2, 4),
                new Drop(BlockKind.Stilts, 4, 4),
                new Drop(BlockKind.Platform, 2, 2),
                new Drop(BlockKind.SquareRoom, 2, 2),
                new Drop(BlockKind.GableRoof, 2, 2),
                new Drop(BlockKind.Ladder, 6, 3),
                new Drop(BlockKind.Crate, 1, 6),
                new Drop(BlockKind.Crate, 0, 6),
            }),
            new Template("TEMPLATE_SILO", new[]
            {
                new Drop(BlockKind.RoundRoom, 2, 2, Paint: STONE),
                new Drop(BlockKind.RoundRoom, 2, 2),
                new Drop(BlockKind.ConeRoof, 2, 2),
                new Drop(BlockKind.Door, 6, 3),
                new Drop(BlockKind.Crate, 6, 1),
                new Drop(BlockKind.Crate, 6, 6),
                new Drop(BlockKind.Crate, 7, 6),
            }),
        },
        [BuildingKind.Market] = new[]
        {
            new Template("TEMPLATE_BAZAAR", new[]
            {
                new Drop(BlockKind.Stall, 1, 1),
                new Drop(BlockKind.Awning, 1, 1),
                new Drop(BlockKind.Stall, 5, 1),
                new Drop(BlockKind.Awning, 5, 1, Paint: BLUE),
                new Drop(BlockKind.Stall, 3, 5),
                new Drop(BlockKind.Awning, 3, 5, Paint: OCHRE),
                new Drop(BlockKind.Crate, 0, 5),
                new Drop(BlockKind.Crate, 6, 5),
                new Drop(BlockKind.Banner, 3, 3),
            }),
            new Template("TEMPLATE_HALL", new[]
            {
                new Drop(BlockKind.SquareRoom, 2, 2),
                new Drop(BlockKind.GableRoof, 2, 2, Paint: RED),
                new Drop(BlockKind.Door, 6, 3),
                new Drop(BlockKind.Stall, 0, 3),
                new Drop(BlockKind.Awning, 0, 3),
                new Drop(BlockKind.Stall, 3, 6),
                new Drop(BlockKind.Awning, 3, 6, Paint: BLUE),
                new Drop(BlockKind.Banner, 7, 1),
            }),
        },
        [BuildingKind.Totem] = new[]
        {
            new Template("TEMPLATE_TOTEM_POLE", new[]
            {
                new Drop(BlockKind.RoundTower, 3, 3),
                new Drop(BlockKind.Totem, 3, 3),
                new Drop(BlockKind.Totem, 3, 3, Paint: BLUE),
                new Drop(BlockKind.Totem, 3, 3, Paint: BONE),
                new Drop(BlockKind.Horns, 3, 3),
                new Drop(BlockKind.Torch, 1, 3),
                new Drop(BlockKind.Torch, 6, 4),
            }),
            new Template("TEMPLATE_SHRINE", new[]
            {
                new Drop(BlockKind.Platform, 2, 2, Paint: STONE),
                new Drop(BlockKind.Totem, 3, 3),
                new Drop(BlockKind.Totem, 3, 3, Paint: RED),
                new Drop(BlockKind.Horns, 3, 3),
                new Drop(BlockKind.Torch, 2, 2),
                new Drop(BlockKind.Torch, 5, 5),
                new Drop(BlockKind.Crate, 2, 5),
                new Drop(BlockKind.Crate, 5, 2),
            }),
        },
        [BuildingKind.Barracks] = new[]
        {
            new Template("TEMPLATE_WAR_HUT", new[]
            {
                new Drop(BlockKind.SquareRoom, 2, 2, Paint: SHINGLE),
                new Drop(BlockKind.GableRoof, 2, 2, Paint: RED),
                new Drop(BlockKind.Horns, 3, 3),
                new Drop(BlockKind.Door, 6, 3),
                new Drop(BlockKind.Palisade, 2, 0),
                new Drop(BlockKind.Palisade, 2, 7),
                new Drop(BlockKind.Banner, 7, 1, Paint: RED),
                new Drop(BlockKind.Torch, 7, 6),
            }),
            new Template("TEMPLATE_WAR_CAMP", new[]
            {
                new Drop(BlockKind.RoundRoom, 2, 2),
                new Drop(BlockKind.ConeRoof, 2, 2),
                new Drop(BlockKind.Horns, 3, 3),
                new Drop(BlockKind.Door, 6, 3),
                new Drop(BlockKind.Palisade, 0, 2, Turns: 1),
                new Drop(BlockKind.Palisade, 2, 0),
                new Drop(BlockKind.Palisade, 2, 7),
                new Drop(BlockKind.Banner, 7, 0, Paint: RED),
                new Drop(BlockKind.Banner, 7, 7, Paint: RED),
            }),
        },
        [BuildingKind.Hall] = new[]
        {
            // A temple: steps, a room inside, a row of columns front and back, the roof lands on the columns
            new Template("TEMPLATE_TEMPLE", new[]
            {
                new Drop(BlockKind.Steps, 2, 0),
                new Drop(BlockKind.Steps, 2, 4),
                new Drop(BlockKind.SquareRoom, 2, 2, Paint: BONE),
                new Drop(BlockKind.Column, 2, 0), new Drop(BlockKind.Column, 3, 0), new Drop(BlockKind.Column, 4, 0), new Drop(BlockKind.Column, 5, 0),
                new Drop(BlockKind.Column, 2, 7), new Drop(BlockKind.Column, 3, 7), new Drop(BlockKind.Column, 4, 7), new Drop(BlockKind.Column, 5, 7),
                new Drop(BlockKind.Pediment, 2, 0),
            }),
            // A palace: four rooms on a wide base, towers at the corners, a temple roof over the middle
            new Template("TEMPLATE_PALACE", new[]
            {
                new Drop(BlockKind.Steps, 0, 0), new Drop(BlockKind.Steps, 4, 0), new Drop(BlockKind.Steps, 0, 4), new Drop(BlockKind.Steps, 4, 4),
                new Drop(BlockKind.SquareRoom, 0, 0, Paint: BONE), new Drop(BlockKind.SquareRoom, 4, 0, Paint: BONE),
                new Drop(BlockKind.SquareRoom, 0, 4, Paint: BONE), new Drop(BlockKind.SquareRoom, 4, 4, Paint: BONE),
                new Drop(BlockKind.RoundTower, 0, 0, Paint: STONE), new Drop(BlockKind.RoundTower, 6, 0, Paint: STONE),
                new Drop(BlockKind.RoundTower, 0, 6, Paint: STONE), new Drop(BlockKind.RoundTower, 6, 6, Paint: STONE),
                new Drop(BlockKind.Pediment, 2, 0, Paint: OCHRE),
                new Drop(BlockKind.Banner, 0, 0, Paint: RED), new Drop(BlockKind.Banner, 7, 7, Paint: RED),
            }),
        },
        [BuildingKind.Watchtower] = new[]
        {
            // The ladders go up before the platform: listed after it they would land on top of it
            new Template("TEMPLATE_WOOD_TOWER", new[]
            {
                new Drop(BlockKind.Stilts, 3, 3),
                new Drop(BlockKind.Stilts, 3, 3),
                new Drop(BlockKind.Stilts, 3, 3),
                new Drop(BlockKind.Ladder, 5, 3),
                new Drop(BlockKind.Ladder, 5, 3),
                new Drop(BlockKind.Ladder, 5, 3),
                new Drop(BlockKind.Platform, 2, 2),
                new Drop(BlockKind.Stilts, 3, 3),
                new Drop(BlockKind.SmallConeRoof, 3, 3),
                new Drop(BlockKind.Palisade, 2, 0),
                new Drop(BlockKind.Palisade, 2, 7),
                new Drop(BlockKind.Palisade, 0, 2, Turns: 1),
                new Drop(BlockKind.Palisade, 7, 2, Turns: 1),
            }),
            new Template("TEMPLATE_STONE_TOWER", new[]
            {
                new Drop(BlockKind.RoundTower, 3, 3),
                new Drop(BlockKind.RoundTower, 3, 3),
                new Drop(BlockKind.RoundTower, 3, 3),
                new Drop(BlockKind.Door, 5, 3),
                new Drop(BlockKind.Platform, 2, 2, Paint: STONE),
                new Drop(BlockKind.Stilts, 3, 3),
                new Drop(BlockKind.SmallConeRoof, 3, 3, Paint: STONE),
                new Drop(BlockKind.Torch, 2, 6),
                new Drop(BlockKind.Crate, 6, 1),
            }),
        },
    };

    public static int Count(BuildingKind kind)
    {
        return TEMPLATES[kind].Length;
    }

    /// <summary>Translation key of a template name.</summary>
    /// <param name="kind">(Required) The function.</param>
    /// <param name="index">(Required) 0..Count(kind).</param>
    public static string NameKey(BuildingKind kind, int index)
    {
        return TEMPLATES[kind][index].NameKey;
    }

    /// <summary>A new design of a function from one of its templates.</summary>
    /// <param name="kind">(Required) The function.</param>
    /// <param name="index">(Required) 0..Count(kind).</param>
    /// <returns>The design with every block of the template.</returns>
    /// <example><code>hut.Setup(BuildingTemplates.Build(BuildingKind.Dwelling, 1), faction: 2);</code></example>
    public static BuildingDesign Build(BuildingKind kind, int index)
    {
        Template[] templates = TEMPLATES[kind];
        if (index < 0 || index >= templates.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), $"index must be 0..{templates.Length - 1} for {kind}, got '{index}'.");
        }
        var design = new BuildingDesign(kind);
        int toMiddle = (BuildingDesign.GRID_SIZE - TEMPLATE_SIZE) / 2;
        foreach (Drop drop in templates[index].Drops)
        {
            if (!design.TryPlace(drop.Kind, drop.X + toMiddle, drop.Z + toMiddle, drop.Turns, drop.Paint))
            {
                throw new System.InvalidOperationException($"Template '{templates[index].NameKey}' must fit the grid, {drop.Kind} at ({drop.X}, {drop.Z}) does not.");
            }
        }
        return design;
    }

    /// <summary>The first template of a function: what a new tribe builds.</summary>
    public static BuildingDesign Default(BuildingKind kind)
    {
        return Build(kind, 0);
    }
}
