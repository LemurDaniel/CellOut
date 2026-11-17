// NOTE:
//   What the player reached across games, kept in user://progress.cfg: the furthest phase
//   per planet (the galaxy menu opens the start phases by it, Core/StartPhases.cs) and the
//   games played. Headless runs (the smoke test) neither read nor write the file, so tests
//   stay reproducible; F10 in the title menu (IsCheatOpen) or CELLOUT_ALL_PHASES=1 opens every phase.
//
//   StartPhase reached = PlanetProgress.Reached("TERRA");
//   PlanetProgress.Reach("TERRA", StartPhase.Pack);        // only ever moves forward
//   PlanetProgress.CountGame();                            // once per started game

//
//   Chapters:
//     PROGRESS   Reads and writes the furthest phase per planet and the games played.
//

using System;
using Godot;

namespace CellOut;

public static class PlanetProgress
{
    // ===========================================================================
    // ==============================   PROGRESS   ===============================
    // ===========================================================================
    //
    //   Reached     The furthest phase reached on a planet, the RNA world without a record.
    //   FurthestAnywhere   The furthest phase reached on any planet.
    //   Reach       Remembers a phase when it is further than the record.
    //   Games       Games started so far, on every planet together.
    //   CountGame   One more game started.
    //

    // Renders and tests keep their progress next to their saves (CELLOUT_SAVE_DIR), never in the player's file
    private static string FILE_PATH => string.IsNullOrEmpty(OS.GetEnvironment("CELLOUT_SAVE_DIR")) ? "user://progress.cfg" : OS.GetEnvironment("CELLOUT_SAVE_DIR") + "/progress.cfg";
    private const string PLANETS = "planets";
    private const string TOTALS = "totals";
    private const string GAMES_KEY = "games";
    private const string HEADLESS_DISPLAY = "headless";

    private static bool IsStored => HEADLESS_DISPLAY != DisplayServer.GetName();
    private static bool IsAllOpen => IsCheatOpen || "1" == OS.GetEnvironment("CELLOUT_ALL_PHASES");

    /// <summary>F10 in the title menu: every phase may be chosen for this session, nothing is saved.</summary>
    public static bool IsCheatOpen { get; set; }

    private static ConfigFile _file;

    private static ConfigFile File => _file ??= Read();

    /// <summary>The furthest phase reached on a planet.</summary>
    /// <param name="planet">(Required) Key of the planet.</param>
    public static StartPhase Reached(string planet)
    {
        if (IsAllOpen)
        {
            return StartPhase.Tribe;
        }
        string saved = (string)File.GetValue(PLANETS, planet, "");
        return Enum.TryParse(saved, out StartPhase phase) ? phase : StartPhase.RnaWorld;
    }

    /// <summary>The furthest phase reached on any planet, opens parts across games (Creature/Design/PartUnlocks.cs).</summary>
    public static StartPhase FurthestAnywhere
    {
        get
        {
            StartPhase furthest = StartPhase.RnaWorld;
            foreach (Planet planet in Planets.All)
            {
                StartPhase reached = Reached(planet.Key);
                furthest = reached > furthest ? reached : furthest;
            }
            return furthest;
        }
    }

    /// <summary>Remembers a phase on a planet, only when it is further than what was reached before.</summary>
    /// <param name="planet">(Required) Key of the planet.</param>
    /// <param name="phase">(Required) The phase the game just reached.</param>
    public static void Reach(string planet, StartPhase phase)
    {
        if (phase <= Reached(planet))
        {
            return;
        }
        File.SetValue(PLANETS, planet, phase.ToString());
        Save();
        Log.Verbose("PlanetProgress", $"{planet}: phase {phase} reached.");
    }

    /// <summary>Games started so far, on every planet together.</summary>
    public static int Games => (int)File.GetValue(TOTALS, GAMES_KEY, 0);

    /// <summary>Counts one more started game.</summary>
    public static void CountGame()
    {
        File.SetValue(TOTALS, GAMES_KEY, Games + 1);
        Save();
    }

    /// <summary>The progress file, empty when none exists yet or in a headless run.</summary>
    private static ConfigFile Read()
    {
        var file = new ConfigFile();
        if (!IsStored || !FileAccess.FileExists(FILE_PATH))
        {
            return file;
        }
        Error error = file.Load(FILE_PATH);
        if (Error.Ok != error)
        {
            // Handled: a broken file only means nothing was reached yet, the next save rewrites it
            Log.Verbose("PlanetProgress", $"'{FILE_PATH}' is not readable ({error}) -> starting fresh.");
            return new ConfigFile();
        }
        return file;
    }

    private static void Save()
    {
        if (!IsStored)
        {
            return;
        }
        Error error = File.Save(FILE_PATH);
        if (Error.Ok != error)
        {
            GD.PushWarning($"[PlanetProgress] '{FILE_PATH}' must be writable to keep the progress, got {error}.");
        }
    }
}
