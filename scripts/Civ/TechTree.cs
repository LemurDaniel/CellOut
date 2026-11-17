// NOTE:
//   The tech tree of the civilization: nine techs of the modern age in three tiers and
//   three rows, each needs the ones before it on its row (University also Computer).
//   (Until 2026-11-17 the same nine were antique: pottery, bronze, farming, masonry,
//   sailing, writing, philosophy, navigation, currency; SaveCodec.Migrate maps old saves.) A tech
//   costs knowledge (civ.techs in data/balance.json) and is researched at once. What the
//   researched techs do together is one CivBonuses record, read by the tribe (food,
//   buildings), the rivals (damage, trade) and the city itself (knowledge, ships). Pure
//   logic, no engine; the tree view is Civ/TechTreeView.cs.
//
//   var done = new HashSet<TechKind> { TechKind.Cooling };
//   bool can = TechTree.CanResearch(TechKind.Concrete, done, knowledge: 30f);   // true
//   CivBonuses bonuses = TechTree.BonusesOf(done);                           // FoodPerFruit 1

//
//   Chapters:
//     TYPES     The techs, their place in the tree and the bonuses they add up to.
//     TREE      Every tech with what it needs, costs and whether it can be researched.
//     BONUSES   What a set of researched techs does.
//

using System.Collections.Generic;
using System.Linq;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   TechKind     The techs of the modern age.
//   TechInfo     A tech: its tier (column, 0 first), its row and the techs it needs.
//   CivBonuses   What the researched techs do together; NONE before any tech.
//

public enum TechKind { Cooling, Steel, Tractor, Concrete, Engine, Computer, University, Radar, Bank }

public readonly record struct TechInfo(TechKind Kind, int Tier, int Row, TechKind[] Needs);

public sealed record CivBonuses
{
    public static readonly CivBonuses NONE = new();

    public int FoodPerFruit { get; init; }                       // extra food per gathered fruit (cooling)
    public float Damage { get; init; } = 1f;                     // damage of the own kin (steel)
    public float FarmFoodPerMinute { get; init; }                // food per minute and villager without any fruit (tractor)
    public float BuildingHealth { get; init; } = 1f;             // health of the own buildings (concrete)
    public bool CanSail { get; init; }                           // the chieftain takes the ship instead of swimming (engine)
    public float ShipSpeed { get; init; } = 1f;                  // speed of the ship against walking on land (engine, radar)
    public float Knowledge { get; init; } = 1f;                  // knowledge of the city (computer)
    public float KnowledgePerBuilding { get; init; }             // knowledge per minute and building (university)
    public float Trade { get; init; } = 1f;                      // what traders and tribute bring (bank)
}

public static class TechTree
{
    // ===========================================================================
    // =================================   TREE   ================================
    // ===========================================================================
    //
    //   TIERS / ROWS   Size of the tree.
    //   TECHS          Every tech in enum order, three rows: industry, sea, knowledge.
    //   Get            The entry of a tech.
    //   CostOf         Knowledge a tech costs.
    //   IsOpen         Every tech it needs is researched.
    //   CanResearch    Open, not researched yet and the knowledge pays for it.
    //

    public const int TIERS = 3;
    public const int ROWS = 3;

    public static readonly IReadOnlyList<TechInfo> TECHS = new[]
    {
        new TechInfo(TechKind.Cooling,    0, 0, System.Array.Empty<TechKind>()),
        new TechInfo(TechKind.Steel,     0, 1, System.Array.Empty<TechKind>()),
        new TechInfo(TechKind.Tractor,    0, 2, System.Array.Empty<TechKind>()),
        new TechInfo(TechKind.Concrete,    1, 0, new[] { TechKind.Cooling }),
        new TechInfo(TechKind.Engine,    1, 1, new[] { TechKind.Steel }),
        new TechInfo(TechKind.Computer,    1, 2, new[] { TechKind.Tractor }),
        new TechInfo(TechKind.University, 2, 0, new[] { TechKind.Concrete, TechKind.Computer }),
        new TechInfo(TechKind.Radar, 2, 1, new[] { TechKind.Engine }),
        new TechInfo(TechKind.Bank,   2, 2, new[] { TechKind.Computer }),
    };

    private static IReadOnlyDictionary<TechKind, TechBalance> Tune => Balance.Current.Civ.Techs;

    /// <summary>The entry of a tech. TECHS lists the kinds in enum order.</summary>
    public static TechInfo Get(TechKind kind)
    {
        return TECHS[(int)kind];
    }

    /// <summary>Knowledge a tech costs to research.</summary>
    public static int CostOf(TechKind kind)
    {
        return Tune[kind].Cost;
    }

    /// <summary>Whether every tech a tech needs is researched.</summary>
    /// <param name="kind">(Required) The tech.</param>
    /// <param name="researched">(Required) The techs researched so far.</param>
    public static bool IsOpen(TechKind kind, IReadOnlySet<TechKind> researched)
    {
        return Get(kind).Needs.All(researched.Contains);
    }

    /// <summary>Whether a tech can be researched right now.</summary>
    /// <param name="kind">(Required) The tech.</param>
    /// <param name="researched">(Required) The techs researched so far.</param>
    /// <param name="knowledge">(Required) The knowledge of the city.</param>
    /// <example><code>TechTree.CanResearch(TechKind.Cooling, new HashSet&lt;TechKind&gt;(), 10f)   // true</code></example>
    public static bool CanResearch(TechKind kind, IReadOnlySet<TechKind> researched, float knowledge)
    {
        return !researched.Contains(kind) && IsOpen(kind, researched) && knowledge >= CostOf(kind);
    }

    // ===========================================================================
    // ===============================   BONUSES   ===============================
    // ===========================================================================
    //
    //   BonusesOf   Adds up what the researched techs do, every Amount from civ.techs:
    //
    //     Cooling      + Amount food per gathered fruit
    //     Steel        x Amount damage of the own kin
    //     Tractor      + Amount food per minute and villager
    //     Concrete     x Amount health of the own buildings
    //     Engine       the chieftain takes the ship, Amount times the land speed
    //     Computer     x Amount knowledge
    //     University   + Amount knowledge per minute and building
    //     Radar        x Amount speed of the ship
    //     Bank         x Amount of what traders and tribute bring
    //

    /// <summary>What a set of researched techs does together.</summary>
    /// <param name="researched">(Required) The techs researched so far.</param>
    /// <returns>The bonuses, CivBonuses.NONE for none.</returns>
    public static CivBonuses BonusesOf(IReadOnlySet<TechKind> researched)
    {
        float Amount(TechKind kind, float without) => researched.Contains(kind) ? Tune[kind].Amount : without;

        return new CivBonuses
        {
            FoodPerFruit         = (int)Amount(TechKind.Cooling, 0f),
            Damage               = Amount(TechKind.Steel, 1f),
            FarmFoodPerMinute    = Amount(TechKind.Tractor, 0f),
            BuildingHealth       = Amount(TechKind.Concrete, 1f),
            CanSail              = researched.Contains(TechKind.Engine),
            ShipSpeed            = Amount(TechKind.Engine, 1f) * Amount(TechKind.Radar, 1f),
            Knowledge            = Amount(TechKind.Computer, 1f),
            KnowledgePerBuilding = Amount(TechKind.University, 0f),
            Trade                = Amount(TechKind.Bank, 1f),
        };
    }
}
