// NOTE:
//   Unit tests for saving (Core/Save/): every creature template and a cell design survive the
//   way to JSON and back unchanged, the codec stamps and checks the version and refuses
//   broken files, buildings come back block by block.
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter SaveTests

//
//   Chapters:
//     DESIGNS   Creature and cell designs through JSON.
//     CODEC     Version, broken files, infinity, buildings.
//

using System;
using System.Linq;
using Godot;
using Xunit;

namespace CellOut.Tests;

public class SaveTests
{
    // ===========================================================================
    // ===============================   DESIGNS   ===============================
    // ===========================================================================
    //
    //   EveryTemplateSurvives   Every template: save, JSON, load, save again gives the same JSON.
    //   CellDesignSurvives      Parts, color and shape of a cell design come back.
    //

    [Fact]
    public void EveryTemplateSurvives()
    {
        foreach (CreatureTemplate template in CreatureTemplates.All)
        {
            CreatureDesign design = template.Build();
            string first  = SaveCodec.ToJson(GameWith(design.Save()));
            CreatureDesign back = CreatureDesign.FromSave(SaveCodec.FromJson(first).Player.Creature);
            string second = SaveCodec.ToJson(GameWith(back.Save()));

            Assert.Equal(first, second);
            Assert.Equal(design.Parts.Count, back.Parts.Count);
            Assert.Equal(design.Girths, back.Girths);
            Assert.Equal(design.Color, back.Color);
        }
    }

    [Fact]
    public void CellDesignSurvives()
    {
        var design = new CellDesign();
        design.TryPlace(PartKind.Flagellum, new Vector2(-0.9f, 0f), paidCost: 3);
        design.TryPlace(PartKind.Spike, new Vector2(0.9f, 0f), paidCost: 2);
        SaveCellDesign saved = design.Save();

        var back = new CellDesign();
        back.Load(SaveCodec.FromJson(SaveCodec.ToJson(GameWith(null) with { Player = new SavePlayer { Cell = saved } })).Player.Cell);

        Assert.Equal(design.Parts.Count, back.Parts.Count);
        Assert.Equal(design.Parts.Select(part => part.Kind), back.Parts.Select(part => part.Kind));
        Assert.Equal(design.Parts[0].Position, back.Parts[0].Position);
    }

    // ===========================================================================
    // ================================   CODEC   ================================
    // ===========================================================================
    //
    //   VersionIsStamped      Written files carry VERSION, a newer one is refused.
    //   BrokenFilesFail       Empty text, no JSON, no player block.
    //   InfinitySurvives      The infinite level threshold of a protocell.
    //   BuildingsComeBack     A blueprint with stacked blocks comes back with the same blocks.
    //

    [Fact]
    public void VersionIsStamped()
    {
        string json = SaveCodec.ToJson(GameWith(null));
        Assert.Contains($"\"version\": {SaveCodec.VERSION}", json);
        Assert.Equal(SaveCodec.VERSION, SaveCodec.FromJson(json).Version);
        Assert.Throws<InvalidOperationException>(() => SaveCodec.FromJson(json.Replace($"\"version\": {SaveCodec.VERSION}", "\"version\": 99")));
    }

    [Fact]
    public void BrokenFilesFail()
    {
        Assert.Throws<ArgumentException>(() => SaveCodec.FromJson(""));
        Assert.Throws<InvalidOperationException>(() => SaveCodec.FromJson("{ no json"));
        Assert.Throws<InvalidOperationException>(() => SaveCodec.FromJson("{ \"version\": 1, \"meta\": { \"planet\": \"TERRA\" } }"));
    }

    [Fact]
    public void InfinitySurvives()
    {
        SaveGame game = GameWith(null) with { Player = new SavePlayer { NextLevelAt = float.PositiveInfinity, Cell = new SaveCellDesign() } };
        Assert.True(float.IsPositiveInfinity(SaveCodec.FromJson(SaveCodec.ToJson(game)).Player.NextLevelAt));
    }

    [Fact]
    public void BuildingsComeBack()
    {
        BuildingDesign design = BuildingTemplates.Default(BuildingKind.Dwelling);
        BuildingDesign back   = SaveData.ToBuilding(SaveData.Building(design));

        Assert.Equal(design.Kind, back.Kind);
        Assert.Equal(design.Blocks, back.Blocks);
    }

    /// <summary>A minimal game around a creature design (null for none).</summary>
    private static SaveGame GameWith(SaveCreatureDesign creature)
    {
        return new SaveGame
        {
            Meta   = new SaveMeta { Planet = "TERRA", Stage = Player.ORGANISM_STAGE, SavedAt = "2026-11-17T12:00:00" },
            Player = new SavePlayer { Stage = Player.ORGANISM_STAGE, Cell = new SaveCellDesign(), Creature = creature },
        };
    }
}
