// NOTE:
//   A star system of the galaxy: its star (sun, orange, red or white dwarf, blue giant) and
//   its planets on up to ORBITS orbits. A playable system (Of) carries a planet of
//   data/planets.json on its home orbit and three to five neighbors (gas and ice giants
//   outside, lava, ice and rock worlds inside); a wild system (Wild) has 0 to 6 planets and
//   nothing to play yet. Neither neighbors nor wild systems can be chosen in the galaxy menu,
//   a later space phase is meant to reach them, so every body has a stable key and the same
//   look in every run: systems are seeded by their key. Pure logic, no engine; the menu
//   draws it (Game/Ui/Menu/GalaxySystemsLayer.cs, StarSystemView.cs), Core/Galaxy.cs places them.
//
//   StarSystem home = StarSystem.Of(Planets.Get("RUBRA"));
//   StarSystem wild = StarSystem.Wild("S-042", new Vector2(0.4f, -0.3f));
//   Color star      = StarSystem.ColorOf(home.Star);

//
//   Chapters:
//     TYPES    Star kinds, neighbor kinds, a neighbor and the system.
//     SYSTEM   Seeds the planets of a playable or a wild system.
//     STARS    Color and size of a star kind.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   StarKind       The star in the middle of a system.
//   NeighborKind   What a planet without life is made of.
//   Neighbor       One planet: key, kind, orbit (0 nearest the star), size against a playable
//                  planet, where on the orbit it starts (radians) and its look (a Planet record for the globe).
//                  Giants may wear a ring (HasRing).
//   Moon           A moon of the playable planet: key, size against it, distance in its radii, speed, start, look.
//   StarSystem     Key, place on the disk, star, the home orbit of a playable planet and the other planets.
//

public enum StarKind { Sun, OrangeDwarf, RedDwarf, WhiteDwarf, BlueGiant }

public enum NeighborKind { GasGiant, IceGiant, Lava, Ice, Rock }

public sealed class StarSystem
{
    public readonly record struct Neighbor(string Key, NeighborKind Kind, int Orbit, float Size, float Start, Planet Look)
    {
        /// <summary>About every third giant wears a ring, the same one in every run.</summary>
        public bool HasRing => (Kind is NeighborKind.GasGiant or NeighborKind.IceGiant) && 0 == Math.Abs(SeedOf(Key) % RING_SHARE);
    }

    public readonly record struct Moon(string Key, float Size, float Distance, float Speed, float Start, Planet Look);

    private const int RING_SHARE = 3;

    public string Key { get; }

    public Vector2 Spot { get; }

    public StarKind Star { get; }

    /// <summary>Orbit of the playable planet, -1 in a wild system.</summary>
    public int HomeOrbit { get; }

    /// <summary>Key of the playable planet, null in a wild system.</summary>
    public string PlanetKey { get; }

    public IReadOnlyList<Neighbor> Neighbors { get; }

    /// <summary>Moons of the playable planet, empty in a wild system.</summary>
    public IReadOnlyList<Moon> Moons { get; }

    private StarSystem(string key, Vector2 spot, StarKind star, int homeOrbit, string planetKey, IReadOnlyList<Neighbor> neighbors, IReadOnlyList<Moon> moons)
    {
        Moons     = moons;
        Key       = key;
        Spot      = spot;
        Star      = star;
        HomeOrbit = homeOrbit;
        PlanetKey = planetKey;
        Neighbors = neighbors;
    }

    // ===========================================================================
    // ===============================   SYSTEM   ================================
    // ===========================================================================
    //
    //   ORBITS   Orbits of a system.
    //   Of       The system of a playable planet: its star from the file, the planet on the
    //            second or third orbit, three to five neighbors, the same for the same key.
    //   Wild     A system without life: a star by the dice (red dwarfs most often) and 0 to 6 planets.
    //   MoonsOf  Zero to two moons of the playable planet: size against it, distance in its radii, speed in radians per second.
    //   SeedOf   A stable seed from a key (string hashes change per run).
    //   KindAt   Rocky inside, giants outside.
    //   LookOf   The globe look of a neighbor kind, varied by the dice.
    //

    public const int ORBITS = 6;
    public const int MAX_MOONS = 2;
    private const string MOON_SALT = "/moons";

