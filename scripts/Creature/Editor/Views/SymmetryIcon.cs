// NOTE:
//   A square button that draws a symmetry of the creature editor: two mirrored halves
//   on both sides of an axis (axis symmetry), or a ring of dots around a center with its
//   count (point symmetry). The CreatureToolbar shows one of each, they exclude each other.
//
//   var icon = new SymmetryIcon { Count = 0, CustomMinimumSize = new Vector2(52, 52) };   // 0 = the axis icon
//   icon.IsOn = session.IsSymmetric;

//
//   Chapters:
//     SYMMETRY ICON   What it shows and how it is drawn.
//

using Godot;

namespace CellOut;

public partial class SymmetryIcon : Button
{
    // ===========================================================================
    // ============================   SYMMETRY ICON   ============================
    // ===========================================================================
    //
    //   Count, IsOn, Accent   What the icon shows, every change redraws it.
    //   _Ready                A bare button: the icon draws itself.
    //   _Draw                 The frame, then the mirrored halves or the ring with its count.
    //

    private const int IDLE_DOTS = 5;                 // dots of the ring icon while point symmetry is off
    private const int COUNT_FONT_SIZE = 15;

    private int _count;
    private bool _isOn;
    private Color _accent = Colors.White;

    /// <summary>0 for the axis icon, else the ring icon with this many dots (the point symmetry count).</summary>
    public int Count
    {
        get => _count;
        set { _count = value; QueueRedraw(); }
    }

    /// <summary>Whether this symmetry is the active one: lit in the accent color.</summary>
    public bool IsOn
    {
        get => _isOn;
        set { _isOn = value; QueueRedraw(); }
    }

    /// <summary>Color of the lit icon.</summary>
    public Color Accent
    {
        get => _accent;
        set { _accent = value; QueueRedraw(); }
    }

    /// <summary>True for the ring icon (point symmetry), false for the axis icon.</summary>
    public bool IsRing { get; set; }

    public override void _Ready()
    {
        Flat      = true;
        FocusMode = FocusModeEnum.None;
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }
        MouseEntered += () => EditorStyle.ScaleTo(this, 1.08f);
        MouseExited  += () => EditorStyle.ScaleTo(this, 1f);
    }

    public override void _Draw()
    {
        Vector2 center = Size * 0.5f;
        float   half   = Mathf.Min(Size.X, Size.Y) * 0.5f - 2f;
        Color   ink    = _isOn ? _accent : new Color(EditorStyle.RIM_COLOR, 0.8f);
        Color   fill   = _isOn ? new Color(_accent, 0.22f) : new Color(0f, 0f, 0f, 0.25f);
        var     frame  = new Rect2(center - Vector2.One * half, Vector2.One * half * 2f);
        DrawRect(frame, fill);
        DrawRect(frame, ink, false, _isOn ? 2.5f : 1.5f);

        if (!IsRing)
        {
            // Axis symmetry: a dashed axis, a drop on each side as mirror images
            for (float y = -half * 0.75f; y < half * 0.75f; y += half * 0.3f)
            {
                DrawLine(center + new Vector2(0f, y), center + new Vector2(0f, y + half * 0.16f), ink, 1.5f, true);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                DrawCircle(center + new Vector2(side * half * 0.45f, 0f), half * 0.24f, ink);
                DrawCircle(center + new Vector2(side * half * 0.2f, -half * 0.3f), half * 0.11f, ink);
            }
            return;
        }

        // Point symmetry: dots in a ring around the center, the count in the middle while it is on
        int   dots   = _isOn && _count >= 2 ? _count : IDLE_DOTS;
        float radius = half * 0.66f;
        for (int i = 0; i < dots; i++)
        {
            float angle = Mathf.Tau * i / dots - Mathf.Pi * 0.5f;
            DrawCircle(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, half * 0.12f, ink);
        }
        if (_isOn && _count >= 2)
        {
            Font   font = GetThemeDefaultFont();
            string text = _count.ToString();
            Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1f, COUNT_FONT_SIZE);
            DrawString(font, center + new Vector2(-size.X * 0.5f, size.Y * 0.3f), text, HorizontalAlignment.Left, -1f, COUNT_FONT_SIZE, ink);
        }
        else
        {
            DrawCircle(center, half * 0.1f, ink);
        }
    }
}
