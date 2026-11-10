// NOTE:
//   Unit tests for the pure world logic: the RNA template and chain of the prelude
//   (Prelude/Chain/) and the island outline of the coast (Creature/World/IslandShape.cs).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter WorldTests

//
//   Chapters:
//     PAIR THE TEMPLATE   Base pairing and the template that grows on demand.
//     PULL THE CHAIN      Adding, following, counting and breaking links.
//     OUTLINE ISLANDS     Shore radius, land depth, the way to the sea and placement.
//

using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;

namespace CellOut.Tests;

public class WorldTests
{
    private const int PRECISION = 4;

    // ===========================================================================
    // =========================   PAIR THE TEMPLATE   ===========================
    // ===========================================================================
    //
    //   BasesPairBothWays        A-U and G-C, in both directions.
    //   TemplateKeepsItsBases    A position rolls once and answers the same afterwards.
    //   WantedPairsTheTemplate   The wanted base is the partner of the template base.
    //

    [Fact]
    public void BasesPairBothWays()
    {
        Assert.Equal(RnaBase.U, RnaTemplate.Pair(RnaBase.A));
        Assert.Equal(RnaBase.A, RnaTemplate.Pair(RnaBase.U));
        Assert.Equal(RnaBase.C, RnaTemplate.Pair(RnaBase.G));
        Assert.Equal(RnaBase.G, RnaTemplate.Pair(RnaBase.C));
    }

    [Fact]
    public void TemplateKeepsItsBases()
    {
        var rolls    = new Queue<RnaBase>(new[] { RnaBase.A, RnaBase.G, RnaBase.C });
        var template = new RnaTemplate(rolls.Dequeue);

        Assert.Equal(RnaBase.C, template.At(2));
        Assert.Equal(RnaBase.A, template.At(0));
        Assert.Equal(RnaBase.G, template.At(1));
        Assert.Empty(rolls);
    }

    [Fact]
    public void WantedPairsTheTemplate()
    {
        var template = new RnaTemplate(() => RnaBase.G);

        Assert.Equal(RnaBase.C, template.WantedAt(0));
    }

    // ===========================================================================
    // ===========================   PULL THE CHAIN   ============================
    // ===========================================================================
    //
    //   LinksLineUpBehindTheHead   Each new link sits one spacing behind the last one.
    //   ChainFollowsTheHead        After the head moved, every link keeps the spacing.
    //   MutationsCountSeparately   Paired links count for the goal, mutations keep their trait.
    //   BiteCutsFromTheHitLink     The first link in the circle and everything behind it is lost.
    //

    private static RnaChain ChainOfFour()
    {
        var chain = new RnaChain(spacing: 0.5f, size: 2f);
        chain.Add(RnaBase.A, null, Vector2.Zero, 0f);
        chain.Add(RnaBase.U, MutationKind.Tough, Vector2.Zero, 0f);
        chain.Add(RnaBase.G, null, Vector2.Zero, 0f);
        chain.Add(RnaBase.C, MutationKind.Sluggish, Vector2.Zero, 0f);
        return chain;
    }

    [Fact]
    public void LinksLineUpBehindTheHead()
    {
        RnaChain chain = ChainOfFour();

        Assert.Equal(new Vector2(-1f, 0f), chain.Links[0].Position);
        Assert.Equal(new Vector2(-4f, 0f), chain.Links[3].Position);
    }

    [Fact]
    public void ChainFollowsTheHead()
    {
        RnaChain chain = ChainOfFour();
        var      head  = new Vector2(0f, 3f);

        chain.Follow(head, heading: Mathf.Pi * 0.5f);

        Assert.Equal(1f, chain.Links[0].Position.DistanceTo(head), PRECISION);
        for (int i = 1; i < chain.Count; i++)
        {
            Assert.Equal(1f, chain.Links[i].Position.DistanceTo(chain.Links[i - 1].Position), PRECISION);
        }
    }

    [Fact]
    public void MutationsCountSeparately()
    {
        RnaChain chain = ChainOfFour();

        Assert.Equal(4, chain.Count);
        Assert.Equal(2, chain.PairedCount);
        Assert.Equal(2, chain.MutationCount);
        Assert.Equal(new[] { MutationKind.Tough, MutationKind.Sluggish }, chain.Mutations);
    }

