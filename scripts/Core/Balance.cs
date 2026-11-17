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
//     TRIBE      Pack, fruit of the islands and the village of the tribe.
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
    public required TribeBalance Tribe { get; init; }
    public required CivBalance Civ { get; init; }
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
        RequireEveryKind(balance.Tribe.Buildings, "tribe.buildings");
        RequireEveryKind(balance.Civ.Techs, "civ.techs");

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
}

public sealed record DietBalance
{
    public required float HalfLife { get; init; }                // seconds until a meal counts half
    public required float PlantWeight { get; init; }             // eaten food lying around counts this many times as plant
    public required float LightWeight { get; init; }             // photosynthesis counts this share as plant
    public required float MeatWeight { get; init; }              // eaten cells and their remains count this many times as meat
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
//   CellEffectsBalance   The passive thrust of the flagellum (Cell/CellStats.cs).
//   AbilityBalance       Cooldown and numbers of one cell ability (Game/Abilities/).
//   CellCombatBalance    Arcs and damage of spikes, jaws, plates, stings and suckers (Cell/CellStats.cs).
//   CritterBalance       Hunting rules and speed of the NPC cells (Cell/Critter.cs).
//   GiantBalance         Giant cells deep in the background until the player grew (Game/Deep/).
//

public sealed record CellBalance
{
    public required IReadOnlyDictionary<PartKind, CellPartBalance> Parts { get; init; }
    public required CellEffectsBalance Effects { get; init; }
    public required CellCombatBalance Combat { get; init; }
    public required IReadOnlyDictionary<PartKind, AbilityBalance> Abilities { get; init; }
}

public sealed record CellPartBalance
{
    public required int Cost { get; init; }                      // evolution points
    public float MinDiet { get; init; } = -1f;                   // diet range that unlocks it, -1 herbivore .. 1 carnivore
    public float MaxDiet { get; init; } = 1f;
}

public sealed record CellEffectsBalance
{
    public required float FlagellumSpeed { get; init; }          // added speed share at full thrust (from the back)
    public required float FlagellumFrontThrust { get; init; }    // thrust share when it sits at the front
}

/// <summary>
/// Numbers of one cell ability (Cell/Abilities/AbilitySet.cs, Game/Abilities/AbilityResolver.cs).
/// What Damage, Power, Reach, Duration and Linger mean depends on the part, see the table
/// in the header of AbilityResolver.cs. Reach is in body sizes beyond the membrane.
/// </summary>
public sealed record AbilityBalance
{
    public required float Cooldown { get; init; }                // seconds until it fires again, 0: works all the time
    public float Duration { get; init; }                         // seconds the effect runs (cloak, slime laying, stun of a bolt)
    public float Damage { get; init; }                           // at the reference size, per second for clouds
    public float Power { get; init; }                            // strength: push in swim speeds, pull, heal share, slow share
    public float Reach { get; init; }                            // body sizes beyond the membrane
    public float Linger { get; init; }                           // seconds a cloud or a slime patch stays
    public int Count { get; init; } = 1;                         // targets of a bolt
}

public sealed record CellCombatBalance
{
    public required float SpikeArc { get; init; }                // half angle a spike covers, radians
    public required float JawArc { get; init; }
    public required float PlateArc { get; init; }                // half angle a plate shields, radians
    public required float SpikeDamage { get; init; }             // at the reference size
    public required float JawDamage { get; init; }
    public required float PlateDamageFactor { get; init; }       // share of the damage that gets through a creature shell (a cell plate is a shield instead)
    public required float StingArc { get; init; }                // half angle a nematocyst covers, radians
    public required float StingReach { get; init; }              // reach beyond the membrane in body sizes
    public required float StingDamage { get; init; }             // per shot at the reference size, times the toxin
    public required float StingCooldown { get; init; }           // seconds until the same cell is stung again
    public required float StunTime { get; init; }                // seconds a stung cell is paralysed
    public required float StunSpeed { get; init; }               // share of the speed left while paralysed
    public required float SuckerArc { get; init; }
    public required float SuckerDamage { get; init; }            // per second while latched, at the reference size, times the toxin
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
    public required float ColonySpeed { get; init; }             // a cluster swims slower than a single hunter
    public required GiantBalance Giants { get; init; }
}

/// <summary>Giant cells in the background of the cell stages (Game/Deep/DeepRule.cs).</summary>
public sealed record GiantBalance
{
    public required int Count { get; init; }                     // giants deep below the player at once
    public required float MinSize { get; init; }                 // body size at the spawn in player sizes
    public required float MaxSize { get; init; }
    public required float SurfaceRatio { get; init; }            // rises to the plane once it is at most this many player sizes big
    public required float RiseSeconds { get; init; }             // from the depth up to the plane
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
    public required IReadOnlyDictionary<CreaturePartKind, AbilityBalance> Abilities { get; init; }   // the ability parts (Game/Abilities/CreatureAbilityResolver.cs)
}

public sealed record CreaturePartBalance
{
    public required int Cost { get; init; }                      // evolution points for the pair
    public required float Weight { get; init; }                  // kg in design scale, the clay blob weighs about 10
    public float Carry { get; init; }                            // weight a pair of legs carries on land
    public float Stride { get; init; }                           // walking speed of a leg kind, 1 = normal legs
    public float Lift { get; init; }                             // weight a pair of wings lifts
    public int UnlockStage { get; init; }                        // Player.Stage this game must reach before the part can be built (Creature/Design/PartUnlocks.cs)
    public string UnlockReached { get; init; }                   // StartPhase reached once on any planet before the part can be built
}

public sealed record CreatureBodyBalance
{
    public required float Density { get; init; }                 // kg per unit volume

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
    public required float PincersMagnet { get; init; }
    public required float SuckersMagnet { get; init; }
    public required float LureMagnet { get; init; }
    public required float GillsSwim { get; init; }

