// NOTE:
//   The phases a game can start in from the galaxy menu and which of them are open: the
//   RNA world always, every later one once it was reached on that planet in an earlier
//   game (Core/PlanetProgress.cs remembers it). A phase is one step further than the
//   player stage it starts with (Player.Stage 0 protocell = phase 1). Pure logic, no engine.
//
//   StartPhase reached = StartPhases.OfStage(player.Stage);
//   bool isOpen        = StartPhases.IsOpen(StartPhase.Pack, reached);
//   CheatTarget? jump  = StartPhases.JumpOf(StartPhase.LandAnimal);   // LandAnimal

//
//   Chapters:
//     PHASES   The phases, the stage they start in and whether they are open.
//

namespace CellOut;

public enum StartPhase { RnaWorld, Protocell, SingleCell, Multicellular, Organism, LandAnimal, Pack, Tribe }

public static class StartPhases
{
    // ===========================================================================
    // ===============================   PHASES   ================================
    // ===========================================================================
    //
    //   COUNT     Number of phases.
    //   OfStage   The phase a player stage belongs to.
    //   IsOpen    Whether a phase may be chosen, by the furthest phase reached on the planet.
    //   JumpOf    The cheat jump that puts a fresh game into a phase, null for the first two
    //             (the RNA world and the protocell have their own start).
    //   NameKey   PHASE_<NAME> in locale/translations.csv.
    //

    public const int COUNT = 8;

    /// <summary>The phase of a player stage: the protocell (stage 0) is phase 1, the tribe (stage 6) phase 7.</summary>
    /// <example><code>StartPhases.OfStage(Player.LAND_STAGE)  // StartPhase.LandAnimal</code></example>
    public static StartPhase OfStage(int stage)
    {
        return (StartPhase)System.Math.Clamp(stage + 1, 0, COUNT - 1);
    }

    /// <summary>Whether a phase may be chosen: the RNA world always, later ones once reached.</summary>
    /// <param name="phase">(Required) The phase to start in.</param>
    /// <param name="reached">(Required) The furthest phase reached on the planet.</param>
    public static bool IsOpen(StartPhase phase, StartPhase reached)
    {
        return StartPhase.RnaWorld == phase || phase <= reached;
    }

    /// <summary>The cheat jump that brings a freshly born protocell into a phase.</summary>
    /// <returns>Null for the RNA world and the protocell.</returns>
    public static CheatTarget? JumpOf(StartPhase phase)
    {
        return phase switch
        {
            StartPhase.SingleCell    => CheatTarget.SingleCell,
            StartPhase.Multicellular => CheatTarget.Multicellular,
            StartPhase.Organism      => CheatTarget.Organism,
            StartPhase.LandAnimal    => CheatTarget.LandAnimal,
            StartPhase.Pack          => CheatTarget.Pack,
            StartPhase.Tribe         => CheatTarget.Tribe,
            _                        => null,
        };
    }

    /// <summary>The translation key of a phase name.</summary>
    public static string NameKey(StartPhase phase)
    {
        return $"PHASE_{phase}".ToUpperInvariant();
    }
}
