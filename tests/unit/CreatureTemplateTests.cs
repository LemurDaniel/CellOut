// NOTE:
//   Unit tests for the template library of the creature editor
//   (Creature/Design/CreatureTemplates.cs). The templates are content: the tests load
//   the real files of data/creatures (TestSetup) and check the rules every template
//   has to keep, not the look of a single animal.
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter CreatureTemplateTests

//
//   Chapters:
//     LOAD TEMPLATES    Every file of the game builds, is named and stays a shortcut.
//     PARSE TEMPLATES   What a file may and may not contain.
//

using System;
using System.Linq;
using Godot;
using Xunit;

namespace CellOut.Tests;

public class CreatureTemplateTests
{
    // ===========================================================================
    // ===========================   LOAD TEMPLATES   ============================
    // ===========================================================================
    //
    //   LibraryIsSortedAndNamed    Unique keys in card order, the plain bodies first, every name translated.
    //   EveryTemplateBuilds        Every file of data/creatures builds a body with a spine and parts.
    //   PlainBodiesAreFree         Fish and dolphin: the starter bodies, cost 0, no color of their own.
    //   TemplateCostsItsParts      The cost is the catalog price of every part that is not free.
    //   NewtIsReadyForTheLand      The newt stands on two pairs of legs, breathes air and walks.
    //   EveryBuildIsFresh          Two builds of a template share no parts list.
    //

    [Fact]
    public void LibraryIsSortedAndNamed()
    {
        string[] keys         = CreatureTemplates.All.Select(template => template.Key).ToArray();
        string   translations = TestSetup.ReadRepoFile(TestSetup.TRANSLATION_FILE);

        Assert.True(keys.Length >= 13, $"The library must hold at least the thirteen first templates, got {keys.Length}.");
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.Equal(CreatureTemplates.PLAIN_FISH_KEY, keys[0]);
        Assert.Equal(CreatureTemplates.PLAIN_MAMMAL_KEY, keys[1]);
        Assert.Equal(CreatureTemplates.All.Select(template => template.Order).OrderBy(order => order), CreatureTemplates.All.Select(template => template.Order));
        foreach (CreatureTemplate template in CreatureTemplates.All)
        {
            Assert.True(translations.Contains($"\n{template.NameKey},"), $"{template.NameKey} must be translated in locale/translations.csv.");
        }
    }

    [Fact]
    public void EveryTemplateBuilds()
    {
        foreach (CreatureTemplate template in CreatureTemplates.All)
        {
            CreatureDesign design = template.Build();

            Assert.InRange(design.Girths.Count, CreatureDesign.MIN_VERTEBRAE, CreatureDesign.MAX_VERTEBRAE);
            Assert.NotEmpty(design.Parts);
            Assert.True(CreatureStats.From(design).Weight > 0f, $"{template.Key} must weigh something.");
        }
    }

    [Fact]
    public void PlainBodiesAreFree()
    {
        CreatureDesign fish    = CreatureTemplates.Plain(CreatureType.Fish).Build();
        CreatureDesign dolphin = CreatureTemplates.Plain(CreatureType.MarineMammal).Build();

        Assert.Equal(CreatureType.Fish, fish.Type);
        Assert.Equal(CreatureType.MarineMammal, dolphin.Type);
        Assert.Equal(0, CreatureTemplates.CostOf(fish));
        Assert.Equal(0, CreatureTemplates.CostOf(dolphin));
        Assert.Equal(1, dolphin.Count(CreaturePartKind.Fluke));
        Assert.Null(fish.Color);
    }

    [Fact]
    public void TemplateCostsItsParts()
    {
        foreach (CreatureTemplate template in CreatureTemplates.All)
        {
            CreatureDesign design = template.Build();
            foreach (CreaturePart part in design.Parts)
            {
                // Free are only the part at the tail tip and the eyes, everything else was paid at its catalog price
                bool isFreeKind = CreaturePartKind.Eye == part.Kind || CreatureMount.Tail == part.Info.Mount;
                Assert.True(part.PaidCost == part.Info.CostFor(part.Side) || (0 == part.PaidCost && isFreeKind),
                    $"{template.Key}: {part.Kind} must be paid {part.Info.CostFor(part.Side)} EP, got {part.PaidCost}.");
            }
            Assert.True(design.Parts.Count(part => 0 == part.PaidCost) <= 2, $"{template.Key} may bring two free parts.");
        }
    }

