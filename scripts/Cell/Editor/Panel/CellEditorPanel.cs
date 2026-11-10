// NOTE:
//   The frame of the cell editor: a grey-out over the paused game
//   (shaders/greyout.gdshader) and the panel that slides in from the left (background:
//   shaders/editor_panel.gdshader, a wobbling membrane edge) with header (title, diet
//   bar), body (palette left, preview right, both handed in) and footer (info card,
//   points badge, done button). The panel and its highlights take on a hint of the diet.
//   A view: CellEditor feeds it through Refresh and ShowInfo, it reports DonePressed,
//   SlidOut and AccentChanged.
//   Looks and tweens: EditorStyle.
//
//   var panel = new CellEditorPanel();
//   panel.Setup(palette, preview);
//   panel.DonePressed += editor.Finish;
//   AddChild(panel);
//   panel.Open();
//   panel.Refresh(points, diet, previewDiet);

//
//   Chapters:
//     EVENTS AND SETUP   What the panel reports and the views it frames.
//     SLIDE              Slides the panel in and out and fades the grey-out.
//     DIET TINT          The panel and its highlights take on a hint of the diet.
//     REFRESH            Title, points, diet preview and the info card.
//     BUILD UI           Grey-out, panel, header, body and footer.
//

using System;
using Godot;

namespace CellOut;

public partial class CellEditorPanel : Control
{
    // ===========================================================================
    // ==========================   EVENTS AND SETUP   ===========================
    // ===========================================================================
    //
    //   DonePressed     The done button was clicked.
    //   SlidOut         The panel finished sliding out and is hidden now.
    //   AccentChanged   The diet tint moved on: the accent color for other highlights.
    //   Setup           Hands in the palette and the preview shown in the body.
    //

    /// <summary>The done button was clicked.</summary>
    public event Action DonePressed;

    /// <summary>The panel finished sliding out and hid itself.</summary>
    public event Action SlidOut;

    /// <summary>The diet tint moved on: the accent color of the diet, for highlights outside the panel.</summary>
    public event Action<Color> AccentChanged;

    private Control _palette;
    private Control _preview;

    /// <summary>Hands in the two views of the body, call before the panel enters the tree.</summary>
    /// <param name="palette">(Required) The part palette, shown left.</param>
    /// <param name="preview">(Required) The cell preview, shown right.</param>
    public void Setup(Control palette, Control preview)
    {
        _palette = palette;
        _preview = preview;
    }

    // ===========================================================================
    // ================================   SLIDE   ================================
    // ===========================================================================
    //
    //   Open        Shows the panel, still slid out.
    //   SlideTo     Tweens the panel and the grey-out to open (1) or closed (0).
    //   ApplySlide  Sizes the panel to its screen share, moves it by its own width
    //               and sets the grey-out amount.
    //

    private const float SLIDE_SECONDS = 0.4f;
    private const float PANEL_SHARE = 0.65f;         // of the screen width, the paused game stays visible right of it
    private const float EDGE_PIXELS = 40f;           // strip for the wobbling membrane, see editor_panel.gdshader

    private Tween _slideTween;
    private float _slide;

    /// <summary>Makes the panel visible for a new session, SlideTo(1) brings it in.</summary>
    public void Open()
    {
        Visible     = true;
        _lastPoints = -1;
    }

