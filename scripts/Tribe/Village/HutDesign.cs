// NOTE:
//   The blueprint of a hut as plain rules: a wall, a roof and any number of add-ons.
//   What they cost (food and wood) and what they do (health against raids, villagers per
//   hut, defense of the village, trade and relation bonus, extra food per fruit) comes
//   from data/balance.json (tribe.huts), the design only sums it up. Pure logic without
//   the engine: the HutEditor edits it, the TribeCommander builds from it, the rival
//   tribes raid against it, tests/unit checks it.
//
//   var design = new HutDesign();
//   design.Wall = WallKind.Stone;
//   design.Toggle(HutAddon.Palisade);
//   int food = design.FoodCost;   // wall + roof + add-ons

//
//   Chapters:
//     TYPES     Walls, roofs and add-ons.
//     DESIGN    The blueprint, its costs and its effects.
//

using System.Collections.Generic;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   WallKind   Clay, woven wattle or stone: the health of the hut.
//   RoofKind   Grass, reed or shingles: how many villagers the hut raises.
//   HutAddon   Storehouse, market, totem, palisade, watchtower: village effects.
//

public enum WallKind { Clay, Wattle, Stone }

public enum RoofKind { Grass, Reed, Shingle }

public enum HutAddon { Storehouse, Market, Totem, Palisade, Watchtower }

public sealed class HutDesign
{
    // ===========================================================================
    // ===============================   DESIGN   ================================
    // ===========================================================================
    //
    //   Wall / Roof      The chosen wall and roof.
    //   Addons           The chosen add-ons, each at most once.
    //   Has / Toggle     Whether an add-on is on, switching it.
    //   CopyFrom         Takes over another design.
    //   FoodCost         Food of wall, roof and add-ons.
    //   WoodCost         Wood of wall, roof and add-ons.
    //   Health           Hit points of the hut against raids (the wall).
    //   Villagers        Villagers the hut raises (the roof).
    //   Defense          Defense the hut adds to the village (palisade, watchtower).
    //   TradeBonus       Share more a trader brings (market).
    //   RelationBonus    Extra relation per trade (totem).
    //   FoodPerFruit     Extra food per gathered fruit while the hut stands (storehouse).
    //

    /// <summary>Costs and effects of walls, roofs and add-ons from data/balance.json (tribe.huts).</summary>
    private static HutsBalance Tune => Balance.Current.Tribe.Huts;

    public WallKind Wall { get; set; } = WallKind.Clay;
    public RoofKind Roof { get; set; } = RoofKind.Grass;

    private readonly HashSet<HutAddon> _addons = new();

    public IReadOnlyCollection<HutAddon> Addons => _addons;

    public bool Has(HutAddon addon)
    {
        return _addons.Contains(addon);
    }

    /// <summary>Switches an add-on on or off.</summary>
    /// <returns>True when it is on afterwards.</returns>
    public bool Toggle(HutAddon addon)
    {
        if (!_addons.Add(addon))
        {
            _addons.Remove(addon);
            return false;
        }
        return true;
    }

    /// <summary>Takes over wall, roof and add-ons of another design.</summary>
    /// <param name="other">(Required) The design copied.</param>
    public void CopyFrom(HutDesign other)
    {
        Wall = other.Wall;
        Roof = other.Roof;
        _addons.Clear();
        _addons.UnionWith(other._addons);
    }

    public int FoodCost => Tune.Walls[Wall].Food + Tune.Roofs[Roof].Food + Sum(addon => Tune.Addons[addon].Food);

    public int WoodCost => Tune.Walls[Wall].Wood + Tune.Roofs[Roof].Wood + Sum(addon => Tune.Addons[addon].Wood);

    public float Health => Tune.Walls[Wall].Health;

    public int Villagers => Tune.Roofs[Roof].Villagers;

    public float Defense => SumF(addon => Tune.Addons[addon].Defense);

    public float TradeBonus => SumF(addon => Tune.Addons[addon].TradeBonus);

    public float RelationBonus => SumF(addon => Tune.Addons[addon].RelationBonus);

    public int FoodPerFruit => Sum(addon => Tune.Addons[addon].FoodPerFruit);

    private int Sum(System.Func<HutAddon, int> value)
    {
        int sum = 0;
        foreach (HutAddon addon in _addons)
        {
            sum += value(addon);
        }
        return sum;
    }

    private float SumF(System.Func<HutAddon, float> value)
    {
        float sum = 0f;
        foreach (HutAddon addon in _addons)
        {
            sum += value(addon);
        }
        return sum;
    }
}
