// NOTE:
//   The village builder of the tribe, opened by Main with B or the panel button. Full
//   screen over the paused game, like the creature editor. Tabs on top pick the building
//   function (dwelling, storehouse, market, totem, watchtower), each has its own draft.
//   Left: the block palette. Middle: the draft on a patch of ground, a picked block
//   follows the pointer as a ghost and a click drops it: it lands on the ground or on
//   the block pointed at. Right click removes a block (the ones above fall), left drag
//   turns the view, the wheel zooms, R turns the picked block. Right: what the function
//   costs and does, its templates and the paints. Below: rotate, clear, cancel and done.
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

            // Left release: a click without rotation drops the picked block
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (_stage.Release() && _stage.CellAt(left.Position) is BuildingStage.Hit hit)
                {
                    Session.PlaceAt(hit.X, hit.Z);
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
        foreach ((BuildingKind kind, Button tab) in _tabs)
        {
            tab.Text = Tr($"BUILDING_{kind}".ToUpperInvariant());
            tab.SetPressedNoSignal(kind == Session.Kind);
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
    //   _Ready      Keeps running while paused, builds everything hidden, follows the session.
    //   BuildTop    Title and the function tabs.
    //   BuildFoot   Info line, rotate, clear, cancel and done.
    //

    private const int MARGIN = 24;
    private static readonly Color ACCENT = new(0.8f, 0.6f, 0.4f);
    private static readonly Color TOOL_ACCENT = new(0.6f, 0.7f, 0.9f);
    private static readonly Color BACKGROUND = new(0.05f, 0.09f, 0.12f);

    private readonly Dictionary<BuildingKind, Button> _tabs = new();
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
        var background = new ColorRect { Color = BACKGROUND, MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, MARGIN);
        }
        _root.AddChild(margin);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 12);
        margin.AddChild(rows);

        rows.AddChild(BuildTop());

        // Workbench: palette, stage, sidebar
        var workbench = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        workbench.AddThemeConstantOverride("separation", 16);
        rows.AddChild(workbench);

        _palette = new BuildingPalette();
        _palette.Setup(Session);
        _palette.BlockPicked  += kind => Session.Select(kind);
        _palette.HoverChanged += RefreshInfo;
        workbench.AddChild(_palette);

        _stage = new BuildingStage();
        _stage.Setup(Session);
        _stage.GuiInput    += OnStageInput;
        _stage.MouseExited += _stage.HideGhost;
        workbench.AddChild(_stage);

        _sidebar = new BuildingSidebar();
        _sidebar.Setup(Session);
        _sidebar.TemplatePressed += Session.ApplyTemplate;
        _sidebar.PaintPicked     += Session.PickPaint;
        workbench.AddChild(_sidebar);

        rows.AddChild(BuildFoot());

        Session.Changed          += Refresh;
        Session.SelectionChanged += Refresh;
    }

    private Control BuildTop()
    {
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 8);
        _title = CreatureEditorUi.NewLabel(36);
        GameFonts.UseDisplay(_title);
        _title.CustomMinimumSize = new Vector2(320, 0);
        top.AddChild(_title);
        foreach (BuildingKind kind in Enum.GetValues<BuildingKind>())
        {
            var tab = new Button
            {
                CustomMinimumSize = new Vector2(150, 44),
                FocusMode         = Control.FocusModeEnum.None,
                ToggleMode        = true,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            };
            tab.AddThemeFontSizeOverride("font_size", 18);
            EditorStyle.ApplyTab(tab, ACCENT);
            BuildingKind shown = kind;
            tab.Pressed += () =>
            {
                Session.ShowKind(shown);
                Refresh();     // also re-presses the tab when the function did not change
            };
            top.AddChild(tab);
            _tabs[kind] = tab;
        }
        return top;
    }

    private Control BuildFoot()
    {
        var foot = new HBoxContainer();
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
