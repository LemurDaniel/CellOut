// NOTE:
//   Frame of the creature editor: the grey-out over the paused game and the full screen
//   panel (animated water with a membrane edge) that slides in from the left. It holds
//   the title and stacks the rows the CreatureEditor hands it; the workbench is the row
//   that only shows while sculpting (palette, turntable, stats).
//
//   var frame = new CreatureEditorFrame();
//   frame.Setup(session);
//   AddChild(frame);
//   frame.AddRow(tabs);
//   frame.AddWorkbench(palette, stage, stats);
//   frame.Visible = true;
//   frame.SlideTo(1f);

//
//   Chapters:
//     BUILD FRAME   Grey-out, panel, margin, the row stack and the title.
//     SLIDE         Slides the panel in and out and fades the grey-out.
//

using Godot;

namespace CellOut;

public partial class CreatureEditorFrame : Control
{
    // ===========================================================================
    // =============================   BUILD FRAME   =============================
    // ===========================================================================
    //
    //   Setup          Builds grey-out, panel and title, hidden and closed.
    //   AddRow         Adds a row below the last one.
    //   AddWorkbench   Adds the sculpting row with its parts side by side.
    //   Refresh        Title of the phase, the workbench only while sculpting.
    //

    private const float PANEL_SHARE = 1f;              // full screen, the membrane edge sits just off screen
    private const float EDGE_PIXELS = 40f;
    private const int PANEL_MARGIN = 24;
    private static readonly Shader GREYOUT_SHADER = GD.Load<Shader>("res://shaders/greyout.gdshader");
    private static readonly Shader PANEL_SHADER = GD.Load<Shader>("res://shaders/editor_panel.gdshader");

    private CreatureEditorSession _session;
    private ShaderMaterial _greyout;
    private ShaderMaterial _panelBackground;
    private Control _panel;
    private VBoxContainer _rows;
    private Label _titleLabel;
    private HBoxContainer _workbench;

    /// <summary>Builds the frame, call once before AddChild.</summary>
    /// <param name="session">(Required) The sitting, its phase decides title and workbench.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session    = session;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible     = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Grey-out over the paused game (the clay blob of the cutscene breathes behind it)
        _greyout = new ShaderMaterial { Shader = GREYOUT_SHADER };
        var greyout = new ColorRect { Material = _greyout, MouseFilter = MouseFilterEnum.Stop };
        greyout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(greyout);

        // Panel: full height on the left, animated water with a membrane edge, slides by its own width
        _panel = new Control { AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 1f };
        AddChild(_panel);
        _panelBackground = new ShaderMaterial { Shader = PANEL_SHADER };
        _panelBackground.SetShaderParameter("edge_px", EDGE_PIXELS);
        var background = new ColorRect { Material = _panelBackground, MouseFilter = MouseFilterEnum.Stop };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel.AddChild(background);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", PANEL_MARGIN);
        margin.AddThemeConstantOverride("margin_top", PANEL_MARGIN - 4);
        margin.AddThemeConstantOverride("margin_right", PANEL_MARGIN + (int)EDGE_PIXELS);
        margin.AddThemeConstantOverride("margin_bottom", PANEL_MARGIN - 4);
        _panel.AddChild(margin);

        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 12);
        margin.AddChild(_rows);

        _titleLabel = CreatureEditorUi.NewLabel(38);
        GameFonts.UseDisplay(_titleLabel);
        _rows.AddChild(_titleLabel);
    }

    /// <summary>Adds a row below the title and the rows added before.</summary>
    public void AddRow(Control row)
    {
        _rows.AddChild(row);
    }

    /// <summary>Adds the sculpting row, it fills the height left and hides during the type choice.</summary>
    /// <param name="parts">(Required) Its parts from left to right: tile palette, turntable, stats.</param>
    /// <example><code>frame.AddWorkbench(palette, stage, stats);</code></example>
    public void AddWorkbench(params Control[] parts)
    {
        _workbench = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _workbench.AddThemeConstantOverride("separation", 16);
        _rows.AddChild(_workbench);
        foreach (Control part in parts)
        {
            _workbench.AddChild(part);
        }
    }

    /// <summary>Updates the title and shows the workbench once the type is chosen.</summary>
    public void Refresh()
    {
        _titleLabel.Text   = Tr(_session.IsChoosingType ? "CREATURE_CHOOSE_TITLE" : "CREATURE_EDITOR_TITLE");
        _workbench.Visible = !_session.IsChoosingType;
    }

    // ===========================================================================
    // ================================   SLIDE   ================================
    // ===========================================================================
    //
    //   _Ready      Starts closed and follows the window size.
    //   SlideTo     Tweens the panel and the grey-out to open (1) or closed (0).
    //   ApplySlide  Sizes the panel to its screen share, moves it by its own width
    //               and sets the grey-out amount.
    //

    private const float SLIDE_SECONDS = 0.4f;

    private Tween _slideTween;
    private float _slide;

    public override void _Ready()
    {
        ApplySlide(0f);
        GetViewport().SizeChanged += () => ApplySlide(_slide);
    }

    /// <summary>Slides the panel, the frame hides itself once it is closed.</summary>
    /// <param name="target">(Required) 1 open, 0 closed.</param>
    public void SlideTo(float target)
    {
        _slideTween?.Kill();
        _slideTween = CreateTween()
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .SetTrans(target > 0f ? Tween.TransitionType.Quart : Tween.TransitionType.Cubic)
            .SetEase(target > 0f ? Tween.EaseType.Out : Tween.EaseType.In);
        _slideTween.TweenMethod(Callable.From<float>(ApplySlide), _slide, target, SLIDE_SECONDS);
        if (target <= 0f)
        {
            _slideTween.TweenCallback(Callable.From(() => Visible = false));
        }
    }

    private void ApplySlide(float slide)
    {
        _slide = slide;
        Vector2 screen = GetViewportRect().Size;
        float   width  = screen.X * PANEL_SHARE + EDGE_PIXELS;
        _panel.OffsetTop    = 0f;
        _panel.OffsetBottom = 0f;
        _panel.OffsetLeft   = -(1f - slide) * width;
        _panel.OffsetRight  = _panel.OffsetLeft + width;
        _panelBackground.SetShaderParameter("rect_size", new Vector2(width, screen.Y));
        _greyout.SetShaderParameter("amount", Mathf.Clamp(slide, 0f, 1f));
    }
}
