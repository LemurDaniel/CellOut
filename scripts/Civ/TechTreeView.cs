// NOTE:
//   The tech tree of the city as a full screen view over the game (Civ/TechTree.cs): three
//   tiers from left to right, one row per line of thought (craft, sea, word), every tech a
//   glass tile with its drawn symbol, name and cost, the lines to what it needs glow once
//   both ends are researched. Researched tiles shine gold, open ones in the accent color,
//   closed ones are grey with a lock; a click researches an open tile. On the right the
//   next age waits greyed out. Hovering a tile shows what it does at the bottom. T opens
//   and closes it, Escape closes it. Created by Society, which researches on ResearchPressed.
//
//   var view = new TechTreeView();
//   view.Watch(civ);
//   view.ResearchPressed += kind => civ.Research(kind);
//   AddChild(view);
//   view.Toggle();

//
//   Chapters:
//     VIEW      Opening, closing, the keys and the texts.
//     TILES     One tile per tech, its state and its symbol.
//     LINES     The connections between the tiles.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class TechTreeView : CanvasLayer
{
    // ===========================================================================
    // =================================   VIEW   ================================
    // ===========================================================================
    //
    //   ResearchPressed   An open tile was clicked, Society researches it.
    //   Watch             The city whose techs show, before AddChild.
    //   IsOpen            Whether the view shows.
    //   Toggle            Opens (only in a city) or closes the view.
    //   _Ready            Backdrop, title, knowledge line, tree, footer, close button.
    //   _UnhandledInput   T toggles, Escape closes.
    //   Refresh           Knowledge line and the state of every tile.
    //

    public event System.Action<TechKind> ResearchPressed;

    private const float TILE_WIDTH = 230f;
    private const float TILE_HEIGHT = 92f;
    private const float COLUMN_STEP = 320f;          // pixels from one tier to the next
    private const float ROW_STEP = 130f;
    private static readonly Color BACKDROP = new(0.02f, 0.03f, 0.07f, 0.88f);
    private static readonly Color OPEN_ACCENT = new(0.55f, 0.85f, 0.95f);
    private static readonly Color DONE_ACCENT = new(1f, 0.8f, 0.35f);
    private static readonly Color CLOSED_ACCENT = new(0.45f, 0.45f, 0.5f);
    private static readonly Color KNOWLEDGE_COLOR = new(0.75f, 0.85f, 1f);

    private CivKeeper _civ;
    private Control _root;
    private Control _tree;
    private Label _title;
    private Label _knowledge;
    private Label _footer;

    /// <summary>The city whose techs show, call before AddChild.</summary>
    /// <param name="civ">(Required) The city.</param>
    public void Watch(CivKeeper civ)
    {
        _civ = civ;
        _civ.Changed += Refresh;
    }

    public bool IsOpen => _root.Visible;

    /// <summary>Opens the view (only once the city stands) or closes it.</summary>
    public void Toggle()
    {
        bool isOpening = !_root.Visible && _civ.IsCity;
        _root.Visible = isOpening;
        if (isOpening)
        {
            Refresh();
            EditorStyle.PopIn(_tree, 0f);
        }
    }

    public override void _Ready()
    {
        Layer = 12;     // over the hub bar and the tribe panel
        _root = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var backdrop = new ColorRect { Color = BACKDROP, MouseFilter = Control.MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(backdrop);

        // Title and knowledge top left
        _title = NewLabel(44);
        GameFonts.UseDisplay(_title);
        _title.Position = new Vector2(48, 28);
        _root.AddChild(_title);
        _knowledge = NewLabel(20);
        _knowledge.Modulate = KNOWLEDGE_COLOR;
        _knowledge.Position = new Vector2(52, 92);
        _root.AddChild(_knowledge);

        // The tree in the middle, its own draw call paints the lines under the tiles
        float width  = COLUMN_STEP * TechTree.TIERS + TILE_WIDTH;
        float height = ROW_STEP * (TechTree.ROWS - 1) + TILE_HEIGHT;
        var middle = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        middle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(middle);
        _tree = new Control { CustomMinimumSize = new Vector2(width, height), PivotOffset = new Vector2(width, height) * 0.5f, MouseFilter = Control.MouseFilterEnum.Ignore };
        _tree.Draw += DrawLines;
        middle.AddChild(_tree);
        foreach (TechInfo tech in TechTree.TECHS)
        {
            AddTile(tech);
        }
        AddNextAge();

        // What the hovered tech does, bottom middle
        _footer = NewLabel(18);
        _footer.HorizontalAlignment = HorizontalAlignment.Center;
        _footer.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        _footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _footer.OffsetLeft   = -420f;
        _footer.OffsetRight  = 420f;
        _footer.OffsetTop    = -110f;
        _footer.OffsetBottom = -40f;
        _root.AddChild(_footer);

        // Close button top right
        var close = new Button { Text = "×", FocusMode = Control.FocusModeEnum.None, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(52, 52) };
        close.AddThemeFontSizeOverride("font_size", 28);
        EditorStyle.ApplyButton(close, CLOSED_ACCENT);
        close.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        close.Position += new Vector2(-76f, 28f);
        close.Pressed  += Toggle;
        _root.AddChild(close);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key || null == _civ)
        {
            return;
        }
        bool isToggle = Key.T == key.Keycode && (_civ.IsCity || _root.Visible);
        bool isClose  = Key.Escape == key.Keycode && _root.Visible;
        if (isToggle || isClose)
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The knowledge line and the state of every tile.</summary>
    public void Refresh()
    {
        if (null == _title || !_root.Visible)
        {
            return;
        }
        _title.Text     = Tr("TECH_TREE_TITLE");
        _knowledge.Text = string.Format(Tr("TECH_KNOWLEDGE"), (int)_civ.Knowledge, Mathf.RoundToInt(_civ.KnowledgeRate));
        foreach ((TechKind kind, Button tile) in _tiles)
        {
            RefreshTile(kind, tile);
        }
        _tree.QueueRedraw();
    }

    // ===========================================================================
    // ================================   TILES   ================================
    // ===========================================================================
    //
    //   TileAt         Where the tile of a tech sits in the tree.
    //   AddTile        A glass tile: symbol left, name and cost right; hover names the effect.
    //   RefreshTile    Gold when researched, accent when open, grey and locked when closed.
    //   AddNextAge     The next age, greyed out on the right.
    //   DrawSymbol     The drawn symbol of a tech.
    //

    private const float SYMBOL_SIZE = 56f;

    private readonly Dictionary<TechKind, Button> _tiles = new();
    private readonly Dictionary<TechKind, Label> _costs = new();

    private static Vector2 TileAt(int tier, int row)
    {
        return new Vector2(tier * COLUMN_STEP, row * ROW_STEP);
    }

    private void AddTile(TechInfo tech)
    {
        TechKind kind = tech.Kind;
        var tile = new Button
        {
            Position          = TileAt(tech.Tier, tech.Row),
            Size              = new Vector2(TILE_WIDTH, TILE_HEIGHT),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            PivotOffset       = new Vector2(TILE_WIDTH, TILE_HEIGHT) * 0.5f,
        };
        var symbol = new Control { Position = new Vector2(14f, (TILE_HEIGHT - SYMBOL_SIZE) * 0.5f), Size = Vector2.One * SYMBOL_SIZE, MouseFilter = Control.MouseFilterEnum.Ignore };
        symbol.Draw += () => DrawSymbol(symbol, kind, tile.Disabled ? CLOSED_ACCENT : Colors.White);
        tile.AddChild(symbol);
        Label name = NewLabel(19);
        name.Text     = Tr($"TECH_{kind}".ToUpperInvariant());
        name.Position = new Vector2(SYMBOL_SIZE + 26f, 16f);
        tile.AddChild(name);
        Label cost = NewLabel(15);
        cost.Position = new Vector2(SYMBOL_SIZE + 26f, 50f);
        tile.AddChild(cost);
        _costs[kind] = cost;

        tile.Pressed      += () => OnTilePressed(kind, tile);
        tile.MouseEntered += () =>
        {
            _footer.Text = Tr($"TECH_{kind}_EFFECT".ToUpperInvariant());
            EditorStyle.ScaleTo(tile, 1.05f);
        };
        tile.MouseExited  += () => EditorStyle.ScaleTo(tile, 1f);
        _tree.AddChild(tile);
        _tiles[kind] = tile;
    }

    private void OnTilePressed(TechKind kind, Button tile)
    {
        ResearchPressed?.Invoke(kind);
        EditorStyle.Punch(tile, 1.12f);
    }

    private void RefreshTile(TechKind kind, Button tile)
    {
        bool isDone = _civ.Researched.Contains(kind);
        bool isOpen = TechTree.IsOpen(kind, _civ.Researched);
        bool canPay = _civ.Knowledge >= TechTree.CostOf(kind);
        tile.Disabled = !isDone && !isOpen;
        EditorStyle.ApplyTile(tile, isDone ? DONE_ACCENT : isOpen ? OPEN_ACCENT : CLOSED_ACCENT);
        tile.Modulate = isDone || isOpen ? Colors.White : new Color(1f, 1f, 1f, 0.6f);
        Label cost = _costs[kind];
        cost.Text     = isDone ? Tr("TECH_DONE") : string.Format(Tr("TECH_COST"), TechTree.CostOf(kind));
        cost.Modulate = isDone ? DONE_ACCENT : isOpen && canPay ? KNOWLEDGE_COLOR : new Color(1f, 0.6f, 0.55f);
        tile.GetChild<Control>(0).QueueRedraw();
    }

    private void AddNextAge()
    {
        var next = new Button
        {
            Position          = TileAt(TechTree.TIERS, 1) + new Vector2(-COLUMN_STEP + TILE_WIDTH + 70f, 0f),
            Size              = new Vector2(TILE_WIDTH * 0.75f, TILE_HEIGHT),
            Disabled          = true,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            Modulate          = new Color(1f, 1f, 1f, 0.45f),
        };
        EditorStyle.ApplyTile(next, CLOSED_ACCENT);
        Label name = NewLabel(17);
        name.Text                = Tr("TECH_NEXT_AGE");
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.VerticalAlignment   = VerticalAlignment.Center;
        name.Size                = next.Size;
        name.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        next.AddChild(name);
        next.MouseEntered += () => _footer.Text = Tr("TECH_NEXT_AGE_DESC");
        _tree.AddChild(next);
    }

    /// <summary>The symbol of a tech in a square of SYMBOL_SIZE, drawn in one color.</summary>
    private static void DrawSymbol(Control canvas, TechKind kind, Color color)
    {
        Vector2 c   = Vector2.One * SYMBOL_SIZE * 0.5f;
        Color   dim = color.Darkened(0.35f);
        // A soft round shadow under every symbol
        canvas.DrawCircle(c + new Vector2(0f, 3f), SYMBOL_SIZE * 0.42f, new Color(0f, 0f, 0f, 0.25f));
        switch (kind)
        {
            case TechKind.Pottery:          // an amphora with two handles
                canvas.DrawCircle(c + new Vector2(0f, 6f), 14f, color);
                canvas.DrawRect(new Rect2(c + new Vector2(-5f, -16f), new Vector2(10f, 12f)), color);
                canvas.DrawRect(new Rect2(c + new Vector2(-8f, -19f), new Vector2(16f, 4f)), dim);
                canvas.DrawArc(c + new Vector2(-9f, -6f), 6f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 10, color, 3f, true);
                canvas.DrawArc(c + new Vector2(9f, -6f), 6f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 10, color, 3f, true);
                canvas.DrawLine(c + new Vector2(-12f, 6f), c + new Vector2(12f, 6f), dim, 2f, true);
                break;
            case TechKind.Bronze:           // a short sword
                canvas.DrawColoredPolygon(new[] { c + new Vector2(0f, -22f), c + new Vector2(5f, -14f), c + new Vector2(4f, 8f), c + new Vector2(-4f, 8f), c + new Vector2(-5f, -14f) }, color);
                canvas.DrawRect(new Rect2(c + new Vector2(-12f, 8f), new Vector2(24f, 5f)), dim);
                canvas.DrawRect(new Rect2(c + new Vector2(-3f, 13f), new Vector2(6f, 9f)), dim);
                canvas.DrawCircle(c + new Vector2(0f, 23f), 3.5f, dim);
                break;
            case TechKind.Farming:          // an ear of grain
                canvas.DrawLine(c + new Vector2(0f, 24f), c + new Vector2(0f, -20f), dim, 3f, true);
                for (int i = 0; i < 5; i++)
                {
                    float y = -16f + i * 7f;
                    canvas.DrawCircle(c + new Vector2(-5f, y), 4f, color);
                    canvas.DrawCircle(c + new Vector2(5f, y + 3f), 4f, color);
                }
                break;
            case TechKind.Masonry:          // a wall of offset stones
                for (int row = 0; row < 3; row++)
                {
                    float shift = 0 == row % 2 ? 0f : 9f;
                    for (int i = -1; i < 2; i++)
                    {
                        canvas.DrawRect(new Rect2(c + new Vector2(i * 18f - 8f + shift - 4.5f, row * 12f - 16f), new Vector2(16f, 10f)), 1 == row ? dim : color);
                    }
                }
                break;
            case TechKind.Sailing:          // a ship under sail
                canvas.DrawColoredPolygon(new[] { c + new Vector2(-22f, 8f), c + new Vector2(22f, 8f), c + new Vector2(15f, 18f), c + new Vector2(-15f, 18f) }, dim);
                canvas.DrawLine(c + new Vector2(0f, 8f), c + new Vector2(0f, -22f), dim, 3f, true);
                canvas.DrawColoredPolygon(new[] { c + new Vector2(2f, -20f), c + new Vector2(20f, 4f), c + new Vector2(2f, 4f) }, color);
                canvas.DrawColoredPolygon(new[] { c + new Vector2(-2f, -14f), c + new Vector2(-2f, 4f), c + new Vector2(-15f, 4f) }, color);
                break;
            case TechKind.Writing:          // a scroll with lines
                canvas.DrawRect(new Rect2(c + new Vector2(-16f, -14f), new Vector2(32f, 28f)), color);
                canvas.DrawCircle(c + new Vector2(-16f, 0f), 6f, dim);
                canvas.DrawCircle(c + new Vector2(16f, 0f), 6f, dim);
                for (int i = 0; i < 3; i++)
                {
                    canvas.DrawLine(c + new Vector2(-9f, -7f + i * 7f), c + new Vector2(9f, -7f + i * 7f), dim, 2f, true);
                }
                break;
            case TechKind.Philosophy:       // a small temple
                canvas.DrawColoredPolygon(new[] { c + new Vector2(-22f, -8f), c + new Vector2(0f, -22f), c + new Vector2(22f, -8f) }, color);
                for (int i = 0; i < 4; i++)
                {
                    canvas.DrawRect(new Rect2(c + new Vector2(-17f + i * 11f, -6f), new Vector2(6f, 20f)), color);
                }
                canvas.DrawRect(new Rect2(c + new Vector2(-22f, 15f), new Vector2(44f, 5f)), dim);
                break;
            case TechKind.Navigation:       // a compass star
                canvas.DrawArc(c, 19f, 0f, Mathf.Tau, 32, dim, 2.5f, true);
                canvas.DrawColoredPolygon(new[] { c + new Vector2(0f, -24f), c + new Vector2(5f, -5f), c + new Vector2(24f, 0f), c + new Vector2(5f, 5f), c + new Vector2(0f, 24f), c + new Vector2(-5f, 5f), c + new Vector2(-24f, 0f), c + new Vector2(-5f, -5f) }, color);
                canvas.DrawCircle(c, 3f, dim);
                break;
            case TechKind.Currency:         // two stacked coins
                canvas.DrawCircle(c + new Vector2(-6f, 4f), 13f, dim);
                canvas.DrawCircle(c + new Vector2(6f, -4f), 13f, color);
                canvas.DrawArc(c + new Vector2(6f, -4f), 9f, 0f, Mathf.Tau, 24, dim, 2f, true);
                break;
        }
        // A padlock over a closed tech
        if (CLOSED_ACCENT == color)
        {
            Vector2 at = c + new Vector2(16f, 16f);
            canvas.DrawArc(at + new Vector2(0f, -4f), 5f, Mathf.Pi, Mathf.Tau, 10, Colors.White, 2f, true);
            canvas.DrawRect(new Rect2(at + new Vector2(-7f, -4f), new Vector2(14f, 11f)), Colors.White);
        }
    }

    // ===========================================================================
    // ================================   LINES   ================================
    // ===========================================================================
    //
    //   DrawLines   A soft curve from every tech to each one it needs, gold once both are
    //               researched, light when the tech is open, dim when it is closed.
    //

    private void DrawLines()
    {
        if (null == _civ)
        {
            return;
        }
        foreach (TechInfo tech in TechTree.TECHS)
        {
            foreach (TechKind need in tech.Needs)
            {
                TechInfo from  = TechTree.Get(need);
                Vector2  start = TileAt(from.Tier, from.Row) + new Vector2(TILE_WIDTH, TILE_HEIGHT * 0.5f);
                Vector2  end   = TileAt(tech.Tier, tech.Row) + new Vector2(0f, TILE_HEIGHT * 0.5f);
                bool     isLit = _civ.Researched.Contains(need) && _civ.Researched.Contains(tech.Kind);
                bool     isOpen = _civ.Researched.Contains(need);
                Color    color = isLit ? DONE_ACCENT : isOpen ? new Color(OPEN_ACCENT, 0.8f) : new Color(1f, 1f, 1f, 0.18f);
                var      points = new Vector2[17];
                for (int i = 0; i < points.Length; i++)
                {
                    // A smooth step from one height to the other: flat at both tiles
                    float t = i / 16f;
                    float s = t * t * (3f - 2f * t);
                    points[i] = new Vector2(Mathf.Lerp(start.X, end.X, t), Mathf.Lerp(start.Y, end.Y, s));
                }
                if (isLit)
                {
                    _tree.DrawPolyline(points, new Color(DONE_ACCENT, 0.25f), 9f, true);
                }
                _tree.DrawPolyline(points, color, 3f, true);
            }
        }
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 6));
        return label;
    }
}
