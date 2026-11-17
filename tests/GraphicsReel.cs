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
//   $env:CELLOUT_REEL_GALAXY = '1'    # optional: only the title menu, the galaxy (still 58), then a planet up close (still 118), end with --quit-after 130
//   $env:CELLOUT_REEL_PLANET = 'RUBRA' # optional: the planet of the game (Core/Planets.cs keys), with the galaxy the one shown up close
//   $env:CELLOUT_REEL_PHASE = 'Pack'  # optional: starts like the galaxy menu in this phase (StartPhase), the jumps run by themselves (still 238)
//   $env:CELLOUT_REEL_WIDE = '1'      # optional, while rushing: the camera zooms all the way out and the HUD hides, for looking at the scenery
//   $env:CELLOUT_REEL_MAP = '1'       # optional: the tribe opens the world map (rushing: still 198)
//   $env:CELLOUT_REEL_MENU = '1'      # optional: the tribe opens the build menu (rushing: still 198)
//   $env:CELLOUT_REEL_FAN = '1'       # optional: the tribe gets a roar sac and a ram brow, key 3 opens the fan of hunt (rushing: still 198)
//   $env:CELLOUT_REEL_ABILITIES = '1' # optional: the single cell gets every ability and fires them (stills: frames 76, 86, 96, 104), end with --quit-after 118
//   $env:CELLOUT_REEL_ENCOUNTER = '1' # optional, with CELLOUT_REEL_TRIBE: the pack meets a foreign pack, courts (still 80) and displays (still 104)

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
    private static readonly bool IS_SHOWING_FAN = "1" == OS.GetEnvironment("CELLOUT_REEL_FAN");
    private static readonly bool IS_LOOKING_UP = "1" == OS.GetEnvironment("CELLOUT_REEL_SKY");
    private static readonly bool IS_WIDE = "1" == OS.GetEnvironment("CELLOUT_REEL_WIDE");
    private static readonly bool IS_SHOWING_GALAXY = "1" == OS.GetEnvironment("CELLOUT_REEL_GALAXY");
    private static readonly string GALAXY_PLANET = OS.GetEnvironment("CELLOUT_REEL_PLANET");
    private static readonly StartPhase? START_PHASE = System.Enum.TryParse(OS.GetEnvironment("CELLOUT_REEL_PHASE"), true, out StartPhase phase) ? phase : null;
    private static readonly string TEMPLATE_KEY = OS.GetEnvironment("CELLOUT_REEL_TEMPLATE");
    private static readonly bool IS_SHOWING_ABILITIES = "1" == OS.GetEnvironment("CELLOUT_REEL_ABILITIES");
    private static readonly bool IS_SHOWING_ENCOUNTER = "1" == OS.GetEnvironment("CELLOUT_REEL_ENCOUNTER");
    private static readonly CreatureEditorMode EDITOR_MODE = System.Enum.TryParse(OS.GetEnvironment("CELLOUT_REEL_MODE"), true, out CreatureEditorMode mode) ? mode : CreatureEditorMode.Build;

    private Main _main;
    private int _frame;

    // ---------------------------------------------------------------------------
    // Start
    //   Instantiates the main scene and keeps running while it is paused.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
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
            if (240 == _frame)
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

    /// <summary>The title menu only: the galaxy, from frame 70 a planet up close.</summary>
    private void ShowGalaxy()
    {
        if (70 != _frame)
        {
            return;
        }
        TitleMenu menu = _main.FindChildren("*", "", true, false).OfType<TitleMenu>().FirstOrDefault();
        menu?.ShowPlanet(Planets.Get(string.IsNullOrEmpty(GALAXY_PLANET) ? "RUBRA" : GALAXY_PLANET));
    }
}