// NOTE:
//   The small card of the galaxy menu that tells what lies under the mouse (Menu/GalaxyView.cs):
//   for a star system its name (Core/StarNames.cs), catalog key and star kind, its planets as
//   a row of dots from the star outwards, what they are, how far it is from the current
//   planet in light years and whether life lives there (the playable ones, with the furthest
//   phase reached) or it waits for the space phase. For Sgr A* what the black hole is. Sits
//   beside the point it describes, flips left at the right edge. Ignores the mouse.
//
//   var card = new SystemCard();
//   AddChild(card);
//   card.ShowSystem(system, screenSpot);
//   card.ShowHole(screenSpot);
//   card.HideCard();

//
//   Chapters:
//     SHOW    A system, Sgr A* or nothing; placing the card beside its point.
//     BUILD   Panel, labels and the row of planets.
//

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace CellOut;

public partial class SystemCard : PanelContainer
{
    // ===========================================================================
    // =================================   SHOW   ================================
    // ===========================================================================
    //
    //   ShowSystem   Fills the card with a star system and shows it beside its point.
    //   ShowHole     The card of Sgr A*.
    //   HideCard     Fades the card out.
    //   PlaceAt      Beside a screen point, flipped left at the right edge, kept on the screen.
    //   Census       "2× Gas giant · 1× Lava world" for the planets of a system.
    //   Number       A whole number with the thousands mark of the current language.
    //

    private const float GAP = 24f;                   // pixels between the point and the card
    private const float FADE_SECONDS = 0.12f;
    private static readonly Color LIFE_COLOR = new(0.55f, 0.95f, 0.6f);
    private static readonly Color WILD_COLOR = new(0.75f, 0.72f, 1f);
    private static readonly Color HOLE_COLOR = new(1f, 0.7f, 0.4f);

    private StarSystem _system;
    private Tween _fade;
    private float _goal;                             // alpha the card fades to, so a fade runs once per change

    /// <summary>Fills the card with a star system and shows it beside its point.</summary>
    /// <param name="system">(Required) The system under the mouse.</param>
    /// <param name="at">(Required) Its star on the screen, in pixels of the parent.</param>
    public void ShowSystem(StarSystem system, Vector2 at)
    {
        if (system != _system)
        {
            _system = system;
            Fill(system);
        }
        _border.BorderColor = new Color(StarSystem.ColorOf(system.Star), 0.7f);
        PlaceAt(at);
        FadeTo(1f);
    }

    /// <summary>The card of Sgr A*, the black hole in the middle.</summary>
    /// <param name="at">(Required) The hole on the screen.</param>
    public void ShowHole(Vector2 at)
    {
        _system             = null;
        _title.Text         = "Sgr A*";
        _kind.Text          = Tr("MENU_HOLE_KIND");
        _census.Text        = Tr("MENU_HOLE_MASS");
        _distance.Text      = string.Format(Tr("MENU_SYSTEM_DISTANCE"), Number(Galaxy.LightYears(Vector2.Zero, Planets.Current.GalaxySpot)), Tr($"PLANET_{Planets.Current.Key}"));
        _status.Text        = Tr("MENU_HOLE_NOTE");
        _status.Modulate    = HOLE_COLOR;
        _row.Visible        = false;
        _border.BorderColor = new Color(HOLE_COLOR, 0.7f);
        PlaceAt(at);
        FadeTo(1f);
    }

    /// <summary>Fades the card out, the next show fills it again.</summary>
    public void HideCard()
    {
        _system = null;
        FadeTo(0f);
    }

    private void Fill(StarSystem system)
    {
        Planet planet = null == system.PlanetKey ? null : Planets.Get(system.PlanetKey);
        _title.Text   = null == planet ? StarNames.Of(system.Key) : Tr($"PLANET_{planet.Key}");
        _kind.Text    = $"{(null == planet ? system.Key : StarNames.Of(system.Key))} · {Tr($"STAR_{system.Star}".ToUpperInvariant())}";
        _census.Text  = Census(system);
        _row.Visible  = true;
        _row.QueueRedraw();

        // Distance from the current planet, its own system is home
        bool isHome = planet?.Key == Planets.Current.Key;
        _distance.Text = isHome
            ? Tr("MENU_SYSTEM_HOME")
            : string.Format(Tr("MENU_SYSTEM_DISTANCE"), Number(Galaxy.LightYears(system.Spot, Planets.Current.GalaxySpot)), Tr($"PLANET_{Planets.Current.Key}"));

        // Life where a game can start, the furthest phase reached there; the rest waits for space
        if (null == planet)
        {
            _status.Text     = Tr("MENU_SYSTEM_WILD");
            _status.Modulate = WILD_COLOR;
            return;
        }
        StartPhase reached = PlanetProgress.Reached(planet.Key);
        _status.Text     = StartPhase.RnaWorld == reached ? Tr("MENU_SYSTEM_LIFE_NEW") : string.Format(Tr("MENU_SYSTEM_LIFE"), Tr(StartPhases.NameKey(reached)));
        _status.Modulate = LIFE_COLOR;
    }

