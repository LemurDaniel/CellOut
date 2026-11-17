// NOTE:
//   Unit tests for the tribe: the stock (Tribe/Village/TribeEconomy.cs), the blueprint of
//   a building with its blocks and the effects of its function, the templates
//   (Tribe/Building/) and a sitting in the village builder
//   (Tribe/Building/Editor/BuildingEditorSession.cs) and the ways and paths (Tribe/TribeWays.cs).
//   The expected values belong to
//   tests/unit/balance.fixture.json (tribe.buildings).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter TribeTests

//
//   Chapters:
//     KEEP THE STOCK        Gathering, chopping and the refusal of an empty stock.
//     DESIGN THE BUILDING   Effects of the function, dropping, stacking and removing blocks, templates.
//     SIT IN THE BUILDER    Picking, centering, dropping and the result of a sitting.
//     WALK A PATH           The three ways of the pack and the paths of the tribe (Tribe/TribeWays.cs).
//

using Xunit;

namespace CellOut.Tests;

public class TribeTests
{
    private const int GRID = BuildingDesign.GRID_SIZE;

    // ===========================================================================
    // ===========================   KEEP THE STOCK   ============================
    // ===========================================================================
    //
    //   StockStartsEmpty            No food, no wood, no buildings, a dwelling cannot be built.
    //   GatheringPaysForHuts        A dwelling costs 6 food and 3 wood, the stock drops by it.
    //   OverspendIsRefused          Short of wood the building is refused and nothing changes.
    //   NegativeGatheringIsAnError  Food and wood gathered are never negative.
    //

    [Fact]
    public void StockStartsEmpty()
    {
        var economy = new TribeEconomy();

        Assert.Equal(0, economy.Food);
        Assert.Equal(0, economy.Wood);
        Assert.Equal(0, economy.Huts);
        Assert.False(economy.CanAfford(new BuildingDesign(BuildingKind.Dwelling)));
    }

