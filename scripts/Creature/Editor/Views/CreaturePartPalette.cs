// NOTE:
//   Tile palette of the creature editor: one floating icon tile per part of the shown
//   tab that the body type may use, with name, cost and weight. A tile is selected by a
//   click or dragged onto the clay. A view: it raises PartPicked and HoverChanged, the
//   drag itself is started by the function handed to Setup (CreatureDropGhost.StartDrag).
//
//   var palette = new CreaturePartPalette();
//   palette.Setup(session, ghost.StartDrag);
//   palette.PartPicked += Select;
//   frame.AddWorkbench(palette, stage, stats);
//   palette.Rebuild();        // type, tab or color changed
//   palette.RefreshTiles();   // points or selection changed

//
//   Chapters:
//     PART PALETTE   Builds the tiles and keeps texts, pressed and affordable state.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreaturePartPalette : ScrollContainer
{
    // ===========================================================================
    // ============================   PART PALETTE   =============================
    // ===========================================================================
    //
    //   PartPicked     Raised when a tile is pressed (its kind) or unpressed (null).
    //   HoverChanged   Raised when the cursor enters or leaves a tile.
    //   Hovered        The part of the tile under the cursor, null for none.
    //   Setup          Scroll area with the tile grid.
    //   ClearHover     Forgets the hovered tile.
    //   Rebuild        One icon tile per part of the shown tab the body type may use, popping in.
    //   RefreshTiles   Name, cost, weight, pressed and affordable state of every tile.
    //

    private const int TILE_ICON_PIXELS = 168;           // wider than the spot: the part reaches over its edges
    private const float TILE_ICON_TOP = -30f;           // and above it, under the texts of the row above
    private const float TILE_BOB_PIXELS = 5f;
    private const int PALETTE_COLUMNS = 2;
    private static readonly Vector2 TILE_SIZE = new(150, 140);
    private const float TILE_HOVER_SCALE = 1.06f;
    private const float TILE_POP_DELAY = 0.12f;
    private const float TILE_POP_STAGGER = 0.03f;

    private sealed record PaletteTile(CreaturePartInfo Info, Button Button, Control Icon, Label Name, Label Cost);

    public event System.Action<CreaturePartKind?> PartPicked;
    public event System.Action HoverChanged;

    private CreatureEditorSession _session;
    private System.Func<CreaturePartInfo, Control, Variant> _startDrag;
    private GridContainer _grid;
    private readonly List<PaletteTile> _tiles = new();

    public CreaturePartInfo Hovered { get; private set; }

    /// <summary>Builds the scroll area, call once before the palette enters the tree.</summary>
    /// <param name="session">(Required) The sitting: body type, tab, color, points and selection.</param>
    /// <param name="startDrag">(Required) Starts the drag of a tile: part and the control showing the drag preview in, drag data out.</param>
    /// <example><code>palette.Setup(session, ghost.StartDrag);</code></example>
    public void Setup(CreatureEditorSession session, System.Func<CreaturePartInfo, Control, Variant> startDrag)
    {
        _session             = session;
        _startDrag           = startDrag;
        CustomMinimumSize    = new Vector2(PALETTE_COLUMNS * (TILE_SIZE.X + 10) + 28, 0);
        HorizontalScrollMode = ScrollMode.Disabled;

        var paletteMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            paletteMargin.AddThemeConstantOverride(side, 8);
        }
        AddChild(paletteMargin);
        _grid = new GridContainer { Columns = PALETTE_COLUMNS, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 10);
        _grid.AddThemeConstantOverride("v_separation", 10);
        paletteMargin.AddChild(_grid);
    }

    /// <summary>Forgets the hovered tile, called when a sitting begins or the tab changes.</summary>
    public void ClearHover()
    {
        Hovered = null;
    }

    /// <summary>Rebuilt on every Open, type choice, tab switch and recoloring: type and tab decide which parts are offered.</summary>
    public void Rebuild()
    {
        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        _tiles.Clear();
        if (_session.IsChoosingType)
        {
            return;
        }

        var   group  = new ButtonGroup { AllowUnpress = true };
        Color accent = CreatureEditorUi.TypeAccent(_session.Draft.Type);
        foreach (CreaturePartInfo info in CreatureCatalog.ForCategory(_session.Draft.Type, _session.Category))
        {
            var button = new Button
            {
                CustomMinimumSize = TILE_SIZE,
                ToggleMode        = true,
                ButtonGroup       = group,
                FocusMode         = FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };
            EditorStyle.ApplyFloating(button, accent);
            _grid.AddChild(button);

            // The part floats free: bigger than its spot, reaching above it and under the texts, bobbing
            Control icon = CreatureIcon.New(CreatureIcon.DesignFor(info, _session.Draft.Type), _session.DraftColor, TILE_ICON_PIXELS, isTurning: false, isPartsOnly: true);
            icon.Position = new Vector2((TILE_SIZE.X - TILE_ICON_PIXELS) * 0.5f, TILE_ICON_TOP);
            button.AddChild(icon);
            EditorStyle.Bob(icon, TILE_BOB_PIXELS, _tiles.Count * 0.23f);

            // Name and cost at the bottom, drawn above every icon (also the ones of the next row)
            var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End, ZIndex = 1 };
            stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 2);
            stack.AddThemeConstantOverride("separation", -2);
            button.AddChild(stack);
            Label name = CreatureEditorUi.NewLabel(16);
            Label cost = CreatureEditorUi.NewLabel(13);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            cost.HorizontalAlignment = HorizontalAlignment.Center;
            cost.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
            stack.AddChild(name);
            stack.AddChild(cost);

            // Switching tiles unpresses the old one, that must not clear the new selection
            CreaturePartKind kind = info.Kind;
            button.Toggled += isPressed =>
            {
                if (isPressed) PartPicked?.Invoke(kind);
                else if (kind == _session.Selected) PartPicked?.Invoke(null);
            };
            button.SetDragForwarding(Callable.From<Vector2, Variant>(_ => _startDrag(info, _grid)), new Callable(), new Callable());
            button.MouseEntered += () =>
            {
                Hovered = info;
                HoverChanged?.Invoke();
                EditorStyle.ScaleTo(button, TILE_HOVER_SCALE);
            };
            button.MouseExited += () =>
            {
                Hovered = null;
                HoverChanged?.Invoke();
                EditorStyle.ScaleTo(button, 1f);
            };
            EditorStyle.PopIn(button, TILE_POP_DELAY + _tiles.Count * TILE_POP_STAGGER);
            _tiles.Add(new PaletteTile(info, button, icon, name, cost));
        }
    }

    /// <summary>Updates name, cost and weight of every tile, presses the selected one and fades the unaffordable ones.</summary>
    public void RefreshTiles()
    {
        foreach (PaletteTile tile in _tiles)
        {
            int  cost        = _session.CostOf(tile.Info);
            bool isAvailable = cost <= _session.Points;
            tile.Name.Text       = Tr(tile.Info.NameKey);
            tile.Cost.Text       = $"{cost} EP ·{string.Format(Tr("CREATURE_UNIT_KG"), tile.Info.Weight.ToString("0.0"))}";
            tile.Button.Disabled = !isAvailable;
            tile.Button.SetPressedNoSignal(tile.Info.Kind == _session.Selected);
            CreatureEditorUi.SetAlpha(tile.Icon, isAvailable ? 1f : 0.3f);
            CreatureEditorUi.SetAlpha(tile.Name, isAvailable ? 1f : 0.45f);
        }
    }
}
