// NOTE:
//   Catalog of every part the creature editor offers once the cluster became an
//   organism: its category tab, cost, weight, where it sits on the body and which body
//   type may use it (fish or marine mammal, chosen first in the CreatureEditor). Legs
//   carry weight on land (Carry), wings lift it (Lift, flying itself comes later), see
//   CreatureStats. Texts live in locale/translations.csv as CREATURE_<KIND>_NAME /
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
    Legs, StubbyLegs, ClawLegs,
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
    //   All          Every part in palette order.
    //   ForType      The parts a body type may use, in palette order.
    //   ForCategory  The parts of one tab a body type may use.
    //   Get          Info of one kind.
    //

    /*
      Weights in kg of design scale: the clay blob weighs about 10 (CreatureStats).
      Normal legs carry 14, so a blob with fins walks at about full speed on one pair.
      Feather wings lift 10: a slim body with two pairs could fly, a fat one never.
    */
    private static readonly CreaturePartInfo[] PARTS =
    {
        // Fins
        new(CreaturePartKind.CaudalFin,     CreatureCategory.Fins,    2, 0.6f, CreatureMount.Tail,  CreatureType.Fish),
        new(CreaturePartKind.Fluke,         CreatureCategory.Fins,    2, 0.8f, CreatureMount.Tail,  CreatureType.MarineMammal),
        new(CreaturePartKind.DorsalFin,     CreatureCategory.Fins,    1, 0.3f, CreatureMount.Top,   null),
        new(CreaturePartKind.PectoralFin,   CreatureCategory.Fins,    1, 0.4f, CreatureMount.Limb,  null),
        // Weapons
        new(CreaturePartKind.Jaw,           CreatureCategory.Weapons, 2, 0.9f, CreatureMount.Head,  null),
        new(CreaturePartKind.Spines,        CreatureCategory.Weapons, 2, 0.7f, CreatureMount.Top,   CreatureType.Fish),
        // Eyes
        new(CreaturePartKind.PitEye,        CreatureCategory.Eyes,    1, 0.1f, CreatureMount.Flank, null),
        new(CreaturePartKind.Eye,           CreatureCategory.Eyes,    2, 0.3f, CreatureMount.Flank, null),
        new(CreaturePartKind.StalkEye,      CreatureCategory.Eyes,    3, 0.6f, CreatureMount.Flank, null),
        // Limbs: sculpted by their joints (1 to 4), a limb reaching the ground carries (CreatureStats)
        new(CreaturePartKind.StubLimb,      CreatureCategory.Limbs,   1, 0.3f, CreatureMount.Limb,  null),
        new(CreaturePartKind.BentLimb,      CreatureCategory.Limbs,   1, 0.45f, CreatureMount.Limb, null),
        new(CreaturePartKind.Limb,          CreatureCategory.Limbs,   1, 0.6f, CreatureMount.Limb,  null),
        new(CreaturePartKind.Tentacle,      CreatureCategory.Limbs,   2, 0.7f, CreatureMount.Limb,  null),
        // Feet
        new(CreaturePartKind.Legs,          CreatureCategory.Feet,    2, 1.5f, CreatureMount.Limb,  null, Carry: 14f, Stride: 1f),
        new(CreaturePartKind.StubbyLegs,    CreatureCategory.Feet,    2, 1.8f, CreatureMount.Limb,  null, Carry: 20f, Stride: 0.75f),
        new(CreaturePartKind.ClawLegs,      CreatureCategory.Feet,    3, 2.0f, CreatureMount.Limb,  null, Carry: 12f, Stride: 1.15f),
        // Hands
        new(CreaturePartKind.Hands,         CreatureCategory.Hands,   2, 0.8f, CreatureMount.Limb,  null),
        new(CreaturePartKind.Claws,         CreatureCategory.Hands,   3, 1.0f, CreatureMount.Limb,  null),
        // Brain
        new(CreaturePartKind.SmallBrain,    CreatureCategory.Brain,   2, 0.4f, CreatureMount.Skull, null),
        new(CreaturePartKind.BigBrain,      CreatureCategory.Brain,   4, 1.2f, CreatureMount.Skull, null),
        // Lungs
        new(CreaturePartKind.Lung,          CreatureCategory.Lungs,   2, 0.8f, CreatureMount.Inner, CreatureType.Fish),
        new(CreaturePartKind.Blowhole,      CreatureCategory.Lungs,   1, 0.3f, CreatureMount.Top,   CreatureType.MarineMammal),
        new(CreaturePartKind.BigLung,       CreatureCategory.Lungs,   3, 1.5f, CreatureMount.Inner, null),
        // Wings
        new(CreaturePartKind.MembraneWings, CreatureCategory.Wings,   3, 1.5f, CreatureMount.Top,   null, Lift: 6f),
        new(CreaturePartKind.FeatherWings,  CreatureCategory.Wings,   4, 2.5f, CreatureMount.Top,   null, Lift: 10f),
    };

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
