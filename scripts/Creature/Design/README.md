# Creature/Design

The parts of a body plan. `../CreatureDesign.cs` is the coordinator: it owns the placed parts, counts every change and keeps its public API; the classes here hold the detail logic and call no engine API besides `Mathf` and the value structs (exception: `BodyPlans.ForCritter` rolls with `Core/Rand.cs`).

| File | Content |
| --- | --- |
| `CreaturePart.cs` | Record of one placed part: kind, spine share, points paid, size, side, pose, limb joints, id and parent (branch). |
| `CreatureSpine.cs` | The spine: girth, height and position per vertebra (bending, pushing, adding and removing in the middle or at the ends) and the curves through them (profile radius, height, slope, shares, volume, inside test). |
| `PartPlacement.cs` | Placement rules: the side a part takes, the snapped spot of a click, the part closest to a share, branches of a limb, the share a part keeps when the spine grows at an end. |
| `LimbJoints.cs` | Joints of sculpted limbs: the default chain per kind, stretching a fresh leg to the ground, moving one joint with clamped segment length. |
| `CreatureLook.cs` | Look of a body plan: custom color, paint pattern (`CreaturePattern`) and spray paint dots (`PaintSplat`) with strokes and undo. |
| `BodyPlans.cs` | Ready-made plans: the clay blob, the starter bodies, the type examples and the NPC creatures per critter kind. |
