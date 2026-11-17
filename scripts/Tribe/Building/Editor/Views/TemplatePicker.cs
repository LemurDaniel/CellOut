// NOTE:
//   The template window of the village builder (wish of the user 2026-11-17): a window
//   over the builder that shows every template of the function as a card with its live 3D
//   preview over its name, in three shelves: the village, the modern town and the player's
//   own library (Building/BlueprintLibrary.cs, shared by every game). A click on a card
//   reports it (Picked) and closes the window. Below, the current draft can be saved into
//   the library under a name; own cards carry a button that deletes them. Escape or the
//   close button leave without a choice.
//
//   var picker = new TemplatePicker();
//   picker.Picked += design => session.ApplyDesign(design);
//   root.AddChild(picker);
//   picker.Open(session.Draft);

//
//   Chapters:
//     TEMPLATE WINDOW   The window, its shelves of cards and the save row.
//

using System;
using Godot;

namespace CellOut;

public partial class TemplatePicker : Control
{
    // ===========================================================================
    // ===========================   TEMPLATE WINDOW   ===========================
    // ===========================================================================
    //
    //   Picked   A card was clicked, with the design it shows.
    //   IsOpen   Whether the window shows.
    //   Open     Shows the templates of the function of a draft.
    //   OpenLibrary   The same outside the builder, with tabs for every function.
    //   Closed   The window was closed, with or without a choice.
    //   Close    Hides the window.
    //   _Input   Escape closes the window before the builder sees the key.
    //

    public event Action<BuildingDesign> Picked;
    public event Action Closed;

    private const int CARD_COLUMNS = 5;
    private const int CARD_PIXELS = 150;             // edge of the preview
    private const int CARD_NAME_HEIGHT = 36;
    private const int CARD_GAP = 12;
    private const int DELETE_PIXELS = 26;
    private static readonly Vector2 WINDOW_SIZE = new(960, 640);
    private static readonly Color CARD_ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color OWN_ACCENT = new(0.55f, 0.8f, 0.6f);
    private static readonly Color DIM_COLOR = new(0f, 0f, 0f, 0.55f);
    private static readonly Color WINDOW_COLOR = new(0.05f, 0.08f, 0.12f, 0.96f);

