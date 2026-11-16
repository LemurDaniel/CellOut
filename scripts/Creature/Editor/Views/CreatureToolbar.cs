// NOTE:
//   The tools of the creature editor, sorted into three panels the editor shows by mode:
//   - PaintPanel (paint mode, left of the turntable): color with its zones, skin and pattern,
//     every choice open under a headline.
//   - DetailPanel (paint mode, right): the spray can with its own colors and the mouth.
//   - FormPanel (build mode, right): posture, hover, symmetry, vertebrae, and what belongs to
//     the picked vertebra (ring shape) or the picked limb (its look).
//   A view: it only reports clicks (events) and shows the state of the session (Refresh).
//   It is a plain node that owns no layout itself, the editor places its three panels.
//
//   var toolbar = new CreatureToolbar();
//   toolbar.Setup(session);
//   toolbar.ColorPicked += PickColor;
//   AddChild(toolbar);
//   frame.AddWorkbench(palette, toolbar.PaintPanel, stage, toolbar.FormPanel, toolbar.DetailPanel);

//
//   Chapters:
//     BUILD TOOLBAR     The three panels with their sections.
//     REFRESH TOOLBAR   Texts, pressed and picked states.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CreatureToolbar : Node
{
    // ===========================================================================
    // ============================   BUILD TOOLBAR   ============================
    // ===========================================================================
    //
    //   Events             One per click or slider, named after what was picked.
    //   PaintPanel, DetailPanel, FormPanel   The three panels, placed by the editor.
    //   Setup              Builds the three panels.
    //   BuildPaintPanel    Color with zones, skin, pattern with its options.
    //   BuildDetailPanel   Spray can with its own colors, the mouth.
    //   BuildFormPanel     Posture, hover, symmetry, vertebrae, ring shape, limb look.
    //   NewPanel           A new CreaturePanel, the one the helpers below add to.
    //   AddHeadline, AddSliderRow, NewSwatches   Hand over to the panel being built (Views/CreaturePanel.cs).
    //

    public const float SIDE_WIDTH = 262f;            // the right panels, room for a scroll bar included
    private const int MAX_TIP_JOINTS = 4;            // the most joints a limb has (a tentacle)

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
    public event System.Action<Color?> PatternColorPicked;
    public event System.Action<Color?> PartColorPicked;
    public event System.Action<Color?, int> PartTipColorPicked;
    public event System.Action<float> PatternScaleChanged;
    public event System.Action<bool> PatternWrapToggled;
    public event System.Action<bool> MouthLineToggled;
    public event System.Action<float, float> MouthPlaceChanged;
    public event System.Action LimbLookPressed;
    public event System.Action<float, float> RingShapeChanged;

    /// <summary>Paint mode, left of the turntable: color, skin and pattern.</summary>
    public Control PaintPanel { get; private set; }

    /// <summary>Paint mode, right of the turntable: spray can and mouth.</summary>
    public Control DetailPanel { get; private set; }

    /// <summary>Build mode, right of the turntable: posture, symmetry, vertebrae and the picked vertebra or limb.</summary>
    public Control FormPanel { get; private set; }

    private CreatureEditorSession _session;
    private CreaturePanel _panel;                        // the panel being built
    private VBoxContainer _sections;                     // its column
    private readonly List<CreaturePanel> _panels = new();

    // Paint panel
    private HFlowContainer _zoneRow;
    private readonly List<ZoneIcon> _zoneIcons = new();
    private ZoneIcon _partIcon;
    private CreatureZone _zone = CreatureZone.Body;      // the zone the swatches paint
    private bool _paintsPart;                            // the swatches paint the picked part instead of a zone
    private Label _zoneLabel;
    private HFlowContainer _tipRow;                      // picked limb: paint it whole or from a joint outwards
    private readonly List<Button> _tipButtons = new();
    private int _tipFrom;                                // 0 the whole part, else the joint the swatches paint from
    private HFlowContainer _swatchRow;
    private readonly List<(Color? Color, Button Swatch)> _swatches = new();
    private readonly List<SkinIcon> _skinTiles = new();
    private HFlowContainer _patternRow;
    private readonly List<(CreaturePattern Pattern, Button Button)> _patternButtons = new();
    private Button _wrapButton;
    private Label _scaleLabel;
    private HSlider _scaleSlider;
    private Control _scaleRow;
    private HFlowContainer _paintRow;
    private readonly List<(Color? Color, Button Swatch)> _patternSwatches = new();

    // Detail panel
    private Button _sprayButton;
    private Button _undoSprayButton;
    private Button _clearSprayButton;
    private HFlowContainer _sprayRow;
    private readonly List<(Color? Color, Button Swatch)> _spraySwatches = new();
    private Button _mouthButton;
    private Label _mouthLevelLabel;
    private HSlider _mouthLevelSlider;
    private Control _mouthLevelRow;
    private Label _mouthLengthLabel;
    private HSlider _mouthLengthSlider;
    private Control _mouthLengthRow;

    // Form panel
    private readonly List<Control> _bodyRows = new();    // posture, hover and symmetry: they belong to the body, hidden while an own part is sculpted
    private Label _postureLabel;
    private HSlider _postureSlider;
    private Label _hoverLabel;
    private HSlider _hoverSlider;
    private SymmetryIcon _axisIcon;
    private SymmetryIcon _ringIcon;
    private Label _symmetryLabel;
    private Button _addButton;
    private Button _removeButton;
    private Label _crownLabel;
    private HSlider _crownSlider;
    private Control _crownRow;
    private Label _bellyLabel;
    private HSlider _bellySlider;
    private Control _bellyRow;
    private Button _limbLookButton;

    /// <summary>Builds the three panels, call once before the editor places them.</summary>
    /// <param name="session">(Required) The sitting whose modes and paint the panels show.</param>
    /// <param name="paintWidth">(Required) Width of the paint panel: as wide as the part palette it replaces, so the turntable stays put.</param>
    public void Setup(CreatureEditorSession session, float paintWidth)
    {
        _session = session;
        BuildPaintPanel(paintWidth);
        BuildDetailPanel();
        BuildFormPanel();
    }

    private void BuildPaintPanel(float width)
    {
        PaintPanel = NewPanel(width);

        // Color: one icon per zone (body, belly, head, limbs, tail end) and one for the picked part. The picked one
        // takes the swatches: their first swatch is the default for the body and "no color of its own" for the others
        AddHeadline("CREATURE_SECTION_COLOR");
        _zoneRow = new HFlowContainer();
        _zoneRow.AddThemeConstantOverride("h_separation", 6);
        _zoneRow.AddThemeConstantOverride("v_separation", 4);
        _sections.AddChild(_zoneRow);
        foreach (CreatureZone zone in System.Enum.GetValues<CreatureZone>())
        {
            var icon = new ZoneIcon { Zone = zone, CustomMinimumSize = new Vector2(46, 36) };
            icon.Pressed += () =>
            {
                _zone       = zone;
                _paintsPart = false;
                Refresh();
            };
            _zoneRow.AddChild(icon);
            _zoneIcons.Add(icon);
        }
        _partIcon = new ZoneIcon { IsPart = true, CustomMinimumSize = new Vector2(46, 36) };
        _partIcon.Pressed += () =>
        {
            _paintsPart = true;
            Refresh();
        };
        _zoneRow.AddChild(_partIcon);
        _zoneLabel = new Label();
        _zoneLabel.AddThemeFontSizeOverride("font_size", 13);
        _sections.AddChild(_zoneLabel);

        // A picked limb: the swatches paint it whole or only from one of its joints outwards (white lower legs)
        _tipRow = new HFlowContainer();
        _tipRow.AddThemeConstantOverride("h_separation", 6);
        _tipRow.AddThemeConstantOverride("v_separation", 4);
        _sections.AddChild(_tipRow);
        for (int from = 0; from <= MAX_TIP_JOINTS; from++)
        {
            int    joint = from;
            Button pill  = CreatureEditorUi.NewPillButton(new Vector2(0, 26), 12);
            pill.Pressed += () =>
            {
                _tipFrom = joint;
                Refresh();
            };
            _tipRow.AddChild(pill);
            _tipButtons.Add(pill);
        }

        // Body colors: coral default first, translucent last. A see-through color only fits the whole body
        var bodyColors = new List<Color?> { null };
        bodyColors.AddRange(EditorStyle.BODY_SWATCHES.Select(color => (Color?)color));
        _swatchRow = NewSwatches(bodyColors, 30, _swatches, color =>
        {
            bool isOpaque = null == color || color.Value.A >= 0.99f;
            if (_paintsPart)
            {
                if (isOpaque && _tipFrom > 0) PartTipColorPicked?.Invoke(color, _tipFrom);
                else if (isOpaque) PartColorPicked?.Invoke(color);
            }
            else if (CreatureZone.Body == _zone)
            {
                ColorPicked?.Invoke(color);
            }
            else if (isOpaque)
            {
                ZoneColorPicked?.Invoke(_zone, color);
            }
        });

        // Skin: one icon per surface, drawn in the body color
        AddHeadline("CREATURE_SECTION_SKIN");
        var skinRow = new HFlowContainer();
        skinRow.AddThemeConstantOverride("h_separation", 10);
        skinRow.AddThemeConstantOverride("v_separation", 8);
        _sections.AddChild(skinRow);
        foreach (CreatureSkin skin in System.Enum.GetValues<CreatureSkin>())
        {
            var tile = new SkinIcon { Skin = skin, CustomMinimumSize = new Vector2(42, 42) };
            tile.Pressed += () => SkinPicked?.Invoke(skin);
            skinRow.AddChild(tile);
            _skinTiles.Add(tile);
        }

        // Pattern: one pill per pattern, then its options (all around, fineness) and its second color
        AddHeadline("CREATURE_SECTION_PATTERN");
        _patternRow = new HFlowContainer();
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
        _wrapButton            = CreatureEditorUi.NewPillButton(new Vector2(0, 30), 14);
        _wrapButton.ToggleMode = true;
        _wrapButton.Toggled   += isOn => PatternWrapToggled?.Invoke(isOn);
        _patternRow.AddChild(_wrapButton);
        _scaleLabel  = new Label();
        _scaleSlider = new HSlider { MinValue = CreatureLook.MIN_PATTERN_SCALE, MaxValue = CreatureLook.MAX_PATTERN_SCALE, Step = 0.2 };
        _scaleSlider.ValueChanged += value => PatternScaleChanged?.Invoke((float)value);
        _scaleRow = AddSliderRow(_scaleLabel, _scaleSlider);
        var paintColors = new List<Color?> { null };
        paintColors.AddRange(EditorStyle.BODY_SWATCHES.Where(color => color.A >= 0.99f).Select(color => (Color?)color));
        _paintRow = NewSwatches(paintColors, 26, _patternSwatches, color => PatternColorPicked?.Invoke(color));
    }

    private void BuildDetailPanel()
    {
        DetailPanel = NewPanel(SIDE_WIDTH);

        // Spray can: its own colors apart from the pattern, undo and wipe only while it is on (Refresh)
        AddHeadline("CREATURE_SPRAY");
        _sprayButton            = CreatureEditorUi.NewPillButton(new Vector2(0, 38), 16);
        _sprayButton.ToggleMode = true;
        _sprayButton.Toggled   += isSpraying => SprayToggled?.Invoke(isSpraying);
        _sections.AddChild(_sprayButton);
        var sprayColors = new List<Color?> { null };
        sprayColors.AddRange(EditorStyle.BODY_SWATCHES.Where(color => color.A >= 0.99f).Select(color => (Color?)color));
        _sprayRow = NewSwatches(sprayColors, 24, _spraySwatches, color => SprayColorPicked?.Invoke(color));
        _undoSprayButton = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _undoSprayButton.Pressed += () => UndoSprayPressed?.Invoke();
        _clearSprayButton = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _clearSprayButton.Pressed += () => ClearSprayPressed?.Invoke();
        foreach (Button button in new[] { _undoSprayButton, _clearSprayButton })
        {
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            _sections.AddChild(button);
        }

        // The mouth: on or off, its height on the head and its length
        AddHeadline("CREATURE_SECTION_FACE");
        _mouthButton            = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _mouthButton.ToggleMode = true;
        _mouthButton.Toggled   += isOn => MouthLineToggled?.Invoke(isOn);
        _sections.AddChild(_mouthButton);
        _mouthLevelLabel  = new Label();
        _mouthLevelSlider = new HSlider { MinValue = -1, MaxValue = 1, Step = 0.05 };
        _mouthLevelSlider.ValueChanged += value => MouthPlaceChanged?.Invoke((float)value, (float)_mouthLengthSlider.Value);
        _mouthLevelRow = AddSliderRow(_mouthLevelLabel, _mouthLevelSlider);
        _mouthLengthLabel  = new Label();
        _mouthLengthSlider = new HSlider { MinValue = CreatureLook.MIN_MOUTH_LENGTH, MaxValue = CreatureLook.MAX_MOUTH_LENGTH, Step = 0.05 };
        _mouthLengthSlider.ValueChanged += value => MouthPlaceChanged?.Invoke((float)_mouthLevelSlider.Value, (float)value);
        _mouthLengthRow = AddSliderRow(_mouthLengthLabel, _mouthLengthSlider);
    }

    private void BuildFormPanel()
    {
        FormPanel = NewPanel(SIDE_WIDTH);
        AddHeadline("CREATURE_SECTION_FORM");

        // Posture: the whole body from nose down over level to upright, the stage tilts its view with it
        _postureLabel  = new Label();
        _postureSlider = new HSlider { MinValue = -90, MaxValue = 90, Step = 15 };
        _postureSlider.ValueChanged += value => PostureChanged?.Invoke((float)value);
        _bodyRows.Add(AddSliderRow(_postureLabel, _postureSlider));

        // Hover: how high the body floats above the ground on land, 0 rests on its lowest part
        _hoverLabel  = new Label();
        _hoverSlider = new HSlider { MinValue = 0, MaxValue = CreatureLook.MAX_HOVER, Step = 0.1 };
        _hoverSlider.ValueChanged += value => HoverChanged?.Invoke((float)value);
        _bodyRows.Add(AddSliderRow(_hoverLabel, _hoverSlider));

        // Symmetry: two icons that exclude each other. The axis icon mirrors new parts (a second click turns it
        // off: single halves), the ring icon sets them in a ring around the spine, one more per click up to the maximum
        var symmetry = new HBoxContainer();
        symmetry.AddThemeConstantOverride("separation", 12);
        _sections.AddChild(symmetry);
        _axisIcon = new SymmetryIcon { CustomMinimumSize = new Vector2(52, 52) };
        _axisIcon.Pressed += () => SymmetryToggled?.Invoke(_session.RadialCount >= 2 || !_session.IsSymmetric);
        _ringIcon = new SymmetryIcon { IsRing = true, CustomMinimumSize = new Vector2(52, 52) };
        _ringIcon.Pressed += () => RadialPicked?.Invoke(_session.RadialCount < 2 || _session.RadialCount >= CreatureDesign.MAX_RADIAL ? 2 : _session.RadialCount + 1);
        _symmetryLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _symmetryLabel.AddThemeFontSizeOverride("font_size", 14);
        symmetry.AddChild(_axisIcon);
        symmetry.AddChild(_ringIcon);
        symmetry.AddChild(_symmetryLabel);
        _bodyRows.Add(symmetry);

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
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            vertebrae.AddChild(button);
        }

        // The ring of the picked vertebra: its upper and lower half from a keel over round to boxy (only while one is picked)
        _crownLabel  = new Label();
        _crownSlider = new HSlider { MinValue = -1, MaxValue = 1, Step = 0.1 };
        _crownSlider.ValueChanged += value => RingShapeChanged?.Invoke((float)value, (float)_bellySlider.Value);
        _crownRow = AddSliderRow(_crownLabel, _crownSlider);
        _bellyLabel  = new Label();
        _bellySlider = new HSlider { MinValue = -1, MaxValue = 1, Step = 0.1 };
        _bellySlider.ValueChanged += value => RingShapeChanged?.Invoke((float)_crownSlider.Value, (float)value);
        _bellyRow = AddSliderRow(_bellyLabel, _bellySlider);

        // The tube of the picked limb takes the look of the next limb kind (only while a limb, foot or hand is picked)
        _limbLookButton = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _limbLookButton.Pressed += () => LimbLookPressed?.Invoke();
        EditorStyle.ApplyButton(_limbLookButton, EditorStyle.RIM_COLOR);
        _sections.AddChild(_limbLookButton);
    }

    /// <summary>A new panel; _panel is the one being built afterwards.</summary>
    private CreaturePanel NewPanel(float width)
    {
        _panel = new CreaturePanel();
        _panel.Setup(width);
        _panels.Add(_panel);
        _sections = _panel.Sections;
        return _panel;
    }

    private void AddHeadline(string key) => _panel.AddHeadline(key);

    private HBoxContainer AddSliderRow(Label label, HSlider slider) => _panel.AddSliderRow(label, slider);

    private HFlowContainer NewSwatches(IEnumerable<Color?> colors, int pixels, List<(Color? Color, Button Swatch)> swatches, System.Action<Color?> picked)
    {
        return _panel.AddSwatches(colors, pixels, swatches, picked);
    }

    // ===========================================================================
    // ===========================   REFRESH TOOLBAR   ===========================
    // ===========================================================================
    //
    //   Refresh         Which panels show, texts, pressed switches, picked colors and what depends on the selection.
    //   RefreshPaint    Zones, swatches, skin and pattern.
    //   RefreshDetail   Spray can and mouth.
    //   RefreshForm     Posture, hover, symmetry, vertebrae, ring shape and limb look.
    //   PunchColors     The body colors pop after a pick.
    //   PunchPatterns   The pattern pills pop after a pick.
    //

    private static readonly System.Globalization.CultureInfo NUMBER = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Shows the panels of the mode with the state of the session.</summary>
    public void Refresh()
    {
        PaintPanel.Visible  = _session.IsPainting;
        DetailPanel.Visible = _session.IsPainting;
        FormPanel.Visible   = _session.HasHandles;
        foreach (CreaturePanel panel in _panels)
        {
            panel.Translate();
        }
        Color accent = CreatureEditorUi.TypeAccent(_session.Draft.Type);
        RefreshPaint(accent);
        RefreshDetail(accent);
        RefreshForm(accent);
    }

    private void RefreshPaint(Color accent)
    {
        CreatureDesign draft  = _session.Draft;
        int            picked = _session.PickedPart;

        // Zone icons in their colors; the part icon only while a part is picked, it shows the color the part wears
        bool hasPart = picked >= 0 && picked < draft.Parts.Count;
        _paintsPart  = _paintsPart && hasPart;
        foreach (ZoneIcon icon in _zoneIcons)
        {
            icon.Body        = _session.DraftColor;
            icon.Fill        = CreatureZone.Body == icon.Zone ? _session.DraftColor : draft.ZoneColor(icon.Zone);
            icon.IsPicked    = !_paintsPart && icon.Zone == _zone;
            icon.TooltipText = Tr($"CREATURE_ZONE_{icon.Zone}".ToUpperInvariant());
        }
        _partIcon.Disabled    = !hasPart;
        _partIcon.Body        = _session.DraftColor;
        _partIcon.Fill        = hasPart ? draft.Parts[picked].Tint ?? _session.DraftColor : null;
        _partIcon.IsPicked    = _paintsPart;
        _partIcon.TooltipText = Tr("CREATURE_ZONE_PART");
        _zoneLabel.Text       = Tr(_paintsPart ? "CREATURE_ZONE_PART" : $"CREATURE_ZONE_{_zone}".ToUpperInvariant());

        // A picked limb can be painted from a joint outwards: one pill for the whole part and one per joint
        int joints = _paintsPart && null != draft.Parts[picked].Joints ? draft.Parts[picked].Joints.Count : 0;
        _tipFrom        = Mathf.Min(_tipFrom, joints);
        _tipRow.Visible = joints > 0;
        for (int from = 0; from < _tipButtons.Count; from++)
        {
            _tipButtons[from].Visible = from <= joints;
            _tipButtons[from].Text    = 0 == from ? Tr("CREATURE_TIP_WHOLE") : string.Format(Tr("CREATURE_TIP_FROM"), from);
            EditorStyle.ApplyButton(_tipButtons[from], from == _tipFrom ? accent : EditorStyle.RIM_COLOR);
        }

        // The swatches show what the picked zone or part wears. The first swatch: the default color for the body,
        // the plain body color (no color of its own) for everything else
        bool   isBody = !_paintsPart && CreatureZone.Body == _zone;
        Color? worn   = _paintsPart ? (_tipFrom > 0 ? (_tipFrom == draft.Parts[picked].TipFrom ? draft.Parts[picked].TipColor : null) : draft.Parts[picked].Tint)
            : isBody ? draft.Color : draft.ZoneColor(_zone);
        foreach ((Color? swatchColor, Button swatch) in _swatches)
        {
            Color shown = swatchColor ?? (isBody ? Player.ORGANISM_COLOR : _session.DraftColor);
            EditorStyle.ApplySwatch(swatch, shown, swatchColor == worn);
            swatch.Disabled = !isBody && null != swatchColor && swatchColor.Value.A < 0.99f;
        }

        foreach (SkinIcon tile in _skinTiles)
        {
            tile.Tint        = _session.DraftColor;
            tile.IsPicked    = tile.Skin == draft.Skin;
            tile.TooltipText = Tr($"CREATURE_SKIN_{tile.Skin}".ToUpperInvariant());
        }

        // Pattern options and the second color only with a pattern picked
        bool hasPattern = CreaturePattern.None != draft.Pattern;
        foreach ((CreaturePattern pattern, Button button) in _patternButtons)
        {
            button.Text = Tr($"CREATURE_PATTERN_{pattern}".ToUpperInvariant());
            EditorStyle.ApplyButton(button, pattern == draft.Pattern ? accent : EditorStyle.RIM_COLOR);
        }
        _wrapButton.Text    = Tr("CREATURE_PATTERN_WRAP");
        _wrapButton.Visible = hasPattern;
        _wrapButton.SetPressedNoSignal(draft.PatternWraps);
        EditorStyle.ApplyButton(_wrapButton, draft.PatternWraps ? accent : EditorStyle.RIM_COLOR);
        _scaleRow.Visible = hasPattern;
        _scaleLabel.Text  = string.Format(Tr("CREATURE_PATTERN_SCALE"), draft.PatternScale.ToString("0.0", NUMBER));
        _scaleSlider.SetValueNoSignal(draft.PatternScale);
        _paintRow.Visible = hasPattern;
        foreach ((Color? paintColor, Button swatch) in _patternSwatches)
        {
            EditorStyle.ApplySwatch(swatch, paintColor ?? _session.DraftColor.Darkened(0.45f), paintColor == draft.PatternColor);
        }
    }

    private void RefreshDetail(Color accent)
    {
        CreatureDesign draft = _session.Draft;
        _sprayButton.Text      = Tr(_session.IsSpraying ? "CREATURE_SPRAY_ON" : "CREATURE_SPRAY_OFF");
        _undoSprayButton.Text  = Tr("CREATURE_SPRAY_UNDO");
        _clearSprayButton.Text = Tr("CREATURE_SPRAY_CLEAR");
        _sprayButton.SetPressedNoSignal(_session.IsSpraying);
        EditorStyle.ApplyButton(_sprayButton, _session.IsSpraying ? accent : EditorStyle.RIM_COLOR);
        _sprayRow.Visible          = _session.IsSpraying;
        _undoSprayButton.Visible   = _session.IsSpraying;
        _clearSprayButton.Visible  = _session.IsSpraying;
        _undoSprayButton.Disabled  = 0 == draft.Splats.Count;
        _clearSprayButton.Disabled = 0 == draft.Splats.Count;
        foreach ((Color? sprayColor, Button swatch) in _spraySwatches)
        {
            EditorStyle.ApplySwatch(swatch, sprayColor ?? _session.DraftColor.Darkened(0.45f), sprayColor == _session.PickedSprayColor);
        }

        _mouthButton.Text = Tr(draft.HasMouthLine ? "CREATURE_MOUTH_ON" : "CREATURE_MOUTH_OFF");
        _mouthButton.SetPressedNoSignal(draft.HasMouthLine);
        EditorStyle.ApplyButton(_mouthButton, draft.HasMouthLine ? accent : EditorStyle.RIM_COLOR);
        _mouthLevelRow.Visible  = draft.HasMouthLine;
        _mouthLengthRow.Visible = draft.HasMouthLine;
        _mouthLevelLabel.Text   = string.Format(Tr("CREATURE_MOUTH_LEVEL"), draft.MouthLevel.ToString("0.00", NUMBER));
        _mouthLengthLabel.Text  = string.Format(Tr("CREATURE_MOUTH_LENGTH"), draft.MouthLength.ToString("0.00", NUMBER));
        _mouthLevelSlider.SetValueNoSignal(draft.MouthLevel);
        _mouthLengthSlider.SetValueNoSignal(draft.MouthLength);
    }

    private void RefreshForm(Color accent)
    {
        // The vertebra rows work on the clay on the turntable: the body, or an own part in the sculpt mode
        CreatureDesign draft = _session.Sculpted;
        foreach (Control row in _bodyRows)
        {
            row.Visible = CreatureEditorMode.Build == _session.Mode;
        }
        int degrees = Mathf.RoundToInt(Mathf.RadToDeg(draft.Pitch));
        _postureLabel.Text = string.Format(Tr("CREATURE_POSTURE"), degrees);
        _postureSlider.SetValueNoSignal(degrees);
        _hoverLabel.Text = string.Format(Tr("CREATURE_HOVER"), draft.Hover.ToString("0.0", NUMBER));
        _hoverSlider.SetValueNoSignal(draft.Hover);

        bool isRing = _session.RadialCount >= 2;
        _axisIcon.IsOn   = !isRing && _session.IsSymmetric;
        _axisIcon.Accent = accent;
        _ringIcon.IsOn   = isRing;
        _ringIcon.Count  = _session.RadialCount;
        _ringIcon.Accent = accent;
        _axisIcon.TooltipText = Tr("CREATURE_SYMMETRY_AXIS");
        _ringIcon.TooltipText = Tr("CREATURE_SYMMETRY_POINT");
        _symmetryLabel.Text   = isRing ? string.Format(Tr("CREATURE_SYMMETRY_RING"), _session.RadialCount) : Tr(_session.IsSymmetric ? "CREATURE_SYMMETRY_ON" : "CREATURE_SYMMETRY_OFF");

        _addButton.Text        = Tr("CREATURE_ADD_VERTEBRA");
        _removeButton.Text     = Tr("CREATURE_REMOVE_VERTEBRA");
        _addButton.Disabled    = draft.Girths.Count >= CreatureDesign.MAX_VERTEBRAE;
        _removeButton.Disabled = draft.Girths.Count <= CreatureDesign.MIN_VERTEBRAE;

        // The ring of the picked vertebra
        int vertebra = _session.PickedVertebra;
        _crownRow.Visible = vertebra >= 0;
        _bellyRow.Visible = vertebra >= 0;
        if (vertebra >= 0)
        {
            _crownLabel.Text = string.Format(Tr("CREATURE_CROWN"), draft.Crowns[vertebra].ToString("0.0", NUMBER));
            _bellyLabel.Text = string.Format(Tr("CREATURE_BELLY_EDGE"), draft.Bellies[vertebra].ToString("0.0", NUMBER));
            _crownSlider.SetValueNoSignal(draft.Crowns[vertebra]);
            _bellySlider.SetValueNoSignal(draft.Bellies[vertebra]);
        }

        // The look of the picked limb
        int  picked = _session.PickedPart;
        bool isLimb = picked >= 0 && picked < draft.Parts.Count && null != draft.Parts[picked].Joints;
        _limbLookButton.Visible = isLimb;
        if (isLimb)
        {
            _limbLookButton.Text = string.Format(Tr("CREATURE_LIMB_LOOK"), Tr($"CREATURE_{draft.Parts[picked].LimbStyle}_NAME".ToUpperInvariant()));
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
