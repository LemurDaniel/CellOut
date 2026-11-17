// NOTE:
//   Jumps through every stage for a movie render, so the look of the game can be checked
//   by eye: protocell, single cell, cell cluster, organism in the sea, coast, land animal,
//   pack at the nest, tribe at the fire behind its chieftain.
//   No checks, the frames are for the eye. Quits by itself. The stills worth looking at
//   are the last frames before each jump (STILL_FRAMES).
//
//   godot --path . res://tests/GraphicsReel.tscn --write-movie <dir>/f.png --fixed-fps 30
//   $env:CELLOUT_GRAPHICS = 'Low'      # optional: force a graphics tier (Low, Medium, High)
//   $env:CELLOUT_REEL_LAND = '1'       # optional: rush to the land animal, only the last still is worth a look
//   $env:CELLOUT_REEL_TRIBE = '1'      # optional: rush to the pack and the tribe (stills: frames 112 and 212)
//   $env:CELLOUT_REEL_TEMPLATE = 'SHARK'   # optional: the creature editor stays open with this template (CreatureTemplates), end with --quit-after 300
//   $env:CELLOUT_REEL_MODE = 'Paint'   # optional, with a template: the editor shows this mode (Build, Paint, Animate) instead of the build mode
//   $env:CELLOUT_REEL_PART = '0'       # optional, with a mode: the index of the part picked in the editor
//   $env:CELLOUT_REEL_SKY = '1'       # optional, while rushing: the camera tilts flat, the still shows sky and horizon
//   $env:CELLOUT_REEL_GALAXY = '1'    # optional: only the title menu, the galaxy (still 58), then a planet up close (still 118), end with --quit-after 130; longer: a neighbor hovered (still 330), the start dives into the planet (still 410), --quit-after 420
//   $env:CELLOUT_REEL_PLANET = 'RUBRA' # optional: the planet of the game (Core/Planets.cs keys), with the galaxy the one shown up close
//   $env:CELLOUT_REEL_GALAXY_TOUR = '1' # optional, with the galaxy: a tour first, stills 70 (whole spiral), 100 (deep in an arm), 130 (Sgr A*), 142 (warp), 175 (planet), end with --quit-after 180
//   $env:CELLOUT_REEL_PHASE = 'Pack'  # optional: starts like the galaxy menu in this phase (StartPhase), the jumps run by themselves (still 238)
//   $env:CELLOUT_REEL_CIV = '1'       # optional, with the phase Civilization: buildings around the headquarters, four techs, the tech tree opens; the reel quits 130 frames after the city stands (stills: the last frame before the techs and the last frame)
//   $env:CELLOUT_REEL_SAIL = '1'      # optional, with CELLOUT_REEL_CIV: instead of the tech tree the chieftain stands in the sea with its retinue: the ship (last frame)
//   (CELLOUT_REEL_WIDE = '1' with CELLOUT_REEL_CIV: the chieftain looks at the headquarters from afar, zoomed out, no HUD)
//   $env:CELLOUT_REEL_LANDING = '1'   # optional, with CELLOUT_REEL_CIV: raiders from over the sea land by ship, the chieftain watches from the shore
//   $env:CELLOUT_REEL_HALL = '1'      # optional, with CELLOUT_REEL_CIV: instead of the tech tree the builder shows the headquarters with the modern blocks
//   $env:CELLOUT_REEL_TEMPLATES = '1' # optional, with CELLOUT_REEL_CIV: the builder with its template window (last frame)
//   $env:CELLOUT_REEL_PAUSE = '1'     # optional, with CELLOUT_REEL_CIV: the pause menu, with CELLOUT_REEL_LIBRARY = '1' its template library (last frame)
//   $env:CELLOUT_REEL_VISIT = '1'     # optional, with CELLOUT_REEL_CIV: instead of the tech tree the chieftain visits a neighbor over the sea
//   $env:CELLOUT_REEL_WIDE = '1'      # optional, while rushing: the camera zooms all the way out and the HUD hides, for looking at the scenery
//   $env:CELLOUT_REEL_MAP = '1'       # optional: the tribe opens the world map (rushing: still 198)
//   $env:CELLOUT_REEL_MENU = '1'      # optional: the tribe opens the build menu (rushing: still 198)
//   $env:CELLOUT_REEL_PLACE = '1'     # optional: the tribe places a storehouse, the build view over the plots of the settlement (rushing: still 198); with CELLOUT_REEL_CIV the build view over the city (last frame)
//   $env:CELLOUT_REEL_SAVE = '1'      # optional, with the tribe: saves the game (set CELLOUT_SAVE_DIR to a scratch folder!)
//   $env:CELLOUT_REEL_CONTINUE = 'TERRA' # optional: continues the saved game of that planet like the planet view (still 118), with CELLOUT_SAVE_DIR
//   $env:CELLOUT_REEL_FAN = '1'       # optional: the tribe gets a roar sac and a ram brow, key 3 opens the fan of hunt (rushing: still 198)
//   $env:CELLOUT_REEL_ABILITIES = '1' # optional: the single cell gets every ability and fires them (stills: frames 76, 86, 96, 104), end with --quit-after 118
//   $env:CELLOUT_REEL_ENCOUNTER = '1' # optional, with CELLOUT_REEL_TRIBE: the pack meets a foreign pack, courts (still 80) and displays (still 104)
//   $env:CELLOUT_REEL_WILD = '1'      # optional, with CELLOUT_REEL_TRIBE: the pack stands at a herd of prey (still 62) and in front of the lone beast (still 110)

