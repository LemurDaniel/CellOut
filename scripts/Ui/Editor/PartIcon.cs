// NOTE:
//   Small live 3D icon of one editor part: a mini cell that carries only this part,
//   rendered in its own SubViewport and framed on the part. Used by the CellEditor
//   palette tiles; with isPartOnly the cell is left out, that is the part hanging at
//   the cursor while dragging. Also holds the shared studio (environment and light)
//   that the editor preview uses.
//
//   var icon = new PartIcon(PartKind.Jaw, player.BodyColor, 64);
//   tile.AddChild(icon);

//
//   Chapters:
//     STUDIO      Viewport with environment and light, shared with the editor preview.
//     BUILD ICON  Mini cell with the part, camera framed on the part.
//

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
    //   IconPoint    Where the part sits on the mini cell: rim parts in front,
    //                the flagellum behind, inner parts near the nucleus.
    //

    private const float CAMERA_DISTANCE = 2.7f;
    private const float CAMERA_PITCH = 0.95f;        // radians above the plane
    private const float PART_ONLY_ZOOM_IN = 0.6f;    // the bare part is small, the camera comes closer

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
        var camera = new Camera3D { Fov = 40f, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), target + offset);
        viewport.AddChild(camera);
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
