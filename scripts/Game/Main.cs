// NOTE:
//   The game world: an endless primordial soup around the player. Spawns food and
//   cells relative to the player size, resolves eating and damage, and drives the
//   camera, stage changes and mutations. Logic runs in 2D on the plane, rendering
//   in 3D (see Core/Plane3D.cs). Attached to the root of scenes/main.tscn.

//
//   Chapters:
//     BUILD WORLD          Environment, light, camera, layers, player and HUD.
//     MENU AND INTRO       Title menu over the paused soup, the RNA prelude, then the birth of the protocell.
//     FOLLOW CAMERA        Tilted chase camera with wheel zoom, third person over the islands, sky and planet.
//     RESOLVE CONTACTS     Food, player vs. cells and cells vs. cells per physics frame.
//     SPAWN                Food and cells around the view.
//     REACT TO EVENTS      Pops, the cell editor, stage changes and deaths.
//     PLAY SHORT CUTSCENES The stage cutscene and the transition into an editor.
//     EVOLVE INTO ORGANISM Cutscene from 64 cells to the clay blob, creature editor, the new sea.
//     COAST AND CHEATS     The coast of the organism stage, land checks, the F10 stage jumps.
//     HELPERS              Value scale, kill reward, view radius, splash and layers.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class Main : Node2D
{
    private const int MAX_FOOD = 140;
    private const int MAX_CRITTERS = 24;
    private const int START_FOOD = 90;
    private const int START_CRITTERS = 8;
    private const float CAM_FOV = 50f;
    private const float CAM_TILT = 0.5f;            // tilt from vertical in radians
    private const float CAM_DISTANCE = 14f;         // camera distance in player sizes
    private const float CRITTER_GROWTH_CAP = 3f;    // critters grow up to this many player sizes
    private static readonly Color BACKGROUND_COLOR = new(0.04f, 0.13f, 0.17f);
    private static readonly Color SOUP_AMBIENT = new(0.5f, 0.8f, 0.85f);
    private static readonly Color SKY_AMBIENT = new(0.78f, 0.84f, 0.92f);
    private const float SOUP_AMBIENT_ENERGY = 0.6f;
    private const float SKY_AMBIENT_ENERGY = 0.8f;

    public Player Player { get; private set; }
    public Hud Hud { get; private set; }
    public CellEditor Editor { get; private set; }
    public CreatureEditor CreatureEditor { get; private set; }
    public Node2D FoodLayer { get; private set; }
    public Node2D CritterLayer { get; private set; }

    // ===========================================================================
    // =============================   BUILD WORLD   =============================
    // ===========================================================================
    //
    //   1. Scene       Environment, sun, camera and backdrop.
    //   2. Layers      Food, critters, the player and effects in draw order.
    //   3. HUD         Binds HUD, editors and the F10 cheat menu and connects the player signals.
    //   4. Menu        The title menu over the empty soup, it fills after the start (SPAWN).
    //

    private Camera3D _camera;
    private DirectionalLight3D _sun;
    private Godot.Environment _environment;
    private Backdrop _backdrop;
    private Node2D _fxLayer;

    public override void _Ready()
    {
        GD.Randomize();

        // ---------------------------------------------------------------------------
        // Scene
        //   Environment, sun, camera and backdrop.
        // ---------------------------------------------------------------------------

        var environment = new Godot.Environment
        {
            BackgroundMode     = Godot.Environment.BGMode.Color,
            BackgroundColor    = BACKGROUND_COLOR,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor  = SOUP_AMBIENT,
            AmbientLightEnergy = SOUP_AMBIENT_ENERGY,
            GlowEnabled        = true,
            GlowIntensity      = 0.7f,
            GlowBloom          = 0.05f,
            SsaoEnabled        = true,                   // contact shadows where parts meet the body, radius in UpdateCamera
            SsaoIntensity      = 1.6f,
        };
        _environment = environment;
        AddChild(new WorldEnvironment { Environment = environment });

        _sun = new DirectionalLight3D
        {
            Basis         = Basis.LookingAt(new Vector3(0.35f, -1f, 0.45f), Vector3.Up),
            LightColor    = new Color(1f, 0.97f, 0.9f),
            LightEnergy   = 1.1f,
            ShadowEnabled = true,
            ShadowOpacity = 0.6f,
        };
        AddChild(_sun);

        _camera = new Camera3D { Fov = CAM_FOV };
        AddChild(_camera);
        _camera.MakeCurrent();

        _backdrop = new Backdrop();
        AddChild(_backdrop);

        // ---------------------------------------------------------------------------
        // Layers
        //   Food, critters, the player and effects in draw order.
        // ---------------------------------------------------------------------------

        FoodLayer    = AddLayer();
        CritterLayer = AddLayer();
        Player       = new Player();
        AddChild(Player);
        _fxLayer     = AddLayer();
        _camDistance = Player.BodySize() * CAM_DISTANCE * 0.6f;   // start slightly zoomed in
        UpdateCamera(0f);

        // ---------------------------------------------------------------------------
        // HUD
        //   Binds HUD, editors and the F10 cheat menu and connects the player signals.
        // ---------------------------------------------------------------------------

        Hud = new Hud();
        AddChild(Hud);
        Hud.Bind(Player);
        Hud.EditorRequested += () => OpenEditor();
        Editor = new CellEditor();
        AddChild(Editor);
        Editor.Closed         += OnEditorClosed;
        CreatureEditor = new CreatureEditor();
        AddChild(CreatureEditor);
        CreatureEditor.Closed  += OnCreatureEditorClosed;
        Player.EvolveRequested += () => StartEvolve();
        AddChild(new CheatMenu { Main = this });
        Player.EvolutionReady += OnEvolutionReady;
        Player.StageChanged   += OnStageChanged;
        Player.Divided        += OnPlayerDivided;
        Player.Died           += OnPlayerDied;
        Player.CellsLost      += OnPlayerLostCells;

        // ---------------------------------------------------------------------------
        // Menu
        //   The title menu over the empty soup, it fills after the start (SPAWN).
        // ---------------------------------------------------------------------------

        ShowMenu();

        // Try again after a game over: no title menu, straight into the RNA prelude
        if (_isRestartQueued)
        {
            _isRestartQueued = false;
            StartGame();
        }
    }

    // ===========================================================================
    // ===========================   MENU AND INTRO   ============================
    // ===========================================================================
    //
    //   1. Menu      Pauses the soup, hides player and HUD, camera high above the spawn.
    //   2. Start     Fades the menu, then the RNA prelude (also after a retry).
    //   3. Prelude   The RNA strand swims until it reaches a lipid vesicle (Prelude/RnaWorld.cs).
    //   4. Intro     Flies the camera to the gameplay pose while the intro builds the protocell.
    //   5. Birth     Shows player and HUD, resumes the game, the soup starts filling, start banner.
    //

    private const float MENU_CAMERA_DISTANCE = 26f;  // in player sizes
    private const float MENU_CAMERA_TILT = 0.25f;    // tilt from vertical in radians

    /// <summary>Whether the title menu or the intro still runs, the game is paused meanwhile.</summary>
    public bool IsInMenu { get; private set; }

    private TitleMenu _menu;
    private bool _isIntroRunning;

    // ---------------------------------------------------------------------------
    // Menu
    //   Pauses the soup, hides player and HUD, camera high above the spawn.
    // ---------------------------------------------------------------------------

    private void ShowMenu()
    {
        IsInMenu         = true;
        GetTree().Paused = true;
        Player.IsHidden  = true;
        Hud.Visible      = false;

        float   size  = Player.BodySize();
        Vector3 focus = Plane3D.To3D(Player.GlobalPosition);
        _camera.Position = focus + new Vector3(0f, Mathf.Cos(MENU_CAMERA_TILT), Mathf.Sin(MENU_CAMERA_TILT)) * size * MENU_CAMERA_DISTANCE;
        _camera.LookAt(focus, Vector3.Up);
        _camera.Far = size * MENU_CAMERA_DISTANCE * 3f;
        _backdrop.Follow(new Vector2(focus.X, focus.Z), size, ViewRadiusAt(size * MENU_CAMERA_DISTANCE));

        _menu = new TitleMenu();
        AddChild(_menu);
        _menu.StartPressed    += () => StartGame();
        _menu.LanguagePressed += Hud.ToggleLanguage;
        Log.Verbose("Main", "Title menu shown, game paused.");
    }

    // ---------------------------------------------------------------------------
    // Start
    //   Fades the menu, then the RNA prelude (also after a retry).
    // ---------------------------------------------------------------------------

    /// <summary>Leaves the title menu: plays the RNA prelude and the intro, or skips them.</summary>
    /// <param name="isInstant">(Optional) Skips menu fade, prelude and intro, e.g. in the smoke test. Defaults to false.</param>
    /// <param name="isPreludeSkipped">(Optional) Goes straight to the intro, e.g. for the cutscene reel. Defaults to false.</param>
    /// <example><code>main.StartGame(isInstant: true);</code></example>
    public void StartGame(bool isInstant = false, bool isPreludeSkipped = false)
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
            UpdateCamera(0f);
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
        var   prelude = new RnaWorld { Camera = _camera, Center = Player.GlobalPosition, Size = size };
        AddChild(prelude);
        prelude.CameraMoved += (focus, distance) => _backdrop.Follow(focus, size, ViewRadiusAt(distance));

        // The protocell is born where the strand found its vesicle and keeps its mutations (traits, EP, tint)
        prelude.Finished += (vesicle, mutations) =>
        {
            prelude.QueueFree();
            Player.GlobalPosition = vesicle;
            Player.ApplyMutations(mutations.Select(kind => (MutationKind)kind).ToList());
            StartIntro();
        };
        Log.Verbose("Main", "RNA prelude started.");
    }

    // ---------------------------------------------------------------------------
    // Intro
    //   Flies the camera to the gameplay pose while the intro builds the protocell.
    // ---------------------------------------------------------------------------

    private void StartIntro()
    {
        // The gameplay pose comes from UpdateCamera, then the camera returns to its current pose for the flight
        Transform3D startPose = _camera.GlobalTransform;
        float       far       = _camera.Far;
        UpdateCamera(0f);
        Transform3D gamePose = _camera.GlobalTransform;
        _camera.GlobalTransform = startPose;
        _camera.Far             = Mathf.Max(far, _camera.Far);

        var intro = new Intro
        {
            Camera    = _camera,
            CameraEnd = gamePose,
            Center    = Plane3D.To3D(Player.GlobalPosition),
            Size      = Player.BodySize(),
            BodyColor = Player.BodyColor,
        };
        AddChild(intro);

        // The sea floor follows the flight, else its edges show while the camera is still high up
        Vector2 spawn = Player.GlobalPosition;
        float   size  = Player.BodySize();
        intro.CameraMoved += distance => _backdrop.Follow(spawn, size, ViewRadiusAt(distance));
        _backdrop.Follow(spawn, size, ViewRadiusAt(startPose.Origin.DistanceTo(Plane3D.To3D(spawn))));
        intro.Finished += () =>
        {
            intro.QueueFree();
            OnIntroFinished();
        };
    }

    // ---------------------------------------------------------------------------
    // Birth
    //   Shows player and HUD, resumes the game, the soup starts filling, start banner.
    // ---------------------------------------------------------------------------

    private void OnIntroFinished()
    {
        IsInMenu         = false;
        _isIntroRunning  = false;
        Player.IsHidden  = false;
        Hud.Visible      = true;
        GetTree().Paused = false;
        StartPopulating();
        SpawnSplash(Player.GlobalPosition, Player.BodySize() * 1.4f, Player.BodyColor);
        Hud.ShowBanner("BANNER_START_TITLE", "BANNER_START_TEXT");
        Log.Verbose("Main", "Intro finished -> game running.");
    }

    // ===========================================================================
    // ============================   FOLLOW CAMERA   ============================
    // ===========================================================================
    //
    //   _UnhandledInput   Mouse wheel zooms, E opens the editor, a right drag orbits the third person camera.
    //   ZoomBy            Changes the zoom factor by steps, clamped.
    //   _Process          Updates the camera once per frame.
    //   ThirdPerson       0 top view .. 1 third person, the land animal blends over TP_BLEND_SECONDS.
    //   YawBehind         Camera yaw behind a facing.
    //   FollowFacing      Swings the third person camera behind the walking creature.
    //   UpdateCamera      Eases view, distance and focus, then moves camera, planet, sky and backdrop.
    //   UpdateSky         From the dark soup to sky, sunlight and haze over the open sea.
    //
    //   Top view: tilted CAM_TILT from vertical, screen up = -y on the plane.
    //   Third person: behind and above the land animal, swinging behind it while it walks,
    //   orbited by a right drag (yaw, tilt), the keys turn with it (Player.InputTurn), the
    //   world bends to a planet (Plane3D.SetCurve).
    //

    private const float ZOOM_STEP = 1.15f;
    public const float MIN_ZOOM = 0.5f;
    public const float MAX_ZOOM = 2.5f;
    private const float TP_BLEND_SECONDS = 2.2f;
    private const float TP_TILT = 1.2f;              // third person: tilt from vertical, low over the ground
    private const float TP_MIN_TILT = 0.6f;
    private const float TP_MAX_TILT = 1.45f;
    private const float TP_DISTANCE = 6f;            // in player sizes
    private const float TP_LOOK_UP = 0.9f;           // the camera looks this far above the body, in player sizes
    private const float TP_CLEARANCE = 0.35f;        // the camera stays this far above the ground, in player sizes
    private const float TP_FAR = 700f;               // draw distance in player sizes, the horizon is nearer
    private const float TP_VIEW_RADIUS = 28f;        // spawning and props around the player, in player sizes
    private const float ORBIT_SPEED = 0.006f;        // radians per pixel of a right drag
    private const float FOLLOW_SPEED = 2.2f;         // third person: how fast the camera swings behind the walking creature
    private const float FOLLOW_BACKWARD_LIMIT = -0.3f;   // facing this much towards the camera: no swing
    private const float TP_SEA_SINK = 0.95f;         // third person: swimmers sink this many sizes, just below the low sea (Coast.OPEN_SEA_HEIGHT)
    private const float TP_SHADOW_DISTANCE = 90f;   // third person: tall trees throw shadows this far, in player sizes
    private const float PLANET_RADIUS = 8000f;       // in Unit of the coast, the islands are ~260 Unit across

    /// <summary>Wheel zoom on top of the automatic distance, 1 = default, bigger = farther away.</summary>
    public float ZoomFactor { get; private set; } = 1f;

    /// <summary>0 top view .. 1 third person, eased with SmoothStep when used.</summary>
    public float ThirdPerson { get; private set; }

    private float _camDistance;
    private const float SSAO_RADIUS = 0.35f;         // contact shadow reach in body sizes
    private Vector3 _camFocus = Vector3.Zero;
    private float _camYaw;                           // third person: 0 = camera south of the player (+z)
    private float _camTilt = TP_TILT;
    private float _view;                             // SmoothStep of ThirdPerson, the blend everything uses
    private bool _isOrbiting;

    public override void _UnhandledInput(InputEvent @event)
    {
        // E opens the cell editor with the points collected so far
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } && OpenEditor())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        // Third person: a right drag orbits the camera, the cursor hides meanwhile
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right } orbit && (_view > 0.5f || !orbit.Pressed))
        {
            _isOrbiting     = orbit.Pressed;
            Input.MouseMode = _isOrbiting ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventMouseMotion motion && _isOrbiting)
        {
            _camYaw  = Mathf.Wrap(_camYaw - motion.Relative.X * ORBIT_SPEED, -Mathf.Pi, Mathf.Pi);
            _camTilt = Mathf.Clamp(_camTilt - motion.Relative.Y * ORBIT_SPEED, TP_MIN_TILT, TP_MAX_TILT);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton button || !button.Pressed)
        {
            return;
        }
        switch (button.ButtonIndex)
        {
            case MouseButton.WheelUp:
                ZoomBy(-1);
                break;
            case MouseButton.WheelDown:
                ZoomBy(1);
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Zooms out (positive steps) or in (negative steps), clamped to MIN_ZOOM..MAX_ZOOM.</summary>
    /// <param name="steps">(Required) Number of wheel steps, each one ZOOM_STEP.</param>
    /// <example><code>ZoomBy(-2)  // two steps closer</code></example>
    public void ZoomBy(int steps)
    {
        ZoomFactor = Mathf.Clamp(ZoomFactor * Mathf.Pow(ZOOM_STEP, steps), MIN_ZOOM, MAX_ZOOM);
        Log.Verbose("Main", $"Zoom factor {ZoomFactor:F2}.");
    }

    public override void _Process(double delta)
    {
        UpdateCamera((float)delta);
    }

    /// <summary>The camera yaw of a creature looking in a direction: the camera behind it.</summary>
    private static float YawBehind(float facing)
    {
        Vector2 behind = -Vector2.FromAngle(facing);
        return Mathf.Atan2(behind.X, behind.Y);
    }

    /*
      Third person, the camera swings behind the walking creature, faster the faster it walks:
      > Orbiting with the right mouse button: the hand of the player wins, no swing
      > Standing still: the camera stays where it is, the player may look around
      > Walking towards the camera (S): no swing, else it would whip around 180 degrees
      > Otherwise: the yaw eases towards behind the facing, A/D turn into a curve
    */
    /// <summary>Swings the third person camera behind the facing of the walking creature.</summary>
    private void FollowFacing(float dt)
    {
        if (_view < 0.5f || _isOrbiting || dt <= 0f)
        {
            return;
        }
        float speedShare = Mathf.Clamp(Player.Velocity.Length() / Mathf.Max(Player.CurrentSpeed(), 0.001f), 0f, 1f);
        if (speedShare < 0.1f)
        {
            return;
        }
        Vector2 cameraForward = -new Vector2(Mathf.Sin(_camYaw), Mathf.Cos(_camYaw));
        if (Vector2.FromAngle(Player.Facing).Dot(cameraForward) < FOLLOW_BACKWARD_LIMIT)
        {
            return;
        }
        _camYaw = Mathf.LerpAngle(_camYaw, YawBehind(Player.Facing), Mathf.Clamp(dt * FOLLOW_SPEED * speedShare, 0f, 1f));
    }

    /*
      1. View      Blends towards third person once the player is a land animal; the
                   blend starts with the camera yaw behind the creature, then swings
                   behind it while it walks (FollowFacing).
      2. Pose      Yaw and tilt from the top view pose to the orbit pose, distance and the
                   look-up point eased, the camera never dips into a hill.
      3. Planet    Curvature with the blend, flat around the camera.
      4. Scenery   Shadows, contact shadows, sky and backdrop grow along.
    */
    /// <summary>Chase camera, top view or third person, its distance relative to the player size.</summary>
    /// <param name="dt">(Required) Frame time, 0 snaps focus and view to the player.</param>
    private void UpdateCamera(float dt)
    {
        float playerSize = Player.BodySize();

        // View
        float target = Player.IsLandAnimal ? 1f : 0f;
        if (target > 0f && ThirdPerson <= 0f)
        {
            _camYaw  = YawBehind(Player.Facing);
            _camTilt = TP_TILT;
            Log.Verbose("Main", "Camera turns into the third person view.");
        }
        ThirdPerson = dt <= 0f ? target : Mathf.MoveToward(ThirdPerson, target, dt / TP_BLEND_SECONDS);
        _view       = Mathf.SmoothStep(0f, 1f, ThirdPerson);
        FollowFacing(dt);

        // Pose
        float   yaw   = Mathf.LerpAngle(0f, _camYaw, _view);
        float   tilt  = Mathf.Lerp(CAM_TILT, _camTilt, _view);
        float   sink  = Player.IsOnLand ? 0f : playerSize * TP_SEA_SINK;
        Vector3 focus = Plane3D.To3D(Player.GlobalPosition, (TP_LOOK_UP * playerSize + Player.GroundLift - sink) * _view);
        if (dt <= 0f)
        {
            _camFocus = focus;
        }
        float distance = playerSize * Mathf.Lerp(CAM_DISTANCE, TP_DISTANCE, _view) * ZoomFactor;
        _camDistance = Mathf.Lerp(_camDistance, distance, Mathf.Clamp(dt * 1.5f, 0f, 1f));
        _camFocus    = _camFocus.Lerp(focus, Mathf.Clamp(dt * 5f, 0f, 1f));

        Vector3 offset   = new Vector3(Mathf.Sin(yaw) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Cos(yaw) * Mathf.Sin(tilt)) * _camDistance;
        Vector3 position = _camFocus + offset;
        if (null != Coast && _view > 0f)
        {
            var   ground = new Vector2(position.X, position.Z);
            float floor  = (Coast.GroundHeight(ground) + TP_CLEARANCE) * playerSize;
            position.Y   = Mathf.Max(position.Y, floor);
        }
        _camera.Position = position;
        _camera.LookAt(_camFocus, Vector3.Up);
        _camera.Near     = _camDistance * 0.05f;
        _camera.Far      = Mathf.Lerp(_camDistance * 8f, playerSize * TP_FAR, _view);
        Player.InputTurn = -yaw;

        // Planet
        float unit = null == Coast ? playerSize : Coast.Unit;
        Plane3D.SetCurve(_view / (2f * PLANET_RADIUS * unit), position);
        Plane3D.SeaDrop = _view * playerSize * TP_SEA_SINK;

        // Scenery: light, contact shadows and backdrop grow along, so shadows and motes fit at every size
        _sun.DirectionalShadowMaxDistance = Mathf.Lerp(_camDistance * 3f, playerSize * TP_SHADOW_DISTANCE, _view);
        _environment.SsaoRadius           = playerSize * SSAO_RADIUS;
        UpdateSky(playerSize);
        _backdrop.IsMotesShown = _view < 0.5f;
        _backdrop.Follow(new Vector2(_camFocus.X, _camFocus.Z), playerSize, ViewRadius());
    }

    private static readonly Color SKY_TOP = new(0.24f, 0.5f, 0.86f);
    private static readonly Color SKY_HORIZON = new(0.7f, 0.84f, 0.95f);
    private static readonly Color SEA_BELOW = new(0.07f, 0.3f, 0.42f);
    private const float FOG_DENSITY = 0.0025f;       // per player size, half the light at ~280 sizes
    private ProceduralSkyMaterial _sky;

    /// <summary>Background, ambient light and haze between the soup (view 0) and the open sky (view 1).</summary>
    private void UpdateSky(float playerSize)
    {
        if (_view <= 0f)
        {
            _environment.BackgroundMode     = Godot.Environment.BGMode.Color;
            _environment.FogEnabled         = false;
            _environment.AmbientLightColor  = SOUP_AMBIENT;
            _environment.AmbientLightEnergy = SOUP_AMBIENT_ENERGY;
            return;
        }

        if (null == _sky)
        {
            _sky = new ProceduralSkyMaterial { SunAngleMax = 20f };
            _environment.Sky = new Sky { SkyMaterial = _sky };
        }
        _sky.SkyTopColor                = BACKGROUND_COLOR.Lerp(SKY_TOP, _view);
        _sky.SkyHorizonColor            = BACKGROUND_COLOR.Lerp(SKY_HORIZON, _view);
        _sky.GroundHorizonColor         = BACKGROUND_COLOR.Lerp(SKY_HORIZON, _view);
        _sky.GroundBottomColor          = BACKGROUND_COLOR.Lerp(SEA_BELOW, _view);
        _environment.BackgroundMode     = Godot.Environment.BGMode.Sky;
        _environment.FogEnabled         = true;
        _environment.FogLightColor      = SKY_HORIZON;
        _environment.FogDensity         = FOG_DENSITY * _view / playerSize;
        _environment.FogSkyAffect       = 0f;
        _environment.AmbientLightColor  = SOUP_AMBIENT.Lerp(SKY_AMBIENT, _view);
        _environment.AmbientLightEnergy = Mathf.Lerp(SOUP_AMBIENT_ENERGY, SKY_AMBIENT_ENERGY, _view);
    }

    // ===========================================================================
    // ==========================   RESOLVE CONTACTS   ===========================
    // ===========================================================================
    //
    //   1. Food               Attracts, swallows and cleans up food.
    //   2. Engulf             Pulls swallowed cells into their eaters, digests them when done.
    //   3. Player vs. cells   Swallow smaller cells, get swallowed by bigger ones, bump equal ones,
    //                         cells far away despawn.
    //   4. Cells vs. cells    Bigger cells start swallowing smaller ones, equal ones push apart.
    //   5. Refill             Fills the soup after the start, then keeps it topped up outside the view.
    //

    public override void _PhysicsProcess(double delta)
    {
        float   dt              = (float)delta;
        float   playerSize      = Player.BodySize();
        Vector2 playerPosition  = Player.GlobalPosition;
        float   despawnDistance = ViewRadius() * 2.5f;
        UpdateCoast();

        // ---------------------------------------------------------------------------
        // Food
        //   Attracts, swallows and cleans up food.
        // ---------------------------------------------------------------------------

        foreach (Node node in FoodLayer.GetChildren())
        {
            var   food     = (Food)node;
            if (null != Coast && FoodHabitat.Land == food.Habitat)
            {
                food.GroundHeight = Coast.GroundHeight(food.Position) * playerSize;
            }
            else if (null != Coast)
            {
                food.Position = Coast.ToSea(food.Position, food.Size);
            }
            float distance = playerPosition.DistanceTo(food.Position);
            if (distance > despawnDistance)
            {
                food.QueueFree();
                continue;
            }
            if (distance < playerSize * Player.MagnetRange + 10f)
            {
                food.Position = food.Position.MoveToward(playerPosition, playerSize * 3f * dt);
            }
            if (distance < playerSize * 0.9f)
            {
                Player.AddBiomass(food.Value, food.IsMeat ? BiomassSource.Meat : BiomassSource.Plant);
                SpawnSplash(food.Position, food.Size * 3f, food.ColorOf());
                food.QueueFree();
            }
        }

        // ---------------------------------------------------------------------------
        // Engulf
        //   Pulls swallowed cells into their eaters, digests them when done.
        // ---------------------------------------------------------------------------

        UpdateEngulfing(dt, playerSize);

        // ---------------------------------------------------------------------------
        // Player vs. cells
        //   Swallow smaller cells, get swallowed by bigger ones, bump equal ones, cells far away despawn.
        // ---------------------------------------------------------------------------

        float thornScale = playerSize / CellBody.REFERENCE_SIZE;
        foreach (Node node in CritterLayer.GetChildren())
        {
            var critter = (Critter)node;
            if (critter.IsQueuedForDeletion() || critter.IsEngulfed)
            {
                continue;
            }
            // Sea creatures stay in the water
            if (null != Coast)
            {
                critter.Position = Coast.ToSea(critter.Position, critter.Radius);
            }
            float distance = playerPosition.DistanceTo(critter.Position);
            if (distance > despawnDistance)
            {
                critter.QueueFree();
                continue;
            }

            // A jaw towards the cell and every division let the player swallow bigger cells
            float direction   = (critter.Position - playerPosition).Angle();
            float eatRatio    = Player.EatRatioToward(direction);
            bool  canEat      = Player.Stage >= Player.SINGLE_STAGE && playerSize > critter.Radius * eatRatio;
            bool  isDangerous = critter.CanEat(playerSize);
            float thornHit    = Player.ContactDamageToward(direction) * thornScale;
            float critterHit  = critter.ContactDamageToward(direction + Mathf.Pi);   // its spikes and jaws towards the player
            if (distance > playerSize + critter.Radius)
            {
                continue;
            }

            // Smaller: the membrane flows around it on contact, ENGULF finishes the meal
            if (canEat)
            {
                if (distance < playerSize + critter.Radius * ENGULF_CONTACT)
                {
                    // Spiky prey stings once while it is swallowed
                    critter.StartEngulf(Player);
                    Player.Digest(critterHit, critter.Position);
                    Log.Verbose("Main", $"Player engulfs a {critter.Kind} of radius {critter.Radius:F1}.");
                }
                continue;
            }

            // Bigger: its membrane flows around the player, pulls it in and digests it
            if (isDangerous)
            {
                EngulfPlayer(critter, distance, playerSize, thornHit, critterHit, dt);
                continue;
            }

            // Equal: push apart, spikes and jaws towards it hurt on bump
            Vector2 normal  = (critter.Position - playerPosition).Normalized();
            float   overlap = playerSize + critter.Radius - distance;
            critter.Position += normal * overlap * 0.5f;
            Player.Position  -= normal * overlap * 0.5f;
            if (thornHit > 0f && critter.BumpCooldown <= 0f)
            {
                critter.Hurt(thornHit, Player.GlobalPosition);
                critter.BumpCooldown = 0.5f;
            }
            if (critterHit > 0f)
            {
                Player.TakeDamage(critterHit, critter.Position);
            }
        }

        // ---------------------------------------------------------------------------
        // Cells vs. cells
        //   Bigger cells start swallowing smaller ones, equal ones push apart.
        // ---------------------------------------------------------------------------

        ResolveCritterContacts(playerSize);

        // ---------------------------------------------------------------------------
        // Refill
        //   Fills the soup after the start, then keeps it topped up outside the view.
        // ---------------------------------------------------------------------------

        if (UpdatePopulating(dt))
        {
            return;
        }
        if (FoodLayer.GetChildCount() < MAX_FOOD)
        {
            SpawnFood(isInView: false);
        }
        if (CritterLayer.GetChildCount() < MAX_CRITTERS && Rand.Chance(0.05f))
        {
            SpawnCritter(isInView: false);
        }
    }

    private const float ENGULF_SECONDS = 1.4f;     // from the first touch to digested
    private const float ENGULF_CONTACT = 0.3f;     // share of the prey radius that must touch the player

    /*
      Every frame, for each swallowed cell:
      > Its eater is gone (eaten, freed)
          - Release it, it swims on.
      > Otherwise
          - The eater stretches pseudopods towards it (EngulfPoint, EngulfReach: up while
            the arms wrap around, down once the prey is inside).
          - Pull it in a little further, digest it once done: biomass for the player,
            growth for a critter.
    */
    /// <summary>Advances every swallow, hands out the reward of finished ones.</summary>
    private void UpdateEngulfing(float dt, float playerSize)
    {
        // Pseudopods only reach out while a swallow runs
        Player.EngulfReach = 0f;
        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is Critter critter)
            {
                critter.EngulfReach = 0f;
            }
        }

        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is not Critter prey || !prey.IsEngulfed || prey.IsQueuedForDeletion())
            {
                continue;
            }

            // Its eater is gone (eaten, freed)
            CellBody eater = prey.Engulfer;
            if (!IsInstanceValid(eater) || eater.IsQueuedForDeletion())
            {
                prey.ReleaseEngulf();
                continue;
            }

            // Pseudopods towards the prey, the strongest swallow of an eater wins
            float reach = Mathf.Sin(prey.EngulfProgress * Mathf.Pi);
            if (reach >= eater.EngulfReach)
            {
                eater.EngulfReach = reach;
                eater.EngulfPoint = prey.GlobalPosition;
            }
            if (!prey.AdvanceEngulf(dt, ENGULF_SECONDS))
            {
                continue;
            }

            // Digested
            if (eater == Player)
            {
                Player.AddBiomass(KillReward(prey.EngulfRadius), BiomassSource.Meat);
            }
            else if (eater is Critter hunter)
            {
                hunter.Devour(prey.EngulfRadius, playerSize * CRITTER_GROWTH_CAP);
            }
            prey.QueueFree();
        }
    }

    private const float PLAYER_PULL = 0.3f;        // pull into a bigger cell at full depth, share of the player's speed
    private const float PULL_AT_CONTACT = 0.25f;   // share of PLAYER_PULL at the first touch, it grows with the depth
    private const float DIGEST_PER_SECOND = 1f;    // share of the old hit damage per second inside
    private const float THORNS_PER_SECOND = 2f;    // spikes and jaws keep hurting the eater from inside

    /*
      A bigger cell swallows the player like the player swallows small ones, but the
      player can fight it:
      - The eater's pseudopods reach around the player, the deeper it sits the more.
      - The pull drags the player to the eater's center, weak at the first touch and up to
        PLAYER_PULL of its own speed deep inside, swimming away gets it out (contact lost =
        free). The eater itself slows down while it eats (Critter, ENGULF_SLOWDOWN).
      - Inside, the player is digested steadily, faster by the eater's spikes and jaws
        (armor towards the eater halves it), and its own spikes and jaws hurt the eater
        (its armor halves that), which may pop and set it free.
      - Health at zero while inside means game over (Player.Die), a cluster loses cells first.
      - The grace period after a death or lost cells stops the swallow.
    */
    /// <summary>One frame of a bigger cell swallowing the player.</summary>
    /// <param name="critterHit">(Required) Spike and jaw damage of the eater towards the player, per second inside.</param>
    private void EngulfPlayer(Critter critter, float distance, float playerSize, float thornHit, float critterHit, float dt)
    {
        if (Player.Invulnerable > 0f)
        {
            return;
        }

        // Pseudopods around the player
        float depth = 1f - distance / (playerSize + critter.Radius);
        critter.EngulfReach = Mathf.Max(critter.EngulfReach, Mathf.Clamp(depth * 2f, 0f, 1f));
        critter.EngulfPoint = Player.GlobalPosition;

        // Pull towards the eater's center, weak at the first touch, stronger the deeper
        Vector2 toCenter = critter.GlobalPosition - Player.GlobalPosition;
        float   pull     = PLAYER_PULL * Mathf.Lerp(PULL_AT_CONTACT, 1f, Mathf.Clamp(depth * 2f, 0f, 1f));
        Player.Position += toCenter.LimitLength(Player.CurrentSpeed() * pull * dt);

        // Digest the player, thorns hurt back
        float damage = (0 == Player.Stage ? 6f : 12f) * critter.Radius / playerSize;
        Player.Digest((damage * DIGEST_PER_SECOND + critterHit) * dt, critter.Position);
        if (thornHit > 0f)
        {
            critter.Hurt(thornHit * THORNS_PER_SECOND * dt, Player.GlobalPosition);
        }
    }

    /// <summary>Checks every pair of cells once: start swallowing or push apart.</summary>
    private void ResolveCritterContacts(float playerSize)
    {
        var critters = new List<Critter>();
        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is Critter critter && !critter.IsQueuedForDeletion() && !critter.IsEngulfed)
            {
                critters.Add(critter);
            }
        }

        for (int i = 0; i < critters.Count; i++)
        {
            for (int j = i + 1; j < critters.Count; j++)
            {
                Critter a = critters[i];
                Critter b = critters[j];
                if (a.IsQueuedForDeletion() || b.IsQueuedForDeletion() || a.IsEngulfed || b.IsEngulfed)
                {
                    continue;
                }
                float distance = a.Position.DistanceTo(b.Position);
                if (distance >= a.Radius + b.Radius)
                {
                    continue;
                }

                // Bigger one flows around the smaller one on contact, ENGULF finishes the meal
                Critter big   = a.Radius >= b.Radius ? a : b;
                Critter small = big == a ? b : a;
                if (big.CanEat(small.Radius))
                {
                    small.StartEngulf(big);
                    continue;
                }

                // Similar size: push apart
                Vector2 normal  = distance > 0.01f ? (b.Position - a.Position).Normalized() : Vector2.Right;
                float   overlap = a.Radius + b.Radius - distance;
                a.Position -= normal * overlap * 0.5f;
                b.Position += normal * overlap * 0.5f;
            }
        }
    }

    // ===========================================================================
    // ================================   SPAWN   ================================
    // ===========================================================================
    //
    //   StartPopulating   Queues the starting food and cells, the soup is empty before.
    //   UpdatePopulating  Spawns the queue bit by bit inside the view, cells with a plop.
    //   SpawnFood         One piece of food (molecule, sea or land food), size and value relative to the player.
    //   SpawnCritter      One cell, kind and size depend on the stage, never right on top of the player.
    //   SpawnPosition     A point inside the view or in a ring just outside it, in the sea if asked.
    //

    private const float POPULATE_SECONDS = 5f;     // the starting soup fills over this time
    private const float SEA_FOOD_LOOK = 2.2f;      // sea and land food are drawn bigger than their value size,
    private const float LAND_FOOD_LOOK = 2.6f;     // next to a creature a molecule sized apple looked lost

    private int _pendingFood;
    private int _pendingCritters;
    private float _populateTime;

    private void StartPopulating()
    {
        _pendingFood     = START_FOOD;
        _pendingCritters = START_CRITTERS;
        _populateTime    = 0f;
        Log.Verbose("Main", $"Populating: {START_FOOD} molecules and {START_CRITTERS} cells over {POPULATE_SECONDS} s.");
    }

    /// <summary>
    /// Spawns as much of the queue as the elapsed share of POPULATE_SECONDS allows:
    /// molecules evenly, cells evenly but starting a second later.
    /// </summary>
    /// <returns>True while the queue is not empty, the normal refill waits meanwhile.</returns>
    private bool UpdatePopulating(float dt)
    {
        if (0 == _pendingFood && 0 == _pendingCritters)
        {
            return false;
        }
        _populateTime += dt;
        float share       = Mathf.Clamp(_populateTime / POPULATE_SECONDS, 0f, 1f);
        float cellShare   = Mathf.Clamp((_populateTime - 1f) / (POPULATE_SECONDS - 1f), 0f, 1f);
        int   foodDue     = Mathf.CeilToInt(START_FOOD * share) - (START_FOOD - _pendingFood);
        int   crittersDue = Mathf.CeilToInt(START_CRITTERS * cellShare) - (START_CRITTERS - _pendingCritters);
        for (int i = 0; i < foodDue && _pendingFood > 0; i++, _pendingFood--)
        {
            SpawnFood(isInView: true);
        }
        for (int i = 0; i < crittersDue && _pendingCritters > 0; i++, _pendingCritters--)
        {
            Critter critter = SpawnCritter(isInView: true);
            SpawnSplash(critter.Position, critter.Radius * 1.5f, critter.BodyColor);
        }
        return true;
    }

    // Off for now (decision of the user): land food popping out of nothing looked wrong,
    // it comes back with a source (e.g. fruit growing on trees and falling down)
    private static readonly bool IS_LAND_FOOD_SPAWNED = false;    // readonly, not const: no unreachable code warning

    /*
      Habitat by stage and spot:
      - Up to the cluster: molecules of the soup
      - Organism: sea food in the water, land food (fruit, mushrooms, berries) on the islands
        only with IS_LAND_FOOD_SPAWNED, else that spot stays empty
    */
    private void SpawnFood(bool isInView)
    {
        float   size     = Player.BodySize();
        Vector2 position = SpawnPosition(isInView, isSeaOnly: false);
        var     food     = new Food { Habitat = Player.IsOrganism ? FoodHabitat.Sea : FoodHabitat.Soup };
        if (Player.IsOrganism && null != Coast && Coast.IsOnLand(position))
        {
            if (!IS_LAND_FOOD_SPAWNED)
            {
                return;
            }
            food.Habitat      = FoodHabitat.Land;
            food.GroundHeight = Coast.GroundHeight(position) * size;
        }
        food.Setup(size * Rand.Range(0.15f, 0.28f), (FoodKind)Rand.Index(3));
        food.LookScale = FoodHabitat.Land == food.Habitat ? LAND_FOOD_LOOK : FoodHabitat.Sea == food.Habitat ? SEA_FOOD_LOOK : 1f;
        food.Position = position;
        FoodLayer.AddChild(food);
    }

    /// <returns>The new cell, already in the critter layer.</returns>
    private Critter SpawnCritter(bool isInView)
    {
        float playerSize = Player.BodySize();
        float roll       = GD.Randf();

        /*
          Kind by stage and roll:
          - Protocell:   60% bacteria (0.4-0.8x), 40% amoebas (1.3-1.8x, dangerous)
          - Later:       55% bacteria (0.35-0.8x), 30% amoebas (0.8-1.4x), 15% hunters (1.4-2.2x)
        */
        CritterKind kind;
        float radius;
        if (0 == Player.Stage)
        {
            kind   = roll < 0.6f ? CritterKind.Bacterium : CritterKind.Amoeba;
            radius = playerSize * (CritterKind.Bacterium == kind ? Rand.Range(0.4f, 0.8f) : Rand.Range(1.3f, 1.8f));
        }
        else if (roll < 0.55f)
        {
            kind   = CritterKind.Bacterium;
            radius = playerSize * Rand.Range(0.35f, 0.8f);
        }
        else if (roll < 0.85f)
        {
            kind   = CritterKind.Amoeba;
            radius = playerSize * Rand.Range(0.8f, 1.4f);
        }
        else
        {
            kind   = CritterKind.Hunter;
            radius = playerSize * Rand.Range(1.4f, 2.2f);
        }

        // In the organism stage the sea is full of creatures instead of cells
        var critter = new Critter { Target = Player };
        critter.Setup(radius, kind, playerSize, Player.Stage);
        if (Player.IsOrganism)
        {
            critter.Creature = CreatureDesign.ForCritter(kind);
        }

        // Keep a safety distance of ten player sizes when spawning in view
        Vector2 position     = SpawnPosition(isInView);
        Vector2 fromPlayer   = position - Player.GlobalPosition;
        float   safeDistance = playerSize * 10f;
        if (isInView && fromPlayer.Length() < safeDistance)
        {
            position = Player.GlobalPosition + fromPlayer.Normalized() * safeDistance;
        }
        critter.Position = position;
        critter.Popped  += OnCritterPopped;
        CritterLayer.AddChild(critter);
        return critter;
    }

    private const int SEA_SPOT_TRIES = 8;

    /// <summary>A random spawn point around the player.</summary>
    /// <param name="isInView">(Required) Inside the view instead of the ring just outside it.</param>
    /// <param name="isSeaOnly">(Optional) Tries a few points for one in the sea (deep on an island
    /// the coast would pile them up at the shore), else pushes the last one out. Defaults to true.</param>
    private Vector2 SpawnPosition(bool isInView, bool isSeaOnly = true)
    {
        float   viewRadius = ViewRadius();
        Vector2 position   = Player.GlobalPosition;
        for (int i = 0; i < SEA_SPOT_TRIES; i++)
        {
            float distance = isInView ? Rand.Range(0.1f, 1f) * viewRadius : Rand.Range(1.05f, 1.6f) * viewRadius;
            position = Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * distance;
            if (null == Coast || !isSeaOnly || !Coast.IsOnLand(position))
            {
                break;
            }
        }
        return null == Coast || !isSeaOnly ? position : Coast.ToSea(position, Player.BodySize() * 2f);
    }

    // ===========================================================================
    // ===========================   REACT TO EVENTS   ===========================
    // ===========================================================================
    //
    //   OnCritterPopped    A popped cell leaves four rich meat chunks.
    //   OnEvolutionReady   A stage advance opens the cell editor (after its stage cutscene).
    //   OpenEditor         Pauses and opens the cell editor, as organism the creature editor
    //                      (E, HUD button, stage advance), after the editor transition.
    //   OpenEditorNow      The editor of the stage, without transition.
    //   OnEditorClosed     Resumes the game.
    //   OnPlayerDivided    Plop around the cluster, banner at 64 cells.
    //   OnStageChanged     Stage cutscene, then the banner of the stage.
    //   OnPlayerDied       Game over: splash, the soup pauses, the game over screen shows.
    //   Restart            Reloads the scene, straight into a new game or to the title menu.
    //   OnPlayerLostCells  Red plops at the cluster edge and banner, the cluster lives on.
    //

    private const int POP_FOOD = 4;
    private const float POP_FOOD_BONUS = 3f;

    private void OnCritterPopped(Vector2 position, float radius, Color color)
    {
        SpawnSplash(position, radius * 1.8f, color);
        for (int i = 0; i < POP_FOOD; i++)
        {
            var food = new Food { IsMeat = true, Habitat = Player.IsOrganism ? FoodHabitat.Sea : FoodHabitat.Soup };
            food.Setup(radius * Rand.Range(0.2f, 0.3f), (FoodKind)Rand.Index(3));
            food.LookScale = FoodHabitat.Sea == food.Habitat ? SEA_FOOD_LOOK : 1f;
            food.Position = position + Vector2.FromAngle(Rand.Angle()) * radius * 0.6f;
            food.Value   *= POP_FOOD_BONUS;
            FoodLayer.CallDeferred(Node.MethodName.AddChild, food);
        }
    }

    /// <summary>A stage advance opens the editor by itself, an open one shows the new points.</summary>
    private void OnEvolutionReady()
    {
        if (Editor.IsOpen)
        {
            Editor.Refresh();
            return;
        }

        // The stage cutscene runs first, the editor follows when it is done (PLAY SHORT CUTSCENES)
        if (_shortCutscene is StageCutscene)
        {
            _isEditorQueued = true;
            return;
        }
        OpenEditor();
    }

    /// <summary>
    /// Pauses and opens the cell editor, from the single cell on and outside the menu.
    /// An organism gets the creature editor instead.
    /// </summary>
    /// <returns>True when an editor opens, right away with IsAnimationSkipped, else after the transition.</returns>
    /// <example><code>main.OpenEditor();   // what E and the HUD button do</code></example>
    public bool OpenEditor()
    {
        bool isBusy = IsInMenu || IsEvolving || null != _shortCutscene || Editor.IsOpen || CreatureEditor.IsOpen;
        if (isBusy || Player.Stage < Player.SINGLE_STAGE)
        {
            return false;
        }
        GetTree().Paused = true;
        if (IsAnimationSkipped)
        {
            OpenEditorNow();
            return true;
        }
        PlayEditorTransition();
        return true;
    }

    /// <summary>The editor of the stage: the creature editor for an organism, else the cell editor.</summary>
    private void OpenEditorNow()
    {
        if (Player.IsOrganism)
        {
            CreatureEditor.Open(Player, isFirstShaping: false);
            return;
        }
        Editor.Open(Player);
    }

    private void OnEditorClosed()
    {
        GetTree().Paused = false;
    }

    /// <summary>A plop around the cluster, the full cluster gets its banner.</summary>
    private void OnPlayerDivided(int cellCount)
    {
        SpawnSplash(Player.GlobalPosition, Player.BodySize() * 1.3f, Player.BodyColor);
        if (Player.MAX_CELLS == cellCount)
        {
            Hud.ShowBanner("BANNER_CLUSTER_TITLE", "BANNER_CLUSTER_TEXT");
        }
    }

    /// <summary>Stage cutscene, then the banner. The organism already had the evolve cutscene, it only gets its banner.</summary>
    private void OnStageChanged(int stage)
    {
        switch (stage)
        {
            case Player.SINGLE_STAGE:
                PlayStageCutscene(stage, "BANNER_SINGLE_TITLE", "BANNER_SINGLE_TEXT");
                break;
            case Player.MULTICELLULAR_STAGE:
                PlayStageCutscene(stage, "BANNER_MULTI_TITLE", "BANNER_MULTI_TEXT");
                break;
            case Player.ORGANISM_STAGE:
                Hud.ShowBanner("BANNER_ORGANISM_TITLE", "BANNER_ORGANISM_TEXT");
                break;
            case Player.LAND_STAGE:
                PlayStageCutscene(stage, "BANNER_LAND_TITLE", "BANNER_LAND_TEXT");
                break;
        }
    }

    private void OnPlayerDied()
    {
        SpawnSplash(Player.GlobalPosition, Player.BodySize() * 2f, new Color(1f, 0.4f, 0.4f));
        GetTree().Paused = true;
        Hud.Visible      = false;

        var screen = new GameOverScreen { StageKey = Player.STAGE_KEYS[Player.Stage], Biomass = Player.Biomass };
        AddChild(screen);
        screen.RestartPressed += () => Restart(isToMenu: false);
        screen.MenuPressed    += () => Restart(isToMenu: true);
        Log.Verbose("Main", $"Game over at stage {Player.Stage} with biomass {Player.Biomass:F0}.");
    }

    // Survives the scene reload: the new Main starts the game right away instead of the menu
    private static bool _isRestartQueued;

    /// <summary>A fresh soup: reloads the scene, a restart skips the title menu and plays the RNA prelude.</summary>
    /// <param name="isToMenu">(Required) True shows the title menu, false starts a new game at once.</param>
    public void Restart(bool isToMenu)
    {
        _isRestartQueued = !isToMenu;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    /// <summary>Red plops around the edge of the cluster, one per lost cell (at most eight).</summary>
    private void OnPlayerLostCells(int lost)
    {
        const int MAX_PLOPS = 8;
        for (int i = 0; i < Mathf.Min(lost, MAX_PLOPS); i++)
        {
            Vector2 edge = Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * Player.BodySize() * 0.9f;
            SpawnSplash(edge, Player.Radius * 1.5f, new Color(1f, 0.4f, 0.4f));
        }
        Hud.ShowBanner("BANNER_CELLS_LOST_TITLE", "BANNER_CELLS_LOST_TEXT");
    }

    // ===========================================================================
    // =========================   PLAY SHORT CUTSCENES   ========================
    // ===========================================================================
    //
    //   IsAnimationSkipped     Plays both instantly, e.g. in the smoke test and for the F10 cheats.
    //   PlayStageCutscene      Pauses, hides the HUD, shows the stage, then banner and a queued editor.
    //   PlayEditorTransition   Dive into the body, the editor opens when the screen is covered.
    //   PlayerCenter           The rendered center of the player body in 3D.
    //
    //   Only one short cutscene runs at a time. A stage advance during one gets its banner only.
    //

    /// <summary>(Optional) Skips the stage cutscenes and the editor transition. Defaults to false.</summary>
    public bool IsAnimationSkipped { get; set; }

    private Node3D _shortCutscene;
    private bool _isEditorQueued;

    /// <summary>Celebrates a stage advance, the banner and a queued editor follow when it is done.</summary>
    /// <param name="stage">(Required) The stage just reached.</param>
    /// <param name="titleKey">(Required) Banner title key in locale/translations.csv.</param>
    /// <param name="textKey">(Required) Banner text key.</param>
    private void PlayStageCutscene(int stage, string titleKey, string textKey)
    {
        if (IsAnimationSkipped || IsInMenu || IsEvolving || null != _shortCutscene)
        {
            Log.Verbose("Main", $"Stage {stage} without cutscene (skipped: {IsAnimationSkipped}, busy: {null != _shortCutscene}).");
            Hud.ShowBanner(titleKey, textKey);
            return;
        }
        GetTree().Paused = true;
        Hud.Visible      = false;
        var cutscene = new StageCutscene { Camera = _camera, Center = PlayerCenter(), Size = Player.BodySize(), Stage = stage };
        _shortCutscene = cutscene;
        AddChild(cutscene);
        cutscene.Finished += () =>
        {
            cutscene.QueueFree();
            _shortCutscene   = null;
            Hud.Visible      = true;
            GetTree().Paused = false;
            Hud.ShowBanner(titleKey, textKey);
            if (_isEditorQueued)
            {
                _isEditorQueued = false;
                OpenEditor();
            }
        };
    }

    /// <summary>Dives into the body, the editor opens when the screen is covered. The game is already paused.</summary>
    private void PlayEditorTransition()
    {
        var transition = new EditorTransition { Camera = _camera, Center = PlayerCenter(), Size = Player.BodySize(), Tint = Player.BodyColor };
        _shortCutscene = transition;
        AddChild(transition);

        // The transition fades its flash out over the editor and frees itself
        transition.Finished += () =>
        {
            _shortCutscene = null;
            OpenEditorNow();
        };
    }

    /// <summary>The creature visual of an organism (lifted on land, dropped on the planet), else the cell on the plane.</summary>
    private Vector3 PlayerCenter()
    {
        return Player.IsOrganism && null != Player.CreatureVisual ? Player.CreatureVisual.GlobalPosition : Plane3D.To3D(Player.GlobalPosition);
    }

    // ===========================================================================
    // =========================   EVOLVE INTO ORGANISM   ========================
    // ===========================================================================
    //
    //   1. Cutscene   Pauses, hides player and HUD, clears the cells, plays the evolve cutscene.
    //   2. Shaping    The blob settled: the player takes on the clay color, the creature editor opens.
    //   3. New sea    Editor closed: the cutscene goes, the creature swims, creatures fill the sea.
    //

    /// <summary>Whether the evolve cutscene or the first creature editor still runs.</summary>
    public bool IsEvolving { get; private set; }

    private EvolveCutscene _evolveCutscene;

    // ---------------------------------------------------------------------------
    // Cutscene
    //   Pauses, hides player and HUD, clears the cells, plays the evolve cutscene.
    // ---------------------------------------------------------------------------

    /// <summary>Starts the evolution of the full cluster into an organism.</summary>
    /// <param name="isInstant">(Optional) Skips the cutscene and opens the creature editor right away, e.g. in the smoke test. Defaults to false.</param>
    /// <returns>True when the evolution started.</returns>
    /// <example><code>if (player.CanEvolve) main.StartEvolve(isInstant: true);</code></example>
    public bool StartEvolve(bool isInstant = false)
    {
        if (IsEvolving || IsInMenu || null != _shortCutscene || !Player.CanEvolve)
        {
            return false;
        }
        IsEvolving       = true;
        GetTree().Paused = true;
        Player.IsHidden  = true;
        Hud.Visible      = false;

        // The cells of the soup make room, creatures follow after the shaping (New sea)
        foreach (Node critter in CritterLayer.GetChildren())
        {
            critter.QueueFree();
        }
        Log.Verbose("Main", $"Evolving into an organism (instant: {isInstant}).");
        if (isInstant)
        {
            OnEvolveCutsceneFinished();
            return true;
        }

        _evolveCutscene = new EvolveCutscene
        {
            Camera    = _camera,
            Center    = Plane3D.To3D(Player.GlobalPosition),
            Size      = Player.BodySize(),
            BodyColor = Player.BodyColor,
        };
        AddChild(_evolveCutscene);
        _evolveCutscene.Finished += OnEvolveCutsceneFinished;
        return true;
    }

    // ---------------------------------------------------------------------------
    // Shaping
    //   The blob settled, the creature editor opens (the coral clay, see Player.ORGANISM_COLOR).
    // ---------------------------------------------------------------------------

    private void OnEvolveCutsceneFinished()
    {
        CreatureEditor.Open(Player, isFirstShaping: true);
    }

    // ---------------------------------------------------------------------------
    // New sea
    //   Editor closed: the cutscene goes, the creature swims, creatures fill the sea.
    // ---------------------------------------------------------------------------

    private void OnCreatureEditorClosed()
    {
        GetTree().Paused = false;
        if (!IsEvolving)
        {
            return;
        }
        IsEvolving = false;

        // After a cutscene the camera eases back out from its close pose, an instant evolution keeps the game camera
        if (IsInstanceValid(_evolveCutscene))
        {
            _camDistance = _camera.GlobalPosition.DistanceTo(_camFocus);
            _evolveCutscene.QueueFree();
        }
        _evolveCutscene = null;
        Player.IsHidden = false;
        Hud.Visible     = true;
        SpawnSplash(Player.GlobalPosition, Player.BodySize() * 1.6f, Player.BodyColor);

        // Creatures and sea food pop up like the soup after the start (UpdatePopulating), the molecules go
        foreach (Node food in FoodLayer.GetChildren())
        {
            food.QueueFree();
        }
        _pendingFood     = START_FOOD;
        _pendingCritters = START_CRITTERS;
        _populateTime    = 0f;
        Log.Verbose("Main", "Organism released, the sea fills with creatures and sea food.");
    }

    // ===========================================================================
    // ===========================   COAST AND CHEATS   ==========================
    // ===========================================================================
    //
    //   CoastAt       Biomass from which the coast shows up, the next step after the organism.
    //   UpdateCoast   Spawns the coast once, keeps it around the player, tells the player where land is.
    //   SpawnCoast    An archipelago ahead of the player, its first shore far off or right in front.
    //   CheatTo       F10 menu: jumps forward to a stage, the coast or more points.
    //

    private const float COAST_GROWTH = 2f;           // the coast needs twice the biomass of the evolution
    private const float TRUNK_CONTACT = 0.5f;        // the body touches a trunk this many sizes from its center

    public Coast Coast { get; private set; }

    // Grows from the biomass of the evolution, not from EvolveAt: a long multicellular phase must not bring the coast right away
    public float CoastAt => Mathf.Max(Player.EvolveAt, Player.EvolvedAt) * COAST_GROWTH;

    private void UpdateCoast()
    {
        if (null == Coast && Player.IsOrganism && !IsEvolving && Player.Biomass >= CoastAt)
        {
            SpawnCoast(isNear: false);
        }
        if (null == Coast)
        {
            return;
        }
        float size = Player.BodySize();
        Coast.Follow(Player.GlobalPosition, size, ViewRadius(), _camera.Far, _view);
        Player.IsOnLand   = Coast.IsOnLand(Player.GlobalPosition);
        Player.GroundLift = Player.IsOnLand ? Coast.SurfaceLift(Player.GlobalPosition, size) : 0f;

        // Trunks block the way, the body slides around them
        if (Player.IsOnLand)
        {
            Player.Position = Coast.PushOutOfTrees(Player.Position, size * TRUNK_CONTACT);
        }
    }

    /// <summary>An archipelago ahead of the player (its facing), the landing island first.</summary>
    /// <param name="isNear">(Required) Right in front (cheat) instead of just outside the view.</param>
    /// <returns>The coast, also when it already existed.</returns>
    public Coast SpawnCoast(bool isNear)
    {
        if (null != Coast)
        {
            return Coast;
        }
        float   size      = Player.BodySize();
        Vector2 direction = Vector2.FromAngle(Player.Facing);
        float   distance  = isNear ? size * 4f : ViewRadius() * 1.1f;
        Coast = Coast.Scatter(Player.GlobalPosition, direction, distance, size, GD.Randi());
        AddChild(Coast);
        Coast.Follow(Player.GlobalPosition, size, ViewRadius());
        Hud.ShowBanner("BANNER_COAST_TITLE", "BANNER_COAST_TEXT");
        Log.Verbose("Main", $"Coast spawned {distance:F0} ahead (near: {isNear}).");
        return Coast;
    }

    /// <summary>Stage jumps of the F10 menu, only forward. Editors that open on the way close again.</summary>
    /// <param name="target">(Required) What to jump to.</param>
    /// <example><code>main.CheatTo(CheatTarget.Organism);</code></example>
    public void CheatTo(CheatTarget target)
    {
        if (IsInMenu || IsEvolving || null != _shortCutscene)
        {
            return;
        }
        Log.Verbose("Main", $"Cheat -> {target}.");

        // Stage jumps skip the short cutscenes, the recursion restores the flag on the way out
        bool wasSkipped = IsAnimationSkipped;
        IsAnimationSkipped = true;
        try
        {
            ApplyCheat(target);
        }
        finally
        {
            IsAnimationSkipped = wasSkipped;
        }
    }

    /// <summary>The jump itself, see CheatTo.</summary>
    private void ApplyCheat(CheatTarget target)
    {
        switch (target)
        {
            case CheatTarget.Points:
                Player.GrantPoints(10);
                break;
            case CheatTarget.SingleCell:
                Player.AddBiomass(Mathf.Max(0f, Player.STAGE_THRESHOLDS[Player.SINGLE_STAGE] - Player.Biomass), BiomassSource.Raw);
                break;
            case CheatTarget.Multicellular:
                CheatTo(CheatTarget.SingleCell);
                Player.AddBiomass(Mathf.Max(0f, Player.NextDivisionAt - Player.Biomass), BiomassSource.Raw);
                Player.Divide();
                break;
            case CheatTarget.FullCluster:
                CheatTo(CheatTarget.Multicellular);
                Player.AddBiomass(Mathf.Max(0f, Player.EvolveAt - Player.Biomass), BiomassSource.Raw);
                while (Player.Divide()) { }
                break;
            case CheatTarget.Organism:
                CheatTo(CheatTarget.FullCluster);
                CloseCheatEditors();
                StartEvolve();
                return;
            case CheatTarget.Coast:
                if (!Player.IsOrganism)
                {
                    return;
                }
                Player.AddBiomass(Mathf.Max(0f, CoastAt - Player.Biomass), BiomassSource.Raw);
                SpawnCoast(isNear: true);
                break;
            case CheatTarget.LandAnimal:
                if (!Player.IsOrganism)
                {
                    return;
                }
                Player.GiveLegsAndLung();
                Coast landing = SpawnCoast(isNear: true);
                Player.Position = landing.Origin + landing.Normal * Player.BodySize() * 2f;
                break;
        }
        CloseCheatEditors();
    }

    /// <summary>Stage advances on the way open the cell editor, a cheat jumps past it.</summary>
    private void CloseCheatEditors()
    {
        if (Editor.IsOpen)
        {
            Editor.Finish();
        }
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   KillReward    Biomass for an eaten cell.
    //   ViewRadius    Visible radius on the plane at the current camera distance.
    //   ViewRadiusAt  Visible radius on the plane at any camera distance (title menu).
    //   SpawnSplash   A plop effect on the plane.
    //   AddLayer      An empty 2D layer below Main.
    //

    private const float KILL_REWARD_FACTOR = 0.045f;

    /// <summary>Biomass for an eaten cell.</summary>
    /// <param name="radius">(Required) Radius of the eaten cell on the plane.</param>
    /// <returns>The biomass, about 7-13% of the own biomass for a large prey.</returns>
    public float KillReward(float radius)
    {
        return radius * radius * KILL_REWARD_FACTOR;
    }

    /// <summary>Radius of the visible area on the plane at the current camera distance, in third person a fixed reach.</summary>
    private float ViewRadius()
    {
        return Mathf.Lerp(ViewRadiusAt(_camDistance), Player.BodySize() * TP_VIEW_RADIUS, _view);
    }

    /// <summary>Radius of the visible area on the plane at a camera distance, with a margin for the camera tilt.</summary>
    private float ViewRadiusAt(float distance)
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float   aspect   = viewport.X / Mathf.Max(viewport.Y, 1f);
        return distance * Mathf.Tan(Mathf.DegToRad(CAM_FOV * 0.5f)) * Mathf.Sqrt(1f + aspect * aspect) * 1.25f;
    }

    private void SpawnSplash(Vector2 position, float radius, Color color)
    {
        var splash = new Splash { Position = Plane3D.To3D(position), MaxRadius = radius, Color = color };
        _fxLayer.CallDeferred(Node.MethodName.AddChild, splash);
    }

    private Node2D AddLayer()
    {
        var layer = new Node2D();
        AddChild(layer);
        return layer;
    }
}
