// NOTE:
//   Game over over the paused soup: a big "Game Over", how far the player came (stage and
//   biomass), "Try again" (new game with intro) and "Title menu". Keeps running while the
//   tree is paused. Created and wired by Main when the player dies.
//
//   var screen = new GameOverScreen { StageKey = Player.STAGE_KEYS[player.Stage], Biomass = player.Biomass };
//   AddChild(screen);
//   screen.RestartPressed += () => Restart(isToMenu: false);
//   screen.MenuPressed    += () => Restart(isToMenu: true);

//
//   Chapters:
//     SIGNALS    Restart and menu signals, keyboard restart.
//     BUILD UI   Dark vignette, title, result line and the two buttons, popping in.
//

using Godot;

namespace CellOut;

public partial class GameOverScreen : CanvasLayer
{
    // ===========================================================================
    // ===============================   SIGNALS   ===============================
    // ===========================================================================
    //
    //   RestartPressed    A new game, Main reloads the scene and skips the title menu.
    //   MenuPressed       Back to the title menu, Main reloads the scene.
    //   _UnhandledInput   Enter and Space try again.
    //

    [Signal] public delegate void RestartPressedEventHandler();
    [Signal] public delegate void MenuPressedEventHandler();

    /// <summary>Translation key of the stage the player reached, e.g. STAGE_SINGLE.</summary>
    public string StageKey { get; set; } = Player.STAGE_KEYS[0];

    /// <summary>Biomass at the end, shown rounded.</summary>
    public float Biomass { get; set; }

    private bool _isChosen;

    private void Choose(bool isRestart)
    {
        if (_isChosen)
        {
            return;
        }
        _isChosen = true;
        Log.Verbose("GameOverScreen", isRestart ? "Try again pressed." : "Title menu pressed.");
        EmitSignal(isRestart ? SignalName.RestartPressed : SignalName.MenuPressed);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Space })
        {
            GetViewport().SetInputAsHandled();
            Choose(isRestart: true);
        }
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   _Ready     Keeps running while paused, builds and fades in.
    //   BuildUi    Vignette, centered title, result line and the two buttons.
    //   NewButton  A styled button that grows under the mouse.
    //   NewLabel   Label with outline.
    //

    private const float FADE_SECONDS = 0.6f;
    private const int TITLE_FONT_SIZE = 96;
    private static readonly Color DEATH_ACCENT = new(1f, 0.45f, 0.4f);
    private static readonly Color MENU_ACCENT = new(0.55f, 0.85f, 0.95f);

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 30;
        Control root = BuildUi();
        root.Modulate = new Color(1f, 1f, 1f, 0f);
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenProperty(root, "modulate:a", 1f, FADE_SECONDS);
    }

    private Control BuildUi()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // Vignette: darker than the title menu, the soup fades into the background
        var vignette = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors  = new[] { new Color(0.06f, 0.01f, 0.02f, 0.45f), new Color(0.02f, 0.0f, 0.01f, 0.92f) },
                    Offsets = new[] { 0.1f, 1f },
                },
                Fill     = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo   = new Vector2(1.1f, 0.5f),
            },
            ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        vignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(vignette);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 22);
        center.AddChild(column);

        // Title and how far the player came
        Label title = NewLabel(TITLE_FONT_SIZE);
        GameFonts.UseDisplay(title);
        title.Text                = Tr("GAMEOVER_TITLE");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", DEATH_ACCENT);
        column.AddChild(title);

        Label result = NewLabel(22);
        result.Text                = string.Format(Tr("GAMEOVER_TEXT"), Tr(StageKey), Mathf.RoundToInt(Biomass));
        result.HorizontalAlignment = HorizontalAlignment.Center;
        result.Modulate            = new Color(1f, 1f, 1f, 0.85f);
        column.AddChild(result);

        // Try again, title menu
        var buttons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 24);
        buttons.CustomMinimumSize = new Vector2(0f, 90f);
        column.AddChild(buttons);
        Button restart = NewButton("GAMEOVER_RESTART", DEATH_ACCENT, 24);
        restart.Pressed += () => Choose(isRestart: true);
        buttons.AddChild(restart);
        Button menu = NewButton("GAMEOVER_MENU", MENU_ACCENT, 20);
        menu.Pressed += () => Choose(isRestart: false);
        buttons.AddChild(menu);

        EditorStyle.PopIn(title, 0.1f);
        EditorStyle.PopIn(restart, 0.45f);
        EditorStyle.PopIn(menu, 0.55f);
        return root;
    }

    private Button NewButton(string textKey, Color accent, int fontSize)
    {
        var button = new Button
        {
            Text              = Tr(textKey),
            CustomMinimumSize = new Vector2(240, 60),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        EditorStyle.ApplyButton(button, accent);
        button.MouseEntered += () => EditorStyle.ScaleTo(button, 1.08f);
        button.MouseExited  += () => EditorStyle.ScaleTo(button, 1f);
        return button;
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }
}
