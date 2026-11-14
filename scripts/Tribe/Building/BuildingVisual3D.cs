// NOTE:
//   The look of a village building from its blueprint (BuildingDesign): every placed
//   block as a small 3D piece on the grid, standing on y = 0 and centered on the grid.
//   One grid cell is CELL model units, a room block is two units wide. Used by Hut in the
//   world, by the stage of the BuildingEditor and by its palette icons (NewBlock), so all
//   three show exactly the same pieces. Primitives only for now, +X is the front of a block.
//
//   var visual = new BuildingVisual3D();
//   visual.Build(design);
//   AddChild(visual);
//   Node3D piece = BuildingVisual3D.NewBlock(BlockKind.Totem, BlockCatalog.DEFAULT_PAINT);

//
//   Chapters:
//     BUILD BUILDING   Every block of a design at its place on the grid.
//     MESHES           The shared primitive meshes and the helpers that add them.
//     BUILD BLOCK      One block in its own frame: rooms and frames, roofs, decoration.
//

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
            Node3D piece = NewBlock(block.Kind, block.Paint);
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
    // ===============================   MESHES   ================================
    // ===========================================================================
    //
    //   Box / Cylinder / Cone / Pyramid / Prism   Shared unit meshes.
    //   AddBox        A box by center and size.
    //   AddCylinder   An upright cylinder by its bottom center, radius and height.
    //   AddCone       An upright cone by its bottom center, radius and height.
    //   AddPyramid    A four sided pyramid by its bottom center, half side and height.
    //   AddRing       A flat ring by center and radius.
    //   AddBall       A low poly ball by center and size.
    //

    private const float SHADE = 0.3f;                // how much darker the trim of a block is
    private static readonly Color DOOR_COLOR = new(0.2f, 0.14f, 0.08f);
    private static readonly Color STEP_COLOR = new(0.5f, 0.5f, 0.52f);
    private static readonly Color FLAME_COLOR = new(1f, 0.65f, 0.2f);
    private static readonly Color FRUIT_RED = new(0.85f, 0.3f, 0.25f);
    private static readonly Color FRUIT_GOLD = new(0.95f, 0.75f, 0.25f);
    private static readonly Color FRUIT_GREEN = new(0.5f, 0.7f, 0.3f);
    private static readonly Color BONE_COLOR = new(0.9f, 0.88f, 0.8f);

    private static BoxMesh _box;
    private static CylinderMesh _cylinder;
    private static CylinderMesh _cone;
    private static CylinderMesh _pyramid;
    private static PrismMesh _prism;

    private static BoxMesh Box => _box ??= new BoxMesh { Size = Vector3.One };
    private static CylinderMesh Cylinder => _cylinder ??= new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 1f, RadialSegments = 20, Rings = 1 };
    private static CylinderMesh Cone => _cone ??= new CylinderMesh { TopRadius = 0f, BottomRadius = 1f, Height = 1f, RadialSegments = 20, Rings = 1 };
    private static CylinderMesh Pyramid => _pyramid ??= new CylinderMesh { TopRadius = 0f, BottomRadius = 1f, Height = 1f, RadialSegments = 4, Rings = 1 };
    private static PrismMesh Prism => _prism ??= new PrismMesh { Size = Vector3.One };

    private static MeshInstance3D AddBox(Node3D parent, Color color, Vector3 center, Vector3 size, float roughness = 0.9f)
    {
        return CellVisual3D.AddMesh(parent, Box, CellVisual3D.Material(color, 0f, roughness), center, size);
    }

    private static MeshInstance3D AddCylinder(Node3D parent, Color color, Vector3 bottom, float radius, float height, float roughness = 0.9f)
    {
        return CellVisual3D.AddMesh(parent, Cylinder, CellVisual3D.Material(color, 0f, roughness), bottom + Vector3.Up * height * 0.5f, new Vector3(radius, height, radius));
    }

    private static MeshInstance3D AddCone(Node3D parent, Color color, Vector3 bottom, float radius, float height)
    {
        return CellVisual3D.AddMesh(parent, Cone, CellVisual3D.Material(color, 0f, 1f), bottom + Vector3.Up * height * 0.5f, new Vector3(radius, height, radius));
    }

    /// <summary>The four sided cone has its corners on the axes: a quarter of a half turn squares it with the grid.</summary>
    private static MeshInstance3D AddPyramid(Node3D parent, Color color, Vector3 bottom, float halfSide, float height)
    {
        float radius = halfSide * Mathf.Sqrt2;
        MeshInstance3D pyramid = CellVisual3D.AddMesh(parent, Pyramid, CellVisual3D.Material(color, 0f, 1f), bottom + Vector3.Up * height * 0.5f, new Vector3(radius, height, radius));
        pyramid.Rotation = new Vector3(0f, Mathf.Pi * 0.25f, 0f);
        return pyramid;
    }

    private static MeshInstance3D AddRing(Node3D parent, Color color, Vector3 center, float radius)
    {
        return CellVisual3D.AddMesh(parent, CellVisual3D.TorusMesh(), CellVisual3D.Material(color, 0f, 0.9f), center, new Vector3(radius, radius * 0.8f, radius));
    }

    private static MeshInstance3D AddBall(Node3D parent, Color color, Vector3 center, Vector3 size, float glow = 0f)
    {
        return CellVisual3D.AddMesh(parent, CellVisual3D.SphereMesh(true), CellVisual3D.Material(color, glow, 0.8f), center, size);
    }

    // ===========================================================================
    // =============================   BUILD BLOCK   =============================
    // ===========================================================================
    //
    //   NewBlock      A block as a node: its footprint centered on the origin, standing on y = 0.
    //   BuildFrame    Rooms, towers, stilts, platform and palisade.
    //   BuildRoof     Cone, pyramid and gable roofs and the awning.
    //   BuildDecor    Door, totem, horns, stall, crate, basket, banner, torch and ladder.
    //

    /// <summary>One block in its own frame, unturned: +X is its front.</summary>
    /// <param name="kind">(Required) The block.</param>
    /// <param name="paint">(Required) Index in BlockCatalog.PAINTS or BlockCatalog.DEFAULT_PAINT.</param>
    /// <returns>The node, to be placed with Place or shown on its own (palette icon, drop ghost).</returns>
    public static Node3D NewBlock(BlockKind kind, int paint)
    {
        var   piece = new Node3D();
        Color color = BlockCatalog.PaintOf(kind, paint);
        switch (BlockCatalog.Get(kind).Category)
        {
            case BlockCategory.Frame:
                BuildFrame(piece, kind, color);
                break;
            case BlockCategory.Roof:
                BuildRoof(piece, kind, color);
                break;
            case BlockCategory.Decor:
                BuildDecor(piece, kind, color);
                break;
        }
        return piece;
    }

    private static void BuildFrame(Node3D piece, BlockKind kind, Color color)
    {
        Color trim = color.Darkened(SHADE);
        switch (kind)
        {
            // A round wall with upright poles and a rim at foot and top
            case BlockKind.RoundRoom:
                AddCylinder(piece, color, Vector3.Zero, 0.96f, 1f);
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.Tau * (i + 0.5f) / 10f;
                    AddCylinder(piece, trim, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.96f, 0.045f, 1f);
                }
                AddRing(piece, trim, new Vector3(0f, 0.06f, 0f), 1f);
                AddRing(piece, trim, new Vector3(0f, 0.94f, 0f), 1f);
                break;

            // A square wall with corner posts and a plank band
            case BlockKind.SquareRoom:
                AddBox(piece, color, new Vector3(0f, 0.5f, 0f), new Vector3(1.86f, 1f, 1.86f));
                foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
                {
                    AddBox(piece, trim, new Vector3(corner.X * 0.93f, 0.5f, corner.Y * 0.93f), new Vector3(0.14f, 1f, 0.14f));
                }
                AddBox(piece, trim, new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 0.08f, 1.9f));
                AddBox(piece, trim, new Vector3(0f, 0.96f, 0f), new Vector3(1.96f, 0.08f, 1.96f));
                break;

            // A slim round tower with stones bulging out
            case BlockKind.RoundTower:
                AddCylinder(piece, color, Vector3.Zero, 0.46f, 1f);
                for (int i = 0; i < 9; i++)
                {
                    float angle  = Mathf.Tau * i / 9f + (i % 2) * 0.3f;
                    float height = 0.18f + 0.3f * (i % 3);
                    AddBall(piece, trim, new Vector3(Mathf.Cos(angle) * 0.45f, height, Mathf.Sin(angle) * 0.45f), new Vector3(0.13f, 0.09f, 0.13f));
                }
                AddRing(piece, trim, new Vector3(0f, 0.96f, 0f), 0.5f);
                break;

            // A slim square tower with corner posts
            case BlockKind.SquareTower:
                AddBox(piece, color, new Vector3(0f, 0.5f, 0f), new Vector3(0.88f, 1f, 0.88f));
                foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
                {
                    AddBox(piece, trim, new Vector3(corner.X * 0.44f, 0.5f, corner.Y * 0.44f), new Vector3(0.1f, 1f, 0.1f));
                }
                AddBox(piece, trim, new Vector3(0f, 0.5f, 0f), new Vector3(0.92f, 0.07f, 0.92f));
                break;

            // Four posts with a beam frame on top and a cross brace on two sides
            case BlockKind.Stilts:
                foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
                {
                    AddCylinder(piece, color, new Vector3(corner.X * 0.4f, 0f, corner.Y * 0.4f), 0.055f, 1f);
                }
                foreach (float side in new[] { -0.4f, 0.4f })
                {
                    AddBox(piece, trim, new Vector3(0f, 0.95f, side), new Vector3(0.92f, 0.08f, 0.08f));
                    AddBox(piece, trim, new Vector3(side, 0.95f, 0f), new Vector3(0.08f, 0.08f, 0.92f));
                    AddBox(piece, trim, new Vector3(0f, 0.5f, side), new Vector3(1.1f, 0.05f, 0.05f)).Rotation = new Vector3(0f, 0f, 0.75f * side / 0.4f);
                }
                break;

            // A deck on short legs, joists below the planks
            case BlockKind.Platform:
                AddBox(piece, color, new Vector3(0f, 0.43f, 0f), new Vector3(2f, 0.14f, 2f));
                for (int i = -1; i <= 1; i++)
                {
                    AddBox(piece, trim, new Vector3(0f, 0.31f, i * 0.8f), new Vector3(2f, 0.1f, 0.1f));
                }
                foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
                {
                    AddBox(piece, trim, new Vector3(corner.X * 0.9f, 0.18f, corner.Y * 0.9f), new Vector3(0.12f, 0.36f, 0.12f));
                }
                break;

            // A row of sharpened stakes along X, tied with a rail
            case BlockKind.Palisade:
                for (int i = 0; i < 7; i++)
                {
                    float x      = -0.87f + i * 0.29f;
                    float height = 0.74f + 0.08f * (i % 2);
                    AddCylinder(piece, color, new Vector3(x, 0f, 0f), 0.1f, height);
                    AddCone(piece, color, new Vector3(x, height, 0f), 0.1f, 0.2f);
                }
                AddBox(piece, trim, new Vector3(0f, 0.5f, 0.1f), new Vector3(1.96f, 0.06f, 0.05f));
                break;
        }
    }

    private static void BuildRoof(Node3D piece, BlockKind kind, Color color)
    {
        Color trim = color.Darkened(SHADE);
        switch (kind)
        {
            // A wide thatched cone with two bound bands
            case BlockKind.ConeRoof:
                AddCone(piece, color, Vector3.Zero, 1.3f, 1f);
                AddRing(piece, trim, new Vector3(0f, 0.3f, 0f), 0.93f);
                AddRing(piece, trim, new Vector3(0f, 0.6f, 0f), 0.54f);
                break;

            // A four sided roof in two steps with a knob on top
            case BlockKind.PyramidRoof:
                AddPyramid(piece, trim, Vector3.Zero, 1.25f, 0.55f);
                AddPyramid(piece, color, new Vector3(0f, 0.12f, 0f), 1.02f, 0.86f);
                AddBall(piece, trim, new Vector3(0f, 0.98f, 0f), Vector3.One * 0.07f);
                break;

            // A gable roof, the ridge along Z, with a ridge beam
            case BlockKind.GableRoof:
                CellVisual3D.AddMesh(piece, Prism, CellVisual3D.Material(color, 0f, 0.8f), new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 2.2f));
                CellVisual3D.AddMesh(piece, Prism, CellVisual3D.Material(trim, 0f, 0.8f), new Vector3(0f, 0.46f, 0f), new Vector3(2.2f, 0.92f, 2.32f));
                AddBox(piece, trim, new Vector3(0f, 1f, 0f), new Vector3(0.1f, 0.08f, 2.36f));
                break;

            case BlockKind.SmallConeRoof:
                AddCone(piece, color, Vector3.Zero, 0.72f, 1f);
                AddRing(piece, trim, new Vector3(0f, 0.35f, 0f), 0.48f);
                break;

            case BlockKind.SmallPyramidRoof:
                AddPyramid(piece, trim, Vector3.Zero, 0.7f, 0.5f);
                AddPyramid(piece, color, new Vector3(0f, 0.1f, 0f), 0.56f, 0.88f);
                break;

            // Four poles and a tilted cloth with a hanging edge at the front
            case BlockKind.Awning:
                Color pole = BlockCatalog.PaintOf(BlockKind.Stilts, BlockCatalog.DEFAULT_PAINT);
                foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
                {
                    AddCylinder(piece, pole, new Vector3(corner.X * 0.42f, 0f, corner.Y * 0.42f), 0.03f, 0.8f + 0.09f * -corner.X);
                }
                AddBox(piece, color, new Vector3(0f, 0.85f, 0f), new Vector3(1.08f, 0.04f, 1.08f)).Rotation = new Vector3(0f, 0f, -0.2f);
                AddBox(piece, BONE_COLOR, new Vector3(0f, 0.87f, 0f), new Vector3(0.3f, 0.04f, 1.1f)).Rotation = new Vector3(0f, 0f, -0.2f);
                AddBox(piece, color, new Vector3(0.53f, 0.68f, 0f), new Vector3(0.03f, 0.14f, 1.08f));
                break;
        }
    }

    private static void BuildDecor(Node3D piece, BlockKind kind, Color color)
    {
        Color trim = color.Darkened(SHADE);
        switch (kind)
        {
            // A porch: the dark door leans on the wall behind (-X), two posts, a lintel and a step in front
            case BlockKind.Door:
                AddBox(piece, DOOR_COLOR, new Vector3(-0.24f, 0.4f, 0f), new Vector3(0.1f, 0.8f, 0.5f));
                foreach (float side in new[] { -0.3f, 0.3f })
                {
                    AddBox(piece, color, new Vector3(-0.18f, 0.45f, side), new Vector3(0.1f, 0.9f, 0.1f));
                }
                AddBox(piece, color, new Vector3(-0.16f, 0.92f, 0f), new Vector3(0.16f, 0.1f, 0.82f));
                AddBox(piece, trim, new Vector3(-0.02f, 0.99f, 0f), new Vector3(0.46f, 0.04f, 0.9f)).Rotation = new Vector3(0f, 0f, -0.25f);
                AddBox(piece, STEP_COLOR, new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.06f, 0.6f));
                break;

            // A carved pole segment: a face to the front, wings to the sides, rings at foot and top
            case BlockKind.Totem:
                AddCylinder(piece, color, Vector3.Zero, 0.3f, 1f, 0.6f);
                AddRing(piece, trim, new Vector3(0f, 0.06f, 0f), 0.33f);
                AddRing(piece, trim, new Vector3(0f, 0.94f, 0f), 0.33f);
                foreach (float side in new[] { -0.12f, 0.12f })
                {
                    AddBall(piece, BONE_COLOR, new Vector3(0.27f, 0.66f, side), Vector3.One * 0.075f);
                    AddBall(piece, DOOR_COLOR, new Vector3(0.33f, 0.66f, side), Vector3.One * 0.035f);
                }
                AddBox(piece, trim, new Vector3(0.33f, 0.42f, 0f), new Vector3(0.16f, 0.3f, 0.14f));
                AddBox(piece, trim, new Vector3(0f, 0.74f, 0f), new Vector3(0.1f, 0.2f, 1.3f));
                break;

            // A skull with two horns, a crest for a roof or a pole
            case BlockKind.Horns:
                AddBall(piece, color, new Vector3(0f, 0.16f, 0f), new Vector3(0.2f, 0.16f, 0.17f));
                foreach (float side in new[] { -1f, 1f })
                {
                    AddCone(piece, color, new Vector3(0f, 0.14f, side * 0.14f), 0.07f, 0.46f).Rotation = new Vector3(side * 0.9f, 0f, 0f);
                    AddBall(piece, DOOR_COLOR, new Vector3(0.17f, 0.19f, side * 0.07f), Vector3.One * 0.035f);
                }
                break;

            // A table with goods on it
            case BlockKind.Stall:
                AddBox(piece, color, new Vector3(0f, 0.4f, 0f), new Vector3(0.94f, 0.06f, 0.94f));
                foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) })
                {
                    AddBox(piece, trim, new Vector3(corner.X * 0.4f, 0.19f, corner.Y * 0.4f), new Vector3(0.07f, 0.38f, 0.07f));
                }
                AddBall(piece, FRUIT_RED, new Vector3(0.2f, 0.49f, 0.22f), Vector3.One * 0.075f);
                AddBall(piece, FRUIT_GOLD, new Vector3(0.05f, 0.49f, 0.3f), Vector3.One * 0.07f);
                AddBall(piece, FRUIT_GREEN, new Vector3(0.22f, 0.49f, 0.05f), Vector3.One * 0.07f);
                AddBox(piece, trim, new Vector3(-0.2f, 0.5f, -0.2f), new Vector3(0.26f, 0.14f, 0.26f));
                break;

            case BlockKind.Crate:
                AddBox(piece, color, new Vector3(0f, 0.21f, 0f), new Vector3(0.4f, 0.42f, 0.4f));
                AddBox(piece, trim, new Vector3(0f, 0.08f, 0f), new Vector3(0.43f, 0.06f, 0.43f));
                AddBox(piece, trim, new Vector3(0f, 0.34f, 0f), new Vector3(0.43f, 0.06f, 0.43f));
                break;

            // A woven basket full of fruit
            case BlockKind.Basket:
                AddCylinder(piece, color, Vector3.Zero, 0.2f, 0.3f);
                AddRing(piece, trim, new Vector3(0f, 0.3f, 0f), 0.21f);
                AddBall(piece, FRUIT_RED, new Vector3(0.06f, 0.33f, 0.05f), Vector3.One * 0.075f);
                AddBall(piece, FRUIT_GOLD, new Vector3(-0.07f, 0.33f, 0.02f), Vector3.One * 0.07f);
                AddBall(piece, FRUIT_GREEN, new Vector3(0f, 0.34f, -0.08f), Vector3.One * 0.065f);
                break;

            // A tall pole with a cloth to the front
            case BlockKind.Banner:
                Color staff = BlockCatalog.PaintOf(BlockKind.Stilts, BlockCatalog.DEFAULT_PAINT);
                AddCylinder(piece, staff, Vector3.Zero, 0.035f, 1.5f);
                AddBall(piece, staff, new Vector3(0f, 1.52f, 0f), Vector3.One * 0.06f);
                AddBox(piece, color, new Vector3(0.26f, 1.22f, 0f), new Vector3(0.46f, 0.4f, 0.025f));
                AddBox(piece, BONE_COLOR, new Vector3(0.26f, 1.22f, 0f), new Vector3(0.16f, 0.16f, 0.035f));
                break;

            // A pole with a bowl and a glowing flame
            case BlockKind.Torch:
                AddCylinder(piece, color, Vector3.Zero, 0.035f, 0.8f);
                AddCylinder(piece, trim, new Vector3(0f, 0.78f, 0f), 0.09f, 0.08f);
                AddBall(piece, FLAME_COLOR, new Vector3(0f, 0.94f, 0f), new Vector3(0.07f, 0.13f, 0.07f), 2.2f)
                    .CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                break;

            // Two rails with rungs, leaning back (-X) against what stands behind
            case BlockKind.Ladder:
                var ladder = new Node3D { Position = new Vector3(0.12f, 0f, 0f), Rotation = new Vector3(0f, 0f, 0.22f) };
                piece.AddChild(ladder);
                foreach (float side in new[] { -0.15f, 0.15f })
                {
                    AddBox(ladder, color, new Vector3(0f, 0.52f, side), new Vector3(0.045f, 1.06f, 0.045f));
                }
                for (int i = 0; i < 5; i++)
                {
                    AddBox(ladder, trim, new Vector3(0f, 0.14f + i * 0.2f, 0f), new Vector3(0.035f, 0.035f, 0.3f));
                }
                break;
        }
    }
}
