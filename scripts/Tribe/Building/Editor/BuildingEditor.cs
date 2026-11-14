// NOTE:
//   The village builder of the tribe, opened by Main with B or the hub button. Full
//   screen over the paused game, which stays visible behind it out of focus (the own
//   island with the village, Main hides the HUD meanwhile). Icon tabs on top pick the building
//   function (dwelling, storehouse, market, totem, watchtower, warrior hut), each shows
//   its own draft as a live icon.
//   Left: the block palette, six group icons with three blocks each. Middle: the draft on a patch of ground, a picked block
//   follows the pointer as a ghost and a click drops it: it lands on the ground or on
//   the block pointed at. Right click removes a block (the ones above fall), left drag
//   turns the view, the wheel zooms, R turns the picked block. Right: what the function
//   costs and does, the templates menu and the paint menu (patterns and paints: they go with the next block
//   and repaint a block clicked while none is picked). Below: rotate, clear, cancel and done.
//   Blocks are looks only. "Done" hands every draft to the tribe (Applied), "Cancel"
//   drops the sitting.
//
//   This class is the coordinator: it keeps the public API, creates the session
//   (BuildingEditorSession), the stage and the views (Views/), wires their events and
//   routes the input of the stage.
//
//   var editor = new BuildingEditor();
//   editor.Applied += session => tribe.TakeDesigns(session.Result);
//   editor.Closed  += () => GetTree().Paused = false;
//   AddChild(editor);
//   editor.Open(tribe.DesignOf);

//
//   Chapters:
//     STATE AND API   Signals, session and the calls Main and the tests use.
//     ROUTE INPUT     Hands the input of the stage to the session.
//     REFRESH         Tabs, info line and the views.
//     BUILD EDITOR    Background, rows, stage and views.
//

