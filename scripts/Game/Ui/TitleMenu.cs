// NOTE:
//   Title menu: the Milky Way with the planets to play on (Menu/GalaxyView.cs), the bobbing
//   "Cell Out" logo with its tag line, the graphics and language buttons. A click on a
//   planet flies onto it and shows it up close with its start phases (Menu/PlanetView.cs),
//   Back or Escape returns to the galaxy. Start (button, Enter or Space in the planet view)
//   makes the planet current (Core/Planets.cs), fades the menu out, raises StartPressed
//   with the phase and frees itself. Created by Flow/StartFlow.cs.
//
//   var menu = new TitleMenu();
//   AddChild(menu);
//   menu.StartPressed    += phase => Start(phase);
//   menu.LanguagePressed += Hud.ToggleLanguage;
//   menu.ShowPlanet(Planets.Get("RUBRA"));      // e.g. from the graphics reel

//
//   Chapters:
//     SIGNALS AND INPUT   Start and language, keyboard, the two views.
//     ANIMATE             Logo bob, fade in and fade out.
//     BUILD UI            Galaxy, planet view, logo, tag line, settings.
//

using Godot;

namespace CellOut;

public partial class TitleMenu : CanvasLayer
{
    // ===========================================================================
    // ==========================   SIGNALS AND INPUT   ==========================
    // ===========================================================================
    //
    //   StartPressed      The player starts on the current planet in a phase, the menu fades already (a C# event: an enum is kept as it is).
    //   LanguagePressed   The language button was pressed, Main toggles via the HUD.
    //   ShowPlanet        Flies onto a planet and shows it up close.
    //   ShowGalaxy        Back out to the whole galaxy.
    //   Start             Chooses the planet, fades out, raises StartPressed once and frees the menu.
    //   ContinuePressed / Continue   A saved game of the planet view goes on (Core/Save/SaveSlots.cs).
    //   CycleGraphics     The graphics button: next tier (Core/GraphicsQuality.cs), remembered for the next start.
    //   _UnhandledInput   Enter and Space start in the planet view (open the first planet in the galaxy), Escape goes back, F10 opens every phase.
    //   _Notification     Re-translates after a locale change.
    //

    public event System.Action<StartPhase> StartPressed;
    public event System.Action<SaveGame> ContinuePressed;

    [Signal] public delegate void LanguagePressedEventHandler();

    private const float FADE_SECONDS = 0.5f;

    private bool _isStarting;

    /// <summary>Flies onto a planet and shows it up close with its start phases.</summary>
    /// <param name="planet">(Required) The planet.</param>
    public void ShowPlanet(Planet planet)
    {
        if (_isStarting)
        {
            return;
        }
        _galaxy.Focus(planet);
        _planet.Show(planet);
        FadeLogo(0f);
    }

    /// <summary>Back out to the galaxy.</summary>
    public void ShowGalaxy()
    {
        _planet.Visible = false;
        _galaxy.Unfocus();
        FadeLogo(1f);
    }

    /// <summary>Makes the shown planet current, fades the menu out, raises StartPressed and frees the menu.</summary>
    /// <param name="phase">(Required) The phase to start in.</param>
    public void Start(StartPhase phase)
    {
        if (_isStarting)
        {
            return;
        }
        _isStarting = true;
        Planets.Choose((_planet.Planet ?? Planets.Current).Key);
        if (_planet.Visible)
        {
            _planet.Dive();
        }
        Log.Verbose("TitleMenu", $"Start on {Planets.Current.Key} in phase {phase} -> fading out.");

        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        fade.TweenProperty(_root, "modulate:a", 0f, FADE_SECONDS);
        fade.Chain().TweenCallback(Callable.From(QueueFree));
        StartPressed?.Invoke(phase);
    }

