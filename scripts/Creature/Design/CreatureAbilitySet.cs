// NOTE:
//   The abilities a creature gets from its ability parts (wish of the user 2026-11-17, tabs
//   Gentle, Clever and Wild of the creature editor): every ability part fires on a key slot
//   (or a click on it), the rest of the body brings none. Cooldowns and running effects
//   come from AbilityClock, the numbers from data/balance.json (creature.abilities). Pure
//   logic without the engine; Game/Abilities/CreatureAbilityResolver.cs does the rest.
//
//   var abilities = CreatureAbilitySet.From(player.Creature);
//   abilities.Tick(dt);
//   if (abilities.TryUse(CreaturePartKind.RoarSac)) { ... roar ... }

//
//   Chapters:
//     BUILD   Which parts are abilities, the key slots of a body plan, the diet of a part.
//

using System.Collections.Generic;

namespace CellOut;

public sealed class CreatureAbilitySet : AbilityClock<CreaturePartKind>
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   Tune        The numbers of a kind from data/balance.json (creature.abilities), null when it has none.
    //   IsAbility   Whether a part kind gives an ability.
    //   From        Counts the ability parts, fills the key slots in placement order.
    //   DietOf      The diet of an ability part by its tab: the color of its slot.
    //

    /// <summary>The numbers of a kind from data/balance.json (creature.abilities), null when it has none.</summary>
    public static AbilityBalance Tune(CreaturePartKind kind)
    {
        return Balance.Current.Creature.Abilities.GetValueOrDefault(kind);
    }

    public CreatureAbilitySet() : base(Tune)
    {
    }

    /// <summary>Whether a part kind gives an ability (it has numbers in creature.abilities).</summary>
    /// <example><code>CreatureAbilitySet.IsAbility(CreaturePartKind.RoarSac)   // true</code></example>
    public static bool IsAbility(CreaturePartKind kind)
    {
        return null != Tune(kind);
    }

    /// <summary>The abilities of a body plan, null gives none.</summary>
    /// <param name="design">(Optional) The body plan, null for a cell.</param>
    /// <example><code>CreatureAbilitySet.From(creature).Slots   // [ScentGland, RamBrow]</code></example>
    public static CreatureAbilitySet From(CreatureDesign design)
    {
        var set = new CreatureAbilitySet();
        if (null == design)
        {
            return set;
        }
        foreach (CreaturePart part in design.Parts)
        {
            if (IsAbility(part.Kind))
            {
                set.Add(part.Kind, isKey: true);
            }
        }
        return set;
    }

    /// <summary>The diet of an ability part by its tab: gentle herbivore, clever omnivore, wild carnivore.</summary>
    public static DietClass DietOf(CreaturePartKind kind)
    {
        return CreatureCatalog.Get(kind).Category switch
        {
            CreatureCategory.Gentle => DietClass.Herbivore,
            CreatureCategory.Wild   => DietClass.Carnivore,
            _                       => DietClass.Omnivore,
        };
    }
}
