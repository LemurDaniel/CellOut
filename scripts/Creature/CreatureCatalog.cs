// NOTE:
//   Catalog of every part the creature editor offers once the cluster became an
//   organism: its category tab, cost, weight, where it sits on the body and which body
//   type may use it (fish or marine mammal, chosen first in the CreatureEditor). Legs
//   carry weight on land (Carry), wings lift it (Lift, flying itself comes later), see
//   CreatureStats. Cost, weight, carry, stride and lift come from data/balance.json
//   (Core/Balance.cs). Texts live in locale/translations.csv as CREATURE_<KIND>_NAME /
//   CREATURE_<KIND>_DESC, the tabs as CREATURE_CATEGORY_<CATEGORY>.
//
//   CreaturePartInfo info = CreatureCatalog.Get(CreaturePartKind.Fluke);
//   bool isOffered        = info.IsFor(CreatureType.MarineMammal);   // true

//
//   Chapters:
//     TYPES     Body types, categories, mounts, part kinds and the info record.
//     CATALOG   The parts with their rules and the lookups.
//

using System.Collections.Generic;
using System.Linq;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   CreatureType       Fish (side to side) or marine mammal (up and down, breathes air).
//   CreatureCategory   Tab of the editor palette, in tab order.
//   CreatureMount      Where a part sits: tail end, nose, skull, back, flanks, limbs or inside.
//   CreaturePartKind   Every placeable creature part.
//   CreaturePartInfo   Rules of one part kind.
//

public enum CreatureType { Fish, MarineMammal }

public enum CreatureCategory { Fins, Weapons, Eyes, Limbs, Feet, Hands, Brain, Lungs, Wings }

/*
  Mounts share their spots: two parts on the same mount keep a gap along the spine.
  Tail, head and skull hold one part each. Flank (eyes) and limb (fins, legs, hands)
  are separate rows on the side, so eyes never block a pair of legs.
*/
public enum CreatureMount { Tail, Head, Skull, Top, Flank, Limb, Inner }

public enum CreaturePartKind
{
    // Fins
    CaudalFin, Fluke, DorsalFin, PectoralFin,
    // Weapons
    Jaw, Spines,
    // Eyes
    PitEye, Eye, StalkEye,
    // Limbs: sculpted, 1 to 4 joints
    StubLimb, BentLimb, Limb, Tentacle,
    // Feet
    Legs, StubbyLegs, ClawLegs, CrabLegs,
    // Hands
    Hands, Claws,
    // Brain
    SmallBrain, BigBrain,
    // Lungs
    Lung, Blowhole, BigLung,
    // Wings
    MembraneWings, FeatherWings,
}

