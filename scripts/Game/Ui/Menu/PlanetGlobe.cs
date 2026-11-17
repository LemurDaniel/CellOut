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
