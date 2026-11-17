// NOTE:
//   Unit tests for the planets of the galaxy menu (data/planets.json, Core/Planets.cs) and
//   the start phases (Core/StartPhases.cs): the real file loads, every planet has its
//   texts, broken entries fail loudly, phases open by what was reached.
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter PlanetTests

//
//   Chapters:
//     PLANETS   The table of the game, its texts and its checks.
//     PHASES    Which phases are open, the stage of a phase and the jump into it.
//

using System;
using System.Linq;
using Xunit;

namespace CellOut.Tests;

public class PlanetTests
{
    // ===========================================================================
    // ===============================   PLANETS   ===============================
    // ===========================================================================
    //
    //   GameFileLoads            Five planets, unique keys, Terra first and current by default.
    //   EveryPlanetHasTexts      PLANET_<KEY> and PLANET_<KEY>_DESC in translations.csv.
    //   PlanetsDiffer            The water world has less land and fewer islands than the dry one.
    //   BrokenEntriesFail        A two-channel color, too many islands, an unknown field and a twin key are refused.
    //

    private static readonly string GAME_FILE = TestSetup.ReadRepoFile(TestSetup.PLANET_FILE);

    [Fact]
    public void GameFileLoads()
    {
        var planets = Planets.Parse(GAME_FILE);

        Assert.Equal(5, planets.Count);
        Assert.Equal("TERRA", planets[0].Key);
        Assert.Equal(planets.Count, planets.Select(planet => planet.Key).Distinct().Count());
        Assert.Equal("TERRA", Planets.Current.Key);
    }

    [Fact]
    public void EveryPlanetHasTexts()
    {
        string csv = TestSetup.ReadRepoFile(TestSetup.TRANSLATION_FILE);
        foreach (Planet planet in Planets.Parse(GAME_FILE))
        {
            Assert.Contains($"\nPLANET_{planet.Key},", csv);
            Assert.Contains($"\nPLANET_{planet.Key}_DESC,", csv);
        }
        foreach (StartPhase phase in Enum.GetValues<StartPhase>())
        {
            Assert.Contains($"\n{StartPhases.NameKey(phase)},", csv);
        }
    }

    [Fact]
    public void PlanetsDiffer()
    {
        var planets  = Planets.Parse(GAME_FILE);
        Planet water = planets.Single(planet => "THALASSA" == planet.Key);
        Planet dry   = planets.Single(planet => "ARIDA" == planet.Key);

        Assert.True(water.LandShare < dry.LandShare);
        Assert.True(water.Islands < dry.Islands);
        Assert.True(water.LandScale < dry.LandScale);
        Assert.True(planets.Single(planet => "RUBRA" == planet.Key).FoliageColor.R > 0.5f);
    }

    [Fact]
    public void BrokenEntriesFail()
    {
        string one = GAME_FILE.Substring(0, GAME_FILE.IndexOf("},", StringComparison.Ordinal) + 1) + "]";
        Assert.Single(Planets.Parse(one));

        Assert.Throws<InvalidOperationException>(() => Planets.Parse(one.Replace("\"ocean\": [0.08, 0.32, 0.62]", "\"ocean\": [0.08, 0.32]")));
        Assert.Throws<InvalidOperationException>(() => Planets.Parse(one.Replace("\"islands\": 6", "\"islands\": 9")));
        Assert.Throws<InvalidOperationException>(() => Planets.Parse(one.Replace("\"ice\": 0.12", "\"ice\": 0.12, \"lava\": 1")));
        Assert.Throws<InvalidOperationException>(() => Planets.Parse("[" + one.Trim('[', ']', '\n', '\r', ' ') + "," + one.Trim('[', ']', '\n', '\r', ' ') + "]"));
        Assert.Throws<ArgumentException>(() => Planets.Parse(""));
    }

    // ===========================================================================
    // ================================   PHASES   ===============================
    // ===========================================================================
    //
    //   RnaWorldIsAlwaysOpen   Nothing reached: only the RNA world; the land animal reached opens everything up to it.
    //   PhaseFollowsStage      The protocell is phase 1, the tribe the last one, out of range is clamped.
    //   JumpsLeadIntoPhases    The first two phases start without a jump, the others with the matching cheat.
    //   GalaxyIsAlwaysTheSame  Playable systems first, every wild one apart from the others, the same keys and stars every time.
    //   SystemsKeepTheirRules  A playable system holds its planet on the second or third orbit and 3..5 neighbors, a wild one 0..6 planets.
    //   PartsOpenByProgress    The healing moss opens on land, the ink sac once a pack was reached anywhere, the scent gland always.
    //

