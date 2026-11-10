// NOTE:
//   The graphics tiers of the game: Low for laptop graphics, Medium in between, High for
//   a dedicated card. The one place that knows what a tier switches on.
//   The renderer itself (Forward+) is fixed at start; a device without Vulkan falls back
//   to OpenGL, where the screen-space effects do not exist, so it always runs Low.
//   Whoever owns a viewport, an environment or a camera asks here and listens to Changed.
//
//   GraphicsQuality.Use(GraphicsQuality.Detect());        // first start
//   GraphicsQuality.ApplyTo(GetViewport());
//   GraphicsQuality.ApplyTo(environment);
//   GraphicsQuality.Changed += () => GraphicsQuality.ApplyTo(environment);
//   $env:CELLOUT_GRAPHICS = 'High'                        # forces a tier, for renders and tests

//
//   Chapters:
//     TIER    The current tier, detection and switching.
//     APPLY   What a tier means for viewport, shadows, environment and depth of field.
//

using System;
using Godot;

namespace CellOut;

public enum GraphicsTier { Low, Medium, High }

public static class GraphicsQuality
{
    // ===========================================================================
    // ================================   TIER   =================================
    // ===========================================================================
    //
    //   Tier         The tier in use.
    //   Changed      Raised after the tier changed, owners apply it again.
    //   IsFallback   The device runs the OpenGL fallback: only Low is possible.
    //   Forced       The tier of CELLOUT_GRAPHICS, null when unset.
    //   Detect       A fitting tier for the graphics adapter.
    //   Use          Switches the tier.
    //   Next         The tier after the current one, for a button that cycles.
    //

    private const string FALLBACK_METHOD = "gl_compatibility";
    private const string FORCE_VARIABLE = "CELLOUT_GRAPHICS";

    public static GraphicsTier Tier { get; private set; } = GraphicsTier.Medium;

    public static event Action Changed;

    public static bool IsFallback => FALLBACK_METHOD == RenderingServer.GetCurrentRenderingMethod();

    /// <summary>The tier forced by the environment variable CELLOUT_GRAPHICS (Low, Medium, High), null when unset or unknown.</summary>
    public static GraphicsTier? Forced => Enum.TryParse(OS.GetEnvironment(FORCE_VARIABLE), ignoreCase: true, out GraphicsTier tier) ? tier : null;

    /*
      > OpenGL fallback: Low, nothing else works there
      > Dedicated card: High
      > Integrated chip or software rendering: Low (an Iris Xe holds 50+ fps at 1080p on Low,
        20 to 45 on Medium), the player can step up in the title menu
      > Virtual or unknown adapter: Medium
    */
    /// <summary>A fitting tier for the graphics adapter of this device.</summary>
    /// <example><code>GraphicsQuality.Use(GraphicsQuality.Detect());</code></example>
    public static GraphicsTier Detect()
    {
        if (IsFallback)
        {
            return GraphicsTier.Low;
        }
        return RenderingServer.GetVideoAdapterType() switch
        {
            RenderingDevice.DeviceType.DiscreteGpu   => GraphicsTier.High,
            RenderingDevice.DeviceType.IntegratedGpu => GraphicsTier.Low,
            RenderingDevice.DeviceType.Cpu           => GraphicsTier.Low,
            _                                        => GraphicsTier.Medium,
        };
    }

    /// <summary>Switches the tier and tells every owner. The OpenGL fallback stays on Low.</summary>
    /// <param name="tier">(Required) The wanted tier.</param>
    public static void Use(GraphicsTier tier)
    {
        Tier = IsFallback ? GraphicsTier.Low : tier;
        RenderingServer.DirectionalSoftShadowFilterSetQuality(ShadowFilter());
        Log.Verbose("GraphicsQuality", $"Tier {Tier} on {RenderingServer.GetVideoAdapterName()} ({RenderingServer.GetCurrentRenderingMethod()}).");
        Changed?.Invoke();
    }

