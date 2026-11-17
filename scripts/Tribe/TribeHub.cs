// NOTE:
//   The hub bar bottom left that bundles what the player steers from the land animal on:
//   it holds the round build button (it opens the build menu of the TribePanel), the map
//   of the archipelago (a click or M opens it large as the world map, with the names of
//   the tribes and the neighbors beside it: mood, relation, a gift button), the counters
//   of the population, the food and the wood (the goal of the stage is the progress ring
//   of the dial, Game/Ui/VitalsDial.cs). It grows with the stages: the map from the land
//   animal on, the population with the pack, build button, food and wood with the tribe. The bar is a raised slab (light upper edge, shadow
//   below), the map and the counters lie in wells sunk into it. Created by Society.
//
//   var hub = new TribeHub();
//   hub.Watch(session, pack, tribe, rivals);
//   hub.BuilderPressed += () => panel.ToggleBuildMenu();
//   hub.GiftPressed    += rival => rivals.Gift(rival);
//   AddChild(hub);

//
//   Chapters:
//     HUB         The bar: builder button, small map and counters.
//     WORLD MAP   The large map over the game.
//

using Godot;

namespace CellOut;

public partial class TribeHub : CanvasLayer
{
    // ===========================================================================
    // =================================   HUB   =================================
    // ===========================================================================
    //
    //   BuilderPressed   The build button was pressed, Society opens the build menu.
    //   Watch            The world, the pack, the tribe and the rivals the hub shows.
    //   _Ready           The bar in the bottom left corner.
    //   NewRoundButton   A round button of the bar with a drawn glyph.
    //   Slab / Well      The raised look of the bar and the sunk look of what lies in it.
    //   _Process         Visibility by stage and the numbers of the counters.
    //   NewCounter       A drawn glyph and a number.
    //   DrawBuilder      The hut on the builder button.
    //

    [Signal] public delegate void BuilderPressedEventHandler();

    private const float BAR_LEFT = 10f;
    private const float BAR_BOTTOM = 8f;
    private const float BUTTON_SIZE = 64f;
    private static readonly Vector2 MAP_SIZE = new(196f, 84f);
    private static readonly Color BAR_COLOR = new(0.09f, 0.16f, 0.2f, 0.96f);
    private static readonly Color BAR_EDGE = new(0.55f, 0.72f, 0.8f);
    private static readonly Color WELL_COLOR = new(0.03f, 0.06f, 0.08f);
    private static readonly Color BUILDER_ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color PEOPLE_COLOR = new(0.98f, 0.5f, 0.45f);
    private static readonly Color FOOD_COLOR = new(0.9f, 0.35f, 0.3f);
    private static readonly Color WOOD_COLOR = new(0.62f, 0.42f, 0.22f);

    private enum Glyph { People, Food, Wood }

    private WorldSession _session;
    private PackKeeper _pack;
    private TribeCommander _tribe;
    private RivalKeeper _rivals;
    private PanelContainer _bar;
    private Button _builder;
    private Button _mapButton;
    private Control _people;
    private Control _food;
    private Control _wood;
    private Label _peopleCount;
    private Label _foodCount;
    private Label _woodCount;

    /// <summary>Hands in what the hub shows, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="pack">(Required) The pack: nest and members.</param>
    /// <param name="tribe">(Required) The tribe: units and stock.</param>
    /// <param name="rivals">(Required) The rival tribes for the map and the neighbors beside it.</param>
    public void Watch(WorldSession session, PackKeeper pack, TribeCommander tribe, RivalKeeper rivals)
    {
        _session   = session;
        _pack      = pack;
        _tribe     = tribe;
        _rivals    = rivals;
    }

