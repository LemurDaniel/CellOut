# Creature/Visual

The pieces of the 3D organism. `../CreatureVisual3D.cs` is the coordinator: it follows the body, owns the clay materials, rebuilds on change and keeps its public API; the classes here build, animate and pick.

| File | Content |
| --- | --- |
| `CreatureParts3D.cs` | Node component: every placed part on pivots along the spine (`BuildPart`, `BASE_PART_SIZES`), swim wave on the pivots, eye blink, blowhole spout, picking answers. One instance per rebuild. |
| `CreatureLimbs3D.cs` | Sculpted limbs: clay tube per flank through the joints, knuckles, foot or hand on the tip, root on the body or on the posed joint of a parent (branch), limb frames for the editor. |
| `CreaturePartPoser.cs` | Poses the built pieces of a part (side, size, shift, turn, mirror for the left flank) and remembers meshes and anchors for picking by ray. |
| `CreatureClay.cs` | Clay geometry: the body mesh lofted along the spine, the limb tube mesh, the fillet where a part grows out of the skin. |
| `CreatureModels.cs` | Blender part models: `MODELS` (file and animation per part kind), recolored instances, mirrored attachment, fin membrane material. |
| `SwimWave.cs` | Pure math: offset of the spine at an X for the swim wave, the same formula as `shaders/clay.gdshader`. |
| `RayPick.cs` | Pure math: where a ray enters a bounding box (slab test). |
