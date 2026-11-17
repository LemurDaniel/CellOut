// NOTE:
//   A goal-directed autopilot for the tribe, the sibling of tests/PackPlay.cs: it starts in
//   the phase Pack, eats itself into the diet of a path, founds the tribe and then plays
//   towards the city and the goal of its path like a player with a plan, to measure the
//   pace of the stage. It builds whenever the stock pays for the next building of its
//   plan, founds the city at the fire as soon as it may, and between that it visits the
//   neighbor on its own island with its retinue and uses the way of its path:
//     Pacifist      courts (key 1) and gives food.
//     Territorial   displays (key 2).
//     Warlike       hunts and razes (key 3).
//   Headless, the time runs TIME_SCALE times as fast. It prints a line every REPORT_SECONDS.
//
//   $env:CELLOUT_PLAY_PERSONA = 'Warlike'     # optional, a TribePath, defaults to Territorial
//   $env:CELLOUT_PLAY_SECONDS = '900'         # optional, game seconds at most, defaults to 900
//   $env:CELLOUT_PLAY_SHOTS = '<folder>'      # optional, with a window (no --headless): a screenshot every 6 game seconds
//   godot --headless --path . res://tests/TribePlay.tscn

//
//   Chapters:
//     PLAY   Start, the plan per physics frame and the report.
//

using System;
using System.Linq;
using Godot;

namespace CellOut;

public partial class TribePlay : Node
{
    // ===========================================================================
    // =================================   PLAY   ================================
    // ===========================================================================
    //
    //   1. Start    Main in the phase Pack, the diet of the path, then the tribe.
    //   2. Plan     Every physics frame: build, found the city, visit the neighbor, use the way.
    //   3. Report   A line every REPORT_SECONDS, the summary at the end, then quit.
    //

    private static readonly TribePath PATH = Enum.TryParse(OS.GetEnvironment("CELLOUT_PLAY_PERSONA"), true, out TribePath path) ? path : TribePath.Territorial;
    private static readonly float SECONDS = float.TryParse(OS.GetEnvironment("CELLOUT_PLAY_SECONDS"), out float seconds) ? seconds : 900f;
    private static readonly string SHOTS = OS.GetEnvironment("CELLOUT_PLAY_SHOTS");
    private static readonly float TIME_SCALE = string.IsNullOrEmpty(SHOTS) ? 4f : 2f;
    private const float SHOT_SECONDS = 6f;
    private const float WARM_UP = 6f;                // game seconds before the plan starts: the phase start runs its jumps
    private const float REPORT_SECONDS = 60f;
    private const float REST_HEALTH = 0.35f;         // below this share of health the chieftain walks home
    private const float VISIT_SECONDS = 45f;         // it visits the neighbor this long, then it looks after the village as long

    /// <summary>What the persona builds, in this order, again from the start once all stand.</summary>
    private static readonly BuildingKind[] PLAN = PATH switch
    {
        TribePath.Pacifist => new[] { BuildingKind.Dwelling, BuildingKind.Storehouse, BuildingKind.Totem, BuildingKind.Market, BuildingKind.Dwelling },
        TribePath.Warlike  => new[] { BuildingKind.Dwelling, BuildingKind.Barracks, BuildingKind.Storehouse, BuildingKind.Barracks, BuildingKind.Watchtower },
        _                  => new[] { BuildingKind.Dwelling, BuildingKind.Storehouse, BuildingKind.Watchtower, BuildingKind.Totem, BuildingKind.Dwelling },
    };

    private Main _main;
    private float _time;
    private float _nextReport;
    private float _nextShot;
    private bool _isStarted;
    private bool _isFed;
    private float _tribeAt = -1f;
    private float _cityAt = -1f;
    private float _wonAt = -1f;
    private int _built;

