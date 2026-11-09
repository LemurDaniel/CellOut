// NOTE:
//   Look of the cell editor: colors and the style boxes of tiles, pills and buttons,
//   plus small tween helpers for the bouncy feel. Pure helpers, CellEditor applies them.
//
//   EditorStyle.ApplyTile(button, EditorStyle.DIET_COLORS[DietClass.Carnivore]);
//   EditorStyle.Punch(badge, 1.15f);

//
//   Chapters:
//     COLORS       Rim, diet and text colors.
//     STYLE BOXES  Tiles per diet, pills and the done button.
//     MOTION       Scale tweens: punch, hover and pop-in.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public static class EditorStyle
{
    // ===========================================================================
    // ===============================   COLORS   ================================
    // ===========================================================================
    //
    //   RIM_COLOR     Neutral pale blue of the panel edge and highlights.
    //   OUTLINE       Dark outline of every label.
    //   DIET_COLORS   One accent per diet class, the same as the diet bar.
    //   DietTint      Tint color and strength for a diet value, the hint the panel takes on.
    //   Accent        RIM_COLOR leaning towards the diet tint.
    //

    public static readonly Color RIM_COLOR = new(0.72f, 0.82f, 0.96f);
    public static readonly Color OUTLINE = new(0.02f, 0.08f, 0.1f);
    public static readonly Dictionary<DietClass, Color> DIET_COLORS = new()
    {
        [DietClass.Herbivore] = new Color(0.35f, 0.85f, 0.4f),
        [DietClass.Omnivore]  = new Color(0.95f, 0.85f, 0.35f),
        [DietClass.Carnivore] = new Color(0.95f, 0.35f, 0.3f),
    };

    private const float TINT_STRENGTH = 0.5f;        // a full herbivore or carnivore tints half way, a hint only

    /// <summary>
    /// The diet hint: herbivore green below 0, carnivore red above 0, neutral at 0.
    /// The strength grows with the distance from the middle.
    /// </summary>
    /// <param name="diet">(Required) Diet value, -1 herbivore .. 1 carnivore.</param>
    /// <returns>Tint color and strength 0..TINT_STRENGTH.</returns>
    /// <example><code>EditorStyle.DietTint(0.6f)  // (carnivore red, 0.3)</code></example>
    public static (Color Color, float Amount) DietTint(float diet)
    {
        Color color = diet < 0f ? DIET_COLORS[DietClass.Herbivore] : DIET_COLORS[DietClass.Carnivore];
        return (color, Mathf.Clamp(Mathf.Abs(diet), 0f, 1f) * TINT_STRENGTH);
    }

    /// <summary>RIM_COLOR leaning towards the diet tint, for glowing highlights.</summary>
    public static Color Accent(float diet)
    {
        (Color color, float amount) = DietTint(diet);
        return RIM_COLOR.Lerp(color, Mathf.Clamp(amount * 1.6f, 0f, 1f));
    }

    // ===========================================================================
    // ============================   STYLE BOXES   ==============================
    // ===========================================================================
    //
    //   ApplyTile     Rounded tile tinted by its diet: calm, hover, selected glow, locked.
    //   Pill          Rounded box with border and optional glow.
    //   ApplyButton   Pill styles for a button in an accent color.
    //

    private const int TILE_RADIUS = 16;
    private const int PILL_RADIUS = 22;

    /// <summary>Styles a palette tile in its diet color, selected tiles glow.</summary>
    /// <param name="button">(Required) The tile.</param>
    /// <param name="accent">(Required) The diet color.</param>
    public static void ApplyTile(Button button, Color accent)
    {
        button.AddThemeStyleboxOverride("normal",        Tile(accent, 0.86f, 0.3f, 1, 0f));
        button.AddThemeStyleboxOverride("hover",         Tile(accent, 0.74f, 0.75f, 2, 0.25f));
        button.AddThemeStyleboxOverride("pressed",       Tile(accent, 0.68f, 1f, 3, 0.6f));
        button.AddThemeStyleboxOverride("hover_pressed", Tile(accent, 0.62f, 1f, 3, 0.75f));
        button.AddThemeStyleboxOverride("focus",         new StyleBoxEmpty());
        var disabled = Tile(accent, 0.93f, 0.1f, 1, 0f);
        disabled.BgColor = new Color(0.03f, 0.07f, 0.09f, 0.7f);
        button.AddThemeStyleboxOverride("disabled", disabled);
    }

    private static StyleBoxFlat Tile(Color accent, float darken, float borderAlpha, int border, float glow)
    {
        var style = new StyleBoxFlat
        {
            BgColor     = accent.Darkened(darken),
            BorderColor = new Color(accent, borderAlpha),
            ShadowColor = new Color(accent, glow * 0.6f),
            ShadowSize  = glow > 0f ? 10 : 0,
        };
        style.SetBorderWidthAll(border);
        style.SetCornerRadiusAll(TILE_RADIUS);
        style.AntiAliasing = true;
        return style;
    }

    /// <summary>A rounded pill, e.g. the points badge.</summary>
    /// <param name="background">(Required) Fill color.</param>
    /// <param name="border">(Required) Border color, also the glow color.</param>
    /// <param name="glow">(Optional) Glow strength 0..1. Defaults to 0.</param>
    public static StyleBoxFlat Pill(Color background, Color border, float glow = 0f)
    {
        var style = new StyleBoxFlat
        {
            BgColor     = background,
            BorderColor = border,
            ShadowColor = new Color(border, glow * 0.5f),
            ShadowSize  = glow > 0f ? 12 : 0,
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(PILL_RADIUS);
        style.SetContentMarginAll(8);
        style.ContentMarginLeft  = 18;
        style.ContentMarginRight = 18;
        return style;
    }

    /// <summary>Pill styles for every state of a button.</summary>
    /// <param name="button">(Required) The button.</param>
    /// <param name="accent">(Required) Accent color of border, glow and fill.</param>
    public static void ApplyButton(Button button, Color accent)
    {
        button.AddThemeStyleboxOverride("normal",  Pill(accent.Darkened(0.55f), accent, 0.35f));
        button.AddThemeStyleboxOverride("hover",   Pill(accent.Darkened(0.4f), accent.Lightened(0.3f), 0.8f));
        button.AddThemeStyleboxOverride("pressed", Pill(accent.Darkened(0.25f), accent.Lightened(0.5f), 1f));
        button.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_outline_color", OUTLINE);
        button.AddThemeConstantOverride("outline_size", 5);
    }

    // ===========================================================================
    // ===============================   MOTION   ================================
    // ===========================================================================
    //
    //   Punch    Pops a control to a scale and springs back.
    //   ScaleTo  Eases a control to a scale, e.g. on hover.
    //   PopIn    Grows a control from nothing after a delay, with overshoot.
    //
    //   Every tween keeps running while the game is paused, the editor is open then.
    //

    private const float PUNCH_SECONDS = 0.28f;
    private const float HOVER_SECONDS = 0.12f;
    private const float POP_SECONDS = 0.35f;

    /// <summary>Pops the control to a scale and springs back to 1.</summary>
    public static void Punch(Control control, float scale)
    {
        CenterPivot(control);
        Tween tween = NewTween(control);
        tween.TweenProperty(control, "scale", Vector2.One * scale, PUNCH_SECONDS * 0.35f);
        tween.TweenProperty(control, "scale", Vector2.One, PUNCH_SECONDS * 0.65f)
            .SetTrans(Tween.TransitionType.Elastic);
    }

    /// <summary>Eases the control to a scale.</summary>
    public static void ScaleTo(Control control, float scale)
    {
        CenterPivot(control);
        NewTween(control).TweenProperty(control, "scale", Vector2.One * scale, HOVER_SECONDS);
    }

    /// <summary>Grows the control from scale 0 to 1 after a delay, with a little overshoot.</summary>
    public static void PopIn(Control control, float delay)
    {
        CenterPivot(control);
        control.Scale = Vector2.Zero;
        NewTween(control).TweenProperty(control, "scale", Vector2.One, POP_SECONDS)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Back);
    }

    private const string TWEEN_META = "editor_scale_tween";

    /// <summary>A new scale tween, the previous one of the control is killed so they never fight.</summary>
    private static Tween NewTween(Control control)
    {
        if (control.HasMeta(TWEEN_META) && control.GetMeta(TWEEN_META).AsGodotObject() is Tween previous)
        {
            previous.Kill();
        }
        Tween tween = control.CreateTween()
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        control.SetMeta(TWEEN_META, tween);
        return tween;
    }

    /// <summary>Scales around the center, the size may only be known from the minimum size yet.</summary>
    private static void CenterPivot(Control control)
    {
        Vector2 size = control.Size == Vector2.Zero ? control.CustomMinimumSize : control.Size;
        control.PivotOffset = size * 0.5f;
    }
}
