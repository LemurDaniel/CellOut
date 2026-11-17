// NOTE:
//   The standing with the stranger close by, drawn below the hint of the TribePanel, so a
//   courtship or a display visibly moves something:
//     Heart row   the relation of its pack from hostile (red, left) to friendly (green,
//                 right) with a white marker, ticks where it turns hostile, friendly and
//                 where an animal joins the player's pack.
//     Crown row   the respect won by displays, a tick where tribute starts, and an arrow:
//                 up when a display right now would be won, down when it would be lost.
//   A view: the panel hands in the numbers every frame, it holds no state of the game.
//
//   var meter = new EncounterMeter();
//   panel.AddChild(meter);
//   meter.Refresh(new EncounterMeter.State(relation: 0.2f, respect: 0.5f, isDisplayWon: true));
//   meter.Refresh(null);    // no stranger close by: hidden

//
//   Chapters:
//     STATE   What the meter shows.
//     DRAW    Both rows with their symbols.
//

using Godot;

namespace CellOut;

public partial class EncounterMeter : Control
{
    // ===========================================================================
    // ================================   STATE   ================================
    // ===========================================================================
    //
    //   State     The standing with one stranger.
    //   Refresh   Takes the numbers and redraws, null hides the meter.
    //

    /// <summary>The standing with the stranger close by.</summary>
    /// <param name="Relation">(Required) -1 hostile .. 1 friendly.</param>
    /// <param name="Respect">(Required) 0 .. 1, won by displays.</param>
    /// <param name="IsDisplayWon">(Required) Whether a display right now would be won.</param>
    public readonly record struct State(float Relation, float Respect, bool IsDisplayWon);

    private static readonly Vector2 SIZE = new(300f, 46f);

    private State _state;

    public override void _Ready()
    {
        CustomMinimumSize   = SIZE;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter         = MouseFilterEnum.Ignore;
        Visible             = false;
    }

    /// <summary>Shows the standing with a stranger, or hides the meter.</summary>
    /// <param name="state">(Required) The numbers, null without a stranger close by.</param>
    public void Refresh(State? state)
    {
        Visible = state.HasValue;
        if (state is State shown && shown != _state)
        {
            _state = shown;
            QueueRedraw();
        }
    }

    // ===========================================================================
    // =================================   DRAW   ================================
    // ===========================================================================
    //
    //   _Draw       The heart row and the crown row.
    //   DrawTrack   The dark rounded track of a row.
    //   DrawHeart   The symbol of the relation.
    //   DrawCrown   The symbol of the respect.
    //   DrawArrow   Up for a display that would be won, down for one that would be lost.
    //

    private const float ICON_WIDTH = 26f;
    private const float ARROW_WIDTH = 22f;
    private const float BAR_HEIGHT = 12f;
    private const float ROW_GAP = 23f;
    private const int GRADIENT_STEPS = 24;

    private static readonly Color TRACK_COLOR = new(0.05f, 0.09f, 0.12f, 0.85f);
    private static readonly Color TICK_COLOR = new(1f, 1f, 1f, 0.75f);
    private static readonly Color RESPECT_COLOR = new(1f, 0.82f, 0.35f);
    private static readonly Color NEUTRAL_COLOR = new(0.62f, 0.66f, 0.68f);