    public override void _Ready()
    {
        Layer = 9;      // below the Hud: its banners lie over the world map

        _bar = new PanelContainer { Visible = false };
        _bar.AddThemeStyleboxOverride("panel", Slab());
        _bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _bar.GrowVertical = Control.GrowDirection.Begin;
        _bar.Position    += new Vector2(BAR_LEFT, -BAR_BOTTOM);
        AddChild(_bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        _bar.AddChild(row);

        // The village builder: a round button with a drawn hut (B acts through Main)
        _builder = NewRoundButton(BUILDER_ACCENT);
        _builder.Pressed += () => EmitSignal(SignalName.BuilderPressed);
        _builder.Draw    += DrawBuilder;
        row.AddChild(_builder);

        // The small map, the button over it opens the world map
        _mapButton = new Button
        {
            CustomMinimumSize = MAP_SIZE,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            ClipContents      = true,
        };
        _mapButton.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _mapButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _mapButton.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        _mapButton.AddThemeStyleboxOverride("hover", EditorStyle.Pill(new Color(1f, 1f, 1f, 0.12f), Colors.White));
        var map = new WorldMap { Size = MAP_SIZE, ShowBehindParent = true, FrameColor = WELL_COLOR };
        map.Watch(_session, _pack, _rivals);
        _mapButton.AddChild(map);
        _mapButton.Pressed += ToggleWorldMap;
        var mapWell = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        mapWell.AddThemeStyleboxOverride("panel", Well(18, 4));
        mapWell.AddChild(_mapButton);
        row.AddChild(mapWell);

        var counters = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        counters.AddThemeConstantOverride("separation", 4);
        row.AddChild(counters);
        (_people, _peopleCount) = NewCounter(counters, Glyph.People);
        (_food, _foodCount)     = NewCounter(counters, Glyph.Food);
        (_wood, _woodCount)     = NewCounter(counters, Glyph.Wood);

        BuildWorldMap();
    }

    /// <summary>A round button of the bar in an accent color, its glyph is drawn by the caller. No focus: the keys act through input events.</summary>
    private static Button NewRoundButton(Color accent)
    {
        var button = new Button
        {
            CustomMinimumSize = Vector2.One * BUTTON_SIZE,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        EditorStyle.ApplyButton(button, accent);
        foreach (string state in new[] { "normal", "hover", "pressed" })
        {
            ((StyleBoxFlat)button.GetThemeStylebox(state)).SetCornerRadiusAll((int)(BUTTON_SIZE * 0.5f));
        }
        return button;
    }

    /// <summary>The bar as a raised slab: the border fades from a light edge into the fill, a shadow falls below it.</summary>
    private static StyleBoxFlat Slab()
    {
        var slab = new StyleBoxFlat
        {
            BgColor      = BAR_COLOR,
            BorderColor  = BAR_EDGE,
            BorderBlend  = true,
            ShadowColor  = new Color(0f, 0f, 0f, 0.5f),
            ShadowSize   = 14,
            ShadowOffset = new Vector2(0f, 5f),
            AntiAliasing = true,
        };
        slab.SetBorderWidthAll(4);
        slab.BorderWidthBottom = 2;
        slab.SetCornerRadiusAll(46);
        slab.SetContentMarginAll(12);
        return slab;
    }

    /// <summary>A well sunk into the slab: dark fill, a dark upper edge that fades in, a light lower lip.</summary>
    private static StyleBoxFlat Well(int radius, int margin)
    {
        var well = new StyleBoxFlat
        {
            BgColor      = WELL_COLOR,
            BorderColor  = new Color(BAR_EDGE, 0.35f),
            BorderBlend  = true,
            AntiAliasing = true,
        };
        well.BorderWidthBottom = 2;
        well.SetCornerRadiusAll(radius);
        well.SetContentMarginAll(margin);
        return well;
    }

    /*
      > Before the land animal: hidden
      > Land animal: the map
      > Pack: the population joins
      > Tribe: the builder button, food and wood join
    */
    public override void _Process(double delta)
    {
        if (null == _session)
        {
            return;
        }
        Player player  = _session.Player;
        bool   isTribe = _tribe.IsActive;
        bool   hasPack = null != _pack.Nest || _pack.Members > 0;
        _bar.Visible   = player.IsLandAnimal && null != _session.Coast && !player.IsDead;
        if (!_bar.Visible)
        {
            _worldMap.Visible = false;
            return;
        }
        _builder.Visible     = isTribe;
        _builder.TooltipText = Tr("TRIBE_DESIGN");
        _mapButton.TooltipText = Tr("HUB_MAP");
        _people.Visible      = hasPack || isTribe;
        _food.Visible        = isTribe;
        _wood.Visible        = isTribe;
        _people.TooltipText  = Tr("HUB_PEOPLE");
        _food.TooltipText    = Tr("HUB_FOOD");
        _wood.TooltipText    = Tr("HUB_WOOD");
        _peopleCount.Text    = (_pack.Members + 1).ToString();
        _foodCount.Text      = _tribe.Economy.Food.ToString();
        _woodCount.Text      = _tribe.Economy.Wood.ToString();
        if (_worldMap.Visible)
        {
            RefreshNeighbors();
        }
    }

    /// <summary>A row of a drawn glyph and a number.</summary>
    /// <returns>The row (for visibility and tooltip) and the label of the number.</returns>
    private static (Control Row, Label Count) NewCounter(VBoxContainer parent, Glyph glyph)
    {
        var well = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        StyleBoxFlat box = Well(12, 2);
        box.ContentMarginLeft  = 8;
        box.ContentMarginRight = 8;
        well.AddThemeStyleboxOverride("panel", box);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        well.AddChild(row);
        var icon = new Control { CustomMinimumSize = new Vector2(24, 24), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        icon.Draw += () => DrawGlyph(icon, glyph);
        row.AddChild(icon);
        var count = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(40, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        count.AddThemeFontSizeOverride("font_size", 18);
        count.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        count.AddThemeConstantOverride("outline_size", 4);
        row.AddChild(count);
        parent.AddChild(well);
        return (well, count);
    }

    /// <summary>People: a head on shoulders. Food: a fruit with a leaf. Wood: two stacked logs.</summary>
    private static void DrawGlyph(Control icon, Glyph glyph)
    {
        switch (glyph)
        {
            case Glyph.People:
                icon.DrawCircle(new Vector2(12, 7), 5.5f, PEOPLE_COLOR);
                icon.DrawRect(new Rect2(3, 14, 18, 9), PEOPLE_COLOR);
                icon.DrawCircle(new Vector2(12, 14), 9f, PEOPLE_COLOR);
                break;
            case Glyph.Food:
                icon.DrawCircle(new Vector2(12, 14), 8.5f, FOOD_COLOR);
                icon.DrawLine(new Vector2(12, 7), new Vector2(17, 2), new Color(0.4f, 0.7f, 0.3f), 4f, true);
                break;
            case Glyph.Wood:
                icon.DrawRect(new Rect2(2, 3, 20, 8), WOOD_COLOR);
                icon.DrawRect(new Rect2(2, 13, 20, 8), WOOD_COLOR.Darkened(0.2f));
                icon.DrawCircle(new Vector2(22, 7), 4f, WOOD_COLOR.Lightened(0.35f));
                icon.DrawCircle(new Vector2(22, 17), 4f, WOOD_COLOR.Lightened(0.2f));
                break;

        }
    }

    /// <summary>A hut: a wall, a door and a roof on top.</summary>
    private void DrawBuilder()
    {
        Vector2 center = Vector2.One * BUTTON_SIZE * 0.5f;
        _builder.DrawRect(new Rect2(center + new Vector2(-13, -2), new Vector2(26, 18)), Colors.White);
        _builder.DrawRect(new Rect2(center + new Vector2(-4, 5), new Vector2(8, 11)), BUILDER_ACCENT.Darkened(0.5f));
        _builder.DrawColoredPolygon(new[] { center + new Vector2(-19, -1), center + new Vector2(0, -19), center + new Vector2(19, -1) }, Colors.White);
    }

    // ===========================================================================
    // ==============================   WORLD MAP   ==============================
    // ===========================================================================
    //
    //   IsWorldMapOpen    Whether the large map shows.
    //   BuildWorldMap     The large map in the middle of the screen, hidden.
    //   ToggleWorldMap    Opens or closes it.
    //   GiftPressed       A gift button beside the map was pressed, Society lets the rivals take it.
    //   BuildNeighbors    The column beside the map: one row per neighbor, mood, relation and a gift.
    //   RefreshNeighbors  Texts and the gift buttons, grey without food, only for settled tribes.
    //   _UnhandledInput   M toggles it, Escape closes it.
    //

    private static readonly Vector2 WORLD_MAP_SIZE = new(720f, 380f);
    private const float NEIGHBOR_WIDTH = 290f;
    private const int MAX_NEIGHBORS = 4;
    private static readonly Color GIFT_ACCENT = new(0.6f, 0.85f, 0.55f);

    [Signal] public delegate void GiftPressedEventHandler(RivalTribe rival);

    private readonly System.Collections.Generic.List<(Control Row, Label Name, Label Mood, Button Gift)> _neighbors = new();
    private Label _neighborTitle;

    private Button _worldMap;
    private Label _worldMapTitle;

    public bool IsWorldMapOpen => _worldMap.Visible;

    private void BuildWorldMap()
    {
        // A click anywhere on it closes it again
        _worldMap = new Button
        {
            Visible           = false,
            CustomMinimumSize = WORLD_MAP_SIZE + new Vector2(36 + NEIGHBOR_WIDTH, 64),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        StyleBoxFlat frame = Slab();
        frame.SetCornerRadiusAll(22);
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
        {
            _worldMap.AddThemeStyleboxOverride(state, frame);
        }
        _worldMap.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _worldMap.GrowHorizontal = Control.GrowDirection.Both;
        _worldMap.GrowVertical   = Control.GrowDirection.Both;
        _worldMap.Pressed       += ToggleWorldMap;
        AddChild(_worldMap);

        _worldMapTitle = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, Position = new Vector2(18, 8), MouseFilter = Control.MouseFilterEnum.Ignore };
        _worldMapTitle.AddThemeFontSizeOverride("font_size", 30);
        GameFonts.UseDisplay(_worldMapTitle);
        _worldMap.AddChild(_worldMapTitle);

        var map = new WorldMap { Position = new Vector2(12, 52), Size = WORLD_MAP_SIZE, HasNames = true, FrameColor = BAR_COLOR };
        map.Watch(_session, _pack, _rivals);
        _worldMap.AddChild(map);
        BuildNeighbors();
    }

    private void BuildNeighbors()
    {
        var column = new VBoxContainer { Position = new Vector2(WORLD_MAP_SIZE.X + 24, 52), CustomMinimumSize = new Vector2(NEIGHBOR_WIDTH, 0) };
        column.AddThemeConstantOverride("separation", 10);
        _worldMap.AddChild(column);
        _neighborTitle = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = Control.MouseFilterEnum.Ignore };
        _neighborTitle.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(_neighborTitle);

        for (int i = 0; i < MAX_NEIGHBORS; i++)
        {
            var well = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            well.AddThemeStyleboxOverride("panel", Well(14, 8));
            column.AddChild(well);
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            well.AddChild(row);
            var texts = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
            texts.AddThemeConstantOverride("separation", 0);
            row.AddChild(texts);
            var name = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = Control.MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 16);
            var mood = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = Control.MouseFilterEnum.Ignore };
            mood.AddThemeFontSizeOverride("font_size", 13);
            mood.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.7f));
            texts.AddChild(name);
            texts.AddChild(mood);
            var gift = new Button
            {
                FocusMode         = Control.FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                CustomMinimumSize = new Vector2(96, 34),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            gift.AddThemeFontSizeOverride("font_size", 13);
            EditorStyle.ApplyButton(gift, GIFT_ACCENT);
            int index = i;
            gift.Pressed += () =>
            {
                if (index < _rivals.Rivals.Count)
                {
                    EmitSignal(SignalName.GiftPressed, _rivals.Rivals[index]);
                }
            };
            row.AddChild(gift);
            _neighbors.Add((well, name, mood, gift));
        }
    }

