// NOTE:
//   An autopilot that plays the game for a while like a restless player, to shake out errors
//   that only show in long play: it starts in a phase of the galaxy menu (like the graphics
//   reel), then walks around with real key presses (WASD), calls and sends the retinue (F),
//   uses the ways (1, 2, 3), acts (Space), opens and closes map and tech tree (M, T), builds
//   when the stock allows, researches, provokes raids, sails out to the other islands and
//   saves the game. Every few seconds it logs what it did. Errors of the engine and of C#
//   land in the output: run it and search the output for ERROR.
//
//   $env:CELLOUT_PLAY_PHASE = 'Civilization'   # optional, StartPhase, defaults to Tribe
//   $env:CELLOUT_PLAY_SECONDS = '300'          # optional, seconds of game time, defaults to 240
//   $env:CELLOUT_SAVE_DIR = '<scratch>'        # recommended: the autopilot saves the game
//   $env:CELLOUT_PLAY_VERBOSE = '1'           # optional: every action in the output
//   godot --headless --path . res://tests/PlayTest.tscn

//
//   Chapters:
//     PLAY   Start, the loop of actions and the report at the end.
//

using System;
using System.Linq;
using Godot;

namespace CellOut;

public partial class PlayTest : Node
{
    // ===========================================================================
    // =================================   PLAY   ================================
    // ===========================================================================
    //
    //   1. Start    Main in the phase, time runs TIME_SCALE times as fast.
    //   2. Act      Every ACT_SECONDS one action of the restless player, keys held for a while.
    //   3. Report   What the game looks like at the end, then quit.
    //

    private static readonly StartPhase PHASE = Enum.TryParse(OS.GetEnvironment("CELLOUT_PLAY_PHASE"), true, out StartPhase phase) ? phase : StartPhase.Tribe;
    private static readonly float SECONDS = float.TryParse(OS.GetEnvironment("CELLOUT_PLAY_SECONDS"), out float seconds) ? seconds : 240f;
    private const float TIME_SCALE = 3f;
    private static readonly bool IS_VERBOSE = "1" == OS.GetEnvironment("CELLOUT_PLAY_VERBOSE");   // every action in the output, not every tenth
    private const float ACT_SECONDS = 1.5f;
    private const float WARM_UP = 8f;                // game seconds before the first action: the phase start runs its jumps

    private static readonly Key[] WALK_KEYS = { Key.W, Key.A, Key.S, Key.D };

    private Main _main;
    private float _time;
    private float _nextAct = WARM_UP;
    private int _actions;
    private readonly Random _dice = new(17);
    private Key? _held;

