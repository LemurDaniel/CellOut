// NOTE:
//   One saved game as plain data (wish of the user 2026-11-17): what cannot be recomputed
//   from a seed. The game classes fill and read these records themselves (Save / Load on
//   CellDesign, CreatureDesign, Player, Coast, PackKeeper, TribeCommander, RivalKeeper),
//   Core/Save/SaveCodec.cs turns them into one JSON per slot. Godot types are kept as
//   number arrays (SaveData), enums by name, so a renamed class never breaks a save.
//   Not saved: NPC cells and creatures, food, particles, running cutscenes; they come
//   back on their own after loading.
//
//   SaveGame game = saveKeeper.Capture();
//   string json   = SaveCodec.ToJson(game);
//
//   File layout (SaveCodec.VERSION 1):
//     { "version": 1, "meta": {...}, "world": {...} | null, "player": {...}, "society": {...} | null }

//
//   Chapters:
//     GAME      The whole save and its meta block.
//     WORLD     Islands and foundations of the coast.
//     PLAYER    Stage, growth, diet, mutations and both designs.
//     DESIGNS   Cell design, creature design with spine, look and parts.
//     SOCIETY   Pack, tribe with its buildings, the neighbors.
//     DATA      Conversions between Godot types and number arrays.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// =================================   GAME   ================================
// ===========================================================================

public sealed record SaveGame
{
    public int Version { get; init; }
    public SaveMeta Meta { get; init; }
    public SaveWorld World { get; init; }
    public SavePlayer Player { get; init; }
    public SaveSociety Society { get; init; }
}

/// <summary>What the slot list shows without loading the game: planet, stage, when, how long.</summary>
public sealed record SaveMeta
{
    public string Planet { get; init; }
    public int Stage { get; init; }
    public string SavedAt { get; init; }             // ISO 8601, local time
    public double PlaySeconds { get; init; }
}

// ===========================================================================
// ================================   WORLD   ================================
// ===========================================================================

public sealed record SaveWorld
{
    public float Unit { get; init; }
    public List<SaveIsland> Islands { get; init; } = new();
    public float[] Origin { get; init; }
    public float[] Normal { get; init; }
    public List<SavePad> Pads { get; init; } = new();
}

public sealed record SaveIsland(float[] Center, float Radius, float Seed);

public sealed record SavePad(float[] Center, float Radius);

// ===========================================================================
// ================================   PLAYER   ===============================
// ===========================================================================

public sealed record SavePlayer
{
    public int Stage { get; init; }
    public int Level { get; init; }
    public float Biomass { get; init; }
    public float NextLevelAt { get; init; }
    public float LevelStart { get; init; }
    public int EvolutionPoints { get; init; }
    public float DietPlant { get; init; }
    public float DietMeat { get; init; }
    public float Hp { get; init; }
    public List<string> Mutations { get; init; } = new();
    public int CellCount { get; init; } = 1;
    public float EvolvedAt { get; init; }
    public float[] Position { get; init; }
    public float Facing { get; init; }
    public SaveCellDesign Cell { get; init; }
    public SaveCreatureDesign Creature { get; init; }
}

// ===========================================================================
// ================================   DESIGNS   ==============================
// ===========================================================================

public sealed record SaveCellDesign
{
    public List<SaveCellPart> Parts { get; init; } = new();
    public float[] Color { get; init; }
    public float[] Shape { get; init; }
    public float[] Heights { get; init; }
}

public sealed record SaveCellPart(string Kind, float[] Position, int PaidCost, float[] Offset, float[] Rotation, float Lift, float Size);

public sealed record SaveCreatureDesign
{
    public string Type { get; init; }
    public SaveSpine Spine { get; init; }
    public SaveLook Look { get; init; }
    public List<SaveCreaturePart> Parts { get; init; } = new();
    public int LastId { get; init; }
    public List<SaveShape> Shapes { get; init; } = new();
    public int LastShapeId { get; init; }
}

public sealed record SaveShape(int Id, SaveCreatureDesign Form);

