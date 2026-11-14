// NOTE:
//   The stock of the tribe as plain rules: food the villagers gathered, wood they
//   chopped, what a hut of a given design costs and how many stand. Pure logic without
//   the engine, so the TribeCommander uses it and tests/unit checks it. Every number
//   comes from data/balance.json (tribe.buildings through BuildingDesign).
//
//   var economy = new TribeEconomy();
//   economy.AddFood(3);
//   economy.AddWood(2);
//   if (economy.CanAfford(design)) economy.BuildHut(design);

//
//   Chapters:
//     STOCK   Food, wood, gathering and paying for huts.
//

namespace CellOut;

public sealed class TribeEconomy
{
    // ===========================================================================
    // ================================   STOCK   ================================
    // ===========================================================================
    //
    //   Food        Food in stock.
    //   Wood        Wood in stock.
    //   Huts        Huts built so far.
    //   CanAfford   Whether the stock pays for a hut of a design.
    //   AddFood     Villagers gathered fruit.
    //   AddWood     Villagers chopped wood.
    //   Spend       Pays food and wood, false when the stock is short (gifts, huts).
    //   BuildHut    Pays for a building of a design and counts it, false when the stock is short.
    //

    public int Food { get; private set; }
    public int Wood { get; private set; }
    public int Huts { get; private set; }

    public bool CanAfford(BuildingDesign design)
    {
        return Food >= design.FoodCost && Wood >= design.WoodCost;
    }

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

    /// <summary>Adds chopped wood to the stock.</summary>
    /// <param name="amount">(Required) Wood chopped, at least 0.</param>
    public void AddWood(int amount)
    {
        if (amount < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(amount), $"amount must be at least 0, got '{amount}'.");
        }
        Wood += amount;
    }

    /// <summary>Pays food and wood when the stock allows it.</summary>
    /// <param name="food">(Required) Food to pay, at least 0.</param>
    /// <param name="wood">(Required) Wood to pay, at least 0.</param>
    /// <returns>True when paid.</returns>
    public bool Spend(int food, int wood)
    {
        if (food < 0 || wood < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(food), $"food and wood must be at least 0, got '{food}' and '{wood}'.");
        }
        if (Food < food || Wood < wood)
        {
            return false;
        }
        Food -= food;
        Wood -= wood;
        return true;
    }

    /// <summary>Pays for a hut of a design when the stock allows it.</summary>
    /// <param name="design">(Required) The blueprint, its FoodCost and WoodCost are paid.</param>
    /// <returns>True when the hut is paid and counted.</returns>
    /// <example><code>if (economy.BuildHut(design)) PlaceHut(design);</code></example>
    public bool BuildHut(BuildingDesign design)
    {
        if (!Spend(design.FoodCost, design.WoodCost))
        {
            return false;
        }
        Huts++;
        return true;
    }
}
