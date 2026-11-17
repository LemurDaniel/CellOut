// NOTE:
//   The map of the archipelago, drawn from the coast itself: sea, sand and grass sampled
//   from Coast.LandDepth into a small texture (the real shorelines, not circles) with a
//   relief: the land is lit from the upper left, rises towards the middle of an island
//   and throws a shadow onto the sea. The map sits in its frame like in a well: rounded
//   corners in the color of the frame and a shadow along the upper edge. On top
//   the own fire, the own people, the player and the fires of the rival tribes in their
//   color.
//   Used twice by the TribeHub: small in the bar and large as the world map, where
//   the tribes also carry their names. It only reads the world, a click is reported by
//   its owner.
//
//   var map = new WorldMap { CustomMinimumSize = new Vector2(190, 96), FrameColor = barColor };
//   map.Watch(session, pack, rivals);
//   AddChild(map);

//
//   Chapters:
//     WATCH   What the map shows and when it samples the coast again.
//     DRAW    Terrain texture and the markers.
//

using Godot;

namespace CellOut;

public partial class WorldMap : Control
{
    // ===========================================================================
    // ================================   WATCH   ================================
    // ===========================================================================
    //
    //   HasNames    (Optional) Writes the names of the tribes next to their fires (the large map).
    //   FrameColor  (Optional) Color of what surrounds the map, its rounded corners are filled with it.
    //   Watch       The world, the pack, the rivals and the camera the map shows.
    //   _Process    Redraws a few times per second, samples the terrain when the coast changed.
    //   Sample      Paints sea, sand and grass of the area around all islands into the texture.
    //

    private const int TEXTURE_WIDTH = 224;
    private const int SHADOW_TEXELS = 3;             // how far an island throws its shadow onto the sea
    private const float RELIEF = 0.16f;              // how much a slope facing the light brightens
    private const float REDRAW_SECONDS = 0.15f;
    private const float AREA_MARGIN = 1.15f;         // the map shows this much more than the islands
    private static readonly Color DEEP_COLOR = new(0.1f, 0.26f, 0.5f);
    private static readonly Color SEA_COLOR = new(0.16f, 0.38f, 0.64f);
    private static readonly Color SHALLOW_COLOR = new(0.3f, 0.62f, 0.78f);
    private static readonly Color SAND_COLOR = new(0.9f, 0.82f, 0.56f);
    private static readonly Color GRASS_COLOR = new(0.46f, 0.7f, 0.34f);
    private static readonly Color INLAND_COLOR = new(0.24f, 0.48f, 0.24f);

    public bool HasNames { get; set; }

    public Color FrameColor { get; set; } = new(0.05f, 0.09f, 0.12f);

    private WorldSession _session;
    private PackKeeper _pack;
    private RivalKeeper _rivals;
    private Coast _sampled;
    private ImageTexture _terrain;
    private Rect2 _area;
    private float _redrawIn;

