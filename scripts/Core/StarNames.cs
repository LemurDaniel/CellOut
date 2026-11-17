// NOTE:
//   Names of the star systems of the galaxy (Core/Galaxy.cs) and their planets: every
//   system gets a pronounceable name built from syllables, seeded by its key, so the same
//   star keeps its name in every run and later in the space phase. Planets are named like
//   real exoplanets: the name of their star plus a small letter by orbit order (b, c, d ...).
//   Pure logic, no engine. Shown by the hover card of the galaxy menu (Game/Ui/Menu/SystemCard.cs).
//
//   string star   = StarNames.Of("S-042");            // e.g. "Velaris"
//   string planet = StarNames.PlanetOf("S-042", 1);   // e.g. "Velaris c"

//
//   Chapters:
//     NAMES   Syllables, the name of a system and of its planets.
//

using System;

namespace CellOut;

public static class StarNames
{
    // ===========================================================================
    // ================================   NAMES   ================================
    // ===========================================================================
    //
    //   STARTS / MIDDLES / ENDS   The syllables, empty middles keep some names short.
    //   BRIDGES                   Middles for names that came out too short.
    //   Of                        The name of a system by its key.
    //   PlanetOf                  The name of the n-th planet of a system.
    //   Join                      Two syllables without a clash of vowels.
    //

    private const int MIN_LENGTH = 5;

    private static readonly string[] STARTS =
    {
        "Al", "Ast", "Be", "Cael", "Cor", "Del", "Er", "Fen", "Gal", "Hy", "Ix", "Kal", "Ly", "Mor",
        "Nor", "Or", "Pel", "Qua", "Ri", "Sel", "Tar", "Thal", "Ul", "Vel", "Wen", "Xan", "Yr", "Zel",
    };

    private static readonly string[] MIDDLES =
    {
        "a", "e", "i", "o", "ra", "ri", "lo", "ne", "ta", "mi", "do", "ve", "sa", "phe", "ga", "li", "", "", "",
    };

    private static readonly string[] ENDS =
    {
        "n", "s", "x", "ris", "on", "is", "us", "ia", "ar", "eth", "ix", "or", "um", "el", "an", "ea",
    };

    private static readonly string[] BRIDGES = { "ra", "lo", "ne", "ta", "mi", "do", "ve", "sa", "ru", "ka" };

    /// <summary>The name of a star system, always the same for the same key.</summary>
    /// <param name="key">(Required) The key of the system (StarSystem.Key), e.g. "S-042" or "TERRA".</param>
    /// <example><code>StarNames.Of("S-042")   // a name like "Velaris"</code></example>
    public static string Of(string key)
    {
        var    dice   = new Random(StarSystem.SeedOf(key) ^ 0x5EED);
        string start  = STARTS[dice.Next(STARTS.Length)];
        string middle = MIDDLES[dice.Next(MIDDLES.Length)];
        string end    = ENDS[dice.Next(ENDS.Length)];
        string bridge = BRIDGES[dice.Next(BRIDGES.Length)];
        string name   = Join(Join(start, middle), end);
        // Very short names ("Ril") take a bridge syllable that starts with a consonant instead
        return name.Length >= MIN_LENGTH ? name : Join(Join(start, bridge), end);
    }

    /// <summary>Two syllables in a row, the second loses its first vowel when both meet with a vowel ("Ri" + "el" = "Ril").</summary>
    private static string Join(string first, string second)
    {
        if (0 == second.Length || 0 == first.Length || !IsVowel(first[^1]) || !IsVowel(second[0]))
        {
            return first + second;
        }
        return second.Length > 1 ? first + second[1..] : first;
    }

    /// <summary>The name of a planet: its star plus a small letter by orbit order, the first planet is "b".</summary>
    /// <param name="key">(Required) The key of the system.</param>
    /// <param name="index">(Required) The planet's place among the planets of its system, 0 nearest the star.</param>
    /// <example><code>StarNames.PlanetOf("S-042", 0)   // "Velaris b"</code></example>
    public static string PlanetOf(string key, int index)
    {
        return $"{Of(key)} {(char)('b' + Math.Clamp(index, 0, 24))}";
    }

    private static bool IsVowel(char letter)
    {
        return "aeiouy".Contains(char.ToLowerInvariant(letter));
    }
}