    /// <summary>The tier a cycling button switches to: Low, Medium, High and around. The OpenGL fallback has only Low.</summary>
    public static GraphicsTier Next()
    {
        return IsFallback ? GraphicsTier.Low : (GraphicsTier)(((int)Tier + 1) % Enum.GetValues<GraphicsTier>().Length);
    }

    // ===========================================================================
    // ================================   APPLY   ================================
    // ===========================================================================
    //
    //   ApplyTo(Viewport)      Render scale and anti-aliasing of the 3D view.
    //   ApplyTo(Environment)   Glow, contact shadows, indirect light.
    //   ApplyTo(Attributes)    Depth of field strength, the owner sets the distances.
    //   HasDepthOfField        Whether the tier blurs what is out of focus.
    //
    //                 Low            Medium          High
    //   Render scale  75 %           100 %           100 %
    //   Edges         none           MSAA 2x         MSAA 4x
    //   Shadows       hard           soft            softer
    //   Glow          yes            yes             yes
    //   SSAO          no             yes             yes
    //   SSIL          no             no              yes
    //   Depth blur    no             far             far and near
    //

    // Low smooths its edges only by scaling up: FXAA together with a render scale below 1 breaks the
    // frame in Godot 4.7.2 ("resolve attachments must match the number of color attachments")
    private const float LOW_RENDER_SCALE = 0.75f;
    private const float GLOW_INTENSITY = 0.55f;
    private const float GLOW_THRESHOLD = 1f;         // only what shines by itself blooms, lit sand and skin keep their contrast
    private const float SSAO_INTENSITY = 1.6f;
    private const float DOF_AMOUNT = 0.06f;

    public static bool HasDepthOfField => GraphicsTier.Low != Tier;

    /// <summary>Render scale and anti-aliasing of a 3D viewport.</summary>
    /// <param name="viewport">(Required) The viewport of the game or of a preview.</param>
    public static void ApplyTo(Viewport viewport)
    {
        viewport.Scaling3DScale = GraphicsTier.Low == Tier ? LOW_RENDER_SCALE : 1f;
        viewport.Msaa3D = Tier switch
        {
            GraphicsTier.High   => Viewport.Msaa.Msaa4X,
            GraphicsTier.Medium => Viewport.Msaa.Msaa2X,
            _                   => Viewport.Msaa.Disabled,
        };
    }

    /// <summary>Glow, contact shadows and indirect light of an environment. Radii stay with the owner, they scale with the scene.</summary>
    /// <param name="environment">(Required) The environment of the game or of a preview.</param>
    public static void ApplyTo(Godot.Environment environment)
    {
        environment.GlowEnabled      = true;
        environment.GlowIntensity    = GLOW_INTENSITY;
        environment.GlowBloom        = 0f;
        environment.GlowHdrThreshold = GLOW_THRESHOLD;
        environment.SsaoEnabled      = GraphicsTier.Low != Tier;
        environment.SsaoIntensity    = SSAO_INTENSITY;
        environment.SsilEnabled      = GraphicsTier.High == Tier;
    }

    /// <summary>Depth of field of a camera: off, far blur, or far and near blur. The owner sets the distances.</summary>
    /// <param name="attributes">(Required) The attributes of the camera.</param>
    public static void ApplyTo(CameraAttributesPractical attributes)
    {
        attributes.DofBlurFarEnabled  = HasDepthOfField;
        attributes.DofBlurNearEnabled = GraphicsTier.High == Tier;
        attributes.DofBlurAmount      = DOF_AMOUNT;
    }

    private static RenderingServer.ShadowQuality ShadowFilter()
    {
        return Tier switch
        {
            GraphicsTier.High   => RenderingServer.ShadowQuality.SoftMedium,
            GraphicsTier.Medium => RenderingServer.ShadowQuality.SoftLow,
            _                   => RenderingServer.ShadowQuality.Hard,
        };
    }
}
