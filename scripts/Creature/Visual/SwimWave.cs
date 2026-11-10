// NOTE:
//   The swim wave of an organism: how far the spine swings at an X, side to side for a
//   fish, up and down for a marine mammal. Pure math without engine calls, the same
//   formula as shaders/clay.gdshader, so the parts ride the wave of the body mesh.
//
//   float offset = SwimWave.Bend(part.X, design.HalfLength, amplitude, phase);

//
//   Chapters:
//     BEND THE SPINE   The offset of the spine at an X.
//

using Godot;

namespace CellOut;

public static class SwimWave
{
    // ===========================================================================
    // ===========================   BEND THE SPINE   ============================
    // ===========================================================================
    //
    //   Bend   Offset of the spine at an X, zero at the nose, growing to the tail.
    //

    /// <summary>
    /// Sideways (fish) or vertical (marine mammal) offset of the spine at an X. Zero at
    /// the nose, growing quadratically to the tail. The same formula as shaders/clay.gdshader.
    /// </summary>
    /// <param name="x">(Required) Spine X in unit space.</param>
    /// <param name="halfLength">(Required) CreatureDesign.HalfLength.</param>
    /// <param name="amplitude">(Required) Offset at the tail.</param>
    /// <param name="phase">(Required) Wave phase.</param>
    /// <returns>The offset in unit space.</returns>
    /// <example><code>SwimWave.Bend(0.9f, 0.9f, 0.1f, 0f)   // 0, the nose stays still</code></example>
    public static float Bend(float x, float halfLength, float amplitude, float phase)
    {
        float t = Mathf.Clamp((halfLength - x) / (2f * halfLength), 0f, 1f);
        return amplitude * t * t * Mathf.Sin(x * 4f - phase);
    }
}
