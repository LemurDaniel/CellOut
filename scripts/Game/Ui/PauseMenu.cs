// NOTE:
//   The pause menu (wish of the user 2026-11-17): Escape in the running game pauses and
//   shows a small menu in the middle: continue, save, the template library and back to the
//   main menu. The library is the template window of the village builder
//   (Tribe/Building/Editor/Views/TemplatePicker.cs) with a tab per building function: the
//   player's own templates of every game, a click on a card makes it the blueprint of the
//   tribe. Escape again continues. It stays out of the way while a menu, cutscene, editor,
//   the tech tree or the build view has the screen: those use Escape themselves.
//   Created by Main, as its first child: it sees Escape after everyone else.
//
//   var pause = new PauseMenu { Main = this };
//   AddChild(pause);
//   MoveChild(pause, 0);

//
//   Chapters:
//     PAUSE MENU   Toggle with Escape, the buttons and the library.
//

using Godot;

namespace CellOut;

public partial class PauseMenu : CanvasLayer
{
    // ===========================================================================
    // =============================   PAUSE MENU   ==============================
    // ===========================================================================
    //
    //   Main              (Required) The game the menu pauses and acts on.
    //   IsOpen            Whether the menu shows.
    //   Open / Close      Pauses and shows the menu, hides it and continues.
    //   OpenLibrary       The template library over the menu.
    //   _UnhandledInput   Escape opens the menu in the free game and closes it again.
    //

    public Main Main { get; set; }

    private const int BUTTON_WIDTH = 300;
    private static readonly Color ACCENT = new(0.6f, 0.7f, 0.9f);
    private static readonly Color DIM_COLOR = new(0f, 0f, 0f, 0.45f);

    private Control _root;
    private Label _title;
    private Button _resume;
    private Button _save;
    private Button _library;
    private Button _menu;
    private TemplatePicker _templates;

    public bool IsOpen => _root.Visible;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 25;

        _root = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var dim = new ColorRect { Color = DIM_COLOR, MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", EditorStyle.Pill(new Color(0.05f, 0.08f, 0.12f, 0.94f), ACCENT, 0.2f));
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical   = Control.GrowDirection.Both;
        _root.AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 22);
        }
        panel.AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        _title = CreatureEditorUi.NewLabel(34);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        GameFonts.UseDisplay(_title);
        box.AddChild(_title);
        _resume  = NewButton(box);
        _save    = NewButton(box);
        _library = NewButton(box);
        _menu    = NewButton(box);
        _resume.Pressed  += Close;
        _save.Pressed    += () =>
        {
            Main.Saves.SaveNow();
            Close();
        };
        _library.Pressed += OpenLibrary;
        _menu.Pressed    += () => Main.Restart(isToMenu: true);

        // The library lies over the menu, a picked template becomes the blueprint of the tribe
        _templates = new TemplatePicker();
        _templates.Picked += design => Main.Tribe.TakeDesign(design);
        _root.AddChild(_templates);
        ApplyTexts();
    }

    private static Button NewButton(VBoxContainer box)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(BUTTON_WIDTH, 42),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        EditorStyle.ApplyButton(button, ACCENT);
        box.AddChild(button);
        return button;
    }

    /// <summary>Pauses the game and shows the menu.</summary>
    /// <returns>True when it opened: only in the free game, not over a menu, cutscene, editor, the tech tree or the build view.</returns>
    public bool Open()
    {
        if (IsOpen || Main.IsBusy || Main.Placer.IsPlacing || Main.TechTree.IsOpen || GetTree().Paused)
        {
            return false;
        }
        ApplyTexts();
        GetTree().Paused = true;
        _root.Visible    = true;
        Log.Verbose("PauseMenu", "Opened.");
        return true;
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        _templates.Close();
        _root.Visible    = false;
        GetTree().Paused = false;
        Log.Verbose("PauseMenu", "Closed.");
    }

    /// <summary>The template library over the menu, with a tab per building function.</summary>
    public void OpenLibrary()
    {
        _templates.OpenLibrary(Main.Tribe.DesignOf);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            return;
        }
        if (IsOpen)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
        else if (Open())
        {
            GetViewport().SetInputAsHandled();
        }
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
        _title.Text   = Tr("PAUSE_TITLE");
        _resume.Text  = Tr("PAUSE_RESUME");
        _save.Text    = Tr("PAUSE_SAVE");
        _library.Text = Tr("PAUSE_LIBRARY");
        _menu.Text    = Tr("PAUSE_MENU");
    }
}