    /// <summary>Name, mood, relation and respect per neighbor, a gift button for settled tribes that are still standing.</summary>
    private void RefreshNeighbors()
    {
        _neighborTitle.Text = Tr("HUB_NEIGHBORS");
        var rivals   = _rivals.Rivals;
        int giftFood = Balance.Current.Tribe.Village.GiftFood;
        for (int i = 0; i < _neighbors.Count; i++)
        {
            (Control row, Label name, Label mood, Button gift) = _neighbors[i];
            row.Visible = i < rivals.Count;
            if (!row.Visible)
            {
                continue;
            }
            RivalTribe rival = rivals[i];
            name.Text = rival.IsSettled ? Tr(rival.NameKey) : Tr(PackEncounter.NameOf(rival));
            mood.Text = rival.Respect > 0.005f
                ? string.Format(Tr("HUB_NEIGHBOR_RESPECT"), TribePanel.MoodOf(rival, key => Tr(key)), (int)Mathf.Round(rival.Relation * 100f), (int)Mathf.Round(rival.Respect * 100f))
                : string.Format(Tr("HUB_NEIGHBOR_MOOD"), TribePanel.MoodOf(rival, key => Tr(key)), (int)Mathf.Round(rival.Relation * 100f));
            gift.Text     = string.Format(Tr("RIVAL_GIFT"), giftFood);
            gift.Visible  = _tribe.IsActive && rival.IsSettled && !rival.IsDefeated;
            gift.Disabled = _tribe.Economy.Food < giftFood;
        }
    }

    /// <summary>Opens or closes the large map.</summary>
    public void ToggleWorldMap()
    {
        _worldMap.Visible   = !_worldMap.Visible && _bar.Visible;
        _worldMapTitle.Text = Tr("HUB_WORLD_MAP");
        Log.Verbose("TribeHub", $"World map {(_worldMap.Visible ? "opened" : "closed")}.");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        bool isToggle = Key.M == key.Keycode && _bar.Visible;
        bool isClose  = Key.Escape == key.Keycode && _worldMap.Visible;
        if (isToggle || isClose)
        {
            ToggleWorldMap();
            GetViewport().SetInputAsHandled();
        }
    }
}
