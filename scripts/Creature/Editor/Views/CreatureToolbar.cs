// NOTE:
//   The tool column right of the turntable of the creature editor, from top to bottom:
//   the body colors, the pattern pills with the second color, the spray can with undo
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
    //   SymmetryToggled         The symmetry switch changed (isSymmetric).
    //   SprayToggled            The spray can was switched (isSpraying).
    //   UndoSprayPressed        Undo of the spray can was clicked.
    //   ClearSprayPressed       Wipe of the spray can was clicked.
    //   ColorPicked             A body color was clicked (null = coral default).
    //   PatternPicked           A pattern pill was clicked.
    //   PatternColorPicked      A second color was clicked (null = dark shade of the body).
    //   Setup                   The panel and its sections from top to bottom.
    //   BuildColorRows          Body colors, pattern pills and the second color.
    //   BuildSprayButtons       Spray can, undo and wipe.
    //   BuildSculptButtons      The symmetry switch and - and + for vertebrae.
    //   NewSeparator            A thin line between two sections.
    //

    private const float TOOLBAR_WIDTH = 250f;

    public event System.Action AddVertebraPressed;
    public event System.Action RemoveVertebraPressed;
    public event System.Action<bool> SymmetryToggled;
    public event System.Action<bool> SprayToggled;
    public event System.Action UndoSprayPressed;
    public event System.Action ClearSprayPressed;
    public event System.Action<Color?> ColorPicked;
    public event System.Action<CreaturePattern> PatternPicked;
    public event System.Action<Color?> PatternColorPicked;

    private CreatureEditorSession _session;
    private VBoxContainer _sections;
    private Button _addButton;
    private Button _removeButton;
    private Button _symmetryButton;
    private Button _sprayButton;
    private Button _undoSprayButton;
    private Button _clearSprayButton;
    private HFlowContainer _swatchRow;
    private readonly List<(Color? Color, Button Swatch)> _swatches = new();
    private HFlowContainer _patternRow;
    private HFlowContainer _paintRow;
    private readonly List<(CreaturePattern Pattern, Button Button)> _patternButtons = new();
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
        _sections.AddThemeConstantOverride("separation", 12);
        AddChild(_sections);
        BuildColorRows();
        _sections.AddChild(NewSeparator());
        BuildSprayButtons();
        _sections.AddChild(NewSeparator());
        BuildSculptButtons();
    }

    private void BuildColorRows()
    {
        // Body colors: coral default first, translucent last, wrapping into rows
        _swatchRow = new HFlowContainer();
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
            swatch.Pressed      += () => ColorPicked?.Invoke(swatchColor);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            _swatchRow.AddChild(swatch);
            _swatches.Add((swatchColor, swatch));
        }
        _sections.AddChild(NewSeparator());

        // Paint: one pill per pattern, under them the second color (opaque swatches, dark shade first)
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
        foreach (Button button in new[] { _sprayButton, _undoSprayButton, _clearSprayButton })
        {
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            _sections.AddChild(button);
        }
    }

    private void BuildSculptButtons()
    {
        _symmetryButton            = CreatureEditorUi.NewPillButton(new Vector2(0, 38), 16);
        _symmetryButton.ToggleMode = true;
        _symmetryButton.Toggled   += isSymmetric => SymmetryToggled?.Invoke(isSymmetric);
        _sections.AddChild(_symmetryButton);

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

        _addButton.Text           = Tr("CREATURE_ADD_VERTEBRA");
        _removeButton.Text        = Tr("CREATURE_REMOVE_VERTEBRA");
        _symmetryButton.Text      = Tr(_session.IsSymmetric ? "CREATURE_SYMMETRY_ON" : "CREATURE_SYMMETRY_OFF");
        _sprayButton.Text         = Tr("CREATURE_SPRAY");
        _undoSprayButton.Text     = Tr("CREATURE_SPRAY_UNDO");
        _clearSprayButton.Text    = Tr("CREATURE_SPRAY_CLEAR");
        _sprayButton.SetPressedNoSignal(_session.IsSpraying);
        EditorStyle.ApplyButton(_sprayButton, _session.IsSpraying ? accent : EditorStyle.RIM_COLOR);
        _undoSprayButton.Visible   = _session.IsSpraying;
        _clearSprayButton.Visible  = _session.IsSpraying;
        _undoSprayButton.Disabled  = 0 == draft.Splats.Count;
        _clearSprayButton.Disabled = 0 == draft.Splats.Count;
        _symmetryButton.SetPressedNoSignal(_session.IsSymmetric);
        EditorStyle.ApplyButton(_symmetryButton, _session.IsSymmetric ? accent : EditorStyle.RIM_COLOR);
        _addButton.Disabled       = draft.Girths.Count >= CreatureDesign.MAX_VERTEBRAE;
        _removeButton.Disabled    = draft.Girths.Count <= CreatureDesign.MIN_VERTEBRAE;

        foreach ((CreaturePattern pattern, Button button) in _patternButtons)
        {
            button.Text = Tr($"CREATURE_PATTERN_{pattern}".ToUpperInvariant());
            EditorStyle.ApplyButton(button, pattern == draft.Pattern ? accent : EditorStyle.RIM_COLOR);
        }
        _paintRow.Visible = CreaturePattern.None != draft.Pattern || _session.IsSpraying;
        foreach ((Color? paintColor, Button swatch) in _patternSwatches)
        {
            EditorStyle.ApplySwatch(swatch, paintColor ?? _session.DraftColor.Darkened(0.45f), paintColor == draft.PatternColor);
        }
        foreach ((Color? swatchColor, Button swatch) in _swatches)
        {
            EditorStyle.ApplySwatch(swatch, swatchColor ?? Player.ORGANISM_COLOR, swatchColor == draft.Color);
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
