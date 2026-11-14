// NOTE:
//   Small live 3D icon of a whole building: the blueprint of a function in its own
//   studio, the camera framed on its blocks (not on the whole plot). The icon of a building
//   function wherever one is picked: the tabs of the village builder and the build
//   buttons of the HUD, so the player sees the own design instead of a word.
//
//   var icon = new BuildingIcon(64);
//   icon.Display(tribe.DesignOf(BuildingKind.Market));
//   button.AddChild(icon);

using Godot;

namespace CellOut;

public partial class BuildingIcon : SubViewportContainer
{
    private const float CAMERA_FOV = 35f;
    private const float FIT_MARGIN = 1.05f;
    private static readonly Vector3 CAMERA_DIRECTION = new Vector3(0.8f, 0.55f, 1f).Normalized();

    private readonly BuildingVisual3D _building = new();
    private readonly Camera3D _camera = new() { Fov = CAMERA_FOV, Current = true };
    private readonly BuildingDesign _shown = new(BuildingKind.Dwelling);
    private bool _hasShown;

    /// <summary>Builds the empty icon, Display fills it.</summary>
    /// <param name="pixels">(Required) Edge length of the square icon.</param>
    public BuildingIcon(int pixels)
    {
        Stretch           = true;
        CustomMinimumSize = new Vector2(pixels, pixels);
        Size              = new Vector2(pixels, pixels);
        MouseFilter       = MouseFilterEnum.Ignore;

        SubViewport viewport = PartIcon.NewStudio(new Vector2I(pixels, pixels));
        AddChild(viewport);
        viewport.AddChild(_building);
        viewport.AddChild(_camera);
    }

    /// <summary>Shows a blueprint, rebuilt only when its blocks changed since the last call.</summary>
    /// <param name="design">(Required) The blueprint.</param>
    public void Display(BuildingDesign design)
    {
        if (_hasShown && System.Linq.Enumerable.SequenceEqual(_shown.Blocks, design.Blocks))
        {
            return;
        }
        _hasShown = true;
        _shown.CopyFrom(design);
        _building.Build(design);

        // The box around every block, in model units; an empty design shows the middle of the plot
        Vector3 low  = Vector3.One * float.PositiveInfinity;
        Vector3 high = Vector3.One * float.NegativeInfinity;
        foreach (PlacedBlock block in design.Blocks)
        {
            (int width, int depth) = BlockCatalog.Footprint(block.Kind, block.Turns);
            Vector3 pivot = BuildingVisual3D.PivotOf(block.Kind, block.X, block.Y, block.Z, block.Turns);
            Vector3 half  = new Vector3(width, 0f, depth) * BuildingVisual3D.CELL * 0.5f;
            low  = low.Min(pivot - half);
            high = high.Max(pivot + half + Vector3.Up * BlockCatalog.Get(block.Kind).Height * BuildingVisual3D.CELL);
        }
        if (0 == design.Blocks.Count)
        {
            low  = -Vector3.One;
            high = Vector3.One;
        }

        // The camera looks at the middle of that box from the front right, far enough for its bounding sphere
        Vector3 center = (low + high) * 0.5f;
        float   radius = Mathf.Max((high - low).Length() * 0.5f, 1f);
        float   reach  = radius / Mathf.Sin(Mathf.DegToRad(CAMERA_FOV) * 0.5f) * FIT_MARGIN;
        _camera.Transform = new Transform3D(Basis.LookingAt(-CAMERA_DIRECTION, Vector3.Up), center + CAMERA_DIRECTION * reach);
    }
}
