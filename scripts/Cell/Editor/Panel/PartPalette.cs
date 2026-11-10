// NOTE:
//   The part palette of the cell editor: a scrolling column with one header and a grid
//   of live 3D icon tiles (PartIcon) per diet class, tinted per diet, locked or
//   unaffordable ones dimmed. A click on a tile asks to select its part, hovering shows
//   its info, dragging a tile hangs the bare part at the cursor (the drop is handled by
//   the CellPreview). Reads the session, changes nothing in it: it reports through
//   events and CellEditor decides.
//
//   var palette = new PartPalette();
//   palette.Setup(session);
//   palette.SelectRequested += editor.Select;
//   body.AddChild(palette);
//   palette.Rebuild();        // on every Open
//   palette.UpdateTiles();    // after points, diet or selection changed

//
//   Chapters:
//     EVENTS AND SETUP   What the palette reports and the session it reads.
//     BUILD PALETTE      Headers and tiles, rebuilt on every Open.
//     REFRESH            Tile states and translated texts.
//     DRAG TILE          The bare part at the cursor while a tile is dragged.
//

using System;
using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class PartPalette : ScrollContainer
{
    // ===========================================================================
    // ==========================   EVENTS AND SETUP   ===========================
    // ===========================================================================
    //
    //   SelectRequested   A tile was pressed (its part) or unpressed (null).
    //   FocusChanged      The hovered or dragged part changed, the info card is stale.
    //   Focused           The dragged, else the hovered part, null for none.
    //   Setup             Hands in the session the tiles read.
    //   _Ready            The scroll area with room around the tiles.
    //

    private const int TILE_COLUMNS = 3;
    private static readonly Vector2 TILE_SIZE = new(108, 104);

    /// <summary>A tile was pressed (its part) or the selected tile was unpressed (null).</summary>
    public event Action<PartKind?> SelectRequested;

    /// <summary>The hovered or dragged part changed.</summary>
    public event Action FocusChanged;

    private CellEditSession _session;
    private VBoxContainer _column;
    private PartInfo _hovered;

    /// <summary>The part the info card should describe: the dragged one, else the hovered one, null for none.</summary>
    public PartInfo Focused => _dragged ?? _hovered;

    /// <summary>Hands in the shared session, call before the palette enters the tree.</summary>
    /// <param name="session">(Required) The session with player, points and selection.</param>
    public void Setup(CellEditSession session)
    {
        _session = session;
    }

    public override void _Ready()
    {
        CustomMinimumSize    = new Vector2(TILE_COLUMNS * (TILE_SIZE.X + 8) + 16, 0);
        HorizontalScrollMode = ScrollMode.Disabled;

        // Room around the tiles, so hover growth and glow are not clipped by the scroll area
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 6);
        }
        AddChild(margin);
        _column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(_column);
    }

    // ===========================================================================
    // ============================   BUILD PALETTE   ============================
    // ===========================================================================
    //
    //   Rebuild     One header and a grid of icon tiles per diet class, popping in.
    //   BuildTile   Tinted tile with icon, name and cost, hover grows it.
    //

    private const int TILE_ICON_PIXELS = 124;           // wider than the spot: the part reaches over its edges
    private const float TILE_ICON_TOP = -22f;           // and above it, under the texts of the row above
    private const float TILE_BOB_PIXELS = 4f;
    private const float TILE_HOVER_SCALE = 1.06f;
    private const float TILE_POP_DELAY = 0.12f;
    private const float TILE_POP_STAGGER = 0.03f;

    private sealed record PaletteTile(PartInfo Info, Button Button, PartIcon Icon, Label Name, Label Cost);

    private readonly List<PaletteTile> _tiles = new();
    private readonly List<(DietClass Diet, Label Header)> _dietHeaders = new();

    /// <summary>Rebuilt on every Open: the stage decides which parts are offered and the icons use the body color.</summary>
    public void Rebuild()
    {
        foreach (Node child in _column.GetChildren())
        {
            _column.RemoveChild(child);
            child.QueueFree();
        }
        _tiles.Clear();
        _dietHeaders.Clear();
        _hovered = null;

        var group = new ButtonGroup { AllowUnpress = true };
        foreach (DietClass diet in new[] { DietClass.Herbivore, DietClass.Omnivore, DietClass.Carnivore })
        {
            // Header: diet name and a fading line in its color
            Color accent = EditorStyle.DIET_COLORS[diet];
            var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            header.AddThemeConstantOverride("separation", 10);
            Label title = CellEditorPanel.NewLabel(17);
            title.ZIndex = 1;                                  // above the floating parts reaching up
            title.AddThemeColorOverride("font_color", accent);
            var line = new ColorRect
            {
                Color               = new Color(accent, 0.3f),
                CustomMinimumSize   = new Vector2(0f, 2f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical   = SizeFlags.ShrinkCenter,
                MouseFilter         = MouseFilterEnum.Ignore,
            };
            header.AddChild(title);
            header.AddChild(line);
            _column.AddChild(header);
            _dietHeaders.Add((diet, title));

            var grid = new GridContainer { Columns = TILE_COLUMNS };
            grid.AddThemeConstantOverride("h_separation", 8);
            grid.AddThemeConstantOverride("v_separation", 8);
            _column.AddChild(grid);

            foreach (PartInfo info in PartCatalog.All())
            {
                if (diet != info.Diet || _session.Player.Stage < info.MinStage)
                {
                    continue;
                }
                Button tile = BuildTile(info, group);
                grid.AddChild(tile);
                EditorStyle.PopIn(tile, TILE_POP_DELAY + _tiles.Count * TILE_POP_STAGGER);
            }
        }
    }

    private Button BuildTile(PartInfo info, ButtonGroup group)
    {
        var button = new Button
        {
            CustomMinimumSize = TILE_SIZE,
            ToggleMode        = true,
            ButtonGroup       = group,
            FocusMode         = FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        EditorStyle.ApplyFloating(button, EditorStyle.DIET_COLORS[info.Diet]);

        // The part floats free: bigger than its spot, reaching above it and under the texts, bobbing
        var icon = new PartIcon(info.Kind, _session.Player.BodyColor, TILE_ICON_PIXELS, isPartOnly: true)
        {
            Position = new Vector2((TILE_SIZE.X - TILE_ICON_PIXELS) * 0.5f, TILE_ICON_TOP),
        };
        button.AddChild(icon);
        EditorStyle.Bob(icon, TILE_BOB_PIXELS, _tiles.Count * 0.23f);

        // Name and cost at the bottom, drawn above every icon (also the ones of the next row)
        var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End, ZIndex = 1 };
        stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 2);
        stack.AddThemeConstantOverride("separation", -2);
        button.AddChild(stack);
        Label name = CellEditorPanel.NewLabel(14);
        Label cost = CellEditorPanel.NewLabel(12);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        cost.HorizontalAlignment = HorizontalAlignment.Center;
        cost.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[info.Diet].Lightened(0.3f));
        stack.AddChild(name);
        stack.AddChild(cost);

        // Switching tiles unpresses the old one, that must not clear the new selection
        PartKind kind = info.Kind;
        button.Toggled += isPressed =>
        {
            if (isPressed) SelectRequested?.Invoke(kind);
            else if (kind == _session.Selected) SelectRequested?.Invoke(null);
        };

        // Hover grows the tile and shows its card, also for locked parts (why they are locked)
        button.MouseEntered += () =>
        {
            _hovered = info;
            FocusChanged?.Invoke();
            EditorStyle.ScaleTo(button, TILE_HOVER_SCALE);
        };
        button.MouseExited += () =>
        {
            _hovered = null;
            FocusChanged?.Invoke();
            EditorStyle.ScaleTo(button, 1f);
        };
        button.SetDragForwarding(Callable.From<Vector2, Variant>(_ => GetTileDrag(info)), new Callable(), new Callable());

        _tiles.Add(new PaletteTile(info, button, icon, name, cost));
        return button;
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   UpdateHeaders   Translated name of every diet class.
    //   UpdateTiles     Name, cost, pressed and locked state of every tile.
    //   SetAlpha        Fades a canvas item.
    //

    /// <summary>Translates the diet headers, also after a locale change.</summary>
    public void UpdateHeaders()
    {
        foreach ((DietClass diet, Label header) in _dietHeaders)
        {
            header.Text = Tr($"DIET_{diet}".ToUpperInvariant());
        }
    }

    /// <summary>Updates name, cost, pressed and dimmed state of every tile from the session.</summary>
    public void UpdateTiles()
    {
        foreach (PaletteTile tile in _tiles)
        {
            bool isAvailable = _session.IsAvailable(tile.Info);
            tile.Name.Text       = Tr(tile.Info.NameKey);
            tile.Cost.Text       = $"{tile.Info.Cost} EP";
            tile.Button.Disabled = !isAvailable;
            tile.Button.SetPressedNoSignal(tile.Info.Kind == _session.Selected);
            SetAlpha(tile.Icon, isAvailable ? 1f : 0.3f);
            SetAlpha(tile.Name, isAvailable ? 1f : 0.45f);
        }
    }

    private static void SetAlpha(CanvasItem item, float alpha)
    {
        Color modulate = item.Modulate;
        modulate.A    = alpha;
        item.Modulate = modulate;
    }

    // ===========================================================================
    // ==============================   DRAG TILE   ==============================
    // ===========================================================================
    //
    //   Dragged              The part being dragged, null when none.
    //   GetTileDrag          Starts dragging a tile: the bare part hangs at the cursor.
    //   SetDragIconVisible   Hides the part at the cursor while its ghost shows on the cell.
    //   EndDrag              Forgets the drag once it is dropped or cancelled.
    //

    private const int DRAG_ICON_PIXELS = 96;

    private Control _dragIcon;
    private PartInfo _dragged;

    /// <summary>The part being dragged from a tile, null when none.</summary>
    public PartInfo Dragged => _dragged;

    /// <summary>Locked or unaffordable parts cannot be dragged.</summary>
    private Variant GetTileDrag(PartInfo info)
    {
        if (!_session.IsAvailable(info))
        {
            return default;
        }

        // The cursor sits in the middle of the bare part
        _dragIcon = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _dragIcon.AddChild(new PartIcon(info.Kind, _session.Player.BodyColor, DRAG_ICON_PIXELS, true)
        {
            Position = -Vector2.One * DRAG_ICON_PIXELS * 0.5f,
        });
        _column.SetDragPreview(_dragIcon);
        _dragged = info;
        FocusChanged?.Invoke();
        return (int)info.Kind;
    }

    /// <summary>Shows or hides the part at the cursor, a no-op without a running drag.</summary>
    /// <param name="isVisible">(Required) False while the ghost shows on the cell.</param>
    public void SetDragIconVisible(bool isVisible)
    {
        if (IsInstanceValid(_dragIcon))
        {
            _dragIcon.Visible = isVisible;
        }
    }

    /// <summary>Forgets the dragged part and its icon (Godot frees the icon itself).</summary>
    public void EndDrag()
    {
        _dragged  = null;
        _dragIcon = null;
    }
}
