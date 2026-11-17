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
using System.Linq;
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
    //   AbilityPressed    An ability slot was clicked, Main fires it like its key.
    //   BindWays          The encounters of pack and tribe for the slots of the three ways.
    //   SlotsNow          The ability slots of this frame: key abilities of the cell or the three ways.
    //   FansOf            From the pack on: per way its fan, the way and the ability parts of its tab.
    //   PressWayKey       Key 1, 2 or 3: opens the fan of that way, again uses the way.
    //   _Process      Offered buttons breathe, the ability slots follow their cooldowns.
    //   ShowBanner    Big title with subtitle, fades out after a few seconds.
    //

    private const float BANNER_HOLD = 3f;
    private const float BANNER_FADE = 1.2f;
    private const string GOAL_SEPARATOR = " · ";

    private Player _player;
    private Label _stageLabel;
    private VitalsDial _dial;
    private AbilitySlots _abilitySlots;
    private Label _banner;
    private Label _bannerSub;
    private string _bannerTitleKey = "";
    private string _bannerTextKey = "";
    private object[] _bannerArgs = System.Array.Empty<object>();
    private Tween _bannerTween;
    private Button _languageButton;
    [Signal] public delegate void EditorRequestedEventHandler();
    [Signal] public delegate void AbilityPressedEventHandler(int index);
    [Signal] public delegate void AbilityFanPressedEventHandler(int way, int part);

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

        // Cooldowns of the key abilities change every frame
        if (null != _player)
        {
            List<AbilitySlots.Slot> slots = SlotsNow();
            _abilitySlots.Refresh(slots, _player.BodyColor, FansOf(slots));
        }
        Breathe(_divideButton, _divideShownAt, 0f);
        Breathe(_organismButton, _organismShownAt, 2.6f);
    }

    private PackEncounter _encounter;

    /// <summary>The encounters of pack and tribe: from the pack on the slots show the three ways.</summary>
    /// <param name="encounter">(Required) The encounters with foreign packs and tribes.</param>
    public void BindWays(PackEncounter encounter)
    {
        _encounter = encounter;
    }

    /*
      The slots of this frame:
      > A living cell: its key abilities (AbilitySet.Slots), the disc in the diet class of the part
      > A creature: from the pack on court (herbivore), display (omnivore), hunt (carnivore), dim
        without a stranger close by (hunt stays lit in the tribe, it tears down huts); then the
        ability parts of its body plan in the color of their tab
    */
    private List<AbilitySlots.Slot> SlotsNow()
    {
        var slots = new List<AbilitySlots.Slot>();
        if (_player.IsHidden || _player.IsDead)
        {
            return slots;
        }
        if (!_player.IsOrganism)
        {
            foreach (PartKind part in _player.Abilities.Slots)
            {
                slots.Add(new AbilitySlots.Slot(part, null, null, PartCatalog.Get(part).Diet, _player.Abilities.CooldownShare(part), _player.Abilities.IsRunning(part), IsDim: false));
            }
            return slots;
        }
        if (_encounter is { IsActive: true })
        {
            foreach (PackWay way in System.Enum.GetValues<PackWay>())
            {
                bool isDim = null == _encounter.Target && !(PackWay.Hunt == way && _player.IsTribal);
                slots.Add(new AbilitySlots.Slot(null, way, null, TribeWays.DietOf(way), _encounter.CooldownShare(way), IsRunning: false, isDim));
            }
        }
        if (_encounter is { IsActive: true })
        {
            return slots;     // from the pack on the ability parts sit in the fans of the ways (FansOf)
        }
        CreatureAbilitySet abilities = _player.CreatureAbilities;
        foreach (CreaturePartKind part in abilities.Slots)
        {
            slots.Add(new AbilitySlots.Slot(null, null, part, CreatureAbilitySet.DietOf(part), abilities.CooldownShare(part), abilities.IsRunning(part), IsDim: false));
        }
        return slots;
    }

    /*
      The fans of the ways (wish of the user 2026-11-17): from the pack on every way opens a fan
      of the ability parts of the creature in the color of that way (court gentle, display
      clever, hunt wild), the way itself is not in it (its key pressed again uses it). A way
      without such parts and every other slot fire at once.
    */
    private List<IReadOnlyList<AbilitySlots.Slot>> _fans = new();

    private List<IReadOnlyList<AbilitySlots.Slot>> FansOf(List<AbilitySlots.Slot> slots)
    {
        var fans = new List<IReadOnlyList<AbilitySlots.Slot>>();
        _fans = fans;
        CreatureAbilitySet abilities = _player.CreatureAbilities;
        foreach (AbilitySlots.Slot slot in slots)
        {
            if (slot.Way is not PackWay way)
            {
                fans.Add(null);
                continue;
            }
            var fan = new List<AbilitySlots.Slot>();
            foreach (CreaturePartKind part in abilities.Slots.Where(part => CreatureAbilitySet.DietOf(part) == TribeWays.DietOf(way)))
            {
                fan.Add(new AbilitySlots.Slot(null, null, part, slot.Diet, abilities.CooldownShare(part), abilities.IsRunning(part), IsDim: false));
            }
            fans.Add(0 == fan.Count ? null : fan);
        }
        return fans;
    }

    /// <summary>The key of a way (1, 2 or 3): opens its fan, the same key again uses the way.</summary>
    /// <returns>False when no way slot shows, the key goes on to the encounter then.</returns>
    public bool PressWayKey(int way)
    {
        return _abilitySlots.Visible && _abilitySlots.PressKey(way);
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

        // The key slots of the cell abilities in an arc around the dial
        _abilitySlots = new AbilitySlots();
        _abilitySlots.SlotPressed += index => EmitSignal(SignalName.AbilityPressed, index);
        _abilitySlots.FanPressed  += (slot, item) =>
        {
            // The way itself, or a part of its fan
            if (slot >= _fans.Count || null == _fans[slot] || item >= _fans[slot].Count)
            {
                return;
            }
            AbilitySlots.Slot chosen = _fans[slot][item];
            EmitSignal(SignalName.AbilityFanPressed, slot, chosen.Creature is CreaturePartKind part ? (int)part : -1);   // -1 never comes from a fan of parts, kept for the way itself
        };
        AddChild(_abilitySlots);
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
