// NOTE:
//   The chain of links behind the head of one RNA strand of the prelude
//   (Prelude/RnaWorld.cs), player and rivals alike: which bases hang on it, which of them
//   are mutations, where they are on the plane, how they follow the head, where a bite
//   breaks the chain and where too many mutations break it (error catastrophe). Pure logic on value structs without any engine call, so it runs in
//   a unit test. Prelude/View/RnaStrand.cs draws it.
//
//   var chain = new RnaChain(RnaStrand.LINK_SPACING, size);
//   chain.Add(RnaBase.A, null, head, heading);
//   chain.Follow(head, heading);                       // after the head moved
//   int cut = chain.FirstLinkAt(bite, reach);          // -1 for none
//   IReadOnlyList<RnaChain.Link> lost = chain.CutAt(cut);
//   int unstable = chain.ErrorCut(maxRun: 3, maxShare: 0.34f, minLinks: 6);   // -1 while stable

//
//   Chapters:
//     LINKS      The links and their counts.
//     MOVEMENT   Adding a link at the end and following the head.
//     BITES      Finding the bitten link and cutting the chain there.
//     ERRORS     Where too many mutations break the chain.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public sealed class RnaChain
{
    // ===========================================================================
    // ================================   LINKS   ================================
    // ===========================================================================
    //
    //   Link            One link: its base, the trait of a mutation and its plane position.
    //   RnaChain        (Required) spacing and size: the distance between two links.
    //   Links           All links, the first one hangs on the head.
    //   Count           All links behind the head.
    //   PairedCount     Links that paired correctly, they count for the goal.
    //   MutationCount   Links that stuck without pairing.
    //   Mutations       The traits of those links (Prelude/Mutation.cs).
    //

    /// <summary>One link of a chain.</summary>
    /// <param name="Kind">(Required) The base of the link.</param>
    /// <param name="Mutation">(Required) null: paired, else the trait the wrong base rolled.</param>
    /// <param name="Position">(Required) Plane position of the link.</param>
    public readonly record struct Link(RnaBase Kind, MutationKind? Mutation, Vector2 Position)
    {
        public bool IsMutation => null != Mutation;
    }

    private readonly List<Link> _links = new();
    private readonly float _spacing;
    private readonly float _size;

    /// <summary>An empty chain.</summary>
    /// <param name="spacing">(Required) Distance between two links, in body sizes.</param>
    /// <param name="size">(Required) Body size in world units, the spacing scales with it.</param>
    public RnaChain(float spacing, float size)
    {
        _spacing = spacing;
        _size    = size;
    }

    public IReadOnlyList<Link> Links => _links;
    public int Count => _links.Count;
    public int PairedCount => _links.Count(link => !link.IsMutation);
    public int MutationCount => _links.Count(link => link.IsMutation);

    /// <summary>The traits of the mutation links, in chain order. A bitten off link takes its trait along.</summary>
    public IReadOnlyList<MutationKind> Mutations => _links.Where(link => link.IsMutation).Select(link => link.Mutation.Value).ToList();

    // ===========================================================================
    // ==============================   MOVEMENT   ===============================
    // ===========================================================================
    //
    //   Add      A link at the end of the chain, one spacing behind the last one.
    //   Follow   Every link keeps the spacing to the one in front of it.
    //

    /// <summary>Adds a link at the end, one spacing behind the last link (or the head) against the heading.</summary>
    /// <param name="kind">(Required) The base of the link.</param>
    /// <param name="mutation">(Required) null for a paired link, else the trait of the mutation.</param>
    /// <param name="head">(Required) Plane position of the head.</param>
    /// <param name="heading">(Required) Heading of the head in radians.</param>
    /// <returns>The new link.</returns>
    /// <example><code>new RnaChain(0.5f, 2f).Add(RnaBase.A, null, Vector2.Zero, 0f).Position  // (-1, 0)</code></example>
    public Link Add(RnaBase kind, MutationKind? mutation, Vector2 head, float heading)
    {
        Vector2 previous = 0 == _links.Count ? head : _links[^1].Position;
        var     link     = new Link(kind, mutation, previous - Vector2.FromAngle(heading) * _spacing * _size);
        _links.Add(link);
        return link;
    }

    /// <summary>Pulls the chain behind the head: every link keeps the spacing to the one in front.</summary>
    /// <param name="head">(Required) Plane position of the head.</param>
    /// <param name="heading">(Required) Heading of the head in radians, the direction for a link lying on its front one.</param>
    public void Follow(Vector2 head, float heading)
    {
        Vector2 front = head;
        for (int i = 0; i < _links.Count; i++)
        {
            Vector2 back     = _links[i].Position - front;
            Vector2 position = front + (back.IsZeroApprox() ? -Vector2.FromAngle(heading) : back.Normalized()) * _spacing * _size;
            _links[i] = _links[i] with { Position = position };
            front     = position;
        }
    }

    // ===========================================================================
    // ================================   BITES   ================================
    // ===========================================================================
    //
    //   FirstLinkAt   Index of the first link inside a circle, -1 for none.
    //   CutAt         Removes the links from an index on and returns them.
    //

    /// <summary>The first link inside a circle, seen from the head.</summary>
    /// <param name="point">(Required) Center of the circle on the plane.</param>
    /// <param name="radius">(Required) Radius of the circle, the link radius included.</param>
    /// <returns>The index of the link, -1 when no link is inside.</returns>
    public int FirstLinkAt(Vector2 point, float radius)
    {
        for (int i = 0; i < _links.Count; i++)
        {
            if (_links[i].Position.DistanceTo(point) <= radius)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Breaks the chain: removes the link at an index and every link behind it.</summary>
    /// <param name="index">(Required) Index of the first lost link, 0..Count.</param>
    /// <returns>The lost links in chain order.</returns>
    /// <example><code>chain.CutAt(chain.Count - 1).Count  // 1, the last link</code></example>
    public IReadOnlyList<Link> CutAt(int index)
    {
        List<Link> lost = _links.GetRange(index, _links.Count - index);
        _links.RemoveRange(index, lost.Count);
        return lost;
    }

    // ===========================================================================
    // ================================   ERRORS   ===============================
    // ===========================================================================
    //
    //   Error catastrophe (Eigen): a replicator that copies with too many errors loses its
    //   information. Here the chain breaks once its tail has too many mutations.
    //
    //   ErrorCut   Index where the chain breaks under its mutations, -1 while it is stable.
    //

    /// <summary>
    /// Where the chain breaks under its mutations, checked after every new link:
    /// <para>> maxRun mutations in a row at the end: the chain breaks at the first of them.</para>
    /// <para>> From minLinks links on more than maxShare of them mutations: the chain breaks at
    ///   the first mutation in its back half, the front half stays.</para>
    /// </summary>
    /// <param name="maxRun">(Required) Mutations in a row at the end that break the chain.</param>
    /// <param name="maxShare">(Required) Share of mutations above which the chain breaks, 0..1.</param>
    /// <param name="minLinks">(Required) The share only counts from this many links on.</param>
    /// <returns>The index of the first lost link (for CutAt), -1 while the chain is stable.</returns>
    /// <example><code>chain.ErrorCut(3, 0.34f, 6)  // 2 for paired, paired, mutation, mutation, mutation</code></example>
    public int ErrorCut(int maxRun, float maxShare, int minLinks)
    {
        // Mutations in a row at the end
        int runStart = _links.Count;
        while (runStart > 0 && _links[runStart - 1].IsMutation)
        {
            runStart--;
        }
        if (_links.Count - runStart >= maxRun)
        {
            return runStart;
        }

        // Too many mutations overall
        if (_links.Count < minLinks || MutationCount <= maxShare * _links.Count)
        {
            return -1;
        }
        for (int i = _links.Count / 2; i < _links.Count; i++)
        {
            if (_links[i].IsMutation)
            {
                return i;
            }
        }
        return -1;
    }
}
