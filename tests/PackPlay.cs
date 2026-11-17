// NOTE:
//   A goal-directed autopilot for the pack stage: where tests/PlayTest.cs presses keys at
//   random to shake out errors, this one plays towards the tribe like a player with a
//   plan, to measure the pace of the stage. It starts in the phase Pack and follows one
//   persona until the tribe is founded (or the time is up), then prints a timeline:
//     Forager     eats the closest land food.
//     Hunter      hunts the closest foreign animal or prey (key 3), eats the meat.
//     Courter     courts the closest foreign animal (key 1), eats what lies around.
//     Displayer   displays at the closest foreign animal (key 2), eats what lies around.
//   Headless, the time runs TIME_SCALE times as fast.
//
//   $env:CELLOUT_PLAY_PERSONA = 'Hunter'      # optional, defaults to Forager
//   $env:CELLOUT_PLAY_SECONDS = '600'         # optional, game seconds at most, defaults to 600
//   $env:CELLOUT_PLAY_SHOTS = '<folder>'      # optional, with a window (no --headless): a screenshot every 4 game seconds, the time runs twice as fast
//   godot --headless --path . res://tests/PackPlay.tscn

//
//   Chapters:
//     PLAY   Start, the plan of the persona per physics frame and the report.
//

using System;
using System.Linq;
using Godot;

namespace CellOut;

public partial class PackPlay : Node
{
    // ===========================================================================
    // =================================   PLAY   ================================
    // ===========================================================================
    //
    //   1. Start    Main in the phase Pack, time runs TIME_SCALE times as fast.
    //   2. Plan     Every physics frame the persona picks where to walk and which way to use.
    //   3. Report   A line every REPORT_SECONDS, the summary at the end, then quit.
    //

    private enum Persona { Forager, Hunter, Courter, Displayer }

    private static readonly Persona PERSONA = Enum.TryParse(OS.GetEnvironment("CELLOUT_PLAY_PERSONA"), true, out Persona persona) ? persona : Persona.Forager;
    private static readonly float SECONDS = float.TryParse(OS.GetEnvironment("CELLOUT_PLAY_SECONDS"), out float seconds) ? seconds : 600f;
    private static readonly string SHOTS = OS.GetEnvironment("CELLOUT_PLAY_SHOTS");
    private static readonly float TIME_SCALE = string.IsNullOrEmpty(SHOTS) ? 4f : 2f;
    private const float SHOT_SECONDS = 4f;
    private const float WARM_UP = 6f;                // game seconds before the plan starts: the phase start runs its jumps
    private const float REPORT_SECONDS = 30f;
    private const float REST_HEALTH = 0.35f;         // below this share of health the persona walks home to the nest

    private Main _main;
    private float _time;
    private float _nextReport = WARM_UP;
    private float _nextShot = WARM_UP;
    private bool _isStarted;
    private float _startBiomass;
    private int _takeovers;                          // the leader died and lives on in a member
    private int _members;