    // ---------------------------------------------------------------------------
    // Start
    //   Main in the phase, time runs TIME_SCALE times as fast.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        TestFiles.KeepAwayFromPlayer();
        ProcessMode = ProcessModeEnum.Always;
        _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _main);
        Engine.TimeScale = TIME_SCALE;
        GD.Print($"[PlayTest] Plays {PHASE} for {SECONDS} game seconds.");
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_time < 0.3f)
        {
            return;
        }
        if (!_isStarted)
        {
            _isStarted = true;
            _main.IsAnimationSkipped = true;
            _main.FindChildren("*", "", true, false).OfType<StartFlow>().First().StartIn(PHASE);
        }
        if (_main.CreatureEditor.IsOpen)
        {
            _main.CreatureEditor.ChooseType(CreatureType.Fish);
            _main.CreatureEditor.Finish();
        }
        if (_main.BuildingEditor.IsOpen)
        {
            _main.BuildingEditor.Finish();
        }

        // ---------------------------------------------------------------------------
        // Act
        //   Every ACT_SECONDS one action of the restless player, keys held for a while.
        // ---------------------------------------------------------------------------

        if (_time >= _nextAct)
        {
            _nextAct = _time + ACT_SECONDS;
            Act();
        }

        // ---------------------------------------------------------------------------
        // Report
        //   What the game looks like at the end, then quit.
        // ---------------------------------------------------------------------------

        if (_time >= SECONDS)
        {
            Player player = _main.Player;
            GD.Print($"[PlayTest] Done after {_actions} actions: stage {player.Stage}, dead {player.IsDead}, buildings {_main.Tribe.Huts.Count}, "
                + $"food {_main.Tribe.Economy.Food}, wood {_main.Tribe.Economy.Wood}, techs {_main.Civ.Researched.Count}, members {_main.Pack.Members}, "
                + $"rivals {_main.Rivals.Rivals.Count} (defeated {_main.Rivals.Rivals.Count(rival => rival.IsDefeated)}).");
            Engine.TimeScale = 1f;
            GetTree().Quit();
        }
    }

    private bool _isStarted;

    /// <summary>One action of the restless player, chosen by the dice.</summary>
    private void Act()
    {
        _actions++;
        Player player = _main.Player;
        if (player.IsDead)
        {
            GD.Print("[PlayTest] The player died, restarting in the phase.");
            return;
        }
        Release();
        int roll = _dice.Next(20);
        string action;
        switch (roll)
        {
            case < 7:
                _held = WALK_KEYS[_dice.Next(WALK_KEYS.Length)];
                Send(_held.Value, isPressed: true);
                action = $"walk {_held}";
                break;
            case 7:
                Tap(KEY_RETINUE);
                action = "retinue";
                break;
            case 8 or 9:
                Key way = new[] { Godot.Key.Key1, Godot.Key.Key2, Godot.Key.Key3 }[_dice.Next(3)];
                Tap(way);
                action = $"way {way}";
                break;
            case 10:
                Tap(Godot.Key.Space);
                action = "act";
                break;
            case 11:
                Tap(Godot.Key.M);
                Tap(Godot.Key.M);
                action = "map";
                break;
            case 12:
                Tap(Godot.Key.T);
                Tap(Godot.Key.T);
                action = "tech tree";
                break;
            case 13:
                _main.CheatTo(CheatTarget.Resources);
                BuildingKind kind = new[] { BuildingKind.Dwelling, BuildingKind.Market, BuildingKind.Storehouse, BuildingKind.Watchtower, BuildingKind.Totem, BuildingKind.Barracks }[_dice.Next(6)];
                action = $"build {kind}: {_main.Tribe.TryBuild(kind)}";
                break;
            case 14:
                _main.Civ.CheatKnowledge(30f);
                TechKind? open = TechTree.TECHS.Select(tech => (TechKind?)tech.Kind).FirstOrDefault(tech => TechTree.CanResearch(tech.Value, _main.Civ.Researched, _main.Civ.Knowledge));
                action = $"research {open}: {(open is TechKind tech && _main.Civ.Research(tech))}";
                break;
            case 15:
                RivalTribe rival = _main.Rivals.Rivals.Where(each => each.IsSettled && !each.IsDefeated).OrderBy(_ => _dice.Next()).FirstOrDefault();
                if (null != rival)
                {
                    _main.Rivals.ForceRaid(rival);
                }
                action = $"raid by {rival?.NameKey}";
                break;
            case 16:
                // Out to sea and over to another island, the retinue on board if the city sails
                Coast coast = _main.Coast;
                if (null == coast || coast.Islands.Count < 2)
                {
                    action = "sail: no coast";
                    break;
                }
                Coast.Island island = coast.Islands[1 + _dice.Next(coast.Islands.Count - 1)];
                Vector2 way2 = (island.Center - player.GlobalPosition).Normalized();
                player.GlobalPosition = island.Center - way2 * (Coast.IslandRadius(island, way2.Angle() + Mathf.Pi) + player.BodySize() * 3f);
                player.Facing = way2.Angle();
                _held = Godot.Key.W;
                Send(Godot.Key.W, isPressed: true);
                action = $"sail to island at {island.Center}";
                break;
            case 17:
                _main.FindChildren("*", "", true, false).OfType<SaveKeeper>().FirstOrDefault()?.SaveNow();
                action = "save";
                break;
            case 18:
                if (null != _main.Tribe.Hall)
                {
                    player.GlobalPosition = _main.Tribe.Hall.Position;     // straight into the headquarters: it must push out
                }
                action = "into the headquarters";
                break;
            default:
                Tap(Godot.Key.Space);
                _main.CheatTo(CheatTarget.Resources);
                action = "act with stock";
                break;
        }
        if (IS_VERBOSE || 0 == _actions % 10)
        {
            GD.Print($"[PlayTest] t={_time:F0}s stage {player.Stage} members {_main.Pack.Members} buildings {_main.Tribe.Huts.Count} last: {action}");
        }
    }

    private const Key KEY_RETINUE = Godot.Key.F;

    private static void Send(Key key, bool isPressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = isPressed });
    }

    private static void Tap(Key key)
    {
        Send(key, isPressed: true);
        Send(key, isPressed: false);
    }

    private void Release()
    {
        if (_held is Key held)
        {
            Send(held, isPressed: false);
            _held = null;
        }
    }
}
