// NOTE:
//   The star system behind the planet view (Menu/PlanetView.cs): the sun glowing at the left
//   edge, the orbits as flat ellipses around it (the playable planet sits on its own orbit,
//   where the big globe of the planet view is) and the neighbor planets of Core/StarSystem.cs
//   travelling slowly along theirs. The neighbors cannot be chosen, they are dimmed and
//   ignore the mouse. Fills its parent.
//
//   var system = new StarSystemView();
//   AddChild(system);
//   system.Show(planet, globeCenter: new Vector2(0.28f, 0.5f));   // share of the size

//
//   Chapters:
//     SHOW      The system of a planet: sun color, orbits and neighbor globes.
//     ORBITS    Where an orbit runs, the sun glow and the orbit lines.
//     MOTION    The neighbors travel along their orbits.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class StarSystemView : Control
{
    // ===========================================================================
    // =================================   SHOW   ================================
    // ===========================================================================
    //
    //   Show   Builds the system of a planet: the sun in its color, one globe per neighbor.
    //

    private const float NEIGHBOR_PIXELS = 64f;       // globe size of a neighbor of size 1
    private const float NEIGHBOR_DIM = 0.8f;         // brightness of the neighbors, the playable planet stands out

    private readonly List<(StarSystem.Neighbor Neighbor, PlanetGlobe Globe)> _neighbors = new();
    private StarSystem _system;
    private Vector2 _home = new(0.28f, 0.5f);
    private Color _sun = Colors.White;
    private TextureRect _glow;
    private float _time;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _glow = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors  = new[] { new Color(1f, 1f, 0.95f, 1f), new Color(1f, 0.9f, 0.6f, 0.55f), new Color(1f, 0.8f, 0.5f, 0f) },
                    Offsets = new[] { 0f, 0.12f, 1f },
                },
                Fill     = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo   = new Vector2(1f, 0.5f),
                Width    = 256,
                Height   = 256,
            },
            ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Size        = Vector2.One * SUN_GLOW,
        };
        AddChild(_glow);
    }

    /// <summary>Builds the system of a planet.</summary>
    /// <param name="planet">(Required) The playable planet in the middle of the view.</param>
    /// <param name="globeCenter">(Required) Where its big globe sits, as share of this control's size.</param>
    public void Show(Planet planet, Vector2 globeCenter)
    {
        _home   = globeCenter;
        _sun    = StarSystem.ColorOf(planet.StarKind);
        _system = StarSystem.Of(planet);
        foreach ((StarSystem.Neighbor _, PlanetGlobe globe) in _neighbors)
        {
            globe.QueueFree();
        }
        _neighbors.Clear();
        foreach (StarSystem.Neighbor neighbor in _system.Neighbors)
        {
            var globe = new PlanetGlobe { Size = Vector2.One * NEIGHBOR_PIXELS * neighbor.Size, Glow = 0.1f, Spin = 0.08f, Modulate = new Color(NEIGHBOR_DIM, NEIGHBOR_DIM, NEIGHBOR_DIM) };
            globe.Show(neighbor.Look);
            AddChild(globe);
            _neighbors.Add((neighbor, globe));
        }
        _glow.Modulate = _sun;
        QueueRedraw();
    }

    // ===========================================================================
    // ================================   ORBITS   ===============================
    // ===========================================================================
    //
    //   SUN          Where the sun stands, as share of the size (left of the screen, half outside).
    //   Radius       Radius of an orbit: the home orbit runs through the big globe, the others in steps.
    //   OnOrbit      A point on an orbit at an angle, the orbits are flat ellipses.
    //   _Draw        The orbit lines, the home orbit brighter.
    //

    private static readonly Vector2 SUN = new(-0.1f, 0.62f);
    private const float SQUASH = 0.38f;              // the orbits lie flat, seen from above at a low angle
    private const float SUN_GLOW = 900f;

    private Vector2 SunAt => SUN * Size;

    private float Radius(int orbit)
    {
        Vector2 home     = _home * Size - SunAt;
        float   homeSize = new Vector2(home.X, home.Y / SQUASH).Length();
        return homeSize * (orbit + 1.2f) / (_system.HomeOrbit + 1.2f);
    }

    private Vector2 OnOrbit(int orbit, float angle)
    {
        float radius = Radius(orbit);
        return SunAt + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * SQUASH);
    }

    public override void _Draw()
    {
        if (null == _system)
        {
            return;
        }
        for (int orbit = 0; orbit < StarSystem.ORBITS; orbit++)
        {
            bool isHome = orbit == _system.HomeOrbit;
            var  points = new Vector2[97];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = OnOrbit(orbit, i / 96f * Mathf.Tau);
            }
            DrawPolyline(points, new Color(_sun, isHome ? 0.35f : 0.14f), isHome ? 2f : 1.5f, true);
        }
    }

    // ===========================================================================
    // ================================   MOTION   ===============================
    // ===========================================================================
    //
    //   _Process   The sun glow sits on the sun, the neighbors travel, inner ones faster.
    //

    private const float ORBIT_SPEED = 0.05f;         // radians per second on the innermost orbit

    public override void _Process(double delta)
    {
        if (null == _system || !IsVisibleInTree())
        {
            return;
        }
        _time += (float)delta;
        _glow.Position = SunAt - _glow.Size * 0.5f;
        foreach ((StarSystem.Neighbor neighbor, PlanetGlobe globe) in _neighbors)
        {
            float angle = neighbor.Start + _time * ORBIT_SPEED / Mathf.Pow(neighbor.Orbit + 1f, 1.5f);
            globe.Position = OnOrbit(neighbor.Orbit, angle) - globe.Size * 0.5f;
        }
    }

    public override void _Notification(int what)
    {
        if (NotificationResized == what)
        {
            QueueRedraw();
        }
    }
}