public sealed record SaveSpine
{
    public float[] Girths { get; init; }
    public float[] Lifts { get; init; }
    public float[] Sways { get; init; }
    public float[] Heights { get; init; }
    public float[] Widths { get; init; }
    public float[] Crowns { get; init; }
    public float[] Bellies { get; init; }
    public float[] Xs { get; init; }
}

public sealed record SaveLook
{
    public float[] Color { get; init; }
    public string Pattern { get; init; }
    public float[] PatternColor { get; init; }
    public Dictionary<string, float[]> Zones { get; init; } = new();
    public string Skin { get; init; }
    public float Pitch { get; init; }
    public float Hover { get; init; }
    public float PatternScale { get; init; } = 1f;
    public bool PatternWraps { get; init; }
    public bool HasMouthLine { get; init; } = true;
    public float MouthLevel { get; init; }
    public float MouthLength { get; init; } = 1f;
    public float WaveAmount { get; init; } = 1f;
    public float WaveSpeed { get; init; } = 1f;
    public bool IsBreathing { get; init; } = true;
    public List<SaveSplat> Splats { get; init; } = new();
    public int LastStroke { get; init; }
}

public sealed record SaveSplat(float T, float Angle, float Radius, float[] Color, int Stroke);

public sealed record SaveMotion(bool IsOn, float Speed, float Amount, float Phase, bool IsBlinking);

public sealed record SaveCreaturePart
{
    public string Kind { get; init; }
    public float T { get; init; }
    public int PaidCost { get; init; }
    public float Size { get; init; } = 1f;
    public int Side { get; init; }
    public float[] Offset { get; init; }
    public float[] Rotation { get; init; }
    public List<float[]> Joints { get; init; }
    public int Id { get; init; }
    public int ParentId { get; init; }
    public int ParentJoint { get; init; }
    public float[] Stretch { get; init; }
    public string LimbKind { get; init; }
    public int Radial { get; init; }
    public float[] Widths { get; init; }
    public bool IsCentered { get; init; }
    public float[] Tint { get; init; }
    public SaveMotion Motion { get; init; }
    public Dictionary<string, float> Channels { get; init; }
    public float[] TipColor { get; init; }
    public int TipFrom { get; init; }
    public int ShapeId { get; init; }
}

// ===========================================================================
// ===============================   SOCIETY   ===============================
// ===========================================================================

public sealed record SaveSociety
{
    public SavePack Pack { get; init; }
    public SaveTribe Tribe { get; init; }
    public SaveCiv Civ { get; init; }                 // from the civilization on
    public List<SaveRival> Rivals { get; init; } = new();
    public SaveRival Loner { get; init; }             // the lone beast of the island, null before the nest and in older saves
}

public sealed record SaveCiv
{
    public float Knowledge { get; init; }
    public List<string> Techs { get; init; } = new();
}

public sealed record SavePack
{
    public float[] Nest { get; init; }
    public float NestBiomass { get; init; }
    public bool IsStaying { get; init; }
    public List<SaveKin> Kin { get; init; } = new();
}

public sealed record SaveKin(float[] Position, string Mode, bool IsPup);

public sealed record SaveTribe
{
    public string Path { get; init; }
    public int Food { get; init; }
    public int Wood { get; init; }
    public int Built { get; init; }
    public List<SaveBuildingDesign> Designs { get; init; } = new();
    public List<SaveHut> Huts { get; init; } = new();
}

public sealed record SaveBuildingDesign(string Kind, List<SaveBlock> Blocks);

public sealed record SaveBlock(string Kind, int X, int Z, int Turns, int Paint, int Pattern);

public sealed record SaveHut(SaveBuildingDesign Design, float[] Position, float Turn);

public sealed record SaveRival(float Relation, float Respect, bool IsDefeated);

// ===========================================================================
// =================================   DATA   ================================
// ===========================================================================
//
//   Of / ToVector2 / ToVector3 / ToColor   Godot types to number arrays and back, null stays null.
//   Part / ToPart                          A creature part and its record (a public record, no private state).
//   Building / ToBuilding                  A blueprint and its record: the blocks dropped again in their order.
//

public static class SaveData
{
    public static float[] Of(Vector2 value) => new[] { value.X, value.Y };

    public static float[] Of(Vector3 value) => new[] { value.X, value.Y, value.Z };

