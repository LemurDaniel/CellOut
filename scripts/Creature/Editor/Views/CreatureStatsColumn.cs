// NOTE:
//   Stats column of the creature editor: the traits of the draft (CreatureStats) as
//   values and bars: weight, what the legs carry, speed in the water and on land, what
//   the mouth gets out of plants and meat, and whether the wings could lift it. A view without clicks: Refresh shows the draft,
//   Follow does so only when the draft changed (handle drags skip the full refresh).
//
//   var stats = new CreatureStatsColumn();
//   stats.Setup(session);
//   frame.AddWorkbench(palette, stage, stats);
//   stats.Follow();     // every frame

//
//   Chapters:
//     BUILD COLUMN   The card with one row per trait.
//     SHOW TRAITS    Values and bars of the draft.
//

using Godot;

namespace CellOut;

public partial class CreatureStatsColumn : PanelContainer
{
    // ===========================================================================
    // ============================   BUILD COLUMN   =============================
    // ===========================================================================
    //
    //   Setup        The column of trait rows on the right.
    //   NewStatRow   Name, value and a bar for one trait.
    //

    private const float STATS_WIDTH = 290f;

    private sealed record StatRow(string Key, Label Name, Label Value, ColorRect Fill);

    private CreatureEditorSession _session;
    private Label _statsTitle;
    private StatRow _weightRow;
    private StatRow _carryRow;
    private StatRow _swimRow;
    private StatRow _landRow;
    private StatRow _agilityRow;
    private StatRow _healthRow;
    private StatRow _rangeRow;
    private StatRow _plantRow;
    private StatRow _meatRow;
    private StatRow _epRow;
    private StatRow _flightRow;

    /// <summary>Builds the column, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting whose draft is measured.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session          = session;
        CustomMinimumSize = new Vector2(STATS_WIDTH, 0f);
        MouseFilter       = MouseFilterEnum.Ignore;
        StyleBoxFlat style = EditorStyle.Pill(new Color(0.04f, 0.09f, 0.13f, 0.82f), new Color(EditorStyle.RIM_COLOR, 0.45f));
        style.SetContentMarginAll(16);
        AddThemeStyleboxOverride("panel", style);

