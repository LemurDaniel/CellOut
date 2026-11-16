// NOTE:
//   A round button that shows a skin surface (CreatureSkin) in a body color: plain clay,
//   rows of scales or scattered warts, drawn with a few shapes. Used by the CreatureToolbar
//   as the icon of the skin section and as the tiles to choose from. With Pattern set it
//   shows a paint pattern in the second color instead: the pattern icon of the toolbar.
//
//   var icon = new SkinIcon { Skin = CreatureSkin.Scales, Tint = bodyColor, CustomMinimumSize = new Vector2(44, 44) };
//   icon.Pressed += () => SkinPicked?.Invoke(icon.Skin);

//
//   Chapters:
//     SKIN ICON   What it shows and how it is drawn.
//

using Godot;

namespace CellOut;

public partial class SkinIcon : Button
{
    // ===========================================================================
    // ==============================   SKIN ICON   ==============================
    // ===========================================================================
    //
    //   Skin, Tint, IsPicked   What the icon shows, every change redraws it.
    //   _Ready                 A bare button: the icon draws itself.
    //   _Draw                  The disc in the tint, its surface and the ring of a picked icon.
    //

    private const float RIM_WIDTH = 2.5f;
    private const int SCALE_ROWS = 4;
    private static readonly Vector2[] WARTS =
    {
        new(-0.42f, -0.3f), new(0.2f, -0.5f), new(0.5f, 0.05f), new(-0.1f, 0.1f), new(-0.5f, 0.38f), new(0.22f, 0.5f),
    };

    private CreatureSkin _skin;
    private Color _tint = Colors.White;
    private bool _isPicked;

    /// <summary>The surface shown.</summary>
    public CreatureSkin Skin
    {
        get => _skin;
        set { _skin = value; QueueRedraw(); }
    }

    /// <summary>The body color the surface is drawn in.</summary>
    public Color Tint
    {
        get => _tint;
        set { _tint = value; QueueRedraw(); }
    }

    private CreaturePattern? _pattern;
    private Color _paint = Colors.Black;

    /// <summary>Set: the icon shows this paint pattern in Paint instead of a skin surface (the pattern icon of the toolbar).</summary>
    public CreaturePattern? Pattern
    {
        get => _pattern;
        set { _pattern = value; QueueRedraw(); }
    }

    /// <summary>The second color the pattern is drawn in.</summary>
    public Color Paint
    {
        get => _paint;
        set { _paint = value; QueueRedraw(); }
    }

    /// <summary>A picked icon wears a bright ring.</summary>
    public bool IsPicked
    {
        get => _isPicked;
        set { _isPicked = value; QueueRedraw(); }
    }

