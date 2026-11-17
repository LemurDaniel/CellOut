# NOTE:
#   Builds the models of Cell Out in Blender and exports one .glb per model:
#   - assets/models/creature/  feet, hands, pincer, squid club, wings, fins, mouths, spines, shell, gills, brain, stalk eyes, lantern of the organism stage
#   - assets/models/cell/      the sixteen cell editor parts (flagellum, cilium, jaw, electrocyte, ...)
#   - assets/models/tree/      primeval trees of the islands (fern, cycad, horsetail, broadleaf, giant)
#   - assets/models/plant/     undergrowth of the meadows (shrub, berry bush, ground fern, daisy, bell, lupine, boulder)
#   - assets/models/village/   the nest of the pack and the fire pit of the tribe
#   - assets/models/building/  the eighteen blocks of the building designer (walls, roofs, frames, ...)
#   Everything is generated: skin modifier tubes, displaced spheres, an armature per
#   moving part with one looping animation (Walk, Grab, Flap, Flutter, Sway, Whip, Beat, Chomp).
#   Materials only carry names (Skin, Claw, Pad, Feather, Membrane, Fin, FinRay, Brain, EyeWhite,
#   Pupil, Glint, Chitin, Armor, Chloro, Grana, Vacuole, Droplet, Toxin, Venom, Iris, Dark, Beak, Shell, ShellRim, Gill, Lantern, Bark,
#   Leaf, LeafDark, Frond, Horsetail, Needle, Cone, Twig, Moss, Earth, Down, Stone, Log, Ember,
#   Paint, PaintDark, Wood, Bone, Door, Flame, Rope, FruitRed, FruitGold, FruitGreen, Petal, Pollen, Stem, Berry), the
#   game recolors them (Core/PartModels.cs, Core/TreeModels.cs).
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
import models.mouths
import models.defense
import models.cells
import models.new_abilities
import models.new_angler
import models.new_bird
import models.new_crab
import models.new_dino
import models.new_frog
import models.new_jelly
import models.new_mammal
import models.new_heads_mammal
import models.new_heads_bird
import models.new_heads_reptile
import models.new_heads_arthropod
import models.new_newt
import models.new_puffer
import models.new_ray
import models.new_snail
import models.new_snake
import models.new_spider
import models.new_star
import models.new_seahorse
import models.new_shark
import models.new_squid
import models.new_turtle
import models.new_whale
import models.trees
import models.plants
import models.village
import models.buildings
import models.antique
import models.ships
import models.city

# Inside a running Blender the modules stay loaded between runs: reload, helpers first
for module in (models.helpers, models.limbs, models.organs, models.fins, models.mouths, models.defense, models.cells, models.new_abilities, models.trees, models.plants, models.village, models.buildings, models.antique, models.ships, models.city):
    importlib.reload(module)