    // Limbs: legs, sculpted limbs and wings slow the swimming per pair
    public required float LimbDrag { get; init; }
    public required float LimbCarry { get; init; }               // a sculpted limb pair that reaches the ground
    public required float LimbStride { get; init; }
    public required float LimbReach { get; init; }               // a sculpted limb pair held up grabs like a short arm

    // Breathing openings
    public required float BlowholeHealth { get; init; }
}

// ===========================================================================
// ================================   TRIBE   ================================
// ===========================================================================
//
//   TribeBalance     Pack, fruit, village, buildings and rivals of the social stages (Tribe/).
//   PackBalance      Conspecifics, calling, the nest and the pups (Tribe/Pack/PackKeeper.cs, Kin.cs).
//   FruitBalance     Fruit dropping from the trees of the islands (Game/World/Spawner.cs).
//   WildlifeBalance  Herds of small prey on the islands (Game/World/Wildlife.cs).
//   VillageBalance   Villagers, gathering, chopping and fighting of the tribe (Tribe/Village/TribeCommander.cs).
//   BuildingBalance  Costs and effects of one building function (Tribe/Building/BuildingDesign.cs).
//   RivalsBalance    Neighbor tribes, their trades, raids and relations (Tribe/Rivals/RivalKeeper.cs).
//   WaysBalance      Court, display and hunt of the pack, the paths of the tribe (Tribe/TribeWays.cs).
//

public sealed record TribeBalance
{
    public required PackBalance Pack { get; init; }
    public required FruitBalance Fruit { get; init; }
    public required WildlifeBalance Wildlife { get; init; }
    public required VillageBalance Village { get; init; }
    public required IReadOnlyDictionary<BuildingKind, BuildingBalance> Buildings { get; init; }
    public required RivalsBalance Rivals { get; init; }
    public required WaysBalance Ways { get; init; }
}

/// <summary>The three ways of pack and tribe: court, display, hunt and the paths they lock into (Tribe/TribeWays.cs, Tribe/Pack/PackEncounter.cs).</summary>
public sealed record WaysBalance
{
    public required float WayBonus { get; init; }                // a way that fits the diet works up to 1 + this times better
    public required float PathThreshold { get; init; }           // diet beyond +-this locks the warlike or pacifist path
    public required int PacksOnIsland { get; init; }             // foreign packs around the player's nest
    public required int PackSize { get; init; }                  // animals of one foreign pack
    public required float PackSizeSpread { get; init; }          // their size against the player, +-share
    public required float EncounterRange { get; init; }          // body sizes from the player to the stranger it turns to
    public required float CourtRange { get; init; }              // body sizes between the membranes for a courtship
    public required float CourtCooldown { get; init; }           // seconds
    public required float CourtRelation { get; init; }           // relation per courtship at strength 1
    public required float CourtSnap { get; init; }               // damage of a hostile stranger that snaps back
    public required float DisplayRange { get; init; }            // body sizes around the player and the stranger that count for a display
    public required float DisplayCooldown { get; init; }
    public required float DisplayWin { get; init; }              // respect per won display at strength 1
    public required float DisplayLose { get; init; }             // respect lost per lost display
    public required float DisplaySeconds { get; init; }          // how long both sides hold the pose
    public required float HuntRange { get; init; }               // body sizes between the membranes for a lunge
    public required float HuntCooldown { get; init; }
    public required float HuntLunge { get; init; }               // lunge in walking speeds
    public required float HuntDamage { get; init; }              // bite at strength 1
    public required float HuntRelation { get; init; }            // relation lost per hunt
    public required float RespectAt { get; init; }               // respect from which a pack pays tribute and a tribe never raids
    public required float RespectDrift { get; init; }            // respect lost per second
    public required float AdoptAt { get; init; }                 // relation from which a foreign animal joins the pack
    public required float AllyAt { get; init; }                  // relation of an ally, the goal of the pacifist path
    public required float GiftInterval { get; init; }            // seconds between two fruits a friendly pack brings to the nest
    public required float TributeInterval { get; init; }         // seconds between two tributes of a respectful pack or tribe
    public required int KillFood { get; init; }                  // food for the stock per enemy killed on the warlike path
    public required float CourtPerSecond { get; init; }          // relation per second and villager courting a tribe
    public required float StartShift { get; init; }              // start relation of new tribes: + pacifist, - warlike
    public required int AdoptPerPack { get; init; }              // animals one foreign pack lets go to the player's pack at most
    public required float TerritoryRange { get; init; }          // body sizes around its den a hostile pack defends
    public required float LeashRange { get; init; }              // body sizes from its den a hostile pack gives up the chase
    public required float GreetCooldown { get; init; }           // seconds between two greetings (bow, duck) of the same pack
    public required float MeatShare { get; init; }               // size of the meat a killed animal leaves, against its body
    public required float LonerSize { get; init; }               // the lone beast against the player size
    public required float LonerHealth { get; init; }             // its health in healths of a kin
    public required float LonerDamage { get; init; }             // its damage in damages of a kin
    public required float LonerPresence { get; init; }           // bodies of its own size it counts for in a display
    public required float LonerDistance { get; init; }           // share of SameIslandDistance between the nest and its lair
    public required float LonerRelation { get; init; }           // its start relation, hostile
    public required float LonerProwlInterval { get; init; }      // seconds between two prowls to the player's nest
    public required float LonerProwlSeconds { get; init; }       // a prowl ends after this long
    public required float CourtCalm { get; init; }               // seconds a hostile animal starts no fight after a courtship
}

public sealed record PackBalance
{
    public required int KinOnIsland { get; init; }               // wild conspecifics around the land animal at most
    public required float KinSpawnInterval { get; init; }        // seconds between two wild conspecifics
    public required float CallRange { get; init; }               // body sizes a conspecific hears the call
    public required float NestDepth { get; init; }               // Units inland at least for a nest (grass, not sand)
    public required float NestRange { get; init; }               // body sizes around the nest that count as "at the nest"
    public required int PupsAtNest { get; init; }                // pups born when the nest is built
    public required float PupSize { get; init; }                 // share of the adult size at birth
    public required float PupGrowSeconds { get; init; }          // seconds a pup needs to grow up
    public required float FollowDistance { get; init; }          // body sizes a follower keeps behind the leader
    public required float WanderRadius { get; init; }            // body sizes a kin roams around its home
    public required float FoundTribeGrowth { get; init; }        // share more biomass than at the nest that founds the tribe
    public required float NestRegen { get; init; }               // share of the maximum health the leader heals per second at the nest
    public required float PackFoodShare { get; init; }           // share of a meal of a pack member that feeds the leader too (the pack eats together)
    public required float ForageRange { get; init; }             // body sizes a following member walks off to food
    public required int MaxMembers { get; init; }                // members a pack holds at most, wild kin answer the call until then
    public required float LitterShare { get; init; }             // every this share of the growth towards the tribe a pup is born at the nest
}

public sealed record FruitBalance
{
    public required float Interval { get; init; }                // seconds between two fruit drops near the chieftain of a tribe (its people gather them)
    public required int MaxOnGround { get; init; }               // land food lying around a tribe at most
    public required float WildInterval { get; init; }            // seconds between two fruit drops before the tribe: fruit is the slow meal, deeds feed a pack
    public required int WildMaxOnGround { get; init; }           // land food lying around at most before the tribe
    public required float SizeMin { get; init; }                 // a fruit against the player size, smallest
    public required float SizeMax { get; init; }                 // and biggest
}

public sealed record WildlifeBalance
{
    public required int Herds { get; init; }                     // herds of prey around the land animal at most
    public required int HerdSize { get; init; }                  // animals of one herd
    public required float HerdSeconds { get; init; }             // seconds between two new herds
    public required float PreySize { get; init; }                // a prey animal against the player size
    public required float PreyPace { get; init; }                // its pace on the run against the walking pace of a body of the player's size
    public required float PreyHealth { get; init; }              // its health in healths of a kin
    public required float FleeRange { get; init; }               // body sizes it lets the pack come close before it runs
    public required float PreyMeat { get; init; }                // size of the meat it leaves, against its body
}

public sealed record VillageBalance
{
    public required float HutDistance { get; init; }             // Units from the fire to a building placed without a chosen spot
    public required float BuildRadius { get; init; }             // Units beyond the foundation of the settlement the villagers chop trees in
    public required float BuildSpacing { get; init; }            // Units around the middle of a plot that a standing building takes
    public required int SlotInnerCount { get; init; }            // building plots on the inner ring of a settlement (Tribe/Village/Settlement.cs)
    public required float SlotInnerRadius { get; init; }         // Units from the fire to the inner ring
    public required int SlotOuterCount { get; init; }            // plots on the outer ring
    public required float SlotOuterRadius { get; init; }         // Units from the fire to the outer ring
    public required float SlotDepth { get; init; }               // Units of land a plot needs towards the shore, plots nearer to the water stay empty
    public required float VillagerSeconds { get; init; }         // seconds a hut needs to raise a villager
    public required float GatherRange { get; init; }             // body sizes an idle villager looks for fruit
    public required int FoodPerFruit { get; init; }              // food stock one gathered fruit brings
    public required int FoodPerMeat { get; init; }               // food a piece of meat brings on top (prey, fallen enemies)
    public required float ChopSeconds { get; init; }             // seconds a villager chops for one load of wood
    public required int WoodPerChop { get; init; }               // wood one load brings
    public required float KinHealth { get; init; }               // hit points of a grown kin
    public required float KinDamage { get; init; }               // damage per second a kin deals in a fight
    public required float EngageRange { get; init; }             // body sizes an idle villager runs to fight an enemy
    public required int GiftFood { get; init; }                  // food a gift to a rival tribe costs
    public required float WarriorHealth { get; init; }           // a warrior has this many times the health of a kin
    public required float WarriorDamage { get; init; }           // and deals this many times its damage
}

public sealed record BuildingBalance
{
    public required int Food { get; init; }
    public required int Wood { get; init; }
    public required float Health { get; init; }                  // hit points of the building against raids
    public int Villagers { get; init; }                          // villagers it raises in all (dwelling)
    public int Warriors { get; init; }                           // warriors it trains in all (warrior hut)
    public float Defense { get; init; }                          // raid damage is divided by 1 + defense / DefenseScale (watchtower)
    public float TradeBonus { get; init; }                       // share more a trader brings (market)
    public float RelationBonus { get; init; }                    // extra relation per trade (totem)
    public int FoodPerFruit { get; init; }                       // extra food per gathered fruit while it stands (storehouse)
    public float Knowledge { get; init; }                        // knowledge per minute while it stands (hall, academy)
}

public sealed record RivalsBalance
{
    public required int Count { get; init; }                     // neighbor tribes, the first on the own island
    public required float SameIslandDistance { get; init; }      // Units from the own nest to the den of a foreign pack on the same island (times PACK_DISTANCE)
    public required float SettleDistance { get; init; }          // Units from the own fire to the fire of a pack that became a tribe: room for two foundations and land between
    public required int Huts { get; init; }                      // huts of a rival village
    public required int Villagers { get; init; }                 // villagers of a rival village
    public required float FriendlyAt { get; init; }              // relation from which a tribe trades
    public required float HostileAt { get; init; }               // relation below which a tribe raids
    public required float TradeInterval { get; init; }           // seconds between traders of a friendly tribe
    public required int TradeFood { get; init; }                 // a trader brings this much food
    public required int TradeWood { get; init; }                 // and this much wood
    public required float RaidInterval { get; init; }            // seconds between raids of a hostile tribe
    public required int Raiders { get; init; }                   // raiders per raid
    public required float RaidDamage { get; init; }              // damage per second a raider deals to a hut
    public required float DefenseScale { get; init; }            // defense points that halve the raid damage
    public required float RaidSeconds { get; init; }             // a raid ends after this long
    public required float RelationPerTrade { get; init; }        // relation gain per delivered trade
    public required float RelationPerGift { get; init; }         // relation gain per gift
    public required float RelationPerKill { get; init; }         // relation loss when the player's tribe kills a rival
    public required float RelationDrift { get; init; }           // relation moves this much per second towards neutral
    public required float RazeDamage { get; init; }              // damage per second a kin of the player deals to a rival building
    public required float RelationPerRaze { get; init; }         // relation loss per rival building torn down
    public required int DefeatFood { get; init; }                // food looted from a tribe that lost its last building
    public required int DefeatWood { get; init; }                // and wood
}

// ===========================================================================
// ===============================   PRELUDE   ===============================
// ===========================================================================
//
//   PreludeBalance    Evolution points, the error threshold of the chain and the mutations (Prelude/Mutation.cs).
//   MutationBalance   Roll weight and multipliers of one mutation, 1 = no effect.
//

public sealed record PreludeBalance
{
    public required int EpPerMutation { get; init; }      // per good mutation
    public required int MaxMutationEp { get; init; }      // cap of the EP all mutations bring together
    public required int ErrorRun { get; init; }           // mutations in a row that break the chain
    public required float ErrorShare { get; init; }       // share of mutations above which the chain breaks
    public required int ErrorMinLinks { get; init; }      // the share counts from this chain length on
    public required IReadOnlyDictionary<MutationKind, MutationBalance> Mutations { get; init; }
}

public sealed record MutationBalance
{
    public required float Weight { get; init; }           // relative chance in the roll

    public float Health { get; init; } = 1f;
    public float Speed { get; init; } = 1f;
    public float Magnet { get; init; } = 1f;
    public float Digest { get; init; } = 1f;
}

public sealed record CivBalance
{
    public required int FoundBuildings { get; init; }            // standing buildings a tribe needs to found its city (the hall pays the rest)
    public required float KnowledgePerVillager { get; init; }    // knowledge per minute and villager of the city
    public required IReadOnlyDictionary<TechKind, TechBalance> Techs { get; init; }
}

public sealed record TechBalance
{
    public required int Cost { get; init; }                      // knowledge it takes to research
    public required float Amount { get; init; }                  // the size of its effect, Civ/TechTree.cs says what it means per tech
}
