// NOTE:
//   Type cards of the first shaping: two big cards ask what the clay becomes, fish or
//   marine mammal, each with a live example creature in the color of the draft. A view:
//   a click raises TypeChosen, the cards only show while the session waits for the type.
//
//   var cards = new CreatureTypeCards();
//   cards.Setup(session);
//   cards.TypeChosen += ChooseType;
//   frame.AddRow(cards);
//   cards.Rebuild();     // on every Open

//
//   Chapters:
//     TYPE CARDS   Builds the two cards and keeps their texts translated.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreatureTypeCards : HBoxContainer
{
    // ===========================================================================
    // =============================   TYPE CARDS   ==============================
    // ===========================================================================
    //
    //   TypeChosen  Raised when a card is clicked.
    //   Setup       Takes the session, the cards fill the height of the panel.
    //   Rebuild     Two cards with a live example creature each, first shaping only.
    //   Refresh     Shows the cards during the type choice and translates them.
    //

    private const float CARD_POP_DELAY = 0.12f;      // the same start as the palette tiles
    private const float CARD_POP_STAGGER = 0.12f;

    public event System.Action<CreatureType> TypeChosen;

    private CreatureEditorSession _session;
    private readonly List<(CreatureType Type, Label Name, Label Text)> _cardTexts = new();

    /// <summary>Call once before the cards enter the tree.</summary>
    /// <param name="session">(Required) The sitting: its phase and the color of the draft.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session          = session;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 18);
    }

    /// <summary>Rebuilt on every Open, the example creatures take the player color.</summary>
    public void Rebuild()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        _cardTexts.Clear();
        if (!_session.IsChoosingType)
        {
            return;
        }

        int index = 0;
        foreach (CreatureType type in new[] { CreatureType.Fish, CreatureType.MarineMammal })
        {
            Color accent = CreatureEditorUi.TypeAccent(type);
            var card = new Button
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical   = SizeFlags.ExpandFill,
                FocusMode           = FocusModeEnum.None,
                AutoTranslateMode   = AutoTranslateModeEnum.Disabled,
            };
            EditorStyle.ApplyTile(card, accent);
            AddChild(card);

            var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = AlignmentMode.Center };
            stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 14);
            stack.AddThemeConstantOverride("separation", 6);
            card.AddChild(stack);
            Control icon = CreatureIcon.New(CreatureDesign.Example(type), _session.DraftColor, 220, isTurning: true);
            icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            stack.AddChild(icon);
            Label name = CreatureEditorUi.NewLabel(30);
            GameFonts.UseDisplay(name);
            name.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
            Label text = CreatureEditorUi.NewLabel(15);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            foreach (Label label in new[] { name, text })
            {
                label.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(label);
            }
            _cardTexts.Add((type, name, text));

            card.Pressed      += () => TypeChosen?.Invoke(type);
            card.MouseEntered += () => EditorStyle.ScaleTo(card, 1.03f);
            card.MouseExited  += () => EditorStyle.ScaleTo(card, 1f);
            EditorStyle.PopIn(card, CARD_POP_DELAY + index * CARD_POP_STAGGER);
            index++;
        }
    }

    /// <summary>Shows the cards while the type is open and translates name and description.</summary>
    public void Refresh()
    {
        Visible = _session.IsChoosingType;
        foreach ((CreatureType type, Label name, Label text) in _cardTexts)
        {
            name.Text = Tr(CreatureEditorUi.TypeKey(type, "NAME"));
            text.Text = Tr(CreatureEditorUi.TypeKey(type, "DESC"));
        }
    }
}
