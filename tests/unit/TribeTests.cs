// NOTE:
//   Unit tests for Tribe/Village/TribeEconomy.cs: the stock of the tribe, paying for
//   huts. The expected values belong to tests/unit/balance.fixture.json (tribe.village).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter TribeTests

//
//   Chapters:
//     KEEP THE STOCK   Gathering, hut cost and the refusal of an empty stock.
//

using Xunit;

namespace CellOut.Tests;

public class TribeTests
{
    // ===========================================================================
    // ============================   KEEP THE STOCK   ===========================
    // ===========================================================================
    //
    //   StockStartsEmpty            No food, no huts, a hut costs 8 and cannot be built.
    //   GatheringPaysForHuts        8 food build one hut, the stock drops by 8, a second needs 8 more.
    //   OverspendIsRefused          Short of food the hut is refused and nothing changes.
    //   NegativeGatheringIsAnError  Food gathered is never negative.
    //

    [Fact]
    public void StockStartsEmpty()
    {
        var economy = new TribeEconomy();

        Assert.Equal(0, economy.Food);
        Assert.Equal(0, economy.Huts);
        Assert.Equal(8, economy.HutCost);
        Assert.False(economy.CanBuildHut);
    }

    [Fact]
    public void GatheringPaysForHuts()
    {
        var economy = new TribeEconomy();
        for (int i = 0; i < 11; i++)
        {
            economy.AddFood(1);
        }

        Assert.True(economy.CanBuildHut);
        Assert.True(economy.BuildHut());
        Assert.Equal(3, economy.Food);
        Assert.Equal(1, economy.Huts);
        Assert.False(economy.CanBuildHut);

        economy.AddFood(5);
        Assert.True(economy.BuildHut());
        Assert.Equal(0, economy.Food);
        Assert.Equal(2, economy.Huts);
    }

    [Fact]
    public void OverspendIsRefused()
    {
        var economy = new TribeEconomy();
        economy.AddFood(7);

        Assert.False(economy.BuildHut());
        Assert.Equal(7, economy.Food);
        Assert.Equal(0, economy.Huts);
    }

    [Fact]
    public void NegativeGatheringIsAnError()
    {
        var economy = new TribeEconomy();

        Assert.Throws<System.ArgumentOutOfRangeException>(() => economy.AddFood(-1));
    }
}