using System;
using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class BuildingEditor : CanvasLayer
{
    // ===========================================================================
    // ============================   STATE AND API   ============================
    // ===========================================================================
    //
    //   Applied    The player finished: the session holds the result per function (a C# event: a session is no Variant).
    //   Closed     The editor closed, finished or cancelled: Main resumes the game.
    //   IsOpen     Whether the editor shows.
    //   Session    The sitting: drafts, function shown, picked block.
    //   Open       Shows the editor with copies of the tribe's designs, the game must already be paused.
    //   Finish     Emits Applied and closes.
    //   Cancel     Closes without Applied.
    //

    public event Action<BuildingEditorSession> Applied;

    [Signal] public delegate void ClosedEventHandler();

    public bool IsOpen => null != _root && _root.Visible;

    public BuildingEditorSession Session { get; } = new();

    /// <summary>Shows the editor for the designs of the tribe.</summary>
    /// <param name="designOf">(Required) The current design of a function, copied into the drafts.</param>
    /// <example><code>editor.Open(tribe.DesignOf);</code></example>
    public void Open(Func<BuildingKind, BuildingDesign> designOf)
    {
        _root.Visible = true;
        _stage.ResetCamera();
        _stage.HideGhost();
        Session.Begin(designOf);
        Log.Verbose("BuildingEditor", "Opened.");
    }

    /// <summary>Hands the drafts to the tribe and closes.</summary>
    public void Finish()
    {
        if (!IsOpen)
        {
            return;
        }
        Applied?.Invoke(Session);
        Log.Verbose("BuildingEditor", $"Finished, {Session.Draft.Kind} shown with {Session.Draft.Blocks.Count} block(s).");
        Close();
    }

    public void Cancel()
    {
        if (!IsOpen)
        {
            return;
        }
        Log.Verbose("BuildingEditor", "Cancelled.");
        Close();
    }

    private void Close()
    {
        _root.Visible = false;
        EmitSignal(SignalName.Closed);
    }

    // ===========================================================================
    // =============================   ROUTE INPUT   =============================
    // ===========================================================================
    //
    //   OnStageInput      Wheel zooms, right click removes, a left click drops, a left drag turns, hover moves the ghost.
    //   _UnhandledInput   R turns the picked block, Escape cancels.
    //

    private void OnStageInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                _stage.Zoom(MouseButton.WheelUp == wheel.ButtonIndex ? 1f : -1f);
                _stage.Hover(wheel.Position);
                break;

            // Right click: remove the block under the cursor
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } right:
                if (_stage.CellAt(right.Position) is BuildingStage.Hit { Block: >= 0 } removed)
                {
                    Session.Remove(removed.Block);
                    _stage.Hover(right.Position);
                }
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.Pressed:
                _stage.Press(left.Position);
                break;

            // Left release: a click without rotation drops the picked block, without one it repaints the block clicked
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_stage.Release() && _stage.CellAt(left.Position) is BuildingStage.Hit hit)
                {
                    if (null != Session.Selected)
                    {
                        Session.PlaceAt(hit.X, hit.Z);
                    }
                    else
                    {
                        Session.Repaint(hit.Block);
                    }
                    _stage.Hover(left.Position);
                }
                break;

            case InputEventMouseMotion motion when _stage.IsPressed:
                _stage.Orbit(motion);
                break;

            case InputEventMouseMotion hover:
                _stage.Hover(hover.Position);
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        if (Key.R == key.Keycode)
        {
            Session.Rotate();
            GetViewport().SetInputAsHandled();
        }
        else if (Key.Escape == key.Keycode)
        {
            Cancel();
            GetViewport().SetInputAsHandled();
        }
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh         Title, tabs, buttons, palette, sidebar and info line.
    //   RefreshInfo     Name of the hovered or picked block, else the hint.
    //   _Notification   Re-translates after a locale change, hides the ghost when the pointer leaves the stage.
    //

    private void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }
        _title.Text  = Tr("BUILDING_EDITOR_TITLE");
        _done.Text   = Tr("BUILDING_DONE");
        _cancel.Text = Tr("BUILDING_CANCEL");
        _rotate.Text = string.Format(Tr("BUILDING_ROTATE"), Session.Turns * 90);
        _clear.Text  = Tr("BUILDING_CLEAR");
        foreach ((BuildingKind kind, (Button tab, BuildingIcon icon)) in _tabs)
        {
            tab.TooltipText = Tr($"BUILDING_{kind}".ToUpperInvariant());
            tab.SetPressedNoSignal(kind == Session.Kind);
            icon.Display(Session.DraftOf(kind));
        }
        _palette.Refresh();
        _sidebar.Refresh();
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        BlockKind? shown = _palette.Hovered ?? Session.Selected;
        _info.Text = shown is BlockKind kind
            ? Tr($"BLOCK_{kind}".ToUpperInvariant())
            : Tr("BUILDING_HINT");
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _root)
        {
            Refresh();
        }
    }

    // ===========================================================================
    // ============================   BUILD EDITOR   =============================
    // ===========================================================================
    //
    //   _Ready      Keeps running while paused, builds everything hidden, follows the session:
    //               the blurred game is the backdrop, the stage fills the screen on it, the panels lie on top
    //               and let the mouse through between them.
    //   InPanel     A view on a dark, see-through card, so it reads in front of the island.
    //   BuildTop    Title and the function tabs, each the live icon of its draft.
    //   BuildFoot   Info line, rotate, clear, cancel and done.
    //

    private const int MARGIN = 24;
    private const int TAB_ICON_PIXELS = 60;
    private static readonly Vector2 TAB_SIZE = new(88, 68);
    private static readonly Color ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color TOOL_ACCENT = new(0.6f, 0.7f, 0.9f);
    private static readonly Color BACKGROUND = new(0.05f, 0.09f, 0.12f, 0.84f);
    private static readonly Shader BACKDROP_SHADER = GD.Load<Shader>("res://shaders/editor_blur.gdshader");

    private readonly Dictionary<BuildingKind, (Button Tab, BuildingIcon Icon)> _tabs = new();
    private Control _root;
    private Label _title;
    private Label _info;
    private Button _done;
    private Button _cancel;
    private Button _rotate;
    private Button _clear;
    private BuildingStage _stage;
    private BuildingPalette _palette;
    private BuildingSidebar _sidebar;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer       = 20;

        // Full screen over the paused game, it swallows every click
        _root = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        // Backdrop: the paused game, out of focus and dimmed
        var backdrop = new ColorRect { Material = new ShaderMaterial { Shader = BACKDROP_SHADER }, MouseFilter = Control.MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(backdrop);

        _stage = new BuildingStage();
        _stage.Setup(Session);
        _stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _stage.GuiInput    += OnStageInput;
        _stage.MouseExited += _stage.HideGhost;
        _root.AddChild(_stage);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, MARGIN);
        }
        _root.AddChild(margin);
        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 12);
        margin.AddChild(rows);

        rows.AddChild(BuildTop());

        // Workbench: palette, the free view onto the stage, sidebar
        var workbench = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        workbench.AddThemeConstantOverride("separation", 16);
        rows.AddChild(workbench);

        _palette = new BuildingPalette();
        _palette.Setup(Session);
        _palette.GroupPicked  += Session.ShowGroup;
        _palette.BlockPicked  += kind => Session.Select(kind);
        _palette.HoverChanged += RefreshInfo;
        workbench.AddChild(InPanel(_palette));
        workbench.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        _sidebar = new BuildingSidebar();
        _sidebar.Setup(Session);
        _sidebar.PatternPicked += Session.PickPattern;
        _sidebar.PaintPicked   += Session.PickPaint;
        _sidebar.TemplatePicked += Session.ApplyTemplate;
        workbench.AddChild(InPanel(_sidebar));

        rows.AddChild(BuildFoot());

        Session.Changed          += Refresh;
        Session.SelectionChanged += Refresh;
    }

    private static Control InPanel(Control view)
    {
        var panel = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        StyleBoxFlat card = EditorStyle.Pill(BACKGROUND, new Color(EditorStyle.RIM_COLOR, 0.35f));
        card.SetCornerRadiusAll(14);
        card.SetContentMarginAll(12);
        panel.AddThemeStyleboxOverride("panel", card);
        panel.AddChild(view);
        return panel;
    }

    private Control BuildTop()
    {
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        top.AddThemeConstantOverride("separation", 8);
        _title = CreatureEditorUi.NewLabel(36);
        GameFonts.UseDisplay(_title);
        _title.CustomMinimumSize = new Vector2(320, 0);
        top.AddChild(_title);
        foreach (BuildingKind kind in Enum.GetValues<BuildingKind>())
        {
            var tab = new Button
            {
                CustomMinimumSize = TAB_SIZE,
                FocusMode         = Control.FocusModeEnum.None,
                ToggleMode        = true,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            };
            EditorStyle.ApplyTab(tab, ACCENT);
            var icon = new BuildingIcon(TAB_ICON_PIXELS) { Position = new Vector2((TAB_SIZE.X - TAB_ICON_PIXELS) * 0.5f, 4f) };
            tab.AddChild(icon);
            BuildingKind shown = kind;
            tab.Pressed += () =>
            {
                Session.ShowKind(shown);
                Refresh();     // also re-presses the tab when the function did not change
            };
            top.AddChild(tab);
            _tabs[kind] = (tab, icon);
        }
        return top;
    }

    private Control BuildFoot()
    {
        var foot = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        foot.AddThemeConstantOverride("separation", 14);
        _info = CreatureEditorUi.NewLabel(16);
        _info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _info.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        foot.AddChild(_info);

        _rotate = CreatureEditorUi.NewPillButton(new Vector2(220, 48), 16);
        EditorStyle.ApplyButton(_rotate, TOOL_ACCENT);
        _rotate.Pressed += Session.Rotate;
        foot.AddChild(_rotate);
        _clear = CreatureEditorUi.NewPillButton(new Vector2(150, 48), 16);
        EditorStyle.ApplyButton(_clear, TOOL_ACCENT);
        _clear.Pressed += Session.Clear;
        foot.AddChild(_clear);

        _cancel = CreatureEditorUi.NewPillButton(new Vector2(160, 48), 18);
        EditorStyle.ApplyButton(_cancel, EditorStyle.RIM_COLOR);
        _cancel.Pressed += Cancel;
        foot.AddChild(_cancel);
        _done = CreatureEditorUi.NewPillButton(new Vector2(220, 48), 20);
        EditorStyle.ApplyButton(_done, ACCENT);
        _done.Pressed += Finish;
        foot.AddChild(_done);
        return foot;
    }
}
