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
    //   Place      Moves and turns a block node to its place on the grid.
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
        foreach (PlacedBlock block in design.Blocks)
        {
            Node3D piece = NewBlock(block.Kind, block.Paint, block.Pattern);
            Place(piece, block.Kind, block.X, block.Y, block.Z, block.Turns);
            AddChild(piece);
        }
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
