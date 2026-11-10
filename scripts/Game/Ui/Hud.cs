// NOTE:
//   HUD: stage, progress, health, air on land, banners, the diet bar, the editor icon bottom
//   left (grey without points), the division and organism buttons and the language switch.
//   Every text is a key in locale/translations.csv (German and English), translated
//   here with Tr. Created by Main, which calls Bind(player).

//
//   Chapters:
//     STATE AND API    Widgets and the calls Main and the smoke test use.
//     LANGUAGE         Toggles German/English and re-translates every text.
//     BUILD UI         Status panel, diet bar, banner, language button and widget factories.
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
    //   Refresh       Writes stage, goal, progress and health into the widgets.
    //   GoalText      Translated goal line of the current stage.
    //   EditorRequested   The editor icon was pressed, Main opens the editor.
    //   _Process      Offered buttons and the ready editor icon breathe, the air bar follows the breath.
    //   ShowBanner    Big title with subtitle, fades out after a few seconds.
    //

    private const float BANNER_HOLD = 3f;
    private const float BANNER_FADE = 1.2f;
    private const string GOAL_SEPARATOR = " · ";

    private Player _player;
    private Label _stageLabel;
    private Label _goalLabel;
    private ProgressBar _goalBar;
    private ProgressBar _hpBar;
    private ProgressBar _airBar;
    private Label _banner;
    private Label _bannerSub;
    private string _bannerTitleKey = "";
    private string _bannerTextKey = "";
    private Tween _bannerTween;
    private DietBar _dietBar;
    private Button _languageButton;
    [Signal] public delegate void EditorRequestedEventHandler();

    private const float OFFER_POP_SECONDS = 0.5f;

    private Button _divideButton;
    private Button _organismButton;
    private Button _editorIcon;
    private Label _editorBadge;
    private float _time;
    private float _divideShownAt;
    private float _editorReadyAt;
    private float _organismShownAt;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 10;
        BuildStatus();
        BuildDietBar();
        BuildBanner();
        BuildLanguageButton();
        BuildActionButtons();
        BuildEditorIcon();
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
        _goalLabel.Text  = GoalText();
        _goalBar.Value   = _player.GoalProgress() * 100f;
        _hpBar.Value     = _player.Hp / _player.MaxHp * 100f;
        _dietBar.Value   = _player.Diet;
        _dietBar.Visible = _player.Stage >= Player.SINGLE_STAGE;

        // Offered actions pop in: divide once allowed, the organism once the full cluster is big enough
        _divideShownAt   = Offer(_divideButton, _player.CanDivide, _divideShownAt);
        _organismShownAt = Offer(_organismButton, _player.CanEvolve, _organismShownAt);

        // The editor icon is there from the single cell on, grey without points, it punches once points arrive
        bool isEditorReady = _player.EvolutionPoints > 0;
        if (isEditorReady && _editorIcon.Disabled)
        {
            _editorReadyAt = _time;
            EditorStyle.Punch(_editorIcon, 1.25f);
        }
        _editorIcon.Visible     = _player.Stage >= Player.SINGLE_STAGE;
        _editorIcon.Disabled    = !isEditorReady;
        _editorIcon.TooltipText = string.Format(Tr("EDITOR_OPEN"), _player.EvolutionPoints);
        _editorBadge.Text       = _player.EvolutionPoints.ToString();
        _editorBadge.Visible    = isEditorReady;
        _editorIcon.QueueRedraw();
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

        // Air: only while it is not full, it drains on land without a lung
        if (null != _player)
        {
            _airBar.Visible = _player.Air < 0.999f;
            _airBar.Value   = _player.Air * 100f;
        }
        Breathe(_divideButton, _divideShownAt, 0f);
        Breathe(_organismButton, _organismShownAt, 2.6f);
        if (!_editorIcon.Disabled)
        {
            Breathe(_editorIcon, _editorReadyAt, 1.3f);
        }
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
    public void ShowBanner(string titleKey, string textKey)
    {
        _bannerTitleKey = titleKey;
        _bannerTextKey  = textKey;
        _banner.Text    = Tr(titleKey);
        _bannerSub.Text = Tr(textKey);
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
            _bannerSub.Text = Tr(_bannerTextKey);
        }
        Refresh();
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   BuildStatus           Stage, goal, progress, health and air top left.
    //   BuildDietBar          Herbivore / omnivore / carnivore bar top right, from stage 1 on.
    //   BuildBanner           Centered title and subtitle, invisible until ShowBanner.
    //   BuildLanguageButton   Language switch bottom right.
    //   BuildActionButtons    Divide and organism buttons bottom center, each hidden until offered.
    //   BuildEditorIcon       Round editor icon bottom left with a drawn helix and the points badge.
    //   DrawHelix             The helix of the editor icon, white when ready, grey without points.
    //   NewLabel              Label with outline that ignores the mouse and is translated here.
    //   NewBar                Rounded progress bar without percentage.
    //   SetAlpha              Sets the modulate alpha of a control.
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
        _goalLabel  = NewLabel(16);
        _goalBar    = NewBar(new Color(0.45f, 0.9f, 0.6f));
        _hpBar      = NewBar(new Color(0.95f, 0.4f, 0.4f));
        _airBar     = NewBar(new Color(0.55f, 0.85f, 1f));
        _airBar.Visible = false;
        box.AddChild(_stageLabel);
        box.AddChild(_goalLabel);
        box.AddChild(_goalBar);
        box.AddChild(_hpBar);
        box.AddChild(_airBar);
    }

    private void BuildDietBar()
    {
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        margin.GrowHorizontal = Control.GrowDirection.Begin;
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        AddChild(margin);
        _dietBar = new DietBar { Visible = false };
        margin.AddChild(_dietBar);
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
        _languageButton.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        _languageButton.GrowHorizontal = Control.GrowDirection.Begin;
        _languageButton.GrowVertical   = Control.GrowDirection.Begin;
        _languageButton.Position      += new Vector2(-20, -20);
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

    private const float EDITOR_ICON_SIZE = 76f;
    private static readonly Color EDITOR_ACCENT = new(0.95f, 0.85f, 0.35f);
    private static readonly Color EDITOR_GREY = new(0.55f, 0.6f, 0.62f);

    private void BuildEditorIcon()
    {
        // No focus: E opens the editor through Main, a focused button would fire twice
        _editorIcon = new Button
        {
            CustomMinimumSize = Vector2.One * EDITOR_ICON_SIZE,
            Size              = Vector2.One * EDITOR_ICON_SIZE,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            Disabled          = true,
            Visible           = false,
        };
        _editorIcon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _editorIcon.GrowVertical = Control.GrowDirection.Begin;
        _editorIcon.Position    += new Vector2(24f, -24f);
        _editorIcon.PivotOffset  = Vector2.One * EDITOR_ICON_SIZE * 0.5f;
        EditorStyle.ApplyButton(_editorIcon, EDITOR_ACCENT);
        int round = (int)(EDITOR_ICON_SIZE * 0.5f);
        var grey  = EditorStyle.Pill(EDITOR_GREY.Darkened(0.65f), EDITOR_GREY.Darkened(0.2f));
        _editorIcon.AddThemeStyleboxOverride("disabled", grey);
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            ((StyleBoxFlat)_editorIcon.GetThemeStylebox(state)).SetCornerRadiusAll(round);
        }
        _editorIcon.Pressed += () => EmitSignal(SignalName.EditorRequested);
        _editorIcon.Draw    += DrawHelix;
        AddChild(_editorIcon);

        // Points badge on the upper right rim
        _editorBadge = NewLabel(16);
        _editorBadge.HorizontalAlignment = HorizontalAlignment.Center;
        StyleBoxFlat badge = EditorStyle.Pill(EDITOR_ACCENT.Darkened(0.2f), Colors.White, 0.5f);
        badge.SetContentMarginAll(1);
        badge.ContentMarginLeft  = 7;
        badge.ContentMarginRight = 7;
        _editorBadge.AddThemeStyleboxOverride("normal", badge);
        _editorBadge.Position = new Vector2(EDITOR_ICON_SIZE - 26f, -8f);
        _editorIcon.AddChild(_editorBadge);
    }

    /// <summary>Two strands winding around the middle with rungs between them, like the DNA of the protocell.</summary>
    private void DrawHelix()
    {
        const int   STEPS  = 24;
        const float TURNS  = 1.25f;
        Color   color  = _editorIcon.Disabled ? EDITOR_GREY : Colors.White;
        Vector2 center = Vector2.One * EDITOR_ICON_SIZE * 0.5f;
        float   height = EDITOR_ICON_SIZE * 0.52f;
        float   width  = EDITOR_ICON_SIZE * 0.2f;
        var     left   = new Vector2[STEPS + 1];
        var     right  = new Vector2[STEPS + 1];
        for (int i = 0; i <= STEPS; i++)
        {
            float t     = (float)i / STEPS;
            float phase = t * TURNS * Mathf.Tau;
            float y     = center.Y + (t - 0.5f) * height;
            left[i]  = new Vector2(center.X + Mathf.Sin(phase) * width, y);
            right[i] = new Vector2(center.X - Mathf.Sin(phase) * width, y);
            if (0 == i % 4 && i > 0 && i < STEPS)
            {
                _editorIcon.DrawLine(left[i], right[i], new Color(color, 0.6f), 2f, true);
            }
        }
        _editorIcon.DrawPolyline(left, color, 3f, true);
        _editorIcon.DrawPolyline(right, color, 3f, true);
    }

    /// <summary>Pill button without focus: Space and E act through the game, a focused button would fire twice.</summary>
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

    private static ProgressBar NewBar(Color color)
    {
        var background = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.4f) };
        var fill       = new StyleBoxFlat { BgColor = color };
        background.SetCornerRadiusAll(3);
        fill.SetCornerRadiusAll(3);

        var bar = new ProgressBar { CustomMinimumSize = new Vector2(320, 14), ShowPercentage = false };
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);
        return bar;
    }

    private static void SetAlpha(CanvasItem item, float alpha)
    {
        Color modulate = item.Modulate;
        modulate.A    = alpha;
        item.Modulate = modulate;
    }
}
