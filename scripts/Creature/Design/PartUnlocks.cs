// NOTE:
//   Which creature parts may be built yet (wish of the user 2026-11-17: some parts open only
//   at a point of the game or after more games). Two kinds of locks, both in data/balance.json
//   (creature.parts):
//     "unlockStage": 4        in this game: the player stage it opens at (Player.LAND_STAGE = 4)
//     "unlockReached": "Pack" across games: the phase that must have been reached once on any
//                             planet before (Core/PlanetProgress.cs)
//   A part without either is open from the start. Pure logic without the engine: the caller
//   hands in the stage and the furthest phase. The creature editor greys locked parts out
//   and refuses them (Creature/Editor/CreatureEditorSession.cs, Views/CreaturePartPalette.cs).
//
//   bool isOpen = PartUnlocks.IsOpen(CreaturePartKind.RamBrow, player.Stage, PlanetProgress.FurthestAnywhere);
//   string hint = PartUnlocks.HintKey(CreaturePartKind.RamBrow);   // CREATURE_LOCK_REACHED

//
//   Chapters:
//     LOCKS   Whether a part is open, and the key of the hint that says why not.
//

using System;

namespace CellOut;

public static class PartUnlocks
{
    // ===========================================================================
    // ================================   LOCKS   ================================
    // ===========================================================================
    //
    //   IsOpen    Open when the stage and the furthest phase reached anywhere are far enough.
    //   HintKey   CREATURE_LOCK_STAGE or CREATURE_LOCK_REACHED, null for an open part.
    //   StageOf   The stage a part needs in this game, 0 without a lock.
    //   PhaseOf   The phase a part needs across games, null without a lock.
    //

    /// <summary>Whether a part may be built now.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="stage">(Required) Player.Stage of this game.</param>
    /// <param name="reached">(Required) The furthest phase reached on any planet so far.</param>
    public static bool IsOpen(CreaturePartKind kind, int stage, StartPhase reached)
    {
        StartPhase? needed = PhaseOf(kind);
        return stage >= StageOf(kind) && (null == needed || reached >= needed.Value);
    }

    /// <summary>The translation key of the hint on a locked part: which lock holds it.</summary>
    /// <returns>Null for a part without a lock.</returns>
    public static string HintKey(CreaturePartKind kind)
    {
        if (null != PhaseOf(kind))
        {
            return "CREATURE_LOCK_REACHED";
        }
        return StageOf(kind) > 0 ? "CREATURE_LOCK_STAGE" : null;
    }

    /// <summary>The player stage a part needs in this game, 0 without a lock.</summary>
    public static int StageOf(CreaturePartKind kind)
    {
        return Balance.Current.Creature.Parts[kind].UnlockStage;
    }

    /// <summary>The phase a part needs across games, null without a lock.</summary>
    public static StartPhase? PhaseOf(CreaturePartKind kind)
    {
        string phase = Balance.Current.Creature.Parts[kind].UnlockReached;
        if (string.IsNullOrEmpty(phase))
        {
            return null;
        }
        return Enum.TryParse(phase, out StartPhase parsed)
            ? parsed
            : throw new InvalidOperationException($"creature.parts.{kind}.unlockReached must be a StartPhase, got '{phase}'.");
    }
}
