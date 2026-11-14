// NOTE:
//   Jumps through every stage for a movie render, so the look of the game can be checked
//   by eye: protocell, single cell, cell cluster, organism in the sea, coast, land animal,
//   pack at the nest, tribe at the fire from the strategy view.
//   No checks, the frames are for the eye. Quits by itself. The stills worth looking at
//   are the last frames before each jump (STILL_FRAMES).
//
//   godot --path . res://tests/GraphicsReel.tscn --write-movie <dir>/f.png --fixed-fps 30
//   $env:CELLOUT_GRAPHICS = 'Low'      # optional: force a graphics tier (Low, Medium, High)
//   $env:CELLOUT_REEL_LAND = '1'       # optional: rush to the land animal, only the last still is worth a look
//   $env:CELLOUT_REEL_TRIBE = '1'      # optional: rush to the pack and the tribe (stills: frames 112 and 212)

//
//   Chapters:
//     PLAY THE REEL   Frame script from the protocell to the tribe.
//

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
    private const int QUIT_FRAME = 740;
    private const int RUSH_HOLD = 100;              // rushing: every stage poses this many frames (stills: frames 112, 212)

    /// <summary>The last frame of every stage, the ones to look at.</summary>
    public static readonly int[] STILL_FRAMES = { 58, 118, 178, 238, 328, 398, 518, 618, 738 };

    private static readonly bool IS_RUSHING_TRIBE = "1" == OS.GetEnvironment("CELLOUT_REEL_TRIBE");
    private static readonly bool IS_RUSHING = IS_RUSHING_TRIBE || "1" == OS.GetEnvironment("CELLOUT_REEL_LAND");

    private Main _main;
    private int _frame;

    // ---------------------------------------------------------------------------
    // Start
    //   Instantiates the main scene and keeps running while it is paused.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
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
                14 + 2 * RUSH_HOLD => QUIT_FRAME,
                _                  => -1,
            };
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
                if (_main.CreatureEditor.IsOpen)
                {
                    _main.CreatureEditor.ChooseType(CreatureType.Fish);
                    _main.CreatureEditor.Finish();
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
            case QUIT_FRAME:
                GetTree().Quit();
                break;
        }
    }
}
