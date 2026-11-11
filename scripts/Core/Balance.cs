// NOTE:
//   Every tunable gameplay number of Cell Out, read from data/balance.json: the code
//   holds the formulas, the file holds the numbers. Loaded once on first use. Parsing is
//   plain .NET, only the default file read asks Godot, so unit tests feed the same file
//   with Balance.Use. What each section means is listed in data/README.md.
//   Requires: data/balance.json with every section and one entry per part and mutation
//   kind, a missing or unknown entry fails at load with a message that names it.
//
//   float growth = Balance.Current.Player.LevelGrowth;                   // in the game
//   Balance.Use(System.IO.File.ReadAllText("data/balance.json"));        // in a unit test

//
//   Chapters:
//     LOAD       Current balance, parsing and the check for complete tables.
//     PLAYER     Growth, levels, health, division, land and the diet bar.
//     CELL       Cell parts, their effects, contact combat and the NPC cells.
//     CREATURE   Creature parts, body traits, land speed and part effects.
//     PRELUDE    Mutations of the RNA world.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CellOut;

public sealed record Balance
{
    // ===========================================================================
    // ================================   LOAD   =================================
    // ===========================================================================
    //
    //   Sections   One record per part of the game.
    //   Current    The loaded balance, reads data/balance.json on first use.
    //   Use        Replaces the balance with the given JSON text (unit tests).
    //   Parse      Turns JSON text into a checked balance.
    //

    public const string FILE_PATH = "res://data/balance.json";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy    = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling  = JsonUnmappedMemberHandling.Disallow,     // a typo in the file fails instead of being ignored
    };

    public required PlayerBalance Player { get; init; }
    public required DietBalance Diet { get; init; }
    public required CellBalance Cell { get; init; }
    public required CritterBalance Critter { get; init; }
    public required CreatureBalance Creature { get; init; }
    public required PreludeBalance Prelude { get; init; }

    private static Balance _current;

    /// <summary>The balance of the running game, loaded from FILE_PATH on first use.</summary>
    public static Balance Current => _current ??= Parse(ReadProjectFile());

    /// <summary>Replaces the current balance, for unit tests that run without Godot.</summary>
    /// <param name="json">(Required) Content of data/balance.json.</param>
    /// <example><code>Balance.Use(System.IO.File.ReadAllText("data/balance.json"));</code></example>
    public static void Use(string json)
    {
        _current = Parse(json);
    }

    /// <summary>Parses and checks a balance file.</summary>
    /// <param name="json">(Required) JSON text with every section.</param>
    /// <returns>The balance, every part and mutation kind has its entry.</returns>
    public static Balance Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException($"json must hold the content of {FILE_PATH}, got an empty text.", nameof(json));
        }

        Balance balance;
        try
        {
            balance = JsonSerializer.Deserialize<Balance>(json, JSON_OPTIONS);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"{FILE_PATH} must match the records in Core/Balance.cs: {error.Message}", error);
        }
        if (null == balance)
        {
            throw new InvalidOperationException($"{FILE_PATH} must hold a JSON object, got 'null'.");
        }

        RequireEveryKind(balance.Cell.Parts, "cell.parts");
        RequireEveryKind(balance.Creature.Parts, "creature.parts");
        RequireEveryKind(balance.Prelude.Mutations, "prelude.mutations");
        return balance;
    }

    /// <summary>Fails when a table misses an entry for a value of its enum.</summary>
    private static void RequireEveryKind<TKind, TEntry>(IReadOnlyDictionary<TKind, TEntry> table, string section)
        where TKind : struct, Enum
    {
        TKind[] missing = Enum.GetValues<TKind>().Where(kind => !table.ContainsKey(kind)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"{FILE_PATH} must list every {typeof(TKind).Name} in '{section}', missing: {string.Join(", ", missing)}.");
        }
    }

    /// <summary>Reads the file through Godot, so it also works from an exported pack.</summary>
    private static string ReadProjectFile()
    {
        string json = Godot.FileAccess.GetFileAsString(FILE_PATH);
        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException($"{FILE_PATH} must exist and be readable, Godot reported '{Godot.FileAccess.GetOpenError()}'.");
        }
        return json;
    }
}

// ===========================================================================
// ===============================   PLAYER   ================================
// ===========================================================================
//
//   PlayerBalance   Growth, levels, health, division, eating and land (Game/Player.cs).
//   DietBalance     How meals and parts mix on the diet bar (Cell/DietTracker.cs).
//

public sealed record PlayerBalance
{
    // Growth: body size = BodySizeBase + BodySizeGrowth * sqrt(biomass)
    public required float BodySizeBase { get; init; }
    public required float BodySizeGrowth { get; init; }
    public required float SingleCellAt { get; init; }            // biomass that turns the protocell into a single cell

