// NOTE:
//   A small button that shows one color zone of a creature on a little animal: the whole
//   body, the belly, the head, the limbs or the tail, filled in the color the zone has
//   right now. The CreatureToolbar shows one per zone, the picked one takes the swatches.
//
//   var icon = new ZoneIcon { Zone = CreatureZone.Belly, CustomMinimumSize = new Vector2(38, 30) };
//   icon.Pressed += () => PickZone(icon.Zone);

//
//   Chapters:
//     ZONE ICON   What it shows and how it is drawn.
//

using Godot;

namespace CellOut;

public partial class ZoneIcon : Button
{
    // ===========================================================================
    // ==============================   ZONE ICON   ==============================
    // ===========================================================================
    //
    //   Zone, Body, Fill, IsPicked   What the icon shows, every change redraws it.
    //   _Ready                       A bare button: the icon draws itself.
    //   _Draw                        The little animal in the body color, its zone in the zone color.
    //

    private CreatureZone _zone;
    private Color _body = Colors.Gray;
    private Color? _fill;
    private bool _isPicked;

    /// <summary>The zone shown.</summary>
    public CreatureZone Zone
    {
        get => _zone;
        set { _zone = value; QueueRedraw(); }
    }

    /// <summary>The body color of the little animal.</summary>
    public Color Body
    {
        get => _body;
        set { _body = value; QueueRedraw(); }
    }

    /// <summary>The color of the zone, null while it has none: the zone is only outlined then.</summary>
    public Color? Fill
    {
        get => _fill;
        set { _fill = value; QueueRedraw(); }
    }

    /// <summary>The picked zone wears a bright frame.</summary>
    public bool IsPicked
    {
        get => _isPicked;
        set { _isPicked = value; QueueRedraw(); }
    }

    public override void _Ready()
    {
        Flat      = true;
        FocusMode = FocusModeEnum.None;
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }
        MouseEntered += () => EditorStyle.ScaleTo(this, 1.12f);
        MouseExited  += () => EditorStyle.ScaleTo(this, 1f);
    }

    public override void _Draw()
    {
        // The little animal looks to the right: an oval body, a tail fin on the left, two legs below
        Vector2 center = Size * 0.5f + Vector2.Up * 2f;
        float   half   = Mathf.Min(Size.X * 0.36f, Size.Y * 0.5f);
        var     body   = new Vector2(half, half * 0.6f);
        Color   plain  = _body with { A = 1f };
        Color   zone   = (_fill ?? plain.Lightened(0.35f)) with { A = 1f };
        Color   ink    = _isPicked ? Colors.White : new Color(EditorStyle.RIM_COLOR, 0.75f);

        Vector2[] tail =
        {
            center + new Vector2(-body.X * 0.85f, 0f),
            center + new Vector2(-body.X * 1.5f, -body.Y * 0.8f),
            center + new Vector2(-body.X * 1.5f, body.Y * 0.8f),
        };
        DrawColoredPolygon(tail, CreatureZone.Tail == _zone ? zone : plain);
        foreach (float x in new[] { -0.35f, 0.3f })
        {
            DrawLine(center + new Vector2(x * body.X, body.Y * 0.7f), center + new Vector2(x * body.X, body.Y * 1.6f),
                CreatureZone.Limbs == _zone ? zone : plain, half * 0.2f, true);
        }
        DrawSetTransform(center, 0f, new Vector2(1f, body.Y / body.X));
        DrawCircle(Vector2.Zero, body.X, CreatureZone.Body == _zone ? zone : plain);
        if (CreatureZone.Belly == _zone)
        {
            DrawArc(Vector2.Zero, body.X * 0.5f, 0.15f, Mathf.Pi - 0.15f, 20, zone, body.X, true);
        }
        if (CreatureZone.Head == _zone)
        {
            DrawArc(Vector2.Zero, body.X * 0.5f, -1.0f, 1.0f, 16, zone, body.X, true);
        }
        DrawArc(Vector2.Zero, body.X, 0f, Mathf.Tau, 32, ink, _isPicked ? 2.4f : 1.3f, true);
        DrawSetTransform(Vector2.Zero);
        DrawCircle(center + new Vector2(body.X * 0.55f, -body.Y * 0.2f), half * 0.09f, Colors.Black);
    }
}