from models.buildings import (
    build_awning,
    build_banner,
    build_cone_roof,
    build_crate,
    build_door,
    build_gable_roof,
    build_horns,
    build_ladder,
    build_palisade,
    build_platform,
    build_round_room,
    build_round_tower,
    build_small_cone_roof,
    build_square_room,
    build_stall,
    build_stilts,
    build_torch,
    build_totem,
)
from models.cells import (
    build_armor,
    build_cell_jaw,
    build_chromatophore,
    build_cilium,
    build_electrocyte,
    build_eyespot,
    build_filter,
    build_flagellum,
    build_jet_nozzle,
    build_light_sail,
    build_nematocyst,
    build_pseudopod,
    build_slime_gland,
    build_spike,
    build_sucker,
    build_toxin_gland,
)
from models.fins import build_caudal, build_dorsal, build_fluke, build_pectoral
from models.helpers import build_scene, export, join_by_material
from models.defense import build_shell, build_spines
from models.limbs import build_foot, build_hand, build_pincer, build_sucker_club
from models.mouths import build_beak, build_filtermouth, build_jaw
from models.organs import build_gills, build_lure, build_stalk_eye, build_wing
from models.trees import (
    build_tree_broadleaf,
    build_tree_cycad,
    build_tree_fern,
    build_tree_giant,
    build_tree_horsetail,
)
from models.plants import (
    build_plant_bell,
    build_plant_berrybush,
    build_plant_boulder,
    build_plant_daisy,
    build_plant_fernbush,
    build_plant_lupine,
    build_plant_shrub,
)
from models.village import build_fire, build_nest


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
    'creature/creature.stalkeye.glb':      build_stalk_eye,
    'creature/creature.caudalfin.glb':     build_caudal,
    'creature/creature.fluke.glb':         build_fluke,
    'creature/creature.dorsalfin.glb':     build_dorsal,
    'creature/creature.pectoralfin.glb':   build_pectoral,
    'creature/creature.jaw.glb':           build_jaw,
    'creature/creature.filtermouth.glb':   build_filtermouth,
    'creature/creature.beak.glb':          build_beak,
    'creature/creature.spines.glb':        build_spines,
    'creature/creature.shell.glb':         build_shell,
    'creature/creature.gills.glb':         build_gills,
    'creature/creature.lure.glb':          build_lure,
    'creature/creature.pincer.glb':        build_pincer,
    'creature/creature.suckerclub.glb':    build_sucker_club,
    'creature/creature.sharktail.glb':     models.new_shark.build_sharktail,
    'creature/creature.sharkfin.glb':      models.new_shark.build_sharkfin,
    'creature/creature.newttail.glb':      models.new_newt.build_newt_tail,
    'creature/creature.feathergills.glb':  models.new_newt.build_feather_gills,
    'creature/creature.mantlefins.glb':    models.new_squid.build_mantle_fins,
    'creature/creature.siphon.glb':        models.new_squid.build_siphon,
    'creature/creature.fantail.glb':       models.new_puffer.build_fantail,
    'creature/creature.puffermouth.glb':   models.new_puffer.build_puffermouth,
    'creature/creature.curltail.glb':      models.new_seahorse.build_curltail,
    'creature/creature.tubesnout.glb':     models.new_seahorse.build_tubesnout,
    'creature/creature.tallfin.glb':       models.new_whale.build_tallfin,
    'creature/creature.flipper.glb':       models.new_whale.build_flipper,
    'creature/creature.paddle.glb':        models.new_turtle.build_flipper,
    'creature/creature.needlemaw.glb':     models.new_angler.build_needlemaw,
    'creature/creature.carapace.glb':      models.new_crab.build_carapace,
    'creature/creature.elephantfoot.glb':  models.new_mammal.build_elephantfoot,
    'creature/creature.mane.glb':          models.new_mammal.build_mane,
    'creature/creature.elephanthead.glb':  models.new_mammal.build_elephanthead,
    'creature/creature.horsehead.glb':        models.new_heads_mammal.build_horsehead,
    'creature/creature.cathead.glb':          models.new_heads_mammal.build_cathead,
    'creature/creature.rhinohead.glb':        models.new_heads_mammal.build_rhinohead,
    'creature/creature.parrothead.glb':       models.new_heads_bird.build_parrothead,
    'creature/creature.penguinhead.glb':      models.new_heads_bird.build_penguinhead,
    'creature/creature.raptorhead.glb':       models.new_heads_bird.build_raptorhead,
    'creature/creature.crochead.glb':         models.new_heads_reptile.build_crochead,
    'creature/creature.rexhead.glb':          models.new_heads_reptile.build_rexhead,
    'creature/creature.ceratopshead.glb':     models.new_heads_reptile.build_ceratopshead,
    'creature/creature.insecthead.glb':       models.new_heads_arthropod.build_insecthead,
    'creature/creature.spiderhead.glb':       models.new_heads_arthropod.build_spiderhead,
    'creature/creature.crabhead.glb':         models.new_heads_arthropod.build_crabhead,
    'creature/creature.parrotbeak.glb':    models.new_bird.build_parrotbeak,
    'creature/creature.tailfeathers.glb':  models.new_bird.build_tailfeathers,
    'creature/creature.crestfeathers.glb': models.new_bird.build_crestfeathers,
    'creature/creature.penguinflipper.glb':models.new_bird.build_penguinflipper,
    'creature/creature.birdfoot.glb':      models.new_bird.build_birdfoot,
    'creature/creature.tusks.glb':         models.new_mammal.build_tusks,
    'creature/creature.ossicones.glb':     models.new_mammal.build_ossicones,
    'creature/creature.bigears.glb':       models.new_mammal.build_bigears,
    'creature/creature.tufttail.glb':      models.new_mammal.build_tufttail,
    'creature/creature.hoof.glb':          models.new_mammal.build_hoof,
    'creature/creature.spinecrest.glb':    models.defense.build_spinecrest,
    'creature/creature.stararm.glb':       models.new_star.build_stararm,
    'creature/creature.mantawing.glb':     models.new_ray.build_mantawing,
    'creature/creature.whiptail.glb':      models.new_ray.build_whiptail,
    'creature/creature.tailfan.glb':       models.new_ray.build_tailfan,
    'creature/creature.rattle.glb':        models.new_snake.build_rattle,
    'creature/creature.thicktail.glb':     models.new_dino.build_thicktail,
    'creature/creature.fangs.glb':         models.new_spider.build_fangs,
    'creature/creature.forktongue.glb':    models.new_snake.build_forktongue,
    'creature/creature.rexmaw.glb':        models.new_dino.build_rexmaw,
    'creature/creature.urchinspines.glb':  models.new_star.build_urchinspines,
    'creature/creature.stinger.glb':       models.new_spider.build_stinger,
    'creature/creature.casque.glb':        models.new_snake.build_casque,
    'creature/creature.horns.glb':         models.new_dino.build_horns,
    'creature/creature.neckfrill.glb':     models.new_dino.build_neckfrill,
    'creature/creature.spidereyes.glb':    models.new_spider.build_spidereyes,
    'creature/creature.croctail.glb':      models.new_frog.build_croctail,
    'creature/creature.crocfoot.glb':      models.new_frog.build_crocfoot,
    'creature/creature.croceye.glb':       models.new_frog.build_croceye,
    'creature/creature.spiralshell.glb':   models.new_snail.build_spiralshell,
    'creature/creature.nautilusshell.glb': models.new_snail.build_nautilusshell,
    'creature/creature.backplates.glb':    models.new_frog.build_backplates,
    'creature/creature.webbedfoot.glb':    models.new_frog.build_webbedfoot,
    'creature/creature.crocmaw.glb':       models.new_frog.build_crocmaw,
    'creature/creature.jellybell.glb':     models.new_jelly.build_jellybell,
    'creature/creature.oralarms.glb':      models.new_jelly.build_oralarms,
    'creature/creature.sicklefin.glb':     models.new_shark.build_sicklefin,
    'creature/creature.roundflipper.glb':  models.new_whale.build_roundflipper,
    'creature/creature.hindpaddle.glb':    models.new_turtle.build_hindpaddle,
    'creature/creature.finribbon.glb':     models.new_seahorse.build_finribbon,
    'creature/creature.sharkmaw.glb':      models.new_shark.build_sharkmaw,
    'creature/creature.orcamaw.glb':       models.new_whale.build_orcamaw,
    'creature/creature.gapemaw.glb':       models.new_seahorse.build_gapemaw,
    'creature/creature.squidbeak.glb':     models.new_squid.build_squidbeak,
    'creature/creature.crabmouth.glb':     models.new_crab.build_crabmouth,
    'creature/creature.smile.glb':         models.new_newt.build_smile,
    'creature/creature.beadeye.glb':       models.new_shark.build_beadeye,
    'creature/creature.ghosteye.glb':      models.new_angler.build_ghosteye,
    'creature/creature.bulgeeye.glb':      models.new_puffer.build_bulgeeye,
    'creature/creature.patcheye.glb':      models.new_whale.build_patcheye,
    'creature/creature.squideye.glb':      models.new_squid.build_squideye,
    'creature/creature.pegeye.glb':        models.new_crab.build_pegeye,
    'creature/creature.lideye.glb':        models.new_turtle.build_lideye,
    'creature/creature.turreteye.glb':     models.new_seahorse.build_turreteye,
    'creature/creature.frogeye.glb':       models.new_newt.build_frogeye,
    'creature/creature.scentgland.glb':    models.new_abilities.build_scent_gland,
    'creature/creature.healingmoss.glb':   models.new_abilities.build_healing_moss,
    'creature/creature.glowcrest.glb':     models.new_abilities.build_glow_crest,
    'creature/creature.inksac.glb':        models.new_abilities.build_ink_sac,
    'creature/creature.roarsac.glb':       models.new_abilities.build_roar_sac,
    'creature/creature.rambrow.glb':       models.new_abilities.build_ram_brow,
    'cell/cell.flagellum.glb':             build_flagellum,
    'cell/cell.cilium.glb':                build_cilium,
    'cell/cell.jaw.glb':                   build_cell_jaw,
    'cell/cell.spike.glb':                 build_spike,
    'cell/cell.armorplate.glb':            build_armor,
    'cell/cell.filtermouth.glb':           build_filter,
    'cell/cell.eyespot.glb':               build_eyespot,
    'cell/cell.pseudopod.glb':             build_pseudopod,
    'cell/cell.nematocyst.glb':            build_nematocyst,
    'cell/cell.sucker.glb':                build_sucker,
    'cell/cell.lightsail.glb':             build_light_sail,
    'cell/cell.jetnozzle.glb':             build_jet_nozzle,
    'cell/cell.slimegland.glb':            build_slime_gland,
    'cell/cell.chromatophore.glb':         build_chromatophore,
    'cell/cell.toxingland.glb':            build_toxin_gland,
    'cell/cell.electrocyte.glb':           build_electrocyte,
    'tree/tree.fern.glb':                  build_tree_fern,
    'tree/tree.cycad.glb':                 build_tree_cycad,
    'tree/tree.horsetail.glb':             build_tree_horsetail,
    'tree/tree.broadleaf.glb':             build_tree_broadleaf,
    'tree/tree.giant.glb':                 build_tree_giant,
    'plant/plant.shrub.glb':               build_plant_shrub,
    'plant/plant.berrybush.glb':           build_plant_berrybush,
    'plant/plant.fernbush.glb':            build_plant_fernbush,
    'plant/plant.daisy.glb':               build_plant_daisy,
    'plant/plant.bell.glb':                build_plant_bell,
    'plant/plant.lupine.glb':              build_plant_lupine,
    'plant/plant.boulder.glb':             build_plant_boulder,
    'village/village.nest.glb':            build_nest,
    'village/village.fire.glb':            build_fire,
    'building/building.roundroom.glb':     build_round_room,
    'building/building.squareroom.glb':    build_square_room,
    'building/building.roundtower.glb':    build_round_tower,
    'building/building.coneroof.glb':      build_cone_roof,
    'building/building.gableroof.glb':     build_gable_roof,
    'building/building.smallconeroof.glb': build_small_cone_roof,
    'building/building.stilts.glb':        build_stilts,
    'building/building.platform.glb':      build_platform,
    'building/building.ladder.glb':        build_ladder,
    'building/building.palisade.glb':      build_palisade,
    'building/building.torch.glb':         build_torch,
    'building/building.banner.glb':        build_banner,
    'building/building.stall.glb':         build_stall,
    'building/building.awning.glb':        build_awning,
    'building/building.crate.glb':         build_crate,
    'building/building.door.glb':          build_door,
    'building/building.totem.glb':         build_totem,
    'building/building.horns.glb':         build_horns,
    'building/building.column.glb':       models.antique.build_column,
    'building/building.steps.glb':        models.antique.build_steps,
    'building/building.pediment.glb':     models.antique.build_pediment,
    'civ/civ.ship.glb':                  models.ships.build_ship,
    'civ/civ.field.glb':                 models.city.build_field,
    'civ/civ.pier.glb':                  models.city.build_pier,
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
