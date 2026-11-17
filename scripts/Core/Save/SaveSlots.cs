// NOTE:
//   The save files on disk: one slot per planet, user://saves/<PLANET>.json. Writing goes to
//   a temp file first and is renamed afterwards, so a crash never leaves half a save. A
//   broken file reads as none (and stays on disk for a look). Headless runs (the smoke test)
//   neither read nor write, like the settings (Core/UserSettings.cs); CELLOUT_SAVE_DIR moves the
//   folder for renders, so they never touch the player's saves.
//
//   SaveSlots.Write(game);                         // the slot of game.Meta.Planet
//   SaveGame game = SaveSlots.Read("TERRA");       // null without a save
//   bool hasSave  = SaveSlots.Exists("TERRA");

//
//   Chapters:
//     SLOTS   Path, exists, read, write and delete.
//

using Godot;

namespace CellOut;

public static class SaveSlots
{
    // ===========================================================================
    // ================================   SLOTS   ================================
    // ===========================================================================
    //
    //   PathOf   The file of a planet.
    //   Exists   Whether a planet has a save.
    //   Read     The save of a planet, null without one or when it is broken.
    //   Write    Writes a game into the slot of its planet, atomically.
    //   Delete   Removes the save of a planet.
    //

    private const string DEFAULT_FOLDER = "user://saves";

    // Renders and tests save elsewhere (CELLOUT_SAVE_DIR), never over the player's saves
    private static string FOLDER => string.IsNullOrEmpty(OS.GetEnvironment("CELLOUT_SAVE_DIR")) ? DEFAULT_FOLDER : OS.GetEnvironment("CELLOUT_SAVE_DIR");
    private const string HEADLESS_DISPLAY = "headless";

    private static bool IsStored => HEADLESS_DISPLAY != DisplayServer.GetName();

    /// <summary>The save file of a planet.</summary>
    public static string PathOf(string planet)
    {
        return $"{FOLDER}/{planet}.json";
    }

    /// <summary>Whether a planet has a save.</summary>
    public static bool Exists(string planet)
    {
        return IsStored && FileAccess.FileExists(PathOf(planet));
    }

    /// <summary>The save of a planet.</summary>
    /// <returns>Null without a save, in a headless run or when the file cannot be read.</returns>
    public static SaveGame Read(string planet)
    {
        if (!Exists(planet))
        {
            return null;
        }
        string json = FileAccess.GetFileAsString(PathOf(planet));
        try
        {
            return SaveCodec.FromJson(json);
        }
        catch (System.InvalidOperationException error)
        {
            // Handled: a broken or newer save is shown as none, the file stays for a look
            GD.PushWarning($"[SaveSlots] '{PathOf(planet)}' cannot be loaded: {error.Message}");
            return null;
        }
    }

    /// <summary>Writes a game into the slot of its planet: a temp file first, then renamed over the old save.</summary>
    /// <param name="game">(Required) The game, its Meta.Planet names the slot.</param>
    /// <returns>True when it was written, false in a headless run or on a disk error.</returns>
    public static bool Write(SaveGame game)
    {
        if (!IsStored)
        {
            return false;
        }
        DirAccess.MakeDirRecursiveAbsolute(FOLDER);
        string path = PathOf(game.Meta.Planet);
        string temp = path + ".tmp";
        using (FileAccess file = FileAccess.Open(temp, FileAccess.ModeFlags.Write))
        {
            if (null == file)
            {
                GD.PushWarning($"[SaveSlots] '{temp}' must be writable to save, got {FileAccess.GetOpenError()}.");
                return false;
            }
            file.StoreString(SaveCodec.ToJson(game));
        }
        Error error = DirAccess.RenameAbsolute(temp, path);
        if (Error.Ok != error)
        {
            GD.PushWarning($"[SaveSlots] '{temp}' must be renamed to '{path}', got {error}.");
            return false;
        }
        Log.Verbose("SaveSlots", $"Saved {game.Meta.Planet} at stage {game.Meta.Stage}.");
        return true;
    }

    /// <summary>Removes the save of a planet, if any.</summary>
    public static void Delete(string planet)
    {
        if (Exists(planet))
        {
            DirAccess.RemoveAbsolute(PathOf(planet));
        }
    }
}