    // Levels: the first one FirstLevelStep after the single cell, every next at previous * LevelGrowth + LevelStep
    public required float FirstLevelStep { get; init; }
    public required float LevelGrowth { get; init; }
    public required float LevelStep { get; init; }
    public required int EpPerLevel { get; init; }
    public required int EpPerStage { get; init; }
    public required int OrganismBonusEp { get; init; }           // on top for the first creature editor

    // Health and steering
    public required float ProtocellHp { get; init; }
    public required float CellHp { get; init; }
    public required float RegenPerSecond { get; init; }          // share of the maximum health
    public required float Acceleration { get; init; }
    public required float MagnetRange { get; init; }             // food pull range in body sizes
    public required float Knockback { get; init; }               // push back on a hit, times the current speed
    public required float StartInvulnerability { get; init; }    // seconds
    public required float HitInvulnerability { get; init; }
    public required float RespawnInvulnerability { get; init; }

    // Division: the first at DivisionBiomass, every next needs DivisionGrowth times more
    public required float DivisionBiomass { get; init; }
    public required float DivisionGrowth { get; init; }
    public required float HpPerDivision { get; init; }           // share of the base health each division adds
    public required float CellsLostOnDeath { get; init; }        // share of the cells a deadly hit bursts

    // Eating: the body must be larger than the prey radius times the ratio
    public required float JawEatRatio { get; init; }
    public required float EatRatioPerDivision { get; init; }
    public required float MinEatRatio { get; init; }

    // Land
    public required float AirSeconds { get; init; }              // a full breath lasts this long on land
    public required float AirRefill { get; init; }               // per second in the water
    public required float SuffocateDamage { get; init; }         // share of the maximum health per second without air
}

public sealed record DietBalance
{
    public required float HalfLife { get; init; }                // seconds until a meal counts half
    public required float LightWeight { get; init; }             // photosynthesis counts this share as plant
    public required float EatingWeight { get; init; }            // share of the eating lean once parts exist
    public required float NeutralMass { get; init; }             // damps the value while little was eaten
    public required float PlantFoodValue { get; init; }          // multiplier on the value of every piece of a food patch
}

// ===========================================================================
// =================================   CELL   ================================
// ===========================================================================
//
//   CellBalance          Parts, effects and combat of the cell stages.
//   CellPartBalance      Cost and unlock range of one cell part (Cell/PartCatalog.cs).
//   CellEffectsBalance   What every cell part does (Cell/CellStats.cs).
//   CellCombatBalance    Arcs and damage of spikes, jaws and plates (Cell/CellStats.cs).
//   CritterBalance       Hunting rules and speed of the NPC cells (Cell/Critter.cs).
//

public sealed record CellBalance
{
    public required IReadOnlyDictionary<PartKind, CellPartBalance> Parts { get; init; }
    public required CellEffectsBalance Effects { get; init; }
    public required CellCombatBalance Combat { get; init; }
}

public sealed record CellPartBalance
{
    public required int Cost { get; init; }                      // evolution points
    public float MinDiet { get; init; } = -1f;                   // diet range that unlocks it, -1 herbivore .. 1 carnivore
    public float MaxDiet { get; init; } = 1f;
}

public sealed record CellEffectsBalance
{
    public required float ChloroplastLight { get; init; }        // light biomass per second at the reference size
    public required float FilterMouthMagnet { get; init; }       // added pull range in body sizes
    public required float FilterMouthDigestPlant { get; init; }
    public required float ArmorPlateHealth { get; init; }
    public required float FlagellumSpeed { get; init; }          // added speed share at full thrust (from the back)
    public required float FlagellumFrontThrust { get; init; }    // thrust share when it sits at the front
    public required float EyespotMagnet { get; init; }
    public required float EyespotSight { get; init; }            // added sense range share of NPC cells
    public required float VacuoleDigest { get; init; }
    public required float CiliaAcceleration { get; init; }
    public required float JawDigestMeat { get; init; }
    public required float ToxinDamage { get; init; }             // multiplier on spike and jaw damage
}

public sealed record CellCombatBalance
{
    public required float SpikeArc { get; init; }                // half angle a spike covers, radians
    public required float JawArc { get; init; }
    public required float PlateArc { get; init; }
    public required float SpikeDamage { get; init; }             // at the reference size
    public required float JawDamage { get; init; }
    public required float PlateDamageFactor { get; init; }       // share of the damage that gets through a plate
}

public sealed record CritterBalance
{
    public required float EatRatio { get; init; }                // a predator must be this much bigger than its prey
    public required float JawEatRatio { get; init; }
    public required float SatedTime { get; init; }               // seconds without hunting after a meal
    public required float SenseRadii { get; init; }              // sense range in own radii
    public required float WanderSpeed { get; init; }             // share of full speed while drifting
    public required float BacteriumSpeed { get; init; }          // share of the swim speed of the player size
    public required float AmoebaSpeed { get; init; }
    public required float HunterSpeed { get; init; }
}

