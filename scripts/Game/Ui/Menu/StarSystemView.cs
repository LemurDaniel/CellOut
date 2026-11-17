// NOTE:
//   The star system behind the planet view (Menu/PlanetView.cs). The camera rides with the
//   playable planet along its orbit (the planet stays where the big globe of the planet view
//   is), so the star (shaders/menu_sun.gdshader: disk, corona, lens streak, in the color and
//   size of its kind) starts at the left edge and slowly circles it, the flat orbits and the
//   neighbor planets of Core/StarSystem.cs travel with it. Every globe is lit from the star,
//   from behind as a crescent when it stands nearer to the camera than the star. Giants with their
//   rings, in some systems a belt of asteroids, and now and then a comet whose tail points
//   away from the star. The neighbors cannot be chosen: hovering one only names it
//   (Core/StarNames.cs) and says it waits for the space phase. Fills its parent.
//
//   var system = new StarSystemView();
//   AddChild(system);
//   system.Show(planet, globeCenter: new Vector2(0.28f, 0.5f));   // share of the size

//
//   Chapters:
//     SHOW      The system of a planet: star, neighbor globes, belt and comet.
//     ORBITS    Where an orbit runs and the orbit lines.
//     MOTION    Neighbors travel on screen, the hovered one is named.
//     BELT AND COMET   The asteroids between two orbits and the comet.
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
    //   _Ready   The star rect and the label of a hovered neighbor.
    //   Show     Builds the system of a planet: the star in its color and size, one globe per
    //            neighbor (ringed ones in a larger rect), the belt and the comet.
    //

    private const float NEIGHBOR_PIXELS = 64f;       // globe size of a neighbor of size 1
    private const float NEIGHBOR_DIM = 0.8f;         // brightness of the neighbors, the playable planet stands out
    private const float SUN_RECT = 1100f;            // pixels of the star rect, its glow fills it
    private const float SUN_DISK = 0.3f;             // radius of a sun's disk in half rect sizes
    private static readonly Shader SUN_SHADER = GD.Load<Shader>("res://shaders/menu_sun.gdshader");

    private readonly List<(StarSystem.Neighbor Neighbor, PlanetGlobe Globe, int Index)> _neighbors = new();
    private readonly ShaderMaterial _sunMaterial = new() { Shader = SUN_SHADER };
    private StarSystem _system;
    private Vector2 _home = new(0.28f, 0.5f);
    private Color _sun = Colors.White;
    private ColorRect _star;
    private Label _name;
    private float _time;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _star = new ColorRect { Material = _sunMaterial, Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Size = Vector2.One * SUN_RECT };
        AddChild(_star);
        _name = new Label { MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled, HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
        _name.AddThemeFontSizeOverride("font_size", 13);
        _name.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        _name.AddThemeConstantOverride("outline_size", 4);
        AddChild(_name);
    }

    /// <summary>Builds the system of a planet.</summary>
    /// <param name="planet">(Required) The playable planet in the middle of the view.</param>
    /// <param name="globeCenter">(Required) Where its big globe sits, as share of this control's size.</param>
    public void Show(Planet planet, Vector2 globeCenter)
    {
        _home   = globeCenter;
        _time   = 0f;
        _sun    = StarSystem.ColorOf(planet.StarKind);
        _system = StarSystem.Of(planet);
        _sunMaterial.SetShaderParameter("tint", new Vector3(_sun.R, _sun.G, _sun.B));
        _sunMaterial.SetShaderParameter("disk", SUN_DISK * Mathf.Sqrt(StarSystem.SizeOf(planet.StarKind)));
        _sunMaterial.SetShaderParameter("seed", (float)(StarSystem.SeedOf(planet.Key) % 97));

        foreach ((StarSystem.Neighbor _, PlanetGlobe globe, int _) in _neighbors)
        {
            globe.QueueFree();
        }
        _neighbors.Clear();
        _hovered = -1;
        for (int i = 0; i < _system.Neighbors.Count; i++)
        {
            StarSystem.Neighbor neighbor = _system.Neighbors[i];
            float growth = neighbor.HasRing ? PlanetGlobe.RING_GROWTH : 1f;
            var   globe  = new PlanetGlobe { Size = Vector2.One * NEIGHBOR_PIXELS * neighbor.Size * growth, Glow = 0.1f, Spin = 0.08f, Modulate = new Color(NEIGHBOR_DIM, NEIGHBOR_DIM, NEIGHBOR_DIM) };
            globe.Show(neighbor.Look);
            if (neighbor.HasRing)
            {
                globe.Radius = PlanetGlobe.RING_RADIUS;
                globe.Ring   = 1f;
            }
            // Hovering names the neighbor; the mouse passes on, nothing can be clicked
            int index = i;
            globe.MouseFilter   = MouseFilterEnum.Pass;
            globe.MouseEntered += () => _hovered = index;
            globe.MouseExited  += () => _hovered = index == _hovered ? -1 : _hovered;
            AddChild(globe);
            _neighbors.Add((neighbor, globe, i));
        }
        MoveChild(_name, -1);
        SeedBelt();
        SeedComet();
        QueueRedraw();
    }

    // ===========================================================================
    // ================================   ORBITS   ===============================
    // ===========================================================================
    //
    //   SUN_START    Where the star stands at the start, as share of the size (left edge, mostly outside).
    //   HomeRadius   The home orbit runs through the planet: its radius from the start geometry.
    //   Radius       Radius of any orbit, ORBIT_STEP times wider per step outwards from the home orbit.
    //   HomeAngle    Where on its orbit the planet is now: it travels, the camera rides with it.
    //   SunAt        The star now, seen from the riding camera: it circles the planet.
    //   OnOrbit      A point on an orbit at an angle, the orbits are flat ellipses around the star.
    //   _Draw        The orbit lines (the home orbit brighter), the belt, the comet.
    //

    private static readonly Vector2 SUN_START = new(-0.06f, 0.62f);
    private const float SQUASH = 0.38f;              // the orbits lie flat, seen from above at a low angle
    private const float HOME_SPEED = 0.03f;          // radians per second the planet travels on its orbit
    private const float ORBIT_STEP = 1.6f;           // each orbit this much wider than the one inside it, wide gaps like a real system

    private Vector2 StartWay => (_home - SUN_START) * Size;    // from the star to the planet at the start, pixels

    private float HomeRadius => new Vector2(StartWay.X, StartWay.Y / SQUASH).Length();

    private float StartAngle => Mathf.Atan2(StartWay.Y / SQUASH, StartWay.X);

    public float HomeAngle => StartAngle + _time * HOME_SPEED;

    private Vector2 SunAt => _home * Size - Offset(HomeAngle, HomeRadius);

    private float Radius(float orbit)
    {
        return HomeRadius * Mathf.Pow(ORBIT_STEP, orbit - _system.HomeOrbit);
    }

    private static Vector2 Offset(float angle, float radius)
    {
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * SQUASH);
    }

    private Vector2 OnOrbit(float orbit, float angle)
    {
        return SunAt + Offset(angle, Radius(orbit));
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
        DrawBelt();
        DrawComet();
    }

    // ===========================================================================
    // ================================   MOTION   ===============================
    // ===========================================================================
    //
    //   SpeedOf     Radians per second on an orbit: inner ones faster (Kepler), the home orbit HOME_SPEED.
    //   AngleOf     The angle of a neighbor now; at the start every neighbor stands near the planet's angle.
    //   LightAt     Where the light of the star falls on a globe: from the star on the screen, from
    //               behind when the globe stands nearer to the camera than the star (a crescent).
    //   HomeLight   The light on the playable planet, its moons share it (PlanetView).
    //   _Process    The star, the neighbors and their light; the hovered one is named.
    //   NameOf      "Velloth c · Gas giant · unexplored".
    //

    private const float NEIGHBOR_SPREAD = 2.2f;      // radians around the planet the neighbors start in
    private const float LIGHT_FRONT = 0.5f;          // the light comes this much from the front ..
    private const float LIGHT_DEPTH = 0.8f;          // .. and swings this much behind or before the globe on its orbit
    private const float NAME_MARGIN = 12f;

    private int _hovered = -1;

    private float SpeedOf(int orbit)
    {
        return HOME_SPEED * Mathf.Pow(HomeRadius / Radius(orbit), 1.5f);
    }

    private float AngleOf(StarSystem.Neighbor neighbor)
    {
        return StartAngle + (neighbor.Start / Mathf.Tau - 0.5f) * NEIGHBOR_SPREAD + _time * SpeedOf(neighbor.Orbit);
    }

    /// <summary>The direction the light of the star comes from onto a globe (x right, y up, z towards the camera).</summary>
    /// <param name="at">(Required) The middle of the globe on the screen.</param>
    /// <param name="angle">(Required) Its angle on its orbit.</param>
    public Vector3 LightAt(Vector2 at, float angle)
    {
        Vector2 way = (SunAt - at).Normalized();
        return new Vector3(way.X, -way.Y, LIGHT_FRONT - Mathf.Sin(angle) * LIGHT_DEPTH).Normalized();
    }

    public Vector3 HomeLight => null == _system ? PlanetGlobe.DEFAULT_LIGHT : LightAt(_home * Size, HomeAngle);

    public override void _Process(double delta)
    {
        if (null == _system || !IsVisibleInTree())
        {
            return;
        }
        _time += (float)delta;
        _star.Position = SunAt - _star.Size * 0.5f;
        foreach ((StarSystem.Neighbor neighbor, PlanetGlobe globe, int _) in _neighbors)
        {
            float   angle = AngleOf(neighbor);
            Vector2 at    = OnOrbit(neighbor.Orbit, angle);
            globe.Position = at - globe.Size * 0.5f;
            globe.Light    = LightAt(at, angle);
        }
        _name.Visible = _hovered >= 0;
        if (_name.Visible)
        {
            (StarSystem.Neighbor neighbor, PlanetGlobe globe, int index) = _neighbors[_hovered];
            _name.Text     = NameOf(neighbor, index);
            _name.Size     = _name.GetCombinedMinimumSize();
            Vector2 below  = globe.Position + new Vector2(globe.Size.X * 0.5f - _name.Size.X * 0.5f, globe.Size.Y * (neighbor.HasRing ? 0.75f : 1f) + 4f);
            _name.Position = new Vector2(Mathf.Clamp(below.X, NAME_MARGIN, Size.X - _name.Size.X - NAME_MARGIN), below.Y);    // stays on the screen near the edge
        }
        QueueRedraw();
    }


    private string NameOf(StarSystem.Neighbor neighbor, int index)
    {
        return $"{StarNames.PlanetOf(_system.Key, index)} · {Tr($"WORLD_{neighbor.Kind}".ToUpperInvariant())}\n{Tr("MENU_SYSTEM_WILD")}";
    }

    public override void _Notification(int what)
    {
        if (NotificationResized == what)
        {
            QueueRedraw();
        }
    }

    // ===========================================================================
    // ============================   BELT AND COMET   ===========================
    // ===========================================================================
    //
    //   SeedBelt    Every other system has a belt of rocks on a free orbit, seeded by its key.
    //   DrawBelt    The rocks drift slowly along it.
    //   SeedComet   Where the comet comes from and goes, seeded by the key.
    //   DrawComet   Every COMET_PERIOD seconds the comet crosses the screen, head and tail away from the star.
    //   DrawTail    One tail of the comet as a fading fan.
    //

    private const int BELT_ROCKS = 320;
    private const float BELT_SPEED = 0.012f;         // radians per second
    private const float COMET_PERIOD = 24f;          // seconds between two comets
    private const float COMET_SECONDS = 10f;         // seconds a comet takes across the screen
    private const string BELT_SALT = "/belt";
    private const string COMET_SALT = "/comet";

    private readonly List<(float Angle, float Orbit, float Size, float Light)> _rocks = new();
    private Vector2 _cometFrom;
    private Vector2 _cometTo;
    private float _cometDelay;

    private void SeedBelt()
    {
        _rocks.Clear();
        var dice = new System.Random(StarSystem.SeedOf(_system.Key + BELT_SALT));
        if (dice.NextDouble() < 0.5)
        {
            return;
        }
        // A free gap: between two orbits where no planet runs on either side
        var taken = new HashSet<int> { _system.HomeOrbit };
        foreach (StarSystem.Neighbor neighbor in _system.Neighbors)
        {
            taken.Add(neighbor.Orbit);
        }
        int gap = -1;
        for (int orbit = 0; orbit < StarSystem.ORBITS - 1 && gap < 0; orbit++)
        {
            gap = !taken.Contains(orbit) && orbit != _system.HomeOrbit - 1 ? orbit : -1;
        }
        if (gap < 0)
        {
            return;
        }
        for (int i = 0; i < BELT_ROCKS; i++)
        {
            float spread = ((float)dice.NextDouble() + (float)dice.NextDouble() - 1f) * 0.22f;    // denser in the middle of the belt
            _rocks.Add(((float)(dice.NextDouble() * Mathf.Tau), gap + spread, 0.6f + (float)dice.NextDouble() * 1.4f, 0.25f + (float)dice.NextDouble() * 0.55f));
        }
    }

    private void DrawBelt()
    {
        foreach ((float angle, float orbit, float size, float light) in _rocks)
        {
            Vector2 at = OnOrbit(orbit, angle + _time * BELT_SPEED);
            DrawCircle(at, size, new Color(_sun.Lerp(new Color(0.75f, 0.7f, 0.65f), 0.6f), light));
        }
    }

    private void SeedComet()
    {
        var dice = new System.Random(StarSystem.SeedOf(_system.Key + COMET_SALT));
        _cometFrom  = new Vector2(0.75f + (float)dice.NextDouble() * 0.35f, -0.08f);
        _cometTo    = new Vector2(0.15f + (float)dice.NextDouble() * 0.3f, 1.08f);
        _cometDelay = 3f + (float)dice.NextDouble() * 6f;    // the first one comes soon, the player should see it
    }

    /// <summary>A tail of the comet: a fan from its head, fading to nothing at its end.</summary>
    private void DrawTail(Vector2 head, Vector2 way, float length, float width, Color color)
    {
        Vector2 side  = new(-way.Y, way.X);
        var     clear = new Color(color, 0f);
        DrawPolygon(
            new[] { head + side * 1.5f, head + way * length + side * width, head + way * length - side * width, head - side * 1.5f },
            new[] { color, clear, clear, color });
    }

    private void DrawComet()
    {
        float cycle = Mathf.PosMod(_time - _cometDelay, COMET_PERIOD);
        if (_time < _cometDelay || cycle > COMET_SECONDS)
        {
            return;
        }
        // A gentle curve towards the star, the tail always points away from it
        float   t    = cycle / COMET_SECONDS;
        Vector2 at   = _cometFrom.Lerp(_cometTo, t) * Size + new Vector2(-Mathf.Sin(t * Mathf.Pi) * Size.X * 0.08f, 0f);
        Vector2 away = (at - SunAt).Normalized();
        float   fade = Mathf.Sin(t * Mathf.Pi);
        float   tail = 110f + 70f * fade;
        // Dust tail: wide, soft, a little bent back along the path; ion tail: thin, blue, straight away from the star
        Vector2 bent = (away - (_cometTo - _cometFrom).Normalized() * 0.35f).Normalized();
        DrawTail(at, bent, tail * 0.8f, 16f, new Color(1f, 0.95f, 0.85f, 0.22f * fade));
        DrawTail(at, away, tail * 1.3f, 4f, new Color(0.65f, 0.85f, 1f, 0.5f * fade));
        DrawCircle(at, 7f, new Color(0.8f, 0.92f, 1f, 0.25f * fade));
        DrawCircle(at, 2.5f, new Color(1f, 1f, 1f, 0.95f * fade));
    }
}