        var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 10);
        AddChild(rows);
        _statsTitle = CreatureEditorUi.NewLabel(24);
        GameFonts.UseDisplay(_statsTitle);
        rows.AddChild(_statsTitle);

        // Weight first, it decides walking now and flying later
        _weightRow  = NewStatRow(rows, "CREATURE_STAT_WEIGHT");
        _carryRow   = NewStatRow(rows, "CREATURE_STAT_CARRY");
        _landRow    = NewStatRow(rows, "CREATURE_STAT_LAND");
        _flightRow  = NewStatRow(rows, "CREATURE_STAT_FLIGHT");
        rows.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore });
        _swimRow    = NewStatRow(rows, "CREATURE_STAT_SWIM");
        _agilityRow = NewStatRow(rows, "CREATURE_STAT_TURN");
        _healthRow  = NewStatRow(rows, "CREATURE_STAT_HEALTH");
        _rangeRow   = NewStatRow(rows, "CREATURE_STAT_RANGE");
        _plantRow   = NewStatRow(rows, "CREATURE_STAT_PLANT");
        _meatRow    = NewStatRow(rows, "CREATURE_STAT_MEAT");
        _epRow      = NewStatRow(rows, "CREATURE_STAT_EP");
    }

    /// <summary>Name left, value right, a thin bar below that Refresh fills.</summary>
    /// <param name="parent">(Required) The stats column.</param>
    /// <param name="key">(Required) Translation key of the trait name.</param>
    private static StatRow NewStatRow(Container parent, string key)
    {
        var row = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 3);
        parent.AddChild(row);

        var   line  = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        Label name  = CreatureEditorUi.NewLabel(15);
        Label value = CreatureEditorUi.NewLabel(15);
        name.SizeFlagsHorizontal  = SizeFlags.ExpandFill;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        line.AddChild(name);
        line.AddChild(value);
        row.AddChild(line);

        // Bar: dark track, the fill grows from the left via its right anchor
        var track = new ColorRect { Color = new Color(1f, 1f, 1f, 0.08f), CustomMinimumSize = new Vector2(0f, 7f), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(track);
        var fill = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
        fill.SetAnchorsPreset(LayoutPreset.FullRect);
        fill.AnchorRight = 0f;
        track.AddChild(fill);
        return new StatRow(key, name, value, fill);
    }

    // ===========================================================================
    // =============================   SHOW TRAITS   =============================
    // ===========================================================================
    //
    //   Follow    Refreshes when the draft changed since the last refresh.
    //   Refresh   Traits of the draft (CreatureStats) as values and bars.
    //   SetStat   Name, value, fill and color of one row.
    //

    /*
      Bars are filled up to a share of a generous maximum, so a normal body sits in the
      middle and growth stays visible. Weight and carry share one scale (MAX_WEIGHT_BAR).
      The carry bar turns orange when the legs carry less than the body weighs, the
      flight bar fills towards lift = weight and turns green there.
    */
    private const float MAX_WEIGHT_BAR = 30f;
    private const float MAX_SPEED_BAR = 2f;
    private const float MAX_LAND_BAR = 1.2f;
    private const float MAX_AGILITY_BAR = 3f;
    private const float MAX_HEALTH_BAR = 2.5f;
    private const float MAX_RANGE_BAR = 2.5f;
    private const float MAX_DIGEST_BAR = 2f;
    private const int MAX_EP_BAR = 4;
    private static readonly Color GOOD_COLOR = new(0.45f, 0.92f, 0.6f);
    private static readonly Color WARN_COLOR = new(1f, 0.68f, 0.3f);

    private int _statsVersion = -1;

    /// <summary>Stats follow every change, also the handle drags that skip the full refresh. Called every frame.</summary>
    public void Follow()
    {
        if (_session.Draft.Version != _statsVersion)
        {
            Refresh();
        }
    }

    /// <summary>Measures the draft and fills every row, nothing during the type choice.</summary>
    public void Refresh()
    {
        CreatureDesign draft = _session.Draft;
        _statsVersion = draft.Version;
        if (_session.IsChoosingType)
        {
            return;
        }
        CreatureStats stats  = CreatureStats.From(draft);
        Color         accent = CreatureEditorUi.TypeAccent(draft.Type);
        string Kg(float kg) => string.Format(Tr("CREATURE_UNIT_KG"), kg.ToString("0.0"));
        string Percent(float share) => string.Format(Tr("CREATURE_UNIT_PERCENT"), Mathf.RoundToInt(share * 100f));

        // Weight and what the legs carry
        _statsTitle.Text = Tr("CREATURE_STATS_TITLE");
        SetStat(_weightRow, Kg(stats.Weight), stats.Weight / MAX_WEIGHT_BAR, accent);
        bool isCarried = stats.Carry >= stats.Weight;
        SetStat(_carryRow, stats.Carry > 0f ? Kg(stats.Carry) : Tr("CREATURE_CARRY_NONE"), stats.Carry / MAX_WEIGHT_BAR, isCarried ? GOOD_COLOR : WARN_COLOR);

        // Movement and body
        float range = (Balance.Current.Player.MagnetRange + stats.MagnetBonus) / Balance.Current.Player.MagnetRange;
        int   ep    = Balance.Current.Player.EpPerLevel + stats.EpPerLevelBonus;
        SetStat(_swimRow, Percent(stats.SwimSpeed), stats.SwimSpeed / MAX_SPEED_BAR, accent);
        SetStat(_landRow, Percent(stats.LandSpeed), stats.LandSpeed / MAX_LAND_BAR, stats.Carry > 0f && isCarried ? GOOD_COLOR : WARN_COLOR);
        SetStat(_agilityRow, Percent(stats.Acceleration), stats.Acceleration / MAX_AGILITY_BAR, accent);
        SetStat(_healthRow, Percent(stats.Health), stats.Health / MAX_HEALTH_BAR, accent);
        SetStat(_rangeRow, Percent(range), range / MAX_RANGE_BAR, accent);
        SetStat(_plantRow, Percent(stats.DigestPlant), stats.DigestPlant / MAX_DIGEST_BAR, accent);
        SetStat(_meatRow, Percent(stats.DigestMeat), stats.DigestMeat / MAX_DIGEST_BAR, accent);
        SetStat(_epRow, ep.ToString(), (float)ep / MAX_EP_BAR, accent);

        // Flight: not possible yet, the bar shows how close the wings come
        string flight = stats.Lift <= 0f
            ? Tr("CREATURE_FLIGHT_NONE")
            : stats.CanFly ? Tr("CREATURE_FLIGHT_READY") : string.Format(Tr("CREATURE_FLIGHT_HEAVY"), Mathf.RoundToInt(stats.FlightRatio * 100f));
        SetStat(_flightRow, flight, stats.FlightRatio, stats.CanFly ? GOOD_COLOR : WARN_COLOR);
    }

    private void SetStat(StatRow row, string value, float share, Color color)
    {
        row.Name.Text        = Tr(row.Key);
        row.Value.Text       = value;
        row.Fill.AnchorRight = Mathf.Clamp(share, 0f, 1f);
        row.Fill.Color       = color;
    }
}