/// <summary>Rules of one creature part kind.</summary>
/// <param name="Kind">The part.</param>
/// <param name="Category">Palette tab it is listed under.</param>
/// <param name="Cost">Evolution points to place it, refunded on removal.</param>
/// <param name="Weight">Added to the body weight (kg in design scale), pairs count once.</param>
/// <param name="Mount">Where it sits, tail, head and skull hold one part each, flank and limb parts come in pairs (or single halves with symmetry off).</param>
/// <param name="OnlyFor">Body type that may use it, null for both.</param>
/// <param name="Carry">(Optional) Weight a pair of legs carries on land. Defaults to 0.</param>
/// <param name="Stride">(Optional) Walking speed of a leg kind, 1 = normal legs. Defaults to 0.</param>
/// <param name="Lift">(Optional) Weight a pair of wings lifts. Defaults to 0.</param>
public sealed record CreaturePartInfo(
    CreaturePartKind Kind,
    CreatureCategory Category,
    int Cost,
    float Weight,
    CreatureMount Mount,
    CreatureType? OnlyFor,
    float Carry = 0f,
    float Stride = 0f,
    float Lift = 0f)
{
    public string NameKey => $"CREATURE_{Kind}_NAME".ToUpperInvariant();
    public string DescriptionKey => $"CREATURE_{Kind}_DESC".ToUpperInvariant();

    /// <summary>Tail, head and skull hold exactly one part.</summary>
    public bool IsSingleSlot => CreatureMount.Tail == Mount || CreatureMount.Head == Mount || CreatureMount.Skull == Mount;

    public bool IsLeg => Carry > 0f;

    public bool IsWing => Lift > 0f;

    /// <summary>Sculpted limbs (Limbs tab): a chain of joints the editor pulls out, see CreatureDesign.DefaultJoints.</summary>
    public bool IsSculpted => CreatureCategory.Limbs == Category;

    /// <summary>Feet and hands: the end of a sculpted limb. Dropped on the body they bring a fitting limb along.</summary>
    public bool IsEnd => CreatureCategory.Feet == Category || CreatureCategory.Hands == Category;

    /// <summary>Flank, limb parts and wings come as a mirrored pair, with symmetry off as a single half.</summary>
    public bool IsPaired => CreatureMount.Flank == Mount || CreatureMount.Limb == Mount || IsWing;

    /// <summary>Evolution points for a pair (side 0) or a single half (side -1 or 1, rounded up).</summary>
    /// <param name="side">(Required) 0 for the pair or a midline part, -1 left or 1 right for a single half.</param>
    /// <example><code>CreatureCatalog.Get(CreaturePartKind.Hands).CostFor(1)   // 1 instead of 2</code></example>
    public int CostFor(int side)
    {
        return 0 == side || !IsPaired ? Cost : (Cost + 1) / 2;
    }

    /// <summary>Whether a body of the given type may carry the part.</summary>
    public bool IsFor(CreatureType type)
    {
        return null == OnlyFor || type == OnlyFor;
    }
}

public static class CreatureCatalog
{
    // ===========================================================================
    // ===============================   CATALOG   ===============================
    // ===========================================================================
    //
    //   PARTS        Every part with category, cost, weight, mount and body type.
    //   Part         One table row, numbers from data/balance.json.
    //   All          Every part in palette order.
    //   ForType      The parts a body type may use, in palette order.
    //   ForCategory  The parts of one tab a body type may use.
    //   Get          Info of one kind.
    //

