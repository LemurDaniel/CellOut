// NOTE:
//   Title menu over the paused primordial soup: the bobbing "Cell Out" logo, a tag line,
//   the start button and a language switch. Start (button, Enter or Space) fades the menu
//   out, emits StartPressed and frees itself. Created and wired by Main.
//
//   var menu = new TitleMenu();
//   AddChild(menu);
//   menu.StartPressed    += OnStartPressed;
//   menu.LanguagePressed += Hud.ToggleLanguage;

//
//   Chapters:
//     SIGNALS AND INPUT   Start and language signals, keyboard start.
//     ANIMATE             Logo bob, fade in and fade out.
//     BUILD UI            Vignette, logo, tag line, start and language buttons.
//

using Godot;

namespace CellOut;

public partial class TitleMenu : CanvasLayer
{
    // ===========================================================================
    // ==========================   SIGNALS AND INPUT   ==========================
    // ===========================================================================
    //
    //   StartPressed      The player wants to play, the menu is fading out already.
    //   LanguagePressed   The language button was pressed, Main toggles via the HUD.
    //   CycleGraphics     The graphics button: next tier (Core/GraphicsQuality.cs), remembered for the next start.
    //   Start             Fades out, emits StartPressed once and frees the menu.
    //   _UnhandledInput   Enter and Space start as well.
    //   _Notification     Re-translates after a locale change.
    //

    [Signal] public delegate void StartPressedEventHandler();
    [Signal] public delegate void LanguagePressedEventHandler();

    private const float FADE_SECONDS = 0.5f;

    private bool _isStarting;

    /// <summary>Fades the menu out, emits StartPressed once and frees the menu afterwards.</summary>
    public void Start()
    {
        if (_isStarting)
        {
            return;
        }
        _isStarting = true;
        Log.Verbose("TitleMenu", "Start pressed -> fading out.");

        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        fade.TweenProperty(_root, "modulate:a", 0f, FADE_SECONDS);
        fade.TweenProperty(_logo, "position:y", _logo.Position.Y - 80f, FADE_SECONDS)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        fade.Chain().TweenCallback(Callable.From(QueueFree));
        EmitSignal(SignalName.StartPressed);
    }

