// NOTE:
//   Verbose log for the decision points of the game (stage changes, deaths, mutations).
//   Silent by default, enabled with the environment variable CELLOUT_VERBOSE=1.
//
//   $env:CELLOUT_VERBOSE = '1'; godot --path .
//   Log.Verbose("Player", "Biomass 40 reached -> advancing to stage 1.");

using Godot;

namespace CellOut;

public static class Log
{
    private static readonly bool IS_VERBOSE = "1" == OS.GetEnvironment("CELLOUT_VERBOSE");

    /// <summary>Prints "[component] message" when verbose logging is enabled.</summary>
    /// <param name="component">(Required) The class or system that decides, e.g. "Player".</param>
    /// <param name="message">(Required) What was decided and why.</param>
    public static void Verbose(string component, string message)
    {
        if (!IS_VERBOSE)
        {
            return;
        }
        GD.Print($"[{component}] {message}");
    }
}
