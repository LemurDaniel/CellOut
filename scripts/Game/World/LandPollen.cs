// NOTE:
//   Pollen and seeds that float through the air over the land, from the land animal on:
//   soft pale dots drifting with the wind around the camera focus, fading in and out,
//   and a few colored butterflies fluttering low over the meadow.
//   The counterpart of the motes in the water (Backdrop). Sizes follow the player, like
//   everything in the world. Plain particles and geometry, no screen-space effect: the
//   same on every graphics tier.
//   Created by the FollowCamera, which moves it along every frame.
//
//   var pollen = new LandPollen();
//   AddChild(pollen);
//   pollen.Follow(focus, playerSize, isShown: true);

//
//   Chapters:
//     DRIFT   The particles and how they follow the camera focus.
//

using Godot;

namespace CellOut;

public partial class LandPollen : Node3D
{
    // ===========================================================================
    // ================================   DRIFT   ================================
    // ===========================================================================
    //
    //   _Ready             The pollen emitter: soft billboard dots, a slow wind, fading in and out.
    //   BuildButterflies   The butterfly emitter: two-winged billboards in four colors with a wing beat.
    //   Follow             Moves pollen and butterflies to the focus and sizes them to the player.
    //

    private const int AMOUNT = 110;
    private const float LIFETIME = 10f;
    private const float REACH = 16f;                 // half the width of the cloud, in player sizes
    private const float HEIGHT = 3.5f;               // half its height, in player sizes
    private const float LIFT = 3f;                   // its middle above the focus, in player sizes
    private const float DOT_SIZE = 0.11f;           // a dot, in player sizes
    private const float WIND = 0.45f;                // drift per second, in player sizes
    private static readonly Color POLLEN_COLOR = new(1f, 0.97f, 0.78f);

    private CpuParticles3D _particles;
    private QuadMesh _dot;
    private float _size;

