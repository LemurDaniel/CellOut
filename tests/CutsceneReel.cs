// NOTE:
//   Plays the cutscenes one after the other for a movie render: intro, the single cell
//   stage with its editor transition, the multicellular stage with its editor transition.
//   No checks, the frames are for the eye. Quits by itself.
//
//   godot --path . res://tests/CutsceneReel.tscn --write-movie <dir>/f.png --fixed-fps 30

//
//   Chapters:
//     PLAY THE REEL   Frame script from the intro to the second editor.
//

using Godot;

namespace CellOut;

public partial class CutsceneReel : Node
{
    // ===========================================================================
    // ============================   PLAY THE REEL   ============================
    // ===========================================================================
    //
    //   1. Start    Instantiates the main scene and keeps running while it is paused.
    //   2. Script   Intro, single cell, editor, multicellular, editor, quit.
    //

    private const int INTRO_FRAME = 2;
    private const int SINGLE_FRAME = 200;           // the intro takes 6 s = 180 frames
    private const int CLOSE_FIRST_FRAME = 330;
    private const int MULTI_FRAME = 350;
    private const int CLOSE_SECOND_FRAME = 480;
    private const int QUIT_FRAME = 520;

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
    //   Intro, single cell, editor, multicellular, editor, quit.
    // ---------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        _frame++;
        Player player = _main.Player;
        if (null == player)
        {
            return;
        }
        switch (_frame)
        {
            case INTRO_FRAME:
                _main.StartGame(isPreludeSkipped: true);
                break;
            case SINGLE_FRAME:
                player.AddBiomass(Balance.Current.Player.SingleCellAt, BiomassSource.Raw);
                break;
            case CLOSE_FIRST_FRAME:
            case CLOSE_SECOND_FRAME:
                if (_main.Editor.IsOpen)
                {
                    _main.Editor.Finish();
                }
                break;
            case MULTI_FRAME:
                player.AddBiomass(Mathf.Max(0f, player.NextDivisionAt - player.Biomass), BiomassSource.Raw);
                player.Divide();
                break;
            case QUIT_FRAME:
                GetTree().Quit();
                break;
        }
    }
}