    [Fact]
    public void NewtIsReadyForTheLand()
    {
        // The one template made for the first step ashore: its legs reach the ground and it has a lung
        CreatureDesign newt = CreatureTemplates.Get("NEWT").Build();

        Assert.True(newt.LegPairs >= 2, $"The newt must stand on two pairs of legs, {newt.LegPairs} reach the ground.");
        Assert.True(newt.BreathesAir, "The newt must carry a lung.");
        Assert.True(CreatureStats.From(newt).LandSpeed > 0.3f, "The newt must walk, not crawl.");
    }

    [Fact]
    public void EveryBuildIsFresh()
    {
        CreatureTemplate puffer = CreatureTemplates.Get("PUFFER");
        CreatureDesign   first  = puffer.Build();
        CreatureDesign   second = puffer.Build();

        first.RemovePart(0);

        Assert.NotSame(first, second);
        Assert.Equal(first.Parts.Count + 1, second.Parts.Count);
    }

    // ===========================================================================
    // ===========================   PARSE TEMPLATES   ===========================
    // ===========================================================================
    //
    //   FileBecomesBody         Spine, lifts, color, pattern, pose and joints of a file arrive in the design.
    //   UnknownFieldFails       A typo in a file fails instead of being ignored.
    //   OnlyTailAndEyesAreFree  A free jaw is refused.
    //   PartWithoutRoomFails    A second tail fin fails with the template and the part.
    //

    private const string SAMPLE = """
        {
          "key": "SAMPLE", "order": 5, "type": "MarineMammal",
          "girths": [0.3, 0.5, 0.7, 0.6, 0.4],
          "lifts": [0, 0, 0.1, 0.2, 0.3],
          "color": "#ff8000", "pattern": "Spots", "patternColor": "#000000",
          "parts": [
            { "kind": "Fluke", "t": 0, "free": true },
            { "kind": "Jaw", "t": 1, "size": 1.5, "offset": [0, 0.1, 0], "rotation": [0, 0, 0.2] },
            { "kind": "Limb", "t": 0.5, "joints": [[0.05, -0.1, 0.3], [0.1, -0.3, 0.4], [0.15, -0.5, 0.4]] },
            { "kind": "Tentacle", "onPart": 2, "onJoint": 1 }
          ]
        }
        """;

    [Fact]
    public void FileBecomesBody()
    {
        CreatureTemplate template = CreatureTemplates.Parse(SAMPLE);
        CreatureDesign   design   = template.Build();

        Assert.Equal("CREATURE_TEMPLATE_SAMPLE", template.NameKey);
        Assert.Equal(5, template.Order);
        Assert.Equal(CreatureType.MarineMammal, design.Type);
        Assert.Equal(5, design.Girths.Count);
        Assert.Equal(0.3f, design.Lifts[4], 4);
        Assert.Equal(new Color(1f, 128f / 255f, 0f), design.Color);
        Assert.Equal(CreaturePattern.Spots, design.Pattern);
        Assert.Equal(1.5f, design.Parts[1].Size, 4);
        Assert.Equal(0.1f, design.Parts[1].Offset.Y, 4);
        Assert.Equal(0.2f, design.Parts[1].Rotation.Z, 4);
        Assert.Equal(0.4f, design.Parts[2].Joints[1].Z, 4);
        Assert.Equal(design.Parts[2].Id, design.Parts[3].ParentId);
        Assert.Equal(0 + 2 + 1 + 2, CreatureTemplates.CostOf(design));
    }

    [Fact]
    public void UnknownFieldFails()
    {
        string json = SAMPLE.Replace("\"order\": 5", "\"ordr\": 5");

        Assert.Throws<InvalidOperationException>(() => CreatureTemplates.Parse(json));
    }

    [Fact]
    public void OnlyTailAndEyesAreFree()
    {
        string json = SAMPLE.Replace("{ \"kind\": \"Jaw\", \"t\": 1,", "{ \"kind\": \"Jaw\", \"t\": 1, \"free\": true,");

        var error = Assert.Throws<InvalidOperationException>(() => CreatureTemplates.Parse(json));
        Assert.Contains("Jaw", error.Message);
    }

    [Fact]
    public void PartWithoutRoomFails()
    {
        string json = SAMPLE.Replace("{ \"kind\": \"Tentacle\", \"onPart\": 2, \"onJoint\": 1 }", "{ \"kind\": \"CaudalFin\", \"t\": 0 }");

        var error = Assert.Throws<InvalidOperationException>(() => CreatureTemplates.Parse(json).Build());
        Assert.Contains("SAMPLE", error.Message);
        Assert.Contains("CaudalFin", error.Message);
    }
}