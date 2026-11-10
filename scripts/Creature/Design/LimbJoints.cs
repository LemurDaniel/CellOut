// NOTE:
//   The joints of sculpted limbs: the chain every limb kind starts with, stretching a
//   fresh leg to the ground and moving one joint with the segment length clamped.
//   Joints are points in the space of the right limb (shoulder at the origin, +Z away
//   from the body). Pure functions without engine calls, a joint list is never changed
//   in place: every function returns a new one.
//
//   IReadOnlyList<Vector3> joints = LimbJoints.DefaultFor(CreaturePartKind.Tentacle);   // 4 joints
//   Vector3[] moved = LimbJoints.Move(joints, 3, new Vector3(0.3f, 0.2f, 0.4f));

//
//   Chapters:
//     SHAPE LIMB JOINTS   Default chains per kind, the stretch to the ground and moving a joint.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public static class LimbJoints
{
    // ===========================================================================
    // ==========================   SHAPE LIMB JOINTS   ==========================
    // ===========================================================================
    //
    //   DefaultFor        The joints a sculpted limb starts with.
    //   StretchToGround   Scales a fresh leg so its tip reaches the ground.
    //   Move              Moves one joint, the joints further out follow.
    //

    /*
      Sculpted limbs: a chain of joints pulled out of the flank, one kind per joint count.
      Each segment stays MIN..MAX_LIMB_SEGMENT long.

        shoulder (flank) --o-- knee --o-- ankle --o tip
      - StubLimb (1):  a short nub sticking out sideways, pulled down a straight pole leg
      - BentLimb (2):  one knee, hangs to the ground like a leg
      - Limb (3):      knee and ankle, hangs to the ground like a leg
      - Tentacle (4):  curls out and down, the tip bends back up
      - Feet:          a leg of three joints, the foot model on the tip touches the ground
                       of the coast with the body lifted (LAND_LIFT in CreatureVisual3D.cs)
      - Hands:         an arm of three joints reaching forward, held above the ground
      A fresh leg-like limb already carries on land; pulled up and forward it becomes an arm.
    */
    // Before DEFAULT_JOINTS: static fields initialize in order, it reads them
    private static readonly Vector3[] LEG_JOINTS = { new(0.04f, -0.12f, 0.26f), new(0.1f, -0.36f, 0.34f), new(0.12f, -0.6f, 0.34f) };
    private static readonly Vector3[] ARM_JOINTS = { new(0.1f, -0.08f, 0.26f), new(0.26f, -0.18f, 0.34f), new(0.42f, -0.16f, 0.34f) };
    private static readonly IReadOnlyDictionary<CreaturePartKind, Vector3[]> DEFAULT_JOINTS = new Dictionary<CreaturePartKind, Vector3[]>
    {
        [CreaturePartKind.StubLimb] = new Vector3[] { new(0.08f, -0.18f, 0.34f) },
        [CreaturePartKind.BentLimb] = new Vector3[] { new(0.06f, -0.34f, 0.32f), new(0.16f, -0.82f, 0.38f) },
        [CreaturePartKind.Limb]     = new Vector3[] { new(0.04f, -0.12f, 0.28f), new(0.12f, -0.46f, 0.42f), new(0.2f, -0.82f, 0.42f) },
        [CreaturePartKind.Tentacle] = new Vector3[] { new(0.06f, -0.16f, 0.3f), new(0.2f, -0.42f, 0.48f), new(0.36f, -0.66f, 0.46f), new(0.52f, -0.6f, 0.3f) },
        [CreaturePartKind.Legs]       = LEG_JOINTS,
        [CreaturePartKind.StubbyLegs] = LEG_JOINTS,
        [CreaturePartKind.ClawLegs]   = LEG_JOINTS,
        [CreaturePartKind.CrabLegs]   = LEG_JOINTS,
        [CreaturePartKind.Hands]      = ARM_JOINTS,
        [CreaturePartKind.Claws]      = ARM_JOINTS,
    };

    public const float MIN_LIMB_SEGMENT = 0.08f;
    public const float MAX_LIMB_SEGMENT = 0.75f;
    public const float WALK_REACH = 0.1f;           // a limb tip this close above the ground counts as a foot

    /// <summary>The joints a sculpted limb starts with, null for every other part.</summary>
    /// <param name="kind">(Required) The part kind.</param>
    /// <example><code>LimbJoints.DefaultFor(CreaturePartKind.Tentacle).Count   // 4</code></example>
    public static IReadOnlyList<Vector3> DefaultFor(CreaturePartKind kind)
    {
        return DEFAULT_JOINTS.TryGetValue(kind, out Vector3[] joints) ? joints : null;
    }

    /*
      A fresh leg reaches the ground wherever it grows: on a lifted part of the body (a
      neck, a hump) its joints stretch down, under a low belly they shrink. Y of every
      joint scales by the same factor, so the shape stays; the tip lands LEG_TIP_Y below
      the body center (the foot sole touches the coast with the body lifted on land).
    */
    private const float LEG_TIP_Y = -0.7f;
    private const float MIN_LEG_STRETCH = 0.5f;
    private const float MAX_LEG_STRETCH = 2.5f;

    /// <summary>Scales the joints of a fresh leg in Y so its tip reaches the ground.</summary>
    /// <param name="joints">(Required) Joints of the leg, the last one is the tip.</param>
    /// <param name="rootY">(Required) Height of the shoulder in unit space (CreatureDesign.LimbRoot).</param>
    /// <returns>The stretched joints, a new list.</returns>
    /// <example><code>LimbJoints.StretchToGround(joints, rootY: 0.4f)   // a leg under a raised neck gets longer</code></example>
    public static IReadOnlyList<Vector3> StretchToGround(IReadOnlyList<Vector3> joints, float rootY)
    {
        float   tipY    = joints[^1].Y;
        float   stretch = tipY < -0.01f ? Mathf.Clamp((LEG_TIP_Y - rootY) / tipY, MIN_LEG_STRETCH, MAX_LEG_STRETCH) : 1f;
        return joints.Select(joint => new Vector3(joint.X, joint.Y * stretch, joint.Z)).ToArray();
    }

    /// <summary>Moves one joint of a sculpted limb, the joints further out follow by the same shift.</summary>
    /// <param name="joints">(Required) Joints from the shoulder outwards.</param>
    /// <param name="joint">(Required) Joint to move, 0..joints.Count - 1.</param>
    /// <param name="point">(Required) New position in the space of the right limb, the segment length is clamped.</param>
    /// <returns>The moved joints, a new array.</returns>
    /// <example><code>LimbJoints.Move(joints, 2, new Vector3(0.3f, 0.2f, 0.4f));   // the tip reaches forward like an arm</code></example>
    public static Vector3[] Move(IReadOnlyList<Vector3> joints, int joint, Vector3 point)
    {
        Vector3[] moved = joints.ToArray();
        if (joint < 0 || joint >= moved.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(joint), $"joint must be between 0 and {moved.Length - 1}, got '{joint}'.");
        }
        Vector3 previous = 0 == joint ? Vector3.Zero : moved[joint - 1];
        Vector3 segment  = point - previous;
        float   length   = Mathf.Clamp(segment.Length(), MIN_LIMB_SEGMENT, MAX_LIMB_SEGMENT);
        Vector3 target   = previous + (segment.LengthSquared() > 1e-8f ? segment.Normalized() : Vector3.Back) * length;
        Vector3 shift    = target - moved[joint];
        for (int i = joint; i < moved.Length; i++)
        {
            moved[i] += shift;
        }
        return moved;
    }
}
