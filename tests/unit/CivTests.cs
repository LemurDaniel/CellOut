// NOTE:
//   Unit tests for the civilization: the tech tree (Civ/TechTree.cs) with what a tech needs,
//   when it can be researched and what researched techs add up to, and the headquarters
//   (BuildingKind.Hall) with its templates. The expected values belong to
//   tests/unit/balance.fixture.json (civ.techs, tribe.buildings.Hall).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter CivTests

//
//   Chapters:
//     RESEARCH THE TREE   Order, costs and needs of the techs.
//     ADD UP BONUSES      What researched techs do together.
//     RAISE THE HALL      The headquarters, its costs and its templates.
//

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CellOut.Tests;

public class CivTests
{
    // ===========================================================================
    // ==========================   RESEARCH THE TREE   ==========================
    // ===========================================================================
    //
    //   TreeListsEveryTechInOrder   TECHS is in enum order, three tiers of three rows.
    //   FirstTierIsOpen             The first tier needs nothing, costs 10 and is open with 10 knowledge.
    //   LaterTiersNeedTheirRow      Masonry needs Pottery, Philosophy also Writing.
    //   ResearchedOnceOnly          A researched tech cannot be researched again.
    //

    [Fact]
    public void TreeListsEveryTechInOrder()
    {
        Assert.Equal(System.Enum.GetValues<TechKind>().Length, TechTree.TECHS.Count);
        Assert.All(TechTree.TECHS, tech => Assert.Equal(tech.Kind, TechTree.Get(tech.Kind).Kind));
        Assert.All(TechTree.TECHS, tech => Assert.InRange(tech.Tier, 0, TechTree.TIERS - 1));
        Assert.Equal(TechTree.TIERS * TechTree.ROWS, TechTree.TECHS.Select(tech => (tech.Tier, tech.Row)).Distinct().Count());
    }

    [Fact]
    public void FirstTierIsOpen()
    {
        var none = new HashSet<TechKind>();

        Assert.Equal(10, TechTree.CostOf(TechKind.Cooling));
        Assert.True(TechTree.CanResearch(TechKind.Cooling, none, 10f));
        Assert.False(TechTree.CanResearch(TechKind.Cooling, none, 9.9f));
    }

    [Fact]
    public void LaterTiersNeedTheirRow()
    {
        var pottery = new HashSet<TechKind> { TechKind.Cooling };
        var masonry = new HashSet<TechKind> { TechKind.Cooling, TechKind.Concrete };

        Assert.False(TechTree.CanResearch(TechKind.Concrete, new HashSet<TechKind>(), 100f));
        Assert.True(TechTree.CanResearch(TechKind.Concrete, pottery, 25f));
        Assert.False(TechTree.CanResearch(TechKind.University, masonry, 100f));
        masonry.Add(TechKind.Tractor);
        masonry.Add(TechKind.Computer);
        Assert.True(TechTree.CanResearch(TechKind.University, masonry, 45f));
    }

    [Fact]
    public void ResearchedOnceOnly()
    {
        var pottery = new HashSet<TechKind> { TechKind.Cooling };

        Assert.False(TechTree.CanResearch(TechKind.Cooling, pottery, 100f));
    }

    // ===========================================================================
    // ============================   ADD UP BONUSES   ===========================
    // ===========================================================================
    //
    //   NothingResearchedChangesNothing   No tech: the neutral bonuses.
    //   EveryTechAddsItsAmount            The amounts of civ.techs land where TechTree says.
    //

    [Fact]
    public void NothingResearchedChangesNothing()
    {
        Assert.Equal(CivBonuses.NONE, TechTree.BonusesOf(new HashSet<TechKind>()));
    }

    [Fact]
    public void EveryTechAddsItsAmount()
    {
        var all = new HashSet<TechKind>(System.Enum.GetValues<TechKind>());

        CivBonuses bonuses = TechTree.BonusesOf(all);

        Assert.Equal(1, bonuses.FoodPerFruit);
        Assert.Equal(1.5f, bonuses.Damage);
        Assert.Equal(2f, bonuses.FarmFoodPerMinute);
        Assert.Equal(2f, bonuses.BuildingHealth);
        Assert.True(bonuses.CanSail);
        Assert.Equal(2.2f * 1.5f, bonuses.ShipSpeed, 4);
        Assert.Equal(1.5f, bonuses.Knowledge);
        Assert.Equal(1f, bonuses.KnowledgePerBuilding);
        Assert.Equal(2f, bonuses.Trade);
    }

    // ===========================================================================
    // ============================   RAISE THE HALL   ===========================
    // ===========================================================================
    //
    //   HallCostsAndThinks     30 food, 30 wood, 6 knowledge per minute.
    //   HallTemplatesStand     Temple and palace build, every block lands on the grid.
    //

    [Fact]
    public void HallCostsAndThinks()
    {
        BuildingDesign hall = BuildingTemplates.Default(BuildingKind.Hall);

        Assert.Equal(30, hall.FoodCost);
        Assert.Equal(30, hall.WoodCost);
        Assert.Equal(6f, hall.Knowledge);
    }

    [Fact]
    public void HallTemplatesStand()
    {
        Assert.Equal(2, BuildingTemplates.Count(BuildingKind.Hall));
        for (int i = 0; i < BuildingTemplates.Count(BuildingKind.Hall); i++)
        {
            BuildingDesign design = BuildingTemplates.Build(BuildingKind.Hall, i);
            Assert.Contains(design.Blocks, block => BlockKind.RoofDeck == block.Kind);
            Assert.True(design.Blocks.Count >= 8, $"template {i} kept only {design.Blocks.Count} blocks");
        }
    }
}