    /// <summary>The star system of a playable planet, always the same for the same planet.</summary>
    /// <param name="planet">(Required) The playable planet.</param>
    /// <example><code>StarSystem.Of(Planets.Current).Neighbors.Count  // 3..5</code></example>
    public static StarSystem Of(Planet planet)
    {
        var dice  = new Random(SeedOf(planet.Key));
        int home  = dice.Next(1, 3);                              // second or third orbit, warm enough for life
        int count = dice.Next(3, 6);
        List<int> free = Enumerable.Range(0, ORBITS).Where(orbit => orbit != home).OrderBy(_ => dice.Next()).Take(count).OrderBy(orbit => orbit).ToList();
        return new StarSystem(planet.Key, planet.GalaxySpot, planet.StarKind, home, planet.Key, Seed(planet.Key, planet, free, home, dice), MoonsOf(planet));
    }

    /// <summary>A star system without life, always the same for the same key.</summary>
    /// <param name="key">(Required) Its key, e.g. "S-042".</param>
    /// <param name="spot">(Required) Its place on the disk of the galaxy, -1..1.</param>
    /// <param name="isOld">(Optional) A system of the bulge: old stars, no blue giants. Defaults to false.</param>
    public static StarSystem Wild(string key, Vector2 spot, bool isOld = false)
    {
        var    dice = new Random(SeedOf(key));
        double roll = dice.NextDouble() * (isOld ? 0.9 : 1.0);
        StarKind star = roll < 0.42 ? StarKind.RedDwarf : roll < 0.64 ? StarKind.OrangeDwarf : roll < 0.82 ? StarKind.Sun : roll < 0.93 ? StarKind.WhiteDwarf : StarKind.BlueGiant;
        int count = StarKind.WhiteDwarf == star ? dice.Next(0, 3) : dice.Next(0, 7);
        List<int> orbits = Enumerable.Range(0, ORBITS).OrderBy(_ => dice.Next()).Take(count).OrderBy(orbit => orbit).ToList();
        Planet look = Planets.All[0];
        return new StarSystem(key, spot, star, -1, null, Seed(key, look, orbits, 2, dice), Array.Empty<Moon>());
    }

    private static List<Neighbor> Seed(string key, Planet look, List<int> orbits, int home, Random dice)
    {
        var neighbors = new List<Neighbor>();
        for (int i = 0; i < orbits.Count; i++)
        {
            int          orbit   = orbits[i];
            NeighborKind kind    = KindAt(orbit, home, dice);
            bool         isGiant = kind is NeighborKind.GasGiant or NeighborKind.IceGiant;
            float        size    = isGiant ? 1.2f + (float)dice.NextDouble() * 0.6f : 0.4f + (float)dice.NextDouble() * 0.35f;
            string       name    = $"{key}_{(char)('B' + i)}";
            neighbors.Add(new Neighbor(name, kind, orbit, size, (float)(dice.NextDouble() * Math.PI * 2.0), LookOf(look, name, kind, dice)));
        }
        return neighbors;
    }

    /// <summary>Zero to two moons of rock or ice, on their own dice so the planets of the system stay as they were.</summary>
    private static List<Moon> MoonsOf(Planet planet)
    {
        var dice  = new Random(SeedOf(planet.Key + MOON_SALT));
        int count = dice.Next(0, MAX_MOONS + 1);
        var moons = new List<Moon>();
        for (int i = 0; i < count; i++)
        {
            string       key  = $"{planet.Key}_M{i + 1}";
            NeighborKind kind = dice.NextDouble() < 0.7 ? NeighborKind.Rock : NeighborKind.Ice;
            float        size = 0.08f + (float)dice.NextDouble() * 0.08f;
            float        far  = 1.5f + i * 0.55f + (float)dice.NextDouble() * 0.25f;
            float        fast = (0.25f + (float)dice.NextDouble() * 0.15f) / (i + 1f);
            moons.Add(new Moon(key, size, far, fast, (float)(dice.NextDouble() * Math.PI * 2.0), LookOf(planet, key, kind, dice)));
        }
        return moons;
    }

    private static NeighborKind KindAt(int orbit, int home, Random dice)
    {
        if (orbit < home)
        {
            return dice.NextDouble() < 0.6 ? NeighborKind.Lava : NeighborKind.Rock;
        }
        if (orbit >= 4)
        {
            return dice.NextDouble() < 0.55 ? NeighborKind.GasGiant : NeighborKind.IceGiant;
        }
        return dice.NextDouble() < 0.5 ? NeighborKind.Rock : NeighborKind.Ice;
    }

