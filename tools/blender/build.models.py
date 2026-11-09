# NOTE:
#   Builds the models of Cell Out in Blender and exports one .glb per model:
#   - assets/models/creature/  feet, hands, wings, fins, brain, stalk eyes of the organism stage
#   - assets/models/cell/      the ten cell editor parts (flagellum, cilium, jaw, spike, ...)
#   - assets/models/tree/      primeval trees of the islands (fern, cycad, horsetail, broadleaf, giant)
#   Everything is generated: skin modifier tubes, displaced spheres, an armature per
#   moving part with one looping animation (Walk, Grab, Flap, Flutter, Sway, Whip, Beat, Chomp).
#   Materials only carry names (Skin, Claw, Pad, Feather, Membrane, Fin, FinRay, Brain, EyeWhite,
#   Pupil, Glint, Chitin, Armor, Chloro, Grana, Vacuole, Droplet, Toxin, Venom, Iris, Dark, Bark,
#   Leaf, LeafDark, Frond, Horsetail, Needle, Cone), the game recolors them (Core/PartModels.cs,
#   Core/TreeModels.cs).
#   The builders live in models/ (one module per family, see README.md), this file
#   registers them and exports. Requires: Blender 5.2. Usually run through Build-Models.ps1.
#
#   blender -b --factory-startup -P tools/blender/build.models.py -- assets/models
#   blender -b --factory-startup -P tools/blender/build.models.py -- assets/models tree/    # only paths starting with tree/
#
#   Inside a running Blender (MCP, text editor) it builds into its own scene
#   "CellOut Parts" and leaves every other scene alone.

#
#   Chapters:
#     BUILD ALL   Loads the part modules, registers every model, builds and exports them.
#

import importlib
import os
import sys

# The part modules sit next to this file, Blender does not add its folder to the path
GENERATOR_DIR = os.path.dirname(os.path.abspath(__file__))
if GENERATOR_DIR not in sys.path:
    sys.path.insert(0, GENERATOR_DIR)

import models.helpers
import models.limbs
import models.organs
import models.fins
import models.cells
import models.trees

# Inside a running Blender the modules stay loaded between runs: reload, helpers first
for module in (models.helpers, models.limbs, models.organs, models.fins, models.cells, models.trees):
    importlib.reload(module)

from models.cells import (
    build_armor,
    build_cell_jaw,
    build_chloroplast,
    build_cilium,
    build_eyespot,
    build_filter,
    build_flagellum,
    build_spike,
    build_toxin,
    build_vacuole,
)
from models.fins import build_caudal, build_dorsal, build_fluke, build_pectoral
from models.helpers import build_scene, export, join_by_material
from models.limbs import build_foot, build_hand
from models.organs import build_brain, build_stalk_eye, build_wing
from models.trees import (
    build_tree_broadleaf,
    build_tree_cycad,
    build_tree_fern,
    build_tree_giant,
    build_tree_horsetail,
)


# ===========================================================================
# ==============================   BUILD ALL   ==============================
# ===========================================================================
#
#   1. Arguments   Resolves the models folder and the optional path filters after "--".
#   2. Parts       Every model by its path below the models folder.
#   3. Build       Builds every (filtered) model in a cleared scene and exports it.
#

# ---------------------------------------------------------------------------
# Arguments
#   Resolves the models folder and the optional path filters after "--".
# ---------------------------------------------------------------------------

def arguments():
    """'-- assets/models tree/ cell/' -> ('<abs>/assets/models', ['tree/', 'cell/'])"""
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if not args:
        raise ValueError('The models folder must follow "--", e.g. blender -b -P build.models.py -- assets/models')
    os.makedirs(args[0], exist_ok=True)
    return os.path.abspath(args[0]), args[1:]


# ---------------------------------------------------------------------------
# Parts
#   Every model by its path below the models folder.
# ---------------------------------------------------------------------------

PARTS = {
    'creature/creature.paw.glb':           lambda scene: build_foot(scene, 'paw'),
    'creature/creature.columnfoot.glb':    lambda scene: build_foot(scene, 'columnfoot'),
    'creature/creature.talonfoot.glb':     lambda scene: build_foot(scene, 'talonfoot'),
    'creature/creature.crabtip.glb':       lambda scene: build_foot(scene, 'crabtip'),
    'creature/creature.hand.glb':          lambda scene: build_hand(scene, 'hand'),
    'creature/creature.clawpaw.glb':       lambda scene: build_hand(scene, 'clawpaw'),
    'creature/creature.membranewings.glb': lambda scene: build_wing(scene, 'membranewings'),
    'creature/creature.featherwings.glb':  lambda scene: build_wing(scene, 'featherwings'),
    'creature/creature.brain.glb':         build_brain,
    'creature/creature.stalkeye.glb':      build_stalk_eye,
    'creature/creature.caudalfin.glb':     build_caudal,
    'creature/creature.fluke.glb':         build_fluke,
    'creature/creature.dorsalfin.glb':     build_dorsal,
    'creature/creature.pectoralfin.glb':   build_pectoral,
    'cell/cell.flagellum.glb':             build_flagellum,
    'cell/cell.cilium.glb':                build_cilium,
    'cell/cell.jaw.glb':                   build_cell_jaw,
    'cell/cell.spike.glb':                 build_spike,
    'cell/cell.armorplate.glb':            build_armor,
    'cell/cell.filtermouth.glb':           build_filter,
    'cell/cell.eyespot.glb':               build_eyespot,
    'cell/cell.chloroplast.glb':           build_chloroplast,
    'cell/cell.vacuole.glb':               build_vacuole,
    'cell/cell.toxin.glb':                 build_toxin,
    'tree/tree.fern.glb':                  build_tree_fern,
    'tree/tree.cycad.glb':                 build_tree_cycad,
    'tree/tree.horsetail.glb':             build_tree_horsetail,
    'tree/tree.broadleaf.glb':             build_tree_broadleaf,
    'tree/tree.giant.glb':                 build_tree_giant,
}


# ---------------------------------------------------------------------------
# Build
#   Builds every (filtered) model in a cleared scene and exports it.
# ---------------------------------------------------------------------------

def build_all(out_dir, filters=None):
    """Builds every model whose path starts with one of the filters, all without filters."""
    for file_name, build in PARTS.items():
        if filters and not any(file_name.startswith(prefix) for prefix in filters):
            continue
        scene = build_scene()
        build(scene)
        # Static parts (no rig) join per material too, rigged ones did in bind
        if not any('ARMATURE' == obj.type for obj in scene.objects):
            join_by_material([obj for obj in scene.objects if 'MESH' == obj.type])
        export(out_dir, file_name)


if __name__ == '__main__':
    build_all(*arguments())
