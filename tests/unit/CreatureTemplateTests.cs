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
                bool isFreeKind = CreatureMount.Tail == part.Info.Mount || (CreatureCategory.Eyes == part.Info.Category && CreaturePartKind.Lure != part.Kind);
                int price = part.Info.CostFor(part.Side) * part.Copies;      // a ring pays every copy
                Assert.True(part.PaidCost == price || (0 == part.PaidCost && isFreeKind),
                    $"{template.Key}: {part.Kind} must be paid {price} EP, got {part.PaidCost}.");
            }
            Assert.True(design.Parts.Count(part => 0 == part.PaidCost) <= 2, $"{template.Key} may bring two free parts.");
        }
    }

    [Fact]
    public void NewtIsReadyForTheLand()
    {
        // The one template made for the first step ashore: its legs reach the ground
        CreatureDesign newt = CreatureTemplates.Get("NEWT").Build();

        Assert.True(newt.LegPairs >= 2, $"The newt must stand on two pairs of legs, {newt.LegPairs} reach the ground.");
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
    public void TheSamePartTwiceOnOneSpotFails()
    {
        // Parts may sit anywhere; only an unshifted copy on the very same spot is a mistake in the file
        const string DOUBLED = """
            {
              "key": "DOUBLED", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5],
              "parts": [ { "kind": "DorsalFin", "t": 0.5 }, { "kind": "DorsalFin", "t": 0.5 } ]
            }
            """;

        var error = Assert.Throws<InvalidOperationException>(() => CreatureTemplates.Parse(DOUBLED).Build());
        Assert.Contains("DOUBLED", error.Message);
        Assert.Contains("DorsalFin", error.Message);
    }

    [Fact]
    public void StretchIsReadAndClamped()
    {
        CreatureDesign design = CreatureTemplates.Parse("""
            {
              "key": "STRETCHED", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5],
              "parts": [
                { "kind": "CaudalFin", "t": 0, "free": true, "stretch": [1.5, 1, 9] },
                { "kind": "Eye", "t": 0.8, "free": true }
              ]
            }
            """).Build();

        Assert.Equal(new Godot.Vector3(1.5f, 1f, CreatureDesign.MAX_PART_STRETCH), design.Parts[0].Proportion);
        Assert.Equal(Godot.Vector3.One, design.Parts[1].Proportion);     // unstretched parts stay at one
        Assert.Null(design.Parts[1].Stretch);
    }

    [Fact]
    public void AnyPartSitsOnALimbJoint()
    {
        CreatureDesign design = CreatureTemplates.Parse("""
            {
              "key": "STALKS", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5],
              "parts": [
                { "kind": "Tentacle", "t": 0.6 },
                { "kind": "Eye", "onPart": 0, "onJoint": 3 },
                { "kind": "Jaw", "onPart": 0, "onJoint": 1 },
                { "kind": "Tentacle", "onPart": 0, "onJoint": 2 }
              ]
            }
            """).Build();

        Assert.True(CreatureDesign.IsOnBud(design.Parts[1]));
        Assert.True(CreatureDesign.IsOnBud(design.Parts[2]));
        Assert.False(CreatureDesign.IsOnBud(design.Parts[3]));        // a limb on a limb is a branch with joints of its own
        Assert.Equal(1f, design.Parts[2].T);                          // the mouth sits at the nose of its bud
        Assert.Equal(CreatureCatalog.Get(CreaturePartKind.Eye).Cost, design.Parts[1].PaidCost);

        // The limb takes everything on it along
        design.RemovePart(0);
        Assert.Empty(design.Parts);
    }

    [Fact]
    public void FeetAlwaysWalk()
    {
        // The legs of the crab stand out sideways: they are feet all the same
        CreatureDesign crab = CreatureTemplates.Get("CRAB").Build();
        Assert.Equal(4, crab.LegPairs);

        // A foot held up in the air still counts, a bare limb held up does not
        CreatureDesign design = CreatureTemplates.Plain(CreatureType.Fish).Build();
        Assert.True(design.TryPlace(CreaturePartKind.Legs, 0.3f, 0));
        design.SetLimbJoint(design.Parts.Count - 1, 2, new Godot.Vector3(0.3f, 0.6f, 0.4f));
        Assert.True(design.TryPlace(CreaturePartKind.Limb, 0.7f, 0));
        design.SetLimbJoint(design.Parts.Count - 1, 2, new Godot.Vector3(0.3f, 0.6f, 0.4f));
        Assert.Equal(1, design.LegPairs);
    }

    [Fact]
    public void OnlyFinsSwimWithAWave()
    {
        Assert.True(CreatureTemplates.Get("SHARK").Build().SwimsWithWave);
        Assert.False(CreatureTemplates.Get("CRAB").Build().SwimsWithWave);       // legs and pincers, no fin: it glides stiff
        Assert.True(CreatureTemplates.Get("EEL").Build().SwimsWithWave);         // no tail fin, but the fin seam along its back
        Assert.False(CreatureTemplates.Get("TURTLE").Build().SwimsWithWave);     // paddles only: they stroke, the shell stays stiff
        Assert.False(CreatureTemplates.Get("JELLYFISH").Build().SwimsWithWave);  // bell and arms move on their own

        // The free tail fin of the plain body is what makes it wave
        CreatureDesign plain = CreatureTemplates.Plain(CreatureType.Fish).Build();
        Assert.True(plain.SwimsWithWave);
        while (plain.Parts.Count > 0)
        {
            plain.RemovePart(0);
        }
        Assert.False(plain.SwimsWithWave);
    }

    [Fact]
    public void PostureAndSkinAreRead()
    {
        CreatureDesign upright = CreatureTemplates.Parse("""
            { "key": "UPRIGHT", "type": "Fish", "girths": [0.3, 0.5, 0.4], "pitch": 90, "skin": "Scales", "parts": [] }
            """).Build();
        Assert.Equal(CreatureLook.MAX_PITCH, upright.Pitch, 4);
        Assert.Equal(CreatureSkin.Scales, upright.Skin);

        upright.SetPitch(-9f);
        Assert.Equal(-CreatureLook.MAX_PITCH, upright.Pitch, 4);       // clamped to a quarter turn

        CreatureDesign level = CreatureTemplates.Plain(CreatureType.Fish).Build();
        Assert.Equal(0f, level.Pitch);
        Assert.Equal(CreatureSkin.Smooth, level.Skin);
    }

    [Fact]
    public void BranchesOfTheBodyPartColorsAndTheMouthPlace()
    {
        CreatureTemplate template = CreatureTemplates.Parse("""
            {
              "key": "HYDRA", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5],
              "crowns": [0, -1, -1, 0], "bellies": [1, 1, 1, 0],
              "mouthLevel": -0.5, "mouthLength": 9,
              "parts": [
                { "kind": "Trunk", "t": 0.8, "middle": true, "color": "#ff0000" },
                { "kind": "Eye", "onPart": 0, "onJoint": 2 },
                { "kind": "WingLimb", "t": 0.5, "limb": "Tentacle" },
                { "kind": "Finger", "onPart": 2, "onJoint": 1 }
              ]
            }
            """);
        CreatureDesign design = template.Build();

        // A second piece of body with a head of its own, a wing arm with a finger on its joint
        Assert.Equal(4, design.Parts.Count);
        Assert.True(design.Parts[0].IsCentered);
        Assert.Equal(new Color(1f, 0f, 0f), design.Parts[0].Tint);
        Assert.Null(design.Parts[1].Tint);
        Assert.Equal(design.Parts[0].Id, design.Parts[1].ParentId);
        // A bare limb takes the look of another limb kind, cost and kind stay
        Assert.Equal(CreaturePartKind.WingLimb, design.Parts[2].Kind);
        Assert.Equal(CreaturePartKind.Tentacle, design.Parts[2].LimbStyle);
        Assert.True(design.SetLimbKind(2, CreaturePartKind.WingLimb));
        Assert.Null(design.Parts[2].LimbKind);

        // Ring shapes and the mouth place arrive, clamped
        Assert.Equal(-1f, design.Crowns[1], 4);
        Assert.Equal(1f, design.Bellies[0], 4);
        Assert.Equal(-0.5f, design.MouthLevel, 4);
        Assert.Equal(CreatureLook.MAX_MOUTH_LENGTH, design.MouthLength, 4);
        MouthShape mouth = MouthShapes.For(design);
        Assert.Equal(MouthShapes.PLAIN.Level - 0.5f, mouth.Level, 4);
        Assert.Equal(MouthShapes.PLAIN.Length * CreatureLook.MAX_MOUTH_LENGTH, mouth.Length, 4);

        design.SetPartColor(0, null);
        Assert.Null(design.Parts[0].Tint);
    }

    [Fact]
    public void OwnShapesBecomeParts()
    {
        CreatureDesign design = CreatureDesign.FromGirths(CreatureType.Fish, new[] { 0.4f, 0.6f, 0.8f, 0.6f }, null);
        CustomShape horn = design.AddShape();
        horn.Form.SetLift(horn.Form.Girths.Count - 1, 0.3f);
        Assert.False(horn.Form.HasMouthLine);

        // A part made of the shape: free of charge, a pair on the flanks, no effect on the stats
        float health = CreatureStats.From(design).Health;
        Assert.True(design.PlaceShape(horn.Id, 0.5f));
        Assert.False(design.PlaceShape(99, 0.3f));
        Assert.Equal(horn.Id, design.Parts[0].ShapeId);
        Assert.Equal(0, design.Parts[0].PaidCost);
        Assert.Equal(health, CreatureStats.From(design).Health, 4);

        // A copy brings its own clay along, deleting a shape takes its parts with it
        var copy = new CreatureDesign();
        copy.CopyFrom(design);
        Assert.NotSame(horn.Form, copy.ShapeOf(horn.Id).Form);
        Assert.Equal(0.3f, copy.ShapeOf(horn.Id).Form.Lifts[^1], 4);
        Assert.True(design.RemoveShape(horn.Id));
        Assert.Empty(design.Parts);
        Assert.Single(copy.Parts);
    }

    [Fact]
    public void AHeadModelReplacesTheCarvedFace()
    {
        CreatureDesign design = CreatureTemplates.Parse("""
            {
              "key": "SKULL", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5],
              "parts": [
                { "kind": "Jaw", "t": 1 },
                { "kind": "Limb", "t": 0.5 }
              ]
            }
            """).Build();
        Assert.False(design.HasHeadModel);
        Assert.NotNull(MouthShapes.For(design));

        // The skull covers the front of the body: no carved mouth, no teeth. It costs points like every part
        Assert.True(design.TryPlace(CreaturePartKind.ElephantHead, 1f, CreatureCatalog.Get(CreaturePartKind.ElephantHead).Cost));
        Assert.True(design.HasHeadModel);
        Assert.Null(MouthShapes.For(design));
        Assert.Equal(CreatureCategory.Heads, design.Parts[^1].Info.Category);
        Assert.Equal(1f, design.Parts[^1].T, 4);

        // A head on a limb joint is a bud of its own, the body keeps its face
        design.RemovePart(design.Parts.Count - 1);
        Assert.True(design.TryPlaceOnLimb(CreaturePartKind.ElephantHead, 1, 2, 2));
        Assert.False(design.HasHeadModel);
    }

    [Fact]
    public void MotionOfBodyAndPartsArrivesClamped()
    {
        CreatureDesign design = CreatureTemplates.Parse("""
            {
              "key": "STIFF", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5],
              "waveAmount": 9, "waveSpeed": 0, "breathing": false,
              "parts": [
                { "kind": "Limb", "t": 0.5, "motion": { "on": false } },
                { "kind": "Eye", "t": 0.9, "motion": { "blink": false, "speed": 99, "phase": 1.25 } },
                { "kind": "DorsalFin", "t": 0.4, "motion": { "channels": { "Fin": 0, "Root": 9, "Tip": 1 } } }
              ]
            }
            """).Build();

        // Channels of the model animation: clamped, a plain 1 is no setting, the last one taken away leaves none
        CreaturePart fin = design.Parts[2];
        Assert.Equal(0f, fin.ChannelAmount("Fin"), 4);
        Assert.Equal(PartMotion.MAX_AMOUNT, fin.ChannelAmount("Root"), 4);
        Assert.Equal(2, fin.Channels.Count);
        Assert.Equal(1f, fin.ChannelAmount("Unknown"), 4);
        design.SetPartChannel(2, "Fin", 1f);
        design.SetPartChannel(2, "Root", 1f);
        Assert.Null(design.Parts[2].Channels);

        Assert.Equal(CreatureLook.MAX_WAVE_AMOUNT, design.WaveAmount, 4);
        Assert.Equal(CreatureLook.MIN_WAVE_SPEED, design.WaveSpeed, 4);
        Assert.False(design.IsBreathing);

        // Fields left out keep their default, values are clamped, the phase wraps
        Assert.False(design.Parts[0].Movement.IsOn);
        Assert.Equal(1f, design.Parts[0].Movement.Amount, 4);
        PartMotion eye = design.Parts[1].Movement;
        Assert.True(eye.IsOn);
        Assert.False(eye.IsBlinking);
        Assert.Equal(PartMotion.MAX_SPEED, eye.Speed, 4);
        Assert.Equal(0.25f, eye.Phase, 4);

        // A tail fin drives the swim wave of the body only while it moves itself
        Assert.True(design.TryPlace(CreaturePartKind.SharkTail, 0f, 0));
        Assert.True(design.SwimsWithWave);
        design.SetPartMotion(design.Parts.Count - 1, PartMotion.REST with { IsOn = false });
        Assert.False(design.SwimsWithWave);

        // A second color along a limb: from a joint outwards, clamped to its joints, only on parts with joints
        Assert.True(design.SetLimbTip(0, new Color(1f, 1f, 1f, 0.2f), 99));
        Assert.Equal(new Color(1f, 1f, 1f), design.Parts[0].TipColor);
        Assert.Equal(design.Parts[0].Joints.Count, design.Parts[0].TipFrom);
        Assert.True(design.SetLimbTip(0, null, 2));
        Assert.Null(design.Parts[0].TipColor);
        Assert.False(design.SetLimbTip(1, Colors.White, 1));                    // an eye has no joints

        // Untouched parts carry no setting, the plain motion takes a setting away again
        Assert.Null(design.Parts[2].Motion);
        Assert.Equal(PartMotion.REST, design.Parts[2].Movement);
        design.SetPartMotion(0, PartMotion.REST);
        Assert.Null(design.Parts[0].Motion);
    }

    [Fact]
    public void PointSymmetryStandsInARing()
    {
        CreatureTemplate template = CreatureTemplates.Parse("""
            {
              "key": "RING", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5],
              "parts": [
                { "kind": "Feeler", "t": 0.5, "radial": 8 },
                { "kind": "DorsalFin", "t": 0.2, "radial": 99 }
              ]
            }
            """);
        CreatureDesign design = template.Build();

        // A ring of single halves, each at its price and with its share of weight and effect
        CreaturePart feeler = design.Parts[0];
        Assert.Equal(8, feeler.Radial);
        Assert.Equal(1, feeler.Side);
        Assert.Equal(CreatureCatalog.Get(CreaturePartKind.Feeler).CostFor(1) * 8, feeler.PaidCost);
        Assert.Equal(4f, feeler.Share);
        Assert.Equal(CreatureDesign.MAX_RADIAL, design.Parts[1].Radial);      // clamped

        // One slot on the body stays one part
        Assert.True(design.TryPlace(CreaturePartKind.Jaw, 1f, 0));
        Assert.False(design.SetPartRadial(design.Parts.Count - 1, 4));
        Assert.True(design.SetPartRadial(0, 0));
        Assert.Equal(1, design.Parts[0].Copies);
    }

    [Fact]
    public void ColorZonesAreReadAndCleared()
    {
        CreatureDesign design = CreatureTemplates.Parse("""
            {
              "key": "ZONES", "type": "Fish", "girths": [0.3, 0.5, 0.7, 0.5], "color": "#408040",
              "bellyColor": "#f0e8c0", "headColor": "#c04020", "limbColor": "#202028", "tailColor": "#e0a020",
              "parts": []
            }
            """).Build();

        Assert.NotNull(design.ZoneColor(CreatureZone.Belly));
        Assert.Equal(design.BellyColor, design.ZoneColor(CreatureZone.Belly));
        Assert.Equal(new Godot.Color("#c04020"), design.ZoneColor(CreatureZone.Head));
        Assert.Equal(new Godot.Color("#202028"), design.ZoneColor(CreatureZone.Limbs));
        Assert.Equal(new Godot.Color("#e0a020"), design.ZoneColor(CreatureZone.Tail));
        Assert.Equal(design.Color, design.ZoneColor(CreatureZone.Body));          // the body zone is the body color

        // Null takes a zone away again, a copy carries the zones along
        design.SetZoneColor(CreatureZone.Head, null);
        Assert.Null(design.ZoneColor(CreatureZone.Head));
        var copy = new CreatureDesign();
        copy.CopyFrom(design);
        Assert.Equal(design.ZoneColor(CreatureZone.Tail), copy.ZoneColor(CreatureZone.Tail));
        Assert.Null(copy.ZoneColor(CreatureZone.Head));
        Assert.Null(CreatureTemplates.Plain(CreatureType.Fish).Build().ZoneColor(CreatureZone.Limbs));
    }
}