// NOTE:
//   One planet up close, over the darkened galaxy of the title menu: its big turning globe
//   on the left in its star system (Menu/StarSystemView.cs, the neighbors cannot be
//   chosen yet), on the right its name, what makes it special (water, islands, climate),
//   the start phases (the RNA world always, later phases once reached there, Core/
//   PlanetProgress.cs) and the start button. Back returns to the galaxy. Fills its parent.
//
//   var view = new PlanetView();
//   view.StartPressed += phase => startFlow.Start(phase);
//   view.BackPressed  += () => galaxy.Unfocus();
//   AddChild(view);
//   view.Show(planet);

//
//   Chapters:
//     SIGNALS   Start and back.
//     SHOW      Fills the view with a planet, picks its furthest phase.
//     BUILD     Globe, info panel, phase buttons, start and back buttons.
//     MOONS AND DIVE   Moons around the globe, the dive into it at the start.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class PlanetView : Control
{
    // ===========================================================================
    // ===============================   SIGNALS   ===============================
    // ===========================================================================
    //
    //   StartPressed   Start on the shown planet in the chosen phase (C# events: an enum is kept as it is).
    //   ContinuePressed   Continue the saved game of the planet (Core/Save/SaveSlots.cs), shown only with a save.
    //   BackPressed    Back to the galaxy.
    //   Start          Starts with the chosen phase, also from the keyboard (TitleMenu).
    //

    public event System.Action<StartPhase> StartPressed;
    public event System.Action<SaveGame> ContinuePressed;
    public event System.Action BackPressed;

    /// <summary>Starts with the chosen phase.</summary>
    public void Start()
    {
        StartPressed?.Invoke(_phase);
    }

    // ===========================================================================
    // ================================   SHOW   =================================
    // ===========================================================================
    //
    //   Planet      The planet shown, null before the first Show.
    //   Show        Name, text, traits and phases of a planet, the furthest open phase picked.
    //   Refresh     Reads the reached phase again (F10 in the menu).
    //   PickPhase   Marks a phase as the one to start in.
    //   Translate   Every text in the current language.
    //

    public Planet Planet { get; private set; }

    private StartPhase _phase;
    private StartPhase _reached;

    /// <summary>Fills the view with a planet and fades it in.</summary>
    /// <param name="planet">(Required) The planet to show.</param>
    public void Show(Planet planet)
    {
        Planet   = planet;
        _reached = PlanetProgress.Reached(planet.Key);
        _save    = SaveSlots.Read(planet.Key);
        _continueButton.Visible = null != _save;
        _globe.Show(planet);
        _globe.Scale = Vector2.One;
        _panel.Modulate = _backButton.Modulate = Colors.White;
        _system.Show(planet, GLOBE_CENTER);
        ShowMoons(StarSystem.Of(planet));
        PickPhase(StartPhase.RnaWorld);
        Translate();
        Visible  = true;
        Modulate = new Color(1f, 1f, 1f, 0f);
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenInterval(0.35f);
        fade.TweenProperty(this, "modulate:a", 1f, 0.4f);
        EditorStyle.PopIn(_globe, 0.35f);
    }

    /// <summary>Reads what was reached again (F10 opens every phase) and redraws the phases.</summary>
    public void Refresh()
    {
        _reached = PlanetProgress.Reached(Planet.Key);
        PickPhase(_phase);
        Translate();
        foreach (Button button in _phaseButtons)
        {
            button.QueueRedraw();
        }
    }

    /// <summary>Marks the phase to start in, a closed one is ignored.</summary>
    public void PickPhase(StartPhase phase)
    {
        if (!StartPhases.IsOpen(phase, _reached))
        {
            return;
        }
        _phase = phase;
        for (int i = 0; i < _phaseButtons.Count; i++)
        {
            var  each   = (StartPhase)i;
            bool isOpen = StartPhases.IsOpen(each, _reached);
            _phaseButtons[i].Disabled = !isOpen;
            _phaseButtons[i].SetPressedNoSignal(each == _phase);
        }
    }

    public void Translate()
    {
        if (null == Planet)
        {
            return;
        }
        _name.Text  = Tr($"PLANET_{Planet.Key}");
        _text.Text  = Tr($"PLANET_{Planet.Key}_DESC");
        int water   = Mathf.RoundToInt((1f - Planet.LandShare) * 100f);
        _traits.Text = string.Format(Tr("MENU_PLANET_TRAITS"), water, Planet.Islands + 1, StarSystem.Of(Planet).Neighbors.Count);
        _phaseTitle.Text = Tr("MENU_PHASE_TITLE");
        for (int i = 0; i < _phaseButtons.Count; i++)
        {
            var phase = (StartPhase)i;
            _phaseButtons[i].Text        = Tr(StartPhases.NameKey(phase));
            _phaseButtons[i].TooltipText = StartPhases.IsOpen(phase, _reached) ? "" : Tr("MENU_PHASE_LOCKED");
        }
        _startButton.Text = Tr(null == _save ? "MENU_START" : "MENU_NEW_GAME");
        if (null != _save)
        {
            string when = System.DateTime.TryParse(_save.Meta.SavedAt, out System.DateTime saved) ? saved.ToString("dd.MM. HH:mm") : "";
            _continueButton.Text = string.Format(Tr("MENU_CONTINUE"), Tr(Player.STAGE_KEYS[Mathf.Clamp(_save.Meta.Stage, 0, Player.STAGE_KEYS.Length - 1)]), when);
        }
        _backButton.Text  = Tr("MENU_BACK");
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready         Globe left, panel right, back button bottom left.
    //   NewPhaseButton A toggle tile for one phase, a lock drawn on closed ones.
    //

    private const float GLOBE_SIZE = 105f;
    private static readonly Vector2 GLOBE_CENTER = new(0.28f, 0.5f);   // middle of the left part (AnchorRight 0.56), share of the screen
    private const float PANEL_WIDTH = 470f;
    private const float GLOBE_SHARE = 0.8f;          // shaders/planet_globe.gdshader radius: the globe fills this much of its rect
    private static readonly Color START_ACCENT = new(0.55f, 0.85f, 0.95f);
    private static readonly Color PHASE_ACCENT = new(0.75f, 0.7f, 0.95f);

    private readonly List<Button> _phaseButtons = new();
    private PlanetGlobe _globe;
    private PanelContainer _panel;
    private StarSystemView _system;
    private Label _name;
    private Label _text;
    private Label _traits;
    private Label _phaseTitle;
    private Button _startButton;
    private Button _continueButton;
    private SaveGame _save;
    private static readonly Color CONTINUE_ACCENT = new(0.6f, 0.9f, 0.6f);
    private Button _backButton;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible     = false;

        // The star system behind it, the globe sits on its home orbit
        _system = new StarSystemView();
        AddChild(_system);

        // The globe, centered in the left part of the screen
        var left = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 0.56f, AnchorBottom = 1f };
        AddChild(left);
        _left = left;
        _globe = new PlanetGlobe { CustomMinimumSize = Vector2.One * GLOBE_SIZE, PivotOffset = Vector2.One * GLOBE_SIZE * 0.5f, Spin = 0.18f, Glow = 1f };
        left.AddChild(_globe);

        // The panel on the right: name, text, traits, phases, start
        var panel = new PanelContainer { AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 0.5f, AnchorBottom = 0.5f };
        panel.OffsetLeft   = -PANEL_WIDTH - 40f;
        panel.OffsetRight  = -40f;
        panel.GrowVertical = GrowDirection.Both;
        var box = new StyleBoxFlat { BgColor = new Color(0.04f, 0.05f, 0.1f, 0.82f), BorderColor = new Color(0.55f, 0.6f, 0.9f, 0.5f), AntiAliasing = true };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(22);
        box.SetContentMarginAll(24);
        panel.AddThemeStyleboxOverride("panel", box);
        AddChild(panel);
        _panel = panel;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);

        _name = NewLabel(52);
        GameFonts.UseDisplay(_name);
        column.AddChild(_name);
        _text = NewLabel(17);
        _text.AutowrapMode      = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(PANEL_WIDTH - 50f, 0f);
        column.AddChild(_text);
        _traits = NewLabel(15);
        _traits.Modulate          = new Color(0.75f, 0.85f, 1f);
        _traits.AutowrapMode      = TextServer.AutowrapMode.WordSmart;
        _traits.CustomMinimumSize = new Vector2(PANEL_WIDTH - 50f, 0f);
        column.AddChild(_traits);

        _phaseTitle = NewLabel(20);
        column.AddChild(_phaseTitle);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(grid);
        for (int i = 0; i < StartPhases.COUNT; i++)
        {
            grid.AddChild(NewPhaseButton((StartPhase)i));
        }

        var startRow = new CenterContainer { CustomMinimumSize = new Vector2(0f, 76f) };
        column.AddChild(startRow);
        _startButton = new Button { CustomMinimumSize = new Vector2(260, 58), FocusMode = FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled, PivotOffset = new Vector2(130, 29) };
        _startButton.AddThemeFontSizeOverride("font_size", 24);
        EditorStyle.ApplyButton(_startButton, START_ACCENT);
        _startButton.Pressed      += Start;
        _startButton.MouseEntered += () => EditorStyle.ScaleTo(_startButton, 1.06f);
        _startButton.MouseExited  += () => EditorStyle.ScaleTo(_startButton, 1f);
        _continueButton = new Button { CustomMinimumSize = new Vector2(PANEL_WIDTH - 60f, 52), FocusMode = FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled, Visible = false };
        _continueButton.AddThemeFontSizeOverride("font_size", 18);
        EditorStyle.ApplyButton(_continueButton, CONTINUE_ACCENT);
        _continueButton.Pressed += () => ContinuePressed?.Invoke(_save);
        column.AddChild(_continueButton);
        startRow.AddChild(_startButton);

        // Back to the galaxy, bottom left
        _backButton = new Button { FocusMode = FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(150, 44) };
        _backButton.AddThemeFontSizeOverride("font_size", 16);
        EditorStyle.ApplyButton(_backButton, PHASE_ACCENT);
        _backButton.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        _backButton.Position += new Vector2(24f, -68f);
        _backButton.Pressed  += () => BackPressed?.Invoke();
        AddChild(_backButton);
    }

    private Button NewPhaseButton(StartPhase phase)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2((PANEL_WIDTH - 58f) * 0.5f, 40),
            FocusMode         = FocusModeEnum.None,
            ToggleMode        = true,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", 15);
        EditorStyle.ApplyTile(button, PHASE_ACCENT);
        button.Pressed += () => PickPhase(phase);
        // A small padlock on the phases not reached yet
        button.Draw += () =>
        {
            if (!button.Disabled)
            {
                return;
            }
            var at    = new Vector2(button.Size.X - 22f, button.Size.Y * 0.5f);
            var color = new Color(1f, 1f, 1f, 0.55f);
            button.DrawArc(at + new Vector2(0f, -3f), 5f, Mathf.Pi, Mathf.Tau, 10, color, 2f, true);
            button.DrawRect(new Rect2(at + new Vector2(-7f, -3f), new Vector2(14f, 10f)), color);
        };
        _phaseButtons.Add(button);
        return button;
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 6));
        return label;
    }

    // ===========================================================================
    // ============================   MOONS AND DIVE   ===========================
    // ===========================================================================
    //
    //   ShowMoons   A small globe per moon of the planet (Core/StarSystem.cs Moons).
    //   _Process    Light from the star on planet and moons; the moons circle the globe on a
    //               tilted path, in front of it on the near half and behind it on the far half.
    //   Dive        Start: the globe rushes towards the camera while the menu fades.
    //

    private const float MOON_TILT = 0.3f;            // the moon paths seen from a little above
    private const float MOON_MIN_PIXELS = 16f;       // a moon stays visible next to the small globe
    private const float DIVE_SCALE = 22f;
    private const float DIVE_SECONDS = 0.55f;

    private readonly List<(StarSystem.Moon Moon, PlanetGlobe Globe)> _moons = new();
    private Control _left;
    private float _moonTime;

    private void ShowMoons(StarSystem system)
    {
        foreach ((StarSystem.Moon _, PlanetGlobe globe) in _moons)
        {
            globe.QueueFree();
        }
        _moons.Clear();
        foreach (StarSystem.Moon moon in system.Moons)
        {
            var globe = new PlanetGlobe { Size = Vector2.One * Mathf.Max(GLOBE_SIZE * moon.Size, MOON_MIN_PIXELS), Glow = 0.15f, Spin = 0.05f };
            globe.Show(moon.Look);
            AddChild(globe);
            _moons.Add((moon, globe));
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }
        // Planet and moons are lit from where the star stands now (StarSystemView, the camera rides along the orbit)
        Vector3 light = _system.HomeLight;
        _globe.Light  = light;
        _moonTime    += (float)delta;
        Vector2 middle = GLOBE_CENTER * Size;
        float   radius = GLOBE_SIZE * 0.5f * GLOBE_SHARE * _globe.Scale.X;
        foreach ((StarSystem.Moon moon, PlanetGlobe globe) in _moons)
        {
            float angle   = moon.Start + _moonTime * moon.Speed;
            bool  isFront = Mathf.Sin(angle) > 0f;
            globe.Position = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * MOON_TILT) * radius * moon.Distance - globe.Size * 0.5f;
            globe.Scale    = _globe.Scale;
            globe.Light    = light;
            // Moving onto the index of the globe column puts the moon on its other side
            bool isAhead = globe.GetIndex() > _left.GetIndex();
            if (isFront != isAhead)
            {
                MoveChild(globe, _left.GetIndex());
            }
        }
    }

    /// <summary>The globe rushes towards the camera and the panel fades: the start of a game.</summary>
    public void Dive()
    {
        Tween dive = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel()
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        dive.TweenProperty(_globe, "scale", Vector2.One * DIVE_SCALE, DIVE_SECONDS);
        foreach (Control panel in new Control[] { _panel, _backButton })
        {
            dive.TweenProperty(panel, "modulate:a", 0f, DIVE_SECONDS * 0.5f);
        }
    }
}
