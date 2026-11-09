// NOTE:
//   Small live 3D icon of one editor part: a mini cell that carries only this part,
//   rendered in its own SubViewport and framed on the part. With isPartOnly the cell is
//   left out and the camera frames the bare part model once it is built: the CellEditor
//   palette tiles and the part hanging at the cursor while dragging. Also holds the
//   shared studio (environment and light) that the editor preview uses.
//
//   var icon = new PartIcon(PartKind.Jaw, player.BodyColor, 64);
//   tile.AddChild(icon);

//
//   Chapters:
//     STUDIO      Viewport with environment and light, shared with the editor preview.
//     BUILD ICON  Mini cell with the part (or the bare part), camera framed on the part.
//

using System.Linq;
using Godot;

namespace CellOut;

public partial class PartIcon : SubViewportContainer
{
    // ===========================================================================
    // ===============================   STUDIO   ================================
    // ===========================================================================
    //
    //   NewStudio   Transparent SubViewport with its own world, ambient and key light.
    //

    /// <summary>A transparent SubViewport with its own 3D world, ambient light and a key light.</summary>
    /// <param name="size">(Required) Render size in pixels, a stretching container overrides it.</param>
    /// <returns>The viewport, ready for a camera and content.</returns>
    /// <example><code>SubViewport viewport = PartIcon.NewStudio(new Vector2I(64, 64));</code></example>
    public static SubViewport NewStudio(Vector2I size)
    {
        var viewport = new SubViewport
        {
            OwnWorld3D         = true,
            TransparentBg      = true,
            Msaa3D             = Viewport.Msaa.Msaa2X,
            HandleInputLocally = false,
            Size               = size,
        };

        var environment = new Godot.Environment
        {
            BackgroundMode     = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor  = new Color(0.68f, 0.74f, 0.88f),
            AmbientLightEnergy = 0.7f,
            SsaoEnabled        = true,                   // contact shadows where parts meet the body (unit scale)
            SsaoRadius         = 0.35f,
            SsaoIntensity      = 1.6f,
        };
        viewport.AddChild(new WorldEnvironment { Environment = environment });
        viewport.AddChild(new DirectionalLight3D
        {
            Basis       = Basis.LookingAt(new Vector3(0.35f, -1f, 0.45f), Vector3.Up),
            LightEnergy = 1.1f,
        });
        return viewport;
    }

    // ===========================================================================
    // =============================   BUILD ICON   ==============================
    // ===========================================================================
    //
    //   PartIcon     Builds the studio, the mini cell (or the bare part) and the camera.
    //   _Process     Bare part: frames the camera on the part once its model is built.
    //   IconPoint    Where the part sits on the mini cell: rim parts in front,
    //                the flagellum behind, inner parts near the nucleus.
    //

    private const float CAMERA_DISTANCE = 2.7f;
    private const float CAMERA_PITCH = 0.95f;        // radians above the plane
    private const float PART_ONLY_ZOOM_IN = 0.6f;    // until the bare part is framed
    private const float FIT_MARGIN = 1.05f;

    private readonly Camera3D _camera;
    private readonly SubViewport _viewport;
    private readonly bool _isPartOnly;
    private bool _isFramed;

    /// <summary>Builds the icon, it renders and animates on its own.</summary>
    /// <param name="kind">(Required) The part shown.</param>
    /// <param name="bodyColor">(Required) Color of the mini cell, usually the player's.</param>
    /// <param name="pixels">(Required) Edge length of the square icon.</param>
    /// <param name="isPartOnly">(Optional) Shows the bare part without the cell, e.g. for dragging. Defaults to false.</param>
    public PartIcon(PartKind kind, Color bodyColor, int pixels, bool isPartOnly = false)
    {
        Stretch           = true;
        CustomMinimumSize = new Vector2(pixels, pixels);
        MouseFilter       = MouseFilterEnum.Ignore;

        SubViewport viewport = NewStudio(new Vector2I(pixels, pixels));
        AddChild(viewport);
        _viewport   = viewport;
        _isPartOnly = isPartOnly;

        // Mini cell that carries only this part
        Vector2 point  = IconPoint(kind);
        var     design = new CellDesign();
        design.TryPlace(kind, point, 0);
        viewport.AddChild(new CellBody { Radius = 1f, BodyColor = bodyColor, Design = design, IsPartsOnly = isPartOnly });

        // Camera from the front left above, aimed between the center and the part (or at the bare part)
        float   distance = CAMERA_DISTANCE * (isPartOnly ? PART_ONLY_ZOOM_IN : 1f);
        float   focus    = isPartOnly ? (PartKind.Flagellum == kind ? 1.6f : 1f) : 0.6f;
        Vector3 target   = Plane3D.To3D(point * focus);
        Vector3 offset   = new Vector3(-0.35f, Mathf.Sin(CAMERA_PITCH), Mathf.Cos(CAMERA_PITCH)).Normalized() * distance;
        _camera = new Camera3D { Fov = 40f, Current = true };
        _camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), target + offset);
        viewport.AddChild(_camera);
    }

    /// <summary>
    /// The cell visual builds its meshes in its first frame, so the bare part is framed
    /// as soon as visible meshes exist: the camera keeps its direction and moves to
    /// where the bounding sphere of the part fills the view.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_isPartOnly || _isFramed)
        {
            return;
        }
        Aabb? bounds = null;
        foreach (MeshInstance3D mesh in _viewport.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (!mesh.IsVisibleInTree())
            {
                continue;
            }
            Aabb box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = null == bounds ? box : bounds.Value.Merge(box);
        }
        if (null == bounds)
        {
            return;
        }
        Vector3 center    = bounds.Value.GetCenter();
        float   radius    = Mathf.Max(bounds.Value.Size.Length() * 0.5f, 0.05f);
        float   distance  = radius / Mathf.Sin(Mathf.DegToRad(_camera.Fov) * 0.5f) * FIT_MARGIN;
        Vector3 direction = _camera.GlobalTransform.Basis.Z;               // the camera looks along -Z
        _camera.GlobalTransform = new Transform3D(_camera.GlobalTransform.Basis, center + direction * distance);
        _isFramed = true;
    }

    /// <summary>
    /// Front (+X) shows rim parts best, the flagellum belongs behind. Inner parts sit
    /// slightly in front of the nucleus.
    /// </summary>
    private static Vector2 IconPoint(PartKind kind)
    {
        PartInfo info = PartCatalog.Get(kind);
        return info.Placement switch
        {
            Placement.Rim when PartKind.Flagellum == kind => Vector2.Left,
            Placement.Rim                                 => Vector2.Right,
            _                                             => new Vector2(0.35f, 0.15f),
        };
    }
}
