// NOTE:
//   One planet as a turning globe (shaders/planet_globe.gdshader) on a square rect: the
//   markers in the galaxy and the big globe of the planet view. Colors, land, clouds and
//   ice come from the planet (Core/Planets.cs). Ignores the mouse, its button takes it.
//
//   var globe = new PlanetGlobe { CustomMinimumSize = Vector2.One * 64 };
//   globe.Show(Planets.Get("RUBRA"));
//   globe.Glow = 1f;                    // halo up while hovered

//
//   Chapters:
//     GLOBE   Shader material, planet and halo.
//

using Godot;

namespace CellOut;

public partial class PlanetGlobe : ColorRect
{
    // ===========================================================================
    // ================================   GLOBE   ================================
    // ===========================================================================
    //
    //   Show   The colors and shares of a planet, a seed from its key.
    //   Glow   0 .. 1, the halo around the globe.
    //   Spin   Radians per second the ground turns.
    //   Radius / Ring   A ringed giant: smaller globe in a larger rect, the ring around it.
    //   Light   Where the light of its star comes from.
    //

    private static readonly Shader GLOBE_SHADER = GD.Load<Shader>("res://shaders/planet_globe.gdshader");

    private readonly ShaderMaterial _material = new() { Shader = GLOBE_SHADER };

    public PlanetGlobe()
    {
        Material    = _material;
        MouseFilter = MouseFilterEnum.Ignore;
        Color       = Colors.White;
    }

    public float Glow
    {
        set => _material.SetShaderParameter("glow", value);
    }

    public float Spin
    {
        set => _material.SetShaderParameter("spin", value);
    }

    /// <summary>Share of the rect the globe fills (0.8, smaller with a ring: RING_RADIUS).</summary>
    public float Radius
    {
        set => _material.SetShaderParameter("radius", value);
    }

    /// <summary>0 no ring .. 1 a full ring; the rect must be RING_GROWTH times larger and Radius RING_RADIUS.</summary>
    public float Ring
    {
        set => _material.SetShaderParameter("ring", value);
    }

    /// <summary>Where the light comes from: x right, y up, z towards the camera; behind the globe it shows a crescent.</summary>
    public Vector3 Light
    {
        set => _material.SetShaderParameter("sun_dir", value);
    }

    public static readonly Vector3 DEFAULT_LIGHT = new Vector3(-0.55f, 0.45f, 0.7f).Normalized();   // shaders/planet_globe.gdshader sun_dir: upper left, from the front

    public const float RING_RADIUS = 0.44f;          // shaders/planet_globe.gdshader: the outer ring edge (2.15 radii) still fits the rect
    public const float RING_GROWTH = 0.8f / RING_RADIUS;   // rect size of a ringed globe against a plain one of the same globe size

    /// <summary>Shows a planet: its colors, land, clouds and ice, a seed of its own.</summary>
    /// <param name="planet">(Required) The planet.</param>
    public void Show(Planet planet)
    {
        _material.SetShaderParameter("ocean", planet.OceanColor);
        _material.SetShaderParameter("land", planet.LandColor);
        _material.SetShaderParameter("highland", planet.HighlandColor);
        _material.SetShaderParameter("sand", planet.SandColor);
        _material.SetShaderParameter("atmosphere", planet.AtmosphereColor);
        _material.SetShaderParameter("land_share", planet.LandShare);
        _material.SetShaderParameter("clouds", planet.Clouds);
        _material.SetShaderParameter("ice", planet.Ice);
        _material.SetShaderParameter("bands", planet.Bands);
        _material.SetShaderParameter("seed", (float)(System.Linq.Enumerable.Sum(planet.Key, letter => letter * 7) % 97));   // stable per key, string hashes change per run
    }
}
