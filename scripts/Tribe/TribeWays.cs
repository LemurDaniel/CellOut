// NOTE:
//   The three ways of the social stages (wish of the user 2026-11-17): the diet the player
//   built up since the single cell becomes its nature. In the pack it meets foreign packs
//   three ways, court (herbivore), display (omnivore) and hunt (carnivore); founding the
//   tribe locks the diet into a path, pacifist, territorial or warlike, and the path
//   decides what a right click on strangers does and how the tribe wins. Pure logic
//   without the engine, tested in tests/unit/TribeTests.cs. Numbers from
//   data/balance.json (tribe.ways).
//
//   TribePath path = TribeWays.PathOf(player.Diet);
//   float strength = TribeWays.Strength(PackWay.Hunt, player.Diet);
//   bool  isWon    = TribeWays.IsWon(path, rivals.Select(r => new RivalStanding(r.Relation, r.Respect, r.IsDefeated)));

//
//   Chapters:
//     TYPES      The ways of the pack, the paths of the tribe, the standing of a rival.
//     WAYS       Path from the diet, how strong a way is, the display contest.
//     VICTORY    Progress and victory of each path.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   PackWay         Court (herbivore), display (omnivore), hunt (carnivore): keys 1 to 3 in the pack.
//   TribePath       Pacifist, territorial, warlike: the locked way of the tribe.
//   RivalStanding   Relation, respect and defeat of one rival, the input of the victory rules.
//

public enum PackWay { Court, Display, Hunt }

public enum TribePath { Pacifist, Territorial, Warlike }

/// <summary>Where the player's tribe stands with one rival.</summary>
/// <param name="Relation">-1 hostile .. 1 friendly.</param>
/// <param name="Respect">0 .. 1, won by displays.</param>
/// <param name="IsDefeated">It lost its last building.</param>
public readonly record struct RivalStanding(float Relation, float Respect, bool IsDefeated);

public static class TribeWays
{
    // ===========================================================================
    // ================================   WAYS   =================================
    // ===========================================================================
    //
    //   PathOf        The path a diet locks into: below -PathThreshold pacifist, above it warlike.
    //   DietOf        The diet a way belongs to, the color of its slot.
    //   WayOf         The pack way a path grew from.
    //   Affinity      0..1: how well a way fits the diet.
    //   Strength      1 .. 1 + WayBonus: a way that fits the diet works better.
    //   DisplayWins   The display contest: our sizes against theirs.
    //

    private static WaysBalance Tune => Balance.Current.Tribe.Ways;

    /// <summary>The path a diet locks into when the tribe is founded.</summary>
    /// <param name="diet">(Required) -1 herbivore .. 1 carnivore.</param>
    /// <example><code>TribeWays.PathOf(-0.6f)   // Pacifist</code></example>
    public static TribePath PathOf(float diet)
    {
        if (diet < -Tune.PathThreshold)
        {
            return TribePath.Pacifist;
        }
        return diet > Tune.PathThreshold ? TribePath.Warlike : TribePath.Territorial;
    }

    /// <summary>The diet a way belongs to: court the herbivore, display the omnivore, hunt the carnivore (the color of its slot).</summary>
    public static DietClass DietOf(PackWay way)
    {
        return way switch
        {
            PackWay.Court   => DietClass.Herbivore,
            PackWay.Display => DietClass.Omnivore,
            _               => DietClass.Carnivore,
        };
    }

    /// <summary>The pack way a path grew from: court for pacifists, display for the territorial, hunt for warriors.</summary>
    public static PackWay WayOf(TribePath path)
    {
        return path switch
        {
            TribePath.Pacifist    => PackWay.Court,
            TribePath.Territorial => PackWay.Display,
            _                     => PackWay.Hunt,
        };
    }

