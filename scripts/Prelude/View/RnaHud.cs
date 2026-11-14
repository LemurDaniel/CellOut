// NOTE:
//   The HUD of the RNA prelude (Prelude/RnaWorld.cs): the banner in the upper middle that
//   fades out, the status top left with the goal, the wanted base with the next ones
//   and one line per mutation, the warning of an error catastrophe and the evolve button
//   bottom center once the goal is reached.
//   A view: it only shows what Refresh hands in and reports the button through a signal,
//   every text goes through Tr() (locale/translations.csv). Builds itself when it enters the tree.
//
//   var hud = new RnaHud();
//   AddChild(hud);
//   hud.EvolvePressed += () => prelude.Evolve();
//   hud.Refresh(new RnaHud.State(paired, goal, isGoalReached, isEvolving, wanted, upcoming, mutations));
//   hud.ShowErrorCatastrophe();        // too many mutations broke the strand
//   hud.FadeOut();                     // the strand evolves, the cutscene takes the screen

//
//   Chapters:
//     BUILD     Banner, status labels and the evolve button.
//     REFRESH   Shows the state of the player chain, fades out for the cutscene.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class RnaHud : CanvasLayer
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   EvolvePressed       The evolve button was pressed.
    //   UPCOMING_SHOWN      Template bases shown after the wanted one.
    //   _Ready              Banner, goal, the wanted base, mutations, the warning and the evolve button.
    //   BuildEvolveButton   Pill button bottom center like the stage buttons of the game HUD, hidden until offered.
    //   NewLabel            HUD label with outline, translated by hand.
    //

    [Signal] public delegate void EvolvePressedEventHandler();

    public const int UPCOMING_SHOWN = 2;            // template bases shown after the wanted one
    private const float BANNER_HOLD = 3.5f;
    private const float BANNER_FADE = 1.2f;
    private const float FADE_OUT = 0.4f;
    private const float WARNING_HOLD = 1.6f;
    private const float WARNING_FADE = 0.8f;
    private static readonly Color WARNING_COLOR = new(1f, 0.45f, 0.4f);
    private static readonly Color EVOLVE_ACCENT = new(0.95f, 0.65f, 0.45f);    // like the organism button of the game HUD

    private Label _banner;
    private Label _bannerSub;
    private Label _goalLabel;
    private Label _wantedLabel;
    private Label _wantedBase;
    private readonly Label[] _upcoming = new Label[UPCOMING_SHOWN];
    private Label _mutationLabel;
    private VBoxContainer _mutationList;
    private Button _evolveButton;
    private Label _warning;
    private Tween _warningFade;
    private Control _root;

    public override void _Ready()
    {
        Layer = 10;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // Banner in the upper middle, fades out after BANNER_HOLD
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        box.OffsetTop = 110;
        _root.AddChild(box);
        _banner    = NewLabel(64);
        GameFonts.UseDisplay(_banner);
        _bannerSub = NewLabel(20);
        foreach (Label label in new[] { _banner, _bannerSub })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(label);
        }
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        fade.TweenProperty(_banner, "modulate:a", 0f, BANNER_FADE).SetDelay(BANNER_HOLD);
        fade.TweenProperty(_bannerSub, "modulate:a", 0f, BANNER_FADE).SetDelay(BANNER_HOLD);

        // Status top left: goal, wanted base with the next ones, mutations
        var corner = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        corner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        corner.AddThemeConstantOverride("margin_left", 24);
        corner.AddThemeConstantOverride("margin_top", 20);
        _root.AddChild(corner);
        var status = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        corner.AddChild(status);
        _goalLabel = NewLabel(22);
        status.AddChild(_goalLabel);
        var wantedRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        status.AddChild(wantedRow);
        _wantedLabel = NewLabel(20);
        _wantedBase  = NewLabel(30);
        wantedRow.AddChild(_wantedLabel);
        wantedRow.AddChild(_wantedBase);
        for (int i = 0; i < _upcoming.Length; i++)
        {
            _upcoming[i] = NewLabel(20);
            wantedRow.AddChild(_upcoming[i]);
        }
        _mutationLabel = NewLabel(18);
        status.AddChild(_mutationLabel);
        _mutationList = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _mutationList.AddThemeConstantOverride("separation", 0);
        status.AddChild(_mutationList);

        // Warning in the middle, below the banner, hidden until a chain breaks
        _warning = NewLabel(30);
        _warning.HorizontalAlignment = HorizontalAlignment.Center;
        _warning.AddThemeColorOverride("font_color", WARNING_COLOR);
        _warning.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _warning.GrowHorizontal = Control.GrowDirection.Both;
        _warning.OffsetTop      = 260;
        _warning.Modulate       = new Color(1f, 1f, 1f, 0f);
        _root.AddChild(_warning);
        BuildEvolveButton();
    }

    private void BuildEvolveButton()
    {
        var bottom = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bottom.AddThemeConstantOverride("margin_bottom", 40);
        _root.AddChild(bottom);

        // No focus: Space evolves through RnaWorld, a focused button would fire twice
        _evolveButton = new Button
        {
            CustomMinimumSize   = new Vector2(280, 54),
            FocusMode           = Control.FocusModeEnum.None,
            AutoTranslateMode   = AutoTranslateModeEnum.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkEnd,
            Visible             = false,
        };
        _evolveButton.AddThemeFontSizeOverride("font_size", 20);
        EditorStyle.ApplyButton(_evolveButton, EVOLVE_ACCENT);
        _evolveButton.Pressed += () => EmitSignal(SignalName.EvolvePressed);
        bottom.AddChild(_evolveButton);
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   State                  What the HUD shows: the goal, the wanted bases, the mutations and whether it evolves.
    //   Refresh                Writes every text new, also after a language change, offers the evolve button.
    //   ShowErrorCatastrophe   Flashes the warning: too many mutations broke the strand.
//   FadeOut                Fades the whole HUD out when the strand evolves.
    //

    /// <summary>The state of the player chain the HUD shows.</summary>
    /// <param name="PairedCount">(Required) Links that paired correctly.</param>
    /// <param name="GoalLinks">(Required) Paired links the goal needs.</param>
    /// <param name="IsGoalReached">(Required) Enough paired links, the evolve button is offered.</param>
    /// <param name="IsEvolving">(Required) The strand already evolves, the button is gone.</param>
    /// <param name="Wanted">(Required) The base the next link needs.</param>
    /// <param name="Upcoming">(Required) The bases wanted after it, at most UPCOMING_SHOWN are shown.</param>
    /// <param name="Mutations">(Required) The traits of the mutation links on the chain.</param>
    public readonly record struct State(int PairedCount, int GoalLinks, bool IsGoalReached, bool IsEvolving, RnaBase Wanted, IReadOnlyList<RnaBase> Upcoming, IReadOnlyList<MutationKind> Mutations);

    /// <summary>Shows a state, every text freshly translated.</summary>
    /// <param name="state">(Required) The state of the player chain.</param>
    public void Refresh(State state)
    {
        _banner.Text    = Tr("BANNER_START_TITLE");
        _bannerSub.Text = Tr("PRELUDE_TEXT");

        // The wanted base big in its color, the next ones small and dim behind it
        _goalLabel.Text     = state.IsGoalReached ? Tr("PRELUDE_GOAL_EVOLVE") : string.Format(Tr("PRELUDE_GOAL"), state.PairedCount, state.GoalLinks);
        _evolveButton.Text    = Tr("ORGANISM_BUTTON");
        _evolveButton.Visible = state.IsGoalReached && !state.IsEvolving;
        _wantedLabel.Text   = Tr("PRELUDE_WANTED");
        _wantedBase.Text    = state.Wanted.ToString();
        _wantedBase.AddThemeColorOverride("font_color", RnaPalette.ColorOf(state.Wanted));
        for (int i = 0; i < _upcoming.Length && i < state.Upcoming.Count; i++)
        {
            RnaBase next = state.Upcoming[i];
            _upcoming[i].Text = next.ToString();
            _upcoming[i].AddThemeColorOverride("font_color", new Color(RnaPalette.ColorOf(next), 0.55f - 0.15f * i));
        }
        int mutationCount = state.Mutations.Count;
        _mutationLabel.Text    = string.Format(Tr("PRELUDE_MUTATIONS"), mutationCount, Mutation.EvolutionPoints(state.Mutations));
        _mutationLabel.Visible = mutationCount > 0;

        // One line per mutation on the chain: name and effect, green helps, red hurts
        foreach (Node line in _mutationList.GetChildren())
        {
            line.QueueFree();
        }
        foreach (MutationKind kind in state.Mutations)
        {
            Mutation mutation = Mutation.Get(kind);
            Label    line     = NewLabel(16);
            line.Text = $"{Tr(mutation.NameKey)}  {Tr(mutation.DescKey)}";
            line.AddThemeColorOverride("font_color", mutation.IsGood ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.5f, 0.5f));
            _mutationList.AddChild(line);
        }
    }

    /// <summary>Flashes the warning of an error catastrophe, a new one restarts the fade.</summary>
    public void ShowErrorCatastrophe()
    {
        _warning.Text = Tr("PRELUDE_ERROR_CATASTROPHE");
        _warningFade?.Kill();
        _warning.Modulate = Colors.White;
        _warningFade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _warningFade.TweenProperty(_warning, "modulate:a", 0f, WARNING_FADE).SetDelay(WARNING_HOLD);
    }

    /// <summary>Fades the whole HUD out, the cutscene of the evolving strand takes the screen.</summary>
    public void FadeOut()
    {
        _evolveButton.Visible = false;
        CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).TweenProperty(_root, "modulate:a", 0f, FADE_OUT);
    }
}
