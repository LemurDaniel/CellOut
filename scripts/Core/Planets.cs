// NOTE:
//   The planets of the galaxy menu (Game/Ui/Menu/), one entry per planet in data/planets.json:
//   where it lies in the Milky Way, how its globe looks from space, how much land its
//   archipelago has and how its world is colored once played (sky, sun, water, sand and
//   the green of every plant). Current is the planet of the running game: the coast
//   (Creature/Coast.cs), the camera (World/FollowCamera.cs), the water palette and the
//   shader globals of the vegetation (Use) read it. Pure data, parsing calls no engine:
//   Parse and Load take the JSON text, only the default read goes through Godot (FileAccess).
//
//   Planets.Choose("RUBRA");                       // in the menu, before the start
//   int islands = Planets.Current.Islands;
//   Color sky   = Planets.Current.SkyTopColor;
//
//   An entry of data/planets.json (colors as [r, g, b], 0..1, sRGB):
//     "key": "RUBRA",             PLANET_<KEY> and PLANET_<KEY>_DESC in locale/translations.csv
//     "galaxy": [0.12, -0.62],    position on the disk of the galaxy, -1..1 (0 the core)
//     "globeSize": 0.95,          size of the marker and the globe, 1 = normal
//     "ocean", "land", "highland", "sand", "atmosphere"   colors of the globe
//     "landShare": 0.5,           share of land on the globe, 0..1
//     "clouds": 0.35, "ice": 0.0  cloud cover and size of the ice caps, 0..1
//     "islands": 6,               islands of the archipelago (at most Coast.MAX_ISLANDS - 1 besides the landing island)
//     "landScale": 1.1,           radius of every island against the default
//     "foliage": [r, g, b],       the green of grass, leaves and ferns turns towards this color ...
//     "foliageAmount": 0.85,      ... this far (0 keeps the green)
//     "skyTop", "skyHorizon", "sun"   colors of the sky and the sun light over the land
//     "water", "sandTint"         multipliers on the colors of the water and the sand (1 keeps them)
//     "bands": 0                  optional, stripes of a gas giant on the globe (0 none .. 1)
//     "star": "OrangeDwarf"       optional, the star of its system (StarKind), a Sun by default

//
//   Chapters:
//     TYPES     One planet and its colors.
//     PLANETS   The table, the chosen planet and the shader globals.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   Planet   One entry of data/planets.json, colors as float[3] plus Color helpers.
//

public sealed record Planet
{
    public required string Key { get; init; }
    public required float[] Galaxy { get; init; }
    public required float GlobeSize { get; init; }
    public required float[] Ocean { get; init; }
    public required float[] Land { get; init; }
    public required float[] Highland { get; init; }
    public required float[] Sand { get; init; }
    public required float[] Atmosphere { get; init; }
    public required float LandShare { get; init; }
    public required float Clouds { get; init; }
    public required float Ice { get; init; }
    public required int Islands { get; init; }
    public required float LandScale { get; init; }
    public required float[] Foliage { get; init; }
    public required float FoliageAmount { get; init; }
    public required float[] SkyTop { get; init; }
    public required float[] SkyHorizon { get; init; }
    public required float[] Sun { get; init; }
    public required float[] Water { get; init; }
    public required float[] SandTint { get; init; }
    public float Bands { get; init; }                // 0 a rocky globe .. 1 a gas giant of stripes (the neighbors of Core/StarSystem.cs)
    public string Star { get; init; } = nameof(CellOut.StarKind.Sun);   // the star of its system (StarKind)

    public StarKind StarKind => Enum.Parse<StarKind>(Star);

    public Vector2 GalaxySpot => new(Galaxy[0], Galaxy[1]);
    public Color OceanColor => ColorOf(Ocean);
    public Color LandColor => ColorOf(Land);
    public Color HighlandColor => ColorOf(Highland);
    public Color SandColor => ColorOf(Sand);
    public Color AtmosphereColor => ColorOf(Atmosphere);
    public Color FoliageColor => ColorOf(Foliage);
    public Color SkyTopColor => ColorOf(SkyTop);
    public Color SkyHorizonColor => ColorOf(SkyHorizon);
    public Color SunColor => ColorOf(Sun);
    public Color WaterTint => ColorOf(Water);
    public Color SandTintColor => ColorOf(SandTint);

    /// <summary>A color from an [r, g, b] entry.</summary>
    public static Color ColorOf(float[] rgb)
    {
        return new Color(rgb[0], rgb[1], rgb[2]);
    }
}

