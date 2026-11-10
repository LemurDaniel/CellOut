// NOTE:
//   The way into the game: the title menu over the paused soup, the RNA prelude
//   (Prelude/RnaWorld.cs), the intro that builds the protocell, then the birth. Pauses
//   the tree and hides the player while it runs, Main hides the HUD and reacts to
//   Finished (HUD, populating, start banner).
//
//   var startFlow = new StartFlow();
//   startFlow.Setup(session, followCamera);
//   startFlow.LanguagePressed += Hud.ToggleLanguage;
//   startFlow.Finished        += OnStartFinished;
//   AddChild(startFlow);
//   startFlow.ShowMenu();
//   startFlow.Start(isInstant: true);   // e.g. in the smoke test

//
//   Chapters:
//     SETTINGS         The session, the camera and the signals.
//     MENU AND INTRO   Title menu over the paused soup, the RNA prelude, then the birth of the protocell.
//

using System.Linq;
using Godot;

namespace CellOut;

public partial class StartFlow : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   LanguagePressed   The language button of the title menu, Main switches the language.
    //   Finished          The protocell is born, the game runs: Main shows the HUD and fills the soup.
    //   Setup             The session and the camera, before AddChild.
    //

    [Signal] public delegate void LanguagePressedEventHandler();
    [Signal] public delegate void FinishedEventHandler();

    private WorldSession _session;
    private FollowCamera _camera;

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) The player that is hidden, placed and born.</param>
    /// <param name="camera">(Required) The camera the menu, the prelude and the intro move.</param>
    public void Setup(WorldSession session, FollowCamera camera)
    {
        _session = session;
        _camera  = camera;
    }

    // ===========================================================================
    // ===========================   MENU AND INTRO   ============================
    // ===========================================================================
    //
    //   1. Menu      Pauses the soup, hides the player, camera high above the spawn.
    //   2. Start     Fades the menu, then the RNA prelude (also after a retry).
    //   3. Prelude   The RNA strand swims until it reaches a lipid vesicle (Prelude/RnaWorld.cs).
    //   4. Intro     Flies the camera to the gameplay pose while the intro builds the protocell.
    //   5. Birth     Shows the player, resumes the game and reports Finished.
    //

    /// <summary>Whether the title menu or the intro still runs, the game is paused meanwhile.</summary>
    public bool IsInMenu { get; private set; }

    private TitleMenu _menu;
    private bool _isIntroRunning;

    // ---------------------------------------------------------------------------
    // Menu
    //   Pauses the soup, hides the player, camera high above the spawn.
    // ---------------------------------------------------------------------------

    /// <summary>Shows the title menu over the paused soup.</summary>
    public void ShowMenu()
    {
        IsInMenu         = true;
        GetTree().Paused = true;
        Player.IsHidden  = true;
        _camera.ShowMenuPose();

        _menu = new TitleMenu();
        AddChild(_menu);
        _menu.StartPressed    += () => Start();
        _menu.LanguagePressed += () => EmitSignal(SignalName.LanguagePressed);
        Log.Verbose("StartFlow", "Title menu shown, game paused.");
    }

    // ---------------------------------------------------------------------------
    // Start
    //   Fades the menu, then the RNA prelude (also after a retry).
    // ---------------------------------------------------------------------------

    /// <summary>Leaves the title menu: plays the RNA prelude and the intro, or skips them.</summary>
    /// <param name="isInstant">(Optional) Skips menu fade, prelude and intro, e.g. in the smoke test. Defaults to false.</param>
    /// <param name="isPreludeSkipped">(Optional) Goes straight to the intro, e.g. for the cutscene reel. Defaults to false.</param>
    /// <example><code>startFlow.Start(isInstant: true);</code></example>
    public void Start(bool isInstant = false, bool isPreludeSkipped = false)
    {
        if (!IsInMenu || _isIntroRunning)
        {
            return;
        }
        if (isInstant)
        {
            if (IsInstanceValid(_menu))
            {
                _menu.QueueFree();
            }
            _camera.Advance(0f);
            OnIntroFinished();
            return;
        }

        // The menu fades itself out, also when the start did not come from its button
        _isIntroRunning = true;
        if (IsInstanceValid(_menu))
        {
            _menu.Start();
        }
        if (isPreludeSkipped)
        {
            StartIntro();
            return;
        }
        StartPrelude();
    }

    // ---------------------------------------------------------------------------
    // Prelude
    //   The RNA strand swims until it reaches a lipid vesicle (Prelude/RnaWorld.cs).
    // ---------------------------------------------------------------------------

    private void StartPrelude()
    {
        float size    = Player.BodySize();
        var   prelude = new RnaWorld { Camera = _camera.Camera, Center = Player.GlobalPosition, Size = size };
        AddChild(prelude);
        prelude.CameraMoved += (focus, distance) => _camera.FollowBackdrop(focus, size, distance);

        // The protocell is born where the strand found its vesicle and keeps its mutations (traits, EP, tint)
        prelude.Finished += (vesicle, mutations) =>
        {
            prelude.QueueFree();
            Player.GlobalPosition = vesicle;
            Player.ApplyMutations(mutations.Select(kind => (MutationKind)kind).ToList());
            StartIntro();
        };
        Log.Verbose("StartFlow", "RNA prelude started.");
    }

    // ---------------------------------------------------------------------------
    // Intro
    //   Flies the camera to the gameplay pose while the intro builds the protocell.
    // ---------------------------------------------------------------------------

    private void StartIntro()
    {
        // The gameplay pose comes from the follow camera, the camera itself stays in its current pose for the flight
        Transform3D gamePose = _camera.PeekGamePose();

        var intro = new Intro
        {
            Camera    = _camera.Camera,
            CameraEnd = gamePose,
            Center    = Plane3D.To3D(Player.GlobalPosition),
            Size      = Player.BodySize(),
            BodyColor = Player.BodyColor,
        };
        AddChild(intro);

        // The sea floor follows the flight, else its edges show while the camera is still high up
        Vector2 spawn = Player.GlobalPosition;
        float   size  = Player.BodySize();
        intro.CameraMoved += distance => _camera.FollowBackdrop(spawn, size, distance);
        _camera.FollowBackdrop(spawn, size, _camera.Camera.GlobalTransform.Origin.DistanceTo(Plane3D.To3D(spawn)));
        intro.Finished += () =>
        {
            intro.QueueFree();
            OnIntroFinished();
        };
    }

    // ---------------------------------------------------------------------------
    // Birth
    //   Shows the player, resumes the game and reports Finished.
    // ---------------------------------------------------------------------------

    private void OnIntroFinished()
    {
        IsInMenu         = false;
        _isIntroRunning  = false;
        Player.IsHidden  = false;
        GetTree().Paused = false;
        EmitSignal(SignalName.Finished);
    }
}
