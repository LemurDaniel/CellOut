// NOTE:
//   Turns a saved game (Core/Save/SaveGame.cs) into JSON and back: camel case, enums by
//   name, infinity allowed (the level threshold of a protocell is infinite), unknown fields
//   ignored so an older game still opens a newer file's known parts. Every file carries
//   VERSION; an older file goes through Migrate, a newer one is refused. Pure logic, no
//   engine: the unit tests check the round trip, Core/Save/SaveSlots.cs reads and writes the files.
//
//   string   json = SaveCodec.ToJson(game);
//   SaveGame back = SaveCodec.FromJson(json);

//
//   Chapters:
//     CODEC   Write, read, check the version, migrate.
//

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CellOut;

public static class SaveCodec
{
    // ===========================================================================
    // ================================   CODEC   ================================
    // ===========================================================================
    //
    //   VERSION    The layout written now; raise it with every change of the records, then extend Migrate.
    //   ToJson     The game as indented JSON, the version set.
    //   FromJson   The game from its JSON, migrated to VERSION.
    //   Migrate    Lifts an older layout step by step (1 to 2: the antique techs become the modern ones).
    //

    public const int VERSION = 2;

    /// <summary>The techs of the antique age (layout 1) and what each became in the modern age: the same place in the tree, the same effect.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> MODERN_TECHS = new()
    {
        ["Pottery"] = "Cooling", ["Bronze"] = "Steel", ["Farming"] = "Tractor", ["Masonry"] = "Concrete", ["Sailing"] = "Engine",
        ["Writing"] = "Computer", ["Philosophy"] = "University", ["Navigation"] = "Radar", ["Currency"] = "Bank",
    };

    private static readonly JsonSerializerOptions OPTIONS = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        WriteIndented          = true,
        NumberHandling         = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The game as JSON, stamped with VERSION.</summary>
    /// <param name="game">(Required) The saved game.</param>
    public static string ToJson(SaveGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return JsonSerializer.Serialize(game with { Version = VERSION }, OPTIONS);
    }

    /// <summary>The game from its JSON, lifted to the current layout.</summary>
    /// <param name="json">(Required) Content of a save file.</param>
    /// <returns>The game; throws when the text is no save or comes from a newer game.</returns>
    public static SaveGame FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("json must be the content of a save file, got an empty text.", nameof(json));
        }
        SaveGame game;
        try
        {
            game = JsonSerializer.Deserialize<SaveGame>(json, OPTIONS);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"A save file must be valid JSON: {error.Message}", error);
        }
        if (null == game?.Meta || null == game.Player)
        {
            throw new InvalidOperationException("A save file must hold a meta and a player block.");
        }
        if (game.Version > VERSION)
        {
            throw new InvalidOperationException($"The save file has layout {game.Version}, this game reads up to {VERSION}.");
        }
        return Migrate(game);
    }

    private static SaveGame Migrate(SaveGame game)
    {
        // Layout 1 to 2: the civilization turned from the antique to the modern age, its techs got new names.
        // (Antique building blocks of a saved blueprint are dropped when it is read, SaveData.ToBuilding.)
        if (1 == game.Version && null != game.Society?.Civ)
        {
            var techs = game.Society.Civ.Techs.ConvertAll(tech => (MODERN_TECHS.TryGetValue(tech, out string modern) ? modern : tech));
            game = game with { Society = game.Society with { Civ = game.Society.Civ with { Techs = techs } } };
        }
        return game with { Version = VERSION };
    }
}
