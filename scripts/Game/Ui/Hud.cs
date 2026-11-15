// NOTE:
//   HUD: the stage top left, banners, the dial bottom right (VitalsDial: health,
//   progress, diet, air on land and the editor icon in its middle), the division and
//   organism buttons and the language switch top right.
//   Every text is a key in locale/translations.csv (German and English), translated
//   here with Tr. Created by Main, which calls Bind(player).

//
//   Chapters:
//     STATE AND API    Widgets and the calls Main and the smoke test use.
//     LANGUAGE         Toggles German/English and re-translates every text.
//     BUILD UI         Status panel, dial, banner, language button and widget factories.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Hud : CanvasLayer
{
    // ===========================================================================
    // ============================   STATE AND API   ============================
    // ===========================================================================
    //
    //   Bind          Follows the stats of the player.
    //   Refresh       Writes the stage into its label, the numbers into the dial.
    //   GoalText      Translated goal line of the current stage, the tooltip of the progress ring.
    //   EditorRequested   The editor icon of the dial was pressed, Main opens the editor.
    //   _Process      Offered buttons breathe, the air arc of the dial follows the breath.
    //   ShowBanner    Big title with subtitle, fades out after a few seconds.
    //

    private const float BANNER_HOLD = 3f;
    private const float BANNER_FADE = 1.2f;
    private const string GOAL_SEPARATOR = " · ";

    private Player _player;
    private Label _stageLabel;
    private VitalsDial _dial;
    private Label _banner;
    private Label _bannerSub;
    private string _bannerTitleKey = "";
    private string _bannerTextKey = "";
    private object[] _bannerArgs = System.Array.Empty<object>();
    private Tween _bannerTween;
    private Button _languageButton;
    [Signal] public delegate void EditorRequestedEventHandler();

    private const float OFFER_POP_SECONDS = 0.5f;

    private Button _divideButton;
    private Button _organismButton;
    private float _time;
    private float _divideShownAt;
    private float _organismShownAt;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 10;
        BuildStatus();
        BuildDial();
        BuildBanner();
        BuildLanguageButton();
        BuildActionButtons();
        ApplyTexts();
    }

    /// <summary>Follows the stats of the player.</summary>
    /// <param name="player">(Required) The player whose StatsChanged drives Refresh.</param>
    public void Bind(Player player)
    {
        _player = player;
        _player.StatsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (null == _player)
        {
            return;
        }
        _stageLabel.Text = Tr(Player.STAGE_KEYS[_player.Stage]);

        // The dial: health, progress, diet (from the single cell on) and the editor icon with its points
        _dial.Refresh(new VitalsDial.State(
            Health:      _player.Hp / _player.MaxHp,
            Progress:    _player.GoalProgress(),
            Diet:        _player.Diet,
            HasDiet:     _player.Stage >= Player.SINGLE_STAGE,
            Points:      _player.EvolutionPoints,
            HealthTip:   string.Format(Tr("DIAL_HEALTH"), Mathf.CeilToInt(_player.Hp), Mathf.CeilToInt(_player.MaxHp)),
            ProgressTip: GoalText(),
            EditorTip:   string.Format(Tr("EDITOR_OPEN"), _player.EvolutionPoints)));

        // Offered actions pop in: divide once allowed, the organism once the full cluster is big enough
        _divideShownAt   = Offer(_divideButton, _player.CanDivide, _divideShownAt);
        _organismShownAt = Offer(_organismButton, _player.CanEvolve, _organismShownAt);
    }

    /// <summary>Shows or hides an action button, a newly shown one pops in.</summary>
    /// <returns>The time the button was shown, unchanged while it stays.</returns>
    private float Offer(Button button, bool isOffered, float shownAt)
    {
        if (isOffered && !button.Visible)
        {
            shownAt = _time;
            EditorStyle.PopIn(button, 0f);
        }
        button.Visible = isOffered;
        return shownAt;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;

        // Air: it drains on land without a lung, the dial shows it while it is not full
        if (null != _player)
        {
        }
        Breathe(_divideButton, _divideShownAt, 0f);
        Breathe(_organismButton, _organismShownAt, 2.6f);
    }

    /// <summary>A gentle pulse once the pop-in is over, which tweens the same scale.</summary>
    private void Breathe(Button button, float shownAt, float phase)
    {
        bool isPoppedIn = _time - shownAt > OFFER_POP_SECONDS;
        if (button.Visible && isPoppedIn && !button.IsHovered())
        {
            button.Scale = Vector2.One * (1f + 0.04f * Mathf.Sin(_time * 4f + phase));
        }
    }

    /// <summary>
    /// Translated goal line of the current stage:
    /// - Protocell:     "Collect molecules: 12 / 40 → single cell"
    /// - Single cell:   "Biomass 80 · next evolution at 120 · division at 100"
    /// - Multicellular: "Biomass 500 · next evolution at 740 · 4 / 64 cells · division at 625"
    /// - Full cluster:  "Biomass 9800 · next evolution at 12000 · 64 / 64 cells · organism at 24414"
    /// - Organism:      "Biomass 30000 · next evolution at 52000"
    /// </summary>
    private string GoalText()
    {
        if (0 == _player.Stage)
        {
            return string.Format(Tr("GOAL_COLLECT"), (int)_player.Biomass, (int)Balance.Current.Player.SingleCellAt);
        }

        var parts = new List<string>
        {
            string.Format(Tr("GOAL_BIOMASS"), (int)_player.Biomass),
            string.Format(Tr("GOAL_NEXT_LEVEL"), (int)_player.NextLevelAt),
        };
        if (_player.IsOrganism)
        {
            return string.Join(GOAL_SEPARATOR, parts);
        }
        if (_player.CellCount > 1)
        {
            parts.Add(string.Format(Tr("GOAL_CELLS"), _player.CellCount, Player.MAX_CELLS));
        }
        parts.Add(_player.CellCount < Player.MAX_CELLS
            ? string.Format(Tr("GOAL_DIVIDE_AT"), (int)_player.NextDivisionAt)
            : string.Format(Tr("GOAL_ORGANISM_AT"), (int)_player.EvolveAt));
        return string.Join(GOAL_SEPARATOR, parts);
    }

    /// <summary>Shows a big title with subtitle that fades out after a few seconds.</summary>
    /// <param name="titleKey">(Required) Translation key of the title, e.g. BANNER_START_TITLE.</param>
    /// <param name="textKey">(Required) Translation key of the line below.</param>
    /// <param name="args">(Optional) Values for the placeholders of the line below. Defaults to none.</param>
    public void ShowBanner(string titleKey, string textKey, params object[] args)
    {
        _bannerTitleKey = titleKey;
        _bannerTextKey  = textKey;
        _bannerArgs     = args;
        _banner.Text    = Tr(titleKey);
        _bannerSub.Text = string.Format(Tr(textKey), args);
        _bannerTween?.Kill();
        SetAlpha(_banner, 1f);
        SetAlpha(_bannerSub, 1f);
        _bannerTween = CreateTween().SetParallel(true);
        _bannerTween.TweenProperty(_banner, "modulate:a", 0f, BANNER_FADE).SetDelay(BANNER_HOLD);
        _bannerTween.TweenProperty(_bannerSub, "modulate:a", 0f, BANNER_FADE).SetDelay(BANNER_HOLD);
    }

    // ===========================================================================
    // ==============================   LANGUAGE   ===============================
    // ===========================================================================
    //
    //   ToggleLanguage     Switches between German and English.
    //   _Notification      Re-translates every text after a locale change.
    //   ApplyTexts         Static texts, banner and status.
    //

    private const string LOCALE_GERMAN = "de";
    private const string LOCALE_ENGLISH = "en";

    /// <summary>Switches between German and English and remembers it, every text follows via _Notification.</summary>
    public void ToggleLanguage()
    {
        bool   isGerman = TranslationServer.GetLocale().StartsWith(LOCALE_GERMAN);
        string locale   = isGerman ? LOCALE_ENGLISH : LOCALE_GERMAN;
        Log.Verbose("Hud", $"Language switched to '{locale}'.");
        TranslationServer.SetLocale(locale);
        UserSettings.SaveLocale(locale);
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged != what || null == _languageButton)
        {
            return;
        }
        ApplyTexts();
    }

    private void ApplyTexts()
    {
        _languageButton.Text = Tr("LANGUAGE_BUTTON");
        _divideButton.Text   = Tr("DIVIDE_BUTTON");
        _organismButton.Text = Tr("ORGANISM_BUTTON");
        if (!string.IsNullOrEmpty(_bannerTitleKey))
        {
            _banner.Text    = Tr(_bannerTitleKey);
            _bannerSub.Text = string.Format(Tr(_bannerTextKey), _bannerArgs);
        }
        Refresh();
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   BuildStatus           The stage top left.
    //   BuildDial             The dial bottom right: health, progress, diet, air and the editor icon.
    //   BuildBanner           Centered title and subtitle, invisible until ShowBanner.
    //   BuildLanguageButton   Language switch top right.
    //   BuildActionButtons    Divide and organism buttons bottom center, each hidden until offered.
    //   NewLabel              Label with outline that ignores the mouse and is translated here.
    //   SetAlpha             Sets the modulate alpha of a control.
    //

    private void BuildStatus()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        margin.AddChild(box);
        _stageLabel = NewLabel(34);
        GameFonts.UseDisplay(_stageLabel);
        box.AddChild(_stageLabel);
    }

    private void BuildDial()
    {
        _dial = new VitalsDial();
        _dial.EditorPressed += () => EmitSignal(SignalName.EditorRequested);
        AddChild(_dial);
    }

    private void BuildBanner()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        box.OffsetTop = 110;
        AddChild(box);

        _banner    = NewLabel(64);
        GameFonts.UseDisplay(_banner);
        _bannerSub = NewLabel(20);
        _bannerSub.AutowrapMode = TextServer.AutowrapMode.Word;    // long banner lines (tribe) wrap instead of leaving the screen
        foreach (Label label in new[] { _banner, _bannerSub })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            SetAlpha(label, 0f);
            box.AddChild(label);
        }
    }

    private void BuildLanguageButton()
    {
        // No focus: Space and Enter must not toggle the language while swimming
        _languageButton = new Button
        {
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        _languageButton.AddThemeFontSizeOverride("font_size", 14);
        _languageButton.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _languageButton.GrowHorizontal = Control.GrowDirection.Begin;
        _languageButton.Position      += new Vector2(-20, 20);
        _languageButton.Pressed       += ToggleLanguage;
        AddChild(_languageButton);
    }

    private void BuildActionButtons()
    {
        var bottom = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bottom.AddThemeConstantOverride("margin_bottom", 40);
        AddChild(bottom);
        var row = new HBoxContainer
        {
            MouseFilter         = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkEnd,
        };
        row.AddThemeConstantOverride("separation", 18);
        bottom.AddChild(row);

        _divideButton = NewActionButton(new Color(0.55f, 0.95f, 0.7f));
        _divideButton.Pressed += () => _player?.Divide();
        row.AddChild(_divideButton);
        _organismButton = NewActionButton(new Color(0.95f, 0.65f, 0.45f));
        _organismButton.Pressed += () => _player?.RequestEvolve();
        row.AddChild(_organismButton);
    }

    /// <summary>Pill button without focus: Space and Z act through the game, a focused button would fire twice.</summary>
    private static Button NewActionButton(Color accent)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(280, 54),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            Visible           = false,
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        EditorStyle.ApplyButton(button, accent);
        return button;
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label
        {
            MouseFilter       = Control.MouseFilterEnum.Ignore,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.08f, 0.1f));
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }

    private static void SetAlpha(CanvasItem item, float alpha)
    {
        Color modulate = item.Modulate;
        modulate.A    = alpha;
        item.Modulate = modulate;
    }
}
