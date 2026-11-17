// NOTE:
//   The tech tree of the civilization: nine techs of the antique age in three tiers and
//   three rows, each needs the ones before it on its row (Philosophy also Writing). A tech
//   costs knowledge (civ.techs in data/balance.json) and is researched at once. What the
//   researched techs do together is one CivBonuses record, read by the tribe (food,
//   buildings), the rivals (damage, trade) and the city itself (knowledge, ships). Pure
//   logic, no engine; the tree view is Civ/TechTreeView.cs.
//
//   var done = new HashSet<TechKind> { TechKind.Pottery };
//   bool can = TechTree.CanResearch(TechKind.Masonry, done, knowledge: 30f);   // true
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
//   TechKind     The techs of the antique age.
//   TechInfo     A tech: its tier (column, 0 first), its row and the techs it needs.
//   CivBonuses   What the researched techs do together; NONE before any tech.
//

public enum TechKind { Pottery, Bronze, Farming, Masonry, Sailing, Writing, Philosophy, Navigation, Currency }

public readonly record struct TechInfo(TechKind Kind, int Tier, int Row, TechKind[] Needs);

public sealed record CivBonuses
{
    public static readonly CivBonuses NONE = new();

    public int FoodPerFruit { get; init; }                       // extra food per gathered fruit (pottery)
    public float Damage { get; init; } = 1f;                     // damage of the own kin (bronze)
    public float FarmFoodPerMinute { get; init; }                // food per minute and villager without any fruit (farming)
    public float BuildingHealth { get; init; } = 1f;             // health of the own buildings (masonry)
    public bool CanSail { get; init; }                           // the chieftain sails instead of swimming (sailing)
    public float ShipSpeed { get; init; } = 1f;                  // speed of the ship against walking on land (sailing, navigation)
    public float Knowledge { get; init; } = 1f;                  // knowledge of the city (writing)
    public float KnowledgePerBuilding { get; init; }             // knowledge per minute and building (philosophy)
    public float Trade { get; init; } = 1f;                      // what traders and tribute bring (currency)
}

public static class TechTree
{
    // ===========================================================================
    // =================================   TREE   ================================
    // ===========================================================================
    //
    //   TIERS / ROWS   Size of the tree.
    //   TECHS          Every tech in enum order, three rows: craft, sea, word.
    //   Get            The entry of a tech.
    //   CostOf         Knowledge a tech costs.
    //   IsOpen         Every tech it needs is researched.
    //   CanResearch    Open, not researched yet and the knowledge pays for it.
    //

    public const int TIERS = 3;
    public const int ROWS = 3;

    public static readonly IReadOnlyList<TechInfo> TECHS = new[]
    {
        new TechInfo(TechKind.Pottery,    0, 0, System.Array.Empty<TechKind>()),
        new TechInfo(TechKind.Bronze,     0, 1, System.Array.Empty<TechKind>()),
        new TechInfo(TechKind.Farming,    0, 2, System.Array.Empty<TechKind>()),
        new TechInfo(TechKind.Masonry,    1, 0, new[] { TechKind.Pottery }),
        new TechInfo(TechKind.Sailing,    1, 1, new[] { TechKind.Bronze }),
        new TechInfo(TechKind.Writing,    1, 2, new[] { TechKind.Farming }),
        new TechInfo(TechKind.Philosophy, 2, 0, new[] { TechKind.Masonry, TechKind.Writing }),
        new TechInfo(TechKind.Navigation, 2, 1, new[] { TechKind.Sailing }),
        new TechInfo(TechKind.Currency,   2, 2, new[] { TechKind.Writing }),
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
    /// <example><code>TechTree.CanResearch(TechKind.Pottery, new HashSet&lt;TechKind&gt;(), 10f)   // true</code></example>
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
    //     Pottery      + Amount food per gathered fruit
    //     Bronze       x Amount damage of the own kin
    //     Farming      + Amount food per minute and villager
    //     Masonry      x Amount health of the own buildings
    //     Sailing      the chieftain sails, Amount times the land speed
    //     Writing      x Amount knowledge
    //     Philosophy   + Amount knowledge per minute and building
    //     Navigation   x Amount speed of the ship
    //     Currency     x Amount of what traders and tribute bring
    //

    /// <summary>What a set of researched techs does together.</summary>
    /// <param name="researched">(Required) The techs researched so far.</param>
    /// <returns>The bonuses, CivBonuses.NONE for none.</returns>
    public static CivBonuses BonusesOf(IReadOnlySet<TechKind> researched)
    {
        float Amount(TechKind kind, float without) => researched.Contains(kind) ? Tune[kind].Amount : without;

        return new CivBonuses
        {
            FoodPerFruit         = (int)Amount(TechKind.Pottery, 0f),
            Damage               = Amount(TechKind.Bronze, 1f),
            FarmFoodPerMinute    = Amount(TechKind.Farming, 0f),
            BuildingHealth       = Amount(TechKind.Masonry, 1f),
            CanSail              = researched.Contains(TechKind.Sailing),
            ShipSpeed            = Amount(TechKind.Sailing, 1f) * Amount(TechKind.Navigation, 1f),
            Knowledge            = Amount(TechKind.Writing, 1f),
            KnowledgePerBuilding = Amount(TechKind.Philosophy, 0f),
            Trade                = Amount(TechKind.Currency, 1f),
        };
    }
}
