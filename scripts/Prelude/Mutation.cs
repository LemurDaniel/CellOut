// NOTE:
//   Mutations the RNA strand of the prelude (Prelude/RnaWorld.cs) catches with a
//   wrong base. Each one rolls a small trait, good or bad, that stays with the cell
//   phase (protocell to multicellular, Player.ApplyDesign). Only good ones are worth
//   evolution points, capped. The roll is weighted, bad ones are as likely as good ones.
//   Weights, multipliers and points come from data/balance.json, names and effects
//   from translations.csv (MUTATION_<KIND>_NAME / _DESC).
//
//   MutationKind kind = Mutation.Roll();
//   Mutation trait    = Mutation.Get(kind);   // trait.Health == 1.05f for Tough
//   int points        = Mutation.EvolutionPoints(mutations);

//
//   Chapters:
//     TRAITS   Every mutation and its multipliers, the roll and the evolution points.
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
//   Roll              A random mutation, weighted by data/balance.json (Weight).
//   EvolutionPoints   EP of a set of mutations: good ones only, capped.
//

public enum MutationKind { Tough, Swift, Sticky, Greedy, Brittle, Sluggish }

public sealed record Mutation(MutationKind Kind, bool IsGood, float Weight = 1f, float Health = 1f, float Speed = 1f, float Magnet = 1f, float Digest = 1f)
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
        return new Mutation(kind, isGood, tune.Weight, tune.Health, tune.Speed, tune.Magnet, tune.Digest);
    }

    /// <summary>The mutation of a kind.</summary>
    /// <example><code>Mutation.Get(MutationKind.Swift).Speed  // 1.03</code></example>
    public static Mutation Get(MutationKind kind) => ALL.First(mutation => mutation.Kind == kind);

    /// <summary>A random mutation, each kind as likely as its Weight among all weights.</summary>
    public static MutationKind Roll() => Pick(Rand.Range(0f, 1f));

    /// <summary>The mutation at a point of the weighted roll, the walk behind Roll.</summary>
    /// <param name="roll">(Required) 0..1, where in the summed weights the roll lands.</param>
    /// <example><code>Mutation.Pick(0f)  // MutationKind.Tough, the first one</code></example>
    public static MutationKind Pick(float roll)
    {
        float left = roll * ALL.Sum(mutation => mutation.Weight);
        foreach (Mutation mutation in ALL)
        {
            left -= mutation.Weight;
            if (left < 0f)
            {
                return mutation.Kind;
            }
        }
        return ALL[^1].Kind;
    }

    /// <summary>Evolution points a set of mutations brings: EpPerMutation for each good one, at most MaxMutationEp.</summary>
    /// <param name="mutations">(Required) The mutations of the strand.</param>
    /// <example><code>Mutation.EvolutionPoints(new[] { MutationKind.Tough, MutationKind.Sluggish })  // 1</code></example>
    public static int EvolutionPoints(IEnumerable<MutationKind> mutations)
    {
        PreludeBalance tune = Balance.Current.Prelude;
        int            good = mutations.Count(kind => Get(kind).IsGood);
        return System.Math.Min(tune.MaxMutationEp, good * tune.EpPerMutation);
    }

    /// <summary>Translation key of the name, e.g. MUTATION_TOUGH_NAME.</summary>
    public string NameKey => $"MUTATION_{Kind.ToString().ToUpperInvariant()}_NAME";

    /// <summary>Translation key of the effect, e.g. MUTATION_TOUGH_DESC.</summary>
    public string DescKey => $"MUTATION_{Kind.ToString().ToUpperInvariant()}_DESC";
}
