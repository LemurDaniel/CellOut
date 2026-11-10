// NOTE:
//   Live 3D icons of the creature editor: a whole example creature for the type cards,
//   or one part alone for palette tiles and the drag icon. Each icon renders in its own
//   small studio (Cell/Editor/PartIcon.NewStudio). The counterpart of PartIcon for cells.
//
//   Control icon = CreatureIcon.New(CreatureIcon.DesignFor(info, draft.Type), color, 168, isTurning: false, isPartsOnly: true);

//
//   Chapters:
//     BUILD ICON   The icon and the little body that carries a single part.
//

using Godot;

namespace CellOut;

public static class CreatureIcon
{
    // ===========================================================================
    // =============================   BUILD ICON   ==============================
    // ===========================================================================
    //
    //   New         A live 3D creature (cards) or only its part (tiles) in its own small viewport.
    //   DesignFor   The clay blob that carries only one part, placed where it shows best.
    //

    private const float CARD_CAMERA_DISTANCE = 3.6f;
    private const float TILE_CAMERA_DISTANCE = 2.9f;

    /// <summary>A live creature in its own small studio, seen from the front flank.</summary>
    /// <param name="design">(Required) The creature shown.</param>
    /// <param name="color">(Required) Body color.</param>
    /// <param name="pixels">(Required) Edge length of the square icon.</param>
    /// <param name="isTurning">(Required) Turns slowly on the spot (type cards).</param>
    /// <param name="isPartsOnly">(Optional) Only the parts without the body, the camera frames them. Defaults to false.</param>
    /// <returns>The icon, it ignores the mouse.</returns>
    public static Control New(CreatureDesign design, Color color, int pixels, bool isTurning, bool isPartsOnly = false)
    {
        var container = new SubViewportContainer
        {
            Stretch           = true,
            CustomMinimumSize = new Vector2(pixels, pixels),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        SubViewport viewport = PartIcon.NewStudio(new Vector2I(pixels, pixels));
        container.AddChild(viewport);

        var visual = new CreatureVisual3D { Design = design, BodyColor = color, Knead = CreatureVisual3D.EDITOR_KNEAD, IsPartsOnly = isPartsOnly };
        viewport.AddChild(visual);
        if (isTurning)
        {
            visual.Rotation = new Vector3(0f, -0.5f, 0f);
            Tween spin = visual.CreateTween().SetLoops().SetPauseMode(Tween.TweenPauseMode.Process);
            spin.TweenProperty(visual, "rotation:y", 0.5f, 3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            spin.TweenProperty(visual, "rotation:y", -0.5f, 3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        // Cards look at the whole creature, a parts-only tile lets the visual frame its part
        Vector3 offset = new Vector3(0.35f, 0.55f, 1f).Normalized() * (isTurning ? CARD_CAMERA_DISTANCE : TILE_CAMERA_DISTANCE);
        var camera = new Camera3D { Fov = 40f, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), offset);
        viewport.AddChild(camera);
        if (isPartsOnly)
        {
            visual.FitCamera = camera;
        }
        return container;
    }

    /// <summary>The clay blob of a body type that carries only this part: eyes near the nose, arms in front, legs behind.</summary>
    /// <param name="info">(Required) The part shown.</param>
    /// <param name="type">(Required) Body type of the draft, it decides how the part looks.</param>
    public static CreatureDesign DesignFor(CreaturePartInfo info, CreatureType type)
    {
        CreatureDesign design = CreatureDesign.Blob();
        design.Type = type;
        float t = info.Category switch
        {
            CreatureCategory.Eyes  => 0.85f,
            CreatureCategory.Hands => 0.72f,
            CreatureCategory.Feet  => 0.4f,
            CreatureCategory.Limbs => 0.5f,
            _                      => 0.55f,
        };
        design.TryPlace(info.Kind, t, 0);
        return design;
    }
}