    [Fact]
    public void GatheringPaysForHuts()
    {
        var economy = new TribeEconomy();
        var design  = new BuildingDesign(BuildingKind.Dwelling);
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

        Assert.False(economy.BuildHut(new BuildingDesign(BuildingKind.Dwelling)));
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
    // =========================   DESIGN THE BUILDING   =========================
    // ===========================================================================
    //
    //   FunctionSetsCostAndEffect   Each function has its own cost and one effect, blocks change neither.
    //   BlocksStackWhereTheyDrop    A roof dropped on a room lands on top of it, cells answer with their block.
    //   BlocksStayOnTheGrid         Outside the grid and above its top nothing is placed, a turn swaps the footprint.
    //   RemovingLetsBlocksFall      Without the room the roof lies on the ground.
    //   TemplatesFitEveryFunction   Every template builds, the default dwelling is room, roof and door.
    //

    [Fact]
    public void FunctionSetsCostAndEffect()
    {
        var dwelling = new BuildingDesign(BuildingKind.Dwelling);
        Assert.Equal(6, dwelling.FoodCost);
        Assert.Equal(3, dwelling.WoodCost);
        Assert.Equal(30f, dwelling.Health);
        Assert.Equal(2, dwelling.Villagers);
        Assert.Equal(0f, dwelling.Defense);

        Assert.True(dwelling.TryPlace(BlockKind.SquareRoom, 2, 2));
        Assert.True(dwelling.TryPlace(BlockKind.Crate, 0, 0));
        Assert.Equal(6, dwelling.FoodCost);
        Assert.Equal(3, dwelling.WoodCost);

        Assert.Equal(1, new BuildingDesign(BuildingKind.Storehouse).FoodPerFruit);
        Assert.Equal(0.5f, new BuildingDesign(BuildingKind.Market).TradeBonus);
        Assert.Equal(0.1f, new BuildingDesign(BuildingKind.Totem).RelationBonus, 4);
        Assert.Equal(20f, new BuildingDesign(BuildingKind.Watchtower).Defense);
        Assert.Equal(8, new BuildingDesign(BuildingKind.Watchtower).WoodCost);
        Assert.Equal(0, new BuildingDesign(BuildingKind.Watchtower).Villagers);
        Assert.Equal(2, new BuildingDesign(BuildingKind.Barracks).Warriors);
        Assert.Equal(0, new BuildingDesign(BuildingKind.Dwelling).Warriors);
    }

    [Fact]
    public void BlocksStackWhereTheyDrop()
    {
        var design = new BuildingDesign(BuildingKind.Dwelling);

        Assert.True(design.TryPlace(BlockKind.RoundRoom, 2, 2));
        Assert.True(design.TryPlace(BlockKind.ConeRoof, 2, 2, paint: 7));
        Assert.True(design.TryPlace(BlockKind.Crate, 0, 0));

        Assert.Equal(0, design.Blocks[0].Y);
        Assert.Equal(2, design.Blocks[1].Y);
        Assert.Equal(7, design.Blocks[1].Paint);
        Assert.Equal(0, design.Blocks[2].Y);
        Assert.Equal(4, design.Height);
        Assert.Equal(0, design.BlockAt(3, 1, 3));
        Assert.Equal(1, design.BlockAt(5, 3, 5));
        Assert.Equal(2, design.BlockAt(0, 0, 0));
        Assert.Equal(-1, design.BlockAt(GRID - 1, 0, GRID - 1));
        Assert.Equal(-1, design.BlockAt(3, 4, 3));
    }

    [Fact]
    public void BlocksStayOnTheGrid()
    {
        var design = new BuildingDesign(BuildingKind.Watchtower);

        Assert.False(design.TryPlace(BlockKind.RoundRoom, GRID - 3, GRID - 3));
        Assert.False(design.TryPlace(BlockKind.Crate, -1, 0));
        Assert.Null(design.RestHeight(BlockKind.Crate, 0, GRID));

        // The palisade is four cells along X, a quarter turn lays it along Z
        Assert.True(design.TryPlace(BlockKind.Palisade, GRID - 4, GRID - 1));
        Assert.False(design.TryPlace(BlockKind.Palisade, GRID - 3, GRID - 1));
        Assert.True(design.TryPlace(BlockKind.Palisade, GRID - 1, 0, turns: 1));
        Assert.False(design.TryPlace(BlockKind.Palisade, GRID - 1, GRID - 3, turns: 1));

        // Towers of two cells fill the height of the grid, one more sticks out
        for (int i = 0; i < BuildingDesign.GRID_HEIGHT / 2; i++)
        {
            Assert.True(design.TryPlace(BlockKind.RoundTower, 3, 3));
        }
        Assert.Equal(BuildingDesign.GRID_HEIGHT, design.Height);
        Assert.False(design.TryPlace(BlockKind.RoundTower, 3, 3));
        Assert.Equal(0, design.RestHeight(BlockKind.Horns, 0, 0).Value);
    }

    [Fact]
    public void RemovingLetsBlocksFall()
    {
        var design = new BuildingDesign(BuildingKind.Dwelling);
        design.TryPlace(BlockKind.RoundRoom, 2, 2);
        design.TryPlace(BlockKind.ConeRoof, 2, 2);
        design.TryPlace(BlockKind.Horns, 3, 3);

        Assert.Equal(4, design.Blocks[2].Y);
        Assert.False(design.RemoveAt(3));
        Assert.True(design.RemoveAt(0));
        Assert.Equal(2, design.Blocks.Count);
        Assert.Equal(BlockKind.ConeRoof, design.Blocks[0].Kind);
        Assert.Equal(0, design.Blocks[0].Y);
        Assert.Equal(2, design.Blocks[1].Y);

        var copy = new BuildingDesign(BuildingKind.Market);
        copy.CopyFrom(design);
        Assert.Equal(BuildingKind.Dwelling, copy.Kind);
        Assert.Equal(design.Blocks, copy.Blocks);
    }

    [Fact]
    public void TemplatesFitEveryFunction()
    {
        foreach (BuildingKind kind in System.Enum.GetValues<BuildingKind>())
        {
            Assert.True(BuildingTemplates.Count(kind) >= 2);
            for (int i = 0; i < BuildingTemplates.Count(kind); i++)
            {
                BuildingDesign design = BuildingTemplates.Build(kind, i);
                Assert.Equal(kind, design.Kind);
                Assert.True(design.Blocks.Count >= 3, $"{BuildingTemplates.NameKey(kind, i)} has {design.Blocks.Count} block(s).");
                Assert.InRange(design.Height, 2, BuildingDesign.GRID_HEIGHT);
            }
        }

        BuildingDesign hut = BuildingTemplates.Default(BuildingKind.Dwelling);
        Assert.Equal("TEMPLATE_ROUND_HUT", BuildingTemplates.NameKey(BuildingKind.Dwelling, 0));
        Assert.Equal(new[] { BlockKind.RoundRoom, BlockKind.ConeRoof, BlockKind.Door }, System.Linq.Enumerable.Select(hut.Blocks, block => block.Kind));
        Assert.Equal(4, hut.Height);

        // The wooden tower: three stilts, platform, lookout and roof
        Assert.Equal(11, BuildingTemplates.Build(BuildingKind.Watchtower, 0).Height);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => BuildingTemplates.Build(BuildingKind.Totem, 9));
    }

