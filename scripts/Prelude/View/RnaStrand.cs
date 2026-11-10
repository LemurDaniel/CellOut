// NOTE:
//   One RNA strand of the prelude (Prelude/RnaWorld.cs), the player and the rivals alike:
//   a ribozyme head that swims like a snake and a chain of beads behind it. The chain
//   rules are in Prelude/Chain/RnaChain.cs, this component moves the head and draws head,
//   beads and bonds. RnaWorld steers it and decides who bites whom.
//
//   var strand = new RnaStrand();
//   strand.Setup(palette, size, position, heading, isRival: false);
//   AddChild(strand);
//   strand.AddLink(RnaBase.A, mutation: null);
//   strand.Move(targetHeading, speed, turn, dt, time);   // every frame

//
//   Chapters:
//     BUILD    The head with its eyes, the ring of the player.
//     CHAIN    Adding links, finding the bitten link and breaking the chain.
//     SWIM     Turning and moving the head, the chain follows.
//     RIVALS   The wandering heading and the rest after a bite.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class RnaStrand : Node3D
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   HEAD_RADIUS    Radius of the head, in body sizes.
    //   LINK_RADIUS    Radius of a bead, in body sizes.
    //   LINK_SPACING   Distance between two links, in body sizes.
    //   IsRival        Whether the strand is a rival, not the player.
    //   HeadPosition   Plane position of the head.
    //   Heading        Direction the head swims in, in radians.
    //   Chain          The links behind the head.
    //   Setup          (Required) palette, size, position, heading, isRival.
    //                  A head at a point: pink with the wanted-color ring for the player, violet for rivals.
    //

    public const float HEAD_RADIUS = 0.3f;
    public const float LINK_RADIUS = 0.13f;
    public const float LINK_SPACING = 0.34f;

    public bool IsRival { get; private set; }
    public Vector2 HeadPosition { get; private set; }
    public float Heading { get; private set; }
    public RnaChain Chain { get; private set; }

    private RnaPalette _palette;
    private float _size;
    private Node3D _head;
    private MeshInstance3D _ring;           // player only, glows in the wanted color
    private StandardMaterial3D _backbone;

    /// <summary>Builds the head of the strand, called once before AddChild.</summary>
    /// <param name="palette">(Required) The shared materials of the prelude.</param>
    /// <param name="size">(Required) Body size of the player, everything scales with it.</param>
    /// <param name="position">(Required) Plane position of the head.</param>
    /// <param name="heading">(Required) Direction the head looks in, in radians.</param>
    /// <param name="isRival">(Required) Rivals are violet and carry no ring.</param>
    public void Setup(RnaPalette palette, float size, Vector2 position, float heading, bool isRival)
    {
        Color color = isRival ? RnaPalette.RIVAL_COLOR : RnaPalette.BACKBONE_COLOR;
        _palette     = palette;
        _size        = size;
        IsRival      = isRival;
        HeadPosition = position;
        Heading      = heading;
        Chain        = new RnaChain(LINK_SPACING, size);
        _head        = new Node3D();
        _backbone    = RnaPalette.Glow(color, 0.4f);
        AddChild(_head);
        CellVisual3D.AddMesh(_head, CellVisual3D.SphereMesh(), RnaPalette.Glow(color, 0.35f), Vector3.Zero, Vector3.One * HEAD_RADIUS * size);
        if (!isRival)
        {
            _ring = CellVisual3D.AddMesh(_head, CellVisual3D.TorusMesh(), palette.Ring, Vector3.Zero, Vector3.One * HEAD_RADIUS * 1.35f * size);
        }

        // Two dots in front: the strand looks where it swims
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = new Vector3(HEAD_RADIUS * 0.75f, HEAD_RADIUS * 0.5f, side * HEAD_RADIUS * 0.3f) * size;
            CellVisual3D.AddMesh(_head, CellVisual3D.SphereMesh(true), CellVisual3D.Material(new Color(0.08f, 0.05f, 0.1f)), eye, Vector3.One * HEAD_RADIUS * 0.2f * size);
        }
        _head.Position = Plane3D.To3D(position);
    }

    // ===========================================================================
    // ================================   CHAIN   ================================
    // ===========================================================================
    //
    //   AddLink       A bead and its bond at the end of the chain, a mutation link carries its trait.
    //   FirstLinkAt   Index of the first link a bite reaches, -1 for none.
    //   Break         Frees the links from an index on and returns them.
    //

    private readonly List<MeshInstance3D> _beads = new();
    private readonly List<MeshInstance3D> _bonds = new();

    /// <summary>Adds a link with its bead and bond at the end of the chain.</summary>
    /// <param name="kind">(Required) The base of the link.</param>
    /// <param name="mutation">(Required) null for a paired link, else the trait of the mutation.</param>
    public void AddLink(RnaBase kind, MutationKind? mutation)
    {
        RnaChain.Link  link = Chain.Add(kind, mutation, HeadPosition, Heading);
        MeshInstance3D bead = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), _palette.LinkMaterial(kind, mutation), Vector3.Zero, Vector3.One * LINK_RADIUS * _size);
        MeshInstance3D bond = CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), _backbone, Vector3.Zero, Vector3.One);
        bead.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        bond.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        bead.Position   = Plane3D.To3D(link.Position);
        _beads.Add(bead);
        _bonds.Add(bond);
    }

    /// <summary>The first link a bite reaches, seen from the head.</summary>
    /// <param name="point">(Required) Center of the bite on the plane.</param>
    /// <param name="radius">(Required) Reach of the bite on the plane, without the bead.</param>
    /// <returns>The index of the link, -1 when the bite misses.</returns>
    public int FirstLinkAt(Vector2 point, float radius)
    {
        return Chain.FirstLinkAt(point, radius + LINK_RADIUS * _size);
    }

    /// <summary>Breaks the chain at a link: frees its bead and every bead behind it.</summary>
    /// <param name="cut">(Required) Index of the first lost link.</param>
    /// <returns>The lost links in chain order, with their last positions.</returns>
    public IReadOnlyList<RnaChain.Link> Break(int cut)
    {
        IReadOnlyList<RnaChain.Link> lost = Chain.CutAt(cut);
        for (int i = _beads.Count - 1; i >= cut; i--)
        {
            _beads[i].QueueFree();
            _bonds[i].QueueFree();
            _beads.RemoveAt(i);
            _bonds.RemoveAt(i);
        }
        return lost;
    }

    // ===========================================================================
    // ================================   SWIM   =================================
    // ===========================================================================
    //
    //   Move       Turns the head towards a heading, swims, every link follows the one in front.
    //   SpinRing   Tumbles the ring around the player head.
    //

    /// <summary>Turns the head towards a heading and swims forward, the chain follows with a soft wave.</summary>
    /// <param name="targetHeading">(Required) Heading to turn to, in radians.</param>
    /// <param name="speed">(Required) Swim speed in body sizes per second.</param>
    /// <param name="turn">(Required) Turn speed in radians per second.</param>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="time">(Required) Time of the prelude in seconds, drives the bobbing.</param>
    public void Move(float targetHeading, float speed, float turn, float dt, float time)
    {
        // Head: turns towards the target heading, then swims forward
        Heading       = Mathf.RotateToward(Heading, targetHeading, turn * dt);
        HeadPosition += Vector2.FromAngle(Heading) * speed * _size * dt;
        _head.Position = Plane3D.To3D(HeadPosition, Mathf.Sin(time * 2f + Wander) * 0.04f * _size);
        _head.Rotation = new Vector3(0f, Plane3D.FacingToYaw(Heading), 0f);

        // Chain: every link keeps LINK_SPACING to the one in front, with a soft wave
        Chain.Follow(HeadPosition, Heading);
        Vector3 front3D = _head.Position;
        for (int i = 0; i < Chain.Count; i++)
        {
            Vector3 at = Plane3D.To3D(Chain.Links[i].Position, Mathf.Sin(time * 4f - i * 0.7f) * 0.05f * _size);
            _beads[i].Position  = at;
            _bonds[i].Transform = Intro.Rod(front3D, at, 0.035f * _size);
            front3D = at;
        }
    }

    /// <summary>Tumbles the ring around the player head, nothing for a rival.</summary>
    /// <param name="time">(Required) Time of the prelude in seconds.</param>
    public void SpinRing(float time)
    {
        if (null == _ring)
        {
            return;
        }
        _ring.Rotation = new Vector3(0.6f * Mathf.Sin(time * 1.3f), time * 3f, 0.6f);
    }

    // ===========================================================================
    // ===============================   RIVALS   ================================
    // ===========================================================================
    //
    //   Wander           Drifting offset of the target heading while a rival wanders.
    //   DriftWander      Lets the wander offset drift, inside its limit.
    //   IsReadyToBite    Whether the head may bite again.
    //   CoolBite         Counts the rest after a bite down.
    //   RestAfterBite    Starts the rest after a bite.
    //

    private const float WANDER_LIMIT = 0.6f;         // radians the wander offset stays within

    private float _biteCooldown;

    public float Wander { get; private set; }
    public bool IsReadyToBite => 0f == _biteCooldown;

    /// <summary>Lets the wander offset drift by an amount, clamped to its limit.</summary>
    /// <param name="amount">(Required) Change of the offset in radians.</param>
    public void DriftWander(float amount)
    {
        Wander = Mathf.Clamp(Wander + amount, -WANDER_LIMIT, WANDER_LIMIT);
    }

    /// <summary>Counts the rest after a bite down.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void CoolBite(float dt)
    {
        _biteCooldown = Mathf.Max(0f, _biteCooldown - dt);
    }

    /// <summary>The head rests after a bite.</summary>
    /// <param name="seconds">(Required) Seconds until the next bite.</param>
    public void RestAfterBite(float seconds)
    {
        _biteCooldown = seconds;
    }
}
