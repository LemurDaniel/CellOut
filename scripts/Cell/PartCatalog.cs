// NOTE:
//   Catalog of every part the cell editor offers: what it costs, which diet it leans
//   towards, where it can be placed and when it unlocks. Costs and unlock ranges come
//   from data/balance.json (Core/Balance.cs). Texts live in locale/translations.csv as
//   PART_<KIND>_NAME / PART_<KIND>_DESC.
//
//   PartInfo info = PartCatalog.Get(PartKind.Jaw);
//   bool isOpen   = PartCatalog.IsUnlocked(info, player.Stage, player.Diet);

//
//   Chapters:
//     TYPES     Diet classes, placements, part kinds and the info record.
//     CATALOG   The parts with their rules, lookup and unlock check.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   DietClass   Herbivore (passive), omnivore (allround), carnivore (aggressive).
//   Placement   Rim of the membrane or inside the cell.
//   PartKind    Every placeable part.
//   PartInfo    Rules of one part kind.
//

public enum DietClass { Herbivore, Omnivore, Carnivore }

public enum Placement { Rim, Inner }

public enum PartKind
{
    // Herbivore
    Chloroplast, FilterMouth, ArmorPlate,
    // Omnivore
    Flagellum, Eyespot, Vacuole, Cilia,
    // Carnivore
    Spike, Jaw, Toxin,
}

/// <summary>Rules of one part kind.</summary>
/// <param name="Kind">The part.</param>
/// <param name="Diet">Diet class, sets the lean on the diet bar and the palette column.</param>
/// <param name="Cost">Evolution points to place it, refunded on removal.</param>
/// <param name="Placement">Where it sits.</param>
/// <param name="MinStage">First stage that offers it.</param>
/// <param name="MinDiet">Lowest diet value (-1 herbivore .. 1 carnivore) that unlocks it.</param>
/// <param name="MaxDiet">Highest diet value that unlocks it.</param>
public sealed record PartInfo(PartKind Kind, DietClass Diet, int Cost, Placement Placement, int MinStage, float MinDiet, float MaxDiet)
{
    public string NameKey => $"PART_{Kind}_NAME".ToUpperInvariant();
    public string DescriptionKey => $"PART_{Kind}_DESC".ToUpperInvariant();

    /// <summary>Lean on the diet bar: -1 herbivore, 0 omnivore, 1 carnivore.</summary>
    public float Lean => Diet switch
    {
        DietClass.Herbivore => -1f,
        DietClass.Carnivore => 1f,
        _                   => 0f,
    };

    /// <summary>Whether the part needs a diet position to unlock.</summary>
    public bool HasDietLock => MinDiet > -1f || MaxDiet < 1f;
}

public static class PartCatalog
{
    // ===========================================================================
    // ===============================   CATALOG   ===============================
    // ===========================================================================
    //
    //   PARTS            Every part with cost, diet, placement and unlock range.
    //   Part             One table row, numbers from data/balance.json.
    //   All             Every part in palette order.
    //   Get              Info of one kind.
    //   IsUnlocked       Stage and diet position allow the part.
    //

    private const int SINGLE_STAGE = 1;

    /*
      The table holds what a part is (diet class, placement), data/balance.json what it
      costs and where on the diet bar (-1 herbivore .. 1 carnivore) it unlocks:
      - Basic parts of every class: always.
      - Armor plate:                herbivore side.
      - Cilia:                      the middle.
      - Jaw:                        carnivore side.
      - Toxin:                      clearly carnivore.
    */
    private static readonly PartInfo[] PARTS =
    {
        Part(PartKind.Chloroplast, DietClass.Herbivore, Placement.Inner),
        Part(PartKind.FilterMouth, DietClass.Herbivore, Placement.Rim),
        Part(PartKind.ArmorPlate,  DietClass.Herbivore, Placement.Rim),
        Part(PartKind.Flagellum,   DietClass.Omnivore,  Placement.Rim),
        Part(PartKind.Eyespot,     DietClass.Omnivore,  Placement.Rim),
        Part(PartKind.Vacuole,     DietClass.Omnivore,  Placement.Inner),
        Part(PartKind.Cilia,       DietClass.Omnivore,  Placement.Rim),
        Part(PartKind.Spike,       DietClass.Carnivore, Placement.Rim),
        Part(PartKind.Jaw,         DietClass.Carnivore, Placement.Rim),
        Part(PartKind.Toxin,       DietClass.Carnivore, Placement.Inner),
    };

    /// <summary>One table row: cost and unlock range come from Balance.Current.Cell.Parts.</summary>
    private static PartInfo Part(PartKind kind, DietClass diet, Placement placement)
    {
        CellPartBalance tune = Balance.Current.Cell.Parts[kind];
        return new PartInfo(kind, diet, tune.Cost, placement, SINGLE_STAGE, tune.MinDiet, tune.MaxDiet);
    }

    /// <summary>Every part in palette order (herbivore, omnivore, carnivore).</summary>
    public static IReadOnlyList<PartInfo> All()
    {
        return PARTS;
    }

    /// <summary>Info of one part kind.</summary>
    /// <param name="kind">(Required) The part.</param>
    public static PartInfo Get(PartKind kind)
    {
        PartInfo info = PARTS.FirstOrDefault(part => kind == part.Kind);
        if (null == info)
        {
            throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be listed in PARTS, got '{kind}'.");
        }
        return info;
    }

    /// <summary>Whether stage and diet position unlock the part.</summary>
    /// <param name="info">(Required) The part.</param>
    /// <param name="stage">(Required) Current player stage.</param>
    /// <param name="diet">(Required) Current diet value, -1 herbivore .. 1 carnivore.</param>
    /// <example><code>PartCatalog.IsUnlocked(PartCatalog.Get(PartKind.Jaw), 1, 0.3f)  // true</code></example>
    public static bool IsUnlocked(PartInfo info, int stage, float diet)
    {
        return stage >= info.MinStage
            && diet >= info.MinDiet - Mathf.Epsilon
            && diet <= info.MaxDiet + Mathf.Epsilon;
    }
}