    // ===========================================================================
    // =========================   SIT IN THE BUILDER   ==========================
    // ===========================================================================
    //
    //   SittingCopiesTheDesigns       Drafts are copies, the tribe's designs stay untouched until the result is taken.
    //   PickedBlockCentersOnTheCell   A four cell block centers on the pointed cell and stays inside the grid.
    //   TurnAndPaintGoWithTheBlock    R turns the next block, paint and pattern are stored and repaint a placed block, a wrong paint falls back.
    //   EmptyDraftFallsBackToTemplate A cleared draft results in the first template of its function.
    //

    [Fact]
    public void SittingCopiesTheDesigns()
    {
        var mine = new BuildingDesign(BuildingKind.Market);
        mine.TryPlace(BlockKind.Stall, 3, 3);
        var session = new BuildingEditorSession();
        int changes = 0;
        session.Changed += () => changes++;

        session.Begin(kind => BuildingKind.Market == kind ? mine : BuildingTemplates.Default(kind));
        session.ShowKind(BuildingKind.Market);
        session.Select(BlockKind.Awning);

        Assert.True(session.PlaceAt(3, 3));
        Assert.Equal(3, changes);
        Assert.Equal(2, session.Draft.Blocks.Count);
        Assert.Equal(1, session.Draft.Blocks[1].Y);
        Assert.Single(mine.Blocks);
        Assert.Equal(2, session.Result(BuildingKind.Market).Blocks.Count);

        Assert.True(session.Remove(0));
        Assert.Equal(0, session.Draft.Blocks[0].Y);
        Assert.False(session.Remove(4));
    }

    [Fact]
    public void PickedBlockCentersOnTheCell()
    {
        var session = new BuildingEditorSession();

        Assert.Null(session.AnchorFor(4, 4));
        Assert.False(session.PlaceAt(4, 4));

        session.Select(BlockKind.RoundRoom);
        Assert.Equal((3, 3), session.AnchorFor(4, 4));
        Assert.Equal((0, GRID - 4), session.AnchorFor(0, GRID - 1));

        session.Select(BlockKind.Crate);
        Assert.Equal((GRID - 1, 0), session.AnchorFor(GRID - 1, 0));

        session.Select(BlockKind.Crate);
        Assert.Null(session.Selected);
    }

    [Fact]
    public void TurnAndPaintGoWithTheBlock()
    {
        var session = new BuildingEditorSession();
        session.Clear();
        session.Select(BlockKind.Palisade);
        session.Rotate();
        session.PickPaint(2);
        session.PickPattern(3);

        Assert.Equal(1, session.Turns);
        Assert.Equal((7, 3), session.AnchorFor(7, 4));
        Assert.True(session.PlaceAt(7, 4));
        Assert.Equal(new PlacedBlock(BlockKind.Palisade, 7, 0, 3, 1, 2, 3), session.Draft.Blocks[0]);

        // Another paint and pattern repaint the placed block, a pattern outside the list is clamped
        session.PickPaint(8);
        session.PickPattern(99);
        Assert.True(session.Repaint(0));
        Assert.Equal(new PlacedBlock(BlockKind.Palisade, 7, 0, 3, 1, 8, BlockCatalog.PATTERN_COUNT - 1), session.Draft.Blocks[0]);
        Assert.False(session.Repaint(5));

        session.PickPaint(99);
        Assert.Equal(BlockCatalog.DEFAULT_PAINT, session.Paint);
        for (int i = 0; i < 3; i++)
        {
            session.Rotate();
        }
        Assert.Equal(0, session.Turns);
    }

