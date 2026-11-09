// NOTE:
//   Catalog of every part the cell editor offers: what it costs, which diet it leans
//   towards, where it can be placed and when it unlocks. Texts live in
//   locale/translations.csv as PART_<KIND>_NAME / PART_<KIND>_DESC.
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
    //   All              Every part in palette order.
    //   Get              Info of one kind.
    //   IsUnlocked       Stage and diet position allow the part.
    //

    private const int SINGLE_STAGE = 1;
    private const float ANY_MIN = -1f;
    private const float ANY_MAX = 1f;

    /*
      Unlock ranges on the diet bar (-1 herbivore .. 1 carnivore):
      - Basic parts of every class: always.
      - Armor plate, armor cell:    herbivore side, diet <= -0.2.
      - Cilia:                      the middle, -0.5 <= diet <= 0.5.
      - Jaw, spike cell:            carnivore side, diet >= 0.2.
      - Toxin:                      clearly carnivore, diet >= 0.5.
    */
    private static readonly PartInfo[] PARTS =
    {
        new(PartKind.Chloroplast, DietClass.Herbivore, 4, Placement.Inner,  SINGLE_STAGE, ANY_MIN, ANY_MAX),
        new(PartKind.FilterMouth, DietClass.Herbivore, 2, Placement.Rim,    SINGLE_STAGE, ANY_MIN, ANY_MAX),
        new(PartKind.ArmorPlate,  DietClass.Herbivore, 4, Placement.Rim,    SINGLE_STAGE, ANY_MIN, -0.2f),
        new(PartKind.Flagellum,   DietClass.Omnivore,  4, Placement.Rim,    SINGLE_STAGE, ANY_MIN, ANY_MAX),
        new(PartKind.Eyespot,     DietClass.Omnivore,  2, Placement.Rim,    SINGLE_STAGE, ANY_MIN, ANY_MAX),
        new(PartKind.Vacuole,     DietClass.Omnivore,  4, Placement.Inner,  SINGLE_STAGE, ANY_MIN, ANY_MAX),
        new(PartKind.Cilia,       DietClass.Omnivore,  5, Placement.Rim,    SINGLE_STAGE, -0.5f,   0.5f),
        new(PartKind.Spike,       DietClass.Carnivore, 2, Placement.Rim,    SINGLE_STAGE, ANY_MIN, ANY_MAX),
        new(PartKind.Jaw,         DietClass.Carnivore, 5, Placement.Rim,    SINGLE_STAGE, 0.2f,    ANY_MAX),
        new(PartKind.Toxin,       DietClass.Carnivore, 6, Placement.Inner,  SINGLE_STAGE, 0.5f,    ANY_MAX),
    };

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
