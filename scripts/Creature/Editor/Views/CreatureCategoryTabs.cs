// NOTE:
//   Category tabs of the creature editor: one index card tab per part category on a
//   baseline, each showing the first part of its category as a small 3D icon (the name is
//   its tooltip), the shown one stands taller and tabs with placed parts carry their count.
//   A view: a click raises CategoryPressed, hidden during the type choice and in paint mode.
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
    //   AccentOf         The color of a tab: the ability tabs in the color of their diet.
    //   RefreshIcons     The 3D icon of every tab, rebuilt for a new type or color.
    //   Refresh          Tooltip and part count of every tab, the shown one pressed.
    //

    private const float TAB_HEIGHT = 46f;
    private const float TAB_ACTIVE_HEIGHT = 54f;
    private const float TAB_WIDTH = 64f;
    private const int ICON_PIXELS = 60;

    public event System.Action<CreatureCategory> CategoryPressed;

    private CreatureEditorSession _session;
    private HBoxContainer _tabs;
    private readonly List<(CreatureCategory Category, Button Tab)> _tabButtons = new();
    private readonly Dictionary<CreatureCategory, Control> _icons = new();
    private (CreatureType Type, Color Color)? _iconLook;     // what the icons were built for, a new type or color rebuilds them

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
            // Own parts come from the sculpt mode, not from a palette tab
            if (CreatureCategory.Custom == category)
            {
                continue;
            }
            // Tabs stand on the baseline, the pressed one grows taller in Refresh (no hover scale, it would lift off)
            Button tab = CreatureEditorUi.NewPillButton(new Vector2(TAB_WIDTH, TAB_HEIGHT), 13);
            tab.Alignment         = HorizontalAlignment.Right;
            tab.VerticalIconAlignment = VerticalAlignment.Bottom;
            tab.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            tab.ToggleMode        = true;
            tab.ButtonGroup       = group;
            EditorStyle.ApplyTab(tab, AccentOf(category));
            tab.Pressed += () => CategoryPressed?.Invoke(category);
            _tabs.AddChild(tab);
            _tabButtons.Add((category, tab));
        }
    }

    /// <summary>The tabs of the ability parts carry the color of their diet (green, yellow, red), the others the rim color.</summary>
    private static Color AccentOf(CreatureCategory category)
    {
        return category switch
        {
            CreatureCategory.Gentle => EditorStyle.DIET_COLORS[DietClass.Herbivore],
            CreatureCategory.Clever => EditorStyle.DIET_COLORS[DietClass.Omnivore],
            CreatureCategory.Wild   => EditorStyle.DIET_COLORS[DietClass.Carnivore],
            _                       => EditorStyle.RIM_COLOR,
        };
    }

    /// <summary>The 3D icons of the tabs: the first part of every category in the color of the draft, rebuilt when type or color change.</summary>
    private void RefreshIcons()
    {
        (CreatureType, Color) look = (_session.Draft.Type, _session.DraftColor);
        if (look == _iconLook)
        {
            return;
        }
        _iconLook = look;
        foreach ((CreatureCategory category, Button tab) in _tabButtons)
        {
            if (_icons.Remove(category, out Control old))
            {
                old.QueueFree();
            }
            CreaturePartInfo first = null;
            foreach (CreaturePartInfo info in CreatureCatalog.ForCategory(_session.Draft.Type, category))
            {
                first = info;
                break;
            }
            if (null == first)
            {
                continue;
            }
            Control icon = CreatureIcon.New(CreatureIcon.DesignFor(first, _session.Draft.Type), _session.DraftColor, ICON_PIXELS, isTurning: false, isPartsOnly: true);
            icon.MouseFilter = MouseFilterEnum.Ignore;
            icon.Position    = new Vector2((TAB_WIDTH - ICON_PIXELS) * 0.5f, -6f);
            tab.AddChild(icon);
            tab.MoveChild(icon, 0);
            _icons[category] = icon;
        }
    }

    /// <summary>The icon of every tab, its name as tooltip, the count of placed parts in its corner, the shown tab taller.</summary>
    public void Refresh()
    {
        Visible = !_session.IsChoosingType && CreatureEditorMode.Build == _session.Mode;
        if (Visible)
        {
            RefreshIcons();
        }
        foreach ((CreatureCategory category, Button tab) in _tabButtons)
        {
            int placed = 0;
            foreach (CreaturePart part in _session.Draft.Parts)
            {
                placed += category == part.Info.Category ? 1 : 0;
            }
            tab.TooltipText = Tr(CreatureEditorUi.CategoryKey(category));
            tab.Text        = placed > 0 ? placed.ToString() : "";
            tab.SetPressedNoSignal(category == _session.Category);
            tab.CustomMinimumSize = new Vector2(TAB_WIDTH, category == _session.Category ? TAB_ACTIVE_HEIGHT : TAB_HEIGHT);
        }
    }
}
