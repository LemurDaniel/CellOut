// NOTE:
//   The Milky Way of the title menu (shaders/galaxy.gdshader) seen from inside: the view
//   starts close enough to see single star systems (Menu/GalaxySystemsLayer.cs, Core/Galaxy.cs),
//   a drag pans it, the mouse wheel zooms towards the mouse, all the way out to the whole
//   spiral with Sgr A* in its middle. Every playable planet is a clickable globe on its orbit
//   around its star; hovering lights its halo, a click picks it. Focus flies onto a planet
//   (the disk slides and grows under it and darkens), Unfocus flies back to where the
//   player was. Fills its parent.
//
//   var galaxy = new GalaxyView();
//   galaxy.PlanetPicked += planet => menu.ShowPlanet(planet);
//   AddChild(galaxy);
//   galaxy.Focus(planet);    // and galaxy.Unfocus()

//
//   Chapters:
//     VIEW      The galaxy rect, the turning disk and the mapping from disk to screen.
//     NAVIGATE  Drag to pan, wheel to zoom.
//     PLANETS   One marker per playable planet: globe on its orbit, name, hover and click.
//     FOCUS     Flying onto a planet and back out.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class GalaxyView : Control
{
    // ===========================================================================
    // =================================   VIEW   ================================
    // ===========================================================================
    //
    //   _Ready     The galaxy rect, the systems over it, a marker per planet, Sgr A*.
    //   _Process   Turns the disk, hands the view to the shader and the systems, moves the markers.
    //   ToScreen   A disk point in pixels of this control, the inverse of the shader mapping.
    //

    private const float TILT = 0.58f;
    private const float START_SCALE = 1.25f;         // disk radius in screen heights at the start: inside the galaxy, systems visible
    private const float TURN_SPEED = 0.004f;         // radians per second
    private const float START_ANGLE = 0.4f;
    private static readonly Vector2 START_SPOT = new(0.18f, -0.12f);    // where the first planet sits at the start, screen heights from the middle
    private static readonly Shader GALAXY_SHADER = GD.Load<Shader>("res://shaders/galaxy.gdshader");

    private readonly ShaderMaterial _material = new() { Shader = GALAXY_SHADER };
    private GalaxySystemsLayer _systems;
    private Label _center;
    private float _angle = START_ANGLE;
    private float _scale = START_SCALE;
    private Vector2 _pan;
    private float _dim;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Pass;
        var sky = new ColorRect { Material = _material, MouseFilter = MouseFilterEnum.Ignore, Color = Colors.White };
        sky.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(sky);
        _systems = new GalaxySystemsLayer { ToScreen = ToScreen };
        AddChild(_systems);

        // Sgr A*, the black hole in the middle, only named
        _center = new Label { Text = "Sgr A*", MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled, Modulate = new Color(1f, 0.85f, 0.65f, 0.8f) };
        _center.AddThemeFontSizeOverride("font_size", 13);
        _center.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        _center.AddThemeConstantOverride("outline_size", 4);
        AddChild(_center);

        foreach (Planet planet in Planets.All)
        {
            AddMarker(planet);
        }

        // Instruments and the card of what lies under the mouse over everything
        _hud = new GalaxyHud { ToScreen = ToScreen };
        AddChild(_hud);
        _card = new SystemCard();
        AddChild(_card);

        // The view starts at the current planet (Terra in a fresh game), right of the logo
        Vector2 home = Planets.Current.GalaxySpot.Rotated(-_angle);
        _pan = START_SPOT - new Vector2(home.X, home.Y * TILT) * _scale;
    }

    public override void _Process(double delta)
    {
        _angle += (float)delta * TURN_SPEED;
        float aspect = Size.X / Mathf.Max(Size.Y, 1f);
        _material.SetShaderParameter("aspect", aspect);
        _material.SetShaderParameter("angle", _angle);
        _material.SetShaderParameter("tilt", TILT);
        _material.SetShaderParameter("scale", _scale);
        _material.SetShaderParameter("pan", _pan);
        _material.SetShaderParameter("dim", _dim);
        _material.SetShaderParameter("warp", _warp);
        _material.SetShaderParameter("warp_center", _warpCenter);
        _material.SetShaderParameter("warp_way", _warpWay);
        _systems.ViewScale = _scale;
        _systems.Modulate  = new Color(1f, 1f, 1f, 1f - _dim);
        _systems.Hovered   = _hovered;
        _hud.ViewScale     = _scale;
        _hud.Modulate      = _systems.Modulate;
        _hud.Visible       = _dim < 0.95f;
        _center.Position   = ToScreen(Vector2.Zero) + new Vector2(14f, 6f) * (1f + _holeGlow);
        _center.Visible    = _dim < 0.5f;
        _holeGlow = Mathf.MoveToward(_holeGlow, _isHoleHovered ? 1f : 0f, (float)delta * 4f);
        _material.SetShaderParameter("hole_glow", _holeGlow);
        RefreshCard();

        // Markers on their orbits, bigger when close, at the star when far out
        float zoom = Mathf.Clamp(_scale / START_SCALE, 0.55f, 1.8f);
        foreach ((Planet planet, StarSystem system, Button marker, Label name) in _markers)
        {
            Vector2 spot = MarkerSpot(planet, system);
            marker.Size        = Vector2.One * MARKER_SIZE * planet.GlobeSize * zoom;
            marker.PivotOffset = marker.Size * 0.5f;
            marker.GetChild<PlanetGlobe>(0).Size = marker.Size;
            marker.Position = spot - marker.Size * 0.5f;
            name.Position   = spot + new Vector2(-name.Size.X * 0.5f, marker.Size.Y * 0.5f + 2f);
            marker.Modulate = name.Modulate = new Color(1f, 1f, 1f, 1f - _dim);
            marker.Visible  = _dim < 0.95f;
            name.Visible    = marker.Visible;
        }
    }

    /// <summary>A point on the disk of the galaxy (-1..1) in pixels of this control, the same mapping as the shader.</summary>
    public Vector2 ToScreen(Vector2 disk)
    {
        Vector2 turned = disk.Rotated(-_angle);
        var q = new Vector2(turned.X, turned.Y * TILT) * _scale + _pan;
        float aspect = Size.X / Mathf.Max(Size.Y, 1f);
        return (q / new Vector2(aspect, 1f) + new Vector2(0.5f, 0.5f)) * Size;
    }

    // ===========================================================================
    // ===============================   NAVIGATE   ==============================
    // ===========================================================================
    //
    //   MIN_SCALE / MAX_SCALE   The whole spiral .. a few systems.
    //   _GuiInput               A drag with any button pans, the wheel zooms towards the mouse,
    //                           a click without a drag picks what is under the mouse (HOVER AND CLICK),
    //                           moving the mouse hovers.
    //   ZoomAt                  Changes the scale, the disk point under the mouse stays under it.
    //

    private const float MIN_SCALE = 0.42f;
    private const float MAX_SCALE = 4.5f;
    private const float WHEEL_STEP = 1.15f;

    private bool IsNavigable => _dim < 0.05f;

    public override void _GuiInput(InputEvent @event)
    {
        if (!IsNavigable)
        {
            return;
        }
        if (@event is InputEventMouseButton { Pressed: true } wheel && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            StopLooking();
            ZoomAt(wheel.Position, MouseButton.WheelUp == wheel.ButtonIndex ? WHEEL_STEP : 1f / WHEEL_STEP);
            AcceptEvent();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
        {
            // A press without a drag before its release is a click
            if (click.Pressed)
            {
                _pressAt    = click.Position;
                _hasDragged = false;
            }
            else if (!_hasDragged)
            {
                ClickAt(click.Position);
            }
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion motion && 0 != motion.ButtonMask)
        {
            StopLooking();
            _pan += motion.Relative / Size.Y;
            _hasDragged |= motion.Position.DistanceTo(_pressAt) > CLICK_SLOP;
            HoverAt(null);
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion hover)
        {
            HoverAt(hover.Position);
        }
    }

    private const float CLICK_SLOP = 6f;             // pixels a press may move and still be a click

    private Vector2 _pressAt;
    private bool _hasDragged;

    private void ZoomAt(Vector2 mouse, float factor)
    {
        float   aspect = Size.X / Mathf.Max(Size.Y, 1f);
        Vector2 q      = (mouse / Size - new Vector2(0.5f, 0.5f)) * new Vector2(aspect, 1f);
        float   scale  = Mathf.Clamp(_scale * factor, MIN_SCALE, MAX_SCALE);
        // q = disk * scale + pan stays the same for the point under the mouse
        _pan   = q - (q - _pan) * (scale / _scale);
        _scale = scale;
    }

    // ===========================================================================
    // ===========================   HOVER AND CLICK   ===========================
    // ===========================================================================
    //
    //   HoverAt       The star system or Sgr A* under the mouse, null clears it.
    //   HolePixels    The radius of Sgr A* on the screen, the size formula of the shader.
    //   RefreshCard   The card follows what is hovered (a star, a planet marker, the hole), hides in the planet view.
    //   ClickAt       A pointer flies to its planet, a playable star picks its planet,
    //                 a wild star and the hole fly close.
    //

    private const float HOVER_PIXELS = 16f;          // a star counts as hovered this close to the mouse
    private const float HOVER_SHOWN = 0.3f;          // .. once the systems show this much
    private const float SYSTEM_SCALE = 3.2f;         // a click on a wild star flies at least this close
    private const float HOLE_SCALE = 3.5f;
    private const float BH_RING = 0.034f;            // shaders/galaxy.gdshader BH_RING and BH_MAX_SCALE
    private const float BH_MAX_SCALE = 0.9f;

    private GalaxyHud _hud;
    private SystemCard _card;
    private StarSystem _hovered;
    private StarSystem _markerHovered;
    private bool _isHoleHovered;
    private float _holeGlow;

    private void HoverAt(Vector2? mouse)
    {
        _hovered       = null;
        _isHoleHovered = false;
        if (mouse is not Vector2 at || !IsNavigable)
        {
            return;
        }
        float best = HOVER_PIXELS;
        if (_systems.Shown > HOVER_SHOWN)
        {
            foreach (StarSystem system in Galaxy.Systems)
            {
                float distance = ToScreen(system.Spot).DistanceTo(at);
                if (distance < best)
                {
                    best     = distance;
                    _hovered = system;
                }
            }
        }
        _isHoleHovered = null == _hovered && ToScreen(Vector2.Zero).DistanceTo(at) < HolePixels();
    }

    private float HolePixels()
    {
        float size = _scale < BH_MAX_SCALE ? _scale : BH_MAX_SCALE * Mathf.Pow(_scale / BH_MAX_SCALE, 0.6f);
        return Mathf.Max(HOVER_PIXELS, BH_RING * size * Size.Y * 1.6f);
    }

    private void RefreshCard()
    {
        if (!IsNavigable)
        {
            _hovered = _markerHovered = null;
            _isHoleHovered = false;
            _card.HideCard();
        }
        else if (null != _markerHovered)
        {
            _card.ShowSystem(_markerHovered, MarkerSpot(Planets.Get(_markerHovered.PlanetKey), _markerHovered));
        }
        else if (null != _hovered)
        {
            _card.ShowSystem(_hovered, ToScreen(_hovered.Spot));
        }
        else if (_isHoleHovered)
        {
            _card.ShowHole(ToScreen(Vector2.Zero));
        }
        else
        {
            _card.HideCard();
        }
    }

    private void ClickAt(Vector2 mouse)
    {
        Planet pointed = _hud.PlanetAt(mouse);
        if (null != pointed)
        {
            LookAt(pointed.GalaxySpot, Mathf.Max(_scale, START_SCALE));
        }
        else if (null != _hovered?.PlanetKey)
        {
            PlanetPicked?.Invoke(Planets.Get(_hovered.PlanetKey));
        }
        else if (null != _hovered)
        {
            LookAt(_hovered.Spot, Mathf.Max(_scale, SYSTEM_SCALE));
        }
        else if (_isHoleHovered)
        {
            LookAt(Vector2.Zero, HOLE_SCALE);
        }
    }

    // ===========================================================================
    // ===============================   PLANETS   ===============================
    // ===========================================================================
    //
    //   PlanetPicked   A planet was clicked (a C# event: a record is no Variant).
    //   AddMarker      A round button with the globe of a planet and its name below.
    //   MarkerSpot     Where a marker sits on the screen.
    //   DrawProgress   The ring of phases reached around a marker.
    //   Translate      Names in the current language.
    //

    public event System.Action<Planet> PlanetPicked;

    private const float MARKER_SIZE = 46f;
    private const float GLOBE_SHARE = 0.8f;          // shaders/planet_globe.gdshader RADIUS: the globe fills this much of its rect
    private const float PROGRESS_GAP = 4f;           // pixels between the globe and its ring of phases
    private const float PROGRESS_SPACE = 0.06f;      // radians left free at each end of an arc

    private readonly List<(Planet Planet, StarSystem System, Button Marker, Label Name)> _markers = new();

    private void AddMarker(Planet planet)
    {
        float size   = MARKER_SIZE * planet.GlobeSize;
        var   marker = new Button
        {
            Size                    = Vector2.One * size,
            FocusMode               = FocusModeEnum.None,
            Flat                    = true,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
        {
            marker.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }
        var globe = new PlanetGlobe { Size = marker.Size, Glow = 0.4f };
        globe.Show(planet);
        marker.AddChild(globe);

        var name = new Label { Text = planet.Key, MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        name.AddThemeFontSizeOverride("font_size", 15);
        name.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        name.AddThemeConstantOverride("outline_size", 4);
        name.Modulate = new Color(1f, 1f, 1f, 0.85f);

        StarSystem system = System.Linq.Enumerable.First(Galaxy.Systems, each => each.PlanetKey == planet.Key);   // the same instance the hover finds
        marker.MouseEntered += () =>
        {
            _markerHovered = system;
            globe.Glow = 1f;
            EditorStyle.ScaleTo(marker, 1.18f);
        };
        marker.MouseExited += () =>
        {
            _markerHovered = system == _markerHovered ? null : _markerHovered;
            globe.Glow = 0.4f;
            EditorStyle.ScaleTo(marker, 1f);
        };
        marker.Pressed += () => PlanetPicked?.Invoke(planet);
        marker.Draw    += () => DrawProgress(marker, planet);
        AddChild(marker);
        AddChild(name);
        _markers.Add((planet, system, marker, name));
    }

    /// <summary>Where the marker of a planet sits: on its orbit when the systems show, else at its star.</summary>
    private Vector2 MarkerSpot(Planet planet, StarSystem system)
    {
        return _systems.Shown > 0.5f ? _systems.HomeSpot(system) : ToScreen(planet.GalaxySpot);
    }

    /// <summary>A ring of one arc per start phase around the globe, the phases reached on the planet lit (Core/PlanetProgress.cs).</summary>
    private static void DrawProgress(Button marker, Planet planet)
    {
        int     reached = (int)PlanetProgress.Reached(planet.Key);
        Vector2 middle  = marker.Size * 0.5f;
        float   radius  = marker.Size.X * 0.5f * GLOBE_SHARE + PROGRESS_GAP;
        float   arc     = Mathf.Tau / StartPhases.COUNT;
        for (int phase = 0; phase < StartPhases.COUNT; phase++)
        {
            float from  = -Mathf.Pi * 0.5f + phase * arc + PROGRESS_SPACE;
            var   color = phase <= reached ? new Color(planet.AtmosphereColor.Lerp(Colors.White, 0.3f), 0.95f) : new Color(1f, 1f, 1f, 0.16f);
            marker.DrawArc(middle, radius, from, from + arc - PROGRESS_SPACE * 2f, 8, color, 2f, true);
        }
    }

    /// <summary>Names of the planets in the current language.</summary>
    public void Translate()
    {
        foreach ((Planet planet, StarSystem _, Button _, Label name) in _markers)
        {
            name.Text = Tr($"PLANET_{planet.Key}");
        }
    }

    // ===========================================================================
    // ================================   FOCUS   ================================
    // ===========================================================================
    //
    //   Focus     The disk slides and grows so the planet comes to the left third, it darkens.
    //   Unfocus   Back to where the player was before.
    //   LookAt    Flies to any point of the disk at a scale (edge markers, the graphics reel).
    //   StopLooking   Breaks off a LookAt flight.
    //   FlyTo     The tween behind all three, warp streaks on the way.
    //

    private const float FOCUS_SECONDS = 0.8f;
    private const float FOCUS_SCALE = 7f;            // disk radius while a planet is shown: deep inside its arm, the star system lies over it
    private static readonly Vector2 FOCUS_SPOT = new(-0.36f, 0.04f);   // where the planet lands, in screen heights from the middle

    private Vector2 _homePan;
    private float _homeScale = START_SCALE;
    private Tween _focusTween;

    /// <summary>Flies onto a planet: it lands on the left of the screen, the galaxy grows and darkens behind it.</summary>
    /// <param name="planet">(Required) The planet to look at.</param>
    public void Focus(Planet planet)
    {
        _isLooking = false;
        _homePan   = _pan;
        _homeScale = _scale;
        // Where the pan has to be so the planet sits at FOCUS_SPOT at the focus scale (the disk keeps turning slowly)
        Vector2 turned = planet.GalaxySpot.Rotated(-_angle);
        Vector2 pan    = FOCUS_SPOT - new Vector2(turned.X, turned.Y * TILT) * FOCUS_SCALE;
        FlyTo(pan, FOCUS_SCALE, 1f, FOCUS_SPOT);
    }

    /// <summary>Flies back to where the player was before the focus.</summary>
    public void Unfocus()
    {
        _isLooking = false;
        FlyTo(_homePan, _homeScale, 0f, Vector2.Zero);
    }

    /// <summary>Flies the view to a point of the disk at a scale, the stars streak on the way.</summary>
    /// <param name="disk">(Required) The point of the disk (-1..1) that comes to the middle of the screen.</param>
    /// <param name="scale">(Required) The disk radius in screen heights at the end, clamped to the zoom range.</param>
    /// <example><code>galaxy.LookAt(Vector2.Zero, 3f);   // close to Sgr A*</code></example>
    public void LookAt(Vector2 disk, float scale)
    {
        scale = Mathf.Clamp(scale, MIN_SCALE, MAX_SCALE);
        Vector2 turned = disk.Rotated(-_angle);
        FlyTo(-new Vector2(turned.X, turned.Y * TILT) * scale, scale, 0f, Vector2.Zero);
        _isLooking = true;
    }

    private const float WARP_MIN = 0.3f;             // streaks of a short hop

    private bool _isLooking;                         // a LookAt flight runs, the player may break it off

    /// <summary>Breaks off a LookAt flight when the player drags or zooms; a focus flight runs on.</summary>
    private void StopLooking()
    {
        if (!_isLooking)
        {
            return;
        }
        _isLooking = false;
        _focusTween?.Kill();
        _warp = 0f;
    }

    private float _warp;
    private Vector2 _warpCenter = new(0.5f, 0.5f);
    private float _warpWay = 1f;

    /// <summary>Tweens pan, scale and dim; streaks rush away from where the view flies to, most in the middle of the flight.</summary>
    private void FlyTo(Vector2 pan, float scale, float dim, Vector2 target)
    {
        float aspect = Size.X / Mathf.Max(Size.Y, 1f);
        _warpCenter = target / new Vector2(aspect, 1f) + new Vector2(0.5f, 0.5f);
        _warpWay    = scale >= _scale ? 1f : -1f;      // zooming in the streaks rush outwards, out they rush in
        // Longer jumps in scale streak more, a planet focus fully
        float strength = Mathf.Clamp(Mathf.Abs(Mathf.Log(scale / _scale)) / Mathf.Log(FOCUS_SCALE / START_SCALE), WARP_MIN, 1f);
        _focusTween?.Kill();
        _focusTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel()
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _focusTween.TweenMethod(Callable.From<Vector2>(value => _pan = value), _pan, pan, FOCUS_SECONDS);
        _focusTween.TweenMethod(Callable.From<float>(value => _scale = value), _scale, scale, FOCUS_SECONDS);
        _focusTween.TweenMethod(Callable.From<float>(value => _dim = value), _dim, dim, FOCUS_SECONDS);
        _focusTween.TweenMethod(Callable.From<float>(value => _warp = Mathf.Sin(value * Mathf.Pi) * strength), 0f, 1f, FOCUS_SECONDS)
            .SetTrans(Tween.TransitionType.Linear);
    }
}
