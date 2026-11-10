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
        ArrayMesh tube = CreatureClay.LimbMesh(path);
        foreach (float side in new[] { -1f, 1f })
        {
            var piece = new Node3D { Position = RootOn(design, part, (int)side) - pivot.Position, Scale = new Vector3(1f, 1f, side) };
            pivot.AddChild(piece);
            CellVisual3D.AddMesh(piece, tube, _limbClay, Vector3.Zero, Vector3.One);
            CreatureClay.AddFillet(piece, Vector3.Zero, CreatureClay.LIMB_ROOT_RADIUS * CreatureClay.LIMB_FILLET, _knuckleClay);
            for (int i = 1; i < path.Count; i++)
            {
                bool  isTip  = i == path.Count - 1;
                if (isTip && hasEnd)
                {
                    continue;                        // the foot or hand takes the place of the tip knob
                }
                float radius = Mathf.Lerp(CreatureClay.LIMB_ROOT_RADIUS, CreatureClay.LIMB_TIP_RADIUS, (float)i / (path.Count - 1)) * (isTip ? 1.35f : 1.12f);
                CellVisual3D.AddMesh(piece, CellVisual3D.SphereMesh(), _knuckleClay, path[i], Vector3.One * radius);
            }
            if (hasEnd)
            {
                AttachEnd(piece, part.Kind, color, path, side);
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
    private static void AttachEnd(Node3D piece, CreaturePartKind kind, Color color, IReadOnlyList<Vector3> path, float side)
    {
        Node3D  end = CreatureModels.NewModel(kind, color);
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
