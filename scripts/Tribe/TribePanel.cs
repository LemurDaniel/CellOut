// NOTE:
//   The HUD of pack and tribe: the pack size top center, the hint for F, the offered
//   action as a button bottom center (call, build the nest, found the tribe, like the
//   divide and organism buttons of the Hud) and from the tribe on the stock line with
//   the build button. Every text is a key in locale/translations.csv. Created by Main,
//   which wires the buttons to the PackKeeper and the TribeCommander. Reads their state
//   every frame, so it needs no signals from them.
//
//   var panel = new TribePanel();
//   panel.Watch(pack, tribe);
//   panel.ActionPressed   += () => pack.Act();
//   panel.BuildHutPressed += () => tribe.TryBuildHut();
//   AddChild(panel);

//
//   Chapters:
//     PANEL   Widgets, texts and the refresh per frame.
//

using Godot;

namespace CellOut;

public partial class TribePanel : CanvasLayer
{
    // ===========================================================================
    // ================================   PANEL   ================================
    // ===========================================================================
    //
    //   ActionPressed     The offered action was pressed, Main lets the pack act.
    //   BuildHutPressed   The build button was pressed, Main lets the tribe build.
    //   Watch             The pack and the tribe whose state the panel shows.
    //   _Ready            Status line top center, action button and build button bottom center.
    //   _Process          Texts and visibility from the state of pack and tribe.
    //   _Notification     Re-translates after a locale change.
    //

    [Signal] public delegate void ActionPressedEventHandler();
    [Signal] public delegate void BuildHutPressedEventHandler();

    private static readonly Color ACTION_ACCENT = new(0.95f, 0.8f, 0.4f);
    private static readonly Color BUILD_ACCENT = new(0.8f, 0.6f, 0.4f);
    private const string STATUS_SEPARATOR = " · ";

    private PackKeeper _pack;
    private TribeCommander _tribe;
    private Label _status;
    private Label _hint;
    private Button _action;
    private Button _build;
    private PackOffer _shownOffer = PackOffer.None;

    /// <summary>The pack and the tribe whose state the panel shows, call before AddChild.</summary>
    /// <param name="pack">(Required) The pack: offer, members, nest.</param>
    /// <param name="tribe">(Required) The tribe: stock and huts.</param>
    public void Watch(PackKeeper pack, TribeCommander tribe)
    {
        _pack  = pack;
        _tribe = tribe;
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

        // Action and build button bottom center, the row of the Hud buttons is empty at these stages
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
        _action = NewActionButton(ACTION_ACCENT);
        _action.Pressed += () => EmitSignal(SignalName.ActionPressed);
        row.AddChild(_action);
        _build = NewActionButton(BUILD_ACCENT);
        _build.Pressed += () => EmitSignal(SignalName.BuildHutPressed);
        row.AddChild(_build);
    }

    /*
      > Before the land animal, or without a pack and offer: everything hidden
      > Land animal and pack: pack size, the F hint from the pack on, the offered action
      > Tribe: stock line (villagers, food, huts), the build button (grey without food)
    */
    public override void _Process(double delta)
    {
        if (null == _pack)
        {
            return;
        }
        bool isTribe = _tribe.IsActive;
        bool hasPack = null != _pack.Nest || _pack.Members > 0;

        _status.Visible = hasPack || isTribe;
        _hint.Visible   = (hasPack && null != _pack.Nest && !isTribe) || isTribe;
        if (isTribe)
        {
            _status.Text = string.Format(Tr("TRIBE_STATUS"), _tribe.Units().Count, _tribe.Economy.Food, _tribe.Economy.Huts);
            _hint.Text   = Tr("TRIBE_HINT");
        }
        else if (hasPack)
        {
            _status.Text = string.Format(Tr("PACK_STATUS"), _pack.Members);
            _hint.Text   = Tr(_pack.IsStaying ? "PACK_HINT_STAY" : "PACK_HINT_FOLLOW");
        }

        // The offered action pops in when it changes
        PackOffer offer = _pack.Offer;
        if (offer != _shownOffer)
        {
            _shownOffer   = offer;
            _action.Text  = offer switch
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

        _build.Visible  = isTribe;
        _build.Disabled = !isTribe || !_tribe.Economy.CanBuildHut;
        _build.Text     = string.Format(Tr("TRIBE_BUILD_HUT"), _tribe.Economy.HutCost);
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _action)
        {
            _shownOffer = (PackOffer)(-1);   // forces the button text to re-translate in _Process
        }
    }

    /// <summary>Pill button without focus: Space acts through the game, a focused button would fire twice.</summary>
    private static Button NewActionButton(Color accent)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(280, 54),
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
}
