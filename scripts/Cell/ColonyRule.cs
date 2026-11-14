// NOTE:
//   Pure rules of the NPC colonies (CritterKind.Colony), the multicellular enemies:
//   how many cells a new colony gets and how much it shrinks when a deadly hit bursts
//   some of them. No engine calls, tested in tests/unit/CellTests.cs.
//
//   int   cells = ColonyRule.CellCountFor(player.CellCount, Rand.Index(3) - 1);
//   float size  = ColonyRule.SizeAfterBurst(critter.BodySize(), 8, 6);

//
//   Chapters:
//     COLONY RULES   Cell count of a new colony and its size after lost cells.
//

using Godot;

namespace CellOut;

public static class ColonyRule
{
    // ===========================================================================
    // ============================   COLONY RULES   =============================
    // ===========================================================================
    //
    //   CellCountFor     Cells of a new colony: half, equal or double the player's cells.
    //   SizeAfterBurst   Body size after a burst, the area shrinks with the share of lost cells.
    //

    public const int MIN_CELLS = 2;
    public const int MAX_CELLS = 32;                // a 64 cell colony next to a 64 cell player costs too many meshes

    /// <summary>Cells of a new colony, relative to the player, clamped to MIN_CELLS..MAX_CELLS.</summary>
    /// <param name="playerCells">(Required) Current cells of the player, at least 1.</param>
    /// <param name="step">(Required) -1 half, 0 equal, 1 double the player's cells.</param>
    /// <example><code>ColonyRule.CellCountFor(8, -1)   // 4</code></example>
    public static int CellCountFor(int playerCells, int step)
    {
        if (playerCells < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(playerCells), $"playerCells must be at least 1, got '{playerCells}'.");
        }
        if (step < -1 || step > 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(step), $"step must be -1, 0 or 1, got '{step}'.");
        }
        int cells = step switch
        {
            -1 => playerCells / 2,
            1  => playerCells * 2,
            _  => playerCells,
        };
        return Mathf.Clamp(cells, MIN_CELLS, MAX_CELLS);
    }

    /// <summary>Body size after a burst: the area shrinks with the share of the lost cells.</summary>
    /// <param name="size">(Required) Body size before the burst.</param>
    /// <param name="cellsBefore">(Required) Cells before the burst, at least 1.</param>
    /// <param name="cellsAfter">(Required) Cells left, 1..cellsBefore.</param>
    /// <example><code>ColonyRule.SizeAfterBurst(20f, 8, 2)   // 10</code></example>
    public static float SizeAfterBurst(float size, int cellsBefore, int cellsAfter)
    {
        if (cellsBefore < 1 || cellsAfter < 1 || cellsAfter > cellsBefore)
        {
            throw new System.ArgumentOutOfRangeException(nameof(cellsAfter), $"cellsAfter must be 1..cellsBefore ({cellsBefore}), got '{cellsAfter}'.");
        }
        return size * Mathf.Sqrt((float)cellsAfter / cellsBefore);
    }
}
