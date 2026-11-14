// NOTE:
//   The stock of the tribe as plain rules: food the villagers gathered, what a hut costs
//   and how many stand. Pure logic without the engine, so the TribeCommander uses it
//   and tests/unit checks it. Every number comes from data/balance.json (tribe.village).
//
//   var economy = new TribeEconomy();
//   economy.AddFood(3);
//   if (economy.CanBuildHut) economy.BuildHut();

//
//   Chapters:
//     STOCK   Food, gathering and paying for huts.
//

namespace CellOut;

public sealed class TribeEconomy
{
    // ===========================================================================
    // ================================   STOCK   ================================
    // ===========================================================================
    //
    //   Food          Food in stock.
    //   Huts          Huts built so far.
    //   HutCost       Food a hut costs.
    //   CanBuildHut   Whether the stock pays for a hut.
    //   AddFood       Villagers gathered something.
    //   BuildHut      Pays for a hut, false when the stock is short.
    //

    /// <summary>Hut cost from data/balance.json (tribe.village).</summary>
    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    public int Food { get; private set; }
    public int Huts { get; private set; }

    public int HutCost => Tune.HutCost;

    public bool CanBuildHut => Food >= HutCost;

    /// <summary>Adds gathered food to the stock.</summary>
    /// <param name="amount">(Required) Food gathered, at least 0.</param>
    public void AddFood(int amount)
    {
        if (amount < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(amount), $"amount must be at least 0, got '{amount}'.");
        }
        Food += amount;
    }

    /// <summary>Pays for a hut when the stock allows it.</summary>
    /// <returns>True when the hut is paid and counted.</returns>
    /// <example><code>if (economy.BuildHut()) PlaceHut();</code></example>
    public bool BuildHut()
    {
        if (!CanBuildHut)
        {
            return false;
        }
        Food -= HutCost;
        Huts++;
        return true;
    }
}
