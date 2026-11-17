// NOTE:
//   The abilities a cell gets from its parts (decision of the user 2026-11-17: parts are
//   abilities, not small stat bonuses): which part fires how (by itself, on a key, all
//   the time), the four key slots, cooldowns and running effects (AbilityClock) and the
//   charges of the armor plates. Pure logic without the engine, the same for the player
//   and the NPC cells; Game/Abilities/AbilityResolver.cs decides when to fire and what
//   happens. Numbers come from data/balance.json (cell.abilities).
//
//   var abilities = AbilitySet.From(design);
//   abilities.Tick(dt);
//   if (abilities.TryUse(PartKind.Flagellum)) { ... dash ... }
//   PartKind key1 = abilities.Slots[0];

//
//   Chapters:
//     TYPES    How a part fires.
//     BUILD    Triggers, counts and key slots of a design.
//     SHIELD   The charges of the armor plates.
//

using System.Collections.Generic;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   AbilityTrigger   None (only looks), Passive (all the time), Auto (fires by itself), Active (on a key).
//

public enum AbilityTrigger { None, Passive, Auto, Active }

public sealed class AbilitySet : AbilityClock<PartKind>
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   TriggerOf   How a part kind fires.
    //   Tune        The numbers of a kind from data/balance.json (cell.abilities), null when it has none.
    //   From        Counts every part, fills the key slots in placement order, charges the plates.
    //

    /*
      How every part fires:
      - Active (keys 1-4):  flagellum (dash), cilia (dodge), jet nozzle (blast),
                            slime gland (slime trail), chromatophores (cloak)
      - Auto (by itself):   electrocyte (bolt), toxin gland (cloud when hit), jaw (snap),
                            pseudopod (grab), armor plate (shield)
      - Passive:            light sail, filter mouth (vortex), sucker, spike, nematocyst
      - None:               eyespot, it only looks
    */
    /// <summary>How a part kind fires.</summary>
    /// <example><code>AbilitySet.TriggerOf(PartKind.Flagellum)   // Active</code></example>
    public static AbilityTrigger TriggerOf(PartKind kind)
    {
        return kind switch
        {
            PartKind.Flagellum or PartKind.Cilia or PartKind.JetNozzle
                or PartKind.SlimeGland or PartKind.Chromatophore              => AbilityTrigger.Active,
            PartKind.Electrocyte or PartKind.ToxinGland or PartKind.Jaw
                or PartKind.Pseudopod or PartKind.ArmorPlate                  => AbilityTrigger.Auto,
            PartKind.LightSail or PartKind.FilterMouth or PartKind.Sucker
                or PartKind.Spike or PartKind.Nematocyst                      => AbilityTrigger.Passive,
            _                                                                 => AbilityTrigger.None,
        };
    }

    /// <summary>The numbers of a kind from data/balance.json (cell.abilities), null when it has none.</summary>
    public static AbilityBalance Tune(PartKind kind)
    {
        return Balance.Current.Cell.Abilities.GetValueOrDefault(kind);
    }

    public AbilitySet() : base(Tune)
    {
    }

    /// <summary>The abilities of a design.</summary>
    /// <param name="design">(Required) The placed parts.</param>
    /// <example><code>AbilitySet.From(design).Slots   // [Flagellum, Chromatophore]</code></example>
    public static AbilitySet From(CellDesign design)
    {
        var set = new AbilitySet();
        foreach (PlacedPart part in design.Parts)
        {
            set.Add(part.Kind, AbilityTrigger.Active == TriggerOf(part.Kind));
        }
        set.ShieldCharges = set.Count(PartKind.ArmorPlate);
        return set;
    }

    // ===========================================================================
    // ================================   SHIELD   ===============================
    // ===========================================================================
    //
    //   ShieldCharges   Armor plates that can still block a hit.
    //   Tick            Cooldowns and effects (AbilityClock), one broken plate grows back at a time.
    //   TryBlock        Spends a charge on a hit, the plate grows back after its cooldown.
    //

    private float _shieldRegrow;

    /// <summary>Armor plates that can still block a hit, all of them on a fresh cell.</summary>
    public int ShieldCharges { get; private set; }

    /// <summary>Runs down cooldowns and effects, a broken plate grows back one at a time.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (ShieldCharges >= Count(PartKind.ArmorPlate))
        {
            return;
        }
        _shieldRegrow -= dt;
        if (_shieldRegrow <= 0f)
        {
            ShieldCharges++;
            _shieldRegrow = CooldownOf(PartKind.ArmorPlate);
        }
    }

    /// <summary>A plate catches the hit: one charge less, it grows back after the cooldown of the plate.</summary>
    /// <returns>True when a charge blocked the hit, false when every plate is broken.</returns>
    public bool TryBlock()
    {
        if (ShieldCharges <= 0)
        {
            return false;
        }
        if (ShieldCharges == Count(PartKind.ArmorPlate))
        {
            _shieldRegrow = CooldownOf(PartKind.ArmorPlate);
        }
        ShieldCharges--;
        return true;
    }
}