    /// <summary>Slides the panel in or out.</summary>
    /// <param name="target">(Required) 1 for open, 0 for closed. Closed hides the panel at the end (SlidOut).</param>
    public void SlideTo(float target)
    {
        _slideTween?.Kill();
        _slideTween = CreateTween()
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .SetTrans(target > 0f ? Tween.TransitionType.Quart : Tween.TransitionType.Cubic)
            .SetEase(target > 0f ? Tween.EaseType.Out : Tween.EaseType.In);
        _slideTween.TweenMethod(Callable.From<float>(ApplySlide), _slide, target, SLIDE_SECONDS);

        // Closed: hide everything and drop the draft from the preview once the panel is out
        if (target <= 0f)
        {
            _slideTween.TweenCallback(Callable.From(() =>
            {
                Visible = false;
                SlidOut?.Invoke();
            }));
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

    // ===========================================================================
    // ==============================   DIET TINT   ==============================
    // ===========================================================================
    //
    //   TintTo      Eases the tint to the diet the draft would lead to.
    //   ApplyTint   Sets panel shader and done button to the tint of a diet and
    //               reports the accent (AccentChanged) for dish, rim dots and arrow.
    //
    //   The tint follows the preview diet (current diet plus the draft), so placing a
    //   jaw already blushes the panel a little.
    //

    private const float TINT_SECONDS = 0.6f;

    private float _tint;
    private Tween _tintTween;

    private void TintTo(float diet)
    {
        if (Mathf.IsEqualApprox(diet, _tint))
        {
            return;
        }
        _tintTween?.Kill();
        _tintTween = CreateTween()
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _tintTween.TweenMethod(Callable.From<float>(ApplyTint), _tint, diet, TINT_SECONDS);
    }

    private void ApplyTint(float diet)
    {
        _tint = diet;
        (Color color, float amount) = EditorStyle.DietTint(diet);
        _panelBackground.SetShaderParameter("tint_color", color);
        _panelBackground.SetShaderParameter("tint_amount", amount);

        Color accent = EditorStyle.Accent(diet);
        AccentChanged?.Invoke(accent);
        EditorStyle.ApplyButton(_doneButton, accent);
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh        Updates title, points, done button, diet preview and the tint.
    //   ShowInfo       Name and description of a part in the info card, empty without one.
    //   UnlockHintKey  Which diet side unlocks a locked part.
    //

    private int _lastPoints = -1;

    /// <summary>Updates the texts and the diet bar, also after a locale change.</summary>
    /// <param name="points">(Required) Evolution points left.</param>
    /// <param name="diet">(Required) The diet of the player now, -1 herbivore .. 1 carnivore.</param>
    /// <param name="previewDiet">(Required) The diet the draft would lead to, it also sets the tint.</param>
    public void Refresh(int points, float diet, float previewDiet)
    {
        _titleLabel.Text      = Tr("EDITOR_TITLE");
        _pointsLabel.Text     = string.Format(Tr("EDITOR_POINTS"), points);
        _doneButton.Text      = Tr("EDITOR_DONE");
        _dietBar.Value        = diet;
        _dietBar.PreviewValue = previewDiet;
        TintTo(_dietBar.PreviewValue);

        // Points changed: the badge pops, all spent: the done button invites
        if (_lastPoints >= 0 && points != _lastPoints)
        {
            EditorStyle.Punch(_pointsBadge, 1.15f);
            if (0 == points)
            {
                EditorStyle.Punch(_doneButton, 1.12f);
            }
        }
        _lastPoints = points;
    }

    /// <summary>Fills the info card.</summary>
    /// <param name="info">(Required) The dragged, hovered or selected part, null empties the card.</param>
    /// <param name="isUnlocked">(Optional) False shows how to unlock the part instead of its description. Defaults to true.</param>
    public void ShowInfo(PartInfo info, bool isUnlocked = true)
    {
        if (null == info)
        {
            _infoName.Text = "";
            _infoText.Text = "";
            return;
        }
        _infoName.Text = Tr(info.NameKey);
        _infoName.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[info.Diet]);
        _infoText.Text = isUnlocked ? Tr(info.DescriptionKey) : Tr(UnlockHintKey(info));
    }

    private static string UnlockHintKey(PartInfo info)
    {
        return info.MaxDiet < 1f && info.MinDiet > -1f
            ? "EDITOR_LOCKED_OMNIVORE"
            : (info.MaxDiet < 1f ? "EDITOR_LOCKED_HERBIVORE" : "EDITOR_LOCKED_CARNIVORE");
    }

    // ===========================================================================
    // ==============================   BUILD UI   ===============================
    // ===========================================================================
    //
    //   _Ready         Builds everything hidden and closed, follows the window size.
    //   BuildPanel     Grey-out over the game, the membrane panel with header, body and footer.
    //   BuildFooter    Info card, points badge and the done button.
    //   NewLabel       Label with outline, translated here.
    //

    private const int PANEL_MARGIN = 20;
    private static readonly Shader GREYOUT_SHADER = GD.Load<Shader>("res://shaders/greyout.gdshader");
    private static readonly Shader PANEL_SHADER = GD.Load<Shader>("res://shaders/editor_panel.gdshader");

    private ShaderMaterial _greyout;
    private ShaderMaterial _panelBackground;
    private Control _panel;
    private Label _titleLabel;
    private PanelContainer _pointsBadge;
    private Label _pointsLabel;
    private Label _infoName;
    private Label _infoText;
    private Button _doneButton;
    private DietBar _dietBar;

    public override void _Ready()
    {
        BuildPanel();
        Visible = false;
        ApplySlide(0f);
        ApplyTint(0f);
        GetViewport().SizeChanged += () => ApplySlide(_slide);
    }

    private void BuildPanel()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Grey-out over the paused game, it also swallows clicks meant for the HUD
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

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);

        // Header: title, the diet bar with preview marker on the right
        var header = new HBoxContainer();
        layout.AddChild(header);
        _titleLabel = NewLabel(34);
        GameFonts.UseDisplay(_titleLabel);
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _dietBar = new DietBar { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        header.AddChild(_titleLabel);
        header.AddChild(_dietBar);

        // Body: tile palette left, rotatable preview right
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        layout.AddChild(body);
        body.AddChild(_palette);
        body.AddChild(_preview);

        BuildFooter(layout);
    }

    private void BuildFooter(Container parent)
    {
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 14);
        parent.AddChild(footer);

        // Info card: name in the diet color, description below, fixed height so nothing jumps
        var card = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0f, 62f),
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        card.AddThemeConstantOverride("separation", 0);
        footer.AddChild(card);
        _infoName = NewLabel(20);
        _infoText = NewLabel(14);
        _infoText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        card.AddChild(_infoName);
        card.AddChild(_infoText);

        // Points badge and done button
        _pointsBadge = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        _pointsBadge.AddThemeStyleboxOverride("panel", EditorStyle.Pill(new Color(0.08f, 0.2f, 0.2f), EditorStyle.DIET_COLORS[DietClass.Omnivore], 0.4f));
        _pointsLabel = NewLabel(17);
        _pointsLabel.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[DietClass.Omnivore].Lightened(0.4f));
        _pointsBadge.AddChild(_pointsLabel);
        footer.AddChild(_pointsBadge);

        _doneButton = new Button
        {
            CustomMinimumSize = new Vector2(150, 48),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode         = FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        _doneButton.AddThemeFontSizeOverride("font_size", 20);
        _doneButton.Pressed      += () => DonePressed?.Invoke();
        _doneButton.MouseEntered += () => EditorStyle.ScaleTo(_doneButton, 1.06f);
        _doneButton.MouseExited  += () => EditorStyle.ScaleTo(_doneButton, 1f);
        footer.AddChild(_doneButton);
    }

    /// <summary>A label in the look of the editor: outlined, not translated by Godot (the editor sets translated text).</summary>
    /// <param name="fontSize">(Required) Font size in pixels, the outline scales with it.</param>
    /// <example><code>Label title = CellEditorPanel.NewLabel(17);</code></example>
    public static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }
}