    /// <summary>A seed that stays the same for a key in every run.</summary>
    public static int SeedOf(string key)
    {
        int seed = 17;
        foreach (char letter in key)
        {
            seed = unchecked(seed * 31 + letter);
        }
        return seed;
    }

    private static Planet LookOf(Planet home, string key, NeighborKind kind, Random dice)
    {
        float Vary(float value) => Math.Clamp(value + ((float)dice.NextDouble() - 0.5f) * 0.12f, 0f, 1f);
        float[] Rgb(float r, float g, float b) => new[] { Vary(r), Vary(g), Vary(b) };

        // Every neighbor is a dry or banded globe: no foliage, no sea unless lava or ice
        Planet look = home with { Key = key, Clouds = 0f, Ice = 0f, LandShare = 1f, Bands = 0f };
        Planet dry  = kind switch
        {
            NeighborKind.GasGiant => look with { Land = Rgb(0.86f, 0.7f, 0.5f), Highland = Rgb(0.62f, 0.42f, 0.3f), Sand = Rgb(0.9f, 0.8f, 0.65f), Atmosphere = Rgb(1f, 0.85f, 0.65f), Bands = 1f },
            NeighborKind.IceGiant => look with { Land = Rgb(0.55f, 0.78f, 0.92f), Highland = Rgb(0.38f, 0.6f, 0.85f), Sand = Rgb(0.7f, 0.85f, 0.95f), Atmosphere = Rgb(0.6f, 0.85f, 1f), Bands = 0.7f },
            NeighborKind.Lava     => look with { Ocean = Rgb(0.95f, 0.38f, 0.1f), Land = Rgb(0.2f, 0.14f, 0.13f), Highland = Rgb(0.32f, 0.2f, 0.16f), Sand = Rgb(0.45f, 0.2f, 0.12f), Atmosphere = Rgb(1f, 0.45f, 0.2f), LandShare = 0.7f },
            NeighborKind.Ice      => look with { Ocean = Rgb(0.5f, 0.65f, 0.8f), Land = Rgb(0.86f, 0.9f, 0.96f), Highland = Rgb(0.7f, 0.78f, 0.88f), Sand = Rgb(0.8f, 0.85f, 0.92f), Atmosphere = Rgb(0.8f, 0.9f, 1f), LandShare = 0.8f, Ice = 0.6f },
            _                     => look with { Land = Rgb(0.55f, 0.5f, 0.46f), Highland = Rgb(0.4f, 0.36f, 0.33f), Sand = Rgb(0.66f, 0.6f, 0.54f), Atmosphere = Rgb(0.7f, 0.68f, 0.66f) },
        };
        // A rock world has no seas: what would be sea is dark lowland, like the maria of a moon
        return NeighborKind.Rock == kind ? dry with { Ocean = dry.Highland } : dry;
    }

    // ===========================================================================
    // ================================   STARS   ================================
    // ===========================================================================
    //
    //   ColorOf   The light of a star kind.
    //   SizeOf    The size of a star kind against a sun.
    //   DotOf     The color of a planet drawn as a small dot.
    //

    /// <summary>The light of a star kind, sRGB.</summary>
    public static Color ColorOf(StarKind star)
    {
        return star switch
        {
            StarKind.OrangeDwarf => new Color(1f, 0.72f, 0.42f),
            StarKind.RedDwarf    => new Color(1f, 0.45f, 0.32f),
            StarKind.WhiteDwarf  => new Color(0.86f, 0.92f, 1f),
            StarKind.BlueGiant   => new Color(0.6f, 0.75f, 1f),
            _                    => new Color(1f, 0.92f, 0.68f),
        };
    }

    /// <summary>The size of a star kind against a sun, for drawing.</summary>
    public static float SizeOf(StarKind star)
    {
        return star switch
        {
            StarKind.OrangeDwarf => 0.85f,
            StarKind.RedDwarf    => 0.6f,
            StarKind.WhiteDwarf  => 0.4f,
            StarKind.BlueGiant   => 1.7f,
            _                    => 1f,
        };
    }

    /// <summary>The color of a planet drawn as a small dot: lava worlds glow, the others a little lighter than their ground, never black.</summary>
    /// <param name="planet">(Required) A neighbor of a system.</param>
    public static Color DotOf(Neighbor planet)
    {
        return NeighborKind.Lava == planet.Kind
            ? Planet.ColorOf(planet.Look.Ocean)
            : Planet.ColorOf(planet.Look.Land).Lerp(Colors.White, 0.25f);
    }
}
