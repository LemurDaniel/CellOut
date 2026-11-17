// NOTE:
//   Keeps renders and test runs away from the player's files: unless CELLOUT_SAVE_DIR is set,
//   it points it at a folder in the temp directory, so saves (Core/Save/SaveSlots.cs) and the
//   reached phases (Core/PlanetProgress.cs) of a reel never land in user://. Call it first in
//   _Ready of every test scene, before Main loads.
//
//   TestFiles.KeepAwayFromPlayer();

//
//   Chapters:
//     FILES   The scratch folder of a test run.
//

using Godot;

namespace CellOut;

public static class TestFiles
{
    // ===========================================================================
    // ================================   FILES   ================================
    // ===========================================================================
    //
    //   KeepAwayFromPlayer   CELLOUT_SAVE_DIR to a temp folder unless the caller chose one.
    //

    private const string SAVE_DIR = "CELLOUT_SAVE_DIR";
    private const string FOLDER = "cellout-test-files";

    /// <summary>Points saves and progress of this run at a temp folder, unless CELLOUT_SAVE_DIR is set already.</summary>
    public static void KeepAwayFromPlayer()
    {
        if (!string.IsNullOrEmpty(OS.GetEnvironment(SAVE_DIR)))
        {
            return;
        }
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), FOLDER);
        System.IO.Directory.CreateDirectory(folder);
        OS.SetEnvironment(SAVE_DIR, folder.Replace(System.IO.Path.DirectorySeparatorChar, '/'));
        GD.Print($"[TestFiles] Saves and progress of this run go to {folder}.");
    }
}
