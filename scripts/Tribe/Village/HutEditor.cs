// NOTE:
//   The hut designer of the tribe: a panel over the strategy view with a live 3D
//   preview of the blueprint (HutVisual3D in its own studio), the wall and roof choice as
//   tabs, the add-ons as toggles, the costs in food and wood and the effects below.
//   "Apply" hands the design to the TribeCommander (Applied), every hut built from then
//   on uses it. The game keeps running behind it. Created by Main.
//
//   var editor = new HutEditor();
//   editor.Applied += design => tribe.Design.CopyFrom(design);
//   AddChild(editor);
//   editor.Open(tribe.Design, tribe.Economy);   // B or the panel button

//
//   Chapters:
//     STATE AND API   Open, close, the draft and the signals.
//     BUILD UI        Panel, preview, choice rows, cost and effect lines, buttons.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class HutEditor : CanvasLayer
{
    // ===========================================================================
    // ============================   STATE AND API   ============================
    // ===========================================================================
    //
    //   Applied    The player applied the draft, the tribe builds with it from now on (a C# event: a HutDesign is no Variant).
    //   IsOpen     Whether the panel shows.
    //   Draft      The blueprint being edited.
    //   Open       Shows the panel with a copy of a design and the stock to compare with.
    //   Close      Hides the panel.
    //   Apply      Emits Applied with the draft and closes.
    //   _UnhandledInput   Escape closes.
    //

    public event System.Action<HutDesign> Applied;

    public bool IsOpen => _panel.Visible;

    public HutDesign Draft { get; } = new();

    private TribeEconomy _economy;

    /// <summary>Shows the designer with a copy of a design.</summary>
    /// <param name="design">(Required) The current blueprint of the tribe, copied into the draft.</param>
    /// <param name="economy">(Required) The stock, the cost line shows what is affordable.</param>
    public void Open(HutDesign design, TribeEconomy economy)
    {
        Draft.CopyFrom(design);
        _economy       = economy;
        _panel.Visible = true;
        Refresh();
        Log.Verbose("HutEditor", "Opened.");
    }

    public void Close()
    {
        _panel.Visible = false;
    }

    /// <summary>Hands the draft to the tribe and closes.</summary>
    public void Apply()
    {
        Applied?.Invoke(Draft);
        Log.Verbose("HutEditor", $"Applied: {Draft.Wall}, {Draft.Roof}, {Draft.Addons.Count} add-on(s).");
        Close();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsOpen && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   _Ready         Panel in the middle: preview left, choices right, lines and buttons below.
    //   NewPreview     The studio with the hut, slowly turning.
    //   NewChoiceRow   A label and one tab button per option.
    //   Refresh        Rebuilds the preview, marks the chosen options, writes cost and effects.
    //   _Notification  Re-translates after a locale change.
    //

    private const int PREVIEW_PIXELS = 300;
    private const float PREVIEW_DISTANCE = 8f;       // the palisade ring and the watchtower must fit
    private static readonly Color ACCENT = new(0.8f, 0.6f, 0.4f);

    private PanelContainer _panel;
    private Label _title;
    private HutVisual3D _preview;
    private Label _costLine;
    private Label _effectLine;
    private Button _apply;
    private Button _close;
    private readonly Dictionary<WallKind, Button> _wallButtons = new();
    private readonly Dictionary<RoofKind, Button> _roofButtons = new();
    private readonly Dictionary<HutAddon, Button> _addonButtons = new();
    private Label _wallLabel;
    private Label _roofLabel;
    private Label _addonLabel;

    public override void _Ready()
    {
        Layer = 20;
        _panel = new PanelContainer { Visible = false };
        _panel.AddThemeStyleboxOverride("panel", EditorStyle.Pill(new Color(0.05f, 0.08f, 0.12f, 0.94f), ACCENT, 0.4f));
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.GrowVertical   = Control.GrowDirection.Both;
        AddChild(_panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(column);
        _title = NewLabel(26);
        GameFonts.UseDisplay(_title);
        column.AddChild(_title);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        column.AddChild(row);
        row.AddChild(NewPreview());

        var choices = new VBoxContainer();
        choices.AddThemeConstantOverride("separation", 8);
        row.AddChild(choices);
        _wallLabel  = NewChoiceRow(choices, System.Enum.GetValues<WallKind>(), _wallButtons, wall => { Draft.Wall = wall; Refresh(); });
        _roofLabel  = NewChoiceRow(choices, System.Enum.GetValues<RoofKind>(), _roofButtons, roof => { Draft.Roof = roof; Refresh(); });
        _addonLabel = NewChoiceRow(choices, System.Enum.GetValues<HutAddon>(), _addonButtons, addon => { Draft.Toggle(addon); Refresh(); });

        _costLine   = NewLabel(16);
        _effectLine = NewLabel(14);
        column.AddChild(_costLine);
        column.AddChild(_effectLine);

        var buttons = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        buttons.AddThemeConstantOverride("separation", 14);
        column.AddChild(buttons);
        _apply = NewButton(ACCENT);
        _apply.Pressed += Apply;
        buttons.AddChild(_apply);
        _close = NewButton(EditorStyle.RIM_COLOR);
        _close.Pressed += Close;
        buttons.AddChild(_close);
        ApplyTexts();
    }

    /// <summary>The studio with the hut in it, turning slowly.</summary>
    private Control NewPreview()
    {
        var container = new SubViewportContainer
        {
            Stretch           = true,
            CustomMinimumSize = new Vector2(PREVIEW_PIXELS, PREVIEW_PIXELS),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        SubViewport viewport = PartIcon.NewStudio(new Vector2I(PREVIEW_PIXELS, PREVIEW_PIXELS));
        container.AddChild(viewport);
        _preview = new HutVisual3D();
        viewport.AddChild(_preview);
        Tween spin = _preview.CreateTween().SetLoops();
        spin.TweenProperty(_preview, "rotation:y", Mathf.Tau, 14f).From(0f);

        Vector3 offset = new Vector3(0.9f, 0.75f, 1f).Normalized() * PREVIEW_DISTANCE;
        var camera = new Camera3D { Fov = 40f, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), offset + Vector3.Up * 1.1f);
        viewport.AddChild(camera);
        return container;
    }

    /// <summary>A label and one tab button per option.</summary>
    /// <returns>The label, re-translated by ApplyTexts.</returns>
    private Label NewChoiceRow<TOption>(VBoxContainer parent, TOption[] options, Dictionary<TOption, Button> buttons, System.Action<TOption> onPressed)
    {
        Label label = NewLabel(15);
        parent.AddChild(label);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        parent.AddChild(row);
        foreach (TOption option in options)
        {
            var button = new Button
            {
                FocusMode         = Control.FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                ToggleMode        = true,
                CustomMinimumSize = new Vector2(104, 34),
            };
            button.AddThemeFontSizeOverride("font_size", 14);
            EditorStyle.ApplyTab(button, ACCENT);
            TOption chosen = option;
            button.Pressed += () => onPressed(chosen);
            row.AddChild(button);
            buttons[option] = button;
        }
        return label;
    }

    /// <summary>Preview, pressed states, cost and effect lines from the draft.</summary>
    private void Refresh()
    {
        _preview.Build(Draft);
        foreach ((WallKind wall, Button button) in _wallButtons)
        {
            button.SetPressedNoSignal(wall == Draft.Wall);
        }
        foreach ((RoofKind roof, Button button) in _roofButtons)
        {
            button.SetPressedNoSignal(roof == Draft.Roof);
        }
        foreach ((HutAddon addon, Button button) in _addonButtons)
        {
            button.SetPressedNoSignal(Draft.Has(addon));
        }
        bool isAffordable = null == _economy || _economy.CanAfford(Draft);
        _costLine.Text = string.Format(Tr("HUT_COST"), Draft.FoodCost, Draft.WoodCost) + (isAffordable ? "" : " " + Tr("HUT_TOO_EXPENSIVE"));
        _effectLine.Text = string.Format(Tr("HUT_EFFECTS"), (int)Draft.Health, Draft.Villagers, (int)Draft.Defense, (int)(Draft.TradeBonus * 100f), Draft.FoodPerFruit);
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
        _title.Text      = Tr("HUT_EDITOR_TITLE");
        _wallLabel.Text  = Tr("HUT_WALL");
        _roofLabel.Text  = Tr("HUT_ROOF");
        _addonLabel.Text = Tr("HUT_ADDONS");
        _apply.Text      = Tr("HUT_APPLY");
        _close.Text      = Tr("HUT_CLOSE");
        foreach ((WallKind wall, Button button) in _wallButtons)
        {
            button.Text = Tr($"WALL_{wall}".ToUpperInvariant());
        }
        foreach ((RoofKind roof, Button button) in _roofButtons)
        {
            button.Text = Tr($"ROOF_{roof}".ToUpperInvariant());
        }
        foreach ((HutAddon addon, Button button) in _addonButtons)
        {
            button.Text = Tr($"ADDON_{addon}".ToUpperInvariant());
        }
        if (IsOpen)
        {
            Refresh();
        }
    }

    private static Button NewButton(Color accent)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(180, 44),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        EditorStyle.ApplyButton(button, accent);
        return button;
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(3, fontSize / 5));
        return label;
    }
}
