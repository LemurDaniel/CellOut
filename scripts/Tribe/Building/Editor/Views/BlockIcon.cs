// NOTE:
//   Small live 3D icon of one building block for the palette of the village builder: the
//   block alone in its own studio, the camera at a distance that fits its size.
//
//   var icon = new BlockIcon(BlockKind.Totem, 72);
//   tile.AddChild(icon);

using Godot;

namespace CellOut;

public partial class BlockIcon : SubViewportContainer
{
    private const float CAMERA_FOV = 35f;
    private const float FIT_MARGIN = 1.15f;
    private static readonly Vector3 CAMERA_DIRECTION = new Vector3(0.75f, 0.6f, 1f).Normalized();

    /// <summary>Builds the icon, it renders on its own.</summary>
    /// <param name="kind">(Required) The block shown, in its default paint.</param>
    /// <param name="pixels">(Required) Edge length of the square icon.</param>
    public BlockIcon(BlockKind kind, int pixels)
    {
        Stretch           = true;
        CustomMinimumSize = new Vector2(pixels, pixels);
        MouseFilter       = MouseFilterEnum.Ignore;

        SubViewport viewport = PartIcon.NewStudio(new Vector2I(pixels, pixels));
        AddChild(viewport);
        viewport.AddChild(BuildingVisual3D.NewBlock(kind, BlockCatalog.DEFAULT_PAINT));

        // The camera looks at the middle of the block from the front right, far enough for its bounding sphere
        BlockInfo info   = BlockCatalog.Get(kind);
        Vector3   size   = new Vector3(info.Width, info.Height, info.Depth) * BuildingVisual3D.CELL;
        Vector3   center = Vector3.Up * size.Y * 0.5f;
        float     radius = size.Length() * 0.5f;
        float     reach  = radius / Mathf.Sin(Mathf.DegToRad(CAMERA_FOV) * 0.5f) * FIT_MARGIN;
        var       camera = new Camera3D { Fov = CAMERA_FOV, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-CAMERA_DIRECTION, Vector3.Up), center + CAMERA_DIRECTION * reach);
        viewport.AddChild(camera);
    }
}
