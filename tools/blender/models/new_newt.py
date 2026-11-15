# NOTE:
#   New parts of the newt template (fire newt / axolotl mix): the feathery outer gills
#   of an axolotl and the leaf-shaped newt tail with a fin seam along top and bottom.
#   Not registered in build.models.py yet, the specs are in the spec file of the newt.
#
#   ./tools/blender/Show-Model.ps1 -Module new_newt -Call 'build_feather_gills(scene)' -Name feathergills
#   ./tools/blender/Show-Model.ps1 -Module new_newt -Call 'build_newt_tail(scene)' -Name newttail

#
#   Chapters:
#     NEWT PARTS   Feathery outer gills (FlankPair) and the newt tail (Tail).
#

from models.fins import fin
from models.helpers import (
    AXIS_FORWARD,
    AXIS_OUT,
    AXIS_UP,
    FPS,
    G,
    animate,
    bind,
    rig,
    tube,
    wave,
)


# ===========================================================================
# ==============================   NEWT PARTS   =============================
# ===========================================================================
#
#   gill_branch           One gill stalk with its feathers, as stalk and feather mesh.
#   build_feather_gills   Three feathery gill stalks of the right flank, swaying.
#   build_newt_tail       Leaf-shaped tail: a fleshy core with a fin seam above and below.
#

GILL_FEATHERS = 6                    # feather pairs along one stalk
GILL_FEATHER_LENGTH = 0.2            # length of the first feathers, they shrink towards the tip
GILL_SWAY_DEGREES = 9.0

# Every stalk: (base, tip, bow) in Godot axes. The stalks fan out behind the head:
# up, straight out and down, all swept back (-X). The bow pushes the middle of the stalk out.
GILL_STALKS = (
    ((0.0, 0.2, 0.0), (-0.42, 0.78, 0.5), (0.1, 0.1, 0.12)),
    ((-0.04, 0.0, 0.0), (-0.66, 0.2, 0.78), (0.08, 0.0, 0.14)),
    ((0.0, -0.2, 0.0), (-0.5, -0.36, 0.62), (0.08, -0.08, 0.12)),
)


def gill_branch(scene, index, base, tip, bow):
    """
    One gill stalk: a tapering pink stalk from base to tip (Godot axes), bowed by bow in
    its middle, with a red feather pair at GILL_FEATHERS spots. The feathers lie in the
    plane across the stalk that faces back, so the fan reads from the side and from the top.
    Returns the stalk mesh and the feather mesh.
    """
    start, end, bend = G(*base), G(*tip), G(*bow)
    shares = [i / 4 for i in range(5)]
    spine = [start.lerp(end, t) + bend * (4.0 * t * (1.0 - t)) for t in shares]
    stalk = tube(scene, f'Stalk{index}', spine, (0.085, 0.075, 0.062, 0.05, 0.04), [(i, i + 1) for i in range(4)], 'Pad')

    # Feathers: one tree along the stalk (hidden inside it), a pair branching at every spot
    along = (end - start).normalized()
    side = along.cross(G(-1, 0, 0)).normalized()
    points = [start]
    radii = [0.03]
    edges = []
    previous = 0
    for feather in range(GILL_FEATHERS):
        share = (feather + 1.0) / (GILL_FEATHERS + 0.4)
        spot = start.lerp(end, share) + bend * (4.0 * share * (1.0 - share))
        points.append(spot)
        radii.append(0.03)
        edges.append((previous, len(points) - 1))
        previous = len(points) - 1
        length = GILL_FEATHER_LENGTH * (1.0 - 0.45 * share)
        for sign in (-1.0, 1.0):
            # Each feather leans back along the body and towards the stalk tip, ending in a fat drop
            reach = (side * sign + G(-0.55, 0, 0) + along * 0.35).normalized() * length
            points.extend([spot + reach * 0.55, spot + reach])
            radii.extend([0.026, 0.034])
            edges.extend([(previous, len(points) - 2), (len(points) - 2, len(points) - 1)])
    feathers = tube(scene, f'Feather{index}', points, radii, edges, 'Gill', subdivisions=1, max_pieces=3)
    return stalk, feathers


def build_feather_gills(scene):
    """
    The outer gills of an axolotl on the right flank (FlankPair): the origin on the skin,
    out of the body is +Z, about one unit tall. Three pink stalks fan out behind the head
    (up, out, down), each with a row of red feathers. They sway slowly ("Sway", 3 s).
    """
    meshes = []
    bones = []
    rigid = {}
    for index, (base, tip, bow) in enumerate(GILL_STALKS):
        stalk, feathers = gill_branch(scene, index, base, tip, bow)
        meshes.extend([stalk, feathers])
        bones.append((f'Gill{index}', G(*base), G(*tip), None))
        rigid[stalk.name] = f'Gill{index}'
        rigid[feathers.name] = f'Gill{index}'

    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes, rigid=rigid)

    # Sway: every stalk waves back and forth and nods a little, one after the other
    curves = {}
    for index in range(len(GILL_STALKS)):
        curves[f'Gill{index}'] = (AXIS_UP if 1 == index else AXIS_FORWARD,
                                  lambda share, phase=index * 0.22: GILL_SWAY_DEGREES * wave(share, phase))
    animate(armature, 'Sway', FPS * 3, curves)


def build_newt_tail(scene):
    """
    The tail of a newt (Tail): root at the origin on the tail tip, reaching back (-X),
    about 1.2 units long. A fleshy core tapering to a point, a fin seam standing above
    and below it, highest in the middle: the outline of a willow leaf. The seam starts
    half a unit in front of the origin, so it already runs along the end of the body.
    Upright like the tail fin of a fish, it flutters the same way ("Flutter", 1 s).
    """
    # Rays from the core: the upper seam front to back, the tip, the lower seam back to front
    upper = ((0.55, 0.2), (0.25, 0.3), (-0.1, 0.36), (-0.45, 0.36), (-0.75, 0.27), (-0.95, 0.14))
    lower = ((-0.95, -0.1), (-0.75, -0.2), (-0.45, -0.27), (-0.1, -0.27), (0.25, -0.23), (0.55, -0.16))
    sweep = 0.14
    roots = [G(x, 0.0, 0) for x, _ in upper] + [G(-1.02, 0.0, 0)] + [G(x, 0.0, 0) for x, _ in lower]
    tips = [G(x - sweep, y, 0) for x, y in upper] + [G(-1.26, 0.02, 0)] + [G(x - sweep, y, 0) for x, y in lower]

    # The core is the fleshy root of the fin: a long drop, as thick as the tail tip at the origin
    fin(scene, roots, tips, AXIS_OUT, G(-0.4, 0.0, 0), (0.62, 0.06, 0.1), 10.0)