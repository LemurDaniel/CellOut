// NOTE:
//   The bases of RNA, their pairing and the random template the player strand of the
//   prelude (Prelude/RnaWorld.cs) copies. The template grows as far as it is asked, the
//   roll of a new base comes from the caller. Pure logic without any engine call, so it
//   runs in a unit test.
//
//   var template = new RnaTemplate(() => (RnaBase)Rand.Index(RnaTemplate.BASE_COUNT));
//   RnaBase wanted = template.WantedAt(chain.Count);   // the base the next link needs
//   RnaTemplate.Pair(RnaBase.G)                        // RnaBase.C

//
//   Chapters:
//     PAIRING    The four bases and which one pairs with which.
//     TEMPLATE   The random template and the base wanted at a position.
//

using System;
using System.Collections.Generic;

namespace CellOut;

// ===========================================================================
// ===============================   PAIRING   ===============================
// ===========================================================================
//
//   RnaBase      The four bases of RNA, U instead of the T of DNA.
//   BASE_COUNT   The number of bases.
//   Pair         The base that pairs with another one: A-U, G-C.
//

public enum RnaBase { A, U, G, C }

public sealed class RnaTemplate
{
    public const int BASE_COUNT = 4;

    /// <summary>The base that pairs with another one: A-U, G-C.</summary>
    /// <param name="kind">(Required) The base to pair.</param>
    /// <example><code>RnaTemplate.Pair(RnaBase.G)  // RnaBase.C</code></example>
    public static RnaBase Pair(RnaBase kind)
    {
        return kind switch
        {
            RnaBase.A => RnaBase.U,
            RnaBase.U => RnaBase.A,
            RnaBase.G => RnaBase.C,
            _         => RnaBase.G,
        };
    }

    // ===========================================================================
    // ==============================   TEMPLATE   ===============================
    // ===========================================================================
    //
    //   RnaTemplate   (Required) roll: gives the next random base of the template.
    //   At            Base of the random template at a position, grows on demand.
    //   WantedAt      The base that pairs with the template at a position.
    //

    private readonly List<RnaBase> _bases = new();
    private readonly Func<RnaBase> _roll;

    /// <summary>An empty template that grows with the given roll.</summary>
    /// <param name="roll">(Required) Gives a random base, called once per new template position.</param>
    public RnaTemplate(Func<RnaBase> roll)
    {
        _roll = roll ?? throw new ArgumentNullException(nameof(roll), "roll must be a function that gives the next template base.");
    }

    /// <summary>Base of the random template at a position, the template grows as far as it is asked.</summary>
    /// <param name="index">(Required) Position in the template, 0 is the first link.</param>
    /// <example><code>template.At(3)  // the same base every time it is asked</code></example>
    public RnaBase At(int index)
    {
        while (_bases.Count <= index)
        {
            _bases.Add(_roll());
        }
        return _bases[index];
    }

    /// <summary>The base that pairs with the template at a position, a broken chain asks again from there.</summary>
    /// <param name="index">(Required) Position in the template, 0 is the first link.</param>
    /// <example><code>template.WantedAt(0)  // RnaBase.U when the template starts with A</code></example>
    public RnaBase WantedAt(int index)
    {
        return Pair(At(index));
    }
}
