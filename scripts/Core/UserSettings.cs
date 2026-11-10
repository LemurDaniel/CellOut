// NOTE:
//   What the player chose and wants back at the next start: graphics tier and language.
//   Stored in user://settings.cfg. Headless runs (the smoke test) neither read nor write
//   the file, so tests stay reproducible and never touch the settings of the player.
//
//   UserSettings.Load();                              // once at start, applies both
//   UserSettings.SaveGraphics(GraphicsTier.High);
//   UserSettings.SaveLocale("de");

//
//   Chapters:
//     LOAD AND SAVE   Reads the file at start, writes single values.
//

using System;
using Godot;

namespace CellOut;

public static class UserSettings
{
    // ===========================================================================
    // ============================   LOAD AND SAVE   ============================
    // ===========================================================================
    //
    //   Load           Applies the saved language and graphics tier, detects the tier at the first start.
    //   SaveGraphics   Remembers the graphics tier.
    //   SaveLocale     Remembers the language.
    //

    private const string FILE_PATH = "user://settings.cfg";
    private const string SECTION = "settings";
    private const string GRAPHICS_KEY = "graphics";
    private const string LOCALE_KEY = "locale";
    private const string HEADLESS_DISPLAY = "headless";

    private static bool IsStored => HEADLESS_DISPLAY != DisplayServer.GetName();

    /*
      Order of precedence for the graphics tier:
      - CELLOUT_GRAPHICS (renders and tests)
      - The saved choice of the player
      - The tier detected for the graphics adapter
    */
    /// <summary>Applies the saved settings, call once before the world is built.</summary>
    public static void Load()
    {
        ConfigFile file = Read();

        string locale = (string)file.GetValue(SECTION, LOCALE_KEY, "");
        if (!string.IsNullOrEmpty(locale))
        {
            TranslationServer.SetLocale(locale);
        }

        string saved = (string)file.GetValue(SECTION, GRAPHICS_KEY, "");
        GraphicsTier tier = GraphicsQuality.Forced
            ?? (Enum.TryParse(saved, out GraphicsTier chosen) ? chosen : GraphicsQuality.Detect());
        GraphicsQuality.Use(tier);
    }

    /// <summary>Remembers the graphics tier for the next start.</summary>
    /// <param name="tier">(Required) The tier the player chose.</param>
    public static void SaveGraphics(GraphicsTier tier)
    {
        Write(GRAPHICS_KEY, tier.ToString());
    }

    /// <summary>Remembers the language for the next start.</summary>
    /// <param name="locale">(Required) Locale code, e.g. "de".</param>
    public static void SaveLocale(string locale)
    {
        Write(LOCALE_KEY, locale);
    }

    /// <summary>The settings file, empty when none exists yet or in a headless run.</summary>
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
            // Handled: a broken settings file only means the defaults apply, the next save rewrites it
            Log.Verbose("UserSettings", $"'{FILE_PATH}' is not readable ({error}) -> using defaults.");
            return new ConfigFile();
        }
        return file;
    }

    private static void Write(string key, string value)
    {
        if (!IsStored)
        {
            return;
        }
        ConfigFile file = Read();
        file.SetValue(SECTION, key, value);
        Error error = file.Save(FILE_PATH);
        if (Error.Ok != error)
        {
            GD.PushWarning($"[UserSettings] '{FILE_PATH}' must be writable to keep the setting '{key}', got {error}.");
        }
    }
}
