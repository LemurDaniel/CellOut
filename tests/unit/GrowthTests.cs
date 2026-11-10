// NOTE:
//   Unit tests for Game/Growth.cs: body size, level curve, division steps, prey size
//   and lost cells. The expected values belong to tests/unit/balance.fixture.json.
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter GrowthTests

//
//   Chapters:
//     GROW THE PLAYER   Size, levels, division, prey and lost cells.
//

using Xunit;

namespace CellOut.Tests;

public class GrowthTests
{
    // ===========================================================================
    // ===========================   GROW THE PLAYER   ===========================
    // ===========================================================================
    //
    //   BodySizeFollowsRootOfBiomass   10 at the start, 26 at biomass 100, never below the start.
    //   LevelsGetRarer                 First level 45 after the single cell, then x2.1 + 30.
    //   DivisionNeedsMoreEachTime      100, 250, ... and 24 414 for the step after 64 cells.
    //   JawAndDivisionsAllowBiggerPrey 1.15 normally, 0.95 with a jaw, 0.05 less per division, floor 0.75.
    //   DeadlyHitBurstsAQuarter        16 of 64 cells, at least one.
    //

    private const int PRECISION = 3;

    [Fact]
    public void BodySizeFollowsRootOfBiomass()
    {
        Assert.Equal(10f, Growth.BodySize(0f), PRECISION);
        Assert.Equal(26f, Growth.BodySize(100f), PRECISION);
        Assert.Equal(10f, Growth.BodySize(-5f), PRECISION);
    }

    [Fact]
    public void LevelsGetRarer()
    {
        float first  = Growth.FirstLevelAt(40f);
        float second = Growth.NextLevelAt(first);

        Assert.Equal(85f, first, PRECISION);
        Assert.Equal(208.5f, second, PRECISION);
        Assert.Equal(467.85f, Growth.NextLevelAt(second), PRECISION);
    }

    [Fact]
    public void DivisionNeedsMoreEachTime()
    {
        Assert.Equal(100f, Growth.DivisionAt(0), PRECISION);
        Assert.Equal(250f, Growth.DivisionAt(1), PRECISION);
        Assert.Equal(24414.0625f, Growth.DivisionAt(6), 1);
    }

    [Fact]
    public void JawAndDivisionsAllowBiggerPrey()
    {
        Assert.Equal(1.15f, Growth.EatRatio(hasJawToward: false, divisions: 0), PRECISION);
        Assert.Equal(0.95f, Growth.EatRatio(hasJawToward: true, divisions: 0), PRECISION);
        Assert.Equal(0.9f, Growth.EatRatio(hasJawToward: false, divisions: 5), PRECISION);
        Assert.Equal(0.75f, Growth.EatRatio(hasJawToward: true, divisions: 6), PRECISION);
    }

    [Fact]
    public void DeadlyHitBurstsAQuarter()
    {
        Assert.Equal(16, Growth.CellsLost(64));
        Assert.Equal(1, Growth.CellsLost(4));
        Assert.Equal(1, Growth.CellsLost(2));
    }
}
