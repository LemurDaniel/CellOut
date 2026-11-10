// NOTE:
//   Unit tests for Core/Balance.cs: the balance file of the game loads and broken
//   files fail with a message that names the problem.
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter BalanceTests

//
//   Chapters:
//     LOAD BALANCE FILE   The real file and three broken variants.
//

using System;
using Xunit;

namespace CellOut.Tests;

public class BalanceTests
{
    // ===========================================================================
    // ==========================   LOAD BALANCE FILE   ==========================
    // ===========================================================================
    //
    //   GameFileLoads             data/balance.json of the game parses with every table complete.
    //   FixtureLoads              The frozen fixture parses to the values the other tests expect.
    //   MissingPartIsNamed        A part kind without entry fails and names the kind.
    //   UnknownKeyFails           A typo in a key fails instead of being ignored.
    //   MissingValueIsNamed       A missing required number fails and names it.
    //   EmptyTextFails            An empty text is rejected.
    //

    [Fact]
    public void GameFileLoads()
    {
        Balance balance = Balance.Parse(TestSetup.ReadRepoFile(TestSetup.GAME_FILE));

        Assert.All(balance.Cell.Parts.Values, part => Assert.InRange(part.MinDiet, -1f, part.MaxDiet));
        Assert.All(balance.Creature.Parts.Values, part => Assert.True(part.Weight > 0f));
        Assert.True(balance.Player.LevelGrowth > 1f);
    }

    [Fact]
    public void FixtureLoads()
    {
        Balance balance = Balance.Parse(TestSetup.ReadRepoFile(TestSetup.FIXTURE_FILE));

        Assert.Equal(2.1f, balance.Player.LevelGrowth);
        Assert.Equal(40f, balance.Player.SingleCellAt);
        Assert.Equal(5, balance.Cell.Parts[PartKind.Jaw].Cost);
        Assert.Equal(0.2f, balance.Cell.Parts[PartKind.Jaw].MinDiet);
        Assert.Equal(1f, balance.Cell.Parts[PartKind.Jaw].MaxDiet);
        Assert.Equal(14f, balance.Creature.Parts[CreaturePartKind.Legs].Carry);
        Assert.Equal(1.05f, balance.Prelude.Mutations[MutationKind.Tough].Health);
    }

    [Fact]
    public void MissingPartIsNamed()
    {
        string json = TestSetup.ReadRepoFile(TestSetup.FIXTURE_FILE).Replace("\"Toxin\":", "\"Jaw\":");

        // The duplicate Jaw overwrites, Toxin is gone
        var error = Assert.Throws<InvalidOperationException>(() => Balance.Parse(json));
        Assert.Contains("cell.parts", error.Message);
        Assert.Contains("Toxin", error.Message);
    }

    [Fact]
    public void UnknownKeyFails()
    {
        string json = TestSetup.ReadRepoFile(TestSetup.FIXTURE_FILE).Replace("\"halfLife\":", "\"halfLive\": 1, \"halfLife\":");

        var error = Assert.Throws<InvalidOperationException>(() => Balance.Parse(json));
        Assert.Contains("halfLive", error.Message);
    }

    [Fact]
    public void MissingValueIsNamed()
    {
        string json = TestSetup.ReadRepoFile(TestSetup.FIXTURE_FILE).Replace("\"halfLife\": 90,", "");

        var error = Assert.Throws<InvalidOperationException>(() => Balance.Parse(json));
        Assert.Contains("halfLife", error.Message);
    }

    [Fact]
    public void EmptyTextFails()
    {
        Assert.Throws<ArgumentException>(() => Balance.Parse("  "));
    }
}