    /// <summary>Switches to the next graphics tier and remembers it.</summary>
    private void CycleGraphics()
    {
        GraphicsQuality.Use(GraphicsQuality.Next());
        UserSettings.SaveGraphics(GraphicsQuality.Tier);
        Translate();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Space })
        {
            GetViewport().SetInputAsHandled();
            Start();
        }
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _startButton)
        {
            Translate();
        }
    }

    // ===========================================================================
    // ===============================   ANIMATE   ===============================
    // ===========================================================================
    //
    //   _Process   Bobs and tilts the logo, the start button breathes.
    //   FadeIn     Logo drops in, tag line and button follow.
    //

    private const float BOB_PIXELS = 8f;
    private const float BOB_SPEED = 1.6f;
    private const float TILT_RADIANS = 0.025f;
    private const float BREATHE_AFTER = 1.2f;        // seconds, after FadeIn

    private float _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _logo.Rotation = Mathf.Sin(_time * BOB_SPEED * 0.7f) * TILT_RADIANS;
        _logoBob.Position = new Vector2(0f, Mathf.Sin(_time * BOB_SPEED) * BOB_PIXELS);
        // Breathing starts after the pop-in, which tweens the same scale
        if (!_isStarting && _time > BREATHE_AFTER && !_startButton.IsHovered())
        {
            _startButton.Scale = Vector2.One * (1f + 0.025f * Mathf.Sin(_time * 3f));
        }
    }

    private void FadeIn()
    {
        _root.Modulate = new Color(1f, 1f, 1f, 0f);
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenProperty(_root, "modulate:a", 1f, FADE_SECONDS);
        EditorStyle.PopIn(_logo, 0.1f);
        EditorStyle.PopIn(_startButton, 0.45f);
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   _Ready      Keeps running while paused, builds and fades in.
    //   BuildUi     Vignette, centered logo with tag line, start button, graphics and language buttons.
    //   Translate   Texts of tag line and buttons.
    //   NewLabel    Label with outline, translated here.
    //

    private const int LOGO_FONT_SIZE = 120;
    private static readonly Color START_ACCENT = new(0.55f, 0.85f, 0.95f);   // protocell blue

    private Control _root;
    private Control _logo;
    private Control _logoBob;
    private Label _tagLine;
    private Button _startButton;
    private Button _languageButton;
    private Button _graphicsButton;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 30;
        BuildUi();
        Translate();
        FadeIn();
    }

    private void BuildUi()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // Vignette: dark edges, the soup stays visible in the middle
        var vignette = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors  = new[] { new Color(0.01f, 0.03f, 0.06f, 0.15f), new Color(0.01f, 0.03f, 0.06f, 0.85f) },
                    Offsets = new[] { 0.2f, 1f },
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
        _root.AddChild(vignette);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 18);
        center.AddChild(column);

        // Logo: the pivot control pops and tilts, the inner one bobs
        _logo = new Control { CustomMinimumSize = new Vector2(760, 170), MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(_logo);
        _logoBob = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _logoBob.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _logo.AddChild(_logoBob);
        Label title = NewLabel(LOGO_FONT_SIZE);
        GameFonts.UseDisplay(title);
        title.Text                = Tr("BANNER_START_TITLE");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment   = VerticalAlignment.Center;
        title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _logoBob.AddChild(title);

        _tagLine = NewLabel(20);
        _tagLine.HorizontalAlignment = HorizontalAlignment.Center;
        _tagLine.Modulate            = new Color(1f, 1f, 1f, 0.8f);
        column.AddChild(_tagLine);

        // Start button, centered below
        var buttonRow = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        buttonRow.CustomMinimumSize = new Vector2(0f, 90f);
        column.AddChild(buttonRow);
        _startButton = new Button
        {
            CustomMinimumSize = new Vector2(260, 60),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        _startButton.AddThemeFontSizeOverride("font_size", 24);
        EditorStyle.ApplyButton(_startButton, START_ACCENT);
        _startButton.Pressed      += Start;
        _startButton.MouseEntered += () => EditorStyle.ScaleTo(_startButton, 1.08f);
        _startButton.MouseExited  += () => EditorStyle.ScaleTo(_startButton, 1f);
        buttonRow.AddChild(_startButton);

        // Settings bottom right: the language switch like in the HUD
        _languageButton = new Button
        {
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        _languageButton.AddThemeFontSizeOverride("font_size", 14);
        _languageButton.Pressed += () => EmitSignal(SignalName.LanguagePressed);

        // Graphics tier next to it, a click cycles Low, Medium, High
        _graphicsButton = new Button
        {
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        _graphicsButton.AddThemeFontSizeOverride("font_size", 14);
        _graphicsButton.Pressed += CycleGraphics;

        var settings = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        settings.AddThemeConstantOverride("separation", 10);
        settings.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        settings.SizeFlagsVertical   = Control.SizeFlags.ShrinkEnd;
        settings.AddChild(_graphicsButton);
        settings.AddChild(_languageButton);

        // A margin container keeps it inside the screen at every size, also when the text grows
        var corner = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        corner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            corner.AddThemeConstantOverride(side, 20);
        }
        corner.AddChild(settings);
        _root.AddChild(corner);
    }

    private void Translate()
    {
        _tagLine.Text        = Tr("MENU_TAGLINE");
        _startButton.Text    = Tr("MENU_START");
        _languageButton.Text = Tr("LANGUAGE_BUTTON");
        _graphicsButton.Text = string.Format(Tr("GRAPHICS_BUTTON"), Tr($"GRAPHICS_{GraphicsQuality.Tier}".ToUpperInvariant()));
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
