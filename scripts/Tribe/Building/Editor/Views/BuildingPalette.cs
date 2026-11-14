// NOTE:
//   The block palette of the village builder: every block as a tile with its live 3D
//   icon, grouped by category (rooms and frames, roofs, decoration). A click picks the
//   block for dropping (BlockPicked), the picked tile stays pressed, the hovered one is
//   reported for the info line (Hovered, HoverChanged).
//
//   var palette = new BuildingPalette();
//   palette.Setup(session);
//   palette.BlockPicked  += kind => session.Select(kind);
//   palette.HoverChanged += RefreshInfo;

//
//   Chapters:
//     BUILD PALETTE   Category labels and the tile grids.
//     REFRESH         Pressed tiles and translated texts.
//

using System;
using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class BuildingPalette : ScrollContainer
{
    // ===========================================================================
    // ============================   BUILD PALETTE   ============================
    // ===========================================================================
    //
    //   BlockPicked    A tile was clicked.
    //   HoverChanged   The pointer entered or left a tile.
    //   Hovered        The block under the pointer, null for none.
    //   Setup          Builds labels and tiles and follows the selection of the session.
    //

    public event Action<BlockKind> BlockPicked;
    public event Action HoverChanged;

    private const int TILE_PIXELS = 84;
    private const int ICON_PIXELS = 72;
    private const int COLUMNS = 3;
    private static readonly Color ACCENT = new(0.8f, 0.6f, 0.4f);

    private readonly Dictionary<BlockKind, Button> _tiles = new();
    private readonly Dictionary<BlockCategory, Label> _labels = new();
    private BuildingEditorSession _session;

    public BlockKind? Hovered { get; private set; }

    /// <summary>Builds the palette, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting, its picked block presses the tile.</param>
    public void Setup(BuildingEditorSession session)
    {
        _session             = session;
        HorizontalScrollMode = ScrollMode.Disabled;
        SizeFlagsVertical    = SizeFlags.ExpandFill;
        CustomMinimumSize    = new Vector2(COLUMNS * (TILE_PIXELS + 6) + 14, 0);

        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        AddChild(column);
        foreach (BlockCategory category in Enum.GetValues<BlockCategory>())
        {
            Label label = CreatureEditorUi.NewLabel(16);
            column.AddChild(label);
            _labels[category] = label;

            var grid = new GridContainer { Columns = COLUMNS };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            column.AddChild(grid);
            foreach (BlockInfo info in BlockCatalog.BLOCKS)
            {
                if (category == info.Category)
                {
                    grid.AddChild(NewTile(info.Kind));
                }
            }
        }
        _session.SelectionChanged += Refresh;
    }

    private Button NewTile(BlockKind kind)
    {
        var tile = new Button
        {
            CustomMinimumSize = new Vector2(TILE_PIXELS, TILE_PIXELS),
            FocusMode         = FocusModeEnum.None,
            ToggleMode        = true,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        EditorStyle.ApplyTile(tile, ACCENT);
        var icon = new BlockIcon(kind, ICON_PIXELS);
        icon.Position = (new Vector2(TILE_PIXELS, TILE_PIXELS) - new Vector2(ICON_PIXELS, ICON_PIXELS)) * 0.5f;
        tile.AddChild(icon);

        tile.Pressed      += () => BlockPicked?.Invoke(kind);
        tile.MouseEntered += () => SetHovered(kind);
        tile.MouseExited  += () => SetHovered(null);
        _tiles[kind] = tile;
        return tile;
    }

    private void SetHovered(BlockKind? kind)
    {
        Hovered = kind;
        HoverChanged?.Invoke();
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh   Presses the tile of the picked block and translates the category labels.
    //

    public void Refresh()
    {
        foreach ((BlockKind kind, Button tile) in _tiles)
        {
            tile.SetPressedNoSignal(kind == _session.Selected);
        }
        foreach ((BlockCategory category, Label label) in _labels)
        {
            label.Text = Tr($"BLOCK_CATEGORY_{category}".ToUpperInvariant());
        }
    }
}