    private void FadeTo(float alpha)
    {
        if (Mathf.IsEqualApprox(_goal, alpha))
        {
            return;
        }
        _goal   = alpha;
        Visible = true;
        _fade?.Kill();
        _fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _fade.TweenProperty(this, "modulate:a", alpha, FADE_SECONDS);
        if (alpha <= 0f)
        {
            _fade.TweenCallback(Callable.From(Hide));
        }
    }

    private void PlaceAt(Vector2 at)
    {
        Size = GetCombinedMinimumSize();
        Vector2 room   = GetParentAreaSize();
        bool    isLeft = at.X + GAP + Size.X > room.X - 12f;
        float   x      = isLeft ? at.X - GAP - Size.X : at.X + GAP;
        float   y      = Mathf.Clamp(at.Y - Size.Y * 0.5f, 12f, Mathf.Max(12f, room.Y - Size.Y - 12f));
        Position = new Vector2(x, y);
    }

    private string Census(StarSystem system)
    {
        if (0 == system.Neighbors.Count)
        {
            return Tr("MENU_SYSTEM_EMPTY");
        }
        IEnumerable<string> kinds = system.Neighbors
            .GroupBy(planet => planet.Kind)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Count()}× {Tr($"WORLD_{group.Key}".ToUpperInvariant())}");
        return string.Join(" · ", kinds);
    }

    /// <summary>A whole number with the thousands mark of the current language (12.400 in German, 12,400 in English).</summary>
    public static string Number(int value)
    {
        string culture = TranslationServer.GetLocale().StartsWith("de") ? "de-DE" : "en-US";
        return value.ToString("N0", CultureInfo.GetCultureInfo(culture));
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready     Glass panel with a border in the star's color, the labels below each other.
    //   DrawRow    The planets from the star outwards, the playable one with a ring around it.
    //   NewLabel   A label with outline.
    //

    private const float ROW_WIDTH = 230f;
    private const float ROW_HEIGHT = 26f;

    private StyleBoxFlat _border;
    private Label _title;
    private Label _kind;
    private Control _row;
    private Label _census;
    private Label _distance;
    private Label _status;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible     = false;
        Modulate    = new Color(1f, 1f, 1f, 0f);
        _border     = new StyleBoxFlat { BgColor = new Color(0.03f, 0.04f, 0.09f, 0.86f), AntiAliasing = true };
        _border.SetBorderWidthAll(1);
        _border.SetCornerRadiusAll(12);
        _border.SetContentMarginAll(14);
        AddThemeStyleboxOverride("panel", _border);

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 3);
        AddChild(column);
        _title = NewLabel(22);
        column.AddChild(_title);
        _kind = NewLabel(13);
        _kind.Modulate = new Color(1f, 1f, 1f, 0.7f);
        column.AddChild(_kind);
        _row = new Control { CustomMinimumSize = new Vector2(ROW_WIDTH, ROW_HEIGHT), MouseFilter = MouseFilterEnum.Ignore };
        _row.Draw += DrawRow;
        column.AddChild(_row);
        _census = NewLabel(13);
        column.AddChild(_census);
        _distance = NewLabel(13);
        _distance.Modulate = new Color(0.75f, 0.85f, 1f);
        column.AddChild(_distance);
        _status = NewLabel(14);
        column.AddChild(_status);
    }

    private void DrawRow()
    {
        if (null == _system)
        {
            return;
        }
        float mid  = ROW_HEIGHT * 0.5f;
        Color star = StarSystem.ColorOf(_system.Star);
        float step = (ROW_WIDTH - 30f) / StarSystem.ORBITS;
        _row.DrawLine(new Vector2(8f, mid), new Vector2(ROW_WIDTH, mid), new Color(star, 0.25f), 1f, true);
        _row.DrawCircle(new Vector2(6f, mid), 8f * Mathf.Sqrt(StarSystem.SizeOf(_system.Star)), star.Lerp(Colors.White, 0.4f));
        foreach (StarSystem.Neighbor planet in _system.Neighbors)
        {
            var at = new Vector2(30f + (planet.Orbit + 0.5f) * step, mid);
            _row.DrawCircle(at, 2.5f + planet.Size * 3.5f, StarSystem.DotOf(planet));
        }
        if (null != _system.PlanetKey)
        {
            Planet home = Planets.Get(_system.PlanetKey);
            var    at   = new Vector2(30f + (_system.HomeOrbit + 0.5f) * step, mid);
            _row.DrawCircle(at, 6f, home.OceanColor.Lerp(home.LandColor, 0.35f));
            _row.DrawArc(at, 9f, 0f, Mathf.Tau, 24, new Color(LIFE_COLOR, 0.9f), 1.5f, true);
        }
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }
}
