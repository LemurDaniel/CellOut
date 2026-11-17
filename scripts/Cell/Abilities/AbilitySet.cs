// NOTE:
//   The abilities a cell gets from its parts (decision of the user 2026-11-17: parts are
//   abilities, not small stat bonuses): which part fires how (by itself, on a key, all
//   the time), the four key slots, cooldowns, running effects and the charges of the
//   armor plates. Pure logic without the engine, the same for the player and the NPC
//   cells; Game/Abilities/AbilityResolver.cs decides when to fire and what happens.
//   Numbers come from data/balance.json (cell.abilities).
//
//   var abilities = AbilitySet.From(design);
//   abilities.Tick(dt);
//   if (abilities.TryUse(PartKind.Flagellum)) { ... dash ... }
//   PartKind key1 = abilities.Slots[0];

//
//   Chapters:
//     TYPES       How a part fires.
//     BUILD       Counts, key slots and shield charges of a design.
//     COOLDOWNS   Ticking, firing, running effects and the shield.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   AbilityTrigger   None (only looks), Passive (all the time), Auto (fires by itself), Active (on a key).
//

public enum AbilityTrigger { None, Passive, Auto, Active }

public sealed class AbilitySet
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   TriggerOf   How a part kind fires.
    //   From        Counts every part, fills the key slots in placement order, charges the plates.
    //   Count       Parts of a kind, more of them recharge faster.
    //   Slots       Active kinds on the keys 1 to 4, in the order they were placed.
    //

    public const int SLOT_COUNT = 4;
    private const float EXTRA_PART_SPEEDUP = 0.5f;     // every further part of a kind recharges it this much faster

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

    private readonly Dictionary<PartKind, int> _counts = new();
    private readonly List<PartKind> _slots = new();

    /// <summary>The abilities of a design.</summary>
    /// <param name="design">(Required) The placed parts.</param>
    /// <example><code>AbilitySet.From(design).Slots   // [Flagellum, Chromatophore]</code></example>
    public static AbilitySet From(CellDesign design)
    {
        var set = new AbilitySet();
        foreach (PlacedPart part in design.Parts)
        {
            set._counts[part.Kind] = set.Count(part.Kind) + 1;
            if (AbilityTrigger.Active == TriggerOf(part.Kind) && !set._slots.Contains(part.Kind) && set._slots.Count < SLOT_COUNT)
            {
                set._slots.Add(part.Kind);
            }
        }
        set.ShieldCharges = set.Count(PartKind.ArmorPlate);
        return set;
    }

    /// <summary>Placed parts of a kind.</summary>
    public int Count(PartKind kind)
    {
        return _counts.GetValueOrDefault(kind);
    }

    /// <summary>Whether at least one part of the kind sits on the cell.</summary>
    public bool Has(PartKind kind)
    {
        return Count(kind) > 0;
    }

    /// <summary>Active kinds on the keys 1 to 4, in the order they were placed.</summary>
    public IReadOnlyList<PartKind> Slots => _slots;

    // ===========================================================================
    // =============================   COOLDOWNS   ===============================
    // ===========================================================================
    //
    //   Tune            The numbers of a kind, null when it has none.
    //   CooldownOf      Seconds a kind needs to recharge, shorter with more parts.
    //   Tick            Runs down cooldowns and effects, recharges one plate at a time.
    //   IsReady         The part exists and is charged.
    //   TryUse          Fires a charged ability: starts its cooldown and its effect.
    //   IsRunning       Its effect still runs (cloak, slime laying).
    //   CooldownShare   1 right after firing .. 0 charged, for the key slots of the HUD.
    //   ShieldCharges   Armor plates that can still block a hit.
    //   TryBlock        Spends a charge on a hit, the plate grows back after its cooldown.
    //

    private readonly Dictionary<PartKind, float> _cooldowns = new();
    private readonly Dictionary<PartKind, float> _running = new();
    private float _shieldRegrow;

    /// <summary>The numbers of a kind from data/balance.json (cell.abilities), null when it has none.</summary>
    public static AbilityBalance Tune(PartKind kind)
    {
        return Balance.Current.Cell.Abilities.GetValueOrDefault(kind);
    }

    /// <summary>Seconds a kind needs to recharge: its cooldown, every further part EXTRA_PART_SPEEDUP faster.</summary>
    /// <example><code>set.CooldownOf(PartKind.Flagellum)   // 3 with one flagellum, 2 with two</code></example>
    public float CooldownOf(PartKind kind)
    {
        float cooldown = Tune(kind)?.Cooldown ?? 0f;
        return cooldown / (1f + EXTRA_PART_SPEEDUP * Mathf.Max(Count(kind) - 1, 0));
    }

    /// <summary>Runs down every cooldown and running effect, a broken plate grows back one at a time.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Tick(float dt)
    {
        foreach (PartKind kind in new List<PartKind>(_cooldowns.Keys))
        {
            _cooldowns[kind] = Mathf.Max(0f, _cooldowns[kind] - dt);
        }
        foreach (PartKind kind in new List<PartKind>(_running.Keys))
        {
            _running[kind] = Mathf.Max(0f, _running[kind] - dt);
        }
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

    /// <summary>The part exists and its cooldown ran out.</summary>
    public bool IsReady(PartKind kind)
    {
        return Has(kind) && _cooldowns.GetValueOrDefault(kind) <= 0f;
    }

    /// <summary>Fires a charged ability: its cooldown starts, an effect with a duration starts running.</summary>
    /// <param name="kind">(Required) The part kind.</param>
    /// <returns>True when it fired, false without the part or while it recharges.</returns>
    /// <example><code>if (set.TryUse(PartKind.Chromatophore)) body.Fade = 0.8f;</code></example>
    public bool TryUse(PartKind kind)
    {
        if (!IsReady(kind))
        {
            return false;
        }
        _cooldowns[kind] = CooldownOf(kind);
        float duration = Tune(kind)?.Duration ?? 0f;
        if (duration > 0f)
        {
            _running[kind] = duration;
        }
        return true;
    }

    /// <summary>Whether the effect of a kind still runs (cloak, slime laying).</summary>
    public bool IsRunning(PartKind kind)
    {
        return _running.GetValueOrDefault(kind) > 0f;
    }

    /// <summary>1 right after firing .. 0 charged, for the key slots of the HUD.</summary>
    public float CooldownShare(PartKind kind)
    {
        float cooldown = CooldownOf(kind);
        return cooldown > 0f ? Mathf.Clamp(_cooldowns.GetValueOrDefault(kind) / cooldown, 0f, 1f) : 0f;
    }

    /// <summary>Armor plates that can still block a hit, all of them on a fresh cell.</summary>
    public int ShieldCharges { get; private set; }

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