    [Fact]
    public void EmptyDraftFallsBackToTemplate()
    {
        var session = new BuildingEditorSession();
        session.ShowKind(BuildingKind.Totem);
        session.ApplyTemplate(1);
        Assert.Equal(8, session.Draft.Blocks.Count);

        session.Clear();

        Assert.Empty(session.Draft.Blocks);
        Assert.Equal(BuildingTemplates.Default(BuildingKind.Totem).Blocks, session.Result(BuildingKind.Totem).Blocks);
    }

    // ===========================================================================
    // =============================   WALK A PATH   =============================
    // ===========================================================================
    //
    //   DietLocksThePath          Herbivore pacifist, balanced territorial, carnivore warlike, the edges at +-0.33.
    //   EachPathGrewFromAWay      Pacifist courts, territorial displays, warlike hunts.
    //   TheFittingWayIsStronger   Court 2 for a pure herbivore, hunt 1 for it, display 2 when balanced.
    //   DisplayNeedsMoreBody      More body wins, a tie does not.
    //   EachPathHasItsGoal        Allies, respect or defeat count, a defeated rival never counts for peace.
    //   NoRivalsIsNoVictory       Without rivals no path is won.
    //

    [Fact]
    public void DietLocksThePath()
    {
        Assert.Equal(TribePath.Pacifist, TribeWays.PathOf(-0.5f));
        Assert.Equal(TribePath.Territorial, TribeWays.PathOf(0f));
        Assert.Equal(TribePath.Territorial, TribeWays.PathOf(0.33f));
        Assert.Equal(TribePath.Warlike, TribeWays.PathOf(0.34f));
    }

    [Fact]
    public void EachPathGrewFromAWay()
    {
        Assert.Equal(PackWay.Court, TribeWays.WayOf(TribePath.Pacifist));
        Assert.Equal(PackWay.Display, TribeWays.WayOf(TribePath.Territorial));
        Assert.Equal(PackWay.Hunt, TribeWays.WayOf(TribePath.Warlike));
    }

    [Fact]
    public void TheFittingWayIsStronger()
    {
        Assert.Equal(2f, TribeWays.Strength(PackWay.Court, -1f), 4);
        Assert.Equal(1f, TribeWays.Strength(PackWay.Hunt, -1f), 4);
        Assert.Equal(2f, TribeWays.Strength(PackWay.Display, 0f), 4);
        Assert.Equal(1.5f, TribeWays.Strength(PackWay.Display, 0.5f), 4);
    }

    [Fact]
    public void DisplayNeedsMoreBody()
    {
        Assert.True(TribeWays.DisplayWins(4f, 3f));
        Assert.False(TribeWays.DisplayWins(3f, 3f));
    }

    [Fact]
    public void EachPathHasItsGoal()
    {
        var ally      = new RivalStanding(0.9f, 0f, IsDefeated: false);
        var respected = new RivalStanding(0f, 0.7f, IsDefeated: false);
        var defeated  = new RivalStanding(0.9f, 0.7f, IsDefeated: true);

        Assert.True(TribeWays.IsWon(TribePath.Pacifist, new[] { ally, ally }));
        Assert.False(TribeWays.IsWon(TribePath.Pacifist, new[] { ally, defeated }));
        Assert.Equal((1, 2), TribeWays.Progress(TribePath.Territorial, new[] { respected, ally }));
        Assert.True(TribeWays.IsWon(TribePath.Warlike, new[] { defeated }));
        Assert.False(TribeWays.IsWon(TribePath.Warlike, new[] { defeated, respected }));
    }

    [Fact]
    public void NoRivalsIsNoVictory()
    {
        Assert.False(TribeWays.IsWon(TribePath.Warlike, System.Array.Empty<RivalStanding>()));
    }
}
