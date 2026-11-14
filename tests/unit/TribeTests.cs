// NOTE:
//   Unit tests for Tribe/Village/TribeEconomy.cs and HutDesign.cs: the stock of the
//   tribe, the costs and effects of a hut blueprint, paying for huts. The expected
//   values belong to tests/unit/balance.fixture.json (tribe.huts).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter TribeTests

//
//   Chapters:
//     KEEP THE STOCK     Gathering, chopping and the refusal of an empty stock.
//     DESIGN THE HUT     Costs and effects summed from wall, roof and add-ons.
//

using Xunit;

namespace CellOut.Tests;

public class TribeTests
{
    // ===========================================================================
    // ============================   KEEP THE STOCK   ===========================
    // ===========================================================================
    //
    //   StockStartsEmpty            No food, no wood, no huts, the default hut cannot be built.
    //   GatheringPaysForHuts        Clay and grass cost 6 food and 3 wood, the stock drops by it.
    //   OverspendIsRefused          Short of wood the hut is refused and nothing changes.
    //   NegativeGatheringIsAnError  Food and wood gathered are never negative.
    //

    [Fact]
    public void StockStartsEmpty()
    {
        var economy = new TribeEconomy();

        Assert.Equal(0, economy.Food);
        Assert.Equal(0, economy.Wood);
        Assert.Equal(0, economy.Huts);
        Assert.False(economy.CanAfford(new HutDesign()));
    }

    [Fact]
    public void GatheringPaysForHuts()
    {
        var economy = new TribeEconomy();
        var design  = new HutDesign();
        economy.AddFood(10);
        economy.AddWood(4);

        Assert.True(economy.CanAfford(design));
        Assert.True(economy.BuildHut(design));
        Assert.Equal(4, economy.Food);
        Assert.Equal(1, economy.Wood);
        Assert.Equal(1, economy.Huts);
        Assert.False(economy.CanAfford(design));
    }

    [Fact]
    public void OverspendIsRefused()
    {
        var economy = new TribeEconomy();
        economy.AddFood(20);
        economy.AddWood(2);

        Assert.False(economy.BuildHut(new HutDesign()));
        Assert.Equal(20, economy.Food);
        Assert.Equal(2, economy.Wood);
        Assert.Equal(0, economy.Huts);
        Assert.False(economy.Spend(0, 3));
        Assert.True(economy.Spend(5, 0));
        Assert.Equal(15, economy.Food);
    }

    [Fact]
    public void NegativeGatheringIsAnError()
    {
        var economy = new TribeEconomy();

        Assert.Throws<System.ArgumentOutOfRangeException>(() => economy.AddFood(-1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => economy.AddWood(-1));
    }

    // ===========================================================================
    // ============================   DESIGN THE HUT   ===========================
    // ===========================================================================
    //
    //   DefaultHutIsClayAndGrass     6 food, 3 wood, 30 health, 1 villager, no bonus.
    //   StoneAndShinglesCostMore     Stone and shingles: 10 food, 10 wood, 60 health, 3 villagers.
    //   AddonsStackTheirEffects      Palisade and watchtower defend 30, market trades +0.5, storehouse +1 food per fruit.
    //   ToggleSwitchesAnAddon        On, off, copied.
    //

    [Fact]
    public void DefaultHutIsClayAndGrass()
    {
        var design = new HutDesign();

        Assert.Equal(WallKind.Clay, design.Wall);
        Assert.Equal(RoofKind.Grass, design.Roof);
        Assert.Equal(6, design.FoodCost);
        Assert.Equal(3, design.WoodCost);
        Assert.Equal(30f, design.Health);
        Assert.Equal(1, design.Villagers);
        Assert.Equal(0f, design.Defense);
        Assert.Equal(0, design.FoodPerFruit);
    }

    [Fact]
    public void StoneAndShinglesCostMore()
    {
        var design = new HutDesign { Wall = WallKind.Stone, Roof = RoofKind.Shingle };

        Assert.Equal(10, design.FoodCost);
        Assert.Equal(10, design.WoodCost);
        Assert.Equal(60f, design.Health);
        Assert.Equal(3, design.Villagers);
    }

    [Fact]
    public void AddonsStackTheirEffects()
    {
        var design = new HutDesign();
        design.Toggle(HutAddon.Palisade);
        design.Toggle(HutAddon.Watchtower);
        design.Toggle(HutAddon.Market);
        design.Toggle(HutAddon.Storehouse);

        Assert.Equal(30f, design.Defense);
        Assert.Equal(0.5f, design.TradeBonus);
        Assert.Equal(1, design.FoodPerFruit);
        Assert.Equal(6 + 2 + 3 + 4 + 3, design.FoodCost);
        Assert.Equal(3 + 8 + 6 + 3 + 4, design.WoodCost);
    }

    [Fact]
    public void ToggleSwitchesAnAddon()
    {
        var design = new HutDesign();

        Assert.True(design.Toggle(HutAddon.Totem));
        Assert.True(design.Has(HutAddon.Totem));
        Assert.Equal(0.1f, design.RelationBonus, 4);
        Assert.False(design.Toggle(HutAddon.Totem));
        Assert.False(design.Has(HutAddon.Totem));

        design.Toggle(HutAddon.Palisade);
        var copy = new HutDesign();
        copy.CopyFrom(design);
        Assert.True(copy.Has(HutAddon.Palisade));
        Assert.Equal(design.WoodCost, copy.WoodCost);
    }
}
