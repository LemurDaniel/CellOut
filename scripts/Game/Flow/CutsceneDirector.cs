// NOTE:
//   Stages the cutscenes of the running game (Cutscenes/) and knows which one plays: the
//   stage cutscene, the transition into an editor and the evolve cutscene. It only puts
//   them on stage and reports their end. Whether one may play, the pause, the HUD and
//   what follows stay with Main.
//
//   var cutscenes = new CutsceneDirector();
//   cutscenes.Setup(session, followCamera.Camera);
//   cutscenes.StageFinished  += OnStageCutsceneFinished;
//   cutscenes.EditorCovered  += OpenEditorNow;
//   cutscenes.EvolveFinished += OnEvolveCutsceneFinished;
//   AddChild(cutscenes);
//   if (!cutscenes.IsShortPlaying) cutscenes.PlayStage(stage, "BANNER_SINGLE_TITLE", "BANNER_SINGLE_TEXT");

//
//   Chapters:
//     SETTINGS               The session, the camera and the signals.
//     PLAY SHORT CUTSCENES   The stage cutscene and the transition into an editor.
//     PLAY EVOLVE CUTSCENE   From 64 cells to the clay blob, it stays until the first shaping is done.
//

using Godot;

namespace CellOut;

public partial class CutsceneDirector : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   StageFinished    The stage cutscene is over, Main resumes and shows the banner with these keys.
    //   EditorCovered    The editor transition covers the screen, Main opens the editor now.
    //   EvolveFinished   The clay blob settled, Main opens the creature editor.
    //   Setup            The session and the world camera, before AddChild.
    //

    [Signal] public delegate void StageFinishedEventHandler(string titleKey, string textKey);
    [Signal] public delegate void EditorCoveredEventHandler();
    [Signal] public delegate void EvolveFinishedEventHandler();

    private WorldSession _session;
    private Camera3D _camera;

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) The player the cutscenes play around.</param>
    /// <param name="camera">(Required) The world camera the cutscenes move.</param>
    public void Setup(WorldSession session, Camera3D camera)
    {
        _session = session;
        _camera  = camera;
    }

    // ===========================================================================
    // ========================   PLAY SHORT CUTSCENES   =========================
    // ===========================================================================
    //
    //   IsShortPlaying         Whether a stage cutscene or an editor transition runs.
    //   IsStagePlaying         Whether it is the stage cutscene.
    //   PlayStage              Shows the stage, reports StageFinished when it is done.
    //   PlayEditorTransition   Dive into the body, reports EditorCovered when the screen is covered.
    //   PlayerCenter           The rendered center of the player body in 3D.
    //
    //   Only one short cutscene runs at a time. A stage advance during one gets its banner only.
    //

    /// <summary>Whether a stage cutscene or an editor transition runs.</summary>
    public bool IsShortPlaying => null != _shortCutscene;

    /// <summary>Whether the stage cutscene runs, an editor that wants to open waits for its end.</summary>
    public bool IsStagePlaying => _shortCutscene is StageCutscene;

    private Node3D _shortCutscene;

    /// <summary>Celebrates a stage advance. The game is already paused, the HUD hidden.</summary>
    /// <param name="stage">(Required) The stage just reached.</param>
    /// <param name="titleKey">(Required) Banner title key in locale/translations.csv, handed back with StageFinished.</param>
    /// <param name="textKey">(Required) Banner text key, handed back with StageFinished.</param>
    public void PlayStage(int stage, string titleKey, string textKey)
    {
        var cutscene = new StageCutscene { Camera = _camera, Center = PlayerCenter(), Size = Player.BodySize(), Stage = stage };
        _shortCutscene = cutscene;
        AddChild(cutscene);
        cutscene.Finished += () =>
        {
            cutscene.QueueFree();
            _shortCutscene = null;
            EmitSignal(SignalName.StageFinished, titleKey, textKey);
        };
    }

    /// <summary>Dives into the body, the editor opens when the screen is covered. The game is already paused.</summary>
    public void PlayEditorTransition()
    {
        var transition = new EditorTransition { Camera = _camera, Center = PlayerCenter(), Size = Player.BodySize(), Tint = Player.BodyColor };
        _shortCutscene = transition;
        AddChild(transition);

        // The transition fades its flash out over the editor and frees itself
        transition.Finished += () =>
        {
            _shortCutscene = null;
            EmitSignal(SignalName.EditorCovered);
        };
    }

    /// <summary>The creature visual of an organism (lifted on land, dropped on the planet), else the cell on the plane.</summary>
    private Vector3 PlayerCenter()
    {
        return Player.IsOrganism && null != Player.CreatureVisual ? Player.CreatureVisual.GlobalPosition : Plane3D.To3D(Player.GlobalPosition);
    }

    // ===========================================================================
    // ========================   PLAY EVOLVE CUTSCENE   =========================
    // ===========================================================================
    //
    //   HasEvolveCutscene   Whether the evolve cutscene is still on stage.
    //   PlayEvolve          Plays the evolve cutscene, reports EvolveFinished when the blob settled.
    //   EndEvolve           Removes the cutscene once the first shaping is done.
    //

    /// <summary>Whether the evolve cutscene is still on stage, it stays behind the creature editor.</summary>
    public bool HasEvolveCutscene => IsInstanceValid(_evolveCutscene);

    private EvolveCutscene _evolveCutscene;

    /// <summary>Plays the cutscene from 64 cells to the clay blob. The game is already paused, player and HUD hidden.</summary>
    public void PlayEvolve()
    {
        _evolveCutscene = new EvolveCutscene
        {
            Camera    = _camera,
            Center    = Plane3D.To3D(Player.GlobalPosition),
            Size      = Player.BodySize(),
            BodyColor = Player.BodyColor,
        };
        AddChild(_evolveCutscene);
        _evolveCutscene.Finished += () => EmitSignal(SignalName.EvolveFinished);
    }

    /// <summary>Removes the evolve cutscene, also fine when none played (instant evolution).</summary>
    public void EndEvolve()
    {
        if (IsInstanceValid(_evolveCutscene))
        {
            _evolveCutscene.QueueFree();
        }
        _evolveCutscene = null;
    }
}