    /*
      The table holds what a part is (tab, mount, body type), data/balance.json its
      numbers. Weights are kg of design scale: the clay blob weighs about 10 (CreatureStats).
      Normal legs carry a little more than that, so a blob with fins walks at about full
      speed on one pair. A slim body with two pairs of feather wings could fly, a fat one never.
    */
    private static readonly CreaturePartInfo[] PARTS =
    {
        // Fins
        Part(CreaturePartKind.CaudalFin,     CreatureCategory.Fins,    CreatureMount.Tail,  CreatureType.Fish),
        Part(CreaturePartKind.Fluke,         CreatureCategory.Fins,    CreatureMount.Tail,  CreatureType.MarineMammal),
        Part(CreaturePartKind.DorsalFin,     CreatureCategory.Fins,    CreatureMount.Top,   null),
        Part(CreaturePartKind.PectoralFin,   CreatureCategory.Fins,    CreatureMount.Limb,  null),
        // Weapons
        Part(CreaturePartKind.Jaw,           CreatureCategory.Weapons, CreatureMount.Head,  null),
        Part(CreaturePartKind.Spines,        CreatureCategory.Weapons, CreatureMount.Top,   CreatureType.Fish),
        // Eyes
        Part(CreaturePartKind.PitEye,        CreatureCategory.Eyes,    CreatureMount.Flank, null),
        Part(CreaturePartKind.Eye,           CreatureCategory.Eyes,    CreatureMount.Flank, null),
        Part(CreaturePartKind.StalkEye,      CreatureCategory.Eyes,    CreatureMount.Flank, null),
        // Limbs: sculpted by their joints (1 to 4), a limb reaching the ground carries (CreatureStats)
        Part(CreaturePartKind.StubLimb,      CreatureCategory.Limbs,   CreatureMount.Limb,  null),
        Part(CreaturePartKind.BentLimb,      CreatureCategory.Limbs,   CreatureMount.Limb,  null),
        Part(CreaturePartKind.Limb,          CreatureCategory.Limbs,   CreatureMount.Limb,  null),
        Part(CreaturePartKind.Tentacle,      CreatureCategory.Limbs,   CreatureMount.Limb,  null),
        // Feet
        Part(CreaturePartKind.Legs,          CreatureCategory.Feet,    CreatureMount.Limb,  null),
        Part(CreaturePartKind.StubbyLegs,    CreatureCategory.Feet,    CreatureMount.Limb,  null),
        Part(CreaturePartKind.ClawLegs,      CreatureCategory.Feet,    CreatureMount.Limb,  null),
        Part(CreaturePartKind.CrabLegs,      CreatureCategory.Feet,    CreatureMount.Limb,  null),
        // Hands
        Part(CreaturePartKind.Hands,         CreatureCategory.Hands,   CreatureMount.Limb,  null),
        Part(CreaturePartKind.Claws,         CreatureCategory.Hands,   CreatureMount.Limb,  null),
        // Brain
        Part(CreaturePartKind.SmallBrain,    CreatureCategory.Brain,   CreatureMount.Skull, null),
        Part(CreaturePartKind.BigBrain,      CreatureCategory.Brain,   CreatureMount.Skull, null),
        // Lungs
        Part(CreaturePartKind.Lung,          CreatureCategory.Lungs,   CreatureMount.Inner, CreatureType.Fish),
        Part(CreaturePartKind.Blowhole,      CreatureCategory.Lungs,   CreatureMount.Top,   CreatureType.MarineMammal),
        Part(CreaturePartKind.BigLung,       CreatureCategory.Lungs,   CreatureMount.Inner, null),
        // Wings
        Part(CreaturePartKind.MembraneWings, CreatureCategory.Wings,   CreatureMount.Top,   null),
        Part(CreaturePartKind.FeatherWings,  CreatureCategory.Wings,   CreatureMount.Top,   null),
    };

    /// <summary>One table row: cost, weight, carry, stride and lift come from Balance.Current.Creature.Parts.</summary>
    private static CreaturePartInfo Part(CreaturePartKind kind, CreatureCategory category, CreatureMount mount, CreatureType? onlyFor)
    {
        CreaturePartBalance tune = Balance.Current.Creature.Parts[kind];
        return new CreaturePartInfo(kind, category, tune.Cost, tune.Weight, mount, onlyFor, tune.Carry, tune.Stride, tune.Lift);
    }

    /// <summary>Every part in palette order.</summary>
    public static IReadOnlyList<CreaturePartInfo> All()
    {
        return PARTS;
    }

    /// <summary>The parts a body type may use, in palette order.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    public static IReadOnlyList<CreaturePartInfo> ForType(CreatureType type)
    {
        return PARTS.Where(part => part.IsFor(type)).ToList();
    }

    /// <summary>The parts of one palette tab a body type may use.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <param name="category">(Required) The tab.</param>
    /// <example><code>CreatureCatalog.ForCategory(CreatureType.Fish, CreatureCategory.Lungs)   // Lung, BigLung</code></example>
    public static IReadOnlyList<CreaturePartInfo> ForCategory(CreatureType type, CreatureCategory category)
    {
        return PARTS.Where(part => category == part.Category && part.IsFor(type)).ToList();
    }

    /// <summary>Info of one part kind.</summary>
    /// <param name="kind">(Required) The part.</param>
    public static CreaturePartInfo Get(CreaturePartKind kind)
    {
        CreaturePartInfo info = PARTS.FirstOrDefault(part => kind == part.Kind);
        if (null == info)
        {
            throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be listed in PARTS, got '{kind}'.");
        }
        return info;
    }
}
