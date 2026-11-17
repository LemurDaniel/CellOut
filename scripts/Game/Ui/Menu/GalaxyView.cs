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

        // The view starts at the first planet (Terra), right of the logo
        Vector2 home = Planets.All[0].GalaxySpot.Rotated(-_angle);
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
        _systems.ViewScale = _scale;
        _systems.Modulate  = new Color(1f, 1f, 1f, 1f - _dim);
        _center.Position   = ToScreen(Vector2.Zero) + new Vector2(14f, 6f);
        _center.Visible    = _dim < 0.5f;

        // Markers on their orbits, bigger when close, at the star when far out
        float zoom = Mathf.Clamp(_scale / START_SCALE, 0.55f, 1.8f);
        foreach ((Planet planet, StarSystem system, Button marker, Label name) in _markers)
        {
            Vector2 spot = _systems.Shown > 0.5f ? _systems.HomeSpot(system) : ToScreen(planet.GalaxySpot);
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
    private Vector2 ToScreen(Vector2 disk)
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
    //   _GuiInput               A drag with any button pans, the wheel zooms towards the mouse.
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
            ZoomAt(wheel.Position, MouseButton.WheelUp == wheel.ButtonIndex ? WHEEL_STEP : 1f / WHEEL_STEP);
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion motion && 0 != motion.ButtonMask)
        {
            _pan += motion.Relative / Size.Y;
            AcceptEvent();
        }
    }

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
    // ===============================   PLANETS   ===============================
    // ===========================================================================
    //
    //   PlanetPicked   A planet was clicked (a C# event: a record is no Variant).
    //   AddMarker      A round button with the globe of a planet and its name below.
    //   Translate      Names in the current language.
    //

    public event System.Action<Planet> PlanetPicked;

    private const float MARKER_SIZE = 46f;

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

        marker.MouseEntered += () =>
        {
            globe.Glow = 1f;
            EditorStyle.ScaleTo(marker, 1.18f);
        };
        marker.MouseExited += () =>
        {
            globe.Glow = 0.4f;
            EditorStyle.ScaleTo(marker, 1f);
        };
        marker.Pressed += () => PlanetPicked?.Invoke(planet);
        AddChild(marker);
        AddChild(name);
        _markers.Add((planet, StarSystem.Of(planet), marker, name));
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
        _homePan   = _pan;
        _homeScale = _scale;
        // Where the pan has to be so the planet sits at FOCUS_SPOT at the focus scale (the disk keeps turning slowly)
        Vector2 turned = planet.GalaxySpot.Rotated(-_angle);
        Vector2 pan    = FOCUS_SPOT - new Vector2(turned.X, turned.Y * TILT) * FOCUS_SCALE;
        FlyTo(pan, FOCUS_SCALE, 1f);
    }

    /// <summary>Flies back to where the player was before the focus.</summary>
    public void Unfocus()
    {
        FlyTo(_homePan, _homeScale, 0f);
    }

    private void FlyTo(Vector2 pan, float scale, float dim)
    {
        _focusTween?.Kill();
        _focusTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel()
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _focusTween.TweenMethod(Callable.From<Vector2>(value => _pan = value), _pan, pan, FOCUS_SECONDS);
        _focusTween.TweenMethod(Callable.From<float>(value => _scale = value), _scale, scale, FOCUS_SECONDS);
        _focusTween.TweenMethod(Callable.From<float>(value => _dim = value), _dim, dim, FOCUS_SECONDS);
    }
}
