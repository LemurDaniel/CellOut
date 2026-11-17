// NOTE:
//   The right column of the village builder: what the function shown costs and does (the
//   blocks are looks only), the button "Templates" that opens the template window with the layouts of
//   that function and the library (TemplatePicker.cs), and the paint menu: the patterns as drawn swatches, the paints as
//   color swatches.
//   Pattern and paint go with the next block dropped and repaint a block that is clicked
//   while no block is picked. It only reports clicks, the BuildingEditor acts on them.
//
//   var sidebar = new BuildingSidebar();
//   sidebar.Setup(session);
//   sidebar.PatternPicked += pattern => session.PickPattern(pattern);
//   sidebar.PaintPicked   += paint => session.PickPaint(paint);
//   sidebar.TemplatesOpened += () => picker.Open(session.Draft);

//
//   Chapters:
//     BUILD SIDEBAR   Function card, the templates menu, pattern swatches and paint swatches.
//     REFRESH         Texts, the templates of the function shown and the picked pattern and paint.
//

using System;
using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class BuildingSidebar : VBoxContainer
{
    // ===========================================================================
    // ============================   BUILD SIDEBAR   ============================
    // ===========================================================================
    //
    //   PatternPicked   A pattern swatch was clicked, 0 is plain.
    //   PaintPicked     A paint swatch was clicked, BlockCatalog.DEFAULT_PAINT for the first one.
    //   TemplatesOpened The button "Templates" was pressed: the editor opens the template window.
    //   Setup           Builds every widget and follows the session.
    //   DrawPattern     The pattern of a swatch, the same layout as shaders/building_paint.gdshader.
    //

    public event Action<int> PatternPicked;
    public event Action<int> PaintPicked;
    public event Action TemplatesOpened;

    private const int WIDTH = 320;
    private const int SWATCH_PIXELS = 34;
    private const int SWATCH_COLUMNS = 6;
    private const int PATTERN_PIXELS = 46;
    private static readonly Color TOOL_ACCENT = new(0.6f, 0.7f, 0.9f);
    private static readonly Color TEMPLATE_ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color PATTERN_BASE = new(0.72f, 0.55f, 0.38f);
    private static readonly Color PATTERN_TONE = new(0.9f, 0.88f, 0.8f);

    private readonly List<(int Paint, Button Swatch)> _swatches = new();
    private readonly List<Button> _patterns = new();
    private BuildingEditorSession _session;
    private Label _kindName;
    private Label _cost;
    private Label _effect;
    private Label _note;
    private Label _patternLabel;
    private Label _paintLabel;
    private Button _defaultPaint;
    private Button _templates;

    /// <summary>Builds the sidebar, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting: function shown, pattern and paint.</param>
    public void Setup(BuildingEditorSession session)
    {
        _session          = session;
        CustomMinimumSize = new Vector2(WIDTH, 0);
        AddThemeConstantOverride("separation", 8);

        // Function card: name, cost, effect and the note that blocks are looks only
        _kindName = CreatureEditorUi.NewLabel(28);
        GameFonts.UseDisplay(_kindName);
        _cost   = CreatureEditorUi.NewLabel(16);
        _effect = CreatureEditorUi.NewLabel(16);
        _note   = CreatureEditorUi.NewLabel(13);
        foreach (Label label in new[] { _kindName, _cost, _effect, _note })
        {
            label.AutowrapMode      = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(WIDTH, 0);
            AddChild(label);
        }
        _note.Modulate = new Color(1f, 1f, 1f, 0.7f);

        // Templates: one button, the editor opens the template window with previews
        _templates = new Button
        {
            CustomMinimumSize = new Vector2(WIDTH, 38),
            FocusMode         = FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        _templates.AddThemeFontSizeOverride("font_size", 16);
        EditorStyle.ApplyButton(_templates, TEMPLATE_ACCENT);
        _templates.Pressed += () => TemplatesOpened?.Invoke();
        AddChild(_templates);

        // Patterns: one drawn swatch each, the first is plain
        _patternLabel = CreatureEditorUi.NewLabel(16);
        AddChild(_patternLabel);
        var patterns = new HBoxContainer();
        patterns.AddThemeConstantOverride("separation", 8);
        AddChild(patterns);
        for (int i = 0; i < BlockCatalog.PATTERN_COUNT; i++)
        {
            int pattern = i;
            var swatch  = new Button
            {
                CustomMinimumSize = Vector2.One * PATTERN_PIXELS,
                FocusMode         = FocusModeEnum.None,
                ToggleMode        = true,
                ClipContents      = true,
            };
            EditorStyle.ApplyTile(swatch, TOOL_ACCENT);
            swatch.Draw    += () => DrawPattern(swatch, pattern);
            swatch.Pressed += () => PatternPicked?.Invoke(pattern);
            patterns.AddChild(swatch);
            _patterns.Add(swatch);
        }

        // Paints: the first button gives every block its own paint back
        _paintLabel = CreatureEditorUi.NewLabel(16);
        AddChild(_paintLabel);
        _defaultPaint = NewButton(15);
        _defaultPaint.ToggleMode = true;
        _defaultPaint.Pressed   += () => PaintPicked?.Invoke(BlockCatalog.DEFAULT_PAINT);
        AddChild(_defaultPaint);
        var swatches = new GridContainer { Columns = SWATCH_COLUMNS };
        swatches.AddThemeConstantOverride("h_separation", 8);
        swatches.AddThemeConstantOverride("v_separation", 8);
        AddChild(swatches);
        for (int i = 0; i < BlockCatalog.PAINTS.Count; i++)
        {
            int paint  = i;
            var swatch = new Button { CustomMinimumSize = new Vector2(SWATCH_PIXELS, SWATCH_PIXELS), FocusMode = FocusModeEnum.None };
            swatch.Pressed += () => PaintPicked?.Invoke(paint);
            swatches.AddChild(swatch);
            _swatches.Add((paint, swatch));
        }

        _session.Changed          += Refresh;
        _session.SelectionChanged += Refresh;
    }

    private static Button NewButton(int fontSize)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(WIDTH, 38),
            FocusMode         = FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        EditorStyle.ApplyButton(button, TOOL_ACCENT);
        return button;
    }

    /// <summary>Draws a pattern on its swatch: 0 plain, 1 bands, 2 stripes, 3 zigzag, 4 dots, 5 checks.</summary>
    private static void DrawPattern(Button swatch, int pattern)
    {
        const float INSET = 6f;
        const int   CELLS = 3;
        var   area = new Rect2(Vector2.One * INSET, swatch.Size - Vector2.One * INSET * 2f);
        float cell = area.Size.X / CELLS;
        swatch.DrawRect(area, PATTERN_BASE);
        for (int i = 0; i < CELLS; i++)
        {
            float offset = i * cell;
            switch (pattern)
            {
                case 1:
                    swatch.DrawRect(new Rect2(area.Position + new Vector2(0f, offset), new Vector2(area.Size.X, cell * 0.4f)), PATTERN_TONE);
                    break;
                case 2:
                    swatch.DrawRect(new Rect2(area.Position + new Vector2(offset, 0f), new Vector2(cell * 0.5f, area.Size.Y)), PATTERN_TONE);
                    break;
                case 3:
                    var zigzag = new Vector2[CELLS * 2 + 1];
                    for (int k = 0; k < zigzag.Length; k++)
                    {
                        zigzag[k] = area.Position + new Vector2(k * cell * 0.5f, offset + cell * (0 == k % 2 ? 0.75f : 0.25f));
                    }
                    swatch.DrawPolyline(zigzag, PATTERN_TONE, 3f, true);
                    break;
                case 4:
                    for (int k = 0; k < CELLS; k++)
                    {
                        swatch.DrawCircle(area.Position + new Vector2(offset + cell * 0.5f, k * cell + cell * 0.5f), cell * 0.26f, PATTERN_TONE);
                    }
                    break;
                case 5:
                    for (int k = 0; k < CELLS; k++)
                    {
                        if (0 == (i + k) % 2)
                        {
                            swatch.DrawRect(new Rect2(area.Position + new Vector2(offset, k * cell), Vector2.One * cell), PATTERN_TONE);
                        }
                    }
                    break;
            }
        }
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh      Function card, the entries of the templates menu, the picked pattern and paint.
    //   EffectText   The one line that says what the function does.
    //

    public void Refresh()
    {
        BuildingDesign draft = _session.Draft;
        _kindName.Text = Tr(BuildingNames.KeyOf(draft.Kind));
        _cost.Text     = string.Format(Tr("BUILDING_COST"), draft.FoodCost, draft.WoodCost, (int)draft.Health);
        _effect.Text   = EffectText(draft);
        _note.Text     = Tr("BUILDING_COSMETIC");

        _patternLabel.Text = Tr("BUILDING_PATTERN");
        for (int i = 0; i < _patterns.Count; i++)
        {
            _patterns[i].SetPressedNoSignal(i == _session.Pattern);
        }
        _paintLabel.Text   = Tr("BUILDING_PAINT");
        _defaultPaint.Text = Tr("BUILDING_PAINT_DEFAULT");
        _defaultPaint.SetPressedNoSignal(BlockCatalog.DEFAULT_PAINT == _session.Paint);
        foreach ((int paint, Button swatch) in _swatches)
        {
            EditorStyle.ApplySwatch(swatch, BlockCatalog.PAINTS[paint], paint == _session.Paint);
        }

        // The button opens the template window (built-in templates and the library)
        _templates.Text = Tr("BUILDING_TEMPLATES");
    }

    private string EffectText(BuildingDesign draft)
    {
        return draft.Kind switch
        {
            BuildingKind.Dwelling   => string.Format(Tr("BUILDING_DWELLING_EFFECT"), draft.Villagers),
            BuildingKind.Storehouse => string.Format(Tr("BUILDING_STOREHOUSE_EFFECT"), draft.FoodPerFruit),
            BuildingKind.Market     => string.Format(Tr("BUILDING_MARKET_EFFECT"), (int)Mathf.Round(draft.TradeBonus * 100f)),
            BuildingKind.Totem      => string.Format(Tr("BUILDING_TOTEM_EFFECT"), (int)Mathf.Round(draft.RelationBonus * 100f)),
            BuildingKind.Watchtower => string.Format(Tr("BUILDING_WATCHTOWER_EFFECT"), (int)draft.Defense),
            BuildingKind.Barracks   => string.Format(Tr(BuildingNames.IsModern ? "BUILDING_BARRACKS_MODERN_EFFECT" : "BUILDING_BARRACKS_EFFECT"), draft.Warriors),
            BuildingKind.Hall       => string.Format(Tr("BUILDING_HALL_EFFECT"), (int)draft.Knowledge),
            _                       => "",
        };
    }
}
