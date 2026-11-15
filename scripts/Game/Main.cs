// NOTE:
//   The game world: an endless primordial soup around the player. Main is the
//   coordinator: it builds the world, creates the components of World/ (camera,
//   contacts, spawning, coast), Flow/ (start flow, cutscenes) and Tribe/ (pack, tribe,
//   rivals, hut designer, their panel), wires their signals, routes input and frames to them and reacts to the
//   events of the player: editors, stage changes, deaths, the evolution and the cheats. Logic runs in 2D on the plane,
//   rendering in 3D (see Core/Plane3D.cs). Attached to the root of scenes/main.tscn.

//
//   Chapters:
//     BUILD WORLD              Camera, layers, player, HUD and the components.
//     MENU AND INTRO           Title menu, RNA prelude and intro (Flow/StartFlow.cs), then the soup fills.
//     ROUTE INPUT AND FRAMES   Input, camera and the order of a physics frame.
//     REACT TO EVENTS          The cell editor, stage changes and deaths.
//     PLAY SHORT CUTSCENES     The stage cutscene and the transition into an editor.
//     EVOLVE INTO ORGANISM     Cutscene from 64 cells to the clay blob, creature editor, the new sea.
//     COAST AND CHEATS         The coast of the organism stage, the pack, the tribe and the F10 stage jumps.
//     HELPERS                  Kill reward, splash and layers.
//

using Godot;

namespace CellOut;

public partial class Main : Node2D
{
    public const float MIN_ZOOM = FollowCamera.MIN_ZOOM;
    public const float MAX_ZOOM = FollowCamera.MAX_ZOOM;

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
    //   1. Scene        The session and the camera with environment, sun and backdrop.
    //   2. Layers       Food, critters, the player and effects in draw order.
    //   3. HUD          Binds HUD, editors and the F10 cheat menu and connects the player signals.
    //   4. Components   Contacts, spawning, coast, pack, tribe, cutscenes and the start flow, wired by signals.
    //   5. Menu         The title menu over the empty soup, it fills after the start (MENU AND INTRO).
    //

    private WorldSession _session;
    private FollowCamera _followCamera;
    private ContactResolver _contacts;
    private Spawner _spawner;
    private CoastKeeper _coast;
    private PackKeeper _pack;
    private TribeCommander _tribe;
    private RivalKeeper _rivals;
    private BuildingEditor _buildingEditor;
    private BuildingPlacer _placer;
    private TribePanel _tribePanel;
    private readonly System.Collections.Generic.List<CanvasLayer> _tribeHud = new();     // TribePanel and TribeHub
    private TribeHub _hub;
    private CutsceneDirector _cutscenes;
    private StartFlow _startFlow;
    private Node2D _fxLayer;