    // ---------------------------------------------------------------------------
    // Start
    //   Main in the phase Pack, time runs TIME_SCALE times as fast.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        TestFiles.KeepAwayFromPlayer();
        ProcessMode = ProcessModeEnum.Always;
        _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _main);
        Engine.TimeScale = TIME_SCALE;
        GD.Print($"[PackPlay] Plays the pack as {PERSONA} for at most {SECONDS} game seconds.");
    }

    public override void _PhysicsProcess(double delta)
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
            _main.FindChildren("*", "", true, false).OfType<StartFlow>().First().StartIn(StartPhase.Pack);
        }
        if (_main.CreatureEditor.IsOpen)
        {
            _main.CreatureEditor.ChooseType(CreatureType.Fish);
            _main.CreatureEditor.Finish();
        }
        Player player = _main.Player;
        if (_time < WARM_UP || null == _main.Pack.Nest)
        {
            _startBiomass = player.Biomass;
            return;
        }

        // ---------------------------------------------------------------------------
        // Plan
        //   Every physics frame the persona picks where to walk and which way to use.
        // ---------------------------------------------------------------------------

        if (player.IsDead)
        {
            Finish("the leader died");
            return;
        }
        if (player.Invulnerable > 1.5f && _main.Pack.Members < _members)
        {
            _takeovers++;      // a member is gone and the leader has the grace period of a revival
        }
        _members = _main.Pack.Members;
        player.Autopilot = Plan(player);

        // ---------------------------------------------------------------------------
        // Report
        //   A line every REPORT_SECONDS, the summary at the end, then quit.
        // ---------------------------------------------------------------------------

        if (!string.IsNullOrEmpty(SHOTS) && _time >= _nextShot)
        {
            _nextShot = _time + SHOT_SECONDS;
            GetViewport().GetTexture().GetImage().SavePng($"{SHOTS}/{PERSONA}-{(int)_time:D4}.png");
        }
        if (_time >= _nextReport)
        {
            _nextReport = _time + REPORT_SECONDS;
            GD.Print($"[PackPlay] {Status(player)}");
        }
        if (player.IsTribal)
        {
            Finish("the tribe is founded");
        }
        else if (_time >= SECONDS)
        {
            Finish("the time is up");
        }
    }

    /*
      The plan of a frame, the first that applies:
      > Hurt badly: home to the nest, it heals.
      > Grown enough: home to the nest, Space founds the tribe.
      > A wild conspecific in call range: Space calls it.
      > The persona's way: walk to the closest foreign animal (hunters also to prey) and use
        the way once it is the target; food on the way is eaten by walking over it.
      > Otherwise: the closest land food, without any a walk around the nest.
    */
    private Vector2 Plan(Player player)
    {
        PackKeeper pack = _main.Pack;
        float      size = player.BodySize();
        if (player.Hp < player.MaxHp * REST_HEALTH)
        {
            return Towards(player, pack.Nest.Position, size);
        }
        if (PackOffer.FoundTribe == pack.Offer || PackOffer.Call == pack.Offer)
        {
            pack.Act();
        }
        if (player.Biomass >= pack.TribeBiomass)
        {
            return Towards(player, pack.Nest.Position, size);
        }

        Food food = _main.FoodLayer.GetChildren().OfType<Food>()
            .Where(each => FoodHabitat.Land == each.Habitat && !each.IsEngulfed && !each.IsQueuedForDeletion())
            .OrderBy(each => each.Position.DistanceSquaredTo(player.GlobalPosition))
            .FirstOrDefault();
        bool isFoodClose = null != food && food.Position.DistanceTo(player.GlobalPosition) < size * 6f;
        if (Persona.Forager != PERSONA && !isFoodClose)
        {
            // The way of the persona, on the packs it still has something to win from (hunters also on prey)
            PackWay way = Persona.Hunter == PERSONA ? PackWay.Hunt : Persona.Courter == PERSONA ? PackWay.Court : PackWay.Display;
            WaysBalance ways = Balance.Current.Tribe.Ways;
            bool IsOpen(RivalTribe rival) => !rival.IsDefeated && PERSONA switch
            {
                Persona.Courter   => rival.Relation < ways.AllyAt || rival.Released < ways.AdoptPerPack,
                Persona.Displayer => !rival.IsRespectful,
                _                 => true,
            };
            if (null != _main.Encounter.Target && IsOpen(_main.Encounter.TargetPack) || Persona.Hunter == PERSONA && null != _main.Encounter.Prey)
            {
                _main.Encounter.Use(way);
            }
            Kin stranger = _main.Rivals.Everyone.Where(IsOpen)
                .SelectMany(rival => rival.Kin)
                .Concat(Persona.Hunter == PERSONA ? _main.Wildlife.Prey : Enumerable.Empty<Kin>())
                .Where(GodotObject.IsInstanceValid)
                .OrderBy(animal => animal.GlobalPosition.DistanceSquaredTo(player.GlobalPosition))
                .FirstOrDefault();
            if (null != stranger)
            {
                return Towards(player, stranger.GlobalPosition, size * 0.5f);
            }
        }
        if (null != food)
        {
            return Towards(player, food.Position, 0f);
        }
        return Towards(player, pack.Nest.Position + Vector2.FromAngle(_time * 0.2f) * size * 14f, size);
    }

    /// <summary>The direction to a point, zero once within the reach.</summary>
    private static Vector2 Towards(Player player, Vector2 point, float reach)
    {
        Vector2 way = point - player.GlobalPosition;
        return way.Length() <= reach ? Vector2.Zero : way.Normalized();
    }

    private string Status(Player player)
    {
        PackKeeper pack = _main.Pack;
        float share = (player.Biomass - _startBiomass) / Mathf.Max(pack.TribeBiomass - _startBiomass, 1f);
        string rivals = string.Join(", ", _main.Rivals.Everyone.Select(rival => $"{rival.Species} rel {rival.Relation:F2} resp {rival.Respect:F2} kin {rival.Kin.Count}{(rival.IsDefeated ? " gone" : "")}"));
        return $"t={_time:F0}s goal {share * 100f:F0}% hp {player.Hp / player.MaxHp * 100f:F0}% diet {player.Diet:F2} members {pack.Members} takeovers {_takeovers} prey {_main.Wildlife.Prey.Count} | {rivals}";
    }

    private void Finish(string reason)
    {
        GD.Print($"[PackPlay] Done as {PERSONA}: {reason} after {_time:F0}s. {Status(_main.Player)}");
        Engine.TimeScale = 1f;
        SetPhysicsProcess(false);
        GetTree().Quit();
    }
}
