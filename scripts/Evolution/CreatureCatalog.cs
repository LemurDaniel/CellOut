// NOTE:
//   Catalog of every part the creature editor offers once the cluster became an
//   organism: what it costs, where it sits on the body and which body type may use it
//   (fish or marine mammal, chosen first in the CreatureEditor). Legs and lung lead the
//   way onto land (Player, LAND chapter). Texts live in
//   locale/translations.csv as CREATURE_<KIND>_NAME / CREATURE_<KIND>_DESC.
//
//   CreaturePartInfo info = CreatureCatalog.Get(CreaturePartKind.Fluke);
//   bool isOffered        = info.IsFor(CreatureType.MarineMammal);   // true

//
//   Chapters:
//     TYPES     Body types, mounts, part kinds and the info record.
//     CATALOG   The parts with their rules and the lookup.
//

using System.Collections.Generic;
using System.Linq;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   CreatureType       Fish (side to side) or marine mammal (up and down, breathes air).
//   CreatureMount      Where a part sits: tail end, nose, back, both flanks or inside.
//   CreaturePartKind   Every placeable creature part.
//   CreaturePartInfo   Rules of one part kind.
//

public enum CreatureType { Fish, MarineMammal }

public enum CreatureMount { Tail, Head, Top, Side, Inner }

public enum CreaturePartKind { CaudalFin, Fluke, DorsalFin, PectoralFin, Eye, Jaw, Spines, Blowhole, Legs, Lung }

/// <summary>Rules of one creature part kind.</summary>
/// <param name="Kind">The part.</param>
/// <param name="Cost">Evolution points to place it, refunded on removal.</param>
/// <param name="Mount">Where it sits, tail and head hold one part each, side parts come in pairs.</param>
/// <param name="OnlyFor">Body type that may use it, null for both.</param>
public sealed record CreaturePartInfo(CreaturePartKind Kind, int Cost, CreatureMount Mount, CreatureType? OnlyFor)
{
    public string NameKey => $"CREATURE_{Kind}_NAME".ToUpperInvariant();
    public string DescriptionKey => $"CREATURE_{Kind}_DESC".ToUpperInvariant();

    /// <summary>Tail and head hold exactly one part.</summary>
    public bool IsSingleSlot => CreatureMount.Tail == Mount || CreatureMount.Head == Mount;

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
    //   PARTS   Every part with cost, mount and body type.
    //   All     Every part in palette order.
    //   ForType The parts a body type may use, in palette order.
    //   Get     Info of one kind.
    //

    private static readonly CreaturePartInfo[] PARTS =
    {
        new(CreaturePartKind.CaudalFin,   2, CreatureMount.Tail,  CreatureType.Fish),
        new(CreaturePartKind.Fluke,       2, CreatureMount.Tail,  CreatureType.MarineMammal),
        new(CreaturePartKind.DorsalFin,   1, CreatureMount.Top,   null),
        new(CreaturePartKind.PectoralFin, 1, CreatureMount.Side,  null),
        new(CreaturePartKind.Eye,         1, CreatureMount.Side,  null),
        new(CreaturePartKind.Jaw,         2, CreatureMount.Head,  null),
        new(CreaturePartKind.Spines,      2, CreatureMount.Top,   CreatureType.Fish),
        new(CreaturePartKind.Blowhole,    1, CreatureMount.Top,   CreatureType.MarineMammal),
        new(CreaturePartKind.Legs,        2, CreatureMount.Side,  null),
        new(CreaturePartKind.Lung,        2, CreatureMount.Inner, CreatureType.Fish),
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
