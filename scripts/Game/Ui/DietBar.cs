// NOTE:
//   Diet bar: green (herbivore) on the left, yellow (omnivore) in the middle,
//   red (carnivore) on the right, a marker at the current diet and an optional
//   hollow marker for the preview in the editor. Labels below, translated.
//
//   var bar = new DietBar();
//   bar.Value = player.Diet;              // -1 .. 1
//   bar.PreviewValue = float.NaN;         // no preview marker

//
//   Chapters:
//     DIET BAR   Values, layout and drawing.
//

using Godot;

namespace CellOut;

public partial class DietBar : VBoxContainer
{
    // ===========================================================================
    // ==============================   DIET BAR   ===============================
    // ===========================================================================
    //
    //   Value          Current diet, moves the solid marker.
    //   PreviewValue   Diet after the editor changes, NaN hides the hollow marker.
    //   _Ready         Bar canvas and the three labels.
    //   _Notification  Re-translates the labels.
    //   DrawBar        Gradient, zone ticks and markers.
    //

    private static readonly Color HERBIVORE_COLOR = new(0.35f, 0.85f, 0.4f);
    private static readonly Color OMNIVORE_COLOR = new(0.95f, 0.85f, 0.35f);
    private static readonly Color CARNIVORE_COLOR = new(0.95f, 0.35f, 0.3f);
    private const float BAR_HEIGHT = 14f;
    private const float MARKER_HEIGHT = 22f;

    private float _value;
    private float _previewValue = float.NaN;
    private Control _canvas;
    private Label _herbivore;
    private Label _omnivore;
    private Label _carnivore;

    public float Value
    {
        get => _value;
        set { _value = Mathf.Clamp(value, -1f, 1f); _canvas?.QueueRedraw(); }
    }

    public float PreviewValue
    {
        get => _previewValue;
        set { _previewValue = float.IsNaN(value) ? value : Mathf.Clamp(value, -1f, 1f); _canvas?.QueueRedraw(); }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 2);

        _canvas = new Control { CustomMinimumSize = new Vector2(360, MARKER_HEIGHT), MouseFilter = MouseFilterEnum.Ignore };
        _canvas.Draw += DrawBar;
        AddChild(_canvas);

        var labels = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(labels);
        _herbivore = NewLabel(HorizontalAlignment.Left, HERBIVORE_COLOR);
        _omnivore  = NewLabel(HorizontalAlignment.Center, OMNIVORE_COLOR);
        _carnivore = NewLabel(HorizontalAlignment.Right, CARNIVORE_COLOR);
        labels.AddChild(_herbivore);
        labels.AddChild(_omnivore);
        labels.AddChild(_carnivore);
        ApplyTexts();
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _herbivore)
        {
            ApplyTexts();
        }
    }

    private void ApplyTexts()
    {
        _herbivore.Text = Tr("DIET_HERBIVORE");
        _omnivore.Text  = Tr("DIET_OMNIVORE");
        _carnivore.Text = Tr("DIET_CARNIVORE");
    }

    private void DrawBar()
    {
        float width = _canvas.Size.X;
        float top   = (MARKER_HEIGHT - BAR_HEIGHT) * 0.5f;

        // Gradient green -> yellow -> red as two quads with vertex colors
        DrawQuad(0f, width * 0.5f, top, HERBIVORE_COLOR, OMNIVORE_COLOR);
        DrawQuad(width * 0.5f, width, top, OMNIVORE_COLOR, CARNIVORE_COLOR);

        // Zone borders at one third and two thirds
        var tickColor = new Color(0f, 0f, 0f, 0.35f);
        _canvas.DrawLine(new Vector2(width / 3f, top), new Vector2(width / 3f, top + BAR_HEIGHT), tickColor, 2f);
        _canvas.DrawLine(new Vector2(width * 2f / 3f, top), new Vector2(width * 2f / 3f, top + BAR_HEIGHT), tickColor, 2f);

        // Preview marker hollow, current marker solid
        if (!float.IsNaN(_previewValue))
        {
            float previewX = (_previewValue + 1f) * 0.5f * width;
            _canvas.DrawRect(new Rect2(previewX - 4f, 0f, 8f, MARKER_HEIGHT), Colors.White, false, 2f);
        }
        float x = (_value + 1f) * 0.5f * width;
        _canvas.DrawRect(new Rect2(x - 3f, 0f, 6f, MARKER_HEIGHT), Colors.White);
        _canvas.DrawRect(new Rect2(x - 3f, 0f, 6f, MARKER_HEIGHT), new Color(0.02f, 0.08f, 0.1f), false, 1.5f);
    }

    private void DrawQuad(float x0, float x1, float top, Color left, Color right)
    {
        var points = new[] { new Vector2(x0, top), new Vector2(x1, top), new Vector2(x1, top + BAR_HEIGHT), new Vector2(x0, top + BAR_HEIGHT) };
        var colors = new[] { left, right, right, left };
        _canvas.DrawPolygon(points, colors);
    }

    private static Label NewLabel(HorizontalAlignment alignment, Color color)
    {
        var label = new Label
        {
            HorizontalAlignment = alignment,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter         = MouseFilterEnum.Ignore,
            AutoTranslateMode   = AutoTranslateModeEnum.Disabled,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.08f, 0.1f));
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }
}