    public override void _Ready()
    {
        Flat      = true;
        FocusMode = FocusModeEnum.None;
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }
        MouseEntered += () => EditorStyle.ScaleTo(this, 1.12f);
        MouseExited  += () => EditorStyle.ScaleTo(this, 1f);
    }

    public override void _Draw()
    {
        Vector2 center = Size * 0.5f;
        float   radius = Mathf.Min(Size.X, Size.Y) * 0.5f - RIM_WIDTH;
        Color   body   = _tint with { A = 1f };
        Color   dark   = body.Darkened(0.4f);
        Color   light  = body.Lightened(0.3f);
        DrawCircle(center, radius, body);

        if (null != _pattern)
        {
            DrawPattern(center, radius);
            DrawArc(center, radius, 0f, Mathf.Tau, 40, _isPicked ? Colors.White : new Color(EditorStyle.RIM_COLOR, 0.7f), _isPicked ? RIM_WIDTH : 1.5f, true);
            return;
        }

        switch (_skin)
        {
            // Scales: rows of small arcs, every second row shifted by half a scale
            case CreatureSkin.Scales:
                float step = radius * 2f / SCALE_ROWS;
                for (int row = 0; row <= SCALE_ROWS; row++)
                {
                    float y = -radius + row * step;
                    for (float x = -radius - (0 == row % 2 ? 0f : step * 0.5f); x <= radius; x += step)
                    {
                        var at = new Vector2(x + step * 0.5f, y);
                        if (at.Length() < radius - step * 0.35f)
                        {
                            DrawArc(center + at, step * 0.5f, 0.15f, Mathf.Pi - 0.15f, 8, dark, 1.6f, true);
                        }
                    }
                }
                break;

            // Feathers: a few long drops lying side by side
            case CreatureSkin.Feathers:
                foreach (Vector2 feather in WARTS)
                {
                    DrawLine(center + feather * radius * 0.8f + new Vector2(-0.22f, 0f) * radius, center + feather * radius * 0.8f + new Vector2(0.22f, 0f) * radius, dark, radius * 0.16f, true);
                }
                break;

            // Fur: short strokes all over
            case CreatureSkin.Fur:
                for (int i = 0; i < 14; i++)
                {
                    float   angle = i * 2.4f;
                    Vector2 at    = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (0.2f + 0.05f * (i % 9));
                    DrawLine(center + at, center + at + new Vector2(radius * 0.2f, radius * 0.08f), dark, 1.4f, true);
                }
                break;

            // Wrinkles: a few wavy folds from top to bottom
            case CreatureSkin.Wrinkles:
                foreach (float x in new[] { -0.5f, -0.1f, 0.3f, 0.62f })
                {
                    float reach = Mathf.Sqrt(1f - x * x) * radius * 0.85f;
                    var   fold  = new Vector2[9];
                    for (int i = 0; i < fold.Length; i++)
                    {
                        float along = Mathf.Lerp(-reach, reach, i / 8f);
                        fold[i] = center + new Vector2(x * radius + Mathf.Sin(i * 1.3f + x * 5f) * radius * 0.07f, along);
                    }
                    DrawPolyline(fold, dark, 1.5f, true);
                }
                break;

            // Plates: broad bands across, each with a dark groove below
            case CreatureSkin.Plates:
                foreach (float y in new[] { -0.45f, 0f, 0.45f })
                {
                    float reach = Mathf.Sqrt(1f - y * y) * radius * 0.95f;
                    DrawLine(center + new Vector2(-reach, y * radius), center + new Vector2(reach, y * radius), dark, radius * 0.12f, true);
                }
                break;

            // Warts: a few light bumps, each with a dark ring
            case CreatureSkin.Warts:
                foreach (Vector2 wart in WARTS)
                {
                    DrawCircle(center + wart * radius, radius * 0.17f, dark);
                    DrawCircle(center + wart * radius, radius * 0.12f, light);
                }
                break;

            // Smooth: only a soft highlight
            default:
                DrawCircle(center + new Vector2(-0.3f, -0.32f) * radius, radius * 0.22f, light with { A = 0.6f });
                break;
        }
        DrawArc(center, radius, 0f, Mathf.Tau, 40, _isPicked ? Colors.White : new Color(EditorStyle.RIM_COLOR, 0.7f), _isPicked ? RIM_WIDTH : 1.5f, true);
    }

    /// <summary>The paint pattern on the disc: stripes, spots, patches, a saddle, or nothing for a plain body.</summary>
    private void DrawPattern(Vector2 center, float radius)
    {
        Color paint = _paint with { A = 1f };
        switch (_pattern)
        {
            case CreaturePattern.Stripes:
                foreach (float x in new[] { -0.5f, 0f, 0.5f })
                {
                    float reach = Mathf.Sqrt(1f - x * x) * radius * 0.92f;
                    DrawLine(center + new Vector2(x * radius, -reach), center + new Vector2(x * radius, reach), paint, radius * 0.2f, true);
                }
                break;
            case CreaturePattern.Spots:
                foreach (Vector2 spot in WARTS)
                {
                    DrawCircle(center + spot * radius, radius * 0.14f, paint);
                }
                break;
            case CreaturePattern.Patches:
                DrawCircle(center + new Vector2(-0.3f, -0.25f) * radius, radius * 0.36f, paint);
                DrawCircle(center + new Vector2(0.35f, 0.3f) * radius, radius * 0.3f, paint);
                break;
            // Net: the disc in the paint color, a few light lines across
            case CreaturePattern.Net:
                DrawCircle(center, radius * 0.97f, paint);
                Color line = _tint with { A = 1f };
                DrawLine(center + new Vector2(-0.9f, -0.2f) * radius, center + new Vector2(0.85f, 0.35f) * radius, line, radius * 0.1f, true);
                DrawLine(center + new Vector2(-0.1f, -0.95f) * radius, center + new Vector2(0.2f, 0.95f) * radius, line, radius * 0.1f, true);
                DrawLine(center + new Vector2(-0.8f, 0.5f) * radius, center + new Vector2(0.1f, 0.1f) * radius, line, radius * 0.1f, true);
                break;
            case CreaturePattern.Saddle:
                DrawArc(center, radius * 0.5f, Mathf.Pi, Mathf.Tau, 24, paint, radius, true);
                break;
        }
    }
}