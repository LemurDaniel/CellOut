# NOTE:
#   New parts for the shark of the template library: a solid triangular back fin, a
#   crescent tail with a bigger upper lobe, a sickle pectoral fin (fleshy blades without
#   rays, unlike the ray fins of fins.py), the tooth rows of its grin and its bead eye. Not registered in build.models.py yet (the coordinator wires them).
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
from mathutils import Matrix

from models.helpers import (
    AXIS_FORWARD,
    AXIS_OUT,
    AXIS_UP,
    FPS,
    G,
    animate,
    apply_modifiers,
    bind,
    cone,
    ellipsoid,
    link_mesh,
    rig,
    tube,
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
#   build_sicklefin   Pectoral fin of a shark (model space FlankPair): a smooth swept sickle.
#   build_sharkmaw    Teeth of a shark (model space Snout): two rows in the carved grin under the snout.
#   build_beadeye     Eye of a shark (model space Eye): a black bead under a grim brow.
#
#   Both fins stand upright in the XY plane and sway in one second ("Flutter"), like
#   the ray fins: the game plays the same loop on them.
#

def blade(scene, name, rows, material_name, normal=AXIS_OUT):
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
                line.append(bm.verts.new(lead.lerp(trail, share) + normal * (side * thickness * 0.5 * bulge)))
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


def build_sicklefin(scene):
    """
    The pectoral fin of a great white: origin on the skin of the right flank, the fin
    reaches out (+Z) and hangs down by SICKLE_DROOP. Broad at the body, the leading edge
    sweeps back to a blunt point, the trailing edge is hollow like a sickle. About 1.25
    body heights long, smooth, no rays and no knobs.
    """
    SPAN, SINK, STEPS = 1.25, 0.12, 16
    SICKLE_DROOP = math.radians(32.0)
    out = G(0, -math.sin(SICKLE_DROOP), math.cos(SICKLE_DROOP))       # along the fin, from the flank to the tip
    flat = G(0, math.cos(SICKLE_DROOP), math.sin(SICKLE_DROOP))       # across its flat sides

    def along(x, reach):
        return G(x, 0, 0) + out * reach

    rows = []
    for step in range(STEPS + 1):
        share = step / STEPS
        lead_x = 0.34 - 0.8 * share ** 1.3
        # Trailing edge: hollow in the middle, running back to the tip
        trail_x = -0.3 + 0.13 * math.sin(share * math.pi) - 0.24 * share
        middle = (lead_x + trail_x) / 2
        half = (lead_x - trail_x) / 2 * round_tip(share, 0.8)
        reach = -SINK + (SPAN + SINK) * share
        rows.append((along(middle + half, reach), along(middle - half, reach), 0.12 - 0.085 * share))
    fin = blade(scene, 'Fin', rows, 'BackSkin', normal=flat)

    # Sway: the outer half of the fin waves slowly up and down, the tip a little behind
    armature = rig(scene, 'Rig', [
        ('Base', along(0.05, -SINK), along(-0.05, 0.4), None),
        ('Mid', along(-0.05, 0.4), along(-0.25, 0.85), 'Base'),
        ('Tip', along(-0.25, 0.85), along(-0.48, SPAN), 'Mid'),
    ])
    bind(armature, [fin])
    animate(armature, 'Flutter', FPS, {
        'Mid': (AXIS_FORWARD, lambda share: 4.0 * wave(share)),
        'Tip': (AXIS_FORWARD, lambda share: 6.0 * wave(share, -0.15)),
    })


def build_sharkmaw(scene):
    """
    The teeth of a great white in the grin the body carves under its snout (data/mouths.json,
    SharkMaw). Model space Snout: x 0 at the mouth corners .. 1 at the nose tip, the mouth
    line at y 0, z -1 and 1 the flanks at the corners.
    The mouth line sits low on a pointed head, so seen from below the opening is a U that
    closes well behind the nose tip (MAW_FRONT). The teeth follow that U:
    > Upper row:  flat triangles hanging from the upper edge, big at the front, small at the corners
    > Lower row:  smaller ones standing on the lower edge, in the gaps of the upper row
    Right at the front of the U the opening touches the belly line, teeth would hang out
    of the head there: the upper ones shrink towards it, the lower row leaves a gap.
    The numbers of the U fit a pointed head like the shark template with the SharkMaw line
    of mouths.json (length 5, level -0.38); a rounder head closes the U further forward.
    """
    MAW_FRONT = 0.6                  # x where the U closes under the snout
    MAW_WIDTH = 0.94                 # half the width of the U at the corners
    MAW_ROUND = 0.6                  # shape of the U: 0.5 a parabola, higher is more pointed
    MAW_INSET = 0.93                 # the teeth stand a little inside the lips
    SMILE_RISE = 0.187               # the mouth line climbs this far towards the corners (smile 0.14 / 0.75)
    EDGE = 0.267                     # the edges of the opening above and below the mouth line at the front (gape 0.2 / 0.75)
    TOOTH_FLATNESS = 0.45            # thickness of a tooth, share of its width
    UPPER_COUNT = 11
    APEX_GAP = 0.2                   # no lower teeth this close to the front of the U: there the opening lies on the belly line

    def on_lip(across, height):
        """A point of the U at a share across the mouth (-1 left corner .. 1 right corner), height above the mouth line."""
        z = across * MAW_WIDTH
        x = MAW_FRONT * (1.0 - abs(across) ** (1.0 / MAW_ROUND))
        line_y = SMILE_RISE * (1.0 - x * 1.03) ** 2
        # The opening is full only in the front part and closes towards the corners (CreatureClay.CarveMouth)
        share = min(1.0, max(0.0, x * 1.03 / 0.3))
        opening = max(0.22, share * share * (3.0 - 2.0 * share))
        return G(x * MAW_INSET, line_y + height * opening, z * MAW_INSET)

    def tooth(name, across, base_height, tip_height, radius):
        """A flat triangular tooth, its flat side facing out of the mouth."""
        base = on_lip(across, base_height)
        tip = on_lip(across * 0.96, tip_height)
        obj = cone(scene, name, base, tip, radius)
        # Flattened along the body (x): from below the row reads as a saw blade
        flatten = Matrix.Diagonal((TOOTH_FLATNESS, 1.0, 1.0, 1.0))
        obj.data.transform(Matrix.Translation(base) @ flatten @ Matrix.Translation(-base))

    spread = 0.86
    step = 2.0 * spread / (UPPER_COUNT - 1)
    for i in range(UPPER_COUNT):
        across = -spread + i * step
        size = min(1.0 - 0.5 * abs(across), 0.15 + 1.7 * abs(across))
        tooth(f'UpperTooth{i}', across, EDGE * 0.9, EDGE * 0.9 - EDGE * 1.25 * size, 0.1 * (0.55 + 0.45 * size))
    for i in range(UPPER_COUNT - 1):
        across = -spread + (i + 0.5) * step
        if abs(across) < APEX_GAP:
            continue
        size = min(1.0 - 0.5 * abs(across), 0.3 + 2.4 * abs(across))
        tooth(f'LowerTooth{i}', across, -EDGE * 0.9, -EDGE * 0.9 + EDGE * 0.95 * size, 0.085 * (0.55 + 0.45 * size))


def build_beadeye(scene):
    """
    The eye of a great white (model space Eye: eyeball of radius 1 at the origin on the
    right flank, +Z out of the head, +X forward): a glossy black bead, sunk into a
    ring of back skin, under a heavy brow that drops towards the nose, so the shark
    scowls. One small glint keeps it alive.
    """
    # Bead: all black, a little flat so it sits in the socket instead of bulging like a toy eye
    ellipsoid(scene, 'Bead', G(0, 0, 0.05), (0.86, 0.6, 0.86), 'Pupil', subdivisions=3)
    ellipsoid(scene, 'Glint', G(0.3, 0.34, 0.56), (0.15, 0.1, 0.15), 'Glint', subdivisions=2)

    # Socket: a closed ring of skin around the bead
    ring_count = 14
    ring = [G(math.cos(i / ring_count * math.tau) * 0.95, math.sin(i / ring_count * math.tau) * 0.95, 0.0) for i in range(ring_count)]
    tube(scene, 'Socket', ring, [0.2] * ring_count, [(i, (i + 1) % ring_count) for i in range(ring_count)], 'BackSkin')

    # Brow: a thick ridge over the upper half, low over the front of the eye (the scowl);
    # its root is the back end, the front end gets the round cap
    brow_angles = [165, 140, 115, 90, 65, 40, 18]
    brow = []
    for degrees in brow_angles:
        angle = math.radians(degrees)
        drop = 0.32 * max(0.0, math.cos(angle))                    # the front (+X) end hangs lower
        brow.append(G(math.cos(angle) * 1.0, math.sin(angle) * 0.95 - drop, 0.3))
    tube(scene, 'Brow', brow, (0.14, 0.22, 0.28, 0.3, 0.3, 0.27, 0.2), [(i, i + 1) for i in range(len(brow) - 1)], 'BackSkin')