    public static float[] Of(Color value) => new[] { value.R, value.G, value.B, value.A };

    public static float[] Of(Color? value) => null == value ? null : Of(value.Value);

    public static Vector2 ToVector2(float[] value) => null == value ? Vector2.Zero : new Vector2(value[0], value[1]);

    public static Vector3 ToVector3(float[] value) => null == value ? Vector3.Zero : new Vector3(value[0], value[1], value[2]);

    public static Color? ToColor(float[] value) => null == value ? null : new Color(value[0], value[1], value[2], value.Length > 3 ? value[3] : 1f);

    /// <summary>A creature part as its save record.</summary>
    public static SaveCreaturePart Part(CreaturePart part)
    {
        return new SaveCreaturePart
        {
            Kind        = part.Kind.ToString(),
            T           = part.T,
            PaidCost    = part.PaidCost,
            Size        = part.Size,
            Side        = part.Side,
            Offset      = Of(part.Offset),
            Rotation    = Of(part.Rotation),
            Joints      = part.Joints?.Select(Of).ToList(),
            Id          = part.Id,
            ParentId    = part.ParentId,
            ParentJoint = part.ParentJoint,
            Stretch     = null == part.Stretch ? null : Of(part.Stretch.Value),
            LimbKind    = part.LimbKind?.ToString(),
            Radial      = part.Radial,
            Widths      = part.Widths?.ToArray(),
            IsCentered  = part.IsCentered,
            Tint        = Of(part.Tint),
            Motion      = null == part.Motion ? null : new SaveMotion(part.Motion.Value.IsOn, part.Motion.Value.Speed, part.Motion.Value.Amount, part.Motion.Value.Phase, part.Motion.Value.IsBlinking),
            Channels    = part.Channels?.ToDictionary(pair => pair.Key, pair => pair.Value),
            TipColor    = Of(part.TipColor),
            TipFrom     = part.TipFrom,
            ShapeId     = part.ShapeId,
        };
    }

    /// <summary>A creature part back from its save record.</summary>
    public static CreaturePart ToPart(SaveCreaturePart save)
    {
        return new CreaturePart(
            System.Enum.Parse<CreaturePartKind>(save.Kind),
            save.T,
            save.PaidCost,
            Size: save.Size,
            Side: save.Side,
            Offset: ToVector3(save.Offset),
            Rotation: ToVector3(save.Rotation),
            Joints: save.Joints?.Select(ToVector3).ToList(),
            Id: save.Id,
            ParentId: save.ParentId,
            ParentJoint: save.ParentJoint,
            Stretch: null == save.Stretch ? null : ToVector3(save.Stretch),
            LimbKind: null == save.LimbKind ? null : System.Enum.Parse<CreaturePartKind>(save.LimbKind),
            Radial: save.Radial,
            Widths: save.Widths,
            IsCentered: save.IsCentered,
            Tint: ToColor(save.Tint),
            Motion: null == save.Motion ? null : new PartMotion(save.Motion.IsOn, save.Motion.Speed, save.Motion.Amount, save.Motion.Phase, save.Motion.IsBlinking),
            Channels: save.Channels,
            TipColor: ToColor(save.TipColor),
            TipFrom: save.TipFrom,
            ShapeId: save.ShapeId);
    }

    /// <summary>A building blueprint as its save record, blocks in the order they were placed.</summary>
    public static SaveBuildingDesign Building(BuildingDesign design)
    {
        return new SaveBuildingDesign(design.Kind.ToString(), design.Blocks.Select(block => new SaveBlock(block.Kind.ToString(), block.X, block.Z, block.Turns, block.Paint, block.Pattern)).ToList());
    }

    /// <summary>A building blueprint back from its record: the blocks drop again in their order, so they stack as before.</summary>
    public static BuildingDesign ToBuilding(SaveBuildingDesign save)
    {
        var design = new BuildingDesign(System.Enum.Parse<BuildingKind>(save.Kind));
        foreach (SaveBlock block in save.Blocks)
        {
            design.TryPlace(System.Enum.Parse<BlockKind>(block.Kind), block.X, block.Z, block.Turns, block.Paint, block.Pattern);
        }
        return design;
    }
}