    /// <summary>Continues a saved game: its planet becomes current, the menu fades out, ContinuePressed fires once.</summary>
    /// <param name="save">(Required) The saved game.</param>
    public void Continue(SaveGame save)
    {
        if (_isStarting || null == save)
        {
            return;
        }
        _isStarting = true;
        Planets.Choose(save.Meta.Planet);
        Log.Verbose("TitleMenu", $"Continue on {save.Meta.Planet} at stage {save.Meta.Stage}.");
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenProperty(_root, "modulate:a", 0f, FADE_SECONDS);
        fade.TweenCallback(Callable.From(QueueFree));
        ContinuePressed?.Invoke(save);
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
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space)
        {
            GetViewport().SetInputAsHandled();
            if (_planet.Visible)
            {
                _planet.Start();
            }
            else
            {
                ShowPlanet(Planets.Current);
            }
        }
        else if (Key.F10 == key.Keycode)
        {
            // Cheat: every start phase open for this session, like the stage jumps of the F10 menu in the game
            GetViewport().SetInputAsHandled();
            PlanetProgress.IsCheatOpen = !PlanetProgress.IsCheatOpen;
            if (_planet.Visible)
            {
                _planet.Refresh();
            }
        }
        else if (Key.Escape == key.Keycode && _planet.Visible)
        {
            GetViewport().SetInputAsHandled();
            ShowGalaxy();
        }
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _languageButton)
        {
            Translate();
        }
    }

    // ===========================================================================
    // ===============================   ANIMATE   ===============================
    // ===========================================================================
    //
    //   _Process   Bobs and tilts the logo.
    //   FadeIn     The menu fades in, the logo drops in.
    //   FadeLogo   Logo and tag line leave while a planet is shown.
    //

    private const float BOB_PIXELS = 6f;
    private const float BOB_SPEED = 1.6f;
    private const float TILT_RADIANS = 0.025f;

    private float _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _logo.Rotation    = Mathf.Sin(_time * BOB_SPEED * 0.7f) * TILT_RADIANS;
        _logoBob.Position = new Vector2(0f, Mathf.Sin(_time * BOB_SPEED) * BOB_PIXELS);
    }

    private void FadeIn()
    {
        _root.Modulate = new Color(1f, 1f, 1f, 0f);
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenProperty(_root, "modulate:a", 1f, FADE_SECONDS);
        EditorStyle.PopIn(_logo, 0.1f);
    }

    private void FadeLogo(float alpha)
    {
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenProperty(_logoColumn, "modulate:a", alpha, 0.35f);
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   _Ready      Keeps running while paused, builds and fades in.
    //   BuildUi     Galaxy, planet view, logo and tag line top left, graphics and language buttons.
    //   Translate   Every text in the current language.
    //   NewLabel    Label with outline, translated here.
    //

    private const int LOGO_FONT_SIZE = 96;

    private Control _root;
    private GalaxyView _galaxy;
    private PlanetView _planet;
    private Control _logoColumn;
    private Control _logo;
    private Control _logoBob;
    private Label _tagLine;
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

        // The Milky Way fills the screen, the planet view lies over it
        _galaxy = new GalaxyView();
        _galaxy.PlanetPicked += ShowPlanet;
        _root.AddChild(_galaxy);
        _planet = new PlanetView();
        _planet.StartPressed    += Start;
        _planet.ContinuePressed += Continue;
        _planet.BackPressed  += ShowGalaxy;
        _root.AddChild(_planet);

        // Logo top left: the pivot control pops and tilts, the inner one bobs
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(40, 30) };
        column.AddThemeConstantOverride("separation", 4);
        _root.AddChild(column);
        _logoColumn = column;
        _logo = new Control { CustomMinimumSize = new Vector2(560, 120), PivotOffset = new Vector2(280, 60), MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(_logo);
        _logoBob = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _logoBob.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _logo.AddChild(_logoBob);
        Label title = NewLabel(LOGO_FONT_SIZE);
        GameFonts.UseDisplay(title);
        title.Text              = Tr("BANNER_START_TITLE");
        title.VerticalAlignment = VerticalAlignment.Center;
        title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _logoBob.AddChild(title);
        _tagLine = NewLabel(20);
        _tagLine.Modulate = new Color(1f, 1f, 1f, 0.8f);
        column.AddChild(_tagLine);

        // Settings bottom right
        _languageButton = new Button { FocusMode = Control.FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        _languageButton.AddThemeFontSizeOverride("font_size", 14);
        _languageButton.Pressed += () => EmitSignal(SignalName.LanguagePressed);
        _graphicsButton = new Button { FocusMode = Control.FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
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
        _languageButton.Text = Tr("LANGUAGE_BUTTON");
        _graphicsButton.Text = string.Format(Tr("GRAPHICS_BUTTON"), Tr($"GRAPHICS_{GraphicsQuality.Tier}".ToUpperInvariant()));
        _galaxy.Translate();
        _planet.Translate();
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
