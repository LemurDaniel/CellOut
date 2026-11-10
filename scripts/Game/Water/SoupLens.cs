// NOTE:
//   The lens over the 3D view: light shafts from the top left and a vignette
//   (shaders/soup_lens.gdshader), drawn above the world and below every 2D layer and
//   menu. Created by FollowCamera, which sets it once per frame.
//
//   _lens = new SoupLens();
//   AddChild(_lens);
//   _lens.Light(palette.Light, underWater: 1f - view, drift: focus.X / viewRadius);

//
//   Chapters:
//     BUILD AND LIGHT   The full-screen rectangle and its values per frame.
//

using Godot;

namespace CellOut;

public partial class SoupLens : CanvasLayer
{
    // ===========================================================================
    // ===========================   BUILD AND LIGHT   ===========================
    // ===========================================================================
    //
    //   _Ready   A full-screen rectangle with the lens shader, below every other canvas layer.
    //   Light    Light color, how much water lies over the view and the drift of the shafts.
    //

    private const int BELOW_EVERYTHING = -10;        // canvas layer: above the 3D world, below the 2D layers and the HUD
    private const float DRIFT_SHARE = 0.15f;         // the shafts slide this much per view radius the camera moves
    private static readonly Shader LENS_SHADER = GD.Load<Shader>("res://shaders/soup_lens.gdshader");

    private ShaderMaterial _material;

    public override void _Ready()
    {
        Layer       = BELOW_EVERYTHING;
        ProcessMode = ProcessModeEnum.Always;
        _material   = new ShaderMaterial { Shader = LENS_SHADER };
        var rect = new ColorRect { Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(rect);
    }

    /// <summary>Sets the lens for this frame.</summary>
    /// <param name="light">(Required) Color of the light shafts (SoupPalette.Light).</param>
    /// <param name="underWater">(Required) 1 in the soup .. 0 in the open air of the third person view, shafts fade with it.</param>
    /// <param name="drift">(Required) View position in view radii along the shaft axis, the shafts slide with it.</param>
    public void Light(Color light, float underWater, float drift)
    {
        _material.SetShaderParameter("light_color", light);
        _material.SetShaderParameter("strength", Mathf.Clamp(underWater, 0f, 1f));
        _material.SetShaderParameter("drift", drift * DRIFT_SHARE);
    }
}