    public override void _Ready()
    {
        // A soft round dot: a radial gradient, drawn unshaded and facing the camera
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1f, 0.5f),
            Width    = 32,
            Height   = 32,
        };
        var material = new StandardMaterial3D
        {
            ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode          = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale     = true,
            VertexColorUseAsAlbedo = true,
            AlbedoColor            = POLLEN_COLOR,
            AlbedoTexture          = texture,
        };
        _dot = new QuadMesh { Material = material };

        // Fades in, floats, fades out
        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
        fade.SetColor(1, new Color(1f, 1f, 1f, 0f));
        fade.AddPoint(0.2f, new Color(1f, 1f, 1f, 0.8f));
        fade.AddPoint(0.8f, new Color(1f, 1f, 1f, 0.8f));

        _particles = new CpuParticles3D
        {
            Amount         = AMOUNT,
            Lifetime       = LIFETIME,
            Preprocess     = LIFETIME,
            Randomness     = 1f,
            LocalCoords    = false,
            Mesh           = _dot,
            EmissionShape  = CpuParticles3D.EmissionShapeEnum.Box,
            Direction      = new Vector3(1f, 0.15f, 0.35f).Normalized(),
            Spread         = 35f,
            Gravity        = Vector3.Zero,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.4f,
            ColorRamp      = fade,
            CastShadow     = GeometryInstance3D.ShadowCastingSetting.Off,
            Emitting       = false,
        };
        AddChild(_particles);
        BuildButterflies(fade);
    }

    private const int BUTTERFLIES = 14;
    private const float BUTTERFLY_LIFETIME = 14f;
    private const float BUTTERFLY_SIZE = 0.3f;       // wing span, in player sizes
    private const float BUTTERFLY_REACH = 11f;       // half the width of their area, in player sizes
    private const float BUTTERFLY_LIFT = 1.1f;       // they flutter this far above the focus, in player sizes
    private const float BUTTERFLY_SPEED = 0.8f;      // in player sizes per second
    private const int FLAPS = 50;                    // wing beats in a lifetime

    private CpuParticles3D _butterflies;
    private QuadMesh _wings;

    /// <summary>
    /// A few butterflies low over the meadow: two round wings as one billboard, each in
    /// one of four colors, flying level in any direction. The wing beat is the size
    /// pulsing over the lifetime.
    /// </summary>
    private void BuildButterflies(Gradient fade)
    {
        // Two wings: a round spot left and right of the middle
        const int PIXELS = 32;
        Image image = Image.CreateEmpty(PIXELS, PIXELS, false, Image.Format.Rgba8);
        for (int y = 0; y < PIXELS; y++)
        {
            for (int x = 0; x < PIXELS; x++)
            {
                var   point = new Vector2(x + 0.5f, y + 0.5f);
                float wing  = Mathf.Min(point.DistanceTo(new Vector2(9f, 15f)), point.DistanceTo(new Vector2(23f, 15f)));
                image.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(6.5f, 8.5f, wing)));
            }
        }
        var material = new StandardMaterial3D
        {
            ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode          = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale     = true,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture          = ImageTexture.CreateFromImage(image),
        };
        _wings = new QuadMesh { Material = material };

        var colors = new Gradient { InterpolationMode = Gradient.InterpolationModeEnum.Constant };
        colors.SetColor(0, new Color(1f, 0.86f, 0.3f));
        colors.SetColor(1, new Color(0.55f, 0.75f, 1f));
        colors.AddPoint(0.3f, Colors.White);
        colors.AddPoint(0.6f, new Color(1f, 0.55f, 0.7f));

        // Wing beat: the size swings between open and half closed
        var beat = new Curve();
        for (int i = 0; i <= FLAPS * 2; i++)
        {
            beat.AddPoint(new Vector2((float)i / (FLAPS * 2), 0 == i % 2 ? 1f : 0.5f));
        }

        _butterflies = new CpuParticles3D
        {
            Amount            = BUTTERFLIES,
            Lifetime          = BUTTERFLY_LIFETIME,
            Preprocess        = BUTTERFLY_LIFETIME,
            Randomness        = 1f,
            LocalCoords       = false,
            Mesh              = _wings,
            EmissionShape     = CpuParticles3D.EmissionShapeEnum.Box,
            Direction         = Vector3.Right,
            Spread            = 180f,
            Flatness          = 1f,
            Gravity           = Vector3.Zero,
            ColorRamp         = fade,
            ColorInitialRamp  = colors,
            ScaleAmountCurve  = beat,
            CastShadow        = GeometryInstance3D.ShadowCastingSetting.Off,
            Emitting          = false,
        };
        AddChild(_butterflies);
    }

    /// <summary>Keeps the pollen around the camera focus, sized to the player.</summary>
    /// <param name="focus">(Required) 3D point the camera looks at.</param>
    /// <param name="playerSize">(Required) Body size of the player, every measure of the cloud follows it.</param>
    /// <param name="isShown">(Required) Whether pollen is in the air (on land under the open sky).</param>
    /// <example><code>pollen.Follow(_camFocus, playerSize, _view > 0.5f);</code></example>
    public void Follow(Vector3 focus, float playerSize, bool isShown)
    {
        _particles.Emitting   = isShown;
        _butterflies.Emitting = isShown;
        Visible               = isShown;
        if (!isShown)
        {
            return;
        }
        _particles.Position   = focus + Vector3.Up * playerSize * LIFT;
        _butterflies.Position = focus + Vector3.Up * playerSize * BUTTERFLY_LIFT;
        if (Mathf.IsEqualApprox(_size, playerSize))
        {
            return;
        }
        _size = playerSize;
        _dot.Size                      = Vector2.One * playerSize * DOT_SIZE;
        _particles.EmissionBoxExtents  = new Vector3(REACH, HEIGHT, REACH) * playerSize;
        _particles.InitialVelocityMin  = playerSize * WIND * 0.4f;
        _particles.InitialVelocityMax  = playerSize * WIND;
        _wings.Size                       = new Vector2(1f, 1f) * playerSize * BUTTERFLY_SIZE;
        _butterflies.EmissionBoxExtents   = new Vector3(BUTTERFLY_REACH, 0.5f, BUTTERFLY_REACH) * playerSize;
        _butterflies.InitialVelocityMin   = playerSize * BUTTERFLY_SPEED * 0.5f;
        _butterflies.InitialVelocityMax   = playerSize * BUTTERFLY_SPEED;
        Log.Verbose("LandPollen", $"Pollen sized to a player of {playerSize:F0}.");
    }
}