    [Fact]
    public void BiteCutsFromTheHitLink()
    {
        RnaChain chain = ChainOfFour();

        int hit = chain.FirstLinkAt(new Vector2(-3f, 0.1f), radius: 0.2f);
        IReadOnlyList<RnaChain.Link> lost = chain.CutAt(hit);

        Assert.Equal(2, hit);
        Assert.Equal(new[] { RnaBase.G, RnaBase.C }, lost.Select(link => link.Kind));
        Assert.Equal(2, chain.Count);
        Assert.Equal(new[] { MutationKind.Tough }, chain.Mutations);
        Assert.Equal(-1, chain.FirstLinkAt(new Vector2(-3f, 0f), radius: 0.2f));
    }

    // ===========================================================================
    // ===========================   OUTLINE ISLANDS   ===========================
    // ===========================================================================
    //
    //   SeedZeroIsRoundTowardsEast   Radius 10 at angle 0, never beyond the largest bulge.
    //   DepthIsSignedDistance        8 inside, -1 in the sea, the nearest island wins.
    //   ToSeaPushesOffTheShore       A land point moves straight out, a sea point stays.
    //   LandingFacesTheViewer        Shore point towards the viewer, normal onto the land.
    //   NeighborsKeepTheirGap        Both largest bulges plus the gap apart, IsFree agrees.
    //

    private static readonly Coast.Island ISLAND = new(Vector2.Zero, 10f, 0f);

    [Fact]
    public void SeedZeroIsRoundTowardsEast()
    {
        Assert.Equal(10f, IslandShape.Radius(ISLAND, 0f), PRECISION);
        for (float angle = 0f; angle < Mathf.Tau; angle += 0.1f)
        {
            Assert.InRange(IslandShape.Radius(new Coast.Island(Vector2.Zero, 10f, 3.7f), angle), 7.5f, 10f * IslandShape.MAX_BULGE);
        }
    }

    [Fact]
    public void DepthIsSignedDistance()
    {
        var islands = new[] { ISLAND, new Coast.Island(new Vector2(100f, 0f), 10f, 0f) };

        Assert.Equal(8f, IslandShape.LandDepth(islands, new Vector2(2f, 0f)), PRECISION);
        Assert.Equal(-1f, IslandShape.LandDepth(islands, new Vector2(11f, 0f)), PRECISION);
        Assert.Equal(8f, IslandShape.LandDepth(islands, new Vector2(102f, 0f)), PRECISION);
        Assert.Equal(float.MinValue, IslandShape.LandDepth(System.Array.Empty<Coast.Island>(), Vector2.Zero));
    }

    [Fact]
    public void ToSeaPushesOffTheShore()
    {
        var islands = new[] { ISLAND };

        Assert.True(IslandShape.ToSea(islands, new Vector2(4f, 0f), 1f).IsEqualApprox(new Vector2(11f, 0f)));
        Assert.True(IslandShape.ToSea(islands, new Vector2(30f, 2f), 1f).IsEqualApprox(new Vector2(30f, 2f)));
        Assert.True(IslandShape.ToSea(islands, Vector2.Zero, 1f).IsEqualApprox(new Vector2(11f, 0f)));
    }

    [Fact]
    public void LandingFacesTheViewer()
    {
        (Vector2 origin, Vector2 normal) = IslandShape.Landing(ISLAND, new Vector2(-20f, 0f));

        Assert.True(origin.IsEqualApprox(new Vector2(-IslandShape.Radius(ISLAND, Mathf.Pi), 0f)));
        Assert.True(normal.IsEqualApprox(Vector2.Right));
    }

    [Fact]
    public void NeighborsKeepTheirGap()
    {
        Vector2 center = IslandShape.NeighborCenter(ISLAND, radius: 6f, gap: 5f, angle: 0f);

        Assert.True(center.IsEqualApprox(new Vector2(25f, 0f)));
        Assert.True(IslandShape.IsFree(new[] { ISLAND }, center, 6f, player: new Vector2(0f, 200f), minGap: 5f));
        Assert.False(IslandShape.IsFree(new[] { ISLAND }, center - new Vector2(1f, 0f), 6f, player: new Vector2(0f, 200f), minGap: 5f));
        Assert.False(IslandShape.IsFree(new[] { ISLAND }, center, 6f, player: center, minGap: 5f));
    }
}
