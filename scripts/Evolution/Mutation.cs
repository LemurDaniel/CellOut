// NOTE:
//   Mutations the RNA strand of the prelude (World/Prelude/RnaWorld.cs) catches with a
//   wrong base. Each one rolls a small trait, good or bad, that stays with the cell
//   phase (protocell to multicellular, Player.ApplyDesign) and is worth EP_PER_MUTATION
//   evolution points. Names and effects come from translations.csv
//   (MUTATION_<KIND>_NAME / _DESC).
//
//   MutationKind kind = Mutation.Roll();
//   Mutation trait    = Mutation.Get(kind);   // trait.Health == 1.1f for Tough

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
//   EP_PER_MUTATION   Evolution points per mutation, good or bad.
//   ALL               Every mutation with its multipliers.
//   Get               The mutation of a kind.
//   Roll              A random mutation, every kind equally likely (a third is bad).
//

public enum MutationKind { Tough, Swift, Sticky, Greedy, Brittle, Sluggish }

public sealed record Mutation(MutationKind Kind, bool IsGood, float Health = 1f, float Speed = 1f, float Magnet = 1f, float Digest = 1f)
{
    public const int EP_PER_MUTATION = 1;

    public static readonly IReadOnlyList<Mutation> ALL = new[]
    {
        new Mutation(MutationKind.Tough,    IsGood: true,  Health: 1.1f),
        new Mutation(MutationKind.Swift,    IsGood: true,  Speed: 1.06f),
        new Mutation(MutationKind.Sticky,   IsGood: true,  Magnet: 1.2f),
        new Mutation(MutationKind.Greedy,   IsGood: true,  Digest: 1.08f),
        new Mutation(MutationKind.Brittle,  IsGood: false, Health: 0.92f),
        new Mutation(MutationKind.Sluggish, IsGood: false, Speed: 0.95f),
    };

    /// <summary>The mutation of a kind.</summary>
    /// <example><code>Mutation.Get(MutationKind.Swift).Speed  // 1.06</code></example>
    public static Mutation Get(MutationKind kind) => ALL.First(mutation => mutation.Kind == kind);

    /// <summary>A random mutation, every kind equally likely.</summary>
    public static MutationKind Roll() => ALL[Rand.Index(ALL.Count)].Kind;

    /// <summary>Translation key of the name, e.g. MUTATION_TOUGH_NAME.</summary>
    public string NameKey => $"MUTATION_{Kind.ToString().ToUpperInvariant()}_NAME";

    /// <summary>Translation key of the effect, e.g. MUTATION_TOUGH_DESC.</summary>
    public string DescKey => $"MUTATION_{Kind.ToString().ToUpperInvariant()}_DESC";
}
