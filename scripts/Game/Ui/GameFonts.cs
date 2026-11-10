// NOTE:
//   The two generated game fonts (tools/font). The text font is the project default
//   (project.godot -> gui/theme/custom_font), the display font is set explicitly on
//   titles and banners: hollow membrane tubes, nuclei and bouncing letters.
//
//   GameFonts.UseDisplay(titleLabel);

using Godot;

namespace CellOut;

public static class GameFonts
{
    private const string DISPLAY_FONT_PATH = "res://fonts/CellOutDisplay.otf";

    private static Font _display;

    /// <summary>The display font, loaded on first use.</summary>
    public static Font Display => _display ??= GD.Load<Font>(DISPLAY_FONT_PATH);

    /// <summary>Switches a control (label, button) to the display font.</summary>
    /// <param name="control">(Required) The control, its font size stays as set.</param>
    public static void UseDisplay(Control control)
    {
        control.AddThemeFontOverride("font", Display);
    }
}
