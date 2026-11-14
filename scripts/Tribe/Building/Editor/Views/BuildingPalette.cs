// NOTE:
//   The block palette of the village builder: six group icons on top (walls, roofs,
//   frames, defense, trade, decoration), below them the three blocks of the group shown,
//   each a wide tile with its live 3D icon and its name. A click on a group icon shows
//   its blocks (GroupPicked), a click on a block picks it for dropping (BlockPicked), the
//   hovered block is reported for the info line (Hovered, HoverChanged).
//
//   var palette = new BuildingPalette();
//   palette.Setup(session);
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
    //   GroupPicked    A group icon was clicked.
    //   BlockPicked    A block tile was clicked.
    //   HoverChanged   The pointer entered or left a block tile.
    //   Hovered        The block under the pointer, null for none.
    //   Setup          Builds group icons and block tiles and follows the session.
    //

    public event Action<BlockGroup> GroupPicked;
    public event Action<BlockKind> BlockPicked;
    public event Action HoverChanged;

    private const int WIDTH = 288;
    private const int GROUP_COLUMNS = 3;
    private const int GROUP_ICON_PIXELS = 60;
    private const int TILE_HEIGHT = 86;              // six group icons and three tiles fit a 720 pixel screen
    private const int TILE_ICON_PIXELS = 78;
    private static readonly Vector2 GROUP_SIZE = new(88, 68);
    private static readonly Color ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color GROUP_ACCENT = new(0.6f, 0.7f, 0.9f);

    private readonly Dictionary<BlockGroup, Button> _groups = new();
    private readonly Dictionary<BlockKind, (Button Tile, Label Name)> _tiles = new();
    private BuildingEditorSession _session;
    private Label _groupName;

    public BlockKind? Hovered { get; private set; }

    /// <summary>Builds the palette, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting: the group shown and the picked block.</param>
    public void Setup(BuildingEditorSession session)
    {
        _session          = session;
        CustomMinimumSize = new Vector2(WIDTH, 0);
        AddThemeConstantOverride("separation", 8);

        // Group icons: the first block of each group stands for it
        var groups = new GridContainer { Columns = GROUP_COLUMNS };
        groups.AddThemeConstantOverride("h_separation", 12);
        groups.AddThemeConstantOverride("v_separation", 8);
        AddChild(groups);
        foreach (BlockGroup group in Enum.GetValues<BlockGroup>())
        {
            Button     button = NewTile(GROUP_SIZE, GROUP_ACCENT);
            BlockGroup shown  = group;
            foreach (BlockInfo info in BlockCatalog.In(group))
            {
                AddIcon(button, info.Kind, GROUP_ICON_PIXELS, (GROUP_SIZE - Vector2.One * GROUP_ICON_PIXELS) * 0.5f);
                break;
            }
            button.Pressed += () => GroupPicked?.Invoke(shown);
            groups.AddChild(button);
            _groups[group] = button;
        }

        _groupName = CreatureEditorUi.NewLabel(18);
        AddChild(_groupName);

        // Every block has its tile, only the three of the group shown are visible
        foreach (BlockInfo info in BlockCatalog.BLOCKS)
        {
            Button    tile = NewTile(new Vector2(WIDTH, TILE_HEIGHT), ACCENT);
            BlockKind kind = info.Kind;
            AddIcon(tile, kind, TILE_ICON_PIXELS, Vector2.One * (TILE_HEIGHT - TILE_ICON_PIXELS) * 0.5f);
            Label name = CreatureEditorUi.NewLabel(17);
            name.Position     = new Vector2(TILE_ICON_PIXELS + 18, 0);
            name.Size         = new Vector2(WIDTH - TILE_ICON_PIXELS - 26, TILE_HEIGHT);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            name.VerticalAlignment = VerticalAlignment.Center;
            tile.AddChild(name);

            tile.Pressed      += () => BlockPicked?.Invoke(kind);
            tile.MouseEntered += () => SetHovered(kind);
            tile.MouseExited  += () => SetHovered(null);
            AddChild(tile);
            _tiles[kind] = (tile, name);
        }
        _session.SelectionChanged += Refresh;
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
    //             shows the three tiles of the group and translates the names.
    //

    public void Refresh()
    {
        foreach ((BlockGroup group, Button button) in _groups)
        {
            button.SetPressedNoSignal(group == _session.Group);
            button.TooltipText = Tr($"BLOCK_GROUP_{group}".ToUpperInvariant());
        }
        _groupName.Text = Tr($"BLOCK_GROUP_{_session.Group}".ToUpperInvariant());
        foreach ((BlockKind kind, (Button tile, Label name)) in _tiles)
        {
            tile.Visible = BlockCatalog.Get(kind).Group == _session.Group;
            tile.SetPressedNoSignal(kind == _session.Selected);
            name.Text = Tr($"BLOCK_{kind}".ToUpperInvariant());
        }
    }
}
