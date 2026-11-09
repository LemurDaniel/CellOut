# Blender model generator

Builds every model of Cell Out as `.glb` into `assets/models/` (never edit them by hand). Run `./Build-Models.ps1` (all) or `./Build-Models.ps1 -Only tree/` (some).

| File | Content |
| --- | --- |
| `Build-Models.ps1` | Entry point: finds Blender 5.2, runs the generator headless, lists the models. |
| `build.models.py` | Registers every model by its path (`PARTS`) and builds and exports them. |
| `models/helpers.py` | Godot axes, scene, named materials, tubes, cones, ellipsoids, digits, claws, rigs, weights, looping keys, export. |
| `models/limbs.py` | Feet, crab tip, hand and claw paw at the end of the sculpted limbs. |
| `models/organs.py` | Membrane and feather wings, brain, stalk eye. |
| `models/fins.py` | Tail fin, flukes, back fin and paddles with rays and a flutter rig. |
| `models/cells.py` | The ten cell editor parts as micro monsters. |
| `models/trees.py` | Primeval trees of the islands: tree fern, cycad, horsetail, broadleaf, scale tree giant. |

New family: a module in `models/` that imports from `models.helpers`, its builders registered in `PARTS`, new material names in `helpers.MATERIAL_COLORS` and in the game loader (`Core/PartModels.cs` or `Core/TreeModels.cs`).