//
//   Chapters:
//     PLAY THE REEL   Frame script from the protocell to the tribe.
//

using System.Linq;
using Godot;

namespace CellOut;

public partial class GraphicsReel : Node
{
    // ===========================================================================
    // ============================   PLAY THE REEL   ============================
    // ===========================================================================
    //
    //   1. Start    Instantiates the main scene and keeps running while it is paused.
    //   2. Script   One jump per stage, a second of play in between, quit.
    //

    private const int START_FRAME = 2;
    private const int SINGLE_FRAME = 60;
    private const int CLUSTER_FRAME = 120;
    private const int FULL_FRAME = 180;
    private const int EVOLVE_FRAME = 240;
    private const int SHAPE_FRAME = 250;
    private const int COAST_FRAME = 330;
    private const int LAND_FRAME = 400;
    private const int PACK_FRAME = 520;
    private const int TRIBE_FRAME = 620;
    private const int BUILD_FRAME = 630;            // tribe: stock by cheat, one building of every function
    private const int MAP_FRAME = 660;              // tribe, only with CELLOUT_REEL_MAP: the world map open
    private const int DESIGN_FRAME = 700;          // tribe: the village builder opens, full screen
    private const int QUIT_FRAME = 740;
    private const int RUSH_HOLD = 100;              // rushing: every stage poses this many frames (stills: frames 112, 198, 212)

    /// <summary>The last frame of every stage, the ones to look at.</summary>
    public static readonly int[] STILL_FRAMES = { 58, 118, 178, 238, 328, 398, 518, 618, 698, 738 };