// ===========================================================================
// ===============================   CREATURE   ==============================
// ===========================================================================
//
//   CreatureBalance          Parts, body, land and effects of the organism stages.
//   CreaturePartBalance      Cost, weight, carry, stride and lift of one part (Creature/CreatureCatalog.cs).
//   CreatureBodyBalance      Health, speed and weight from type, girth and length (Creature/CreatureStats.cs).
//   CreatureLandBalance      Crawling and walking speed from stride and load.
//   CreatureEffectsBalance   What every creature part does.
//

public sealed record CreatureBalance
{
    public required IReadOnlyDictionary<CreaturePartKind, CreaturePartBalance> Parts { get; init; }
    public required CreatureBodyBalance Body { get; init; }
    public required CreatureLandBalance Land { get; init; }
    public required CreatureEffectsBalance Effects { get; init; }
}

public sealed record CreaturePartBalance
{
    public required int Cost { get; init; }                      // evolution points for the pair
    public required float Weight { get; init; }                  // kg in design scale, the clay blob weighs about 10
    public float Carry { get; init; }                            // weight a pair of legs carries on land
    public float Stride { get; init; }                           // walking speed of a leg kind, 1 = normal legs
    public float Lift { get; init; }                             // weight a pair of wings lifts
}

public sealed record CreatureBodyBalance
{
    public required float Density { get; init; }                 // kg per unit volume
    public required float MammalHealth { get; init; }
    public required float FishSwimSpeed { get; init; }

    // Health = HealthBase + HealthPerGirth * mean girth, times 1 + HealthPerVertebra per vertebra above five
    public required float HealthBase { get; init; }
    public required float HealthPerGirth { get; init; }
    public required float HealthPerVertebra { get; init; }

    // Swim speed = SwimBase - SwimPerGirth * mean girth, times 1 + SwimPerVertebra per vertebra above five
    public required float SwimBase { get; init; }
    public required float SwimPerGirth { get; init; }
    public required float SwimPerVertebra { get; init; }
}

public sealed record CreatureLandBalance
{
    public required float CrawlSpeed { get; init; }              // share of the swim speed without legs on land
    public required float WalkSpeed { get; init; }               // normal legs at load 1

    // Load factor = clamp(LoadBase - LoadSlope * weight / carry, MinLoadFactor, MaxLoadFactor)
    public required float LoadBase { get; init; }
    public required float LoadSlope { get; init; }
    public required float MinLoadFactor { get; init; }
    public required float MaxLoadFactor { get; init; }
    public required float WingFlapBonus { get; init; }           // per wing pair, flapping helps a little on land
}

public sealed record CreatureEffectsBalance
{
    // Fins
    public required float CaudalFinSwim { get; init; }
    public required float FlukeSwim { get; init; }
    public required float FlukeAcceleration { get; init; }
    public required float DorsalFinAcceleration { get; init; }
    public required float PectoralFinAcceleration { get; init; }

    // Mouths, magnet values are added pull range in body sizes
    public required float JawDigestMeat { get; init; }
    public required float FilterMouthMagnet { get; init; }
    public required float FilterMouthDigestPlant { get; init; }
    public required float BeakDigestPlant { get; init; }

    // Defense
    public required float ShellHealth { get; init; }
    public required float ShellSwim { get; init; }               // a shell is bulky in the water
    public required float PoisonGlandDamage { get; init; }       // multiplier on spike and jaw damage

    // Senses
    public required float PitEyeMagnet { get; init; }
    public required float EyeMagnet { get; init; }
    public required float StalkEyeMagnet { get; init; }
    public required float HandsMagnet { get; init; }
    public required float ClawsMagnet { get; init; }

    // Limbs: legs, sculpted limbs and wings slow the swimming per pair
    public required float LimbDrag { get; init; }
    public required float LimbCarry { get; init; }               // a sculpted limb pair that reaches the ground
    public required float LimbStride { get; init; }
    public required float LimbReach { get; init; }               // a sculpted limb pair held up grabs like a short arm

    // Brain and lungs
    public required int SmallBrainEp { get; init; }              // extra evolution points per level
    public required int BigBrainEp { get; init; }
    public required float BigBrainAcceleration { get; init; }
    public required float BlowholeHealth { get; init; }
    public required float BigLungHealth { get; init; }
}

// ===========================================================================
// ===============================   PRELUDE   ===============================
// ===========================================================================
//
//   PreludeBalance    Evolution points and multipliers of the mutations (Prelude/Mutation.cs).
//   MutationBalance   Multipliers of one mutation, 1 = no effect.
//

public sealed record PreludeBalance
{
    public required int EpPerMutation { get; init; }
    public required IReadOnlyDictionary<MutationKind, MutationBalance> Mutations { get; init; }
}

public sealed record MutationBalance
{
    public float Health { get; init; } = 1f;
    public float Speed { get; init; } = 1f;
    public float Magnet { get; init; } = 1f;
    public float Digest { get; init; } = 1f;
}
