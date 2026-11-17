// NOTE:
//   Unit tests for Game/Deep/DeepRule.cs: spawn size, placement between plane and
//   sea floor, surfacing and the rise of the giant cells. The expected values belong
//   to tests/unit/balance.fixture.json (critter.giants).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter DeepTests

//
//   Chapters:
//     FLOAT IN THE DEPTH   Size, placement, surfacing and the rise.
//

using Xunit;

namespace CellOut.Tests;

public class DeepTests
{
    // ===========================================================================
    // =========================   FLOAT IN THE DEPTH   ==========================
    // ===========================================================================
    //
    //   SpawnSizeSpansTheRange     4 to 8 player sizes, the roll clamped to 0..1.
    //   TallGiantIsSquashed        A giant of 6 player sizes squashes to a third, hangs 15.5 deep and faint.
    //   SmallGiantKeepsItsShape    Once it fits the band it keeps its shape, its top below the plane.
    //   SurfacesAtTheRatio         Ready at 2.2 player sizes, not above.
    //   RiseEndsOnThePlane         Starts in the band, ends at depth 0 in its own shape and fully visible after 5 seconds.
    //

    private const int PRECISION = 3;

    [Fact]
    public void SpawnSizeSpansTheRange()
    {
        Assert.Equal(40f, DeepRule.SpawnSize(10f, 0f), PRECISION);
        Assert.Equal(60f, DeepRule.SpawnSize(10f, 0.5f), PRECISION);
        Assert.Equal(80f, DeepRule.SpawnSize(10f, 2f), PRECISION);
    }

    [Fact]
    public void TallGiantIsSquashed()
    {
        DeepPose pose = DeepRule.Placement(60f, 10f);

        Assert.Equal(1f / 3f, pose.Squash, PRECISION);
        Assert.Equal(15.5f, pose.Depth, PRECISION);
        Assert.Equal(DeepRule.DEEP_FADE, pose.Fade, PRECISION);
    }

    [Fact]
    public void SmallGiantKeepsItsShape()
    {
        DeepPose pose = DeepRule.Placement(20f, 20f);

        Assert.Equal(1f, pose.Squash, PRECISION);
        Assert.Equal(19f, pose.Depth, PRECISION);     // 0.35 * 20 + 0.6 * 20
        Assert.True(pose.Depth - 0.6f * 20f > 0f);    // the top stays below the plane
    }

    [Fact]
    public void SurfacesAtTheRatio()
    {
        Assert.True(DeepRule.IsReadyToSurface(44f, 20f));
        Assert.False(DeepRule.IsReadyToSurface(45f, 20f));
    }

    [Fact]
    public void RiseEndsOnThePlane()
    {
        DeepPose start = DeepRule.Rise(44f, 20f, 0f);
        DeepPose end   = DeepRule.Rise(44f, 20f, 5f);

        Assert.Equal(DeepRule.Placement(44f, 20f), start);
        Assert.Equal(0f, end.Depth, PRECISION);
        Assert.Equal(0f, end.Fade, PRECISION);
        Assert.Equal(1f, end.Squash, PRECISION);
        Assert.False(DeepRule.HasRisen(4.9f));
        Assert.True(DeepRule.HasRisen(5f));
    }
}