public static class Planets
{
    // ===========================================================================
    // ===============================   PLANETS   ===============================
    // ===========================================================================
    //
    //   FILE_PATH   The table of the game.
    //   All         Every planet, in the order of the file (the first is the default).
    //   Current     The planet of the running game, the first one until the menu chooses.
    //   Load        Replaces the table, for unit tests without Godot.
    //   Parse       Parses and checks the table.
    //   Choose      Makes a planet current by its key.
    //   Get         A planet by its key.
    //   Use         Hands the colors of the current planet to the shader globals.
    //

    public const string FILE_PATH = "res://data/planets.json";
    public const int MAX_ISLANDS = 7;                // besides the landing island, CoastTerrain.MAX_ISLANDS - 1

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    private static IReadOnlyList<Planet> _all;
    private static Planet _current;

    public static IReadOnlyList<Planet> All => _all ??= Parse(ReadProjectFile());

    public static Planet Current => _current ??= All[0];

    /// <summary>Replaces the table, for unit tests that run without Godot.</summary>
    /// <param name="json">(Required) Content of data/planets.json.</param>
    public static void Load(string json)
    {
        _all     = Parse(json);
        _current = null;
    }

    /// <summary>Parses and checks the table: unique keys, colors with three channels, shares in 0..1, room for the islands.</summary>
    /// <param name="json">(Required) Content of data/planets.json.</param>
    public static IReadOnlyList<Planet> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("json must be the content of data/planets.json, got an empty text.", nameof(json));
        }
        List<Planet> planets;
        try
        {
            planets = JsonSerializer.Deserialize<List<Planet>>(json, JSON_OPTIONS);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"data/planets.json must be valid JSON with known fields: {error.Message}", error);
        }
        if (null == planets || 0 == planets.Count)
        {
            throw new InvalidOperationException("data/planets.json must list at least one planet.");
        }
        foreach (Planet planet in planets)
        {
            Check(planet);
        }
        string twice = planets.GroupBy(planet => planet.Key).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (null != twice)
        {
            throw new InvalidOperationException($"data/planets.json must list every key once, got '{twice}' twice.");
        }
        return planets;
    }

    private static void Check(Planet planet)
    {
        float[][] colors = { planet.Ocean, planet.Land, planet.Highland, planet.Sand, planet.Atmosphere, planet.Foliage, planet.SkyTop, planet.SkyHorizon, planet.Sun, planet.Water, planet.SandTint };
        if (colors.Any(color => 3 != color.Length) || 2 != planet.Galaxy.Length)
        {
            throw new InvalidOperationException($"Planet '{planet.Key}' must have colors of three channels and a galaxy spot of two.");
        }
        bool isShared = planet.LandShare is >= 0f and <= 1f && planet.Clouds is >= 0f and <= 1f && planet.Ice is >= 0f and <= 1f && planet.FoliageAmount is >= 0f and <= 1f;
        if (!Enum.TryParse<StarKind>(planet.Star, out _))
        {
            throw new InvalidOperationException($"Planet '{planet.Key}' must name a star of StarKind, got '{planet.Star}'.");
        }
        if (!isShared || planet.Islands < 1 || planet.Islands > MAX_ISLANDS || planet.LandScale <= 0f)
        {
            throw new InvalidOperationException($"Planet '{planet.Key}' must keep its shares in 0..1, 1..{MAX_ISLANDS} islands and a positive land scale.");
        }
    }

    /// <summary>Makes a planet the one of the next game.</summary>
    /// <param name="key">(Required) Key of a planet in data/planets.json.</param>
    /// <example><code>Planets.Choose("THALASSA");</code></example>
    public static void Choose(string key)
    {
        _current = Get(key);
    }

    /// <summary>A planet by its key.</summary>
    /// <param name="key">(Required) Key of a planet in data/planets.json.</param>
    public static Planet Get(string key)
    {
        return All.FirstOrDefault(planet => planet.Key == key)
            ?? throw new ArgumentException($"key must be a planet of data/planets.json, got '{key}'.", nameof(key));
    }

    /// <summary>The colors of the current planet for every scenery shader (shaders/planet.gdshaderinc).</summary>
    public static void Use()
    {
        Planet planet = Current;
        RenderingServer.GlobalShaderParameterSet("planet_foliage", planet.FoliageColor);
        RenderingServer.GlobalShaderParameterSet("planet_foliage_amount", planet.FoliageAmount);
        RenderingServer.GlobalShaderParameterSet("planet_water", planet.WaterTint);
        RenderingServer.GlobalShaderParameterSet("planet_sand", planet.SandTintColor);
    }

    /// <summary>Reads the file through Godot, so it also works from an exported pack.</summary>
    private static string ReadProjectFile()
    {
        string json = Godot.FileAccess.GetFileAsString(FILE_PATH);
        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException($"{FILE_PATH} must exist and be readable, Godot reported '{Godot.FileAccess.GetOpenError()}'.");
        }
        return json;
    }
}
