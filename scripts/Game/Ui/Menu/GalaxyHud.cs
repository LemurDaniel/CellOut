// NOTE:
//   The quiet instruments over the Milky Way of the galaxy menu (Menu/GalaxyView.cs): a scale
//   bar in light years that keeps a round length while zooming, and at the screen edge a small pointer to every playable planet out of sight, its globe
//   colors in a dot, an arrow outwards and its name. GalaxyView asks PlanetAt on a click and
//   flies there. Ignores the mouse itself.
//
//   var hud = new GalaxyHud { ToScreen = ToScreen };
//   AddChild(hud);
//   hud.ViewScale = scale;                  // every frame
//   Planet planet = hud.PlanetAt(mouse);    // a pointer under the mouse, or null

//
//   Chapters:
//     HUD        Mapping and scale.
//     SCALE BAR  A round number of light years as a bar bottom left.
//     POINTERS   Planets out of sight at the screen edge.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class GalaxyHud : Control
{
    // ===========================================================================
    // =================================   HUD   =================================
    // ===========================================================================
    //
    //   ToScreen    (Required) Disk point to pixels, the mapping of GalaxyView.
    //   ViewScale   Disk radius in screen heights, set every frame.
    //   _Ready      Fills the parent, ignores the mouse.
    //

    public System.Func<Vector2, Vector2> ToScreen { get; set; }

    public float ViewScale { get; set; } = 1f;

    private const float CORNER = 26f;                // pixels from the bottom left corner

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawScaleBar();
        DrawPointers();
    }

    // ===========================================================================
    // ==============================   SCALE BAR   ==============================
    // ===========================================================================
    //
    //   BAR_PIXELS     The length the bar aims for.
    //   DrawScaleBar   The bar with end ticks bottom left, its length written over it.
    //

    private const float BAR_PIXELS = 140f;

    private void DrawScaleBar()
    {
        float perYear = ViewScale * Size.Y / Galaxy.RADIUS_LIGHT_YEARS;
        if (perYear <= 0f)
        {
            return;
        }
        int   years  = Galaxy.RoundLightYears(BAR_PIXELS / perYear);
        float length = years * perYear;
        var   start  = new Vector2(CORNER, Size.Y - CORNER);
        var   end    = start + new Vector2(length, 0f);
        var   color  = new Color(0.8f, 0.85f, 1f, 0.7f);
        DrawLine(start, end, color, 2f, true);
        DrawLine(start + new Vector2(0f, -5f), start + new Vector2(0f, 5f), color, 2f, true);
        DrawLine(end + new Vector2(0f, -5f), end + new Vector2(0f, 5f), color, 2f, true);
        string text = string.Format(Tr("MENU_LIGHT_YEARS"), SystemCard.Number(years));
        DrawString(GetThemeDefaultFont(), start + new Vector2(0f, -10f), text, HorizontalAlignment.Left, -1f, 13, color);
    }

    // ===========================================================================
    // ===============================   POINTERS   ==============================
    // ===========================================================================
    //
    //   EDGE          Pixels the pointers keep from the screen edge.
    //   PlanetAt      The planet of the pointer under the mouse, null if none.
    //   DrawPointers  Dot, arrow and name of every playable planet out of sight.
    //   OnEdge        Where the line from the middle to a point leaves the inner rect.
    //

    private const float EDGE = 34f;
    private const float EDGE_BOTTOM = 70f;           // more room at the bottom: the scale bar and the settings buttons sit there
    private const float POINTER_RADIUS = 9f;
    private const float HIT_PIXELS = 22f;

    private readonly List<(Planet Planet, Vector2 At)> _pointers = new();

    /// <summary>The planet of the pointer under the mouse.</summary>
    /// <returns>Null when no pointer is near the mouse.</returns>
    public Planet PlanetAt(Vector2 mouse)
    {
        foreach ((Planet planet, Vector2 at) in _pointers)
        {
            if (at.DistanceTo(mouse) < HIT_PIXELS)
            {
                return planet;
            }
        }
        return null;
    }

    private void DrawPointers()
    {
        _pointers.Clear();
        if (null == ToScreen)
        {
            return;
        }
        var  inner = new Rect2(Vector2.One * EDGE, Size - new Vector2(EDGE * 2f, EDGE + EDGE_BOTTOM));
        Font font  = GetThemeDefaultFont();
        foreach (Planet planet in Planets.All)
        {
            Vector2 spot = ToScreen(planet.GalaxySpot);
            if (inner.HasPoint(spot))
            {
                continue;
            }
            Vector2 at  = OnEdge(inner, spot);
            Vector2 way = (spot - Size * 0.5f).Normalized();
            Vector2 side = new(-way.Y, way.X);
            _pointers.Add((planet, at));

            // Arrow outwards, the globe colors in a dot, the name inwards
            DrawColoredPolygon(new[] { at + way * 20f, at + way * 12f + side * 6f, at + way * 12f - side * 6f }, new Color(planet.AtmosphereColor, 0.85f));
            DrawCircle(at, POINTER_RADIUS + 2f, new Color(planet.AtmosphereColor, 0.35f));
            DrawCircle(at, POINTER_RADIUS, planet.OceanColor.Lerp(planet.LandColor, 0.4f));
            string  name = Tr($"PLANET_{planet.Key}");
            Vector2 text = font.GetStringSize(name, HorizontalAlignment.Left, -1f, 13);
            Vector2 left = at - way * (POINTER_RADIUS + 10f + text.X * 0.5f * Mathf.Abs(way.X)) - new Vector2(text.X * 0.5f, -text.Y * 0.3f);
            DrawString(font, left, name, HorizontalAlignment.Left, -1f, 13, new Color(1f, 1f, 1f, 0.8f));
        }
    }

    private Vector2 OnEdge(Rect2 inner, Vector2 spot)
    {
        Vector2 middle = Size * 0.5f;
        Vector2 way    = spot - middle;
        float   reachX = Mathf.Abs(way.X) < 0.001f ? float.MaxValue : ((way.X > 0f ? inner.End.X : inner.Position.X) - middle.X) / way.X;
        float   reachY = Mathf.Abs(way.Y) < 0.001f ? float.MaxValue : ((way.Y > 0f ? inner.End.Y : inner.Position.Y) - middle.Y) / way.Y;
        return middle + way * Mathf.Min(reachX, reachY);
    }
}
