// NOTE:
//   Growth rules of the player as plain formulas: body size from biomass, the level
//   curve, when the cells may divide, how big prey may be and how many cells a deadly
//   hit bursts. Pure logic without the engine, so Player uses it and tests/unit checks
//   it. Every number comes from data/balance.json (player, Core/Balance.cs).
//
//   float size   = Growth.BodySize(player.Biomass);
//   float nextAt = Growth.NextLevelAt(player.NextLevelAt);

//
//   Chapters:
//     GROW   Body size, levels, division, prey size and lost cells.
//

using Godot;

namespace CellOut;

public static class Growth
{
    // ===========================================================================
    // ================================   GROW   =================================
    // ===========================================================================
    //
    //   BodySize       Size of the whole body for a biomass.
    //   FirstLevelAt   Biomass of the first level, counted from becoming a single cell.
    //   NextLevelAt    Biomass of the level after the given one.
    //   DivisionAt     Biomass that allows the next division.
    //   EatRatio       How much bigger than its prey the player must be.
    //   CellsLost      Cells a deadly hit bursts in a cluster.
    //

    private static PlayerBalance Tune => Balance.Current.Player;

    /// <summary>The whole body grows with the square root of the biomass, a cluster shares that size.</summary>
    /// <param name="biomass">(Required) Biomass of the player, below 0 counts as 0.</param>
    /// <example><code>Growth.BodySize(0f)   // 10, the protocell at the start</code></example>
    public static float BodySize(float biomass)
    {
        return Tune.BodySizeBase + Mathf.Sqrt(Mathf.Max(biomass, 0f)) * Tune.BodySizeGrowth;
    }

    /// <summary>Biomass of the first level.</summary>
    /// <param name="singleCellAt">(Required) Biomass at the moment the protocell became a single cell.</param>
    public static float FirstLevelAt(float singleCellAt)
    {
        return singleCellAt + Tune.FirstLevelStep;
    }

    /// <summary>Levels get rarer the bigger the body is: every next one needs LevelGrowth times the biomass plus LevelStep.</summary>
    /// <param name="levelAt">(Required) Biomass of the level just reached.</param>
    /// <example><code>Growth.NextLevelAt(85f)   // 208.5</code></example>
    public static float NextLevelAt(float levelAt)
    {
        return levelAt * Tune.LevelGrowth + Tune.LevelStep;
    }

    /// <summary>Biomass from which the cells may divide again.</summary>
    /// <param name="divisions">(Required) Divisions so far, 0 for a single cell.</param>
    /// <example><code>Growth.DivisionAt(1)   // 250, the step from 2 to 4 cells</code></example>
    public static float DivisionAt(int divisions)
    {
        return Tune.DivisionBiomass * Mathf.Pow(Tune.DivisionGrowth, divisions);
    }

    /// <summary>
    /// The player swallows a cell when its body size is larger than the cell radius times
    /// this ratio: a jaw towards the prey and every division allow bigger prey, never
    /// below MinEatRatio.
    /// </summary>
    /// <param name="hasJawToward">(Required) Whether a jaw points at the prey.</param>
    /// <param name="divisions">(Required) Divisions so far.</param>
    /// <example><code>Growth.EatRatio(false, 5)   // 0.9 with 32 cells</code></example>
    public static float EatRatio(bool hasJawToward, int divisions)
    {
        float baseRatio = hasJawToward ? Tune.JawEatRatio : Balance.Current.Critter.EatRatio;
        return Mathf.Max(Tune.MinEatRatio, baseRatio - Tune.EatRatioPerDivision * divisions);
    }

    /// <summary>Cells a deadly hit bursts instead of killing the cluster, at least one.</summary>
    /// <param name="cellCount">(Required) Cells of the cluster before the hit.</param>
    /// <example><code>Growth.CellsLost(64)   // 16</code></example>
    public static int CellsLost(int cellCount)
    {
        return Mathf.Max(1, Mathf.FloorToInt(cellCount * Tune.CellsLostOnDeath));
    }
}