    [Fact]
    public void RnaWorldIsAlwaysOpen()
    {
        Assert.True(StartPhases.IsOpen(StartPhase.RnaWorld, StartPhase.RnaWorld));
        Assert.False(StartPhases.IsOpen(StartPhase.Protocell, StartPhase.RnaWorld));
        Assert.True(StartPhases.IsOpen(StartPhase.LandAnimal, StartPhase.LandAnimal));
        Assert.True(StartPhases.IsOpen(StartPhase.SingleCell, StartPhase.LandAnimal));
        Assert.False(StartPhases.IsOpen(StartPhase.Pack, StartPhase.LandAnimal));
    }

    [Fact]
    public void PhaseFollowsStage()
    {
        Assert.Equal(StartPhase.Protocell, StartPhases.OfStage(0));
        Assert.Equal(StartPhase.LandAnimal, StartPhases.OfStage(Player.LAND_STAGE));
        Assert.Equal(StartPhase.Tribe, StartPhases.OfStage(Player.TRIBE_STAGE));
        Assert.Equal(StartPhase.Civilization, StartPhases.OfStage(Player.CIVILIZATION_STAGE));
        Assert.Equal(StartPhase.Civilization, StartPhases.OfStage(42));
        Assert.Equal(StartPhases.COUNT, Enum.GetValues<StartPhase>().Length);
    }

    [Fact]
    public void JumpsLeadIntoPhases()
    {
        Assert.Null(StartPhases.JumpOf(StartPhase.RnaWorld));
        Assert.Null(StartPhases.JumpOf(StartPhase.Protocell));
        Assert.Equal(CheatTarget.Organism, StartPhases.JumpOf(StartPhase.Organism));
        Assert.Equal(CheatTarget.Tribe, StartPhases.JumpOf(StartPhase.Tribe));
        Assert.Equal(CheatTarget.Civilization, StartPhases.JumpOf(StartPhase.Civilization));
    }

    [Fact]
    public void GalaxyIsAlwaysTheSame()
    {
        var systems = Galaxy.Systems;

        Assert.InRange(systems.Count, Planets.All.Count + Galaxy.WILD_SYSTEMS + Galaxy.CORE_SYSTEMS / 2, Planets.All.Count + Galaxy.WILD_SYSTEMS + Galaxy.CORE_SYSTEMS);
        Assert.Equal("TERRA", systems[0].PlanetKey);
        Assert.All(systems.Skip(Planets.All.Count), system => Assert.Null(system.PlanetKey));
        Assert.Equal(systems.Count, systems.Select(system => system.Key).Distinct().Count());
        for (int i = Planets.All.Count; i < systems.Count; i++)
        {
            Assert.All(systems.Take(i), other => Assert.True(other.Spot.DistanceTo(systems[i].Spot) >= 0.032f));
        }
        StarSystem again = StarSystem.Wild(systems[40].Key, systems[40].Spot);
        Assert.Equal(systems[40].Star, again.Star);
        Assert.Equal(systems[40].Neighbors.Count, again.Neighbors.Count);
    }

    [Fact]
    public void SystemsKeepTheirRules()
    {
        StarSystem rubra = StarSystem.Of(Planets.Get("RUBRA"));
        Assert.InRange(rubra.HomeOrbit, 1, 2);
        Assert.InRange(rubra.Neighbors.Count, 3, 5);
        Assert.DoesNotContain(rubra.Neighbors, neighbor => neighbor.Orbit == rubra.HomeOrbit);
        Assert.Equal(StarKind.OrangeDwarf, rubra.Star);
        Assert.All(Galaxy.Systems, system => Assert.InRange(system.Neighbors.Count, 0, StarSystem.ORBITS));
    }

    [Fact]
    public void PartsOpenByProgress()
    {
        Assert.True(PartUnlocks.IsOpen(CreaturePartKind.ScentGland, Player.ORGANISM_STAGE, StartPhase.RnaWorld));
        Assert.Null(PartUnlocks.HintKey(CreaturePartKind.ScentGland));

        Assert.False(PartUnlocks.IsOpen(CreaturePartKind.HealingMoss, Player.ORGANISM_STAGE, StartPhase.Tribe));
        Assert.True(PartUnlocks.IsOpen(CreaturePartKind.HealingMoss, Player.LAND_STAGE, StartPhase.RnaWorld));
        Assert.Equal("CREATURE_LOCK_STAGE", PartUnlocks.HintKey(CreaturePartKind.HealingMoss));

        Assert.False(PartUnlocks.IsOpen(CreaturePartKind.InkSac, Player.TRIBE_STAGE, StartPhase.LandAnimal));
        Assert.True(PartUnlocks.IsOpen(CreaturePartKind.InkSac, Player.ORGANISM_STAGE, StartPhase.Pack));
        Assert.Equal("CREATURE_LOCK_REACHED", PartUnlocks.HintKey(CreaturePartKind.InkSac));
    }
}