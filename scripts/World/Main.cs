// NOTE:
//   The game world: an endless primordial soup around the player. Spawns food and
//   cells relative to the player size, resolves eating and damage, and drives the
//   camera, stage changes and mutations. Logic runs in 2D on the plane, rendering
//   in 3D (see Core/Plane3D.cs). Attached to the root of scenes/main.tscn.

//
//   Chapters:
//     BUILD WORLD          Environment, light, camera, layers, player and HUD.
//     MENU AND INTRO       Title menu over the paused soup, then the birth of the protocell.
//     FOLLOW CAMERA        Tilted chase camera with wheel zoom, light and backdrop grow along.
//     RESOLVE CONTACTS     Food, player vs. cells and cells vs. cells per physics frame.
//     SPAWN                Food and cells around the view.
//     REACT TO EVENTS      Pops, the cell editor, stage changes and deaths.
//     EVOLVE INTO ORGANISM Cutscene from 64 cells to the clay blob, creature editor, the new sea.
//     COAST AND CHEATS     The coast of the organism stage, land checks, the F10 stage jumps.
//     HELPERS              Value scale, kill reward, view radius, splash and layers.
//

using System.Collections.Generic;
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
            AmbientLightColor  = new Color(0.5f, 0.8f, 0.85f),
            AmbientLightEnergy = 0.6f,
            GlowEnabled        = true,
            GlowIntensity      = 0.7f,
            GlowBloom          = 0.05f,
        };
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
    }

    // ===========================================================================
    // ===========================   MENU AND INTRO   ============================
    // ===========================================================================
    //
    //   1. Menu     Pauses the soup, hides player and HUD, camera high above the spawn.
    //   2. Start    Flies the camera down while the intro builds the protocell.
    //   3. Birth    Shows player and HUD, resumes the game, the soup starts filling, start banner.
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
    //   Flies the camera down while the intro builds the protocell.
    // ---------------------------------------------------------------------------

    /// <summary>Leaves the title menu: plays the intro, or skips it.</summary>
    /// <param name="isInstant">(Optional) Skips menu fade and intro, e.g. in the smoke test. Defaults to false.</param>
    /// <example><code>main.StartGame(isInstant: true);</code></example>
    public void StartGame(bool isInstant = false)
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

        // The gameplay pose comes from UpdateCamera, then the camera returns to the menu pose for the flight
        Transform3D menuPose = _camera.GlobalTransform;
        float       far      = _camera.Far;
        UpdateCamera(0f);
        Transform3D gamePose = _camera.GlobalTransform;
        _camera.GlobalTransform = menuPose;
        _camera.Far             = Mathf.Max(far, _camera.Far);

        // The menu fades itself out, also when the start did not come from its button
        _isIntroRunning = true;
        if (IsInstanceValid(_menu))
        {
            _menu.Start();
        }
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
        _backdrop.Follow(spawn, size, ViewRadiusAt(menuPose.Origin.DistanceTo(Plane3D.To3D(spawn))));
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
    //   _UnhandledInput   Mouse wheel zooms in and out, E opens the cell editor.
    //   ZoomBy            Changes the zoom factor by steps, clamped.
    //   _Process          Updates the camera once per frame.
    //   UpdateCamera      Eases distance and focus, then moves camera, shadow range and backdrop.
    //

    private const float ZOOM_STEP = 1.15f;
    public const float MIN_ZOOM = 0.5f;
    public const float MAX_ZOOM = 2.5f;

    /// <summary>Wheel zoom on top of the automatic distance, 1 = default, bigger = farther away.</summary>
    public float ZoomFactor { get; private set; } = 1f;

    private float _camDistance;
    private Vector3 _camFocus = Vector3.Zero;

    public override void _UnhandledInput(InputEvent @event)
    {
        // E opens the cell editor with the points collected so far
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } && OpenEditor())
        {
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

    /// <summary>Tilted chase camera, its distance relative to the player size.</summary>
    /// <param name="dt">(Required) Frame time, 0 snaps the focus to the player.</param>
    private void UpdateCamera(float dt)
    {
        float   playerSize = Player.BodySize();
        Vector3 focus      = Plane3D.To3D(Player.GlobalPosition);
        if (dt <= 0f)
        {
            _camFocus = focus;
        }
        _camDistance = Mathf.Lerp(_camDistance, playerSize * CAM_DISTANCE * ZoomFactor, Mathf.Clamp(dt * 1.5f, 0f, 1f));
        _camFocus    = _camFocus.Lerp(focus, Mathf.Clamp(dt * 5f, 0f, 1f));

        _camera.Position = _camFocus + new Vector3(0f, Mathf.Cos(CAM_TILT), Mathf.Sin(CAM_TILT)) * _camDistance;
        _camera.LookAt(_camFocus, Vector3.Up);
        _camera.Near = _camDistance * 0.05f;
        _camera.Far  = _camDistance * 8f;

        // Light and backdrop grow along, so shadows and motes fit at every size
        _sun.DirectionalShadowMaxDistance = _camDistance * 3f;
        _backdrop.Follow(new Vector2(_camFocus.X, _camFocus.Z), playerSize, ViewRadius());
    }

    // ===========================================================================
    // ==========================   RESOLVE CONTACTS   ===========================
    // ===========================================================================
    //
    //   1. Food               Attracts, swallows and cleans up food.
    //   2. Player vs. cells   Eat, get hurt or bump, cells far away despawn.
    //   3. Cells vs. cells    Bigger cells eat smaller ones, equal ones push apart.
    //   4. Refill             Fills the soup after the start, then keeps it topped up outside the view.
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
            if (null != Coast)
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
        // Player vs. cells
        //   Eat, get hurt or bump, cells far away despawn.
        // ---------------------------------------------------------------------------

        float thornScale = playerSize / CellBody.REFERENCE_SIZE;
        foreach (Node node in CritterLayer.GetChildren())
        {
            var critter = (Critter)node;
            if (critter.IsQueuedForDeletion())
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
            bool  isDangerous = critter.Radius > playerSize * Critter.EAT_RATIO && CritterKind.Bacterium != critter.Kind;
            float thornHit    = Player.ContactDamageToward(direction) * thornScale;
            critter.Threat   = canEat ? -1 : (isDangerous ? 1 : 0);
            if (distance > playerSize + critter.Radius)
            {
                continue;
            }

            // Smaller: swallowed once its center is inside the player
            if (canEat)
            {
                if (distance < playerSize)
                {
                    Player.AddBiomass(KillReward(critter.Radius), BiomassSource.Meat);
                    SpawnSplash(critter.Position, critter.Radius * 1.5f, critter.BodyColor);
                    critter.QueueFree();
                }
                continue;
            }

            // Bigger: hurts (plates towards it halve that), spikes and jaws towards it hurt back
            if (isDangerous)
            {
                float damage = (0 == Player.Stage ? 6f : 12f) * critter.Radius / playerSize;
                bool  isHit  = Player.TakeDamage(damage, critter.Position);
                if (isHit && thornHit > 0f)
                {
                    critter.Hurt(thornHit);
                }
                continue;
            }

            // Equal: push apart, spikes and jaws towards it hurt on bump
            Vector2 normal  = (critter.Position - playerPosition).Normalized();
            float   overlap = playerSize + critter.Radius - distance;
            critter.Position += normal * overlap * 0.5f;
            Player.Position  -= normal * overlap * 0.5f;
            if (thornHit > 0f && critter.BumpCooldown <= 0f)
            {
                critter.Hurt(thornHit);
                critter.BumpCooldown = 0.5f;
            }
        }

        // ---------------------------------------------------------------------------
        // Cells vs. cells
        //   Bigger cells eat smaller ones, equal ones push apart.
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

    /// <summary>Checks every pair of cells once: eat or push apart.</summary>
    private void ResolveCritterContacts(float playerSize)
    {
        var critters = new List<Critter>();
        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is Critter critter && !critter.IsQueuedForDeletion())
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
                if (a.IsQueuedForDeletion() || b.IsQueuedForDeletion())
                {
                    continue;
                }
                float distance = a.Position.DistanceTo(b.Position);
                if (distance >= a.Radius + b.Radius)
                {
                    continue;
                }

                // Bigger one eats once the center of the smaller one is inside
                Critter big   = a.Radius >= b.Radius ? a : b;
                Critter small = big == a ? b : a;
                if (big.CanEat(small.Radius))
                {
                    if (distance < big.Radius)
                    {
                        big.Devour(small.Radius, playerSize * CRITTER_GROWTH_CAP);
                        SpawnSplash(small.Position, small.Radius * 1.5f, small.BodyColor);
                        small.QueueFree();
                    }
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
    //   SpawnFood         One molecule, size and value relative to the player.
    //   SpawnCritter      One cell, kind and size depend on the stage, never right on top of the player.
    //   SpawnPosition     A point inside the view or in a ring just outside it.
    //

    private const float POPULATE_SECONDS = 5f;     // the starting soup fills over this time

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

    private void SpawnFood(bool isInView)
    {
        var food = new Food();
        food.Setup(Player.BodySize() * Rand.Range(0.15f, 0.28f), (FoodKind)Rand.Index(3));
        food.Position = SpawnPosition(isInView);
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
        critter.Setup(radius, kind, playerSize);
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

    private Vector2 SpawnPosition(bool isInView)
    {
        float viewRadius = ViewRadius();
        float   distance   = isInView ? Rand.Range(0.1f, 1f) * viewRadius : Rand.Range(1.05f, 1.6f) * viewRadius;
        Vector2 position   = Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * distance;
        return null == Coast ? position : Coast.ToSea(position, Player.BodySize() * 2f);
    }

    // ===========================================================================
    // ===========================   REACT TO EVENTS   ===========================
    // ===========================================================================
    //
    //   OnCritterPopped    A popped cell leaves four rich meat chunks.
    //   OnEvolutionReady   A stage advance opens the cell editor.
    //   OpenEditor         Pauses and opens the cell editor, as organism the creature editor
    //                      (E, HUD button, stage advance).
    //   OnEditorClosed     Resumes the game.
    //   OnPlayerDivided    Plop around the cluster, banner at 64 cells.
    //   OnStageChanged     Banner per stage.
    //   OnPlayerDied       Splash and banner.
    //   OnPlayerLostCells  Red plops at the cluster edge and banner, the cluster lives on.
    //

    private const int POP_FOOD = 4;
    private const float POP_FOOD_BONUS = 3f;

    private void OnCritterPopped(Vector2 position, float radius, Color color)
    {
        SpawnSplash(position, radius * 1.8f, color);
        for (int i = 0; i < POP_FOOD; i++)
        {
            var food = new Food { IsMeat = true };
            food.Setup(radius * Rand.Range(0.2f, 0.3f), (FoodKind)Rand.Index(3));
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
        OpenEditor();
    }

    /// <summary>
    /// Pauses and opens the cell editor, from the single cell on and outside the menu.
    /// An organism gets the creature editor instead.
    /// </summary>
    /// <returns>True when an editor opened.</returns>
    /// <example><code>main.OpenEditor();   // what E and the HUD button do</code></example>
    public bool OpenEditor()
    {
        if (IsInMenu || IsEvolving || Editor.IsOpen || CreatureEditor.IsOpen || Player.Stage < Player.SINGLE_STAGE)
        {
            return false;
        }
        GetTree().Paused = true;
        if (Player.IsOrganism)
        {
            CreatureEditor.Open(Player, isFirstShaping: false);
            return true;
        }
        Editor.Open(Player);
        return true;
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

    private void OnStageChanged(int stage)
    {
        switch (stage)
        {
            case 1:
                Hud.ShowBanner("BANNER_SINGLE_TITLE", "BANNER_SINGLE_TEXT");
                break;
            case Player.MULTICELLULAR_STAGE:
                Hud.ShowBanner("BANNER_MULTI_TITLE", "BANNER_MULTI_TEXT");
                break;
            case Player.ORGANISM_STAGE:
                Hud.ShowBanner("BANNER_ORGANISM_TITLE", "BANNER_ORGANISM_TEXT");
                break;
            case Player.LAND_STAGE:
                Hud.ShowBanner("BANNER_LAND_TITLE", "BANNER_LAND_TEXT");
                break;
        }
    }

    private void OnPlayerDied()
    {
        // Death on land: the body washes back into the sea
        if (null != Coast && Coast.IsOnLand(Player.GlobalPosition))
        {
            Player.Position = Coast.ToSea(Player.GlobalPosition, Player.BodySize() * 3f);
        }
        SpawnSplash(Player.GlobalPosition, Player.BodySize() * 2f, new Color(1f, 0.4f, 0.4f));
        Hud.ShowBanner("BANNER_DEATH_TITLE", "BANNER_DEATH_TEXT");
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
        if (IsEvolving || IsInMenu || !Player.CanEvolve)
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
    //   The blob settled: the player takes on the clay color, the creature editor opens.
    // ---------------------------------------------------------------------------

    private void OnEvolveCutsceneFinished()
    {
        Player.BodyColor = EvolveCutscene.ClayFor(Player.BodyColor);
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

        // Creatures pop up like the cells after the start (UpdatePopulating)
        _pendingFood     = 0;
        _pendingCritters = START_CRITTERS;
        _populateTime    = 0f;
        Log.Verbose("Main", "Organism released, the sea fills with creatures.");
    }

    // ===========================================================================
    // ===========================   COAST AND CHEATS   ==========================
    // ===========================================================================
    //
    //   CoastAt       Biomass from which the coast shows up, the next step after the organism.
    //   UpdateCoast   Spawns the coast once, slides it along, tells the player where land is.
    //   SpawnCoast    A shore ahead of the player, far off or right in front.
    //   CheatTo       F10 menu: jumps forward to a stage, the coast or more points.
    //

    private const float COAST_GROWTH = 2f;           // the coast needs twice the biomass of the evolution

    public Coast Coast { get; private set; }

    public float CoastAt => Player.EvolveAt * COAST_GROWTH;

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
        Coast.Follow(Player.GlobalPosition, Player.BodySize(), ViewRadius());
        Player.IsOnLand = Coast.IsOnLand(Player.GlobalPosition);
    }

    /// <summary>A straight shore ahead of the player (its facing), land beyond it.</summary>
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
        Coast = new Coast { Origin = Player.GlobalPosition + direction * distance, Normal = direction, Unit = size };
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
        if (IsInMenu || IsEvolving)
        {
            return;
        }
        Log.Verbose("Main", $"Cheat -> {target}.");
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

    /// <summary>Radius of the visible area on the plane at the current camera distance.</summary>
    private float ViewRadius()
    {
        return ViewRadiusAt(_camDistance);
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
