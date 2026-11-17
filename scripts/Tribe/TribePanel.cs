// NOTE:
//   The HUD of pack and tribe: a hint top center only while there is something to do right
//   now (placing a building, a stranger close by with the keys of the three ways, the next
//   step of the pack), the offered action as a button bottom center (call, build the nest,
//   found the tribe, like the divide and organism buttons of the Hud) and the build menu of
//   the tribe: a row of tiles above the builder button of the TribeHub, the blueprints
//   first, then one tile per building function (a live icon of its blueprint with the cost).
//   Lasting numbers (population, stock, the goal) and the neighbors sit in the TribeHub.
//   Every text is a key in locale/translations.csv. Created by Society, which wires the
//   buttons. Reads the state of pack and tribe every frame, so it needs no signals from them.
//
//   var panel = new TribePanel();
//   panel.Watch(pack, tribe, encounter);
//   panel.ActionPressed   += () => pack.Act();
//   panel.BuildPressed    += kind => placer.Begin(kind);
//   panel.DesignerPressed += () => OpenBuildingEditor();
//   panel.Placing          = placer.Pending;          // every frame: the pressed tile and the hint
//   hub.BuilderPressed    += panel.ToggleBuildMenu;
//   AddChild(panel);

//
//   Chapters:
//     PANEL    Widgets, texts and the refresh per frame.
//     BUILD    The build menu: blueprints and one tile per building function.
//     WAYS     The hint of the three ways and the mood of a rival.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class TribePanel : CanvasLayer
{
    // ===========================================================================
    // ================================   PANEL   ================================
    // ===========================================================================
    //
    //   ActionPressed     The offered action was pressed, Society lets the pack act.
    //   Watch             The pack, the tribe and the encounters whose state the panel shows.
    //   _Ready            The hint top center, the action button bottom center, the build menu.
    //   _Process          Texts and visibility from the state of pack and tribe.
    //   _Notification     Re-translates after a locale change.
    //

    [Signal] public delegate void ActionPressedEventHandler();

    private static readonly Color ACTION_ACCENT = new(0.95f, 0.8f, 0.4f);

    private PackKeeper _pack;
    private TribeCommander _tribe;
    private PackEncounter _encounter;
    private Label _hint;
    private Button _action;
    private PackOffer _shownOffer = PackOffer.None;

    /// <summary>The pack, the tribe and the encounters whose state the panel shows, call before AddChild.</summary>
    /// <param name="pack">(Required) The pack: offer, nest, the next step.</param>
    /// <param name="tribe">(Required) The tribe: stock, blueprints and buildings.</param>
    /// <param name="encounter">(Required) The encounters: the stranger close by.</param>
    public void Watch(PackKeeper pack, TribeCommander tribe, PackEncounter encounter)
    {
        _pack      = pack;
        _tribe     = tribe;
        _encounter = encounter;
    }

    public override void _Ready()
    {
        Layer = 11;

        // A situational hint top center, below the banner line: placing, a stranger close by, the next step
        var top = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        top.OffsetTop = 24;
        AddChild(top);
        _hint = NewLabel(16);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        top.AddChild(_hint);

        // Action button bottom center, the row of the Hud buttons is empty at these stages
        var bottom = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bottom.AddThemeConstantOverride("margin_bottom", 40);
        AddChild(bottom);
        var row = new HBoxContainer
        {
            MouseFilter         = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkEnd,
        };
        row.AddThemeConstantOverride("separation", 18);
        bottom.AddChild(row);
        _action = NewActionButton(ACTION_ACCENT, 280);
        _action.Pressed += () => EmitSignal(SignalName.ActionPressed);
        row.AddChild(_action);
        BuildBuildMenu();
    }

    /*
      > Before the land animal: everything hidden
      > Hint, only while there is something to do right now (the lasting goals sit in the
        TribeHub): placing a building, a stranger close by, else in the pack stage the next
        step (find the kin, go inland, back to the nest)
      > The offered action, the build menu while the tribe has it open
    */
    public override void _Process(double delta)
    {
        if (null == _pack)
        {
            return;
        }
        bool   isTribe = _tribe.IsActive;
        string missing = _pack.OfferHint;
        bool   isStep  = !isTribe && null != missing && !SILENT_HINTS.Contains(missing);

        _hint.Visible = null != Placing || _encounter?.Target is not null || isStep;
        _hint.Text    = null != Placing ? Tr("TRIBE_PLACE_HINT")
            : _encounter?.Target is not null ? WaysHint()
            : isStep ? Tr(missing)
            : "";

        // The offered action pops in when it changes
        PackOffer offer = _pack.Offer;
        if (offer != _shownOffer)
        {
            _shownOffer  = offer;
            _action.Text = offer switch
            {
                PackOffer.Call       => Tr("PACK_CALL_BUTTON"),
                PackOffer.Nest       => Tr("PACK_NEST_BUTTON"),
                PackOffer.FoundTribe => Tr("TRIBE_FOUND_BUTTON"),
                _                    => "",
            };
            if (PackOffer.None != offer)
            {
                EditorStyle.PopIn(_action, 0f);
            }
        }
        _action.Visible = PackOffer.None != offer;

        RefreshBuildMenu(isTribe);
    }

    // Goals that last: the hub shows them as a counter, the top line stays free
    private static readonly HashSet<string> SILENT_HINTS = new() { "PACK_HINT_MORE" };

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _action)
        {
            _shownOffer = (PackOffer)(-1);   // forces the button text to re-translate in _Process
        }
    }

    /// <summary>Pill button without focus: Space acts through the game, a focused button would fire twice.</summary>
    private static Button NewActionButton(Color accent, float width)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(width, 54),
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            Visible           = false,
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        EditorStyle.ApplyButton(button, accent);
        return button;
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label
        {
            MouseFilter       = Control.MouseFilterEnum.Ignore,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            Visible           = false,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   BuildPressed       A build tile was pressed, Main lets the tribe build that function (a C# event: an enum is no Variant).
    //   DesignerPressed    The blueprint tile was pressed, the village builder opens.
    //   IsBuildMenuOpen    Whether the build tiles show.
    //   ToggleBuildMenu    Opens or closes the tiles above the builder button of the TribeHub.
    //   BuildBuildMenu     A row bottom left: the blueprint tile, then one tile per function with its icon and cost.
    //   RefreshBuildMenu   Icon, cost and tooltip per tile, grey without stock, closed outside the tribe.
    //

    public event System.Action<BuildingKind> BuildPressed;

    [Signal] public delegate void DesignerPressedEventHandler();

    /// <summary>(Optional) The function whose building hangs at the mouse (BuildingPlacer), its tile stays pressed. Defaults to none.</summary>
    public BuildingKind? Placing { get; set; }

    public bool IsBuildMenuOpen => _buildMenu.Visible;

    private const int BUILD_ICON_PIXELS = 56;
    private static readonly Vector2 BUILD_TILE_SIZE = new(112, 106);
    private static readonly Vector2 BUILD_MENU_OFFSET = new(10, -118);    // above the hub bar bottom left
    private static readonly Color BUILD_ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color DESIGNER_ACCENT = new(0.55f, 0.75f, 0.9f);
    private static readonly Color COST_COLOR = new(1f, 0.78f, 0.6f);

    private readonly Dictionary<BuildingKind, (Button Tile, BuildingIcon Icon, Label Cost)> _buildTiles = new();
    private HBoxContainer _buildMenu;
    private Label _designerLabel;

    /// <summary>Opens or closes the build tiles (the builder button of the TribeHub, B opens the village builder itself).</summary>
    public void ToggleBuildMenu()
    {
        _buildMenu.Visible = !_buildMenu.Visible && _tribe.IsActive;
        if (_buildMenu.Visible)
        {
            RefreshBuildMenu(isTribe: true);
            EditorStyle.PopIn(_buildMenu, 0f);
        }
    }

    private void BuildBuildMenu()
    {
        _buildMenu = new HBoxContainer { Visible = false };
        _buildMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _buildMenu.GrowVertical = Control.GrowDirection.Begin;
        _buildMenu.Position    += BUILD_MENU_OFFSET;
        _buildMenu.AddThemeConstantOverride("separation", 8);
        AddChild(_buildMenu);

        // First the blueprints: the village builder, where every function gets its look
        Button designer = NewTile(DESIGNER_ACCENT);
        designer.ToggleMode = false;
        designer.Draw += () => DrawPencil(designer);
        _designerLabel = NewTileLabel(designer, Colors.White);
        designer.Pressed += () =>
        {
            _buildMenu.Visible = false;
            EmitSignal(SignalName.DesignerPressed);
        };
        _buildMenu.AddChild(designer);

        foreach (BuildingKind kind in System.Enum.GetValues<BuildingKind>())
        {
            Button tile = NewTile(BUILD_ACCENT);
            var icon = new BuildingIcon(BUILD_ICON_PIXELS) { Position = new Vector2((BUILD_TILE_SIZE.X - BUILD_ICON_PIXELS) * 0.5f, 6) };
            tile.AddChild(icon);
            Label cost = NewTileLabel(tile, COST_COLOR);

            BuildingKind built = kind;
            tile.Pressed += () =>
            {
                _buildMenu.Visible = false;
                BuildPressed?.Invoke(built);
            };
            _buildMenu.AddChild(tile);
            _buildTiles[kind] = (tile, icon, cost);
        }
    }

    /// <summary>A tile of the build menu without focus (the keys act through the game).</summary>
    private static Button NewTile(Color accent)
    {
        var tile = new Button
        {
            CustomMinimumSize = BUILD_TILE_SIZE,
            FocusMode         = Control.FocusModeEnum.None,
            ToggleMode        = true,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        EditorStyle.ApplyTile(tile, accent);
        return tile;
    }

    /// <summary>The text at the foot of a tile, centered.</summary>
    private static Label NewTileLabel(Button tile, Color color)
    {
        Label label = NewLabel(13);
        label.Visible             = true;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment   = VerticalAlignment.Bottom;
        label.Position            = new Vector2(0, BUILD_TILE_SIZE.Y - 44);
        label.Size                = new Vector2(BUILD_TILE_SIZE.X, 38);
        label.AddThemeColorOverride("font_color", color);
        tile.AddChild(label);
        return label;
    }

    /// <summary>A pencil over a sheet on the blueprint tile.</summary>
    private static void DrawPencil(Button tile)
    {
        var center = new Vector2(BUILD_TILE_SIZE.X * 0.5f, 34);
        tile.DrawRect(new Rect2(center + new Vector2(-18, -20), new Vector2(30, 38)), new Color(0.9f, 0.95f, 1f));
        for (int i = 0; i < 3; i++)
        {
            tile.DrawLine(center + new Vector2(-13, -11 + i * 9), center + new Vector2(6, -11 + i * 9), new Color(0.55f, 0.7f, 0.85f), 2f);
        }
        Vector2 tip  = center + new Vector2(2, 16);
        Vector2 back = center + new Vector2(22, -14);
        Vector2 side = (back - tip).Normalized().Orthogonal() * 4f;
        tile.DrawColoredPolygon(new[] { tip, tip + (back - tip) * 0.2f + side, back + side, back - side, tip + (back - tip) * 0.2f - side }, new Color(0.98f, 0.75f, 0.3f));
    }

    private void RefreshBuildMenu(bool isTribe)
    {
        if (!isTribe)
        {
            _buildMenu.Visible = false;
            return;
        }
        if (!_buildMenu.Visible)
        {
            return;
        }
        _designerLabel.Text = Tr("TRIBE_BLUEPRINTS");
        foreach ((BuildingKind kind, (Button tile, BuildingIcon icon, Label cost)) in _buildTiles)
        {
            BuildingDesign design = _tribe.DesignOf(kind);
            icon.Display(design);
            cost.Text        = string.Format(Tr("TRIBE_COST_FOOD"), design.FoodCost) + "\n" + string.Format(Tr("TRIBE_COST_WOOD"), design.WoodCost);
            tile.Disabled    = !_tribe.Economy.CanAfford(design);
            tile.SetPressedNoSignal(kind == Placing);
            tile.TooltipText = string.Format(Tr("TRIBE_BUILD"), Tr($"BUILDING_{kind}".ToUpperInvariant()), design.FoodCost, design.WoodCost);
        }
    }

    // ===========================================================================
    // ================================   WAYS   =================================
    // ===========================================================================
    //
    //   WaysHint   The stranger close by and the keys of the three ways.
    //   MoodOf     The mood word of a rival (also the neighbors on the world map, TribeHub).
    //

    /// <summary>The stranger close by and the keys of the three ways: a pack by its species, a tribe by its name.</summary>
    private string WaysHint()
    {
        RivalTribe stranger = _encounter.TargetPack;
        string     name     = stranger.IsSettled ? Tr(stranger.NameKey) : Tr(PackEncounter.NameOf(stranger));
        return string.Format(Tr("PACK_HINT_WAYS"), name, MoodOf(stranger, key => Tr(key)));
    }

    /// <summary>The mood word of a rival: defeated, respectful, friendly, hostile or neutral.</summary>
    /// <param name="rival">(Required) The rival tribe or foreign pack.</param>
    /// <param name="tr">(Required) The translation of the caller (Node.Tr).</param>
    public static string MoodOf(RivalTribe rival, System.Func<string, string> tr)
    {
        if (rival.IsDefeated)
        {
            return tr("RIVAL_DEFEATED");
        }
        if (rival.IsRespectful)
        {
            return tr("RIVAL_RESPECTFUL");
        }
        return rival.IsFriendly ? tr("RIVAL_FRIENDLY") : rival.IsHostile ? tr("RIVAL_HOSTILE") : tr("RIVAL_NEUTRAL");
    }
}
