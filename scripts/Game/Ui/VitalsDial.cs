// NOTE:
//   The round dial bottom right that bundles what the player watches all the time:
//   in the middle the editor icon (the DNA helix, grey without points, with the points
//   badge), around it the health ring, then the ring of the progress to the next
//   evolution and outside the diet gauge (herbivore green on the left over omnivore
//   yellow at the top to carnivore red on the right, a marker at the current diet). The
//   air shows as a short arc in the gap below the gauge while it is not full. The name
//   of the diet stands left of the dial. Only the disc takes the mouse, each ring
//   answers with its own tooltip.
//   A view: the Hud hands in the numbers with Refresh and hears EditorPressed.
//
//   var dial = new VitalsDial();
//   dial.EditorPressed += () => OpenEditor();
//   AddChild(dial);
//   dial.Refresh(new VitalsDial.State(0.8f, 0.4f, -0.2f, true, 3, "Health 80 / 100", "Biomass 40", "Evolve (Z)"));

//
//   Chapters:
//     TYPES         The numbers and texts the dial shows.
//     STATE         What the Hud hands in, the mouse and the breathing icon.
//     BUILD         The editor icon with its badge and the diet label.
//     DRAW          Disc, rings, diet gauge, air arc and the helix.
//

using Godot;

namespace CellOut;

public partial class VitalsDial : Control
{
    // ===========================================================================
    // ================================   TYPES   ================================
    // ===========================================================================
    //
    //   State   One refresh of the dial.
    //

    /// <summary>What the dial shows after a refresh.</summary>
    /// <param name="Health">(Required) Health share 0..1, the inner ring.</param>
    /// <param name="Progress">(Required) Progress to the next evolution 0..1, the middle ring.</param>
    /// <param name="Diet">(Required) Diet -1 herbivore .. 1 carnivore, the marker on the gauge.</param>
    /// <param name="HasDiet">(Required) Whether the diet counts yet (from the single cell on), else the gauge stays dark.</param>
    /// <param name="Points">(Required) Evolution points, the icon is grey without any.</param>
    /// <param name="HealthTip">(Required) Translated tooltip of the health ring.</param>
    /// <param name="ProgressTip">(Required) Translated tooltip of the progress ring.</param>
    /// <param name="EditorTip">(Required) Translated tooltip of the editor icon.</param>
    public readonly record struct State(float Health, float Progress, float Diet, bool HasDiet, int Points, string HealthTip, string ProgressTip, string EditorTip);

    // ===========================================================================
    // ================================   STATE   ================================
    // ===========================================================================
    //
    //   EditorPressed   The editor icon was pressed.
    //   Refresh         Takes the numbers, a first point punches the icon.
    //   Air             Breath 0..1, the arc shows while it is not full.
    //   _Process        The ready icon breathes, a low health ring pulses.
    //   _HasPoint       Only the disc takes the mouse, the corners let clicks through.
    //   _GetTooltip     The tooltip of the ring under the mouse.
    //

    [Signal] public delegate void EditorPressedEventHandler();

    private const float SIZE = 196f;                 // the control is a square around the disc
    private const float CORNER_MARGIN = 12f;
    private const float DISC_RADIUS = 94f;
    private const float ICON_SIZE = 72f;
    private const float HEALTH_RADIUS = 47f;
    private const float PROGRESS_RADIUS = 60f;
    private const float RING_WIDTH = 10f;
    private const float DIET_RADIUS = 75f;
    private const float DIET_WIDTH = 12f;
    private const float DIET_HALF_SWEEP = Mathf.Tau / 3f;      // the gauge spans 240 degrees over the top
    private const float AIR_HALF_SWEEP = Mathf.Tau / 9f;       // the air arc spans 80 degrees in the gap below
    private const float DIET_ZONE = 1f / 3f;         // beyond this share of the gauge the diet has a side
    private const float LOW_HEALTH = 0.3f;           // below this share the health ring pulses
    private const float POP_SECONDS = 0.5f;          // the punch of the icon runs this long, then it breathes
    private const float TOP = -Mathf.Pi * 0.5f;      // angle of 12 o'clock, angles grow clockwise

    private static readonly Vector2 CENTER = Vector2.One * SIZE * 0.5f;

    private State _state;
    private float _air = 1f;
    private float _time;
    private float _readyAt;
    private Button _icon;
    private Label _badge;
    private Label _dietLabel;

    /// <summary>Breath 0..1 of the land animal, set every frame by the Hud.</summary>
    public float Air
    {
        get => _air;
        set
        {
            if (!Mathf.IsEqualApprox(_air, value))
            {
                _air = value;
                QueueRedraw();
            }
        }
    }