    private static readonly bool IS_RUSHING_TRIBE = "1" == OS.GetEnvironment("CELLOUT_REEL_TRIBE");
    private static readonly bool IS_RUSHING = IS_RUSHING_TRIBE || "1" == OS.GetEnvironment("CELLOUT_REEL_LAND");
    private static readonly bool IS_SHOWING_MAP = "1" == OS.GetEnvironment("CELLOUT_REEL_MAP");
    private static readonly bool IS_SHOWING_MENU = "1" == OS.GetEnvironment("CELLOUT_REEL_MENU");
    private static readonly bool IS_PLACING = "1" == OS.GetEnvironment("CELLOUT_REEL_PLACE");
    private static readonly bool IS_SHOWING_FAN = "1" == OS.GetEnvironment("CELLOUT_REEL_FAN");
    private static readonly bool IS_SAVING = "1" == OS.GetEnvironment("CELLOUT_REEL_SAVE");
    private static readonly string CONTINUE_PLANET = OS.GetEnvironment("CELLOUT_REEL_CONTINUE");
    private static readonly bool IS_LOOKING_UP = "1" == OS.GetEnvironment("CELLOUT_REEL_SKY");
    private static readonly bool IS_WIDE = "1" == OS.GetEnvironment("CELLOUT_REEL_WIDE");
    private static readonly bool IS_SHOWING_GALAXY = "1" == OS.GetEnvironment("CELLOUT_REEL_GALAXY");
    private static readonly string GALAXY_PLANET = OS.GetEnvironment("CELLOUT_REEL_PLANET");
    private static readonly bool IS_TOURING_GALAXY = "1" == OS.GetEnvironment("CELLOUT_REEL_GALAXY_TOUR");
    private static readonly bool IS_SHOWING_CIV = "1" == OS.GetEnvironment("CELLOUT_REEL_CIV");
    private static readonly bool IS_SAILING = "1" == OS.GetEnvironment("CELLOUT_REEL_SAIL");
    private static readonly bool IS_LANDING = "1" == OS.GetEnvironment("CELLOUT_REEL_LANDING");
    private static readonly bool IS_SHOWING_HALL = "1" == OS.GetEnvironment("CELLOUT_REEL_HALL");
    private static readonly bool IS_VISITING = "1" == OS.GetEnvironment("CELLOUT_REEL_VISIT");
    private static readonly StartPhase? START_PHASE = System.Enum.TryParse(OS.GetEnvironment("CELLOUT_REEL_PHASE"), true, out StartPhase phase) ? phase : null;
    private static readonly string TEMPLATE_KEY = OS.GetEnvironment("CELLOUT_REEL_TEMPLATE");
    private static readonly bool IS_SHOWING_ABILITIES = "1" == OS.GetEnvironment("CELLOUT_REEL_ABILITIES");
    private static readonly bool IS_SHOWING_ENCOUNTER = "1" == OS.GetEnvironment("CELLOUT_REEL_ENCOUNTER");
    private static readonly bool IS_SHOWING_WILD = "1" == OS.GetEnvironment("CELLOUT_REEL_WILD");
    private static readonly CreatureEditorMode EDITOR_MODE = System.Enum.TryParse(OS.GetEnvironment("CELLOUT_REEL_MODE"), true, out CreatureEditorMode mode) ? mode : CreatureEditorMode.Build;

    private Main _main;
    private int _frame;

