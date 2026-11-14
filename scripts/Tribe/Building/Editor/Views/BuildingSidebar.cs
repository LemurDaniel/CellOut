// NOTE:
//   The right column of the village builder: what the function shown costs and does (the
//   blocks are looks only), its templates as buttons and the paints as swatches. It only
//   reports clicks, the BuildingEditor acts on them.
//
//   var sidebar = new BuildingSidebar();
//   sidebar.Setup(session);
//   sidebar.TemplatePressed += index => session.ApplyTemplate(index);
//   sidebar.PaintPicked     += paint => session.PickPaint(paint);

//
//   Chapters:
//     BUILD SIDEBAR   Function card, template buttons and paint swatches.
//     REFRESH         Texts, templates of the function shown and the picked paint.
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
    //   TemplatePressed   A template button was clicked, with its index.
    //   PaintPicked       A swatch was clicked, BlockCatalog.DEFAULT_PAINT for the first one.
    //   Setup             Builds every widget and follows the session.
    //

    public event Action<int> TemplatePressed;
    public event Action<int> PaintPicked;

    private const int WIDTH = 320;
    private const int TEMPLATE_COLUMNS = 2;
    private const int SWATCH_PIXELS = 34;
    private const int SWATCH_COLUMNS = 6;
    private static readonly Color ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color TOOL_ACCENT = new(0.6f, 0.7f, 0.9f);

    private readonly List<(int Paint, Button Swatch)> _swatches = new();
    private BuildingEditorSession _session;
    private Label _kindName;
    private Label _cost;
    private Label _effect;
    private Label _note;
    private Label _templatesLabel;
    private GridContainer _templates;
    private Label _paintLabel;
    private Button _defaultPaint;
    private BuildingKind? _templatesOf;

    /// <summary>Builds the sidebar, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting: function shown, turn and paint.</param>
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

        // Templates of the function shown, rebuilt when the function changes
        _templatesLabel = CreatureEditorUi.NewLabel(16);
        AddChild(_templatesLabel);
        _templates = new GridContainer { Columns = TEMPLATE_COLUMNS };
        _templates.AddThemeConstantOverride("h_separation", 8);
        _templates.AddThemeConstantOverride("v_separation", 6);
        AddChild(_templates);

        // Paints: the first swatch gives every block its own paint back
        _paintLabel = CreatureEditorUi.NewLabel(16);
        AddChild(_paintLabel);
        _defaultPaint = NewButton(TOOL_ACCENT, 15, WIDTH);
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

    private static Button NewButton(Color accent, int fontSize, int width)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(width, 38),
            FocusMode         = FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        EditorStyle.ApplyButton(button, accent);
        return button;
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh            Function card, templates and the picked paint.
    //   EffectText         The one line that says what the function does.
    //   RebuildTemplates   One button per template of the function shown.
    //

    public void Refresh()
    {
        BuildingDesign draft = _session.Draft;
        _kindName.Text = Tr($"BUILDING_{draft.Kind}".ToUpperInvariant());
        _cost.Text     = string.Format(Tr("BUILDING_COST"), draft.FoodCost, draft.WoodCost, (int)draft.Health);
        _effect.Text   = EffectText(draft);
        _note.Text     = Tr("BUILDING_COSMETIC");

        _templatesLabel.Text = Tr("BUILDING_TEMPLATES");
        RebuildTemplates(draft.Kind);

        _paintLabel.Text   = Tr("BUILDING_PAINT");
        _defaultPaint.Text = Tr("BUILDING_PAINT_DEFAULT");
        _defaultPaint.SetPressedNoSignal(BlockCatalog.DEFAULT_PAINT == _session.Paint);
        foreach ((int paint, Button swatch) in _swatches)
        {
            EditorStyle.ApplySwatch(swatch, BlockCatalog.PAINTS[paint], paint == _session.Paint);
        }
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
            _                       => "",
        };
    }

    private void RebuildTemplates(BuildingKind kind)
    {
        if (kind != _templatesOf)
        {
            _templatesOf = kind;
            foreach (Node child in _templates.GetChildren())
            {
                child.QueueFree();
            }
            for (int i = 0; i < BuildingTemplates.Count(kind); i++)
            {
                int    index  = i;
                Button button = NewButton(ACCENT, 15, (WIDTH - 8) / TEMPLATE_COLUMNS);
                button.SetMeta("template", i);
                button.Pressed += () => TemplatePressed?.Invoke(index);
                _templates.AddChild(button);
            }
        }
        foreach (Node child in _templates.GetChildren())
        {
            if (child is Button button && !button.IsQueuedForDeletion())
            {
                button.Text = Tr(BuildingTemplates.NameKey(kind, (int)button.GetMeta("template")));
            }
        }
    }
}
