// NOTE:
//   The look of a village building from its blueprint (BuildingDesign): every placed
//   block as its Blender model (tools/blender/models/buildings.py, assets/models/building/)
//   on the grid, standing on y = 0 and centered on the grid. One grid cell is CELL model
//   units, a room block is two units wide, +X is the front of a block. The paint of a
//   block colors its main surfaces (materials Paint and PaintDark), its pattern is painted
//   onto the Paint surface (shaders/building_paint.gdshader), the rest keeps its own colors. Used by Hut in the world, by the stage of the BuildingEditor and by the icons
//   (BlockIcon, BuildingIcon), so all show exactly the same pieces.
//
//   var visual = new BuildingVisual3D();
//   visual.Build(design);
//   AddChild(visual);
//   Node3D piece = BuildingVisual3D.NewBlock(BlockKind.Totem, BlockCatalog.DEFAULT_PAINT);

//
//   Chapters:
//     BUILD BUILDING   Every block of a design at its place on the grid.
//     BUILD BLOCK      One block: its model in its paint.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class BuildingVisual3D : Node3D
{
    // ===========================================================================
    // ===========================   BUILD BUILDING   ============================
    // ===========================================================================
    //
    //   CELL       Model units per grid cell.
    //   Build      Clears and rebuilds every block of a design.
    //   PivotOf    Where a block stands: the center of its footprint at its bottom.
    //   Place      Moves and turns a block node to its place on the grid, with a PlacedBlock also into its free pose.
    //   PieceOf / Pose / CenterOf   The node of a built block, moving it to a new pose, its middle (the gizmo of the builder).
    //   BlockAt    The built block a ray meets first.
    //

    public const float CELL = 0.5f;

    /// <summary>Clears the building and builds it again from a design.</summary>
    /// <param name="design">(Required) The blueprint.</param>
    public void Build(BuildingDesign design)
    {
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        _pieces.Clear();
        foreach (PlacedBlock block in design.Blocks)
        {
            Node3D piece = NewBlock(block.Kind, block.Paint, block.Pattern);
            Place(piece, block);
            AddChild(piece);
            _pieces.Add((piece, BoundsOf(piece)));
        }
    }

    private readonly List<(Node3D Piece, Aabb Bounds)> _pieces = new();

    /// <summary>The node of a block of the design built last, null for an index outside it.</summary>
    /// <param name="index">(Required) Index in BuildingDesign.Blocks.</param>
    public Node3D PieceOf(int index)
    {
        return index >= 0 && index < _pieces.Count ? _pieces[index].Piece : null;
    }

    /// <summary>Moves one block of the design built last to its (new) pose, without building anything again: a drag of the gizmo.</summary>
    /// <param name="index">(Required) Index in BuildingDesign.Blocks.</param>
    /// <param name="block">(Required) The block with its pose.</param>
    public void Pose(int index, PlacedBlock block)
    {
        if (PieceOf(index) is Node3D piece)
        {
            Place(piece, block);
        }
    }

    /// <summary>The middle of a block of the design built last in world space: where its gizmo sits.</summary>
    public Vector3? CenterOf(int index)
    {
        return PieceOf(index) is Node3D piece ? piece.GlobalTransform * _pieces[index].Bounds.GetCenter() : null;
    }

    /*
      The block a ray meets first, by the boxes around the meshes of every block in its own
      frame (so a turned, shifted or stretched block is hit where it shows):
      - The ray is taken into the frame of each block and cut with its box.
      - The hit closest to the origin of the ray wins.
    */
    /// <summary>The block of the design built last that a world ray meets first.</summary>
    /// <param name="origin">(Required) Start of the ray in world space.</param>
    /// <param name="direction">(Required) Its normalized direction.</param>
    /// <returns>Index in BuildingDesign.Blocks, -1 when the ray misses every block.</returns>
    /// <example><code>int block = visual.BlockAt(camera.ProjectRayOrigin(pixel), camera.ProjectRayNormal(pixel));</code></example>
    public int BlockAt(Vector3 origin, Vector3 direction)
    {
        int   hit      = -1;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < _pieces.Count; i++)
        {
            (Node3D piece, Aabb bounds) = _pieces[i];
            Transform3D toPiece = piece.GlobalTransform.AffineInverse();
            Vector3 from  = toPiece * origin;
            Vector3 along = toPiece.Basis * direction;
            if (RayIntoBox(bounds, from, along) is not float reach)
            {
                continue;
            }
            float d = (piece.GlobalTransform * (from + along * reach)).DistanceTo(origin);
            if (d < distance)
            {
                distance = d;
                hit      = i;
            }
        }
        return hit;
    }

    /// <summary>Where a ray enters a box (slab test): the share of its direction up to the first face, null when it misses or the box lies behind it.</summary>
    private static float? RayIntoBox(Aabb box, Vector3 from, Vector3 along)
    {
        float enter = 0f;
        float leave = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float low  = box.Position[axis];
            float high = box.End[axis];
            if (Mathf.Abs(along[axis]) < 1e-6f)
            {
                if (from[axis] < low || from[axis] > high)
                {
                    return null;
                }
                continue;
            }
            float near = (low - from[axis]) / along[axis];
            float far  = (high - from[axis]) / along[axis];
            enter = Mathf.Max(enter, Mathf.Min(near, far));
            leave = Mathf.Min(leave, Mathf.Max(near, far));
        }
        return enter <= leave ? enter : null;
    }

    /// <summary>The box around every mesh of a block in the frame of the block.</summary>
    private static Aabb BoundsOf(Node3D piece)
    {
        Aabb? bounds = null;
        foreach (MeshInstance3D mesh in piece.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            Transform3D toPiece = Transform3D.Identity;
            for (Node node = mesh; node != piece && node is Node3D node3D; node = node.GetParent())
            {
                toPiece = node3D.Transform * toPiece;
            }
            Aabb box = toPiece * mesh.GetAabb();
            bounds = null == bounds ? box : bounds.Value.Merge(box);
        }
        return bounds ?? new Aabb(Vector3.Zero, Vector3.One * CELL);
    }

    /// <summary>The center of the footprint of a block at its bottom, in model units, the grid centered on the origin.</summary>
    /// <param name="kind">(Required) The block.</param>
    /// <param name="x">(Required) Cell of its lowest corner along X.</param>
    /// <param name="y">(Required) Cell height of its bottom.</param>
    /// <param name="z">(Required) Cell of its lowest corner along Z.</param>
    /// <param name="turns">(Required) Quarter turns.</param>
    public static Vector3 PivotOf(BlockKind kind, int x, int y, int z, int turns)
    {
        (int width, int depth) = BlockCatalog.Footprint(kind, turns);
        float half = BuildingDesign.GRID_SIZE * 0.5f;
        return new Vector3(x + width * 0.5f - half, y, z + depth * 0.5f - half) * CELL;
    }

    /// <summary>Moves and turns a block node (NewBlock) to its place on the grid.</summary>
    public static void Place(Node3D piece, BlockKind kind, int x, int y, int z, int turns)
    {
        piece.Position = PivotOf(kind, x, y, z, turns);
        piece.Rotation = new Vector3(0f, turns * Mathf.Pi * 0.5f, 0f);
    }

    /// <summary>Moves a block node to its place on the grid and into its free pose: the turn of the pose on top of the quarter turns, the scale in the frame of the block.</summary>
    public static void Place(Node3D piece, PlacedBlock block)
    {
        Basis turned = Basis.FromEuler(block.Rotation) * new Basis(Vector3.Up, block.Turns * Mathf.Pi * 0.5f);
        piece.Transform = new Transform3D(turned * Basis.FromScale(block.Scale), PivotOf(block.Kind, block.X, block.Y, block.Z, block.Turns) + block.Offset);
    }

    // ===========================================================================
    // =============================   BUILD BLOCK   =============================
    // ===========================================================================
    //
    //   MODEL_FOLDER   Where the generated block models lie.
    //   ModelPath      The .glb of a block.
    //   NewBlock       A block as a node: its footprint centered on the origin, standing on y = 0.
    //   Patterned      The cached pattern material of a paint.
    //

    public const string MODEL_FOLDER = "res://assets/models/building/";

    private const string FLAME_MATERIAL = "Flame";
    private const string PAINT_MATERIAL = "Paint";
    private const float PATTERN_CONTRAST = 0.5f;     // how far the pattern tone is from the paint
    private static readonly Shader PATTERN_SHADER = GD.Load<Shader>("res://shaders/building_paint.gdshader");
    private static readonly Dictionary<(Color, int), ShaderMaterial> _patterned = new();

    /// <summary>The generated model of a block, e.g. building.coneroof.glb.</summary>
    public static string ModelPath(BlockKind kind)
    {
        return $"{MODEL_FOLDER}building.{kind.ToString().ToLowerInvariant()}.glb";
    }

    /// <summary>One block in its own frame, unturned: +X is its front.</summary>
    /// <param name="kind">(Required) The block.</param>
    /// <param name="paint">(Required) Index in BlockCatalog.PAINTS or BlockCatalog.DEFAULT_PAINT.</param>
    /// <param name="pattern">(Optional) 0..BlockCatalog.PATTERN_COUNT, 0 is plain. Defaults to 0.</param>
    /// <returns>The node, to be placed with Place or shown on its own (palette icon, drop ghost).</returns>
    public static Node3D NewBlock(BlockKind kind, int paint, int pattern = 0)
    {
        Color color = BlockCatalog.PaintOf(kind, paint);
        var overrides = pattern > 0 ? new Dictionary<string, Material> { [PAINT_MATERIAL] = Patterned(color, pattern) } : null;
        Node3D piece = PartModels.New(ModelPath(kind), color, overrides);

        // A building is big enough for shadows (the part models switch them off), only the flame of a torch throws none
        foreach (MeshInstance3D mesh in piece.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            bool isFlame = mesh.Name.ToString().StartsWith(FLAME_MATERIAL);
            mesh.CastShadow = isFlame ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On;
        }
        return piece;
    }

    /// <summary>The pattern in a lighter tone on a dark paint, in a darker tone on a light one.</summary>
    private static ShaderMaterial Patterned(Color color, int pattern)
    {
        if (_patterned.TryGetValue((color, pattern), out ShaderMaterial cached))
        {
            return cached;
        }
        var material = new ShaderMaterial { Shader = PATTERN_SHADER };
        material.SetShaderParameter("base_color", color);
        material.SetShaderParameter("pattern_color", color.Luminance < 0.45f ? color.Lightened(PATTERN_CONTRAST) : color.Darkened(PATTERN_CONTRAST));
        material.SetShaderParameter("pattern", pattern);
        _patterned[(color, pattern)] = material;
        return material;
    }
}