    // ---------------------------------------------------------------------------
    // Start
    //   Instantiates the main scene and keeps running while it is paused.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        TestFiles.KeepAwayFromPlayer();
        ProcessMode = ProcessModeEnum.Always;
        if (!string.IsNullOrEmpty(GALAXY_PLANET))
        {
            Planets.Choose(GALAXY_PLANET);    // the game runs on this planet (colors, islands)
        }
        _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _main);
    }

    // ---------------------------------------------------------------------------
    // Script
    //   One jump per stage, a second of play in between, quit.
    // ---------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        _frame++;
        Player player = _main.Player;
        if (null == player)
        {
            return;
        }
        player.Invulnerable = 5f;   // nothing eats the model while it poses

        // Continue a saved game like the planet view does, the still shows it restored
        if (!string.IsNullOrEmpty(CONTINUE_PLANET))
        {
            if (2 == _frame)
            {
                _main.IsAnimationSkipped = true;
                Planets.Choose(CONTINUE_PLANET);
                _main.FindChildren("*", "", true, false).OfType<StartFlow>().First().Continue(SaveSlots.Read(CONTINUE_PLANET));
            }
            if (120 == _frame)
            {
                GetTree().Quit();
            }
            return;
        }

        // A start phase of the galaxy menu: the game jumps there by itself, the creature is shaped with the first template
        if (null != START_PHASE)
        {
            if (2 == _frame)
            {
                _main.IsAnimationSkipped = true;
                _main.FindChildren("*", "", true, false).OfType<StartFlow>().First().StartIn(START_PHASE.Value);
            }
            if (_main.CreatureEditor.IsOpen)
            {
                _main.CreatureEditor.ChooseType(CreatureType.Fish);
                _main.CreatureEditor.Finish();
            }
            if (IS_SHOWING_CIV)
            {
                ShowCity();
            }
            bool isCityDone = IS_SHOWING_CIV && _cityFrame >= 0 && _frame - _cityFrame >= CITY_QUIT;
            if (IS_SHOWING_CIV ? isCityDone : 240 == _frame)
            {
                GetTree().Quit();
            }
            return;
        }

        // The galaxy menu: the Milky Way, then one planet up close, nothing starts
        if (IS_SHOWING_GALAXY)
        {
            ShowGalaxy();
            return;
        }

        // Rushing: straight through the jumps, then the land animal (or pack and tribe) poses until the camera has settled
        int frame = _frame;
        if (IS_RUSHING)
        {
            frame = _frame switch
            {
                2                  => START_FRAME,
                4                  => FULL_FRAME,
                6                  => EVOLVE_FRAME,
                10                 => SHAPE_FRAME,
                14                 => IS_RUSHING_TRIBE ? PACK_FRAME : LAND_FRAME,
                14 + RUSH_HOLD     => IS_RUSHING_TRIBE ? TRIBE_FRAME : QUIT_FRAME,
                24 + RUSH_HOLD     => BUILD_FRAME,
                54 + RUSH_HOLD     => MAP_FRAME,
                64 + RUSH_HOLD     => MAP_FRAME + 10,
                104 + RUSH_HOLD    => DESIGN_FRAME,
                14 + 2 * RUSH_HOLD => QUIT_FRAME,
                _                  => -1,
            };
        }

        // Looking up: a right drag tilts the third person camera flat, so the sky fills the upper half
        if (IS_LOOKING_UP && IS_RUSHING && 60 == _frame)
        {
            _main.FollowCamera.HandleInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
            _main.FollowCamera.HandleInput(new InputEventMouseMotion { Relative = new Vector2(120f, -200f) });
            _main.FollowCamera.HandleInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false });
        }
        // Wide: all the way out, no HUD in front of the scenery
        if (IS_WIDE && IS_RUSHING && 60 == _frame)
        {
            _main.FollowCamera.ZoomBy(20);
        }
        if (IS_WIDE && IS_RUSHING && null != _main.Hud)
        {
            _main.Hud.Visible = false;
        }
        if (IS_SHOWING_ABILITIES)
        {
            ShowAbilities(player);
        }
        if (IS_SHOWING_ENCOUNTER && IS_RUSHING_TRIBE)
        {
            ShowEncounter(player);
        }
        if (IS_SHOWING_WILD && IS_RUSHING_TRIBE)
        {
            ShowWild(player);
        }
        switch (frame)
        {
            case START_FRAME:
                _main.IsAnimationSkipped = true;
                _main.StartGame(isInstant: true);
                break;
            case SINGLE_FRAME:
                _main.CheatTo(CheatTarget.SingleCell);
                break;
            case CLUSTER_FRAME:
                _main.CheatTo(CheatTarget.Multicellular);
                break;
            case FULL_FRAME:
                _main.CheatTo(CheatTarget.FullCluster);
                break;
            case EVOLVE_FRAME:
                _main.StartEvolve(isInstant: true);
                break;
            case SHAPE_FRAME:
                if (_main.CreatureEditor.IsOpen && string.IsNullOrEmpty(TEMPLATE_KEY))
                {
                    _main.CreatureEditor.ChooseType(CreatureType.Fish);
                    _main.CreatureEditor.Finish();
                }
                else if (_main.CreatureEditor.IsOpen)
                {
                    // A template to look at: the editor stays open with it on the turntable, quit by --quit-after
                    _main.CreatureEditor.ChooseTemplate(CreatureTemplates.Get(TEMPLATE_KEY));
                    _main.CreatureEditor.SetMode(EDITOR_MODE);
                    if (CreatureEditorMode.Build == EDITOR_MODE)
                    {
                        _main.CreatureEditor.PickVertebra(1);    // the ring sliders of the form panel show
                    }
                    else
                    {
                        // The part icon of the color zones wakes up, the motion panel shows a limb (the last part of most templates)
                        _main.CreatureEditor.PickPart(int.TryParse(OS.GetEnvironment("CELLOUT_REEL_PART"), out int part) ? part
                            : CreatureEditorMode.Paint == EDITOR_MODE ? 0 : _main.CreatureEditor.Draft.Parts.Count - 1);
                    }
                }
                break;
            case COAST_FRAME:
                _main.CheatTo(CheatTarget.Coast);
                break;
            case LAND_FRAME:
                _main.CheatTo(CheatTarget.LandAnimal);
                break;
            case PACK_FRAME:
                _main.CheatTo(CheatTarget.Pack);
                break;
            case TRIBE_FRAME:
                _main.CheatTo(CheatTarget.Tribe);
                break;
            case BUILD_FRAME:
                _main.CheatTo(CheatTarget.Resources);
                _main.CheatTo(CheatTarget.Resources);
                _main.CheatTo(CheatTarget.Resources);
                foreach (BuildingKind kind in System.Enum.GetValues<BuildingKind>())
                {
                    _main.Tribe.TryBuild(kind);
                }
                break;
            case MAP_FRAME when IS_SAVING:
                _main.Saves.SaveNow();
                break;
            case MAP_FRAME when IS_SHOWING_FAN:
                // Two parts of the wild tab, then key 3 opens the fan of hunt
                var draft = new CreatureDesign();
                draft.CopyFrom(player.Creature);
                draft.TryPlace(CreaturePartKind.RoarSac, 0.6f, paidCost: 0);
                draft.TryPlace(CreaturePartKind.RamBrow, 0.8f, paidCost: 0);
                player.CommitCreature(draft, player.EvolutionPoints);
                break;
            case MAP_FRAME + 10 when IS_SHOWING_FAN:
                _main.Hud.PressWayKey(2);
                break;
            case MAP_FRAME when IS_PLACING:
                // The build view: a storehouse hangs over the plots of the settlement
                _main.CheatTo(CheatTarget.Resources);
                _main.Placer.Begin(BuildingKind.Storehouse);
                _main.Placer.Rotate(1);
                break;
            case MAP_FRAME when IS_SHOWING_MENU:
                _main.TribePanel.ToggleBuildMenu();
                break;
            case MAP_FRAME when IS_SHOWING_MAP:
                _main.Hub.ToggleWorldMap();
                break;
            case DESIGN_FRAME:
                _main.OpenBuildingEditor();
                _main.BuildingEditor.Session.ShowKind(BuildingKind.Watchtower);
                _main.BuildingEditor.Session.ShowKind(BuildingKind.Dwelling);
                _main.BuildingEditor.Session.PickPattern(3);
                _main.BuildingEditor.Session.Repaint(0);
                _main.BuildingEditor.Session.PickPattern(1);
                _main.BuildingEditor.Session.Repaint(1);
                _main.BuildingEditor.Session.Select(BlockKind.Totem);
                // The gizmo on the roof: lifted, tilted and grown a little
                _main.BuildingEditor.Session.Pick(1);
                _main.BuildingEditor.Session.MovePicked(new Vector3(0f, 0.4f, 0f));
                _main.BuildingEditor.Session.TurnPicked(Vector3.Back, 0.3f);
                _main.BuildingEditor.Session.GrowPicked(1.25f);
                _main.BuildingEditor.Session.CommitPose();
                break;
            case QUIT_FRAME:
                GetTree().Quit();
                break;
        }
    }

    /*
      The abilities, only with CELLOUT_REEL_ABILITIES, right after the single cell:
      - 66: every key ability plus electrocyte, toxin gland, light sail and filter mouth,
            three small cells around it
      - 72: dash, 78: slime, 82: a hit wells up the poison cloud, 90: cloak, 100: blast,
            the electrocyte fires by itself at the cells close by
    */
    /*
      The encounter with a foreign pack, only with CELLOUT_REEL_ENCOUNTER while rushing to the tribe:
      - 40: the player and its grown members walk up to the first foreign pack
      - 70: court (bow), 95: display (the pack rears up, the strangers duck)
    */
    private void ShowEncounter(Player player)
    {
        if (0 == _main.Rivals.Rivals.Count)
        {
            return;
        }
        RivalTribe stranger = _main.Rivals.Rivals[0];
        switch (_frame)
        {
            case 40:
                Kin animal = stranger.Kin[0];
                player.Position = animal.Position + Vector2.Left * player.BodySize() * 2.4f;
                player.Facing   = 0f;
                foreach (Kin member in _main.Pack.Kin)
                {
                    member.Position = player.Position + new Vector2(-1.5f, (float)GD.RandRange(-2.0, 2.0)) * player.BodySize();
                }
                break;
            case 70:
                _main.Encounter.Use(PackWay.Court);
                break;
            case 95:
                _main.Encounter.Use(PackWay.Display);
                break;
        }
    }

    /*
      The wildlife of the pack stage, only with CELLOUT_REEL_WILD while rushing to the tribe:
      - 40: the player and its members stand next to the first herd of prey (it runs)
      - 70: they stand in front of the lone beast at its lair, it attacks
    */
    private void ShowWild(Player player)
    {
        Kin stranger = _frame switch
        {
            40 => _main.Wildlife.Prey.FirstOrDefault(),
            70 => _main.Loners.Loners.FirstOrDefault()?.Kin.FirstOrDefault(),
            _  => null,
        };
        if (null == stranger)
        {
            return;
        }
        player.Position = stranger.Position + Vector2.Left * player.BodySize() * 4f;
        player.Facing   = 0f;
        foreach (Kin member in _main.Pack.Kin)
        {
            member.Position = player.Position + new Vector2(-1.5f, (float)GD.RandRange(-2.0, 2.0)) * player.BodySize();
        }
    }

    private void ShowAbilities(Player player)
    {
        switch (_frame)
        {
            case 66:
                var design = new CellDesign();
                design.TryPlace(PartKind.Flagellum, Vector2.Left, 0);
                design.TryPlace(PartKind.SlimeGland, new Vector2(-0.7f, 0.7f), 0);
                design.TryPlace(PartKind.Chromatophore, new Vector2(-0.7f, -0.7f), 0);
                design.TryPlace(PartKind.JetNozzle, Vector2.Right, 0);
                design.TryPlace(PartKind.Electrocyte, Vector2.Up, 0);
                design.TryPlace(PartKind.ToxinGland, Vector2.Down, 0);
                design.TryPlace(PartKind.LightSail, new Vector2(0.7f, 0.7f), 0);
                design.TryPlace(PartKind.FilterMouth, new Vector2(0.7f, -0.7f), 0);
                player.CommitDesign(design, player.EvolutionPoints);
                for (int i = 0; i < 3; i++)
                {
                    var cell = new Critter { Position = player.GlobalPosition + Vector2.FromAngle(i * 2.1f) * player.BodySize() * 2.6f };
                    cell.Setup(player.BodySize() * 0.6f, CritterKind.Amoeba, player.BodySize(), 1);
                    _main.CritterLayer.AddChild(cell);
                }
                break;
            case 72:
                _main.Abilities.UseSlot(0);
                break;
            case 78:
                _main.Abilities.UseSlot(1);
                break;
            case 82:
                player.WasHit = true;
                break;
            case 90:
                _main.Abilities.UseSlot(2);
                break;
            case 100:
                _main.Abilities.UseSlot(3);
                break;
        }
    }

    /// <summary>The title menu only: the galaxy, from frame 70 a planet up close; with CELLOUT_REEL_GALAXY_TOUR first a tour through the galaxy.</summary>
    private void ShowGalaxy()
    {
        TitleMenu  menu   = _main.FindChildren("*", "", true, false).OfType<TitleMenu>().FirstOrDefault();
        GalaxyView galaxy = _main.FindChildren("*", "", true, false).OfType<GalaxyView>().FirstOrDefault();
        Planet     planet = Planets.Get(string.IsNullOrEmpty(GALAXY_PLANET) ? "RUBRA" : GALAXY_PLANET);
        if (!IS_TOURING_GALAXY)
        {
            // The planet up close (still 118), a neighbor hovered (still 330), the start dives into the planet (still 410)
            if (70 == _frame)
            {
                menu?.ShowPlanet(planet);
            }
            else if (300 == _frame)
            {
                PlanetGlobe neighbor = _main.FindChildren("*", "", true, false).OfType<StarSystemView>().FirstOrDefault()?.GetChildren().OfType<PlanetGlobe>().FirstOrDefault(globe => new Rect2(40f, 40f, 680f, 600f).HasPoint(globe.GlobalPosition + globe.Size * 0.5f));   // one on the screen, left of the panel
                MoveMouse(null == neighbor ? Vector2.Zero : neighbor.GlobalPosition + neighbor.Size * 0.5f);
            }
            else if (400 == _frame)
            {
                menu?.Start(StartPhase.RnaWorld);
            }
            return;
        }
        // Tour: the star of the current planet hovered (still 35), whole spiral (still 70), deep in an arm with a star hovered (still 100),
        // Sgr A* hovered (still 130), the flight onto a planet (still 142, warp) and the planet (still 175)
        Vector2 inArm = Planets.All[0].GalaxySpot + new Vector2(0.03f, 0.02f);
        switch (_frame)
        {
            case 28:
                MoveMouse(galaxy?.ToScreen(Planets.Current.GalaxySpot) ?? Vector2.Zero);
                break;
            case 36:
                MoveMouse(Vector2.One * 5f);
                break;
            case 40:
                galaxy?.LookAt(Vector2.Zero, 0.42f);
                break;
            case 72:
                galaxy?.LookAt(inArm, 4.5f);
                break;
            case 96:
                StarSystem near = Galaxy.Systems.Where(system => null == system.PlanetKey).OrderBy(system => system.Spot.DistanceTo(inArm)).First();
                MoveMouse(galaxy?.ToScreen(near.Spot) ?? Vector2.Zero);
                break;
            case 102:
                galaxy?.LookAt(Vector2.Zero, 3.5f);
                break;
            case 126:
                MoveMouse(galaxy?.ToScreen(Vector2.Zero) ?? Vector2.Zero);
                break;
            case 133:
                menu?.ShowPlanet(planet);
                break;
        }
    }

    /// <summary>Moves the mouse like the player would, the GUI hovers what lies under it.</summary>
    private void MoveMouse(Vector2 at)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at });
    }

    private const int CITY_BUILD = 5;               // frames after the city stands: buildings around the headquarters
    private const int CITY_RESEARCH = 60;           // four techs (the city still is the frame before)
    private const int CITY_TREE = 90;               // the tech tree opens (still: 30 frames later)
    private const int CITY_QUIT = 130;

    private int _cityFrame = -1;

    /// <summary>The city of the civilization phase: buildings around the headquarters, then four techs and the open tech tree.</summary>
    private void ShowCity()
    {
        if (_cityFrame < 0 && _main.Civ.IsCity)
        {
            _cityFrame = _frame;
        }
        switch (_cityFrame < 0 ? -1 : _frame - _cityFrame)
        {
            case CITY_BUILD:
                _main.CheatTo(CheatTarget.Resources);
                _main.CheatTo(CheatTarget.Resources);
                foreach (BuildingKind kind in new[] { BuildingKind.Dwelling, BuildingKind.Market, BuildingKind.Storehouse, BuildingKind.Watchtower })
                {
                    _main.Tribe.TryBuild(kind);
                }
                break;
            case CITY_BUILD + 10 when IS_WIDE:
                // Wide: the chieftain stands back and looks at the headquarters, the camera zooms out
                Hut hall = _main.Tribe.Hall;
                _main.Player.GlobalPosition = hall.Position + Vector2.Right.Rotated(0.6f) * (hall.BlockRadius + _main.Player.BodySize() * 7f);
                _main.Player.Facing = (hall.Position - _main.Player.GlobalPosition).Angle();
                _main.FollowCamera.ZoomBy(8);
                _main.Hud.Visible = false;
                break;
            case CITY_BUILD + 20 when IS_LANDING:
                // Raiders of a tribe from over the sea land by ship, the chieftain watches from the shore
                RivalTribe over = _main.Rivals.Rivals.FirstOrDefault(rival => rival.IsSettled && !rival.Entry.IsEqualApprox(rival.Fire));
                if (null != over)
                {
                    _main.Rivals.ForceRaid(over);
                    Vector2 inland = (_main.Tribe.Hall.Position - over.Entry).Normalized();
                    _main.Player.GlobalPosition = over.Entry + inland * _main.Player.BodySize() * 8f;
                    _main.Player.Facing = (-inland).Angle();
                }
                break;
            case CITY_RESEARCH:
                _main.Civ.CheatKnowledge(80f);
                foreach (TechKind kind in new[] { TechKind.Cooling, TechKind.Steel, TechKind.Tractor, TechKind.Engine })
                {
                    _main.Civ.Research(kind);
                }
                break;
            case CITY_TREE when IS_VISITING:
                // A neighbor over the sea: the chieftain stands in front of its headquarters
                RivalTribe far = _main.Rivals.Rivals.FirstOrDefault(rival => rival.IsSettled && !rival.Entry.IsEqualApprox(rival.Fire));
                if (null != far)
                {
                    Hut farHall = far.Huts.FirstOrDefault(hut => BuildingKind.Hall == hut.Design.Kind);
                    float back  = (null == farHall ? 0f : farHall.BlockRadius) + _main.Player.BodySize() * 6f;
                    _main.Player.GlobalPosition = far.Fire + Vector2.Right.Rotated(2.4f) * back;
                    _main.Player.Facing = (far.Fire - _main.Player.GlobalPosition).Angle();
                }
                break;
            case CITY_TREE when IS_SHOWING_HALL:
                // The builder on the headquarters, the modern blocks picked
                _main.OpenBuildingEditor();
                _main.BuildingEditor.Session.ShowKind(BuildingKind.Hall);
                _main.BuildingEditor.Session.ShowEra(BlockEra.Modern);
                break;
            case CITY_TREE when "1" == OS.GetEnvironment("CELLOUT_REEL_TEMPLATES"):
                // The template window of the builder: village, modern and the library with one saved blueprint
                _main.OpenBuildingEditor();
                _main.BuildingEditor.Session.ShowKind(BuildingKind.Dwelling);
                BlueprintLibrary.Add("Mein Haus", BuildingTemplates.Build(BuildingKind.Dwelling, 2));
                _main.BuildingEditor.OpenTemplates();
                break;
            case CITY_TREE when "1" == OS.GetEnvironment("CELLOUT_REEL_PAUSE"):
                // The pause menu, with CELLOUT_REEL_LIBRARY its template library
                _main.Pause.Open();
                if ("1" == OS.GetEnvironment("CELLOUT_REEL_LIBRARY"))
                {
                    _main.Pause.OpenLibrary();
                }
                break;
            case CITY_TREE when IS_PLACING:
                // The build view over the city: its streets, sidewalks and paved ground
                _main.CheatTo(CheatTarget.Resources);
                _main.Placer.Begin(BuildingKind.Storehouse);
                break;
            case CITY_TREE when !IS_SAILING:
                _main.TechTree.Toggle();
                break;
            case CITY_TREE when IS_SAILING:
                // The retinue follows, the chieftain stands in the sea off the shore of its island: the ship
                _main.Pack.ToggleRetinue();
                Coast.Island home = _main.Coast.Islands[0];
                float angle = _main.Player.GlobalPosition.AngleToPoint(home.Center) + Mathf.Pi;
                _main.Player.GlobalPosition = home.Center + Vector2.FromAngle(angle) * (Coast.IslandRadius(home, angle) + _main.Player.BodySize() * 4f);
                _main.Player.Facing = angle;
                break;
        }
    }
}
