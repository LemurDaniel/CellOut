// NOTE:
//   The clock of a set of abilities, the same for cell parts (AbilitySet) and creature
//   parts (CreatureAbilitySet): how many parts of a kind there are, which kinds sit on the
//   key slots, cooldowns (faster with more parts) and running effects. Pure logic without
//   the engine. The numbers of a kind come from the tune lookup the subclass hands in
//   (data/balance.json, cell.abilities or creature.abilities).
//
//   public sealed class AbilitySet : AbilityClock<PartKind> { ... }
//   clock.Tick(dt);
//   if (clock.TryUse(kind)) { ... }

//
//   Chapters:
//     SLOTS       Counting parts and filling the key slots.
//     COOLDOWNS   Ticking, firing and running effects.
//

using System;
using System.Collections.Generic;
using Godot;

namespace CellOut;

public abstract class AbilityClock<TKind> where TKind : struct, Enum
{
    // ===========================================================================
    // ================================   SLOTS   ================================
    // ===========================================================================
    //
    //   SLOT_COUNT   Key slots at most.
    //   Add          Counts one placed part, a key ability takes the next free slot.
    //   Count        Parts of a kind, more of them recharge faster.
    //   Has          At least one part of the kind.
    //   Slots        The kinds on the key slots, in the order they were placed.
    //

    public const int SLOT_COUNT = 4;
    private const float EXTRA_PART_SPEEDUP = 0.5f;     // every further part of a kind recharges it this much faster

    private readonly Func<TKind, AbilityBalance> _tune;
    private readonly Dictionary<TKind, int> _counts = new();
    private readonly List<TKind> _slots = new();

    /// <param name="tune">(Required) The numbers of a kind, null when it has none.</param>
    protected AbilityClock(Func<TKind, AbilityBalance> tune)
    {
        _tune = tune;
    }

    /// <summary>Counts one placed part; a key ability takes the next free slot once.</summary>
    /// <param name="kind">(Required) The part kind.</param>
    /// <param name="isKey">(Required) It fires on a key (or a click on its slot).</param>
    protected void Add(TKind kind, bool isKey)
    {
        _counts[kind] = Count(kind) + 1;
        if (isKey && !_slots.Contains(kind) && _slots.Count < SLOT_COUNT)
        {
            _slots.Add(kind);
        }
    }

    /// <summary>Placed parts of a kind.</summary>
    public int Count(TKind kind)
    {
        return _counts.GetValueOrDefault(kind);
    }

    /// <summary>Whether at least one part of the kind sits on the body.</summary>
    public bool Has(TKind kind)
    {
        return Count(kind) > 0;
    }

    /// <summary>The kinds on the key slots, in the order they were placed.</summary>
    public IReadOnlyList<TKind> Slots => _slots;

    // ===========================================================================
    // =============================   COOLDOWNS   ===============================
    // ===========================================================================
    //
    //   TuneOf          The numbers of a kind, null when it has none.
    //   CooldownOf      Seconds a kind needs to recharge, shorter with more parts.
    //   Tick            Runs down cooldowns and effects.
    //   IsReady         The part exists and is charged.
    //   TryUse          Fires a charged ability: starts its cooldown and its effect.
    //   IsRunning       Its effect still runs (cloak, slime, ink).
    //   CooldownShare   1 right after firing .. 0 charged, for the slots of the HUD.
    //

    private readonly Dictionary<TKind, float> _cooldowns = new();
    private readonly Dictionary<TKind, float> _running = new();

    /// <summary>The numbers of a kind, null when it has none.</summary>
    public AbilityBalance TuneOf(TKind kind)
    {
        return _tune(kind);
    }

    /// <summary>Seconds a kind needs to recharge: its cooldown, every further part EXTRA_PART_SPEEDUP faster.</summary>
    /// <example><code>set.CooldownOf(PartKind.Flagellum)   // 3 with one flagellum, 2 with two</code></example>
    public float CooldownOf(TKind kind)
    {
        float cooldown = TuneOf(kind)?.Cooldown ?? 0f;
        return cooldown / (1f + EXTRA_PART_SPEEDUP * Mathf.Max(Count(kind) - 1, 0));
    }

    /// <summary>Runs down every cooldown and running effect.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public virtual void Tick(float dt)
    {
        foreach (TKind kind in new List<TKind>(_cooldowns.Keys))
        {
            _cooldowns[kind] = Mathf.Max(0f, _cooldowns[kind] - dt);
        }
        foreach (TKind kind in new List<TKind>(_running.Keys))
        {
            _running[kind] = Mathf.Max(0f, _running[kind] - dt);
        }
    }

    /// <summary>The part exists and its cooldown ran out.</summary>
    public bool IsReady(TKind kind)
    {
        return Has(kind) && _cooldowns.GetValueOrDefault(kind) <= 0f;
    }

    /// <summary>Fires a charged ability: its cooldown starts, an effect with a duration starts running.</summary>
    /// <param name="kind">(Required) The part kind.</param>
    /// <returns>True when it fired, false without the part or while it recharges.</returns>
    public bool TryUse(TKind kind)
    {
        if (!IsReady(kind))
        {
            return false;
        }
        _cooldowns[kind] = CooldownOf(kind);
        float duration = TuneOf(kind)?.Duration ?? 0f;
        if (duration > 0f)
        {
            _running[kind] = duration;
        }
        return true;
    }

    /// <summary>Whether the effect of a kind still runs.</summary>
    public bool IsRunning(TKind kind)
    {
        return _running.GetValueOrDefault(kind) > 0f;
    }

    /// <summary>1 right after firing .. 0 charged, for the slots of the HUD.</summary>
    public float CooldownShare(TKind kind)
    {
        float cooldown = CooldownOf(kind);
        return cooldown > 0f ? Mathf.Clamp(_cooldowns.GetValueOrDefault(kind) / cooldown, 0f, 1f) : 0f;
    }
}
