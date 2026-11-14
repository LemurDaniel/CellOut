// NOTE:
//   The HUD of pack and tribe: the pack size top center, the hint of what is missing or
//   the F hint, the offered action as a button bottom center (call, build the nest, found
//   the tribe, like the divide and organism buttons of the Hud) and from the tribe on the
//   count of picked units, on the right one build tile per building function (a live icon
//   of its blueprint with the cost below) and the rival tribes on the left with their
//   relation and a gift button each. Population, stock and the builder button are in the
//   TribeHub. Every text is
//   a key in locale/translations.csv. Created by Main, which wires the buttons. Reads the
//   state of pack, tribe and rivals every frame, so it needs no signals from them.
//
//   var panel = new TribePanel();
//   panel.Watch(pack, tribe, rivals);
//   panel.ActionPressed   += () => pack.Act();
//   panel.BuildPressed    += kind => placer.Begin(kind);
//   panel.Placing          = placer.Pending;          // every frame: the pressed tile and the hint
//   panel.GiftPressed     += rival => rivals.Gift(rival);
//   AddChild(panel);

//
//   Chapters:
//     PANEL    Widgets, texts and the refresh per frame.
//     BUILD    One build tile per building function.
//     RIVALS   One row per rival tribe: name, relation, gift.
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
    //   ActionPressed     The offered action was pressed, Main lets the pack act.
    //   GiftPressed       A gift button was pressed, Main lets the rivals take it.
    //   Watch             The pack, the tribe and the rivals whose state the panel shows.
    //   _Ready            Status line top center, the action button bottom center, build column right, rivals left.
    //   _Process          Texts and visibility from the state of pack, tribe and rivals.
    //   _Notification     Re-translates after a locale change.
    //

    [Signal] public delegate void ActionPressedEventHandler();
    [Signal] public delegate void GiftPressedEventHandler(RivalTribe rival);

    private static readonly Color ACTION_ACCENT = new(0.95f, 0.8f, 0.4f);

    private PackKeeper _pack;
    private TribeCommander _tribe;
    private RivalKeeper _rivals;
    private Label _status;
    private Label _hint;
    private Button _action;
    private PackOffer _shownOffer = PackOffer.None;

    /// <summary>The pack, the tribe and the rivals whose state the panel shows, call before AddChild.</summary>
    /// <param name="pack">(Required) The pack: offer, members, nest.</param>
    /// <param name="tribe">(Required) The tribe: stock, blueprints and buildings.</param>
    /// <param name="rivals">(Required) The rival tribes and their relations.</param>
    public void Watch(PackKeeper pack, TribeCommander tribe, RivalKeeper rivals)
    {
        _pack   = pack;
        _tribe  = tribe;
        _rivals = rivals;
    }

    public override void _Ready()
    {
        Layer = 11;

        // Status and hint top center, below the banner line
        var top = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        top.OffsetTop = 24;
        AddChild(top);
        _status = NewLabel(22);
        _hint   = NewLabel(14);
        foreach (Label label in new[] { _status, _hint })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            top.AddChild(label);
        }

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
        BuildBuildColumn();
        BuildRivalRows();
    }

    /*
      > Before the land animal: everything hidden
      > Land animal and pack: pack size, the hint of what is missing (else the F hint from the
        pack on), the offered action
      > Tribe: the count of picked units, a build tile per function (grey without stock),
        one row per rival tribe
    */
    public override void _Process(double delta)
    {
        if (null == _pack)
        {
            return;
        }
        bool   isTribe = _tribe.IsActive;
        bool   hasPack = null != _pack.Nest || _pack.Members > 0;
        string missing = _pack.OfferHint;

        _status.Visible = hasPack || isTribe;
        _hint.Visible   = isTribe || null != missing || (hasPack && null != _pack.Nest);
        if (isTribe)
        {
            _status.Text = string.Format(Tr("TRIBE_STATUS"), _tribe.Selected.Count, _tribe.Units().Count);
            _hint.Text   = Tr(null != Placing ? "TRIBE_PLACE_HINT" : "TRIBE_HINT");
        }
        else
        {
            // With the nest the goal is the tribe: members of the needed ones
            _status.Text = null == _pack.Nest
                ? string.Format(Tr("PACK_STATUS"), _pack.Members)
                : string.Format(Tr("PACK_STATUS_GOAL"), _pack.Members, Balance.Current.Tribe.Pack.FoundTribeAt);
            // What is missing for the next step beats the F hint
            _hint.Text = null != missing ? Tr(missing) : Tr(_pack.IsStaying ? "PACK_HINT_STAY" : "PACK_HINT_FOLLOW");
        }

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

        RefreshBuildColumn(isTribe);
        RefreshRivalRows(isTribe);
    }

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
    //   BuildPressed         A build tile was pressed, Main lets the tribe build that function (a C# event: an enum is no Variant).
    //   BuildBuildColumn     A column on the right: one tile per function with the icon of its blueprint and its cost.
    //   RefreshBuildColumn   Icon, cost and tooltip per tile, grey without stock.
    //

    public event System.Action<BuildingKind> BuildPressed;

    /// <summary>(Optional) The function whose building hangs at the mouse (BuildingPlacer), its tile stays pressed. Defaults to none.</summary>
    public BuildingKind? Placing { get; set; }

    private const int BUILD_ICON_PIXELS = 60;
    private static readonly Vector2 BUILD_TILE_SIZE = new(170, 68);
    private static readonly Color BUILD_ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color FOOD_COLOR = new(1f, 0.6f, 0.55f);
    private static readonly Color WOOD_COLOR = new(0.9f, 0.72f, 0.5f);

    private readonly Dictionary<BuildingKind, (Button Tile, BuildingIcon Icon, Label Food, Label Wood)> _buildTiles = new();
    private VBoxContainer _buildColumn;

    private void BuildBuildColumn()
    {
        _buildColumn = new VBoxContainer { Visible = false };
        _buildColumn.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight);
        _buildColumn.GrowVertical   = Control.GrowDirection.Both;
        _buildColumn.GrowHorizontal = Control.GrowDirection.Begin;
        _buildColumn.Position      -= new Vector2(20, 0);
        _buildColumn.AddThemeConstantOverride("separation", 8);
        AddChild(_buildColumn);

        foreach (BuildingKind kind in System.Enum.GetValues<BuildingKind>())
        {
            var tile = new Button
            {
                CustomMinimumSize = BUILD_TILE_SIZE,
                FocusMode         = Control.FocusModeEnum.None,
                ToggleMode        = true,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };
            EditorStyle.ApplyTile(tile, BUILD_ACCENT);
            var icon = new BuildingIcon(BUILD_ICON_PIXELS) { Position = new Vector2(4, 4) };
            tile.AddChild(icon);
            Label food = NewCostLabel(tile, FOOD_COLOR, 12f);
            Label wood = NewCostLabel(tile, WOOD_COLOR, 36f);

            BuildingKind built = kind;
            tile.Pressed += () => BuildPressed?.Invoke(built);
            _buildColumn.AddChild(tile);
            _buildTiles[kind] = (tile, icon, food, wood);
        }
    }

    /// <summary>A cost number right of the icon of a build tile, in the color of its resource.</summary>
    private static Label NewCostLabel(Button tile, Color color, float top)
    {
        Label label = NewLabel(16);
        label.Visible  = true;
        label.Position = new Vector2(BUILD_ICON_PIXELS + 12, top);
        label.AddThemeColorOverride("font_color", color);
        tile.AddChild(label);
        return label;
    }

    private void RefreshBuildColumn(bool isTribe)
    {
        _buildColumn.Visible = isTribe;
        if (!isTribe)
        {
            return;
        }
        foreach ((BuildingKind kind, (Button tile, BuildingIcon icon, Label food, Label wood)) in _buildTiles)
        {
            BuildingDesign design = _tribe.DesignOf(kind);
            icon.Display(design);
            food.Text        = string.Format(Tr("TRIBE_COST_FOOD"), design.FoodCost);
            wood.Text        = string.Format(Tr("TRIBE_COST_WOOD"), design.WoodCost);
            tile.Disabled    = !_tribe.Economy.CanAfford(design);
            tile.SetPressedNoSignal(kind == Placing);
            tile.TooltipText = string.Format(Tr("TRIBE_BUILD"), Tr($"BUILDING_{kind}".ToUpperInvariant()), design.FoodCost, design.WoodCost);
        }
    }
    // ===========================================================================
    // ===============================   RIVALS   ================================
    // ===========================================================================
    //
    //   BuildRivalRows     A column on the left with room for every rival tribe.
    //   RefreshRivalRows   Name, relation word and value, the gift button grey without food.
    //

    private const int MAX_RIVAL_ROWS = 4;
    private static readonly Color GIFT_ACCENT = new(0.6f, 0.85f, 0.55f);

    private readonly List<(HBoxContainer Row, Label Label, Button Gift)> _rivalRows = new();
    private VBoxContainer _rivalColumn;

    private void BuildRivalRows()
    {
        _rivalColumn = new VBoxContainer { Visible = false };
        _rivalColumn.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft);
        _rivalColumn.GrowVertical = Control.GrowDirection.Both;
        _rivalColumn.Position    += new Vector2(20, 0);
        _rivalColumn.AddThemeConstantOverride("separation", 8);
        AddChild(_rivalColumn);
        for (int i = 0; i < MAX_RIVAL_ROWS; i++)
        {
            var row = new HBoxContainer { Visible = false };
            row.AddThemeConstantOverride("separation", 10);
            Label label = NewLabel(15);
            label.Visible = true;
            label.CustomMinimumSize = new Vector2(220, 0);
            var gift = new Button
            {
                FocusMode         = Control.FocusModeEnum.None,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                CustomMinimumSize = new Vector2(150, 32),
            };
            gift.AddThemeFontSizeOverride("font_size", 14);
            EditorStyle.ApplyButton(gift, GIFT_ACCENT);
            int index = i;
            gift.Pressed += () =>
            {
                if (index < _rivals.Rivals.Count)
                {
                    EmitSignal(SignalName.GiftPressed, _rivals.Rivals[index]);
                }
            };
            row.AddChild(label);
            row.AddChild(gift);
            _rivalColumn.AddChild(row);
            _rivalRows.Add((row, label, gift));
        }
    }

    private void RefreshRivalRows(bool isTribe)
    {
        IReadOnlyList<RivalTribe> rivals = _rivals.Rivals;
        _rivalColumn.Visible = isTribe && rivals.Count > 0;
        int giftFood = Balance.Current.Tribe.Village.GiftFood;
        for (int i = 0; i < _rivalRows.Count; i++)
        {
            (HBoxContainer row, Label label, Button gift) = _rivalRows[i];
            row.Visible = i < rivals.Count;
            if (!row.Visible)
            {
                continue;
            }
            RivalTribe rival = rivals[i];
            string mood = rival.IsDefeated ? Tr("RIVAL_DEFEATED") : rival.IsFriendly ? Tr("RIVAL_FRIENDLY") : rival.IsHostile ? Tr("RIVAL_HOSTILE") : Tr("RIVAL_NEUTRAL");
            label.Text    = string.Format(Tr("RIVAL_ROW"), Tr(rival.NameKey), mood, (int)Mathf.Round(rival.Relation * 100f));
            gift.Text     = string.Format(Tr("RIVAL_GIFT"), giftFood);
            gift.Visible  = !rival.IsDefeated;
            gift.Disabled = _tribe.Economy.Food < giftFood;
        }
    }
}
