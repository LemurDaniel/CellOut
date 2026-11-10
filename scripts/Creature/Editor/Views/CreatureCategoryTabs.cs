// NOTE:
//   Category tabs of the creature editor: one index card tab per part category on a
//   baseline, the shown one stands taller and tabs with placed parts carry their count
//   ("Eyes · 2"). A view: a click raises CategoryPressed, hidden during the type choice.
//
//   var tabs = new CreatureCategoryTabs();
//   tabs.Setup(session);
//   tabs.CategoryPressed += ShowCategory;
//   frame.AddRow(tabs);

//
//   Chapters:
//     CATEGORY TABS   Builds the tab strip and keeps names, counts and the pressed tab.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreatureCategoryTabs : VBoxContainer
{
    // ===========================================================================
    // ============================   CATEGORY TABS   ============================
    // ===========================================================================
    //
    //   CategoryPressed  Raised when a tab is clicked.
    //   Setup            Tab row on a baseline, one tab per category.
    //   BuildTabs        One index card tab per part category.
    //   Refresh          Name and part count of every tab, the shown one pressed.
    //

    private const float TAB_HEIGHT = 38f;
    private const float TAB_ACTIVE_HEIGHT = 46f;

    public event System.Action<CreatureCategory> CategoryPressed;

    private CreatureEditorSession _session;
    private HBoxContainer _tabs;
    private readonly List<(CreatureCategory Category, Button Tab)> _tabButtons = new();

    /// <summary>Builds the strip, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting: shown tab and the placed parts.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session = session;

        // Category tabs as index card tabs on a baseline, hidden during the type choice
        AddThemeConstantOverride("separation", 0);
        _tabs = new HBoxContainer { CustomMinimumSize = new Vector2(0f, TAB_ACTIVE_HEIGHT) };
        _tabs.AddThemeConstantOverride("separation", 4);
        AddChild(_tabs);
        AddChild(new ColorRect
        {
            Color             = new Color(EditorStyle.RIM_COLOR, 0.8f),
            CustomMinimumSize = new Vector2(0f, 2f),
            MouseFilter       = MouseFilterEnum.Ignore,
        });
        BuildTabs();
    }

    private void BuildTabs()
    {
        var group = new ButtonGroup();
        foreach (CreatureCategory category in System.Enum.GetValues<CreatureCategory>())
        {
            // Tabs stand on the baseline, the pressed one grows taller in Refresh (no hover scale, it would lift off)
            Button tab = CreatureEditorUi.NewPillButton(new Vector2(0, TAB_HEIGHT), 18);
            tab.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            tab.ToggleMode        = true;
            tab.ButtonGroup       = group;
            EditorStyle.ApplyTab(tab, EditorStyle.RIM_COLOR);
            tab.Pressed += () => CategoryPressed?.Invoke(category);
            _tabs.AddChild(tab);
            _tabButtons.Add((category, tab));
        }
    }

    /// <summary>"Eyes" or "Eyes · 2" once parts of the tab sit on the body.</summary>
    public void Refresh()
    {
        Visible = !_session.IsChoosingType;
        foreach ((CreatureCategory category, Button tab) in _tabButtons)
        {
            int placed = 0;
            foreach (CreaturePart part in _session.Draft.Parts)
            {
                placed += category == part.Info.Category ? 1 : 0;
            }
            string name = Tr(CreatureEditorUi.CategoryKey(category));
            tab.Text = placed > 0 ? $"{name} · {placed}" : name;
            tab.SetPressedNoSignal(category == _session.Category);
            tab.CustomMinimumSize = new Vector2(0f, category == _session.Category ? TAB_ACTIVE_HEIGHT : TAB_HEIGHT);
        }
    }
}
