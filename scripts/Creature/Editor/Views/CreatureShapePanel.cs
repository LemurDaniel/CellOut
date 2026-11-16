// NOTE:
//   The sculpt mode of the creature editor, the panel left of the turntable: the own parts
//   of the player ("My parts"). Each is a piece of clay with a spine of its own, shaped on
//   the turntable with the same handles as the body. The panel lists them, adds a new one,
//   deletes the shown one and puts it on the animal. A view: it only reports clicks (events)
//   and shows the session (Refresh). A plain node, the editor places its panel.
//
//   var shapes = new CreatureShapePanel();
//   shapes.Setup(session, paletteWidth);
//   shapes.PlacePressed += () => session.PlacePickedShape();
//   AddChild(shapes);

//
//   Chapters:
//     SHAPE PANEL   Builds the list and its buttons and keeps them in step with the session.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreatureShapePanel : Node
{
    // ===========================================================================
    // =============================   SHAPE PANEL   =============================
    // ===========================================================================
    //
    //   ShapePicked     A part of the list was clicked (its id).
    //   NewPressed      "New part" was clicked.
    //   PlacePressed    "Put on the animal" was clicked.
    //   RemovePressed   "Delete" was clicked.
    //   Panel           The panel, placed by the editor.
    //   Setup           Headline, hint, the list and the three buttons.
    //   Refresh         The panel only in the sculpt mode; one pill per own part, the shown one lit.
    //

    public event System.Action<int> ShapePicked;
    public event System.Action NewPressed;
    public event System.Action PlacePressed;
    public event System.Action RemovePressed;

    /// <summary>Left of the turntable in the sculpt mode.</summary>
    public CreaturePanel Panel { get; private set; }

    private CreatureEditorSession _session;
    private Label _hint;
    private HFlowContainer _list;
    private string _listKey = "";                    // the ids the pills were built for
    private readonly List<(int Id, Button Pill)> _pills = new();
    private Button _newButton;
    private Button _placeButton;
    private Button _removeButton;

    /// <summary>Builds the panel, call once before the editor places it.</summary>
    /// <param name="session">(Required) The sitting: the own shapes of the draft and the one on the turntable.</param>
    /// <param name="width">(Required) Width of the panel: as wide as the part palette it replaces.</param>
    public void Setup(CreatureEditorSession session, float width)
    {
        _session = session;
        Panel    = new CreaturePanel();
        Panel.Setup(width);
        Panel.AddHeadline("CREATURE_SHAPES");
        _hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hint.AddThemeFontSizeOverride("font_size", 13);
        Panel.Sections.AddChild(_hint);
        _list = new HFlowContainer();
        _list.AddThemeConstantOverride("h_separation", 6);
        _list.AddThemeConstantOverride("v_separation", 6);
        Panel.Sections.AddChild(_list);

        _newButton    = CreatureEditorUi.NewPillButton(new Vector2(0, 36), 15);
        _placeButton  = CreatureEditorUi.NewPillButton(new Vector2(0, 40), 16);
        _removeButton = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _newButton.Pressed    += () => NewPressed?.Invoke();
        _placeButton.Pressed  += () => PlacePressed?.Invoke();
        _removeButton.Pressed += () => RemovePressed?.Invoke();
        foreach (Button button in new[] { _newButton, _placeButton, _removeButton })
        {
            Panel.Sections.AddChild(button);
        }
    }

    /// <summary>Shows the own parts of the draft, only in the sculpt mode.</summary>
    public void Refresh()
    {
        bool isShown = CreatureEditorMode.Sculpt == _session.Mode && !_session.IsChoosingType;
        Panel.Visible = isShown;
        if (!isShown)
        {
            return;
        }
        Panel.Translate();
        Color accent = CreatureEditorUi.TypeAccent(_session.Draft.Type);
        _hint.Text         = Tr("CREATURE_SHAPE_HINT");
        _newButton.Text    = Tr("CREATURE_SHAPE_NEW");
        _placeButton.Text  = Tr("CREATURE_SHAPE_PLACE");
        _removeButton.Text = Tr("CREATURE_SHAPE_REMOVE");
        EditorStyle.ApplyButton(_newButton, EditorStyle.RIM_COLOR);
        EditorStyle.ApplyButton(_placeButton, accent);
        EditorStyle.ApplyButton(_removeButton, EditorStyle.RIM_COLOR);
        _placeButton.Disabled  = 0 == _session.PickedShape;
        _removeButton.Disabled = 0 == _session.PickedShape;

        // One pill per own part, rebuilt only when the list changes
        var ids = new List<int>();
        foreach (CustomShape shape in _session.Draft.Shapes)
        {
            ids.Add(shape.Id);
        }
        string key = string.Join(",", ids);
        if (key != _listKey)
        {
            _listKey = key;
            foreach (Node pill in _list.GetChildren())
            {
                _list.RemoveChild(pill);
                pill.QueueFree();
            }
            _pills.Clear();
            foreach (int id in ids)
            {
                Button pill = CreatureEditorUi.NewPillButton(new Vector2(0, 30), 14);
                pill.Pressed += () => ShapePicked?.Invoke(id);
                _list.AddChild(pill);
                _pills.Add((id, pill));
            }
        }
        for (int i = 0; i < _pills.Count; i++)
        {
            _pills[i].Pill.Text = string.Format(Tr("CREATURE_SHAPE_ITEM"), i + 1);
            EditorStyle.ApplyButton(_pills[i].Pill, _pills[i].Id == _session.PickedShape ? accent : EditorStyle.RIM_COLOR);
        }
    }
}
