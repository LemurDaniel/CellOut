# NOTE:
#   New parts for the shark of the template library: a solid triangular back fin and a
#   crescent tail with a bigger upper lobe. Both are fleshy blades without rays, unlike
#   the ray fins of fins.py. Not registered in build.models.py yet (the coordinator wires them).
#   Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   ./tools/blender/Show-Model.ps1 -Module new_shark -Call 'build_sharkfin(scene)' -Name sharkfin
#   ./tools/blender/Show-Model.ps1 -Module new_shark -Call 'build_sharktail(scene)' -Name sharktail

#
#   Chapters:
#     SHARK PARTS   The blade both fins are cut from, the back fin and the tail.
#

import math

import bmesh
import bpy

from models.helpers import (
    AXIS_FORWARD,
    AXIS_OUT,
    AXIS_UP,
    FPS,
    G,
    animate,
    apply_modifiers,
    bind,
    ellipsoid,
    link_mesh,
    rig,
    wave,
)

BLADE_ACROSS = 8                     # columns from the leading to the trailing edge
BLADE_ROUNDNESS = 0.55               # below 1 the blade is plump with blunt edges, 1 is a thin lens


# ===========================================================================
# =============================   SHARK PARTS   =============================
# ===========================================================================
#
#   blade             A solid fin: rows from a leading to a trailing edge, plump in the middle.
#   round_tip         Share of the chord left at a height, so a blade ends in a round tip.
#   build_sharkfin    Back fin of a shark (model space Top): a tall swept triangle.
#   build_sharktail   Tail of a shark (model space Tail): a crescent, the upper lobe bigger.
#
#   Both fins stand upright in the XY plane and sway in one second ("Flutter"), like
#   the ray fins: the game plays the same loop on them.
#

def blade(scene, name, rows, material_name):
    """
    A solid upright fin from rows of (leading point, trailing point, thickness), points
    in Blender axes (use G). Every row is a lens across the chord: both edges are sharp,
    the middle is as thick as the row says. A row with the same point twice closes the
    blade to a tip. The first row stays open, it hides in the body.
    """
    bm = bmesh.new()
    sides = []
    for side in (-1.0, 1.0):
        grid = []
        for lead, trail, thickness in rows:
            line = []
            for column in range(BLADE_ACROSS + 1):
                share = column / BLADE_ACROSS
                bulge = math.sin(share * math.pi) ** BLADE_ROUNDNESS
                line.append(bm.verts.new(lead.lerp(trail, share) + AXIS_OUT * (side * thickness * 0.5 * bulge)))
            grid.append(line)
        sides.append(grid)
    for grid in sides:
        for row in range(len(rows) - 1):
            for column in range(BLADE_ACROSS):
                bm.faces.new([grid[row][column], grid[row][column + 1], grid[row + 1][column + 1], grid[row + 1][column]])

    # Both sides meet at the edges and at a closed tip
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    obj.modifiers.new('Smooth', 'SUBSURF').levels = 1
    apply_modifiers(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def round_tip(share, start=0.86):
    """1 up to start, then a quarter circle down to 0 at share 1."""
    if share <= start:
        return 1.0
    return math.sqrt(max(0.0, 1.0 - ((share - start) / (1.0 - start)) ** 2))


def build_sharkfin(scene):
    """
    The first back fin of a great white: 1 unit tall on a base of about 0.9, the leading
    edge leans back in a soft bow, the apex is round, the trailing edge falls almost
    straight and ends in a small free tip behind the base. The base dips below the
    origin so it sinks into the back.
    """
    HEIGHT, SINK, STEPS = 1.0, 0.12, 16
    rows = []
    for step in range(STEPS + 1):
        share = step / STEPS
        lead_x = 0.5 - 0.78 * share ** 1.25
        # Trailing edge: hollow in the middle, the free rear tip kicks back near the base
        trail_x = -0.36 + 0.09 * math.sin(share * math.pi) - 0.13 * math.exp(-share * 9.0)
        middle = (lead_x + trail_x) / 2
        half = (lead_x - trail_x) / 2 * round_tip(share, 0.78)
        y = -SINK + (HEIGHT + SINK) * share
        rows.append((G(middle + half, y, 0), G(middle - half, y, 0), 0.13 - 0.09 * share))
    fin = blade(scene, 'Fin', rows, 'BackSkin')

    # Sway: the upper half of the fin leans from side to side, the tip a little behind
    armature = rig(scene, 'Rig', [
        ('Base', G(0.05, -SINK, 0), G(-0.05, 0.3, 0), None),
        ('Mid', G(-0.05, 0.3, 0), G(-0.2, 0.68, 0), 'Base'),
        ('Tip', G(-0.2, 0.68, 0), G(-0.32, 1.0, 0), 'Mid'),
    ])
    bind(armature, [fin])
    animate(armature, 'Flutter', FPS, {
        'Mid': (AXIS_FORWARD, lambda share: 5.0 * wave(share)),
        'Tip': (AXIS_FORWARD, lambda share: 7.0 * wave(share, -0.15)),
    })


def build_sharktail(scene):
    """
    The tail of a shark, root at the origin, reaching back (-X): a crescent of two
    swept lobes, the upper one bigger (0.85 up, 0.55 down). A flat keel of skin widens
    the tail stalk in front of it, like the keels of a great white.
    """
    UPPER, LOWER, STEPS = 0.85, 0.55, 12
    rows = []
    for step in range(-STEPS, STEPS + 1):
        share = abs(step) / STEPS
        is_upper = step >= 0
        reach = UPPER if is_upper else LOWER
        # Both edges sweep back towards the tip, the trailing edge is the hollow of the crescent
        lead_x = 0.24 - (0.7 if is_upper else 0.5) * share ** 1.5
        trail_x = -0.26 - (0.36 if is_upper else 0.24) * share ** 0.7
        middle = (lead_x + trail_x) / 2
        half = (lead_x - trail_x) / 2 * round_tip(share, 0.7)
        y = reach * share * (1.0 if is_upper else -1.0)
        rows.append((G(middle + half, y, 0), G(middle - half, y, 0), 0.12 - 0.08 * share))
    # Lower tip first: the blade closes at both ends
    fin = blade(scene, 'Fin', rows, 'BackSkin')
    keel = ellipsoid(scene, 'Keel', G(0.04, 0, 0), (0.22, 0.13, 0.07), 'BackSkin', subdivisions=3)

    # Sway: both lobes trail behind the stalk, the tips a little later
    armature = rig(scene, 'Rig', [
        ('Stalk', G(0.2, 0, 0), G(-0.05, 0, 0), None),
        ('Upper0', G(-0.05, 0.05, 0), G(-0.3, 0.45, 0), 'Stalk'),
        ('Upper1', G(-0.3, 0.45, 0), G(-0.6, 0.85, 0), 'Upper0'),
        ('Lower0', G(-0.05, -0.05, 0), G(-0.22, -0.3, 0), 'Stalk'),
        ('Lower1', G(-0.22, -0.3, 0), G(-0.4, -0.55, 0), 'Lower0'),
    ])
    bind(armature, [fin, keel], rigid={'Keel': 'Stalk'})
    animate(armature, 'Flutter', FPS, {
        'Upper0': (AXIS_UP, lambda share: 7.0 * wave(share)),
        'Upper1': (AXIS_UP, lambda share: 9.0 * wave(share, -0.12)),
        'Lower0': (AXIS_UP, lambda share: 7.0 * wave(share)),
        'Lower1': (AXIS_UP, lambda share: 9.0 * wave(share, -0.12)),
    })
