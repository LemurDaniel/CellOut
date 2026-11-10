// NOTE:
//   Mutations the RNA strand of the prelude (Prelude/RnaWorld.cs) catches with a
//   wrong base. Each one rolls a small trait, good or bad, that stays with the cell
//   phase (protocell to multicellular, Player.ApplyDesign) and is worth evolution
//   points. Multipliers and points come from data/balance.json, names and effects
//   from translations.csv (MUTATION_<KIND>_NAME / _DESC).
//
//   MutationKind kind = Mutation.Roll();
//   Mutation trait    = Mutation.Get(kind);   // trait.Health == 1.05f for Tough

//
//   Chapters:
//     TRAITS   Every mutation and its multipliers, the roll.
//

using System.Collections.Generic;
using System.Linq;

namespace CellOut;

// ===========================================================================
// ===============================   TRAITS   ================================
// ===========================================================================
//
//   MutationKind      Every mutation, good ones first.
//   Mutation          Multipliers of one mutation (1 = no effect) and whether it helps.
//   ALL               Every mutation with its multipliers.
//   Of                One table row, numbers from data/balance.json.
//   Get               The mutation of a kind.
//   Roll              A random mutation, every kind equally likely (a third is bad).
//

public enum MutationKind { Tough, Swift, Sticky, Greedy, Brittle, Sluggish }

public sealed record Mutation(MutationKind Kind, bool IsGood, float Health = 1f, float Speed = 1f, float Magnet = 1f, float Digest = 1f)
{
    // Whether a mutation helps is fixed, its multipliers come from data/balance.json (prelude.mutations)
    public static readonly IReadOnlyList<Mutation> ALL = new[]
    {
        Of(MutationKind.Tough,    isGood: true),
        Of(MutationKind.Swift,    isGood: true),
        Of(MutationKind.Sticky,   isGood: true),
        Of(MutationKind.Greedy,   isGood: true),
        Of(MutationKind.Brittle,  isGood: false),
        Of(MutationKind.Sluggish, isGood: false),
    };

    /// <summary>One table row: the multipliers come from Balance.Current.Prelude.Mutations.</summary>
    private static Mutation Of(MutationKind kind, bool isGood)
    {
        MutationBalance tune = Balance.Current.Prelude.Mutations[kind];
        return new Mutation(kind, isGood, tune.Health, tune.Speed, tune.Magnet, tune.Digest);
    }

    /// <summary>The mutation of a kind.</summary>
    /// <example><code>Mutation.Get(MutationKind.Swift).Speed  // 1.03</code></example>
    public static Mutation Get(MutationKind kind) => ALL.First(mutation => mutation.Kind == kind);

    /// <summary>A random mutation, every kind equally likely.</summary>
    public static MutationKind Roll() => ALL[Rand.Index(ALL.Count)].Kind;

    /// <summary>Translation key of the name, e.g. MUTATION_TOUGH_NAME.</summary>
    public string NameKey => $"MUTATION_{Kind.ToString().ToUpperInvariant()}_NAME";

    /// <summary>Translation key of the effect, e.g. MUTATION_TOUGH_DESC.</summary>
    public string DescKey => $"MUTATION_{Kind.ToString().ToUpperInvariant()}_DESC";
}
