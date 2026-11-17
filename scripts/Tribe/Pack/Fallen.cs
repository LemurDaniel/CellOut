// NOTE:
//   The body of a land animal that just died: it tips onto its side, sinks together and is
//   gone, instead of vanishing in the frame of the deadly bite. Pure look: it belongs to
//   no pack and no list, nothing can bite or court it, the meat it leaves is dropped by
//   whoever resolved the kill. Kin.Hurt puts it where the animal stood, it frees itself.
//
//   AddSibling(Fallen.Of(kin));     // in Kin.Hurt, right before the kin is freed

//
//   Chapters:
//     FALL   The body, the tip onto its side and the sinking.
//

using Godot;

namespace CellOut;

public partial class Fallen : CellBody
{
    // ===========================================================================
    // =================================   FALL   ================================
    // ===========================================================================
    //
    //   Of         The fallen body of a kin: its body plan, color, size, place and facing.
    //   _Process   Tips over, then sinks together, then frees itself.
    //

    private const float TIP_SECONDS = 0.45f;         // it lies on its side this fast
    private const float REST_SECONDS = 0.5f;         // then it lies still for a moment
    private const float SINK_SECONDS = 0.7f;         // then it sinks together and is gone
    private const float LYING = 1.4f;                // radians of the roll onto its side
    private const float GONE_SIZE = 0.05f;           // share of its size at which it is freed

    private float _time;
    private float _size;
    private float _side = 1f;

    /// <summary>The fallen body of a kin that just died, to be added next to it.</summary>
    /// <param name="kin">(Required) The dying kin, still in the tree.</param>
    /// <example><code>AddSibling(Fallen.Of(this));</code></example>
    public static Fallen Of(Kin kin)
    {
        var design = new CreatureDesign();
        design.CopyFrom(kin.Creature);
        return new Fallen
        {
            Creature   = design,
            BodyColor  = kin.BodyColor,
            Radius     = kin.Radius,
            Position   = kin.Position,
            Facing     = kin.Facing,
            GroundLift = kin.GroundLift,
            IsOnLand   = true,
            HurtFlash  = 1f,
            _size      = kin.Radius,
            _side      = Rand.Chance(0.5f) ? 1f : -1f,
        };
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        _time += (float)delta;
        Roll   = _side * LYING * Ease.Smooth(Mathf.Clamp(_time / TIP_SECONDS, 0f, 1f));
        float sinking = Mathf.Clamp((_time - TIP_SECONDS - REST_SECONDS) / SINK_SECONDS, 0f, 1f);
        Radius = _size * Mathf.Lerp(1f, GONE_SIZE, sinking * sinking);
        if (sinking >= 1f)
        {
            QueueFree();
        }
    }
}
