// NOTE:
//   Bridge between the game logic (2D plane) and the 3D rendering.
//   A logic point (x, y) lies at (x, 0, y) in 3D. The camera looks down from above,
//   screen up is -y in logic and -z in 3D.
//
//   Vector3 pos = Plane3D.To3D(cell.GlobalPosition);
//   pos.Y -= Plane3D.Drop(cell.GlobalPosition);     // the planet curvature, 0 in the soup

//
//   Chapters:
//     CONVERSIONS   Plane to 3D, facing to yaw and the mouse on the plane.
//     PLANET        The curvature of the planet, the same as shaders/planet.gdshaderinc.
//

using Godot;

namespace CellOut;

public static class Plane3D
{
    // ===========================================================================
    // =============================   CONVERSIONS   =============================
    // ===========================================================================
    //
    //   To3D          Lifts a plane point into 3D, optionally above or below the plane.
    //   FacingToYaw   Turns a 2D facing angle into the Y rotation of a Node3D.
    //   MouseOnPlane  Projects the mouse cursor onto the plane.
    //

    /// <summary>Lifts a plane point into 3D.</summary>
    /// <param name="point">(Required) The point on the logic plane.</param>
    /// <param name="height">(Optional) Height above the plane, negative is below. Defaults to 0.</param>
    /// <returns>The 3D position (point.X, height, point.Y).</returns>
    /// <example><code>Plane3D.To3D(new Vector2(3, 4))  // (3, 0, 4)</code></example>
    public static Vector3 To3D(Vector2 point, float height = 0f)
    {
        return new Vector3(point.X, height, point.Y);
    }

    /// <summary>
    /// Turns a 2D facing angle into the Y rotation of a Node3D whose local +X points forward.
    /// A Y rotation by t maps +X to (cos t, 0, -sin t), so t = -facing.
    /// </summary>
    /// <param name="facing">(Required) The facing angle on the plane in radians.</param>
    /// <returns>The Y rotation in radians.</returns>
    public static float FacingToYaw(float facing)
    {
        return -facing;
    }

    /// <summary>Projects the mouse cursor onto the plane (y = 0) through the current 3D camera.</summary>
    /// <param name="viewport">(Required) The viewport holding the camera.</param>
    /// <param name="fallback">(Required) Returned without camera or with a ray parallel to the plane.</param>
    /// <returns>The plane point under the mouse cursor.</returns>
    public static Vector2 MouseOnPlane(Viewport viewport, Vector2 fallback)
    {
        Camera3D camera = viewport.GetCamera3D();
        if (null == camera)
        {
            return fallback;
        }

        Vector2 mouse     = viewport.GetMousePosition();
        Vector3 origin    = camera.ProjectRayOrigin(mouse);
        Vector3 direction = camera.ProjectRayNormal(mouse);
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return fallback;
        }

        Vector3 hit = origin + direction * (-origin.Y / direction.Y);
        return new Vector2(hit.X, hit.Z);
    }

    // ===========================================================================
    // ===============================   PLANET   ================================
    // ===========================================================================
    //
    //   Curve      1 / (2 * planet radius), 0 = flat. Set by Main with the shader global.
    //   Origin     Plane point around which the world stays flat (the camera).
    //   SetCurve   Sets both here and as shader globals planet_curve / planet_origin.
    //   Drop       How far a plane point sinks below the flat plane.
    //   SeaDrop    How far swimming bodies sink below the plane, so they stay under the low
    //              sea of the third person view. Set by Main, 0 in the top view.
    //

    public static float Curve { get; private set; }
    public static Vector2 CurveOrigin { get; private set; }

    /// <summary>World units swimming bodies and sea food sink, set by Main.</summary>
    public static float SeaDrop { get; set; }

    /// <summary>Sets the curvature for the CPU (Drop) and the shaders (planet.gdshaderinc).</summary>
    /// <param name="curve">(Required) 1 / (2 * planet radius) in 1 / world units, 0 = flat.</param>
    /// <param name="origin">(Required) 3D point the world bends away from, the camera.</param>
    public static void SetCurve(float curve, Vector3 origin)
    {
        Curve       = curve;
        CurveOrigin = new Vector2(origin.X, origin.Z);
        RenderingServer.GlobalShaderParameterSet("planet_curve", curve);
        RenderingServer.GlobalShaderParameterSet("planet_origin", origin);
    }

    /// <summary>How far a plane point sinks below the plane through the curvature.</summary>
    /// <example><code>Plane3D.Drop(point)  // 0 while the world is flat</code></example>
    public static float Drop(Vector2 point)
    {
        return point.DistanceSquaredTo(CurveOrigin) * Curve;
    }
}
