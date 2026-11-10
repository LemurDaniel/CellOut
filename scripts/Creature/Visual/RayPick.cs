// NOTE:
//   Ray against a bounding box, for picking parts on the turntable of the editors
//   (Visual/CreaturePartPoser.cs, Cell/CellVisual3D.cs). Pure math on value structs, without
//   engine calls.
//
//   float? distance = RayPick.RayEntry(mesh.GlobalTransform * mesh.GetAabb(), from, direction);

//
//   Chapters:
//     HIT A BOX   Where a ray enters a box.
//

using Godot;

namespace CellOut;

public static class RayPick
{
    // ===========================================================================
    // ==============================   HIT A BOX   ==============================
    // ===========================================================================
    //
    //   RayEntry   Where a ray enters a box (slab test), null on a miss.
    //

    /// <summary>
    /// Slab test: per axis the ray enters and leaves the pair of box planes; the ray hits
    /// the box when the latest entry comes before the earliest exit.
    /// </summary>
    /// <param name="box">(Required) The box, in the space of the ray.</param>
    /// <param name="from">(Required) Ray origin.</param>
    /// <param name="direction">(Required) Ray direction.</param>
    /// <returns>Ray length to the entry point (0 inside the box), null on a miss.</returns>
    /// <example><code>RayPick.RayEntry(new Aabb(Vector3.Zero, Vector3.One), new Vector3(-1f, 0.5f, 0.5f), Vector3.Right)   // 1</code></example>
    public static float? RayEntry(Aabb box, Vector3 from, Vector3 direction)
    {
        float enter = 0f;
        float leave = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float origin = from[axis];
            float step   = direction[axis];
            float low    = box.Position[axis];
            float high   = box.End[axis];
            // Parallel to the slab: inside it or a miss
            if (Mathf.Abs(step) < 1e-6f)
            {
                if (origin < low || origin > high) return null;
                continue;
            }
            float t1 = (low - origin) / step;
            float t2 = (high - origin) / step;
            enter = Mathf.Max(enter, Mathf.Min(t1, t2));
            leave = Mathf.Min(leave, Mathf.Max(t1, t2));
        }
        return enter <= leave ? enter : null;
    }
}