    // ---------------------------------------------------------------------------
    // Start
    //   Main in the phase Pack, the diet of the path, then the tribe.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        TestFiles.KeepAwayFromPlayer();
        ProcessMode = ProcessModeEnum.Always;
        _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _main);
        Engine.TimeScale = TIME_SCALE;
        GD.Print($"[TribePlay] Plays the tribe on the {PATH} path for at most {SECONDS} game seconds.");
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
            return;
        }
        if (!_isFed)
        {
            // The meals of the path lock it when the tribe is founded right after
            _isFed = true;
            // Meat and plants in equal parts for the territorial path: the diet of the cheat start is not always balanced
            float meal = player.Biomass * 0.05f;
            player.AddBiomass(TribePath.Pacifist == PATH ? meal : TribePath.Warlike == PATH ? 0f : meal * 0.5f, BiomassSource.Plant);
            player.AddBiomass(TribePath.Warlike == PATH ? meal : TribePath.Pacifist == PATH ? 0f : meal * 0.5f, BiomassSource.Meat);
            return;
        }
        if (!player.IsTribal)
        {
            if (TribeWays.PathOf(player.Diet) != PATH && _time < WARM_UP + 20f)
            {
                _isFed = false;      // the diet is eased in, another meal until the path fits
                return;
            }
            _main.CheatTo(CheatTarget.Tribe);
            _tribeAt = _time;
            GD.Print($"[TribePlay] Tribe founded on the {_main.Tribe.Path} path with {_main.Pack.Members} member(s).");
            return;
        }

        // ---------------------------------------------------------------------------
        // Plan
        //   Every physics frame: build, found the city, visit the neighbor, use the way.
        // ---------------------------------------------------------------------------

        if (player.IsDead)
        {
            Finish("the chieftain died");
            return;
        }
        player.Autopilot = Plan(player);

        // ---------------------------------------------------------------------------
        // Report
        //   A line every REPORT_SECONDS, the summary at the end, then quit.
        // ---------------------------------------------------------------------------

        if (_cityAt < 0f && _main.Civ.IsCity)
        {
            _cityAt = _time;
            GD.Print($"[TribePlay] City founded {_cityAt - _tribeAt:F0}s after the tribe. {Status(player)}");
        }
        if (_wonAt < 0f && _main.Diplomacy.IsWon)
        {
            _wonAt = _time;
            GD.Print($"[TribePlay] Path won {_wonAt - _tribeAt:F0}s after the tribe. {Status(player)}");
        }
        if (!string.IsNullOrEmpty(SHOTS) && _time >= _nextShot)
        {
            _nextShot = _time + SHOT_SECONDS;
            GetViewport().GetTexture().GetImage().SavePng($"{SHOTS}/{PATH}-{(int)_time:D4}.png");
        }
        if (_time >= _nextReport)
        {
            _nextReport = _time + REPORT_SECONDS;
            GD.Print($"[TribePlay] {Status(player)}");
        }
        if (_time >= SECONDS)
        {
            Finish("the time is up");
        }
    }

    /*
      The plan of a frame:
      > The stock pays for the next building of the plan: build it (the next free slot around the fire).
      > The city may be founded: home to the fire, Space founds it.
      > Hurt badly: home to the fire.
      > Half of the time, with a neighbor on the own island that still counts for the goal:
        call the retinue, walk to its closest villager (or building) and use the way of the
        path once it is in reach; pacifists also give food while the stock allows it.
      > Otherwise: around the fire, the retinue back at work, so fruit falls where the villagers gather.
    */
    private Vector2 Plan(Player player)
    {
        PackKeeper     pack  = _main.Pack;
        TribeCommander tribe = _main.Tribe;
        float          size  = player.BodySize();
        Vector2        fire  = pack.Nest.Position;

        // Once enough buildings stand for a city it saves the stock for the headquarters
        bool isSaving = !_main.Civ.IsCity && _main.Civ.Buildings >= Balance.Current.Civ.FoundBuildings;
        if (!isSaving && tribe.TryBuild(PLAN[_built % PLAN.Length]))
        {
            _built++;
        }
        if (PackOffer.FoundCity == pack.Offer)
        {
            pack.Act();
        }
        if (_main.Civ.CanFound || player.Hp < player.MaxHp * REST_HEALTH)
        {
            return Towards(player, fire, size * 2f);
        }

        RivalTribe neighbor = _main.Rivals.Rivals
            .Where(rival => rival.IsSettled && !rival.IsDefeated && rival.Entry.IsEqualApprox(rival.Fire))
            .FirstOrDefault(rival => !TribeWays.IsDone(tribe.Path, new RivalStanding(rival.Relation, rival.Respect, rival.IsDefeated)));
        bool isVisiting = null != neighbor && 0 == (int)(_time / VISIT_SECONDS) % 2;
        if (isVisiting != pack.Retinue > 0)
        {
            pack.ToggleRetinue();
        }
        if (!isVisiting)
        {
            return Towards(player, fire + Vector2.FromAngle(_time * 0.15f) * size * 5f, size);
        }

        PackWay way = TribeWays.WayOf(tribe.Path);
        if (TribePath.Pacifist == tribe.Path && tribe.Economy.Food > 20)
        {
            _main.Rivals.Gift(neighbor);
        }
        if (_main.Encounter.TargetPack == neighbor || PackWay.Hunt == way)
        {
            _main.Encounter.Use(way);
        }
        Kin villager = neighbor.Kin.Where(GodotObject.IsInstanceValid).OrderBy(kin => kin.GlobalPosition.DistanceSquaredTo(player.GlobalPosition)).FirstOrDefault();
        Hut hut      = neighbor.Huts.FirstOrDefault(GodotObject.IsInstanceValid);
        Vector2 goal = PackWay.Hunt == way && null != hut ? hut.Position : villager?.GlobalPosition ?? neighbor.Fire;
        return Towards(player, goal, size * 1.5f);
    }

    /// <summary>The direction to a point, zero once within the reach.</summary>
    private static Vector2 Towards(Player player, Vector2 point, float reach)
    {
        Vector2 way = point - player.GlobalPosition;
        return way.Length() <= reach ? Vector2.Zero : way.Normalized();
    }

    private string Status(Player player)
    {
        TribeCommander tribe  = _main.Tribe;
        string         rivals = string.Join(", ", _main.Rivals.Rivals.Select(rival => $"{rival.Species} rel {rival.Relation:F2} resp {rival.Respect:F2} huts {rival.Huts.Count} kin {rival.Kin.Count}{(rival.IsDefeated ? " gone" : "")}"));
        return $"t={_time - _tribeAt:F0}s hp {player.Hp / player.MaxHp * 100f:F0}% food {tribe.Economy.Food} wood {tribe.Economy.Wood} buildings {tribe.Huts.Count} members {_main.Pack.Members} retinue {_main.Pack.Retinue} goal {_main.Diplomacy.Share * 100f:F0}% | {rivals}";
    }

    private void Finish(string reason)
    {
        string city = _cityAt < 0f ? "no city" : $"city after {_cityAt - _tribeAt:F0}s";
        string won  = _wonAt < 0f ? "path not won" : $"path won after {_wonAt - _tribeAt:F0}s";
        GD.Print($"[TribePlay] Done on the {_main.Tribe.Path} path: {reason}, {city}, {won}. {Status(_main.Player)}");
        Engine.TimeScale = 1f;
        SetPhysicsProcess(false);
        GetTree().Quit();
    }
}
