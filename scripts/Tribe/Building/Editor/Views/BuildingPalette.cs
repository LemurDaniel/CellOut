// NOTE:
//   The block palette of the village builder: the era tabs on top (village, modern, space
//   travel locked until it has blocks), below them the group icons of the era (walls, roofs,
//   frames, defense, trade, decoration; only groups the era has blocks for), then the blocks
//   of the group shown in two columns that scroll, each a tile with its live 3D icon over
//   its name. A click on an era tab shows its groups (EraPicked), on a group icon its blocks
//   (GroupPicked), a click on a block picks it for dropping (BlockPicked), the hovered block
//   is reported for the info line (Hovered, HoverChanged).
//
//   var palette = new BuildingPalette();
//   palette.Setup(session);
//   palette.EraPicked    += era => session.ShowEra(era);
//   palette.GroupPicked  += group => session.ShowGroup(group);
//   palette.BlockPicked  += kind => session.Select(kind);
//   palette.HoverChanged += RefreshInfo;

//
//   Chapters:
//     BUILD PALETTE   Group icons and the block tiles.
//     REFRESH         The group shown, the pressed tile and translated texts.
//

using System;
using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class BuildingPalette : VBoxContainer
{
    // ===========================================================================
    // ============================   BUILD PALETTE   ============================
    // ===========================================================================
    //
    //   EraPicked      An era tab was clicked.
    //   GroupPicked    A group icon was clicked.
    //   BlockPicked    A block tile was clicked.
    //   HoverChanged   The pointer entered or left a block tile.
    //   Hovered        The block under the pointer, null for none.
    //   Setup          Builds group icons and block tiles and follows the session.
    //

    public event Action<BlockEra> EraPicked;
    public event Action<BlockGroup> GroupPicked;
    public event Action<BlockKind> BlockPicked;
    public event Action HoverChanged;

    private const int WIDTH = 288;
    private const int ERA_HEIGHT = 32;
    private const int GROUP_GAP = 6;                 // six groups in one row: era tabs, groups and three rows of tiles fit a 720 pixel screen
    private const int GROUP_ICON_PIXELS = 38;
    private const int TILE_COLUMNS = 2;
    private const int TILE_GAP = 8;
    private const int TILE_HEIGHT = 102;             // the icon over one or two lines of its name
    private const int TILE_ICON_PIXELS = 66;
    private const int TILE_NAME_HEIGHT = 34;
    private const int TILE_ROWS_SHOWN = 3;           // more rows scroll
    private const int SCROLL_BAR = 10;               // room the scroll bar takes beside the tiles
    private static readonly Vector2 GROUP_SIZE = new(43, 43);
    private static readonly Color ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color GROUP_ACCENT = new(0.6f, 0.7f, 0.9f);
    private static readonly Color ERA_ACCENT = new(0.95f, 0.8f, 0.45f);

    private readonly Dictionary<BlockEra, Button> _eras = new();
    private readonly Dictionary<BlockEra, HBoxContainer> _groupRows = new();
    private readonly Dictionary<(BlockEra Era, BlockGroup Group), Button> _groups = new();
    private readonly Dictionary<BlockKind, (Button Tile, Label Name)> _tiles = new();
    private BuildingEditorSession _session;
    private Label _groupName;
    private GridContainer _tileGrid;

    public BlockKind? Hovered { get; private set; }

    /// <summary>Builds the palette, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting: the group shown and the picked block.</param>
    public void Setup(BuildingEditorSession session)
    {
        _session          = session;
        CustomMinimumSize = new Vector2(WIDTH, 0);
        AddThemeConstantOverride("separation", 8);

        // Era tabs: an era without blocks (space travel, for now) shows its tab locked
        var eras = new HBoxContainer();
        eras.AddThemeConstantOverride("separation", 6);
        AddChild(eras);
        foreach (BlockEra era in Enum.GetValues<BlockEra>())
        {
            Button   tab   = NewTile(new Vector2((WIDTH - 12) / 3, ERA_HEIGHT), ERA_ACCENT);
            BlockEra shown = era;
            tab.Disabled   = !HasBlocks(era);
            tab.AddThemeFontSizeOverride("font_size", 15);
            tab.Pressed += () => EraPicked?.Invoke(shown);
            eras.AddChild(tab);
            _eras[era] = tab;
        }

        // Group icons per era: the first block of a group in the era stands for it, groups without blocks are left out
        foreach (BlockEra era in Enum.GetValues<BlockEra>())
        {
            var groups = new HBoxContainer { Visible = false };
            groups.AddThemeConstantOverride("separation", GROUP_GAP);
            AddChild(groups);
            _groupRows[era] = groups;
            foreach (BlockGroup group in Enum.GetValues<BlockGroup>())
            {
                foreach (BlockInfo info in BlockCatalog.In(era, group))
                {
                    Button     button = NewTile(GROUP_SIZE, GROUP_ACCENT);
                    BlockGroup shown  = group;
                    AddIcon(button, info.Kind, GROUP_ICON_PIXELS, (GROUP_SIZE - Vector2.One * GROUP_ICON_PIXELS) * 0.5f);
                    button.Pressed += () => GroupPicked?.Invoke(shown);
                    groups.AddChild(button);
                    _groups[(era, group)] = button;
                    break;
                }
            }
        }

        _groupName = CreatureEditorUi.NewLabel(18);
        AddChild(_groupName);

        // Every block has its tile, only the ones of the group shown are visible: two columns, more than three rows scroll
        var scroll = new ScrollContainer
        {
            CustomMinimumSize    = new Vector2(WIDTH, TILE_ROWS_SHOWN * TILE_HEIGHT + (TILE_ROWS_SHOWN - 1) * TILE_GAP),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical    = SizeFlags.ExpandFill,
        };
        AddChild(scroll);
        _tileGrid = new GridContainer { Columns = TILE_COLUMNS };
        _tileGrid.AddThemeConstantOverride("h_separation", TILE_GAP);
        _tileGrid.AddThemeConstantOverride("v_separation", TILE_GAP);
        scroll.AddChild(_tileGrid);
        int tileWidth = (WIDTH - SCROLL_BAR - (TILE_COLUMNS - 1) * TILE_GAP) / TILE_COLUMNS;
        foreach (BlockInfo info in BlockCatalog.BLOCKS)
        {
            Button    tile = NewTile(new Vector2(tileWidth, TILE_HEIGHT), ACCENT);
            BlockKind kind = info.Kind;
            AddIcon(tile, kind, TILE_ICON_PIXELS, new Vector2((tileWidth - TILE_ICON_PIXELS) * 0.5f, 2f));
            Label name = CreatureEditorUi.NewLabel(13);
            name.Position            = new Vector2(4, TILE_HEIGHT - TILE_NAME_HEIGHT - 2);
            name.Size                = new Vector2(tileWidth - 8, TILE_NAME_HEIGHT);
            name.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.VerticalAlignment   = VerticalAlignment.Center;
            tile.AddChild(name);

            tile.Pressed      += () => BlockPicked?.Invoke(kind);
            tile.MouseEntered += () => SetHovered(kind);
            tile.MouseExited  += () => SetHovered(null);
            _tileGrid.AddChild(tile);
            _tiles[kind] = (tile, name);
        }
        _session.SelectionChanged += Refresh;
    }

    private static bool HasBlocks(BlockEra era)
    {
        foreach (BlockInfo _ in BlockCatalog.In(era))
        {
            return true;
        }
        return false;
    }

    private static Button NewTile(Vector2 size, Color accent)
    {
        var tile = new Button
        {
            CustomMinimumSize = size,
            FocusMode         = FocusModeEnum.None,
            ToggleMode        = true,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        EditorStyle.ApplyTile(tile, accent);
        return tile;
    }

    private static void AddIcon(Button tile, BlockKind kind, int pixels, Vector2 inset)
    {
        tile.AddChild(new BlockIcon(kind, pixels) { Position = inset });
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
    //   Refresh   Presses the icon of the group shown and the tile of the picked block,
    //             shows the tiles of the group and translates the names.
    //

    public void Refresh()
    {
        foreach ((BlockEra era, Button tab) in _eras)
        {
            tab.SetPressedNoSignal(era == _session.Era);
            tab.Text        = Tr($"BLOCK_ERA_{era}".ToUpperInvariant());
            tab.TooltipText = tab.Disabled ? Tr("BLOCK_ERA_LOCKED") : "";
            _groupRows[era].Visible = era == _session.Era;
        }
        foreach (((BlockEra era, BlockGroup group), Button button) in _groups)
        {
            button.SetPressedNoSignal(era == _session.Era && group == _session.Group);
            button.TooltipText = Tr($"BLOCK_GROUP_{group}".ToUpperInvariant());
        }
        _groupName.Text = Tr($"BLOCK_GROUP_{_session.Group}".ToUpperInvariant());
        foreach ((BlockKind kind, (Button tile, Label name)) in _tiles)
        {
            BlockInfo info = BlockCatalog.Get(kind);
            tile.Visible = info.Era == _session.Era && info.Group == _session.Group;
            tile.SetPressedNoSignal(kind == _session.Selected);
            name.Text = Tr($"BLOCK_{kind}".ToUpperInvariant());
        }
    }
}
