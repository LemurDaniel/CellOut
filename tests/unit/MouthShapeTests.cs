// NOTE:
//   Tests of the mouth openings (scripts/Creature/Design/MouthShapes.cs): the file of the
//   game loads, a typo fails loudly and a design finds the opening of its mouth part.
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter MouthShapeTests

using System;
using Xunit;

namespace CellOut.Tests;

public class MouthShapeTests
{
    [Fact]
    public void GameFileLoads()
    {
        var table = MouthShapes.Parse(TestSetup.ReadRepoFile(TestSetup.MOUTH_FILE));

        Assert.NotEmpty(table);
        Assert.All(table.Values, shape => Assert.InRange(shape.Gape, 0f, 1f));
        Assert.All(table.Values, shape => Assert.True(shape.Length > 0f, "length must be positive, the corners sit behind the nose tip"));
    }

    [Fact]
    public void OnlyMouthsCutAnOpening()
    {
        Assert.Throws<InvalidOperationException>(() => MouthShapes.Parse("""{ "Eye": { "gape": 0.1 } }"""));
    }

    [Fact]
    public void TypoInAFieldFails()
    {
        Assert.Throws<InvalidOperationException>(() => MouthShapes.Parse("""{ "Jaw": { "gap": 0.1 } }"""));
    }

    [Fact]
    public void DesignFindsTheOpeningOfItsMouth()
    {
        CreatureDesign plain = CreatureTemplates.Plain(CreatureType.Fish).Build();
        Assert.Same(MouthShapes.PLAIN, MouthShapes.For(plain));     // no mouth part: still a closed line as a face

        CreatureDesign shark = CreatureTemplates.Get("SHARK").Build();
        MouthShape     shape = MouthShapes.For(shark);
        Assert.NotNull(shape);
        Assert.InRange(shape.CornerX(shark), (shark.TailTipX + shark.NoseTipX) * 0.5f, shark.NoseTipX);
    }
}