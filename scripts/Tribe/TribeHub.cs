// NOTE:
//   The hub bar bottom left that bundles what the player steers from the land animal on:
//   it holds the round button of the village builder, the map of
//   the archipelago (a click or M opens it
//   large as the world map, with the names of the tribes), the round home button (a click
//   or H brings the strategy view back over the own fire) and the counters of the
//   population, the food and the wood. It grows with the stages: the map from the land
//   animal on, the population with the pack, builder, home, food and wood with the tribe.
//   The bar is a raised slab (light upper edge, shadow below), the map and the counters
//   lie in wells sunk into it.
//   Created by Main, which opens the builder on BuilderPressed.
//
//   var hub = new TribeHub();
//   hub.Watch(session, pack, tribe, rivals, followCamera);
//   hub.BuilderPressed += () => OpenBuildingEditor();
//   AddChild(hub);

//
//   Chapters:
//     HUB         The bar: builder button, small map, home button and counters.
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
    //   BuilderPressed   The builder button was pressed, Main opens the village builder.
    //   Watch            The world, the pack, the tribe, the rivals and the camera the hub shows.
    //   _Ready           The bar in the bottom left corner.
    //   NewRoundButton   A round button of the bar with a drawn glyph.
    //   Slab / Well      The raised look of the bar and the sunk look of what lies in it.
    //   _Process         Visibility by stage and the numbers of the counters.
    //   GoHome           Brings the strategy view back over the own fire.
    //   NewCounter       A drawn glyph and a number.
    //   DrawBuilder      The hut on the builder button.
    //   DrawHome         The campfire on the home button.
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
    private static readonly Color HOME_ACCENT = new(1f, 0.72f, 0.3f);     // the color of the own fire on the map
    private static readonly Color PEOPLE_COLOR = new(0.98f, 0.5f, 0.45f);
    private static readonly Color FOOD_COLOR = new(0.9f, 0.35f, 0.3f);
    private static readonly Color WOOD_COLOR = new(0.62f, 0.42f, 0.22f);

    private enum Glyph { People, Food, Wood }

    private WorldSession _session;
    private PackKeeper _pack;
    private TribeCommander _tribe;
    private RivalKeeper _rivals;
    private FollowCamera _camera;
    private PanelContainer _bar;
    private Button _builder;
    private Button _mapButton;
    private Button _home;
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
    /// <param name="rivals">(Required) The rival tribes for the map.</param>
    /// <param name="camera">(Required) The camera: its view on the map, the home button moves it.</param>
    public void Watch(WorldSession session, PackKeeper pack, TribeCommander tribe, RivalKeeper rivals, FollowCamera camera)
    {
        _session = session;
        _pack    = pack;
        _tribe   = tribe;
        _rivals  = rivals;
        _camera  = camera;
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
        map.Watch(_session, _pack, _rivals, _camera);
        _mapButton.AddChild(map);
        _mapButton.Pressed += ToggleWorldMap;
        var mapWell = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        mapWell.AddThemeStyleboxOverride("panel", Well(18, 4));
        mapWell.AddChild(_mapButton);
        row.AddChild(mapWell);

        // Home: a round button with a drawn campfire, right of the map it acts on
        _home = NewRoundButton(HOME_ACCENT);
        _home.Pressed += () => GoHome();
        _home.Draw    += DrawHome;
        row.AddChild(_home);

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
      > Tribe: the builder button, the home button, food and wood join
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
        _home.Visible        = isTribe && null != _pack.Nest;
        _home.TooltipText    = Tr("HUB_HOME");
        _people.Visible      = hasPack || isTribe;
        _food.Visible        = isTribe;
        _wood.Visible        = isTribe;
        _people.TooltipText  = Tr("HUB_PEOPLE");
        _food.TooltipText    = Tr("HUB_FOOD");
        _wood.TooltipText    = Tr("HUB_WOOD");
        _peopleCount.Text    = (isTribe ? _tribe.Units().Count : _pack.Members + 1).ToString();
        _foodCount.Text      = _tribe.Economy.Food.ToString();
        _woodCount.Text      = _tribe.Economy.Wood.ToString();
    }

    /// <summary>Brings the strategy view back over the own fire, from wherever the player panned to.</summary>
    /// <returns>True when there is a fire to return to.</returns>
    public bool GoHome()
    {
        if (null == _pack.Nest)
        {
            return false;
        }
        _camera.FocusOn(_pack.Nest.Position);
        Log.Verbose("TribeHub", "Camera returns to the village.");
        return true;
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

    /// <summary>A campfire: a flame with a dark core over two crossed logs.</summary>
    private void DrawHome()
    {
        Vector2 center = Vector2.One * BUTTON_SIZE * 0.5f;
        var flame = new[]
        {
            new Vector2(0, -20), new Vector2(7, -9), new Vector2(11, 1), new Vector2(8, 8),
            new Vector2(0, 11), new Vector2(-8, 8), new Vector2(-11, 1), new Vector2(-7, -9),
        };
        var core = new[] { new Vector2(0, -7), new Vector2(5, 2), new Vector2(3, 8), new Vector2(-3, 8), new Vector2(-5, 2) };
        for (int i = 0; i < flame.Length; i++)
        {
            flame[i] += center;
        }
        for (int i = 0; i < core.Length; i++)
        {
            core[i] += center;
        }
        _home.DrawLine(center + new Vector2(-15, 10), center + new Vector2(15, 18), Colors.White, 5f, true);
        _home.DrawLine(center + new Vector2(-15, 18), center + new Vector2(15, 10), Colors.White, 5f, true);
        _home.DrawColoredPolygon(flame, Colors.White);
        _home.DrawColoredPolygon(core, HOME_ACCENT.Darkened(0.5f));
    }

    // ===========================================================================
    // ==============================   WORLD MAP   ==============================
    // ===========================================================================
    //
    //   IsWorldMapOpen    Whether the large map shows.
    //   BuildWorldMap     The large map in the middle of the screen, hidden.
    //   ToggleWorldMap    Opens or closes it.
    //   _UnhandledInput   M toggles it, Escape closes it, H goes home.
    //

    private static readonly Vector2 WORLD_MAP_SIZE = new(760f, 380f);

    private Button _worldMap;
    private Label _worldMapTitle;

    public bool IsWorldMapOpen => _worldMap.Visible;

    private void BuildWorldMap()
    {
        // A click anywhere on it closes it again
        _worldMap = new Button
        {
            Visible           = false,
            CustomMinimumSize = WORLD_MAP_SIZE + new Vector2(24, 64),
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
        map.Watch(_session, _pack, _rivals, _camera);
        _worldMap.AddChild(map);
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
            return;
        }
        if (Key.H == key.Keycode && _home.IsVisibleInTree() && GoHome())
        {
            GetViewport().SetInputAsHandled();
        }
    }
}
