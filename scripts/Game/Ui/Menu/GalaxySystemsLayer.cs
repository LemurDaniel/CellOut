// NOTE:
//   Every star system of the galaxy menu drawn over the Milky Way (Core/Galaxy.cs): a star in
//   the color and size of its kind with its glow, around it the flat orbits and the planets
//   travelling on them as small dots in their colors. Wild systems cannot be chosen; the
//   playable planets are the buttons of Menu/GalaxyView.cs, which asks HomeSpot where its
//   planet stands. The systems fade in once the view is close enough. Ignores the mouse.
//
//   var layer = new GalaxySystemsLayer { ToScreen = ToScreen };
//   AddChild(layer);
//   layer.Scale = scale;                                  // every frame, the disk radius in screen heights
//   Vector2 spot = layer.HomeSpot(system, time);           // the playable planet on its orbit

//
//   Chapters:
//     LAYER    The mapping from the view, the fade and the time.
//     DRAW     Star, glow, orbits and planets of every system on screen.
//

using Godot;

namespace CellOut;

public partial class GalaxySystemsLayer : Control
{
    // ===========================================================================
    // ================================   LAYER   ================================
    // ===========================================================================
    //
    //   ToScreen    (Required) Disk point to pixels, the mapping of GalaxyView.
    //   ViewScale   Disk radius in screen heights, set every frame.
    //   Hovered     The system under the mouse, drawn in full with brackets.
    //   Shown       0 .. 1, how much of the systems shows at the current scale.
    //   _Process    Advances the time of the orbits and redraws.
    //

    public System.Func<Vector2, Vector2> ToScreen { get; set; }

    public float ViewScale { get; set; } = 1f;

    /// <summary>The system under the mouse: all its orbits and planets show, brackets turn around its star. Null for none.</summary>
    public StarSystem Hovered { get; set; }

    private const float SHOW_FROM = 0.6f;            // scale the systems start to fade in
    private const float SHOW_FULL = 1.1f;            // scale they are fully there
    private const float ORBIT_SPEED = 0.25f;         // radians per second on the innermost orbit

    public float Shown => Mathf.SmoothStep(SHOW_FROM, SHOW_FULL, ViewScale);

    private float _time;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    // ===========================================================================
    // =================================   DRAW   ================================
    // ===========================================================================
    //
    //   StarPixels    Radius of a sun at the current scale.
    //   OrbitPixels   Step between two orbits, wider in playable systems so their globe has room.
    //   HomeSpot      Where the playable planet of a system stands right now.
    //   _Draw         Every system on screen.
    //   OnOrbit       A point on a flat orbit around a star.
    //   DrawReticle   The turning brackets around the hovered star.
    //

    private const float TILT = 0.58f;                // the same tilt as the disk
    private const float PLAYABLE_SPREAD = 2.6f;      // orbits of playable systems are this much wider
    private const float PLANETS_FROM = 1.35f;        // scale the planets of the systems start to show
    private const float PLANETS_FULL = 2.1f;
    private const float RETICLE_SPEED = 0.8f;        // radians per second the brackets turn

    private float StarPixels => Mathf.Clamp(3.4f * ViewScale, 2f, 12f);

    private float OrbitPixels(StarSystem system)
    {
        return Mathf.Clamp(3.6f * ViewScale, 2.5f, 15f) * (null == system.PlanetKey ? 1f : PLAYABLE_SPREAD);
    }

    /// <summary>Where the playable planet of a system stands right now, in pixels.</summary>
    /// <param name="system">(Required) A playable system (HomeOrbit at least 0).</param>
    public Vector2 HomeSpot(StarSystem system)
    {
        return OnOrbit(system, system.HomeOrbit, 0.7f);
    }

    private Vector2 OnOrbit(StarSystem system, int orbit, float start)
    {
        float radius = StarPixels * StarSystem.SizeOf(system.Star) * 2.5f + (orbit + 1) * OrbitPixels(system);
        float angle  = start + _time * ORBIT_SPEED / Mathf.Pow(orbit + 1f, 1.5f);
        return ToScreen(system.Spot) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * TILT) * radius;
    }

    public override void _Draw()
    {
        float shown = Shown;
        if (null == ToScreen || shown <= 0.01f)
        {
            return;
        }
        var view = new Rect2(-Vector2.One * 80f, Size + Vector2.One * 160f);
        foreach (StarSystem system in Galaxy.Systems)
        {
            Vector2 at = ToScreen(system.Spot);
            if (!view.HasPoint(at))
            {
                continue;
            }
            Color light = StarSystem.ColorOf(system.Star);
            float star  = StarPixels * StarSystem.SizeOf(system.Star);

            // Orbits and planets, the orbits only once the view is close; the hovered system shows all of it
            bool  isHovered   = system == Hovered;
            float orbitAlpha  = isHovered ? 0.3f : Mathf.SmoothStep(0.9f, 1.8f, ViewScale) * 0.18f * shown;
            float planetAlpha = isHovered ? 1f : Mathf.SmoothStep(PLANETS_FROM, PLANETS_FULL, ViewScale);    // at the start view only the stars, no confetti
            foreach (StarSystem.Neighbor planet in system.Neighbors)
            {
                if (orbitAlpha > 0.01f)
                {
                    float radius = star * 2.5f + (planet.Orbit + 1) * OrbitPixels(system);
                    DrawOrbit(at, radius, new Color(light, orbitAlpha));
                }
                Vector2 spot = OnOrbit(system, planet.Orbit, planet.Start);
                float   size = Mathf.Max(1.4f, planet.Size * 0.9f * Mathf.Clamp(ViewScale, 1f, 4f));
                DrawCircle(spot, size, new Color(StarSystem.DotOf(planet), shown * planetAlpha));
            }
            if (null != system.PlanetKey && orbitAlpha > 0.01f)
            {
                DrawOrbit(at, star * 2.5f + (system.HomeOrbit + 1) * OrbitPixels(system), new Color(light, orbitAlpha * 2f));
            }

            // The star: a wide soft glow, a brighter halo, a white hot core
            DrawCircle(at, star * 4.5f, new Color(light, 0.07f * shown));
            DrawCircle(at, star * 2.2f, new Color(light, 0.2f * shown));
            DrawCircle(at, star, new Color(light.Lerp(Colors.White, 0.55f), shown));
            if (isHovered)
            {
                DrawReticle(at, star * 4f + 8f, light);
            }
        }
    }

    /// <summary>Four corner brackets around a point, turning slowly: the system under the mouse.</summary>
    private void DrawReticle(Vector2 center, float radius, Color color)
    {
        for (int corner = 0; corner < 4; corner++)
        {
            float angle = _time * RETICLE_SPEED + corner * Mathf.Pi * 0.5f;
            DrawArc(center, radius, angle - 0.35f, angle + 0.35f, 8, new Color(color.Lerp(Colors.White, 0.5f), 0.9f), 2f, true);
        }
    }

    /// <summary>A flat ellipse around a star.</summary>
    private void DrawOrbit(Vector2 center, float radius, Color color)
    {
        var points = new Vector2[33];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / 32f * Mathf.Tau;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * TILT) * radius;
        }
        DrawPolyline(points, color, 1f, true);
    }
}
