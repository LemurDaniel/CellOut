// NOTE:
//   What a building function is called: a few functions change their name once the tribe
//   is a city (wish of the user 2026-11-18): the totem becomes the radio station, the
//   warrior hut the barracks, the dwelling a house. The function, its costs and effect stay
//   the same. Society sets IsModern from the stage, every text asks KeyOf. Pure logic.
//
//   BuildingNames.IsModern = civ.IsCity;
//   label.Text = Tr(BuildingNames.KeyOf(BuildingKind.Totem));   // "BUILDING_TOTEM_MODERN" in a city

//
//   Chapters:
//     NAMES   The translation key of a function, by era.
//

using System.Collections.Generic;

namespace CellOut;

public static class BuildingNames
{
    // ===========================================================================
    // ================================   NAMES   ================================
    // ===========================================================================
    //
    //   IsModern   Whether the game plays in the city: the modern names apply.
    //   KeyOf      The translation key of the name of a function.
    //

    /// <summary>Functions with a name of their own in the city, the others keep theirs.</summary>
    private static readonly HashSet<BuildingKind> RENAMED = new() { BuildingKind.Totem, BuildingKind.Barracks, BuildingKind.Dwelling };

    /// <summary>(Optional) True from the city on, Society keeps it up to date. Defaults to false.</summary>
    public static bool IsModern { get; set; }

    /// <summary>The translation key of the name of a function in the current era.</summary>
    /// <param name="kind">(Required) The function.</param>
    /// <example><code>BuildingNames.KeyOf(BuildingKind.Market)   // "BUILDING_MARKET" in every era</code></example>
    public static string KeyOf(BuildingKind kind)
    {
        string key = $"BUILDING_{kind}".ToUpperInvariant();
        return IsModern && RENAMED.Contains(kind) ? key + "_MODERN" : key;
    }
}