    public override void _Ready()
    {
        GD.Randomize();
        UserSettings.Load();   // language and graphics tier, before anything is built

        // ---------------------------------------------------------------------------
        // Scene
        //   The session and the camera with environment, sun and backdrop.
        // ---------------------------------------------------------------------------

        // Player and layers enter the tree in Layers, the session only needs to know them
        Player       = new Player();
        FoodLayer    = new Node2D();
        CritterLayer = new Node2D();
        _session     = new WorldSession(Player, FoodLayer, CritterLayer);

        _followCamera = new FollowCamera();
        _followCamera.Setup(_session);
        AddChild(_followCamera);

        // ---------------------------------------------------------------------------
        // Layers
        //   Food, critters, the player and effects in draw order.
        // ---------------------------------------------------------------------------

        AddChild(FoodLayer);
        AddChild(CritterLayer);
        AddChild(Player);
        _fxLayer = AddLayer();
        _followCamera.StartClose();

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
        // Components
        //   Contacts, spawning, coast, cutscenes and the start flow, wired by signals.
        // ---------------------------------------------------------------------------

        _contacts = new ContactResolver();
        _contacts.Setup(_session);
        AddChild(_contacts);

        _spawner = new Spawner();
        _spawner.Setup(_session, _followCamera);
        _spawner.SplashRequested += SpawnSplash;
        AddChild(_spawner);

        _coast = new CoastKeeper();
        _coast.Setup(_session, _followCamera);
        _coast.CoastSpawned += () => Hud.ShowBanner("BANNER_COAST_TITLE", "BANNER_COAST_TEXT");
        AddChild(_coast);

        _pack = new PackKeeper();
        _pack.Setup(_session, _followCamera);
        AddChild(_pack);

        _tribe = new TribeCommander();
        _tribe.Setup(_session, _pack, _followCamera);
        AddChild(_tribe);

        _rivals = new RivalKeeper();
        _rivals.Setup(_session, _pack, _tribe);
        _rivals.RaidStarted  += nameKey => Hud.ShowBanner("BANNER_RAID_TITLE", "BANNER_RAID_TEXT", Tr(nameKey));
        _rivals.TradeArrived += (nameKey, food, wood) => Hud.ShowBanner("BANNER_TRADE_TITLE", "BANNER_TRADE_TEXT", Tr(nameKey), food, wood);
        _rivals.TribeDefeated += (nameKey, food, wood) => Hud.ShowBanner("BANNER_DEFEAT_TITLE", "BANNER_DEFEAT_TEXT", Tr(nameKey), food, wood);
        AddChild(_rivals);
        _tribe.AttachRivals(_rivals);

        _placer = new BuildingPlacer();
        _placer.Setup(_session, _tribe);
        AddChild(_placer);

        _buildingEditor = new BuildingEditor();
        _buildingEditor.Applied += session => _tribe.TakeDesigns(session.Result);
        _buildingEditor.Closed  += OnBuildingEditorClosed;
        AddChild(_buildingEditor);

        var tribePanel = new TribePanel();
        _tribePanel    = tribePanel;
        _tribeHud.Add(tribePanel);
        tribePanel.Watch(_pack, _tribe, _rivals);
        tribePanel.ActionPressed   += () => _pack.Act();
        tribePanel.BuildPressed    += kind => _placer.Begin(kind);
        tribePanel.GiftPressed     += rival => _rivals.Gift(rival);
        AddChild(tribePanel);

        _hub = new TribeHub();
        _tribeHud.Add(_hub);
        _hub.Watch(_session, _pack, _tribe, _rivals, _followCamera);
        _hub.BuilderPressed += () => OpenBuildingEditor();
        AddChild(_hub);

        _cutscenes = new CutsceneDirector();
        _cutscenes.Setup(_session, _followCamera.Camera);
        _cutscenes.StageFinished  += OnStageCutsceneFinished;
        _cutscenes.EditorCovered  += OpenEditorNow;
        _cutscenes.EvolveFinished += OnEvolveCutsceneFinished;
        AddChild(_cutscenes);

        _startFlow = new StartFlow();
        _startFlow.Setup(_session, _followCamera);
        _startFlow.LanguagePressed += Hud.ToggleLanguage;
        _startFlow.Finished        += OnStartFinished;
        AddChild(_startFlow);

        // ---------------------------------------------------------------------------
        // Menu
        //   The title menu over the empty soup, it fills after the start (MENU AND INTRO).
        // ---------------------------------------------------------------------------

        Hud.Visible = false;
        _startFlow.ShowMenu();

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
    //   IsInMenu          Whether the title menu or the intro still runs.
    //   StartGame         Leaves the title menu: RNA prelude and intro, or skips them.
    //   OnStartFinished   The protocell is born: HUD, the soup starts filling, start banner.
    //
    //   Menu, prelude and intro themselves are in Flow/StartFlow.cs.
    //

    /// <summary>Whether the title menu or the intro still runs, the game is paused meanwhile.</summary>
    public bool IsInMenu => _startFlow.IsInMenu;

    /// <summary>Leaves the title menu: plays the RNA prelude and the intro, or skips them.</summary>
    /// <param name="isInstant">(Optional) Skips menu fade, prelude and intro, e.g. in the smoke test. Defaults to false.</param>
    /// <param name="isPreludeSkipped">(Optional) Goes straight to the intro, e.g. for the cutscene reel. Defaults to false.</param>
    /// <example><code>main.StartGame(isInstant: true);</code></example>
    public void StartGame(bool isInstant = false, bool isPreludeSkipped = false)
    {
        _startFlow.Start(isInstant, isPreludeSkipped);
    }

    private void OnStartFinished()
    {
        Hud.Visible = true;
        _spawner.StartPopulating();
        Log.Verbose("Main", $"Populating: food patches outside the view, {Spawner.START_CRITTERS} cells over {Spawner.POPULATE_SECONDS} s.");
        // The game title already opened the RNA prelude, the protocell gets its own stage banner
        if (_startFlow.HasPlayedPrelude)
        {
            Hud.ShowBanner("BANNER_PROTOCELL_TITLE", "BANNER_PROTOCELL_TEXT");
        }
        else
        {
            Hud.ShowBanner("BANNER_START_TITLE", "BANNER_START_TEXT");
        }
        Log.Verbose("Main", "Intro finished -> game running.");
    }

    // ===========================================================================
    // =======================   ROUTE INPUT AND FRAMES   ========================
    // ===========================================================================
    //
    //   _UnhandledInput   Z opens the editor, B the village builder, the tribe takes the mouse, the pack Space and F, the rest goes to the camera.
    //   OpenBuildingEditor   Pauses, hides the HUD and opens the village builder of the tribe.
    //   ShowHud              Hides or shows every HUD layer.
    //   ZoomFactor        Wheel zoom of the camera.
    //   ThirdPerson       0 top view .. 1 third person.
    //   ZoomBy            Changes the zoom factor by steps, clamped.
    //   _Process          Advances the camera once per frame.
    //   _PhysicsProcess   Coast, pack, tribe, rivals, contacts and refill, in this order.
    //

    public override void _UnhandledInput(InputEvent @event)
    {
        // Z opens the editor of the stage (cell or creature) with the points collected so far
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Z } && OpenEditor())
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.B } && OpenBuildingEditor())
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_placer.HandleInput(@event) || _tribe.HandleInput(@event) || _pack.HandleInput(@event) || _followCamera.HandleInput(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Pauses and opens the village builder with the blueprints of the tribe.</summary>
    /// <returns>True when it opens: from the tribe on, outside menu, cutscenes and other editors.</returns>
    /// <example><code>main.OpenBuildingEditor();   // what B and the panel button do</code></example>
    public bool OpenBuildingEditor()
    {
        bool isBusy = IsInMenu || IsEvolving || _cutscenes.IsShortPlaying || Editor.IsOpen || CreatureEditor.IsOpen || _buildingEditor.IsOpen;
        if (isBusy || !_tribe.IsActive)
        {
            return false;
        }
        _placer.Cancel();
        GetTree().Paused = true;
        ShowHud(false);
        _buildingEditor.Open(_tribe.DesignOf);
        return true;
    }

    private void OnBuildingEditorClosed()
    {
        ShowHud(true);
        GetTree().Paused = false;
    }

    /// <summary>Hides or shows every HUD layer: the builder shows the game behind it as its backdrop, without texts on it.</summary>
    private void ShowHud(bool isShown)
    {
        Hud.Visible = isShown;
        foreach (CanvasLayer layer in _tribeHud)
        {
            layer.Visible = isShown;
        }
    }

    /// <summary>Wheel zoom on top of the automatic distance, 1 = default, bigger = farther away.</summary>
    public float ZoomFactor => _followCamera.ZoomFactor;

    /// <summary>0 top view .. 1 third person, eased with SmoothStep when used.</summary>
    public float ThirdPerson => _followCamera.ThirdPerson;

    /// <summary>0 chase camera .. 1 strategy view of the tribe.</summary>
    public float StrategyView => _followCamera.Strategy;

    /// <summary>Zooms out (positive steps) or in (negative steps), clamped to MIN_ZOOM..MAX_ZOOM.</summary>
    /// <param name="steps">(Required) Number of wheel steps.</param>
    /// <example><code>ZoomBy(-2)  // two steps closer</code></example>
    public void ZoomBy(int steps)
    {
        _followCamera.ZoomBy(steps);
    }

    public override void _Process(double delta)
    {
        _followCamera.Advance((float)delta);
    }

    /*
      One physics frame, the order matters:
      1. Coast      Spawns and follows, tells the player where land is (World/CoastKeeper.cs).
      2. Pack       Wild kin, the pack, their steering and meals, the offer (Tribe/Pack/PackKeeper.cs).
      3. Tribe      Gathering, chopping, huts and villagers from the tribe on (Tribe/Village/TribeCommander.cs),
                    then the neighbor tribes: traders, raids and fights (Tribe/Rivals/RivalKeeper.cs).
      4. Contacts   Food, engulfing, player vs. cells, cells vs. cells (World/ContactResolver.cs),
                    with the size and position of the player from before the coast moved it.
      5. Refill     Fills the soup after the start, then keeps it topped up, fruit from the trees (World/Spawner.cs).
    */
    public override void _PhysicsProcess(double delta)
    {
        float   dt             = (float)delta;
        float   playerSize     = Player.BodySize();
        Vector2 playerPosition = Player.GlobalPosition;
        float   viewRadius     = _followCamera.ViewRadius();
        _coast.Update(isSpawnAllowed: !IsEvolving);
        _pack.Update(dt);
        _tribe.Update(dt);
        _placer.Update();
        _tribePanel.Placing = _placer.Pending;
        _rivals.Update(dt);
        _contacts.Resolve(dt, playerSize, playerPosition, viewRadius);
        _spawner.Refill(dt);
    }

    // ===========================================================================
    // ===========================   REACT TO EVENTS   ===========================
    // ===========================================================================
    //
    //   OnEvolutionReady   A stage advance shows its new points in an open cell editor.
    //   OpenEditor         Pauses and opens the cell editor, as organism the creature editor
    //                      (E, HUD button), after the editor transition.
    //   OpenEditorNow      The editor of the stage, without transition.
    //   OnEditorClosed     Resumes the game.
    //   OnPlayerDivided    Plop around the cluster, banner at 64 cells.
    //   OnStageChanged     Stage cutscene, then the banner of the stage.
    //   OnPlayerDied       Game over: splash, the soup pauses, the game over screen shows. A pack
    //                      leader lives on in a member instead (PackKeeper.TryTakeOver).
    //   Restart            Reloads the scene, straight into a new game or to the title menu.
    //   OnPlayerLostCells  Red plops at the cluster edge and banner, the cluster lives on.
    //

    /// <summary>A stage advance never opens the editor by itself (E / HUD button do), an open one shows the new points.</summary>
    private void OnEvolutionReady()
    {
        if (Editor.IsOpen)
        {
            Editor.Refresh();
        }
    }

    /// <summary>
    /// Pauses and opens the cell editor, from the single cell on and outside the menu.
    /// An organism gets the creature editor instead.
    /// </summary>
    /// <returns>True when an editor opens, right away with IsAnimationSkipped, else after the transition.</returns>
    /// <example><code>main.OpenEditor();   // what Z and the HUD button do</code></example>
    public bool OpenEditor()
    {
        bool isBusy = IsInMenu || IsEvolving || _cutscenes.IsShortPlaying || Editor.IsOpen || CreatureEditor.IsOpen || _buildingEditor.IsOpen;
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
        _cutscenes.PlayEditorTransition();
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
            case Player.PACK_STAGE:
                PlayStageCutscene(stage, "BANNER_PACK_TITLE", "BANNER_PACK_TEXT");
                break;
            case Player.TRIBE_STAGE:
                // The strategy view rises over the fire, the village is its reference point, the neighbors settle
                _followCamera.StrategyAnchor = _pack.Nest?.Position;
                _rivals.Settle();
                PlayStageCutscene(stage, "BANNER_TRIBE_TITLE", "BANNER_TRIBE_TEXT");
                break;
        }
    }

    private void OnPlayerDied()
    {
        SpawnSplash(Player.GlobalPosition, Player.BodySize() * 2f, new Color(1f, 0.4f, 0.4f));

        // A pack leader lives on in the body of a member, the pack shrinks by one
        if (_pack.TryTakeOver())
        {
            Hud.ShowBanner("BANNER_TAKEOVER_TITLE", "BANNER_TAKEOVER_TEXT");
            Log.Verbose("Main", "The leader died, a pack member takes over.");
            return;
        }
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
    // ========================   PLAY SHORT CUTSCENES   =========================
    // ===========================================================================
    //
    //   IsAnimationSkipped        Plays both instantly, e.g. in the smoke test and for the F10 cheats.
    //   PlayStageCutscene         Pauses, hides the HUD and shows the stage, or only the banner.
    //   OnStageCutsceneFinished   Resumes, then the banner.
    //
    //   The cutscenes themselves are staged by Flow/CutsceneDirector.cs. Only one short
    //   cutscene runs at a time. A stage advance during one gets its banner only.
    //

    /// <summary>(Optional) Skips the stage cutscenes and the editor transition. Defaults to false.</summary>
    public bool IsAnimationSkipped { get; set; }

    /// <summary>Celebrates a stage advance, the banner follows when it is done.</summary>
    /// <param name="stage">(Required) The stage just reached.</param>
    /// <param name="titleKey">(Required) Banner title key in locale/translations.csv.</param>
    /// <param name="textKey">(Required) Banner text key.</param>
    private void PlayStageCutscene(int stage, string titleKey, string textKey)
    {
        if (IsAnimationSkipped || IsInMenu || IsEvolving || _cutscenes.IsShortPlaying)
        {
            Log.Verbose("Main", $"Stage {stage} without cutscene (skipped: {IsAnimationSkipped}, busy: {_cutscenes.IsShortPlaying}).");
            Hud.ShowBanner(titleKey, textKey);
            return;
        }
        GetTree().Paused = true;
        Hud.Visible      = false;
        _cutscenes.PlayStage(stage, titleKey, textKey);
    }

    private void OnStageCutsceneFinished(string titleKey, string textKey)
    {
        Hud.Visible      = true;
        GetTree().Paused = false;
        Hud.ShowBanner(titleKey, textKey);
    }

    // ===========================================================================
    // ========================   EVOLVE INTO ORGANISM   =========================
    // ===========================================================================
    //
    //   1. Cutscene   Pauses, hides player and HUD, clears the cells, plays the evolve cutscene.
    //   2. Shaping    The blob settled, the creature editor opens (the coral clay, see Player.ORGANISM_COLOR).
    //   3. New sea    Editor closed: the cutscene goes, the creature swims, creatures fill the sea.
    //

    /// <summary>Whether the evolve cutscene or the first creature editor still runs.</summary>
    public bool IsEvolving { get; private set; }

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
        if (IsEvolving || IsInMenu || _cutscenes.IsShortPlaying || !Player.CanEvolve)
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
        _cutscenes.PlayEvolve();
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
        if (_cutscenes.HasEvolveCutscene)
        {
            _followCamera.ResumeFromPose();
        }
        _cutscenes.EndEvolve();
        Player.IsHidden = false;
        Hud.Visible     = true;

        // Creatures and sea food pop up like the soup after the start (Spawner.StartPopulating), the molecules go
        foreach (Node food in FoodLayer.GetChildren())
        {
            food.QueueFree();
        }
        _spawner.StartPopulating();
        Log.Verbose("Main", "Organism released, the sea fills with creatures and sea food.");
    }

    // ===========================================================================
    // ==========================   COAST AND CHEATS   ===========================
    // ===========================================================================
    //
    //   Coast        The coast of the organism stage, null until it showed up.
    //   CoastAt      Biomass from which the coast shows up, the next step after the organism.
    //   SpawnCoast   An archipelago ahead of the player, its first shore far off or right in front.
    //   Pack         The pack of the land animal: kin, nest, offer (Tribe/Pack/PackKeeper.cs).
    //   Tribe        The tribe: selection, stock, huts (Tribe/Village/TribeCommander.cs).
    //   Rivals       The neighbor tribes (Tribe/Rivals/RivalKeeper.cs).
    //   BuildingEditor   The village builder (Tribe/Building/Editor/BuildingEditor.cs).
    //   Hub          The hub bar: map, home button, counters (Tribe/TribeHub.cs).
    //   FollowCamera     The camera, for the strategy focus (World/FollowCamera.cs).
    //   CheatTo     F10 menu: jumps forward to a stage, the coast or more points.
    //
    //   The coast itself is kept by World/CoastKeeper.cs.
    //

    public Coast Coast => _session.Coast;

    public float CoastAt => _coast.CoastAt;

    public PackKeeper Pack => _pack;

    public TribeCommander Tribe => _tribe;

    public RivalKeeper Rivals => _rivals;

    public BuildingEditor BuildingEditor => _buildingEditor;

    public BuildingPlacer Placer => _placer;

    public TribeHub Hub => _hub;

    public FollowCamera FollowCamera => _followCamera;

    /// <summary>An archipelago ahead of the player (its facing), the landing island first.</summary>
    /// <param name="isNear">(Required) Right in front (cheat) instead of just outside the view.</param>
    /// <returns>The coast, also when it already existed.</returns>
    public Coast SpawnCoast(bool isNear)
    {
        return _coast.SpawnCoast(isNear);
    }

    /// <summary>Stage jumps of the F10 menu, only forward. A running RNA prelude ends first, editors that open on the way close again.</summary>
    /// <param name="target">(Required) What to jump to.</param>
    /// <example><code>main.CheatTo(CheatTarget.Organism);</code></example>
    public void CheatTo(CheatTarget target)
    {
        _startFlow.SkipPrelude();
        if (IsInMenu || IsEvolving || _cutscenes.IsShortPlaying)
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
            case CheatTarget.Resources:
                _tribe.Economy.AddFood(20);
                _tribe.Economy.AddWood(20);
                break;
            case CheatTarget.SingleCell:
                Player.AddBiomass(Mathf.Max(0f, Balance.Current.Player.SingleCellAt - Player.Biomass), BiomassSource.Raw);
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
            case CheatTarget.Pack:
                CheatToLandAnimal();
                _pack.CheatFoundPack();
                break;
            case CheatTarget.Tribe:
                CheatToLandAnimal();
                _pack.CheatFoundTribe();
                break;
        }
        CloseCheatEditors();
    }

    /// <summary>The land animal on grass, one physics frame in, so the pack cheats find land below the player.</summary>
    private void CheatToLandAnimal()
    {
        if (!Player.IsLandAnimal)
        {
            CheatTo(CheatTarget.LandAnimal);
            _coast.Update(isSpawnAllowed: true);
            Player._PhysicsProcess(0.1);
        }
        if (null != Coast && !Player.IsPackLeader)
        {
            // Deep enough inland for a nest, the nest cheat does not check the ground but the kin should stand on grass
            Player.Position = Coast.Islands[0].Center;
            _coast.Update(isSpawnAllowed: true);
        }
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
    //   SpawnSplash   A plop effect on the plane.
    //   AddLayer      An empty 2D layer below Main.
    //

    /// <summary>Biomass for an eaten cell.</summary>
    /// <param name="radius">(Required) Radius of the eaten cell on the plane.</param>
    /// <returns>The biomass, about 7-13% of the own biomass for a large prey.</returns>
    public float KillReward(float radius)
    {
        return ContactResolver.KillReward(radius);
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