    private readonly BuildingDesign _draft = new(BuildingKind.Dwelling);
    private readonly System.Collections.Generic.List<(BuildingKind Kind, Button Tab)> _kindTabs = new();
    private Func<BuildingKind, BuildingDesign> _designOf;      // set in the library of the pause menu: the tabs switch the function
    private HBoxContainer _kinds;
    private Label _title;
    private VBoxContainer _shelves;
    private LineEdit _name;
    private Button _save;
    private Button _close;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Visible     = false;
        MouseFilter = MouseFilterEnum.Stop;          // nothing behind the window takes a click
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var dim = new ColorRect { Color = DIM_COLOR, MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var window = new PanelContainer { CustomMinimumSize = WINDOW_SIZE };
        window.AddThemeStyleboxOverride("panel", EditorStyle.Pill(WINDOW_COLOR, CARD_ACCENT, 0.06f));
        window.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        window.GrowHorizontal = GrowDirection.Both;
        window.GrowVertical   = GrowDirection.Both;
        AddChild(window);
        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 18);
        }
        window.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        // Head: the title and the close button
        var head = new HBoxContainer();
        column.AddChild(head);
        _title = CreatureEditorUi.NewLabel(28);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        GameFonts.UseDisplay(_title);
        head.AddChild(_title);
        _close = NewButton(new Vector2(120, 38), CARD_ACCENT);
        _close.Pressed += Close;
        head.AddChild(_close);

        // Function tabs, only in the library of the pause menu (the builder has its own)
        _kinds = new HBoxContainer { Visible = false };
        _kinds.AddThemeConstantOverride("separation", 6);
        column.AddChild(_kinds);
        foreach (BuildingKind kind in Enum.GetValues<BuildingKind>())
        {
            BuildingKind shown = kind;
            Button tab = NewButton(new Vector2(0, 34), OWN_ACCENT);
            tab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tab.ToggleMode          = true;
            tab.AddThemeFontSizeOverride("font_size", 13);
            tab.Pressed += () => Show(_designOf(shown));
            _kinds.AddChild(tab);
            _kindTabs.Add((kind, tab));
        }

        // Shelves: they scroll when the cards do not fit
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _shelves = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _shelves.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_shelves);

        // Save row: the current draft goes into the library under a name
        var saveRow = new HBoxContainer();
        saveRow.AddThemeConstantOverride("separation", 10);
        column.AddChild(saveRow);
        _name = new LineEdit { MaxLength = BlueprintLibrary.MAX_NAME_LENGTH, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 38) };
        _name.AddThemeFontSizeOverride("font_size", 16);
        _name.TextSubmitted += _ => SaveDraft();
        saveRow.AddChild(_name);
        _save = NewButton(new Vector2(260, 38), OWN_ACCENT);
        _save.Pressed += SaveDraft;
        saveRow.AddChild(_save);
    }

    private static Button NewButton(Vector2 size, Color accent)
    {
        var button = new Button { CustomMinimumSize = size, FocusMode = FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        button.AddThemeFontSizeOverride("font_size", 16);
        EditorStyle.ApplyButton(button, accent);
        return button;
    }

    /// <summary>Shows the templates of the function of a draft: village, modern, the player's own.</summary>
    /// <param name="draft">(Required) The draft on the stage, copied: "save" puts it into the library.</param>
    public void Open(BuildingDesign draft)
    {
        _designOf      = null;
        _kinds.Visible = false;
        Show(draft);
    }

    /// <summary>Shows the library outside the builder (the pause menu): a row of tabs switches the function, "save" stores the blueprint the tribe builds of it.</summary>
    /// <param name="designOf">(Required) The current blueprint of a function.</param>
    /// <example><code>picker.OpenLibrary(tribe.DesignOf);</code></example>
    public void OpenLibrary(Func<BuildingKind, BuildingDesign> designOf)
    {
        _designOf      = designOf;
        _kinds.Visible = true;
        Show(designOf(_draft.Kind));
    }

    private void Show(BuildingDesign draft)
    {
        _draft.CopyFrom(draft);
        _name.Text = "";
        Fill();
        Visible = true;
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }
        Visible = false;
        _name.ReleaseFocus();
        Closed?.Invoke();
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /*
      The shelves, rebuilt on every opening and after saving or deleting:
      - village: the first BuildingTemplates.VillageCount templates of the function,
      - modern: the ones after them (none for a function without modern templates),
      - own: the entries of the library for the function, each with a delete button.
    */
    private void Fill()
    {
        BuildingKind kind = _draft.Kind;
        _title.Text = $"{Tr("BUILDING_TEMPLATES")} · {Tr(BuildingNames.KeyOf(kind))}";
        _close.Text = Tr("TEMPLATES_CLOSE");
        _save.Text  = Tr("TEMPLATES_SAVE");
        _name.PlaceholderText = Tr("TEMPLATES_NAME");
        foreach ((BuildingKind tabKind, Button tab) in _kindTabs)
        {
            tab.Text = Tr(BuildingNames.KeyOf(tabKind));
            tab.SetPressedNoSignal(tabKind == kind);
        }
        foreach (Node child in _shelves.GetChildren())
        {
            child.QueueFree();
        }

        int village = BuildingTemplates.VillageCount(kind);
        GridContainer shelf = AddShelf("BLOCK_ERA_VILLAGE");
        for (int i = 0; i < village; i++)
        {
            AddCard(shelf, Tr(BuildingTemplates.NameKey(kind, i)), BuildingTemplates.Build(kind, i), CARD_ACCENT, null);
        }
        if (BuildingTemplates.Count(kind) > village)
        {
            shelf = AddShelf("BLOCK_ERA_MODERN");
            for (int i = village; i < BuildingTemplates.Count(kind); i++)
            {
                AddCard(shelf, Tr(BuildingTemplates.NameKey(kind, i)), BuildingTemplates.Build(kind, i), CARD_ACCENT, null);
            }
        }
        shelf = AddShelf("TEMPLATES_OWN");
        foreach (BlueprintLibrary.Entry entry in BlueprintLibrary.Of(kind))
        {
            AddCard(shelf, entry.Name, entry.Design, OWN_ACCENT, entry);
        }
    }

    private GridContainer AddShelf(string titleKey)
    {
        Label title = CreatureEditorUi.NewLabel(18);
        title.Text = Tr(titleKey);
        _shelves.AddChild(title);
        var shelf = new GridContainer { Columns = CARD_COLUMNS };
        shelf.AddThemeConstantOverride("h_separation", CARD_GAP);
        shelf.AddThemeConstantOverride("v_separation", CARD_GAP);
        _shelves.AddChild(shelf);
        return shelf;
    }

    /// <summary>One card: the live preview over the name, an own one with its delete button in the corner.</summary>
    private void AddCard(GridContainer shelf, string name, BuildingDesign design, Color accent, BlueprintLibrary.Entry own)
    {
        var card = new Button
        {
            CustomMinimumSize = new Vector2(CARD_PIXELS + 16, CARD_PIXELS + CARD_NAME_HEIGHT + 8),
            FocusMode         = FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        EditorStyle.ApplyTile(card, accent);
        var icon = new BuildingIcon(CARD_PIXELS) { Position = new Vector2(8, 4) };
        icon.Display(design);
        card.AddChild(icon);
        Label label = CreatureEditorUi.NewLabel(14);
        label.Text                = name;
        label.Position            = new Vector2(4, CARD_PIXELS + 4);
        label.Size                = new Vector2(CARD_PIXELS + 8, CARD_NAME_HEIGHT);
        label.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment   = VerticalAlignment.Center;
        label.MouseFilter         = MouseFilterEnum.Ignore;
        card.AddChild(label);
        card.Pressed += () =>
        {
            Picked?.Invoke(design);
            Close();
        };
        if (null != own)
        {
            Button delete = NewButton(Vector2.One * DELETE_PIXELS, new Color(1f, 0.45f, 0.35f));
            delete.Text        = "×";
            delete.TooltipText = Tr("TEMPLATES_DELETE");
            delete.Position    = new Vector2(CARD_PIXELS + 16 - DELETE_PIXELS - 4, 4);
            delete.Pressed    += () =>
            {
                BlueprintLibrary.Remove(own);
                Fill();
            };
            card.AddChild(delete);
        }
        shelf.AddChild(card);
    }

    /// <summary>The draft goes into the library under the typed name, without one under the name of its function and a number.</summary>
    private void SaveDraft()
    {
        string name = _name.Text.Trim();
        if (0 == name.Length)
        {
            name = $"{Tr(BuildingNames.KeyOf(_draft.Kind))} {BlueprintLibrary.Of(_draft.Kind).Count + 1}";
        }
        if (null != BlueprintLibrary.Add(name, _draft))
        {
            _name.Text = "";
            _name.ReleaseFocus();
            Fill();
        }
    }
}