    /*
      How well a way fits the diet (-1 herbivore .. 1 carnivore):
      - Court:    the more herbivore, max(0, -diet)
      - Hunt:     the more carnivore, max(0, diet)
      - Display:  the more balanced, 1 - |diet|
    */
    /// <summary>0..1: how well a way fits the diet.</summary>
    /// <example><code>TribeWays.Affinity(PackWay.Display, 0f)   // 1</code></example>
    public static float Affinity(PackWay way, float diet)
    {
        float clamped = Mathf.Clamp(diet, -1f, 1f);
        return way switch
        {
            PackWay.Court   => Mathf.Max(0f, -clamped),
            PackWay.Hunt    => Mathf.Max(0f, clamped),
            _               => 1f - Mathf.Abs(clamped),
        };
    }

    /// <summary>Multiplier of a way: every way works, the one that fits the diet up to 1 + WayBonus.</summary>
    /// <example><code>TribeWays.Strength(PackWay.Hunt, 1f)   // 2 with WayBonus 1</code></example>
    public static float Strength(PackWay way, float diet)
    {
        return 1f + Tune.WayBonus * Affinity(way, diet);
    }

    /// <summary>The display contest: the side that shows more body wins, ties go to the one who stands its ground.</summary>
    /// <param name="ours">(Required) Sum of the body sizes on the player's side, times the strength of the way.</param>
    /// <param name="theirs">(Required) Sum of the body sizes on the other side.</param>
    public static bool DisplayWins(float ours, float theirs)
    {
        return ours > theirs;
    }

    // ===========================================================================
    // ===============================   VICTORY   ===============================
    // ===========================================================================
    //
    //   IsDone      Whether one rival counts for the goal of a path.
    //   Progress    Rivals done and rivals in total for a path.
    //   GoalShare   0..1: how far the goal of a path is, for the progress ring.
    //   IsWon       Every rival counts: an alliance, respect from all, or all defeated.
    //

    /*
      What a rival needs for the goal of each path:
      - Pacifist:     ally, relation at AllyAt or above (a defeated one never counts)
      - Territorial:  respect at RespectAt or above (a defeated one never counts)
      - Warlike:      defeated
    */
    /// <summary>Whether one rival counts for the goal of a path.</summary>
    public static bool IsDone(TribePath path, RivalStanding rival)
    {
        return path switch
        {
            TribePath.Pacifist    => !rival.IsDefeated && rival.Relation >= Tune.AllyAt,
            TribePath.Territorial => !rival.IsDefeated && rival.Respect >= Tune.RespectAt,
            _                     => rival.IsDefeated,
        };
    }

    /// <summary>Rivals done and rivals in total for the goal of a path.</summary>
    /// <example><code>TribeWays.Progress(TribePath.Warlike, rivals)   // (1, 3)</code></example>
    public static (int Done, int Total) Progress(TribePath path, IEnumerable<RivalStanding> rivals)
    {
        RivalStanding[] all = rivals.ToArray();
        return (all.Count(rival => IsDone(path, rival)), all.Length);
    }

    /*
      How far the path is, every rival counts the same:
      - Pacifist:     its relation towards AllyAt (a defeated one counts nothing)
      - Territorial:  its respect towards RespectAt (a defeated one counts nothing)
      - Warlike:      1 once defeated
    */
    /// <summary>0..1: how far the goal of a path is, the mean over the rivals, 0 without any.</summary>
    /// <example><code>TribeWays.GoalShare(TribePath.Territorial, rivals)   // 0.5 with half the respect of each</code></example>
    public static float GoalShare(TribePath path, IEnumerable<RivalStanding> rivals)
    {
        RivalStanding[] all = rivals.ToArray();
        if (0 == all.Length)
        {
            return 0f;
        }
        return all.Sum(rival => path switch
        {
            TribePath.Pacifist    => rival.IsDefeated ? 0f : Mathf.Clamp(rival.Relation / Tune.AllyAt, 0f, 1f),
            TribePath.Territorial => rival.IsDefeated ? 0f : Mathf.Clamp(rival.Respect / Tune.RespectAt, 0f, 1f),
            _                     => rival.IsDefeated ? 1f : 0f,
        }) / all.Length;
    }

    /// <summary>The path reached its goal: at least one rival, and every rival counts.</summary>
    public static bool IsWon(TribePath path, IEnumerable<RivalStanding> rivals)
    {
        (int done, int total) = Progress(path, rivals);
        return total > 0 && done == total;
    }
}
