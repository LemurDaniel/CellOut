// NOTE:
//   The ready-made body plans: the clay blob of the editor, the starter body after the
//   type choice, the examples on the type tiles and the NPC creatures of the organism
//   stage. Reached through CreatureDesign (Blob, Starter, Example, ForCritter).
//   Blob, Starter and Example are fixed; ForCritter rolls the hunter and the girth
//   jitter with Core/Rand.cs (the global Godot generator).
//
//   CreatureDesign fish = BodyPlans.Starter(CreatureType.Fish);     // tail fin and eyes for free
//   CreatureDesign npc  = BodyPlans.ForCritter(CritterKind.Hunter); // shark or orca

//
//   Chapters:
//     BUILD BODY PLANS   The clay blob, the starter bodies, the type examples and the NPC creatures.
//

using Godot;

namespace CellOut;

public static class BodyPlans
{
    // ===========================================================================
    // ==========================   BUILD BODY PLANS   ===========================
    // ===========================================================================
    //
    //   Blob         The fresh clay blob the editor starts from.
    //   Starter      The body the clay takes once the type is chosen.
    //   Example      A finished example of a body type for the type choice tiles.
    //   ForCritter   A body plan for an NPC creature of the organism stage.
    //

    private const float NPC_GIRTH_JITTER = 0.08f;
    private static readonly Color ORCA_PATCH_COLOR = new(0.95f, 0.95f, 0.92f);

    /// <summary>The fresh clay blob the editor starts from: five round vertebrae, no parts.</summary>
    /// <example><code>BodyPlans.Blob().Girths.Count   // 5</code></example>
    public static CreatureDesign Blob()
    {
        return CreatureDesign.FromGirths(CreatureType.Fish, 0.62f, 0.9f, 1f, 0.9f, 0.62f);
    }

    /*
      Starter body after the type choice, so the first creature already reads as an animal:
      - Fish:           slim tail stalk, deepest just behind the head, a blunt head
      - Marine mammal:  longer, a thick chest and a narrow tail stock like a dolphin
      Both get their tail (caudal fin or fluke) and a pair of eyes for free (paid 0, so
      removing them refunds nothing).
    */
    /// <summary>The body the clay takes once the type is chosen: streamlined, with tail and eyes.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <example><code>BodyPlans.Starter(CreatureType.Fish).Parts.Count   // 2</code></example>
    public static CreatureDesign Starter(CreatureType type)
    {
        CreatureDesign design = CreatureType.Fish == type
            ? CreatureDesign.FromGirths(type, 0.3f, 0.48f, 0.7f, 0.86f, 0.84f, 0.62f)
            : CreatureDesign.FromGirths(type, 0.26f, 0.42f, 0.66f, 0.88f, 0.92f, 0.82f, 0.6f);
        design.TryPlace(CreatureType.Fish == type ? CreaturePartKind.CaudalFin : CreaturePartKind.Fluke, 0f, 0);
        design.TryPlace(CreaturePartKind.Eye, 0.86f, 0);
        return design;
    }

    /// <summary>A finished example of a body type, shown on the type choice tiles.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <example><code>BodyPlans.Example(CreatureType.MarineMammal).Count(CreaturePartKind.Blowhole)   // 1</code></example>
    public static CreatureDesign Example(CreatureType type)
    {
        CreatureDesign design = CreatureType.Fish == type
            ? CreatureDesign.FromGirths(type, 0.35f, 0.6f, 0.8f, 0.85f, 0.7f, 0.5f)
            : CreatureDesign.FromGirths(type, 0.3f, 0.55f, 0.8f, 0.95f, 0.9f, 0.75f, 0.6f);
        design.TryPlace(CreatureType.Fish == type ? CreaturePartKind.CaudalFin : CreaturePartKind.Fluke, 0f, 0);
        design.TryPlace(CreaturePartKind.DorsalFin, 0.5f, 0);
        design.TryPlace(CreaturePartKind.PectoralFin, 0.68f, 0);
        design.TryPlace(CreaturePartKind.Eye, 0.88f, 0);
        if (CreatureType.MarineMammal == type)
        {
            design.TryPlace(CreaturePartKind.Blowhole, 0.78f, 0);
        }
        return design;
    }

    /*
      NPC creatures per critter kind, the girths jitter a little:
      - Bacterium:  small slim fish with a filter mouth, flees, striped
      - Amoeba:     round puffer with spines and a beak, spotted
      - Hunter:     shark (fish, dark saddle) or orca (marine mammal, white patches), with jaw
    */
    /// <summary>A body plan for an NPC creature of the organism stage.</summary>
    /// <param name="kind">(Required) The critter kind, sets the shape and the parts.</param>
    /// <example><code>BodyPlans.ForCritter(CritterKind.Amoeba).Count(CreaturePartKind.Spines)   // 1</code></example>
    public static CreatureDesign ForCritter(CritterKind kind)
    {
        CreatureDesign design;
        switch (kind)
        {
            case CritterKind.Bacterium:
                design = CreatureDesign.FromGirths(CreatureType.Fish, 0.4f, 0.7f, 0.8f, 0.6f);
                design.TryPlace(CreaturePartKind.CaudalFin, 0f, 0);
                design.TryPlace(CreaturePartKind.Eye, 0.82f, 0);
                design.TryPlace(CreaturePartKind.FilterMouth, 1f, 0);
                design.SetPattern(CreaturePattern.Stripes, null);
                break;
            case CritterKind.Amoeba:
                design = CreatureDesign.FromGirths(CreatureType.Fish, 0.6f, 0.95f, 1f, 0.95f, 0.7f);
                design.TryPlace(CreaturePartKind.CaudalFin, 0f, 0);
                design.TryPlace(CreaturePartKind.Spines, 0.5f, 0);
                design.TryPlace(CreaturePartKind.PectoralFin, 0.6f, 0);
                design.TryPlace(CreaturePartKind.Eye, 0.85f, 0);
                design.TryPlace(CreaturePartKind.Beak, 1f, 0);
                design.SetPattern(CreaturePattern.Spots, null);
                break;
            case CritterKind.Hunter:
                bool isShark = Rand.Chance(0.6f);
                design = isShark
                    ? CreatureDesign.FromGirths(CreatureType.Fish, 0.3f, 0.5f, 0.75f, 0.9f, 0.85f, 0.7f, 0.5f)
                    : CreatureDesign.FromGirths(CreatureType.MarineMammal, 0.35f, 0.6f, 0.85f, 0.95f, 0.95f, 0.8f, 0.6f);
                design.TryPlace(isShark ? CreaturePartKind.CaudalFin : CreaturePartKind.Fluke, 0f, 0);
                design.TryPlace(CreaturePartKind.DorsalFin, 0.55f, 0);
                design.TryPlace(CreaturePartKind.PectoralFin, 0.66f, 0);
                design.TryPlace(CreaturePartKind.Eye, 0.88f, 0);
                design.TryPlace(CreaturePartKind.Jaw, 1f, 0);
                if (!isShark)
                {
                    design.TryPlace(CreaturePartKind.Blowhole, 0.76f, 0);
                }
                design.SetPattern(isShark ? CreaturePattern.Saddle : CreaturePattern.Patches, isShark ? null : ORCA_PATCH_COLOR);
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be a known CritterKind, got '{kind}'.");
        }

        for (int i = 0; i < design.Girths.Count; i++)
        {
            design.SetGirth(i, design.Girths[i] + Rand.Range(-NPC_GIRTH_JITTER, NPC_GIRTH_JITTER));
        }
        return design;
    }
}
