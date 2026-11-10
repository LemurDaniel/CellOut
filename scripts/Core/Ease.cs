// NOTE:
//   Easing curves for the cutscenes, every one maps 0..1 to 0..1 (BackOut overshoots).
//
//   float flight = Ease.InOut(Mathf.Clamp(time / DURATION, 0f, 1f));

//
//   Chapters:
//     CURVES   Smooth, InOut, BackOut, Pulse and Window.
//

using Godot;

namespace CellOut;

public static class Ease
{
    // ===========================================================================
    // ===============================   CURVES   ================================
    // ===========================================================================
    //
    //   Smooth    Smoothstep.
    //   InOut     Cubic ease in and out.
    //   BackOut   Overshoots to about 1.1 and settles at 1.
    //   Pulse     A beat: up fast, down slower, 0 outside its span.
    //   Window    Progress of a time span, clamped to 0..1.
    //

    /// <summary>Smoothstep 0..1.</summary>
    public static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    /// <summary>Cubic ease in and out.</summary>
    public static float InOut(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    /// <summary>Overshoots to about 1.1 and settles at 1.</summary>
    public static float BackOut(float t)
    {
        const float OVERSHOOT = 1.70158f;
        float u = t - 1f;
        return 1f + (OVERSHOOT + 1f) * u * u * u + OVERSHOOT * u * u;
    }

    /// <summary>A heartbeat-like bump: rises in the first fifth of its span, falls over the rest.</summary>
    /// <param name="time">(Required) Current time.</param>
    /// <param name="start">(Required) When the beat starts.</param>
    /// <param name="length">(Required) How long the beat lasts.</param>
    /// <returns>0 outside the span, 1 at the peak.</returns>
    /// <example><code>float beat = Ease.Pulse(time, 4.4f, 0.5f);</code></example>
    public static float Pulse(float time, float start, float length)
    {
        float t = (time - start) / length;
        if (t <= 0f || t >= 1f)
        {
            return 0f;
        }
        return t < 0.2f ? Smooth(t / 0.2f) : 1f - Smooth((t - 0.2f) / 0.8f);
    }

    /// <summary>Progress of the span start..end at time, clamped to 0..1.</summary>
    /// <example><code>float grow = Ease.Window(time, 1.8f, 2.8f);</code></example>
    public static float Window(float time, float start, float end)
    {
        return Mathf.Clamp((time - start) / (end - start), 0f, 1f);
    }
}
