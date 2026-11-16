// NOTE:
//   One panel of the creature editor beside the turntable: a framed column of sections that
//   scrolls instead of pushing the footer off a low window. The views (CreatureToolbar,
//   CreatureMotionPanel) fill it with headlines, slider rows and their own controls.
//
//   var panel = new CreaturePanel();
//   panel.Setup(262f);
//   panel.AddHeadline("CREATURE_SECTION_FORM");
//   panel.AddSliderRow(label, slider);
//   panel.Sections.AddChild(button);
//   panel.Translate();   // on every refresh: the headlines follow the language

//
//   Chapters:
//     PANEL   The framed column, headlines and slider rows.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreaturePanel : ScrollContainer
{
    // ===========================================================================
    // ================================   PANEL   ================================
    // ===========================================================================
    //
    //   Sections       The column the views add their controls to.
    //   Setup          Frame and column in the given width.
    //   AddHeadline    The headline of a section, a thin line above every one but the first.
    //   AddSliderRow   A label and its slider side by side.
    //   AddSwatches    A wrapping row of color swatches.
    //   Translate      Headlines in the current language.
    //

    private const float SLIDER_LABEL_WIDTH = 124f;   // labels of the slider rows, their sliders take the rest

    private readonly List<(string Key, Label Label)> _headlines = new();

    /// <summary>The column of the panel, top to bottom.</summary>
    public VBoxContainer Sections { get; private set; }

    /// <summary>Builds the panel, call once before it enters the tree.</summary>
    /// <param name="width">(Required) Width of the panel, room for a scroll bar included.</param>
    public void Setup(float width)
    {
        HorizontalScrollMode = ScrollMode.Disabled;
        CustomMinimumSize    = new Vector2(width, 0f);
        var frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StyleBoxFlat style = EditorStyle.Pill(new Color(0.04f, 0.09f, 0.13f, 0.82f), new Color(EditorStyle.RIM_COLOR, 0.45f));
        style.SetContentMarginAll(14);
        frame.AddThemeStyleboxOverride("panel", style);
        AddChild(frame);
        Sections = new VBoxContainer();
        Sections.AddThemeConstantOverride("separation", 9);
        frame.AddChild(Sections);
    }

    /// <summary>Adds the headline of a section.</summary>
    /// <param name="key">(Required) Translation key of the headline.</param>
    public void AddHeadline(string key)
    {
        if (Sections.GetChildCount() > 0)
        {
            Sections.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore });
        }
        Label label = CreatureEditorUi.NewLabel(17);
        Sections.AddChild(label);
        _headlines.Add((key, label));
    }

    /// <summary>Adds a label and its slider side by side.</summary>
    /// <returns>The row, for sections that hide it.</returns>
    public HBoxContainer AddSliderRow(Label label, HSlider slider)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        label.CustomMinimumSize = new Vector2(SLIDER_LABEL_WIDTH, 0f);
        label.AddThemeFontSizeOverride("font_size", 13);
        slider.FocusMode           = FocusModeEnum.None;
        slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        slider.SizeFlagsVertical   = SizeFlags.ShrinkCenter;
        row.AddChild(label);
        row.AddChild(slider);
        Sections.AddChild(row);
        return row;
    }

    /// <summary>Adds a wrapping row of color swatches.</summary>
    /// <param name="colors">(Required) The colors, null stands for "none" (shown by the view).</param>
    /// <param name="pixels">(Required) Edge of a swatch.</param>
    /// <param name="swatches">(Required) Takes every swatch with its color, for the refresh of the view.</param>
    /// <param name="picked">(Required) Called with the color of a clicked swatch.</param>
    public HFlowContainer AddSwatches(IEnumerable<Color?> colors, int pixels, List<(Color? Color, Button Swatch)> swatches, System.Action<Color?> picked)
    {
        var row = new HFlowContainer();
        row.AddThemeConstantOverride("h_separation", 7);
        row.AddThemeConstantOverride("v_separation", 7);
        Sections.AddChild(row);
        foreach (Color? color in colors)
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(pixels, pixels), FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            swatch.Pressed      += () => picked(color);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            row.AddChild(swatch);
            swatches.Add((color, swatch));
        }
        return row;
    }

    /// <summary>Shows the headlines in the current language.</summary>
    public void Translate()
    {
        foreach ((string key, Label label) in _headlines)
        {
            label.Text = Tr(key);
        }
    }
}
