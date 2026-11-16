// NOTE:
//   The tool column right of the turntable of the creature editor, from top to bottom:
//   an icon each for color, skin surface and paint pattern (a click unfolds the
//   choices, so the column stays short; the colors come with one icon per color zone), the spray can with undo
//   and wipe, the symmetry switch and - and + for vertebrae. A view: it only reports
//   clicks (events) and shows the state of the session (Refresh).
//
//   var toolbar = new CreatureToolbar();
//   toolbar.Setup(session);
//   toolbar.ColorPicked += PickColor;
//   frame.AddWorkbench(palette, stage, toolbar);

//
//   Chapters:
//     BUILD TOOLBAR     Color swatches, pattern pills, spray can, symmetry and vertebra buttons.
//     REFRESH TOOLBAR   Texts, pressed and picked states.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CreatureToolbar : PanelContainer
{
    // ===========================================================================
    // ============================   BUILD TOOLBAR   ============================
    // ===========================================================================
    //
    //   AddVertebraPressed      + was clicked.
    //   RemoveVertebraPressed   - was clicked.
    //   SymmetryToggled         The axis icon was clicked (isSymmetric: mirror new parts or single halves).
    //   RadialPicked            The ring icon was clicked (the new count of the point symmetry).
    //   SprayToggled            The spray can was switched (isSpraying).
    //   UndoSprayPressed        Undo of the spray can was clicked.
    //   ClearSprayPressed       Wipe of the spray can was clicked.
    //   ColorPicked             A body color was clicked (null = coral default).
    //   PatternPicked           A pattern pill was clicked.
    //   SkinPicked              A skin icon was clicked.
    //   ZoneColorPicked         A color was clicked for a zone other than the body (null = no color of its own).
    //   SprayColorPicked        A color of the spray can was clicked (null = dark shade of the body).
    //   PostureChanged          The posture slider moved (degrees, 0 level, 90 upright).
    //   HoverChanged            The hover slider moved (body sizes above the ground on land).
    //   FlatnessChanged         The flatness slider moved (-1 tall and narrow .. 1 flat and wide).
    //   PatternColorPicked      A second color was clicked (null = dark shade of the body).
    //   Setup                   The panel and its sections from top to bottom.
    //   BuildColorRows          The color and skin icons with their choices, pattern pills and the second color.
    //   Open                    Unfolds the choices of one icon, folds the other.
    //   BuildSprayButtons       Spray can with its own colors, undo and wipe.
    //   BuildSculptButtons      The symmetry switch and - and + for vertebrae.
    //   AddSliderRow            A label and its slider side by side.
    //   NewSeparator            A thin line between two sections.
    //

    private const float TOOLBAR_WIDTH = 250f;
    private const float SLIDER_LABEL_WIDTH = 118f;   // posture and hover labels, their sliders take the rest of the row

    public event System.Action AddVertebraPressed;
    public event System.Action RemoveVertebraPressed;
    public event System.Action<bool> SymmetryToggled;
    public event System.Action<int> RadialPicked;
    public event System.Action<bool> SprayToggled;
    public event System.Action UndoSprayPressed;
    public event System.Action ClearSprayPressed;
    public event System.Action<Color?> ColorPicked;
    public event System.Action<CreaturePattern> PatternPicked;
    public event System.Action<CreatureSkin> SkinPicked;
    public event System.Action<CreatureZone, Color?> ZoneColorPicked;
    public event System.Action<Color?> SprayColorPicked;
    public event System.Action<float> PostureChanged;
    public event System.Action<float> HoverChanged;
    public event System.Action<float> FlatnessChanged;
    public event System.Action<Color?> PatternColorPicked;

    private CreatureEditorSession _session;
    private VBoxContainer _sections;
    private Button _addButton;
    private Button _removeButton;
    private SymmetryIcon _axisIcon;
    private SymmetryIcon _ringIcon;
    private Label _symmetryLabel;
    private Button _sprayButton;
    private Button _undoSprayButton;
    private Button _clearSprayButton;
    private HFlowContainer _swatchRow;
    private readonly List<(Color? Color, Button Swatch)> _swatches = new();
    private HFlowContainer _patternRow;
    private HFlowContainer _paintRow;
    private readonly List<(CreaturePattern Pattern, Button Button)> _patternButtons = new();
    private Label _flatLabel;
    private HSlider _flatSlider;
    private Label _hoverLabel;
    private HSlider _hoverSlider;
    private Label _postureLabel;
    private HSlider _postureSlider;
    private Button _colorIcon;
    private SkinIcon _skinIcon;
    private SkinIcon _patternIcon;
    private Label _iconLabel;
    private HFlowContainer _skinRow;
    private HFlowContainer _sprayRow;
    private readonly List<(Color? Color, Button Swatch)> _spraySwatches = new();
    private HFlowContainer _zoneRow;
    private readonly List<ZoneIcon> _zoneIcons = new();
    private CreatureZone _zone = CreatureZone.Body;      // the zone the swatches paint
    private readonly List<SkinIcon> _skinTiles = new();
    private readonly List<(Color? Color, Button Swatch)> _patternSwatches = new();

    /// <summary>Builds the column, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting whose modes and paint the buttons show.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session          = session;
        CustomMinimumSize = new Vector2(TOOLBAR_WIDTH, 0f);
        StyleBoxFlat style = EditorStyle.Pill(new Color(0.04f, 0.09f, 0.13f, 0.82f), new Color(EditorStyle.RIM_COLOR, 0.45f));
        style.SetContentMarginAll(14);
        AddThemeStyleboxOverride("panel", style);

        _sections = new VBoxContainer();
        _sections.AddThemeConstantOverride("separation", 9);
        AddChild(_sections);
        BuildColorRows();
        _sections.AddChild(NewSeparator());
        BuildSprayButtons();
        _sections.AddChild(NewSeparator());
        BuildSculptButtons();
    }

    private void BuildColorRows()
    {
        // Three icons on top: the body color, the skin surface and the paint pattern. A click opens its choices below,
        // one at a time, a second click folds them away: the column stays short
        var icons = new HBoxContainer();
        icons.AddThemeConstantOverride("separation", 12);
        _sections.AddChild(icons);
        _colorIcon = new Button { CustomMinimumSize = new Vector2(44, 44), FocusMode = FocusModeEnum.None };
        _colorIcon.Pressed += () => Open(_swatchRow);
        _skinIcon = new SkinIcon { CustomMinimumSize = new Vector2(44, 44) };
        _skinIcon.Pressed += () => Open(_skinRow);
        _patternIcon = new SkinIcon { CustomMinimumSize = new Vector2(44, 44) };
        _patternIcon.Pressed += () => Open(_patternRow);
        _iconLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, ClipText = true };
        _iconLabel.AddThemeFontSizeOverride("font_size", 13);
        icons.AddChild(_colorIcon);
        icons.AddChild(_skinIcon);
        icons.AddChild(_patternIcon);
        icons.AddChild(_iconLabel);

        // Body colors: coral default first, translucent last, wrapping into rows
        _swatchRow = new HFlowContainer { Visible = false };
        _swatchRow.AddThemeConstantOverride("h_separation", 8);
        _swatchRow.AddThemeConstantOverride("v_separation", 8);
        _sections.AddChild(_swatchRow);
        var swatchColors = new List<Color?> { null };
        foreach (Color color in EditorStyle.BODY_SWATCHES)
        {
            swatchColors.Add(color);
        }
        foreach (Color? swatchColor in swatchColors)
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(30, 30), FocusMode = FocusModeEnum.None };
            // The swatches paint the picked zone; a see-through color only fits the whole body
            swatch.Pressed      += () =>
            {
                if (CreatureZone.Body == _zone)
                {
                    ColorPicked?.Invoke(swatchColor);
                }
                else if (null == swatchColor || swatchColor.Value.A >= 0.99f)
                {
                    ZoneColorPicked?.Invoke(_zone, swatchColor);
                }
            };
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            _swatchRow.AddChild(swatch);
            _swatches.Add((swatchColor, swatch));
        }

        // Color zones: one icon per zone (body, belly, head, limbs, tail end). The picked one takes the swatches above:
        // their first swatch is the default for the body and "no color of its own" for every other zone
        _zoneRow = new HFlowContainer { Visible = false };
        _zoneRow.AddThemeConstantOverride("h_separation", 4);
        _zoneRow.AddThemeConstantOverride("v_separation", 4);
        _sections.AddChild(_zoneRow);
        foreach (CreatureZone zone in System.Enum.GetValues<CreatureZone>())
        {
            var icon = new ZoneIcon { Zone = zone, CustomMinimumSize = new Vector2(40, 32) };
            icon.Pressed += () =>
            {
                _zone = zone;
                Refresh();
            };
            _zoneRow.AddChild(icon);
            _zoneIcons.Add(icon);
        }

        // Skin: one icon per surface (smooth, scales, warts), drawn in the body color
        _skinRow = new HFlowContainer { Visible = false };
        _skinRow.AddThemeConstantOverride("h_separation", 10);
        _skinRow.AddThemeConstantOverride("v_separation", 8);
        _sections.AddChild(_skinRow);
        foreach (CreatureSkin skin in System.Enum.GetValues<CreatureSkin>())
        {
            var tile = new SkinIcon { Skin = skin, CustomMinimumSize = new Vector2(40, 40) };
            tile.Pressed += () => SkinPicked?.Invoke(skin);
            _skinRow.AddChild(tile);
            _skinTiles.Add(tile);
        }
        _sections.AddChild(NewSeparator());

        // Paint: one pill per pattern, under them the second color (opaque swatches, dark shade first)
        _patternRow = new HFlowContainer { Visible = false };
        _patternRow.AddThemeConstantOverride("h_separation", 6);
        _patternRow.AddThemeConstantOverride("v_separation", 6);
        _sections.AddChild(_patternRow);
        foreach (CreaturePattern pattern in System.Enum.GetValues<CreaturePattern>())
        {
            Button pill = CreatureEditorUi.NewPillButton(new Vector2(0, 30), 14);
            pill.Pressed += () => PatternPicked?.Invoke(pattern);
            _patternRow.AddChild(pill);
            _patternButtons.Add((pattern, pill));
        }
        _paintRow = new HFlowContainer();
        _paintRow.AddThemeConstantOverride("h_separation", 6);
        _paintRow.AddThemeConstantOverride("v_separation", 6);
        _sections.AddChild(_paintRow);
        var paintColors = new List<Color?> { null };
        foreach (Color color in EditorStyle.BODY_SWATCHES.Where(color => color.A >= 0.99f))
        {
            paintColors.Add(color);
        }
        foreach (Color? paintColor in paintColors)
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(24, 24), FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            swatch.Pressed      += () => PatternColorPicked?.Invoke(paintColor);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            _paintRow.AddChild(swatch);
            _patternSwatches.Add((paintColor, swatch));
        }
    }

    /// <summary>Opens the choices of one icon and folds the other away, a click on the open one folds it too.</summary>
    private void Open(Control row)
    {
        bool wasOpen = row.Visible;
        _swatchRow.Visible  = false;
        _zoneRow.Visible    = false;
        _skinRow.Visible    = false;
        _patternRow.Visible = false;
        row.Visible        = !wasOpen;
        _zoneRow.Visible   = _swatchRow.Visible;      // the zones belong to the colors
        Refresh();
    }

    private void BuildSprayButtons()
    {
        // Undo and wipe only show while the spray can is on (Refresh)
        _sprayButton            = CreatureEditorUi.NewPillButton(new Vector2(0, 38), 16);
        _sprayButton.ToggleMode = true;
        _sprayButton.Toggled   += isSpraying => SprayToggled?.Invoke(isSpraying);
        _undoSprayButton        = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _undoSprayButton.Pressed += () => UndoSprayPressed?.Invoke();
        _clearSprayButton       = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _clearSprayButton.Pressed += () => ClearSprayPressed?.Invoke();
        EditorStyle.ApplyButton(_sprayButton, EditorStyle.RIM_COLOR);
        _sections.AddChild(_sprayButton);

        // The colors of the can, its own choice apart from the pattern: only while the can is on (Refresh)
        _sprayRow = new HFlowContainer { Visible = false };
        _sprayRow.AddThemeConstantOverride("h_separation", 6);
        _sprayRow.AddThemeConstantOverride("v_separation", 6);
        _sections.AddChild(_sprayRow);
        var sprayColors = new List<Color?> { null };
        foreach (Color color in EditorStyle.BODY_SWATCHES.Where(color => color.A >= 0.99f))
        {
            sprayColors.Add(color);
        }
        foreach (Color? sprayColor in sprayColors)
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(24, 24), FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            swatch.Pressed      += () => SprayColorPicked?.Invoke(sprayColor);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            _sprayRow.AddChild(swatch);
            _spraySwatches.Add((sprayColor, swatch));
        }
        foreach (Button button in new[] { _undoSprayButton, _clearSprayButton })
        {
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            _sections.AddChild(button);
        }
    }

    private void BuildSculptButtons()
    {
        // Posture: the whole body from nose down over level to upright, the stage tilts its view with it
        // Label and slider share one row each, the column has to fit a small screen
        _postureLabel  = new Label { CustomMinimumSize = new Vector2(SLIDER_LABEL_WIDTH, 0f) };
        _postureSlider = new HSlider { MinValue = -90, MaxValue = 90, Step = 15 };
        _postureSlider.ValueChanged += value => PostureChanged?.Invoke((float)value);
        AddSliderRow(_postureLabel, _postureSlider);

        // Hover: how high the body floats above the ground on land, 0 rests on its lowest part
        _hoverLabel  = new Label { CustomMinimumSize = new Vector2(SLIDER_LABEL_WIDTH, 0f) };
        _hoverSlider = new HSlider { MinValue = 0, MaxValue = CreatureLook.MAX_HOVER, Step = 0.1 };
        _hoverSlider.ValueChanged += value => HoverChanged?.Invoke((float)value);
        AddSliderRow(_hoverLabel, _hoverSlider);

        // Flatness: the cross-section from tall and narrow over round to pressed flat
        _flatLabel  = new Label { CustomMinimumSize = new Vector2(SLIDER_LABEL_WIDTH, 0f) };
        _flatSlider = new HSlider { MinValue = -1, MaxValue = 1, Step = 0.1 };
        _flatSlider.ValueChanged += value => FlatnessChanged?.Invoke((float)value);
        AddSliderRow(_flatLabel, _flatSlider);

        // Symmetry: two icons that exclude each other. The axis icon mirrors new parts (a second click turns it
        // off: single halves), the ring icon sets them in a ring around the spine, one more per click up to the maximum
        var symmetry = new HBoxContainer();
        symmetry.AddThemeConstantOverride("separation", 12);
        _sections.AddChild(symmetry);
        _axisIcon = new SymmetryIcon { CustomMinimumSize = new Vector2(52, 52) };
        _axisIcon.Pressed += () => SymmetryToggled?.Invoke(_session.RadialCount >= 2 || !_session.IsSymmetric);
        _ringIcon = new SymmetryIcon { IsRing = true, CustomMinimumSize = new Vector2(52, 52) };
        _ringIcon.Pressed += () => RadialPicked?.Invoke(_session.RadialCount < 2 || _session.RadialCount >= CreatureDesign.MAX_RADIAL ? 2 : _session.RadialCount + 1);
        _symmetryLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _symmetryLabel.AddThemeFontSizeOverride("font_size", 14);
        symmetry.AddChild(_axisIcon);
        symmetry.AddChild(_ringIcon);
        symmetry.AddChild(_symmetryLabel);

        // - and + for vertebrae side by side, each takes half the width
        var vertebrae = new HBoxContainer();
        vertebrae.AddThemeConstantOverride("separation", 8);
        _sections.AddChild(vertebrae);
        _addButton    = CreatureEditorUi.NewPillButton(new Vector2(0, 38), 16);
        _removeButton = CreatureEditorUi.NewPillButton(new Vector2(0, 38), 16);
        _addButton.Pressed    += () => AddVertebraPressed?.Invoke();
        _removeButton.Pressed += () => RemoveVertebraPressed?.Invoke();
        foreach (Button button in new[] { _removeButton, _addButton })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            vertebrae.AddChild(button);
        }
    }

    /// <summary>A label and its slider side by side.</summary>
    private void AddSliderRow(Label label, HSlider slider)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        label.AddThemeFontSizeOverride("font_size", 13);
        slider.FocusMode           = FocusModeEnum.None;
        slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        slider.SizeFlagsVertical   = SizeFlags.ShrinkCenter;
        row.AddChild(label);
        row.AddChild(slider);
        _sections.AddChild(row);
    }

    private static HSeparator NewSeparator()
    {
        return new HSeparator { MouseFilter = MouseFilterEnum.Ignore };
    }

    // ===========================================================================
    // ===========================   REFRESH TOOLBAR   ===========================
    // ===========================================================================
    //
    //   Refresh         Texts, the pressed switches, undo and wipe, picked colors and pattern.
    //   PunchColors     The body colors pop after a pick.
    //   PunchPatterns   The pattern pills pop after a pick.
    //

    /// <summary>Shows the modes and the paint of the session.</summary>
    public void Refresh()
    {
        CreatureDesign draft  = _session.Draft;
        Color          accent = CreatureEditorUi.TypeAccent(draft.Type);

        int degrees = Mathf.RoundToInt(Mathf.RadToDeg(draft.Pitch));
        _postureLabel.Text = string.Format(Tr("CREATURE_POSTURE"), degrees);
        _postureSlider.SetValueNoSignal(degrees);
        _hoverLabel.Text = string.Format(Tr("CREATURE_HOVER"), draft.Hover.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        _hoverSlider.SetValueNoSignal(draft.Hover);
        _flatLabel.Text = string.Format(Tr("CREATURE_FLATNESS"), draft.Flatness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        _flatSlider.SetValueNoSignal(draft.Flatness);
        _addButton.Text           = Tr("CREATURE_ADD_VERTEBRA");
        _removeButton.Text        = Tr("CREATURE_REMOVE_VERTEBRA");
        _sprayButton.Text         = Tr("CREATURE_SPRAY");
        _undoSprayButton.Text     = Tr("CREATURE_SPRAY_UNDO");
        _clearSprayButton.Text    = Tr("CREATURE_SPRAY_CLEAR");
        _sprayButton.SetPressedNoSignal(_session.IsSpraying);
        EditorStyle.ApplyButton(_sprayButton, _session.IsSpraying ? accent : EditorStyle.RIM_COLOR);
        _undoSprayButton.Visible   = _session.IsSpraying;
        _clearSprayButton.Visible  = _session.IsSpraying;
        _undoSprayButton.Disabled  = 0 == draft.Splats.Count;
        _clearSprayButton.Disabled = 0 == draft.Splats.Count;
        bool isRing = _session.RadialCount >= 2;
        _axisIcon.IsOn   = !isRing && _session.IsSymmetric;
        _axisIcon.Accent = accent;
        _ringIcon.IsOn   = isRing;
        _ringIcon.Count  = _session.RadialCount;
        _ringIcon.Accent = accent;
        _axisIcon.TooltipText = Tr("CREATURE_SYMMETRY_AXIS");
        _ringIcon.TooltipText = Tr("CREATURE_SYMMETRY_POINT");
        _symmetryLabel.Text   = isRing ? string.Format(Tr("CREATURE_SYMMETRY_RING"), _session.RadialCount) : Tr(_session.IsSymmetric ? "CREATURE_SYMMETRY_ON" : "CREATURE_SYMMETRY_OFF");
        _addButton.Disabled       = draft.Girths.Count >= CreatureDesign.MAX_VERTEBRAE;
        _removeButton.Disabled    = draft.Girths.Count <= CreatureDesign.MIN_VERTEBRAE;

        foreach ((CreaturePattern pattern, Button button) in _patternButtons)
        {
            button.Text = Tr($"CREATURE_PATTERN_{pattern}".ToUpperInvariant());
            EditorStyle.ApplyButton(button, pattern == draft.Pattern ? accent : EditorStyle.RIM_COLOR);
        }
        // The two icons show what is picked, the label names what is open
        EditorStyle.ApplySwatch(_colorIcon, _session.DraftColor, _swatchRow.Visible);
        _skinIcon.Skin     = draft.Skin;
        _skinIcon.Tint     = _session.DraftColor;
        _skinIcon.IsPicked = _skinRow.Visible;
        _patternIcon.Pattern  = draft.Pattern;
        _patternIcon.Tint     = _session.DraftColor;
        _patternIcon.Paint    = draft.PatternColor ?? _session.DraftColor.Darkened(0.45f);
        _patternIcon.IsPicked = _patternRow.Visible;
        _colorIcon.TooltipText   = Tr("CREATURE_TOOL_COLOR");
        _skinIcon.TooltipText    = Tr($"CREATURE_SKIN_{draft.Skin}".ToUpperInvariant());
        _patternIcon.TooltipText = Tr($"CREATURE_PATTERN_{draft.Pattern}".ToUpperInvariant());
        _iconLabel.Text = _skinRow.Visible ? Tr($"CREATURE_SKIN_{draft.Skin}".ToUpperInvariant())
            : _patternRow.Visible ? Tr($"CREATURE_PATTERN_{draft.Pattern}".ToUpperInvariant())
            : _swatchRow.Visible ? Tr($"CREATURE_ZONE_{_zone}".ToUpperInvariant()) : "";
        foreach (SkinIcon tile in _skinTiles)
        {
            tile.Tint        = _session.DraftColor;
            tile.IsPicked    = tile.Skin == draft.Skin;
            tile.TooltipText = Tr($"CREATURE_SKIN_{tile.Skin}".ToUpperInvariant());
        }
        // The second color belongs to the pattern, the can has colors of its own
        _paintRow.Visible = _patternRow.Visible && CreaturePattern.None != draft.Pattern;
        _sprayRow.Visible = _session.IsSpraying;
        foreach ((Color? sprayColor, Button swatch) in _spraySwatches)
        {
            EditorStyle.ApplySwatch(swatch, sprayColor ?? _session.DraftColor.Darkened(0.45f), sprayColor == _session.PickedSprayColor);
        }
        foreach ((Color? paintColor, Button swatch) in _patternSwatches)
        {
            EditorStyle.ApplySwatch(swatch, paintColor ?? _session.DraftColor.Darkened(0.45f), paintColor == draft.PatternColor);
        }
        // Zone icons in their colors, the swatches show what the picked zone wears
        foreach (ZoneIcon icon in _zoneIcons)
        {
            icon.Body        = _session.DraftColor;
            icon.Fill        = CreatureZone.Body == icon.Zone ? _session.DraftColor : draft.ZoneColor(icon.Zone);
            icon.IsPicked    = icon.Zone == _zone;
            icon.TooltipText = Tr($"CREATURE_ZONE_{icon.Zone}".ToUpperInvariant());
        }
        Color? worn = draft.ZoneColor(_zone);
        foreach ((Color? swatchColor, Button swatch) in _swatches)
        {
            // The first swatch: the default color for the body, the plain body color (no zone) for the others
            Color shown = swatchColor ?? (CreatureZone.Body == _zone ? Player.ORGANISM_COLOR : _session.DraftColor);
            EditorStyle.ApplySwatch(swatch, shown, swatchColor == worn);
            swatch.Disabled = CreatureZone.Body != _zone && null != swatchColor && swatchColor.Value.A < 0.99f;
        }
    }

    /// <summary>Pops the row of body colors.</summary>
    public void PunchColors()
    {
        EditorStyle.Punch(_swatchRow, 1.05f);
    }

    /// <summary>Pops the row of pattern pills.</summary>
    public void PunchPatterns()
    {
        EditorStyle.Punch(_patternRow, 1.05f);
    }
}
