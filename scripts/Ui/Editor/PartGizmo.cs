// NOTE:
//   Move, turn and scale gizmo of the editors: three arrows (X red, Y green, Z blue)
//   shift along an axis, three rings turn around one, a golden ring around X scales
//   (vertebrae). Which handles show is set per use (ShowHandles). Drawn on top
//   of everything (no depth test) and kept at the same size on screen. The gizmo only
//   answers questions in screen pixels (which handle is under the cursor, how far a drag
//   moves or turns), the editor applies the result to the design.
//
//   var gizmo = new PartGizmo { Camera = camera };
//   previewRoot.AddChild(gizmo);
//   GizmoHandle handle = gizmo.HandleAt(mousePosition);

//
//   Chapters:
//     SETTINGS      Handles, sizes and colors.
//     BUILD GIZMO   Arrows, rings and the center dot, one material per handle.
//     DRAG GIZMO    Hit test, screen size, highlight and the drag deltas.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public enum GizmoHandle { None, MoveX, MoveY, MoveZ, TurnX, TurnY, TurnZ, Scale }

public partial class PartGizmo : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Handles   Axis per handle, move or turn, which handles show.
    //   Look      Sizes in gizmo units and the axis colors.
    //

    // ---------------------------------------------------------------------------
    // Handles
    //   Axis per handle, move or turn.
    // ---------------------------------------------------------------------------

    /// <summary>(Required) The camera of the preview, for the hit test and the screen size.</summary>
    public Camera3D Camera { get; set; }

    /// <summary>The handle under the cursor, lit up.</summary>
    public GizmoHandle Hovered { get; set; }

    /// <summary>The handle being dragged, lit up while the drag lasts.</summary>
    public GizmoHandle Active { get; set; }

    /// <summary>The world axis of a handle, X forward, Y up, Z right flank.</summary>
    public static Vector3 AxisOf(GizmoHandle handle)
    {
        return handle switch
        {
            GizmoHandle.MoveX or GizmoHandle.TurnX => Vector3.Right,
            GizmoHandle.MoveY or GizmoHandle.TurnY => Vector3.Up,
            GizmoHandle.MoveZ or GizmoHandle.TurnZ => Vector3.Back,
            _                                      => Vector3.Zero,
        };
    }

    public static bool IsTurn(GizmoHandle handle)
    {
        return GizmoHandle.TurnX == handle || GizmoHandle.TurnY == handle || GizmoHandle.TurnZ == handle;
    }

    /// <summary>Every handle but the scale ring: shift and turn a part.</summary>
    public static readonly GizmoHandle[] PART_HANDLES =
    {
        GizmoHandle.MoveX, GizmoHandle.MoveY, GizmoHandle.MoveZ, GizmoHandle.TurnX, GizmoHandle.TurnY, GizmoHandle.TurnZ,
    };

    private readonly HashSet<GizmoHandle> _shown = new(PART_HANDLES);

    /// <summary>Shows only the given handles, the others neither draw nor pick.</summary>
    /// <example><code>gizmo.ShowHandles(GizmoHandle.MoveX, GizmoHandle.MoveY, GizmoHandle.Scale);</code></example>
    public void ShowHandles(params GizmoHandle[] handles)
    {
        _shown.Clear();
        _shown.UnionWith(handles);
        foreach ((GizmoHandle handle, List<MeshInstance3D> meshes) in _meshes)
        {
            foreach (MeshInstance3D mesh in meshes)
            {
                mesh.Visible = _shown.Contains(handle);
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Look
    //   Sizes in gizmo units and the axis colors.
    // ---------------------------------------------------------------------------

    private const float SCREEN_SHARE = 0.1f;        // gizmo units per unit of camera distance
    private const float ARROW_LENGTH = 1f;
    private const float SHAFT_RADIUS = 0.03f;
    private const float RING_RADIUS = 0.68f;
    private const float RING_THICKNESS = 0.035f;
    private const float ARROW_PICK_PIXELS = 12f;
    private const float RING_PICK_PIXELS = 9f;
    private const float SCALE_RING_RADIUS = 0.95f;   // outside the turn rings
    private static readonly Color SCALE_COLOR = new(1f, 0.78f, 0.3f);
    private const int RING_SAMPLES = 40;
    private static readonly Color X_COLOR = new(1f, 0.38f, 0.42f);
    private static readonly Color Y_COLOR = new(0.5f, 0.95f, 0.5f);
    private static readonly Color Z_COLOR = new(0.42f, 0.66f, 1f);
    private static readonly Color HOT_COLOR = new(1f, 0.92f, 0.45f);
    private const float IDLE_ALPHA = 0.55f;          // the part stays visible through the handles
    private const float LIT_ALPHA = 0.85f;           // hovered or dragged handle
    private const float DRAG_REST_ALPHA = 0.15f;     // the other handles while one is dragged

    // ===========================================================================
    // =============================   BUILD GIZMO   =============================
    // ===========================================================================
    //
    //   _Ready        Builds the handles, hidden until the editor shows the gizmo.
    //   AddArrow      Shaft and tip along one axis.
    //   AddRing       A thin ring around one axis.
    //   NewMaterial   Unshaded, no depth test, drawn after the scene.
    //   ToAxis        Turns a Y-aligned mesh onto an axis.
    //

    private readonly Dictionary<GizmoHandle, (StandardMaterial3D Material, Color Color)> _materials = new();
    private readonly Dictionary<GizmoHandle, List<MeshInstance3D>> _meshes = new();

    public override void _Ready()
    {
        Visible = false;
        AddArrow(GizmoHandle.MoveX, X_COLOR);
        AddArrow(GizmoHandle.MoveY, Y_COLOR);
        AddArrow(GizmoHandle.MoveZ, Z_COLOR);
        AddRing(GizmoHandle.TurnX, X_COLOR);
        AddRing(GizmoHandle.TurnY, Y_COLOR);
        AddRing(GizmoHandle.TurnZ, Z_COLOR);
        AddRing(GizmoHandle.Scale, SCALE_COLOR, SCALE_RING_RADIUS, Vector3.Right);
        AddMesh(GizmoHandle.None, CellVisual3D.SphereMesh(true), NewMaterial(Colors.White), Basis.FromScale(Vector3.One * 0.07f), Vector3.Zero);
        ShowHandles(PART_HANDLES);
    }

    private void AddArrow(GizmoHandle handle, Color color)
    {
        StandardMaterial3D material = NewMaterial(color);
        _materials[handle] = (material, color);
        Vector3 axis = AxisOf(handle);
        Basis   to   = ToAxis(axis);
        float   tip  = 0.22f;
        AddMesh(handle, CellVisual3D.RodMesh(), material, to * Basis.FromScale(new Vector3(SHAFT_RADIUS, ARROW_LENGTH - tip, SHAFT_RADIUS)), axis * (ARROW_LENGTH - tip) * 0.5f);
        AddMesh(handle, CellVisual3D.ConeMesh(), material, to * Basis.FromScale(new Vector3(0.7f, tip / 0.6f, 0.7f)), axis * (ARROW_LENGTH - tip * 0.5f));
    }

    /// <param name="radius">(Optional) Ring radius in gizmo units. Defaults to RING_RADIUS.</param>
    /// <param name="axis">(Optional) Axis the ring goes around. Defaults to the axis of the handle.</param>
    private void AddRing(GizmoHandle handle, Color color, float radius = RING_RADIUS, Vector3? axis = null)
    {
        StandardMaterial3D material = NewMaterial(color);
        _materials[handle] = (material, color);
        float thickness = GizmoHandle.Scale == handle ? RING_THICKNESS * 1.6f : RING_THICKNESS;
        var   ring      = new TorusMesh { InnerRadius = radius - thickness, OuterRadius = radius + thickness, Rings = 48, RingSegments = 6 };
        AddMesh(handle, ring, material, ToAxis(axis ?? AxisOf(handle)), Vector3.Zero);
    }

    private void AddMesh(GizmoHandle handle, Mesh mesh, Material material, Basis basis, Vector3 position)
    {
        var instance = new MeshInstance3D
        {
            Mesh             = mesh,
            MaterialOverride = material,
            Transform        = new Transform3D(basis, position),
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(instance);
        if (!_meshes.TryGetValue(handle, out List<MeshInstance3D> meshes))
        {
            _meshes[handle] = meshes = new List<MeshInstance3D>();
        }
        meshes.Add(instance);
    }

    private static StandardMaterial3D NewMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            ShadingMode    = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor    = color with { A = IDLE_ALPHA },
            Transparency   = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest    = true,
            RenderPriority = 10,
        };
    }

    /// <summary>Godot meshes stand along +Y (cylinders, cones, the torus axis): this turns +Y onto the axis.</summary>
    private static Basis ToAxis(Vector3 axis)
    {
        if (Vector3.Right == axis) return new Basis(Vector3.Back, -Mathf.Pi * 0.5f);
        if (Vector3.Back == axis) return new Basis(Vector3.Right, Mathf.Pi * 0.5f);
        return Basis.Identity;
    }

    // ===========================================================================
    // =============================   DRAG GIZMO   ==============================
    // ===========================================================================
    //
    //   _Process     Same size on screen at every zoom, lights the hovered or active handle.
    //   HandleAt     The handle under a pixel: arrows first, then rings.
    //   MoveDelta    World shift of a mouse motion along the axis of a move handle.
    //   DragAlong    World distance of a mouse motion along any axis at a point.
    //   TurnDelta    Angle of a mouse motion around the axis of a turn handle.
    //   ScaleFactor  Growth of a mouse motion on the scale ring.
    //

    public override void _Process(double delta)
    {
        if (!Visible || null == Camera)
        {
            return;
        }
        Scale = Vector3.One * Camera.GlobalPosition.DistanceTo(GlobalPosition) * SCREEN_SHARE;
        // Lit handle stands out, while dragging the others fade almost away
        GizmoHandle lit       = GizmoHandle.None != Active ? Active : Hovered;
        float       restAlpha =GizmoHandle.None != Active ? DRAG_REST_ALPHA : IDLE_ALPHA;
        foreach ((GizmoHandle handle, (StandardMaterial3D material, Color color)) in _materials)
        {
            material.AlbedoColor = handle == lit ? HOT_COLOR with { A = LIT_ALPHA } : color with { A = restAlpha };
        }
    }

    /// <summary>The handle under a pixel of the preview.</summary>
    /// <param name="pixel">(Required) Position in the preview viewport.</param>
    /// <returns>GizmoHandle.None when the gizmo is hidden or nothing is close.</returns>
    public GizmoHandle HandleAt(Vector2 pixel)
    {
        if (!Visible || null == Camera || Camera.IsPositionBehind(GlobalPosition))
        {
            return GizmoHandle.None;
        }
        Vector3 origin = GlobalPosition;
        float   scale  = Scale.X;
        Vector2 center = Camera.UnprojectPosition(origin);

        // Arrows: distance to the shaft on screen
        GizmoHandle best     = GizmoHandle.None;
        float       bestDist = ARROW_PICK_PIXELS;
        foreach (GizmoHandle handle in new[] { GizmoHandle.MoveX, GizmoHandle.MoveY, GizmoHandle.MoveZ }.Where(_shown.Contains))
        {
            Vector3 tip = origin + AxisOf(handle) * scale * ARROW_LENGTH;
            if (Camera.IsPositionBehind(tip))
            {
                continue;
            }
            float distance = DistanceToSegment(pixel, center, Camera.UnprojectPosition(tip));
            if (distance < bestDist)
            {
                bestDist = distance;
                best     = handle;
            }
        }
        if (GizmoHandle.None != best)
        {
            return best;
        }

        // Rings: distance to the ring outline, sampled as a polygon on screen
        bestDist = RING_PICK_PIXELS;
        foreach (GizmoHandle handle in new[] { GizmoHandle.TurnX, GizmoHandle.TurnY, GizmoHandle.TurnZ, GizmoHandle.Scale }.Where(_shown.Contains))
        {
            bool    isScale  = GizmoHandle.Scale == handle;
            Basis   to       = ToAxis(isScale ? Vector3.Right : AxisOf(handle));
            float   ring     = isScale ? SCALE_RING_RADIUS : RING_RADIUS;
            Vector2 previous = Vector2.Zero;
            for (int i = 0; i <= RING_SAMPLES; i++)
            {
                float   angle = Mathf.Tau * i / RING_SAMPLES;
                Vector3 point = origin + to * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring * scale;
                Vector2 pixelOnRing = Camera.UnprojectPosition(point);
                if (i > 0)
                {
                    float distance = DistanceToSegment(pixel, previous, pixelOnRing);
                    if (distance < bestDist)
                    {
                        bestDist = distance;
                        best     = handle;
                    }
                }
                previous = pixelOnRing;
            }
        }
        return best;
    }

    /// <summary>
    /// Projects the mouse motion onto the axis as it appears on screen: the shift is the
    /// share of the screen arrow the mouse travelled along it, times the arrow length.
    /// </summary>
    /// <param name="handle">(Required) A move handle.</param>
    /// <param name="relative">(Required) Mouse motion in pixels.</param>
    /// <returns>The world shift, zero when the axis points straight at the camera.</returns>
    public Vector3 MoveDelta(GizmoHandle handle, Vector2 relative)
    {
        Vector3 axis = AxisOf(handle);
        return axis * DragAlong(Camera, GlobalPosition, axis, relative, Scale.X * ARROW_LENGTH);
    }

    /// <summary>
    /// How far a mouse motion moves along a world axis at a point: the motion projected
    /// onto the axis as it appears on screen. Shared with the vertebra handles of the
    /// CreatureEditor.
    /// </summary>
    /// <param name="camera">(Required) The camera of the view.</param>
    /// <param name="at">(Required) World point the drag acts on.</param>
    /// <param name="axis">(Required) Normalized world axis.</param>
    /// <param name="relative">(Required) Mouse motion in pixels.</param>
    /// <param name="length">(Optional) World length sampled on screen. Defaults to 0.1.</param>
    /// <returns>World distance along the axis, 0 when the axis points straight at the camera.</returns>
    public static float DragAlong(Camera3D camera, Vector3 at, Vector3 axis, Vector2 relative, float length = 0.1f)
    {
        Vector2 screen = camera.UnprojectPosition(at + axis * length) - camera.UnprojectPosition(at);
        float   square = screen.LengthSquared();
        return square < 1f ? 0f : relative.Dot(screen) / square * length;
    }

    /// <summary>
    /// The angle the mouse swept around the gizmo center on screen. Screen Y points down,
    /// so a counterclockwise sweep lowers the screen angle; with the axis facing the
    /// camera that is a positive (right hand) turn, facing away a negative one.
    /// </summary>
    /// <param name="handle">(Required) A turn handle.</param>
    /// <param name="from">(Required) Mouse position before the motion.</param>
    /// <param name="to">(Required) Mouse position after the motion.</param>
    /// <returns>The turn in radians around AxisOf(handle).</returns>
    public float TurnDelta(GizmoHandle handle, Vector2 from, Vector2 to)
    {
        Vector2 center = Camera.UnprojectPosition(GlobalPosition);
        float   swept  = Mathf.Wrap((to - center).Angle() - (from - center).Angle(), -Mathf.Pi, Mathf.Pi);
        bool    isFacingCamera = AxisOf(handle).Dot(Camera.GlobalPosition - GlobalPosition) >= 0f;
        return isFacingCamera ? -swept : swept;
    }

    /// <summary>Scale factor of a drag on the scale ring: how much farther from the gizmo center the mouse moved.</summary>
    /// <param name="from">(Required) Mouse position before the motion.</param>
    /// <param name="to">(Required) Mouse position after the motion.</param>
    /// <returns>Above 1 dragged outwards (bigger), below 1 inwards (smaller).</returns>
    public float ScaleFactor(Vector2 from, Vector2 to)
    {
        Vector2 center = Camera.UnprojectPosition(GlobalPosition);
        float   before = Mathf.Max(from.DistanceTo(center), 8f);
        return Mathf.Max(to.DistanceTo(center), 8f) / before;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        return point.DistanceTo(Geometry2D.GetClosestPointToSegment(point, from, to));
    }
}
