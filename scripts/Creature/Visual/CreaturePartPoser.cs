// NOTE:
//   Poses the built pieces of a creature part (side, size, shift and turn from the
//   CreaturePart) and remembers their meshes and anchors, so the editor can pick a part
//   by ray and put its gizmo on it. Owned by CreatureParts3D, one instance per build.
//   Anchors are in the space of the visual, the owner turns them into global points.
//
//   var poser = new CreaturePartPoser();
//   List<Node3D> dropped = poser.Pose(index, part, pivots, baseSize);   // dropped: the other half of a single side
//   int hit = poser.PartAt(from, direction, out int flank);

//
//   Chapters:
//     POSE PARTS   Side, size, shift and turn of the built pieces.
//     PICK PARTS   The part under a ray and the anchor of its gizmo.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public sealed class CreaturePartPoser
{
    // ===========================================================================
    // =============================   POSE PARTS   ==============================
    // ===========================================================================
    //
    //   Pose          Applies side, size, shift and turn of a part to its built pieces.
    //   FlankOf       Which flank a piece sits on, 0 for the midline.
    //   RemovePiece   Drops the other half of a single side part.
    //

    private const float FLANK_EPSILON = 0.005f;
    private static readonly Basis MIRROR = Basis.FromScale(new Vector3(1f, 1f, -1f));

    private readonly List<(int Part, int Flank, MeshInstance3D Mesh)> _partMeshes = new();
    private readonly List<(int Part, int Flank, Vector3 Anchor)> _partAnchors = new();

    /*
      The pieces of a part are the direct children of the pivots built for it.
      > Single half (Side -1 or 1): the pieces of the other flank are dropped.
      > Groups: a paired part splits into its two flanks, any other part is one group.
        Every group turns rigidly around its anchor (mean of its piece positions) and
        moves by the offset. The left flank of a paired part takes the mirror image
        (offset Z negated, turn mirrored: MIRROR * turn * MIRROR).
      > Size grows every piece around its own origin (eyes stay on the flank, legs on the hip).
    */
    /// <summary>Poses the pieces of one part and remembers their meshes and anchors.</summary>
    /// <param name="index">(Required) Index of the part in Design.Parts.</param>
    /// <param name="part">(Required) The placed part with side, size, offset and rotation.</param>
    /// <param name="pivots">(Required) The pivots built for this part, their children are the pieces.</param>
    /// <param name="baseSize">(Required) Base size of the part before CreaturePart.Size.</param>
    /// <returns>The dropped pieces of the other flank (single half), already removed and queued to free.</returns>
    public List<Node3D> Pose(int index, CreaturePart part, IReadOnlyList<Node3D> pivots, float baseSize)
    {
        CreaturePartInfo info         = part.Info;
        Basis            grow         = Basis.FromScale(Vector3.One * baseSize * part.Size);
        Basis            turn         = Basis.FromEuler(part.Rotation);
        Basis            turnMirrored = MIRROR * turn * MIRROR;

        // Pieces with their flank, the other half of a single side part goes
        var pieces  = new List<(Node3D Pivot, Node3D Piece, int Flank)>();
        var dropped = new List<Node3D>();
        foreach (Node3D pivot in pivots)
        {
            foreach (Node3D piece in pivot.GetChildren().OfType<Node3D>())
            {
                int flank = info.IsPaired ? FlankOf(piece) : 0;
                if (0 != part.Side && flank == -part.Side)
                {
                    RemovePiece(piece);
                    dropped.Add(piece);
                    continue;
                }
                pieces.Add((pivot, piece, flank));
            }
        }

        // Every group turns around its anchor and shifts, the left flank mirrored
        foreach (int flank in new[] { -1, 0, 1 })
        {
            var group = pieces.Where(entry => flank == entry.Flank).ToList();
            if (0 == group.Count)
            {
                continue;
            }
            Vector3 anchor = Vector3.Zero;
            foreach ((Node3D pivot, Node3D piece, _) in group)
            {
                anchor += pivot.Position + piece.Position;
            }
            anchor /= group.Count;

            bool    isLeft = flank < 0;
            Basis   rotate = isLeft ? turnMirrored : turn;
            Vector3 shift  = isLeft ? MIRROR * part.Offset : part.Offset;
            foreach ((Node3D pivot, Node3D piece, _) in group)
            {
                Vector3 at = pivot.Position + piece.Position;
                piece.Position = anchor + rotate * (at - anchor) + shift - pivot.Position;
                piece.Basis    = rotate * piece.Basis * grow;
                if (piece is MeshInstance3D mesh)
                {
                    _partMeshes.Add((index, flank, mesh));
                }
                foreach (MeshInstance3D child in piece.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
                {
                    _partMeshes.Add((index, flank, child));
                }
            }
            _partAnchors.Add((index, flank, anchor + shift));
        }
        return dropped;
    }

    private static int FlankOf(Node3D piece)
    {
        if (piece.Position.Z > FLANK_EPSILON) return 1;
        if (piece.Position.Z < -FLANK_EPSILON) return -1;
        return 0;
    }

    private static void RemovePiece(Node3D piece)
    {
        piece.GetParent().RemoveChild(piece);
        piece.QueueFree();
    }

    // ===========================================================================
    // =============================   PICK PARTS   ==============================
    // ===========================================================================
    //
    //   PartAt             The placed part under a ray, for picking in the editor.
    //   TryGetPartAnchor   Where the gizmo of a part sits.
    //

    /// <summary>
    /// The placed part whose mesh bounds a ray hits first. Bounding boxes, not triangles:
    /// coarse, but enough to pick fins, eyes and limbs on the turntable.
    /// </summary>
    /// <param name="from">(Required) Ray origin in global space.</param>
    /// <param name="direction">(Required) Ray direction in global space.</param>
    /// <param name="flank">The flank of the hit piece, 0 for a midline part.</param>
    /// <returns>Index in Design.Parts, -1 when the ray hits no part.</returns>
    public int PartAt(Vector3 from, Vector3 direction, out int flank)
    {
        int   closest  = -1;
        float bestDist = float.PositiveInfinity;
        flank = 0;
        foreach ((int part, int pieceFlank, MeshInstance3D mesh) in _partMeshes)
        {
            if (!GodotObject.IsInstanceValid(mesh) || !mesh.IsVisibleInTree())
            {
                continue;
            }
            float? distance = RayPick.RayEntry(mesh.GlobalTransform * mesh.GetAabb(), from, direction);
            if (null != distance && distance.Value < bestDist)
            {
                bestDist = distance.Value;
                closest  = part;
                flank    = pieceFlank;
            }
        }
        return closest;
    }

    /// <summary>Where the gizmo of a part sits: its shifted anchor (rest pose, without the swim wave).</summary>
    /// <param name="index">(Required) Index in Design.Parts.</param>
    /// <param name="flank">(Required) -1 or 1 for one half of a pair, 0 for a midline part.</param>
    /// <param name="anchor">The anchor in the space of the visual.</param>
    /// <returns>False when the part has no group on that flank (not built yet).</returns>
    public bool TryGetPartAnchor(int index, int flank, out Vector3 anchor)
    {
        foreach ((int part, int groupFlank, Vector3 local) in _partAnchors)
        {
            if (index == part && flank == groupFlank)
            {
                anchor = local;
                return true;
            }
        }
        anchor = Vector3.Zero;
        return false;
    }
}
