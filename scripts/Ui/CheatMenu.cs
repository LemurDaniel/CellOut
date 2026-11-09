// NOTE:
//   Small cheat menu for testing: F10 opens and closes it in the top right corner below
//   the diet bar. Every button jumps forward to a stage (Main.CheatTo), brings the coast
//   right in front or adds evolution points. Texts come from translations.csv like
//   everything else.
//   Created by Main.
//
//   var cheats = new CheatMenu { Main = this };
//   AddChild(cheats);

//
//   Chapters:
//     TARGETS      What the menu can jump to.
//     CHEAT MENU   Toggle with F10, the buttons and their texts.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// ===============================   TARGETS   ===============================
// ===========================================================================
//
//   CheatTarget   Stages, the coast and points, in menu order.
//

public enum CheatTarget { SingleCell, Multicellular, FullCluster, Organism, Coast, LandAnimal, Points }

public partial class CheatMenu : CanvasLayer
{
    // ===========================================================================
    // =============================   CHEAT MENU   ==============================
    // ===========================================================================
    //
    //   Main              (Required) The game the buttons act on.
    //   _Ready            Hidden panel with one button per target.
    //   _UnhandledInput   F10 toggles the panel, also while paused.
    //   ApplyTexts        Title and button texts in the current language.
    //

    public Main Main { get; set; }

    private PanelContainer _panel;
    private Label _title;
    private readonly List<(CheatTarget Target, Button Button)> _buttons = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 30;

        _panel = new PanelContainer { Visible = false };
        _panel.AddThemeStyleboxOverride("panel", EditorStyle.Pill(new Color(0.05f, 0.08f, 0.12f, 0.92f), new Color(1f, 0.45f, 0.35f), 0.4f));
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        _panel.Position      += new Vector2(-20, 260);   // below the banner line
        AddChild(_panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(box);
        _title = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        _title.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.5f));
        box.AddChild(_title);

        foreach (CheatTarget target in System.Enum.GetValues<CheatTarget>())
        {
            var button = new Button
            {
                FocusMode         = Control.FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                CustomMinimumSize = new Vector2(220, 34),
            };
            button.AddThemeFontSizeOverride("font_size", 15);
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            button.Pressed += () => Main?.CheatTo(target);
            box.AddChild(button);
            _buttons.Add((target, button));
        }
        ApplyTexts();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F10 })
        {
            return;
        }
        _panel.Visible = !_panel.Visible;
        Log.Verbose("CheatMenu", $"Cheat menu {(_panel.Visible ? "opened" : "closed")}.");
        GetViewport().SetInputAsHandled();
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _title)
        {
            ApplyTexts();
        }
    }

    private void ApplyTexts()
    {
        _title.Text = Tr("CHEAT_TITLE");
        foreach ((CheatTarget target, Button button) in _buttons)
        {
            button.Text = Tr($"CHEAT_{target}".ToUpperInvariant());
        }
    }
}
