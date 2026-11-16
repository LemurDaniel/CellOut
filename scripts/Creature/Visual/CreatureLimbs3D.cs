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
//     MOVE LIMBS       The walk on land, the paddle in the water, the sway of soft limbs.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public sealed class CreatureLimbs3D
{
    // ===========================================================================
    // =============================   BUILD LIMBS   =============================
    // ===========================================================================
    //
    //   Build       A sculpted limb per flank: a clay tube through its joints, a foot or hand on the tip.
    //   RadiusAt    Radius of the tube at a point of its path.
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
      - Membrane:     chord of a skin that spans from the limb back to the body (a wing), 0 = none
    */
    private sealed record LimbLook(float Root, float Tip, float EndTip = 0f, float Knuckle = 1f, float TipBead = 0f, bool IsSegmented = false, float Membrane = 0f);

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
        // A second piece of body: as thick as the trunk, a round end
        [CreaturePartKind.Trunk]      = new(0.3f, 0.24f, 0.24f, Knuckle: 0f, TipBead: 0.25f),
        // The arm of a wing: thin bones, a skin from them back to the body
        [CreaturePartKind.WingLimb]   = new(0.06f, 0.02f, 0.04f, Knuckle: 0.8f, Membrane: 0.55f),
        // A finger or a toe on the joint of another limb
        [CreaturePartKind.Finger]     = new(0.035f, 0.022f, 0.03f, Knuckle: 0.9f),
    };
    private const float END_SIZE = 1.5f;             // feet and hands on the limb tip, in limb space

    /// <summary>Meta of a tube mesh of a limb: its UV.x runs along the limb, the tip color is cut in the shader.</summary>
    public const string TUBE_META = "limb_tube";

    /// <summary>Meta of a knuckle, bead or end model: the point of the limb path it sits on (1 the first joint .. the tip).</summary>
    public const string POINT_META = "limb_point";

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
                float from = Mathf.Lerp(look.Root, tipRadius, (float)i / (path.Count - 1)) * part.WidthAt(i);
                float to   = (i == path.Count - 2 ? tipRadius : Mathf.Lerp(look.Root, tipRadius, (float)(i + 1) / (path.Count - 1)) * SEGMENT_WAIST) * part.WidthAt(i + 1);
                tubes.Add(CreatureClay.LimbMesh(new[] { path[i], path[i + 1] }, to, from, null, (float)i / (path.Count - 1), (float)(i + 1) / (path.Count - 1)));
            }
        }
        else
        {
            tubes.Add(CreatureClay.LimbMesh(path, tipRadius, look.Root, part.Widths));
        }
        // A wing: the skin from the arm back to the body, its chord follows the thickness at the shoulder
        if (look.Membrane > 0f)
        {
            tubes.Add(CreatureClay.MembraneMesh(path, look.Membrane * part.WidthAt(0)));
        }
        Material joints = look.IsSegmented ? CellVisual3D.Material(color.Darkened(0.4f), 0f, 0.5f) : _knuckleClay;
        // A centered limb grows out of the middle of the back: one piece, no mirror half
        foreach (float side in part.IsCentered ? new[] { 1f } : new[] { -1f, 1f })
        {
            var piece = new Node3D { Position = RootOn(design, part, (int)side) - pivot.Position, Scale = new Vector3(1f, 1f, side) };
            pivot.AddChild(piece);
            foreach (ArrayMesh tube in tubes)
            {
                CellVisual3D.AddMesh(piece, tube, _limbClay, Vector3.Zero, Vector3.One).SetMeta(TUBE_META, true);
            }
            CreatureClay.AddFillet(piece, Vector3.Zero, look.Root * part.WidthAt(0) * CreatureClay.LIMB_FILLET, _knuckleClay);
            for (int i = 1; look.Knuckle > 0f && i < path.Count; i++)
            {
                bool  isTip  = i == path.Count - 1;
                if (isTip && (hasEnd || look.IsSegmented))
                {
                    continue;                        // the foot or hand takes the place of the tip knob, a hard leg ends in a point
                }
                float radius = Mathf.Lerp(look.Root, tipRadius, (float)i / (path.Count - 1)) * (isTip ? 1.35f : 1.12f) * look.Knuckle * part.WidthAt(i);
                CellVisual3D.AddMesh(piece, CellVisual3D.SphereMesh(), joints, path[i], Vector3.One * radius).SetMeta(POINT_META, i);
            }
            if (look.TipBead > 0f && !hasEnd)
            {
                CellVisual3D.AddMesh(piece, CellVisual3D.SphereMesh(), _knuckleClay, path[^1], Vector3.One * look.TipBead).SetMeta(POINT_META, path.Count - 1);
            }
            if (hasEnd)
            {
                AttachEnd(piece, part.Kind, color, path, side, _knuckleClay);
            }
            _built.Add((piece, (int)side));
        }
    }

    /// <summary>Radius of the tube of a limb at one point of its path in limb space, for what sits on its joints.</summary>
    /// <param name="part">(Required) A sculpted limb, a foot or a hand.</param>
    /// <param name="point">(Required) 0 the shoulder, 1 the first joint .. Joints.Count the tip.</param>
    public static float RadiusAt(CreaturePart part, int point)
    {
        LimbLook look  = LOOKS.TryGetValue(part.LimbStyle, out LimbLook known) ? known : PLAIN_LOOK;
        int      count = Mathf.Max(part.Joints?.Count ?? 1, 1);
        return Mathf.Lerp(look.Root, look.Tip, Mathf.Clamp((float)point / count, 0f, 1f)) * part.WidthAt(point);
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
        // The left shoulder mirrors the right one about the spine, which may lie beside the middle (sway)
        Vector3 root = design.LimbRoot(part);
        float   sway = design.SwayAt(root.X);
        return new Vector3(root.X, root.Y, sway + side * (root.Z - sway));
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
        end.SetMeta(POINT_META, path.Count - 1);
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
    //   IsOnLimb      Whether a node hangs on a limb, for the limb color zone.
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

    /// <summary>Whether a node hangs on a limb (its tube, a knuckle, the foot or hand): the limb color zone covers it.</summary>
    public bool IsOnLimb(Node node)
    {
        for (Node above = node; null != above; above = above.GetParent())
        {
            if (above is Node3D piece && _limbFrames.Any(frame => frame.Piece == piece))
            {
                return true;
            }
        }
        return false;
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

    // ===========================================================================
    // =============================   MOVE LIMBS   ==============================
    // ===========================================================================
    //
    //   Rest   Puts every limb back into its rest pose.
    //   Move   Swings every limb around its shoulder: a walk on land, a paddle in the water, a slow sway at rest.
    //

    private const float WALK_SWING = 0.42f;          // radians a walking leg swings to each side at full speed
    private const float PADDLE_SWING = 0.2f;         // a limb paddling in the water
    private const float IDLE_SWING = 0.05f;          // every limb breathes a little at rest
    private const float SOFT_SWING = 0.12f;          // tentacles, feelers and necks sway on their own, also at rest
    private const float SOFT_RATE = 0.35f;           // their sway runs slower than the stride

    private readonly Dictionary<Node3D, Basis> _restPoses = new();

    /// <summary>Puts every limb back into its sculpted rest pose (the editor leaves its animation mode).</summary>
    public void Rest()
    {
        foreach ((Node3D piece, Basis rest) in _restPoses)
        {
            if (GodotObject.IsInstanceValid(piece))
            {
                piece.Basis = rest;
            }
        }
    }

    /*
      The tube of a limb is one rigid piece, so a limb moves as a whole around its shoulder:
      > On land:       a leg swings back and forth with the stride, neighbors and the two
                       flanks half a step apart (a trot)
      > In the water:  a smaller, slower paddle
      > Soft limbs:    tentacles, feelers and necks sway sideways on their own as well
      A limb that carries branches stands still: its branches hang on the joints of its rest pose.
      Every limb follows its own PartMotion: off, faster or slower, a wider or smaller swing, an offset in the cycle.
    */
    /// <summary>Swings the limbs, once per frame.</summary>
    /// <param name="design">(Required) The design the limbs belong to.</param>
    /// <param name="phase">(Required) The stride phase of the body, it runs faster while moving.</param>
    /// <param name="speedShare">(Required) Speed of the body, 0 standing .. 1 full speed.</param>
    /// <param name="isOnLand">(Required) True on land (walk), false in the water (paddle).</param>
    public void Move(CreatureDesign design, float phase, float speedShare, bool isOnLand)
    {
        for (int i = 0; i < _limbFrames.Count; i++)
        {
            (int index, int flank, Node3D piece) = _limbFrames[i];
            if (!GodotObject.IsInstanceValid(piece) || index >= design.Parts.Count)
            {
                continue;
            }
            CreaturePart part = design.Parts[index];
            if (design.Parts.Any(other => other.ParentId == part.Id && null != other.Joints))
            {
                continue;
            }
            if (!_restPoses.TryGetValue(piece, out Basis rest))
            {
                _restPoses[piece] = rest = piece.Basis;
            }
            // The own motion of the part: off stands in its rest pose, else pace, swing and offset in the cycle
            PartMotion motion = part.Movement;
            if (!motion.IsOn)
            {
                piece.Basis = rest;
                continue;
            }
            bool  isSoft = CreaturePartKind.Tentacle == part.LimbStyle || CreaturePartKind.Feeler == part.LimbStyle || CreaturePartKind.Neck == part.LimbStyle;
            bool  isLeg  = design.IsWalkingLimb(part);
            float swing  = isOnLand && isLeg ? WALK_SWING * speedShare
                : isSoft ? 0f
                : PADDLE_SWING * speedShare;
            // Neighbors and the two flanks half a step apart
            float offset = motion.Phase * Mathf.Tau;
            float step   = phase * motion.Speed + index * Mathf.Pi + (flank < 0 ? Mathf.Pi : 0f) + offset;
            float stride = Mathf.Sin(step) * (swing + IDLE_SWING) * motion.Amount;
            float sway   = isSoft ? Mathf.Sin(phase * motion.Speed * SOFT_RATE + index * 1.7f + offset) * SOFT_SWING * motion.Amount : 0f;
            // Around the local Z of the limb (out of the flank) it swings along the body, around its X it sways sideways
            piece.Basis = rest * new Basis(Vector3.Back, stride) * new Basis(Vector3.Right, sway);
        }
    }
}