    /// <summary>Takes the numbers of the player and redraws.</summary>
    /// <param name="state">(Required) Shares, diet, points and the translated tooltips.</param>
    public void Refresh(State state)
    {
        // The icon is grey without points, it punches once points arrive
        bool isReady = state.Points > 0;
        if (isReady && _icon.Disabled)
        {
            _readyAt = _time;
            EditorStyle.Punch(_icon, 1.25f);
        }
        _state            = state;
        _icon.Disabled    = !isReady;
        _icon.TooltipText = state.EditorTip;
        _badge.Text       = state.Points.ToString();
        _badge.Visible    = isReady;
        _dietLabel.Visible = state.HasDiet;
        _dietLabel.Text    = Tr(DietKey(state.Diet));
        _dietLabel.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[DietOf(state.Diet)]);
        _icon.QueueRedraw();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;

        // A gentle pulse once the punch is over, which tweens the same scale
        if (!_icon.Disabled && _time - _readyAt > POP_SECONDS && !_icon.IsHovered())
        {
            _icon.Scale = Vector2.One * (1f + 0.04f * Mathf.Sin(_time * 4f + 1.3f));
        }
        if (_state.Health < LOW_HEALTH)
        {
            QueueRedraw();
        }
    }

    public override bool _HasPoint(Vector2 point)
    {
        return point.DistanceTo(CENTER) <= DISC_RADIUS;
    }

    /*
      From the middle outwards:
      > Up to the health ring: the health
      > Up to the progress ring: the goal of the stage
      > Outside, in the gap below while the air shows: the air
      > Outside otherwise: the name of the diet, nothing before the single cell
    */
    public override string _GetTooltip(Vector2 atPosition)
    {
        Vector2 offset = atPosition - CENTER;
        if (offset.Length() < HEALTH_RADIUS + RING_WIDTH * 0.5f + 1.5f)
        {
            return _state.HealthTip ?? "";
        }
        if (offset.Length() < PROGRESS_RADIUS + RING_WIDTH * 0.5f + 1.5f)
        {
            return _state.ProgressTip ?? "";
        }
        bool isInGap = Mathf.Abs(Mathf.AngleDifference(offset.Angle(), -TOP)) < Mathf.Pi - DIET_HALF_SWEEP;
        if (isInGap && IsAirShown)
        {
            return Tr("DIAL_AIR");
        }
        return _state.HasDiet ? Tr(DietKey(_state.Diet)) : "";
    }

    private bool IsAirShown => _air < 0.999f;

    private static DietClass DietOf(float diet)
    {
        return diet < -DIET_ZONE ? DietClass.Herbivore : diet > DIET_ZONE ? DietClass.Carnivore : DietClass.Omnivore;
    }

    private static string DietKey(float diet)
    {
        return DietOf(diet) switch
        {
            DietClass.Herbivore => "DIET_HERBIVORE",
            DietClass.Carnivore => "DIET_CARNIVORE",
            _                   => "DIET_OMNIVORE",
        };
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready      The dial in the bottom right corner, the icon in its middle, the diet label left of it.
    //   NewLabel    Label with outline that ignores the mouse and is translated here.
    //

    private static readonly Color EDITOR_ACCENT = new(0.95f, 0.85f, 0.35f);
    private static readonly Color EDITOR_GREY = new(0.55f, 0.6f, 0.62f);

    public override void _Ready()
    {
        CustomMinimumSize = Vector2.One * SIZE;
        Size              = Vector2.One * SIZE;
        MouseFilter       = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical   = GrowDirection.Begin;
        Position      -= Vector2.One * CORNER_MARGIN;

        // No focus: Z opens the editor through Main, a focused button would fire twice
        _icon = new Button
        {
            CustomMinimumSize = Vector2.One * ICON_SIZE,
            Size              = Vector2.One * ICON_SIZE,
            Position          = CENTER - Vector2.One * ICON_SIZE * 0.5f,
            PivotOffset       = Vector2.One * ICON_SIZE * 0.5f,
            FocusMode         = FocusModeEnum.None,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            Disabled          = true,
        };
        EditorStyle.ApplyButton(_icon, EDITOR_ACCENT);
        _icon.AddThemeStyleboxOverride("disabled", EditorStyle.Pill(EDITOR_GREY.Darkened(0.65f), EDITOR_GREY.Darkened(0.2f)));
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            ((StyleBoxFlat)_icon.GetThemeStylebox(state)).SetCornerRadiusAll((int)(ICON_SIZE * 0.5f));
        }
        _icon.Pressed += () => EmitSignal(SignalName.EditorPressed);
        _icon.Draw    += DrawHelix;
        AddChild(_icon);

        // Points badge on the upper right rim of the icon
        _badge = NewLabel(16);
        _badge.HorizontalAlignment = HorizontalAlignment.Center;
        StyleBoxFlat badge = EditorStyle.Pill(EDITOR_ACCENT.Darkened(0.2f), Colors.White, 0.5f);
        badge.SetContentMarginAll(1);
        badge.ContentMarginLeft  = 7;
        badge.ContentMarginRight = 7;
        _badge.AddThemeStyleboxOverride("normal", badge);
        _badge.Position = new Vector2(ICON_SIZE - 24f, -8f);
        _badge.Visible  = false;
        _icon.AddChild(_badge);

        // The name of the diet, right aligned against the lower left of the disc
        _dietLabel = NewLabel(15);
        _dietLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _dietLabel.Size     = new Vector2(220f, 22f);
        _dietLabel.Position = new Vector2(-220f + 14f, SIZE - 26f);
        _dietLabel.Visible  = false;
        AddChild(_dietLabel);
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label
        {
            MouseFilter       = MouseFilterEnum.Ignore,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    // ===========================================================================
    // ================================   DRAW   =================================
    // ===========================================================================
    //
    //   _Draw       Disc, health ring, progress ring, diet gauge and the air arc.
    //   DrawRing    A ring filled clockwise from the top up to a share, with round ends.
    //   DrawGauge   The diet gauge: gradient, zone ticks and the marker.
    //   DrawAir     The air arc in the gap below the gauge.
    //   DrawHelix   The helix of the icon, white when ready, grey without points.
    //   OnCircle    A point on a circle around the middle of the dial.
    //

    private const int ARC_POINTS = 64;
    private const int GAUGE_STEPS = 40;

    private static readonly Color DISC_COLOR = new(0.09f, 0.16f, 0.2f, 0.94f);       // the slab of the hub bar
    private static readonly Color DISC_EDGE = new(0.55f, 0.72f, 0.8f, 0.7f);
    private static readonly Color TRACK_COLOR = new(0f, 0f, 0f, 0.45f);
    private static readonly Color HEALTH_COLOR = new(0.95f, 0.4f, 0.4f);
    private static readonly Color PROGRESS_COLOR = new(0.45f, 0.9f, 0.6f);
    private static readonly Color AIR_COLOR = new(0.55f, 0.85f, 1f);

    public override void _Draw()
    {
        DrawCircle(CENTER, DISC_RADIUS, DISC_COLOR);
        DrawArc(CENTER, DISC_RADIUS - 1f, 0f, Mathf.Tau, ARC_POINTS, DISC_EDGE, 2f, true);

        // A low health ring pulses brighter
        Color health = HEALTH_COLOR;
        if (_state.Health < LOW_HEALTH)
        {
            health = health.Lightened(0.25f + 0.25f * Mathf.Sin(_time * 8f));
        }
        DrawRing(HEALTH_RADIUS, _state.Health, health);
        DrawRing(PROGRESS_RADIUS, _state.Progress, PROGRESS_COLOR);
        DrawGauge();
        DrawAir();
    }

    private void DrawRing(float radius, float share, Color color)
    {
        DrawArc(CENTER, radius, 0f, Mathf.Tau, ARC_POINTS, TRACK_COLOR, RING_WIDTH, true);
        share = Mathf.Clamp(share, 0f, 1f);
        if (share <= 0f)
        {
            return;
        }
        float end = TOP + share * Mathf.Tau;
        DrawArc(CENTER, radius, TOP, end, Mathf.Max(2, Mathf.CeilToInt(ARC_POINTS * share)), color, RING_WIDTH, true);
        DrawCircle(OnCircle(TOP, radius), RING_WIDTH * 0.5f, color, true, -1f, true);
        DrawCircle(OnCircle(end, radius), RING_WIDTH * 0.5f, color, true, -1f, true);
    }

    /*
      The gauge runs clockwise from the lower left over the top to the lower right:
      > Before the single cell: only the dark track
      > Otherwise: the gradient green -> yellow -> red as quads with vertex colors, its
        two edges drawn over with a dark line (the quads have no antialiasing), a dark
        tick at each zone border and the white marker at the diet
    */
    private void DrawGauge()
    {
        float start = TOP - DIET_HALF_SWEEP;
        float end   = TOP + DIET_HALF_SWEEP;
        float inner = DIET_RADIUS - DIET_WIDTH * 0.5f;
        float outer = DIET_RADIUS + DIET_WIDTH * 0.5f;
        if (!_state.HasDiet)
        {
            DrawArc(CENTER, DIET_RADIUS, start, end, ARC_POINTS, TRACK_COLOR, DIET_WIDTH, true);
            return;
        }

        Color herbivore = EditorStyle.DIET_COLORS[DietClass.Herbivore];
        Color omnivore  = EditorStyle.DIET_COLORS[DietClass.Omnivore];
        Color carnivore = EditorStyle.DIET_COLORS[DietClass.Carnivore];
        for (int i = 0; i < GAUGE_STEPS; i++)
        {
            float from = (float)i / GAUGE_STEPS;
            float to   = (float)(i + 1) / GAUGE_STEPS;
            Color left  = from < 0.5f ? herbivore.Lerp(omnivore, from * 2f) : omnivore.Lerp(carnivore, from * 2f - 1f);
            Color right = to < 0.5f ? herbivore.Lerp(omnivore, to * 2f) : omnivore.Lerp(carnivore, to * 2f - 1f);
            float a = Mathf.Lerp(start, end, from);
            float b = Mathf.Lerp(start, end, to);
            DrawPolygon(new[] { OnCircle(a, inner), OnCircle(a, outer), OnCircle(b, outer), OnCircle(b, inner) }, new[] { left, left, right, right });
        }
        DrawArc(CENTER, inner, start, end, ARC_POINTS, EditorStyle.OUTLINE, 1.5f, true);
        DrawArc(CENTER, outer, start, end, ARC_POINTS, EditorStyle.OUTLINE, 1.5f, true);

        // Zone borders at one third and two thirds
        foreach (float zone in new[] { -DIET_ZONE, DIET_ZONE })
        {
            float angle = TOP + zone * DIET_HALF_SWEEP;
            DrawLine(OnCircle(angle, inner), OnCircle(angle, outer), new Color(0f, 0f, 0f, 0.4f), 2f, true);
        }

        Vector2 marker = OnCircle(TOP + Mathf.Clamp(_state.Diet, -1f, 1f) * DIET_HALF_SWEEP, DIET_RADIUS);
        DrawCircle(marker, DIET_WIDTH * 0.5f + 3.5f, EditorStyle.OUTLINE, true, -1f, true);
        DrawCircle(marker, DIET_WIDTH * 0.5f + 1.5f, Colors.White, true, -1f, true);
    }

    /// <summary>Only while the air is not full: it drains on land without a lung, towards the left end of the arc.</summary>
    private void DrawAir()
    {
        if (!IsAirShown)
        {
            return;
        }
        float start = -TOP + AIR_HALF_SWEEP;        // the left end, angles grow clockwise and the arc lies below
        float end   = -TOP - AIR_HALF_SWEEP;
        DrawArc(CENTER, DIET_RADIUS, end, start, ARC_POINTS / 4, TRACK_COLOR, RING_WIDTH, true);
        float share = Mathf.Clamp(_air, 0f, 1f);
        if (share > 0f)
        {
            DrawArc(CENTER, DIET_RADIUS, Mathf.Lerp(start, end, share), start, ARC_POINTS / 4, AIR_COLOR, RING_WIDTH, true);
        }
    }

    /// <summary>Two strands winding around the middle with rungs between them, like the DNA of the protocell.</summary>
    private void DrawHelix()
    {
        const int   STEPS  = 24;
        const float TURNS  = 1.25f;
        Color   color  = _icon.Disabled ? EDITOR_GREY : Colors.White;
        Vector2 center = Vector2.One * ICON_SIZE * 0.5f;
        float   height = ICON_SIZE * 0.52f;
        float   width  = ICON_SIZE * 0.2f;
        var     left   = new Vector2[STEPS + 1];
        var     right  = new Vector2[STEPS + 1];
        for (int i = 0; i <= STEPS; i++)
        {
            float t     = (float)i / STEPS;
            float phase = t * TURNS * Mathf.Tau;
            float y     = center.Y + (t - 0.5f) * height;
            left[i]  = new Vector2(center.X + Mathf.Sin(phase) * width, y);
            right[i] = new Vector2(center.X - Mathf.Sin(phase) * width, y);
            if (0 == i % 4 && i > 0 && i < STEPS)
            {
                _icon.DrawLine(left[i], right[i], new Color(color, 0.6f), 2f, true);
            }
        }
        _icon.DrawPolyline(left, color, 3f, true);
        _icon.DrawPolyline(right, color, 3f, true);
    }

    private static Vector2 OnCircle(float angle, float radius)
    {
        return CENTER + Vector2.FromAngle(angle) * radius;
    }
}