    /// <summary>Hands in what the map shows, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="pack">(Required) The pack: nest and kin.</param>
    /// <param name="rivals">(Required) The rival tribes.</param>
    public void Watch(WorldSession session, PackKeeper pack, RivalKeeper rivals)
    {
        _session     = session;
        _pack        = pack;
        _rivals      = rivals;
        MouseFilter  = MouseFilterEnum.Ignore;
        ClipContents = true;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || null == _session?.Coast)
        {
            return;
        }
        if (_session.Coast != _sampled)
        {
            Sample(_session.Coast);
        }
        _redrawIn -= (float)delta;
        if (_redrawIn <= 0f)
        {
            _redrawIn = REDRAW_SECONDS;
            QueueRedraw();
        }
    }

    /*
      The area is the box around every island, widened to the aspect of the control, so
      the texture fills it without distortion. Each texel asks the coast how deep inland
      its point lies:
      > Sea: deep blue far out, lighter towards the shore, darker where an island up
        left of it throws its shadow.
      > Land: sand up to the grass line, then grass that darkens inland; a slope that
        rises towards the lower right faces the light from the upper left and is brighter.
    */
    private void Sample(Coast coast)
    {
        _sampled = coast;
        if (0 == coast.Islands.Count)
        {
            _terrain = null;
            return;
        }
        Rect2 box = new(coast.Islands[0].Center, Vector2.Zero);
        foreach (Coast.Island island in coast.Islands)
        {
            box = box.Merge(new Rect2(island.Center - Vector2.One * island.Radius, Vector2.One * island.Radius * 2f));
        }
        float   aspect = Size.Y > 0f ? Size.X / Size.Y : 2f;
        Vector2 extent = box.Size * AREA_MARGIN;
        extent = extent.X / extent.Y > aspect ? new Vector2(extent.X, extent.X / aspect) : new Vector2(extent.Y * aspect, extent.Y);
        _area  = new Rect2(box.GetCenter() - extent * 0.5f, extent);

        int   height = Mathf.Max(1, Mathf.RoundToInt(TEXTURE_WIDTH / aspect));
        float grass  = Balance.Current.Tribe.Pack.NestDepth * coast.Unit;

        // Depth inland per texel, in grass lines: 0 at the shore, 1 where the grass begins
        var depths = new float[TEXTURE_WIDTH, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < TEXTURE_WIDTH; x++)
            {
                Vector2 point = _area.Position + new Vector2((x + 0.5f) / TEXTURE_WIDTH, (y + 0.5f) / height) * _area.Size;
                depths[x, y] = coast.LandDepth(point) / grass;
            }
        }

        Image image = Image.CreateEmpty(TEXTURE_WIDTH, height, false, Image.Format.Rgb8);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < TEXTURE_WIDTH; x++)
            {
                float depth  = depths[x, y];
                float upLeft = depths[Mathf.Max(x - 1, 0), Mathf.Max(y - 1, 0)];
                Color color;
                if (depth < 0f)
                {
                    color = depth > -1.5f ? SHALLOW_COLOR.Lerp(SEA_COLOR, -depth / 1.5f) : SEA_COLOR.Lerp(DEEP_COLOR, Mathf.Clamp((-depth - 1.5f) / 6f, 0f, 1f));
                    bool isShaded = depths[Mathf.Max(x - SHADOW_TEXELS, 0), Mathf.Max(y - SHADOW_TEXELS, 0)] >= 0f;
                    color = isShaded ? color.Darkened(0.28f) : color;
                }
                else
                {
                    color = depth < 1f ? SAND_COLOR : GRASS_COLOR.Lerp(INLAND_COLOR, Mathf.Clamp((depth - 1f) / 5f, 0f, 1f));
                    float slope = Mathf.Clamp((depth - upLeft) * 4f, -1f, 1f);
                    color = slope > 0f ? color.Lightened(slope * RELIEF) : color.Darkened(-slope * RELIEF);
                }
                image.SetPixel(x, y, color);
            }
        }
        _terrain = ImageTexture.CreateFromImage(image);
        Log.Verbose("WorldMap", $"Sampled {coast.Islands.Count} island(s) into {TEXTURE_WIDTH}x{height}.");
    }

    // ===========================================================================
    // ================================   DRAW   =================================
    // ===========================================================================
    //
    //   _Draw       Terrain, the rival fires, the own fire, the own people, the player, the camera view and the frame on top.
    //   DrawMarker  A dot with a dark rim and a soft halo.
    //   DrawFrame   The shadow along the upper and left edge and the rounded corners.
    //   ToMap       A plane point as a pixel of the control.
    //

    private static readonly Color FIRE_COLOR = new(1f, 0.82f, 0.3f);
    private static readonly Color DEFEATED_COLOR = new(0.45f, 0.45f, 0.45f);
    private static readonly Color RIM_COLOR = new(0.05f, 0.08f, 0.1f);
    private const float CORNER_RADIUS = 16f;

    private float _clock;

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), SEA_COLOR);
        if (null == _terrain)
        {
            return;
        }
        DrawTextureRect(_terrain, new Rect2(Vector2.Zero, Size), false);
        float dot = Mathf.Max(2.5f, Size.Y * 0.035f);
        _clock += REDRAW_SECONDS;

        // Rival tribes: their fire in their color, grey once defeated, the name on the large map.
        // The lair of the lone beast is a dark fang instead of a fire, gone once the beast is
        foreach (RivalTribe rival in _rivals.Everyone)
        {
            Vector2 at    = ToMap(rival.Fire);
            Color   color = rival.IsDefeated ? DEFEATED_COLOR : rival.Tint;
            if (rival.IsLoner)
            {
                if (!rival.IsDefeated)
                {
                    DrawFang(at, dot * 1.5f);
                }
                continue;
            }
            DrawMarker(at, dot * 1.1f, color);
            if (HasNames)
            {
                Font font = GetThemeDefaultFont();
                DrawStringOutline(font, at + new Vector2(dot * 2f, dot), Tr(rival.NameKey), HorizontalAlignment.Left, -1f, 16, 5, RIM_COLOR);
                DrawString(font, at + new Vector2(dot * 2f, dot), Tr(rival.NameKey), HorizontalAlignment.Left, -1f, 16, Colors.White);
            }
        }

        // The own village, its people and the player on top
        if (null != _pack.Nest)
        {
            Vector2 fire = ToMap(_pack.Nest.Position);
            // The own fire flickers
            DrawMarker(fire, dot * (1.3f + 0.15f * Mathf.Sin(_clock * 6f)), FIRE_COLOR);
        }
        foreach (Kin kin in _pack.Kin)
        {
            if (!GodotObject.IsInstanceValid(kin))
            {
                continue;       // freed since the last physics frame, read here outside of it
            }
            if (KinMode.Wild != kin.Mode && !kin.IsQueuedForDeletion())
            {
                DrawCircle(ToMap(kin.Position), dot * 0.55f, _session.Player.BodyColor);
            }
        }
        Vector2 player = ToMap(_session.Player.GlobalPosition);
        DrawMarker(player, dot * 0.8f, Colors.White);
        DrawFrame();
    }

    private static readonly Color FANG_COLOR = new(0.55f, 0.12f, 0.12f);

    /// <summary>The lair of the lone beast: a dark red fang with a rim.</summary>
    private void DrawFang(Vector2 at, float size)
    {
        Vector2[] Shape(float grow) => new[]
        {
            at + new Vector2(-size * 0.7f - grow, -size * 0.6f - grow), at + new Vector2(size * 0.7f + grow, -size * 0.6f - grow), at + new Vector2(0f, size + grow * 1.6f),
        };
        DrawColoredPolygon(Shape(1.6f), RIM_COLOR);
        DrawColoredPolygon(Shape(0f), FANG_COLOR);
    }

    private void DrawMarker(Vector2 at, float radius, Color color)
    {
        DrawCircle(at, radius * 2.2f, new Color(color, 0.22f));
        DrawCircle(at, radius + 1.5f, RIM_COLOR);
        DrawCircle(at, radius, color);
        DrawCircle(at - Vector2.One * radius * 0.3f, radius * 0.35f, new Color(1f, 1f, 1f, 0.55f));
    }

    /// <summary>The map lies deeper than its frame: a shadow falls in from the upper and left edge, the corners are cut round.</summary>
    private void DrawFrame()
    {
        const int   SHADOW_LINES = 7;
        const int   ARC_STEPS    = 8;
        float       radius       = Mathf.Min(CORNER_RADIUS, Size.Y * 0.2f);
        for (int i = 0; i < SHADOW_LINES; i++)
        {
            var shade = new Color(0f, 0f, 0f, 0.3f * (1f - (float)i / SHADOW_LINES));
            DrawRect(new Rect2(0f, i, Size.X, 1f), shade);
            DrawRect(new Rect2(i, 0f, 1f, Size.Y), shade);
        }

        // Each corner: the wedge between the square corner and a quarter circle, filled with the frame color
        var corners = new (Vector2 Corner, Vector2 Inward)[]
        {
            (Vector2.Zero, new Vector2(1f, 1f)), (new Vector2(Size.X, 0f), new Vector2(-1f, 1f)),
            (Size, new Vector2(-1f, -1f)), (new Vector2(0f, Size.Y), new Vector2(1f, -1f)),
        };
        foreach ((Vector2 corner, Vector2 inward) in corners)
        {
            Vector2 center = corner + inward * radius;
            var     wedge  = new Vector2[ARC_STEPS + 2];
            wedge[0] = corner;
            for (int i = 0; i <= ARC_STEPS; i++)
            {
                float angle = Mathf.Pi * 0.5f * i / ARC_STEPS;
                wedge[i + 1] = center - new Vector2(Mathf.Cos(angle) * inward.X, Mathf.Sin(angle) * inward.Y) * radius;
            }
            DrawColoredPolygon(wedge, FrameColor);
        }
    }

    private Vector2 ToMap(Vector2 point)
    {
        return (point - _area.Position) / _area.Size * Size;
    }
}
