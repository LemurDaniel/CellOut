// NOTE:
//   The buttons that float over the turntable of the creature editor: + and - for
//   vertebrae and the symmetry switch bottom left, the spray can with undo and wipe at
//   the left edge, the body colors top center, below them the pattern pills and the
//   second color. A view: it only reports clicks (events) and shows the state of the
//   session (Refresh). Laid over the whole stage, the mouse passes through between the
//   buttons.
//
//   var toolbar = new CreatureToolbar();
//   toolbar.Setup(session);
//   toolbar.ColorPicked += PickColor;
//   stage.AddChild(toolbar);

//
//   Chapters:
//     BUILD TOOLBAR     Vertebra buttons, symmetry, spray column, swatches and pattern pills.
//     REFRESH TOOLBAR   Texts, pressed and picked states.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CreatureToolbar : Control
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
    //   Setup                   Builds every row.
    //   BuildSculptButtons      + and - for vertebrae and the symmetry switch.
    //   BuildSprayColumn        Spray can, undo and wipe.
    //   BuildColorRows          Body colors, pattern pills and the second color.
    //

    private const float PATTERN_ROW_OFFSET = 44f;    // below the color swatches
    private const float PAINT_ROW_OFFSET = 82f;      // second color below the pattern pills

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
    private Button _addButton;
    private Button _removeButton;
    private Button _symmetryButton;
    private Button _sprayButton;
    private Button _undoSprayButton;
    private Button _clearSprayButton;
    private HBoxContainer _swatchRow;
    private readonly List<(Color? Color, Button Swatch)> _swatches = new();
    private HBoxContainer _patternRow;
    private HBoxContainer _paintRow;
    private readonly List<(CreaturePattern Pattern, Button Button)> _patternButtons = new();
    private readonly List<(Color? Color, Button Swatch)> _patternSwatches = new();

    /// <summary>Builds the toolbar, call once before it is added to the stage.</summary>
    /// <param name="session">(Required) The sitting whose modes and paint the buttons show.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session    = session;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildSculptButtons();
        BuildSprayColumn();
        BuildColorRows();
    }

    private void BuildSculptButtons()
    {
        // + and - for vertebrae and the symmetry switch, bottom left over the turntable
        var buttons = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        buttons.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.Minsize, 10);
        buttons.GrowVertical = GrowDirection.Begin;
        buttons.AddThemeConstantOverride("separation", 10);
        AddChild(buttons);
        _addButton    = CreatureEditorUi.NewPillButton(new Vector2(0, 40), 16);
        _removeButton = CreatureEditorUi.NewPillButton(new Vector2(0, 40), 16);
        _addButton.Pressed    += () => AddVertebraPressed?.Invoke();
        _removeButton.Pressed += () => RemoveVertebraPressed?.Invoke();
        foreach (Button button in new[] { _removeButton, _addButton })
        {
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            buttons.AddChild(button);
        }
        _symmetryButton            = CreatureEditorUi.NewPillButton(new Vector2(0, 40), 16);
        _symmetryButton.ToggleMode = true;
        _symmetryButton.Toggled   += isSymmetric => SymmetryToggled?.Invoke(isSymmetric);
        buttons.AddChild(_symmetryButton);
    }

    private void BuildSprayColumn()
    {
        // Spray can, undo and wipe as a compact column at the left edge (the tail end, the head stays free);
        // undo and wipe only show while the spray can is on (Refresh)
        var sprayRow = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        sprayRow.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft, LayoutPresetMode.Minsize, 10);
        sprayRow.GrowVertical = GrowDirection.Both;
        sprayRow.AddThemeConstantOverride("separation", 8);
        AddChild(sprayRow);
        _sprayButton            = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _sprayButton.ToggleMode = true;
        _sprayButton.Toggled   += isSpraying => SprayToggled?.Invoke(isSpraying);
        _undoSprayButton        = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _undoSprayButton.Pressed += () => UndoSprayPressed?.Invoke();
        _clearSprayButton       = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _clearSprayButton.Pressed += () => ClearSprayPressed?.Invoke();
        foreach (Button button in new[] { _sprayButton, _undoSprayButton, _clearSprayButton })
        {
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            sprayRow.AddChild(button);
        }
    }

    private void BuildColorRows()
    {
        // Color swatches, top center over the turntable: coral default first, translucent last
        _swatchRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _swatchRow.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize, 10);
        _swatchRow.GrowHorizontal = GrowDirection.Both;
        _swatchRow.AddThemeConstantOverride("separation", 8);
        AddChild(_swatchRow);
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

        // Paint rows below: one pill per pattern, under it the second color (opaque swatches, dark shade first)
        _patternRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _patternRow.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize, 10);
        _patternRow.OffsetTop     += PATTERN_ROW_OFFSET;
        _patternRow.OffsetBottom  += PATTERN_ROW_OFFSET;
        _patternRow.GrowHorizontal = GrowDirection.Both;
        _patternRow.AddThemeConstantOverride("separation", 6);
        AddChild(_patternRow);
        foreach (CreaturePattern pattern in System.Enum.GetValues<CreaturePattern>())
        {
            Button pill = CreatureEditorUi.NewPillButton(new Vector2(0, 30), 14);
            pill.Pressed += () => PatternPicked?.Invoke(pattern);
            _patternRow.AddChild(pill);
            _patternButtons.Add((pattern, pill));
        }
        _paintRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _paintRow.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize, 10);
        _paintRow.OffsetTop     += PAINT_ROW_OFFSET;
        _paintRow.OffsetBottom  += PAINT_ROW_OFFSET;
        _paintRow.GrowHorizontal = GrowDirection.Both;
        _paintRow.AddThemeConstantOverride("separation", 6);
        AddChild(_paintRow);
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
        foreach ((Color? paintColor, Button swatch) in _patternSwatches)
        {
            swatch.Visible = CreaturePattern.None != draft.Pattern || _session.IsSpraying;
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
