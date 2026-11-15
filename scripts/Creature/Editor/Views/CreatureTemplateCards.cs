// NOTE:
//   Template cards of the first shaping: the library of ready-made animals
//   (Design/CreatureTemplates.cs, one file per animal in data/creatures) as a scrolling
//   grid of cards, each with a live turning creature,
//   its name and what its parts cost. A template the points do not reach is greyed out.
//   A view: a click raises TemplateChosen, the cards only show while the session waits
//   for the choice.
//
//   var cards = new CreatureTemplateCards();
//   cards.Setup(session);
//   cards.TemplateChosen += template => ChooseTemplate(template);
//   frame.AddRow(cards);
//   cards.Rebuild();     // on every Open

//
//   Chapters:
//     TEMPLATE CARDS   Builds the cards and keeps their texts translated.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreatureTemplateCards : ScrollContainer
{
    // ===========================================================================
    // ===========================   TEMPLATE CARDS   ============================
    // ===========================================================================
    //
    //   TemplateChosen  Raised when a card is clicked.
    //   Setup           Takes the session, the grid fills the height of the panel and scrolls when the library outgrows it.
    //   Rebuild         One card per template with a live creature, first shaping only.
    //   Refresh         Shows the cards during the choice, translates them and greys out the dear ones.
    //

    private const int CARD_COLUMNS = 7;
    private const int ICON_PIXELS = 128;
    private const float CARD_HEIGHT = 250f;          // icon, name and price; more rows than fit scroll
    private const float CARD_POP_DELAY = 0.1f;       // the same start as the palette tiles
    private const float CARD_POP_STAGGER = 0.04f;
    private static readonly Color COST_COLOR = new(0.98f, 0.9f, 0.45f);
    private static readonly Color DEAR_COLOR = new(0.95f, 0.45f, 0.4f);

    public event System.Action<CreatureTemplate> TemplateChosen;

    private CreatureEditorSession _session;
    private GridContainer _grid;
    private readonly List<(CreatureTemplate Template, int Cost, Button Card, Label Name, Label Price)> _cards = new();

    /// <summary>Call once before the cards enter the tree.</summary>
    /// <param name="session">(Required) The sitting: its phase, its points and the color of the draft.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session             = session;
        SizeFlagsVertical    = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;
        _grid = new GridContainer { Columns = CARD_COLUMNS, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        AddChild(_grid);
    }

    /// <summary>Rebuilt on every Open: a template without a color of its own shows in the color of the player.</summary>
    public void Rebuild()
    {
        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        _cards.Clear();
        if (!_session.IsChoosingType)
        {
            return;
        }

        int index = 0;
        foreach (CreatureTemplate template in CreatureTemplates.All)
        {
            CreatureDesign body   = template.Build();
            Color          accent = CreatureEditorUi.TypeAccent(body.Type);
            var card = new Button
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize   = new Vector2(0f, CARD_HEIGHT),
                FocusMode           = FocusModeEnum.None,
                AutoTranslateMode   = AutoTranslateModeEnum.Disabled,
            };
            EditorStyle.ApplyTile(card, accent);
            _grid.AddChild(card);

            var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 8);
            stack.AddThemeConstantOverride("separation", 2);
            card.AddChild(stack);
            Control icon = CreatureIcon.New(body, body.Color ?? _session.DraftColor, ICON_PIXELS, isTurning: true);
            icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            stack.AddChild(icon);
            Label name = CreatureEditorUi.NewLabel(18);
            name.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
            Label price = CreatureEditorUi.NewLabel(14);
            foreach (Label label in new[] { name, price })
            {
                label.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(label);
            }
            _cards.Add((template, CreatureTemplates.CostOf(body), card, name, price));

            card.Pressed      += () => TemplateChosen?.Invoke(template);
            card.MouseEntered += () => EditorStyle.ScaleTo(card, card.Disabled ? 1f : 1.04f);
            card.MouseExited  += () => EditorStyle.ScaleTo(card, 1f);
            EditorStyle.PopIn(card, CARD_POP_DELAY + index * CARD_POP_STAGGER);
            index++;
        }
    }

    /// <summary>Shows the cards while the choice is open, translates name and price and greys out what the points do not reach.</summary>
    public void Refresh()
    {
        Visible = _session.IsChoosingType;
        foreach ((CreatureTemplate template, int cost, Button card, Label name, Label price) in _cards)
        {
            bool isDear = cost > _session.Points;
            name.Text   = Tr(template.NameKey);
            price.Text  = 0 == cost ? Tr("CREATURE_TEMPLATE_FREE") : string.Format(Tr("CREATURE_TEMPLATE_COST"), cost);
            price.AddThemeColorOverride("font_color", isDear ? DEAR_COLOR : COST_COLOR);
            card.Disabled = isDear;
            card.Modulate = isDear ? new Color(1f, 1f, 1f, 0.45f) : Colors.White;
        }
    }
}
