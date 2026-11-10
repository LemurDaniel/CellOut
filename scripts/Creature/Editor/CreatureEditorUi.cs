// NOTE:
//   Small pure helpers every part of the creature editor shares: the accent color of
//   a body type, the translation keys of types and tabs, and the label and pill button
//   all views are built from. The style boxes come from Game/Ui/EditorStyle.cs.
//
//   Label title = CreatureEditorUi.NewLabel(24);
//   title.AddThemeColorOverride("font_color", CreatureEditorUi.TypeAccent(draft.Type));

//
//   Chapters:
//     TYPE LOOK   Accent color and translation keys of body types and tabs.
//     WIDGETS     Label, pill button and the fade of a canvas item.
//

using Godot;

namespace CellOut;

public static class CreatureEditorUi
{
    // ===========================================================================
    // ==============================   TYPE LOOK   ==============================
    // ===========================================================================
    //
    //   TypeAccent    Accent of a body type.
    //   TypeKey       Translation key of a body type, name or description.
    //   CategoryKey   Translation key of a palette tab.
    //

    /// <summary>Accent of a body type: ocean blue for fish, lagoon teal for marine mammals.</summary>
    public static Color TypeAccent(CreatureType type)
    {
        return CreatureType.Fish == type ? new Color(0.45f, 0.72f, 1f) : new Color(0.45f, 0.92f, 0.82f);
    }

    /// <summary>Translation key of a body type.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <param name="suffix">(Required) "NAME" or "DESC".</param>
    /// <example><code>CreatureEditorUi.TypeKey(CreatureType.Fish, "NAME")  // "CREATURE_TYPE_FISH_NAME"</code></example>
    public static string TypeKey(CreatureType type, string suffix)
    {
        return $"CREATURE_TYPE_{type}_{suffix}".ToUpperInvariant();
    }

    /// <summary>Translation key of a palette tab.</summary>
    /// <example><code>CreatureEditorUi.CategoryKey(CreatureCategory.Fins)  // "CREATURE_CATEGORY_FINS"</code></example>
    public static string CategoryKey(CreatureCategory category)
    {
        return $"CREATURE_CATEGORY_{category}".ToUpperInvariant();
    }

    // ===========================================================================
    // ===============================   WIDGETS   ===============================
    // ===========================================================================
    //
    //   NewLabel        Label with outline, translated by its view.
    //   NewPillButton   Small pill button without focus.
    //   SetAlpha        Fades a canvas item.
    //

    /// <summary>A label with a dark outline that ignores the mouse, its view translates the text.</summary>
    /// <param name="fontSize">(Required) Font size in pixels, the outline grows with it.</param>
    public static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }

    /// <summary>A button without focus, styled by its view (EditorStyle.ApplyButton, ApplyTab).</summary>
    /// <param name="minimumSize">(Required) Minimum size, 0 lets the text decide.</param>
    /// <param name="fontSize">(Required) Font size in pixels.</param>
    public static Button NewPillButton(Vector2 minimumSize, int fontSize)
    {
        var button = new Button
        {
            CustomMinimumSize = minimumSize,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode         = Control.FocusModeEnum.None,
            AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        return button;
    }

    /// <summary>Sets the alpha of a canvas item, its color stays.</summary>
    public static void SetAlpha(CanvasItem item, float alpha)
    {
        Color modulate = item.Modulate;
        modulate.A    = alpha;
        item.Modulate = modulate;
    }
}