    public override void _Draw()
    {
        WaysBalance   ways   = Balance.Current.Tribe.Ways;
        RivalsBalance rivals = Balance.Current.Tribe.Rivals;
        float left   = ICON_WIDTH;
        float width  = SIZE.X - ICON_WIDTH - ARROW_WIDTH;
        Color hostile  = EditorStyle.DIET_COLORS[DietClass.Carnivore];
        Color friendly = EditorStyle.DIET_COLORS[DietClass.Herbivore];

        // Heart row: red over grey to green, ticks at hostile, friendly and where an animal joins, the marker at the relation
        float heartY = BAR_HEIGHT * 0.5f + 3f;
        DrawHeart(new Vector2(ICON_WIDTH * 0.42f, heartY), _state.Relation >= rivals.FriendlyAt ? friendly : _state.Relation <= rivals.HostileAt ? hostile : NEUTRAL_COLOR);
        DrawTrack(left, heartY, width);
        for (int i = 0; i < GRADIENT_STEPS; i++)
        {
            float from = (float)i / GRADIENT_STEPS;
            float to   = (float)(i + 1) / GRADIENT_STEPS;
            Color a = from < 0.5f ? hostile.Lerp(NEUTRAL_COLOR, from * 2f) : NEUTRAL_COLOR.Lerp(friendly, from * 2f - 1f);
            Color b = to < 0.5f ? hostile.Lerp(NEUTRAL_COLOR, to * 2f) : NEUTRAL_COLOR.Lerp(friendly, to * 2f - 1f);
            float x0 = left + width * from;
            float x1 = left + width * to;
            DrawPolygon(new[] { new Vector2(x0, heartY - 3f), new Vector2(x1, heartY - 3f), new Vector2(x1, heartY + 3f), new Vector2(x0, heartY + 3f) }, new[] { a, b, b, a });
        }
        foreach (float tick in new[] { rivals.HostileAt, rivals.FriendlyAt, ways.AdoptAt })
        {
            float x = left + width * (tick * 0.5f + 0.5f);
            DrawLine(new Vector2(x, heartY - BAR_HEIGHT * 0.5f), new Vector2(x, heartY + BAR_HEIGHT * 0.5f), TICK_COLOR, 2f, true);
        }
        var marker = new Vector2(left + width * (Mathf.Clamp(_state.Relation, -1f, 1f) * 0.5f + 0.5f), heartY);
        DrawCircle(marker, 7.5f, EditorStyle.OUTLINE, true, -1f, true);
        DrawCircle(marker, 5.5f, Colors.White, true, -1f, true);

        // Crown row: gold up to the respect, a tick where tribute starts, the arrow of the display
        float crownY  = heartY + ROW_GAP;
        float respect = Mathf.Clamp(_state.Respect, 0f, 1f);
        DrawCrown(new Vector2(ICON_WIDTH * 0.42f, crownY), respect >= ways.RespectAt ? RESPECT_COLOR : NEUTRAL_COLOR);
        DrawTrack(left, crownY, width);
        if (respect > 0.01f)
        {
            DrawLine(new Vector2(left, crownY), new Vector2(left + width * respect, crownY), RESPECT_COLOR, 6f, true);
        }
        float tributeX = left + width * ways.RespectAt;
        DrawLine(new Vector2(tributeX, crownY - BAR_HEIGHT * 0.5f), new Vector2(tributeX, crownY + BAR_HEIGHT * 0.5f), TICK_COLOR, 2f, true);
        DrawArrow(new Vector2(left + width + ARROW_WIDTH * 0.6f, crownY), _state.IsDisplayWon, _state.IsDisplayWon ? friendly : hostile);
    }

    private void DrawTrack(float left, float y, float width)
    {
        DrawLine(new Vector2(left, y), new Vector2(left + width, y), EditorStyle.OUTLINE, BAR_HEIGHT + 3f, true);
        DrawLine(new Vector2(left, y), new Vector2(left + width, y), TRACK_COLOR, BAR_HEIGHT, true);
    }

    /// <summary>A heart: two lobes and a tip, with a dark rim so it reads on grass and sky.</summary>
    private void DrawHeart(Vector2 center, Color color)
    {
        foreach ((float grow, Color fill) in new[] { (1.6f, EditorStyle.OUTLINE), (0f, color) })
        {
            DrawCircle(center + new Vector2(-3.6f, -2f), 4.4f + grow, fill, true, -1f, true);
            DrawCircle(center + new Vector2(3.6f, -2f), 4.4f + grow, fill, true, -1f, true);
            DrawColoredPolygon(new[] { center + new Vector2(-7.6f - grow, 0f), center + new Vector2(7.6f + grow, 0f), center + new Vector2(0f, 8.5f + grow * 1.4f) }, fill);
        }
    }

    /// <summary>A crown: a band with three points.</summary>
    private void DrawCrown(Vector2 center, Color color)
    {
        Vector2[] Shape(float grow) => new[]
        {
            center + new Vector2(-8f - grow, 6f + grow), center + new Vector2(-8f - grow, -5f - grow), center + new Vector2(-4f, 0f - grow),
            center + new Vector2(0f, -7f - grow * 1.4f), center + new Vector2(4f, 0f - grow), center + new Vector2(8f + grow, -5f - grow),
            center + new Vector2(8f + grow, 6f + grow),
        };
        DrawColoredPolygon(Shape(1.6f), EditorStyle.OUTLINE);
        DrawColoredPolygon(Shape(0f), color);
    }

    private void DrawArrow(Vector2 center, bool isUp, Color color)
    {
        float sign = isUp ? -1f : 1f;
        Vector2[] Shape(float grow) => new[]
        {
            center + new Vector2(0f, (7f + grow * 1.4f) * sign), center + new Vector2(-7f - grow, (-5f - grow) * sign), center + new Vector2(7f + grow, (-5f - grow) * sign),
        };
        DrawColoredPolygon(Shape(1.8f), EditorStyle.OUTLINE);
        DrawColoredPolygon(Shape(0f), color);
    }
}
