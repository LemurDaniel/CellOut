// NOTE:
//   The player's own templates across every game and planet (wish of the user 2026-11-17):
//   a blueprint saved in the village builder lands in user://blueprints.json and shows up
//   in the template window of every later game, next to the built-in templates
//   (BuildingTemplates.cs). Separate from settings, progress and save games. Headless runs
//   (the smoke test) keep the library in memory only; renders and tests with
//   CELLOUT_SAVE_DIR keep their file there, never in the player's.
//
//   BlueprintLibrary.Add("My tower", session.Draft);
//   foreach (BlueprintLibrary.Entry entry in BlueprintLibrary.Of(BuildingKind.Watchtower)) { ... }
//   BlueprintLibrary.Remove(entry);

//
//   Chapters:
//     LIBRARY   The saved blueprints, adding, removing, reading and writing the file.
//

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CellOut;

public static class BlueprintLibrary
{
    // ===========================================================================
    // ===============================   LIBRARY   ===============================
    // ===========================================================================
    //
    //   Entry    One saved blueprint: its name and the design.
    //   Of       The saved blueprints of a function, oldest first.
    //   Add      Saves a copy of a design under a name.
    //   Remove   Deletes a saved blueprint.
    //

    public const int MAX_NAME_LENGTH = 24;

    /// <summary>One saved blueprint.</summary>
    /// <param name="Name">What the player called it.</param>
    /// <param name="Design">The blueprint, a copy of its own.</param>
    public sealed record Entry(string Name, BuildingDesign Design);

    private sealed record StoredEntry(string Name, SaveBuildingDesign Design);

    private sealed record StoredLibrary(int Version, List<StoredEntry> Blueprints);

    private const int VERSION = 1;
    private const string HEADLESS_DISPLAY = "headless";
    private static readonly JsonSerializerOptions OPTIONS = new() { WriteIndented = true };

    private static string FilePath => string.IsNullOrEmpty(OS.GetEnvironment("CELLOUT_SAVE_DIR")) ? "user://blueprints.json" : OS.GetEnvironment("CELLOUT_SAVE_DIR") + "/blueprints.json";
    private static bool IsStored => HEADLESS_DISPLAY != DisplayServer.GetName();

    private static List<Entry> _entries;

    private static List<Entry> Entries => _entries ??= Read();

    /// <summary>The saved blueprints of a function, oldest first.</summary>
    /// <param name="kind">(Required) The function.</param>
    public static IReadOnlyList<Entry> Of(BuildingKind kind)
    {
        return Entries.Where(entry => kind == entry.Design.Kind).ToList();
    }

    /// <summary>Saves a copy of a design under a name, for this and every later game.</summary>
    /// <param name="name">(Required) Its name, trimmed and cut to MAX_NAME_LENGTH.</param>
    /// <param name="design">(Required) The blueprint, copied.</param>
    /// <returns>The new entry, null when the name is empty or the design holds no block.</returns>
    public static Entry Add(string name, BuildingDesign design)
    {
        string trimmed = (name ?? "").Trim();
        if (0 == trimmed.Length || 0 == design.Blocks.Count)
        {
            return null;
        }
        var copy = new BuildingDesign(design.Kind);
        copy.CopyFrom(design);
        var entry = new Entry(trimmed.Length > MAX_NAME_LENGTH ? trimmed[..MAX_NAME_LENGTH] : trimmed, copy);
        Entries.Add(entry);
        Write();
        Log.Verbose("BlueprintLibrary", $"Saved '{entry.Name}' ({design.Kind}, {design.Blocks.Count} block(s)), {Entries.Count} in the library.");
        return entry;
    }

    /// <summary>Deletes a saved blueprint.</summary>
    /// <param name="entry">(Required) An entry of Of.</param>
    public static void Remove(Entry entry)
    {
        if (Entries.Remove(entry))
        {
            Write();
            Log.Verbose("BlueprintLibrary", $"Removed '{entry.Name}', {Entries.Count} left.");
        }
    }

    /// <summary>The library file, empty when none exists yet, it is broken or in a headless run.</summary>
    private static List<Entry> Read()
    {
        var entries = new List<Entry>();
        if (!IsStored || !FileAccess.FileExists(FilePath))
        {
            return entries;
        }
        try
        {
            StoredLibrary stored = JsonSerializer.Deserialize<StoredLibrary>(FileAccess.GetFileAsString(FilePath), OPTIONS);
            foreach (StoredEntry entry in stored?.Blueprints ?? new List<StoredEntry>())
            {
                // Blocks the game no longer has drop out (SaveData.ToBuilding), an entry without any block left is skipped
                BuildingDesign design = SaveData.ToBuilding(entry.Design);
                if (design.Blocks.Count > 0)
                {
                    entries.Add(new Entry(entry.Name, design));
                }
            }
        }
        catch (System.Exception exception) when (exception is JsonException or System.ArgumentException)
        {
            // Handled: a broken library only means no own templates, the next save rewrites the file
            Log.Verbose("BlueprintLibrary", $"'{FilePath}' is not readable ({exception.Message}) -> starting empty.");
            entries.Clear();
        }
        return entries;
    }

    private static void Write()
    {
        if (!IsStored)
        {
            return;
        }
        var stored = new StoredLibrary(VERSION, Entries.Select(entry => new StoredEntry(entry.Name, SaveData.Building(entry.Design))).ToList());
        using FileAccess file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        if (null == file)
        {
            GD.PushWarning($"[BlueprintLibrary] '{FilePath}' must be writable to keep the templates, got {FileAccess.GetOpenError()}.");
            return;
        }
        file.StoreString(JsonSerializer.Serialize(stored, OPTIONS));
    }
}
