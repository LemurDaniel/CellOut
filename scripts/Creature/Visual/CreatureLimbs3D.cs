// NOTE:
//   The sculpted limbs of a 3D organism: a clay tube through the joints of the part on
//   each flank, knuckles, a fillet at the shoulder and the foot or hand model on the tip.
//   Remembers where the limbs ended up, so branches grow from the posed joints of their
//   parent and the editor finds the frame of a limb for its joint handles.
//   Owned by CreatureParts3D: it hands in the pivot a limb hangs on and, once the part is
//   posed, the joint points in visual space (TakeBuilt, Remember). One instance per build.
//
//   var limbs = new CreatureLimbs3D(limbClay, knuckleClay);
//   Node3D pivot = AddPivot(limbs.RootOn(design, part, 1).X);
//   limbs.Build(pivot, design, part, color);

//
//   Chapters:
//     BUILD LIMBS      The tube per flank, its root on the body or a parent and the end on the tip.
//     REMEMBER LIMBS   Posed joint points for branches and the limb frames of the editor.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public sealed class CreatureLimbs3D
{
    // ===========================================================================
    // =============================   BUILD LIMBS   =============================
    // ===========================================================================
    //
    //   Build       A sculpted limb per flank: a clay tube through its joints, a foot or hand on the tip.
    //   RootOn      Where a limb starts on a flank: the body, or the posed joint of its parent (a branch).
    //   AttachEnd   The foot (flat) or hand (along the last segment) on the limb tip.
    //

    private const float GRAB_RATE = 0.6f;            // hands grab and let go
    private const float SEGMENT_WAIST = 0.7f;        // a hard segment thins to this share before the next joint

    /*
      The look of a limb tube per limb kind, radii in limb space:
      - Root, Tip:    radius at the shoulder and at the tip
      - EndTip:       the tip is at least this thick when a foot or hand sits on it
      - Knuckle:      size of the balls on the joints, 0 = a smooth arm without any
      - TipBead:      radius of a bead on the bare tip, 0 = none
      - IsSegmented:  straight hard segments with dark joints instead of one smooth tube
    */
    private sealed record LimbLook(float Root, float Tip, float EndTip = 0f, float Knuckle = 1f, float TipBead = 0f, bool IsSegmented = false);

    private static readonly LimbLook PLAIN_LOOK = new(CreatureClay.LIMB_ROOT_RADIUS, CreatureClay.LIMB_TIP_RADIUS);
    private static readonly IReadOnlyDictionary<CreaturePartKind, LimbLook> LOOKS = new Dictionary<CreaturePartKind, LimbLook>
    {
        // A smooth arm that runs out into a point
        [CreaturePartKind.Tentacle]   = new(CreatureClay.LIMB_ROOT_RADIUS, CreatureClay.TENTACLE_TIP_RADIUS, CreatureClay.TENTACLE_END_RADIUS, Knuckle: 0f),
        [CreaturePartKind.Suckers]    = new(CreatureClay.LIMB_ROOT_RADIUS, CreatureClay.TENTACLE_TIP_RADIUS, CreatureClay.TENTACLE_END_RADIUS, Knuckle: 0f),
        // A thin antenna with a bead on its tip
        [CreaturePartKind.Feeler]     = new(0.045f, 0.02f, 0.04f, Knuckle: 0f, TipBead: 0.05f),
        // The leg of a crab or a spider: hard spindles, dark joints, a pointed tip
        [CreaturePartKind.JointedLeg] = new(0.1f, 0.025f, 0.06f, Knuckle: 0.75f, IsSegmented: true),
        // A thick neck or trunk, smooth, with a round end that carries a head
        [CreaturePartKind.Neck]       = new(0.21f, 0.15f, 0.15f, Knuckle: 0f, TipBead: 0.16f),
    };
    private const float END_SIZE = 1.5f;             // feet and hands on the limb tip, in limb space

    private readonly ShaderMaterial _limbClay;
    private readonly ShaderMaterial _knuckleClay;
    private readonly List<(Node3D Piece, int Flank)> _built = new();                          // handed out by TakeBuilt
    private readonly Dictionary<(int Part, int Flank), List<Vector3>> _jointPoints = new();   // shoulder + joints, visual space

    /// <summary>The limbs of one build.</summary>
    /// <param name="limbClay">(Required) Clay of the tubes: the body look without a swim wave of its own.</param>
    /// <param name="knuckleClay">(Required) Plain clay of knuckles and fillets, without the paint pattern.</param>
    public CreatureLimbs3D(ShaderMaterial limbClay, ShaderMaterial knuckleClay)
    {
        _limbClay    = limbClay;
        _knuckleClay = knuckleClay;
    }

    /*
      A sculpted limb per flank: a piece at the shoulder (CreatureDesign.LimbRoot), mirrored
      on Z for the left flank like the models, holding one clay tube from the shoulder
      through every joint, a knuckle at each joint and a round tip. Procedural instead of a
      Blender model on purpose: the joints are free, no fixed model fits them.
    */
    /// <summary>Builds the limb of a part on both flanks.</summary>
    /// <param name="pivot">(Required) The pivot at the X of RootOn(design, part, 1), it rides the swim wave.</param>
    /// <param name="design">(Required) The design the part belongs to.</param>
    /// <param name="part">(Required) A sculpted limb, a foot or a hand (with Joints).</param>
    /// <param name="color">(Required) The body color, for the end model.</param>
    public void Build(Node3D pivot, CreatureDesign design, CreaturePart part, Color color)
    {
        bool    hasEnd = part.Info.IsEnd;
        var     path  = new List<Vector3> { Vector3.Zero };
        path.AddRange(part.Joints);
        // The look of the tube follows the limb kind, also under a foot or a hand (CreaturePart.LimbStyle)
        LimbLook  look = LOOKS.TryGetValue(part.LimbStyle, out LimbLook known) ? known : PLAIN_LOOK;
        float     tipRadius = hasEnd ? Mathf.Max(look.Tip, look.EndTip) : look.Tip;
        var       tubes = new List<ArrayMesh>();
        if (look.IsSegmented)
        {
            // Hard segments: every one a straight spindle of its own, thick after the joint, thin before the next
            for (int i = 0; i < path.Count - 1; i++)
            {
                float from = Mathf.Lerp(look.Root, tipRadius, (float)i / (path.Count - 1));
                float to   = i == path.Count - 2 ? tipRadius : Mathf.Lerp(look.Root, tipRadius, (float)(i + 1) / (path.Count - 1)) * SEGMENT_WAIST;
                tubes.Add(CreatureClay.LimbMesh(new[] { path[i], path[i + 1] }, to, from));
            }
        }
        else
        {
            tubes.Add(CreatureClay.LimbMesh(path, tipRadius, look.Root));
        }
        Material joints = look.IsSegmented ? CellVisual3D.Material(color.Darkened(0.4f), 0f, 0.5f) : _knuckleClay;
        foreach (float side in new[] { -1f, 1f })
        {
            var piece = new Node3D { Position = RootOn(design, part, (int)side) - pivot.Position, Scale = new Vector3(1f, 1f, side) };
            pivot.AddChild(piece);
            foreach (ArrayMesh tube in tubes)
            {
                CellVisual3D.AddMesh(piece, tube, _limbClay, Vector3.Zero, Vector3.One);
            }
            CreatureClay.AddFillet(piece, Vector3.Zero, look.Root * CreatureClay.LIMB_FILLET, _knuckleClay);
            for (int i = 1; look.Knuckle > 0f && i < path.Count; i++)
            {
                bool  isTip  = i == path.Count - 1;
                if (isTip && (hasEnd || look.IsSegmented))
                {
                    continue;                        // the foot or hand takes the place of the tip knob, a hard leg ends in a point
                }
                float radius = Mathf.Lerp(look.Root, tipRadius, (float)i / (path.Count - 1)) * (isTip ? 1.35f : 1.12f) * look.Knuckle;
                CellVisual3D.AddMesh(piece, CellVisual3D.SphereMesh(), joints, path[i], Vector3.One * radius);
            }
            if (look.TipBead > 0f && !hasEnd)
            {
                CellVisual3D.AddMesh(piece, CellVisual3D.SphereMesh(), _knuckleClay, path[^1], Vector3.One * look.TipBead);
            }
            if (hasEnd)
            {
                AttachEnd(piece, part.Kind, color, path, side, _knuckleClay);
            }
            _built.Add((piece, (int)side));
        }
    }

    /*
      Where a limb starts on one flank, in visual space:
      > A branch: the posed joint of its parent on that flank (the parent is built first);
        a parent that is a single half has only one flank, the other mirrors it.
      > A limb on the body: its shoulder on the lower flank (CreatureDesign.LimbRoot).
    */
    /// <summary>Where a limb starts on one flank, in visual space.</summary>
    /// <param name="design">(Required) The design the part belongs to.</param>
    /// <param name="part">(Required) The limb.</param>
    /// <param name="side">(Required) -1 left flank, 1 right flank.</param>
    public Vector3 RootOn(CreatureDesign design, CreaturePart part, int side)
    {
        int parentIndex = design.IndexOfId(part.ParentId);
        if (parentIndex >= 0)
        {
            int joint = 1 + Mathf.Clamp(part.ParentJoint, 0, design.Parts[parentIndex].Joints.Count - 1);
            if (_jointPoints.TryGetValue((parentIndex, side), out List<Vector3> points))
            {
                return points[joint];
            }
            if (_jointPoints.TryGetValue((parentIndex, -side), out List<Vector3> mirrored))
            {
                return mirrored[joint] * new Vector3(1f, 1f, -1f);
            }
        }
        Vector3 root = design.LimbRoot(part);
        return new Vector3(root.X, root.Y, side * root.Z);
    }

    /*
      The end on the limb tip, in limb space (the piece mirrors the left flank already):
      - Feet stand flat, toes forward (+X), whatever the last segment does.
      - Hands follow the last segment: +X along it, the palm facing down.
    */
    private static void AttachEnd(Node3D piece, CreaturePartKind kind, Color color, IReadOnlyList<Vector3> path, float side, Material clay)
    {
        // The skin of the hand or foot is the clay of its limb, so both are one piece
        Node3D  end = CreatureModels.NewModel(kind, color, new Dictionary<string, Material> { ["Skin"] = clay });
        Vector3 tip = path[^1];
        Basis   turn = Basis.Identity;
        if (CreatureCategory.Hands == CreatureCatalog.Get(kind).Category)
        {
            Vector3 along = (tip - path[^2]).Normalized();
            Vector3 aside = along.Cross(Vector3.Up);
            aside = aside.LengthSquared() < 1e-4f ? Vector3.Back : aside.Normalized();
            turn  = new Basis(along, aside.Cross(along), aside);
        }
        end.Transform = new Transform3D(turn * Basis.FromScale(Vector3.One * END_SIZE), tip);
        piece.AddChild(end);
        if (null != CreatureModels.MODELS[kind].Animation)
        {
            PartModels.PlayLoop(end, CreatureModels.MODELS[kind].Animation, side * 0.2f, GRAB_RATE);
        }
    }

    // ===========================================================================
    // ===========================   REMEMBER LIMBS   ============================
    // ===========================================================================
    //
    //   TakeBuilt     The pieces built since the last call, for the owner to pose and measure.
    //   Remember      Stores the posed piece of a limb and its joint points.
    //   PiecesOf      The posed pieces of a limb, for the parts sitting on its joints.
    //   TryGetFrame   Where a sculpted limb sits, for the joint handles of the editor.
    //

    private readonly List<(int Part, int Flank, Node3D Piece)> _limbFrames = new();

    /// <summary>The limb pieces built since the last call with their flank, the list is emptied.</summary>
    /// <returns>Piece and flank (-1 or 1) of every limb half, not posed yet when Build just ran.</returns>
    public List<(Node3D Piece, int Flank)> TakeBuilt()
    {
        var built = new List<(Node3D Piece, int Flank)>(_built);
        _built.Clear();
        return built;
    }

    /// <summary>Stores the posed piece of a limb: branches grow from its joints, the editor reads its frame.</summary>
    /// <param name="index">(Required) Index of the limb in Design.Parts.</param>
    /// <param name="flank">(Required) -1 or 1.</param>
    /// <param name="piece">(Required) The posed piece at the shoulder.</param>
    /// <param name="points">(Required) Shoulder and every joint in visual space.</param>
    public void Remember(int index, int flank, Node3D piece, List<Vector3> points)
    {
        _limbFrames.Add((index, flank, piece));
        _jointPoints[(index, flank)] = points;
    }

    /// <summary>The posed pieces of a limb, one per flank it has: parts on its joints hang on them and follow pose and swim wave.</summary>
    /// <param name="index">(Required) Index in Design.Parts, a limb.</param>
    public IEnumerable<(int Flank, Node3D Piece)> PiecesOf(int index)
    {
        foreach ((int part, int flank, Node3D piece) in _limbFrames)
        {
            if (index == part && GodotObject.IsInstanceValid(piece))
            {
                yield return (flank, piece);
            }
        }
    }

    /// <summary>The posed shoulder of a sculpted limb on one flank: joints in limb space times this frame are world points.</summary>
    /// <param name="index">(Required) Index in Design.Parts, a Limb.</param>
    /// <param name="flank">(Required) -1 or 1.</param>
    /// <param name="frame">The global transform of the limb piece (pose, size and mirror included).</param>
    /// <returns>False when the limb has no piece on that flank.</returns>
    public bool TryGetFrame(int index, int flank, out Transform3D frame)
    {
        foreach ((int part, int pieceFlank, Node3D piece) in _limbFrames)
        {
            if (index == part && flank == pieceFlank && GodotObject.IsInstanceValid(piece) && piece.IsInsideTree())
            {
                frame = piece.GlobalTransform;
                return true;
            }
        }
        frame = Transform3D.Identity;
        return false;
    }
}
