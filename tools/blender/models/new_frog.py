# NOTE:
#   New parts of the frog family of templates (frog, crocodile): the webbed foot of a
#   frog, the snout details of a crocodile and the rows of bony plates on its back.
#   Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_frog -Call 'build_webbedfoot(scene)' -Name webbedfoot
#   ./tools/blender/Show-Model.ps1 -Module new_frog -Call 'build_backplates(scene)' -Name backplates

#
#   Chapters:
#     FROG PARTS        The webbed foot.
#     CROCODILE PARTS   Snout details, back plates, tail, foot and eye.
#

import math

import bmesh
import bpy
from mathutils import Matrix

from models.helpers import AXIS_UP, FPS, G, animate, bind, curved_claw, digit, ellipsoid, link_mesh, rig, tube, wave

WEB_TOES = ((-62, 0.22), (-32, 0.3), (-4, 0.36), (24, 0.34), (52, 0.26))      # angle in degrees (0 = forward), length
WEB_TOE_DROP = 0.12                  # toes point this much down per unit forward
WEB_TOE_CURL = 0.1                   # and bend down by this share of their length
WEB_REACH = 0.93                     # the skin ends at this share of a toe ...
WEB_SCALLOP = 0.15                   # ... and dips back by this share between two toes
WEB_STEPS = 6                        # columns of the skin between two toes
WEB_RINGS = 5                        # rows of the skin from the ball to its edge
WEB_THICKNESS = 0.012

NOSTRIL_AT = (0.87, 0.64)           # the nostril mound in snout space: forward, up

PLATE_ROWS = ((0.15, 1.0), (0.43, 0.92), (0.68, 0.74))             # out of the middle (z of the unit circle), size of the plates of that row
PLATE_COUNT = 13                     # plates per row from the neck (+x) to the hip (-x)
PLATE_HALF_LENGTH = 0.3              # the game stretches x by Reach * body length: the rows cover about half of the animal
PLATE_SQUEEZE = 0.155                # a plate is this much shorter in x than in z, the stretch of the game makes it square
PLATE_SIZE = 0.15                    # half width of a plate of the middle row
PLATE_KEEL = 0.26                    # height of its keel
PLATE_BOXINESS = 0.55                # exponent on the sphere of a pad: 1 = round, lower = a pillow with corners
PLATE_TAPER = 0.3                    # the field sinks by this share towards neck and hip, where a body gets thinner

TAIL_XS = (0.35, 0.0, -0.3, -0.6, -0.9, -1.2, -1.5, -1.7)            # the core of the tail from inside the body to its tip
TAIL_RADII = (0.24, 0.24, 0.2, 0.165, 0.13, 0.095, 0.06, 0.025)
TAIL_LENGTH = 1.7
TAIL_NARROW = 0.5                    # the tip is this much narrower than round: flat on the sides
TAIL_TALLER = 0.3                    # and this much taller
TAIL_DOUBLE_END = -0.5               # the double row of scutes runs to here, then one crest
TAIL_BANDS = (-0.2, -0.65, -1.05, -1.4)                              # dark bands across the tail (BackSkin)
TAIL_SWAY = (0.0, 5.0, 8.0, 11.0)    # degrees per bone of the Flutter

FOOT_TOES = ((-50, 0.12), (-17, 0.15), (16, 0.15), (48, 0.12))       # angle in degrees (0 = forward), length

# ===========================================================================
# =============================   FROG PARTS   ==============================
# ===========================================================================
#
#   toe_point          A point on a toe: forward, bending down.
#   build_webbedfoot   The webbed foot of a frog: five long toes with round tips, skin between them.
#

def toe_point(center, angle, length, share):
    """The point at a share of a toe leaving center at an angle (degrees, 0 = forward)."""
    direction = G(math.cos(math.radians(angle)), -WEB_TOE_DROP, math.sin(math.radians(angle))).normalized()
    return center + direction * (length * share) + G(0, -length * WEB_TOE_CURL * share * share, 0)


def build_webbedfoot(scene):
    """
    The webbed foot of a frog, the end of a sculpted limb: the ankle at the origin, the
    sole below it, five long thin toes fanned forward (+X) with a round pad on every
    tip and a sheet of skin between them, scalloped at its edge.
    """
    center = G(0.05, -0.075, 0)
    tube(scene, 'Foot', [G(0, 0, 0), center], (0.08, 0.085), [(0, 1)])
    ellipsoid(scene, 'Sole', center + G(0.02, -0.045, 0), (0.1, 0.09, 0.03), 'Pad', subdivisions=2)

    # Toes: thin, a knuckle in the middle, a fat round tip
    for i, (angle, length) in enumerate(WEB_TOES):
        points = [toe_point(center, angle, length, share) for share in (0.0, 0.35, 0.7, 1.0)]
        tube(scene, f'Toe{i}', points, (0.034, 0.022, 0.019, 0.022), [(0, 1), (1, 2), (2, 3)])
        ellipsoid(scene, f'ToeTip{i}', points[-1] + G(0, -0.004, 0), (0.04, 0.04, 0.032), 'Pad', subdivisions=2)

    # Skin: a thin fan from the ball of the foot to a scalloped edge, following the bend of the toes
    bm = bmesh.new()
    columns = []
    for gap, ((angle, length), (next_angle, next_length)) in enumerate(zip(WEB_TOES, WEB_TOES[1:])):
        for step in range(WEB_STEPS + (1 if gap == len(WEB_TOES) - 2 else 0)):
            share = step / WEB_STEPS
            dip = 1.0 - WEB_SCALLOP * math.sin(share * math.pi)
            columns.append((angle + (next_angle - angle) * share, (length + (next_length - length) * share), WEB_REACH * dip))
    for lift in (WEB_THICKNESS, -WEB_THICKNESS):
        layer = []
        for angle, length, reach in columns:
            layer.append([bm.verts.new(toe_point(center, angle, length, reach * ring / WEB_RINGS) + G(0, lift, 0)) for ring in range(WEB_RINGS + 1)])
        for column, following in zip(layer, layer[1:]):
            for ring in range(WEB_RINGS):
                bm.faces.new((column[ring], column[ring + 1], following[ring + 1], following[ring]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new('Web')
    bm.to_mesh(mesh)
    bm.free()
    web = link_mesh(scene, 'Web', mesh, 'Fin')
    for polygon in web.data.polygons:
        polygon.use_smooth = True


# ===========================================================================
# ===========================   CROCODILE PARTS   ===========================
# ===========================================================================
#
#   build_crocmaw      The nostril mound on the snout tip of a crocodile.
#   scute              One bony keel: a pyramid on a rectangle, the tip leaning back.
#   build_backplates   Rows of bony plates along the back.
#   build_croctail     The strong flat tail with a double row of scutes merging into one crest.
#   build_crocfoot     A broad foot with four short clawed toes.
#   build_croceye      A golden eye with a slit pupil in a bony bump under a heavy brow.
#

def build_crocmaw(scene):
    """
    The snout of a crocodile (Snout: x 0 at the mouth corners .. 1 at the nose tip, z the
    width of the head). The long mouth is carved into the head and the game stands the
    teeth on its lips (data/mouths.json), the model only adds what clay and teeth cannot:
    the round nostril mound on the tip of the snout with its two dark nostrils.
    """
    ellipsoid(scene, 'Mound', G(*NOSTRIL_AT, 0), (0.04, 0.22, 0.2), 'Skin', subdivisions=3)
    for side in (-1, 1):
        ellipsoid(scene, f'Nostril{side}', G(NOSTRIL_AT[0] + 0.006, NOSTRIL_AT[1] + 0.14, side * 0.08), (0.01, 0.05, 0.05), 'Dark', subdivisions=2)


def scute(scene, name, length, height, width, lean=0.3, material='ShellRim'):
    """
    One bony keel at the origin: a pyramid on a rectangle (length along x, width along
    Godot z), its tip height up and leaning back by lean * length. Flat faces on
    purpose, the edges catch the light. Move it with mesh.data.transform.
    """
    bm = bmesh.new()
    corners = [bm.verts.new(G(x * length * 0.5, -0.03, z * width * 0.5)) for x, z in ((1, 1), (1, -1), (-1, -1), (-1, 1))]
    tip = bm.verts.new(G(-lean * length, height, 0))
    bm.faces.new(corners)
    for corner, following in zip(corners, corners[1:] + corners[:1]):
        bm.faces.new((corner, following, tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return link_mesh(scene, name, mesh, material)


def build_backplates(scene):
    """
    The bony plates on the back of a crocodile (Hug: around a unit sphere at the origin,
    the game stretches x along the spine, y to the height and z to the width of the
    body). Three rows on each side of the spine, every plate a low square pad with a
    pointed keel, lying on the curve of the back. The rows are short in x because the
    game stretches x by a share of the whole body length.
    """
    for row, (out, size) in enumerate(PLATE_ROWS):
        angle = math.asin(out)
        # The outer rows start later and end earlier: a rounded field of plates
        skipped = (0, 0, 1)[row]
        for index in range(skipped, PLATE_COUNT - skipped):
            along = 1.0 - 2.0 * index / (PLATE_COUNT - 1)
            forward = PLATE_HALF_LENGTH * along
            radius = 0.97 * (1.0 - PLATE_TAPER * along * along)
            for side in (-1, 1):
                name = f'{row}_{index}_{side}'
                pad = ellipsoid(scene, 'Pad' + name, G(0, 0, 0), (1.0, 1.0, 1.0), 'Shell', subdivisions=2)
                for vertex in pad.data.vertices:
                    # A pillow with corners instead of a lens, then squeezed to the plate
                    boxy = [math.copysign(abs(value) ** PLATE_BOXINESS, value) for value in vertex.co]
                    vertex.co = (boxy[0] * PLATE_SIZE * PLATE_SQUEEZE * size, boxy[1] * PLATE_SIZE * size, boxy[2] * 0.075)
                keel = scute(scene, 'Keel' + name, PLATE_SIZE * PLATE_SQUEEZE * size * 1.7, PLATE_KEEL * size + 0.05, PLATE_SIZE * size * 0.75)
                keel.data.transform(Matrix.Translation(G(0, 0.05, 0)))
                # Built lying at the origin, then stood on the back: up along the surface normal there
                stand = Matrix.Translation(G(forward, 0, 0)) @ Matrix.Rotation(side * angle, 4, 'X') @ Matrix.Translation(G(0, radius, 0))
                for mesh in (pad, keel):
                    mesh.data.transform(stand)


def build_croctail(scene):
    """
    The tail of a crocodile (Tail): root at the origin on the tail tip of the body,
    reaching back (-X), about two units long. A strong core as thick as the end of the
    body, flat on the sides towards the tip. On top a double row of scutes that merges
    into a single tall crest, dark bands across. It sweeps from side to side ("Flutter", 2 s).
    """
    def flat(x):
        return min(1.0, max(0.0, -x / TAIL_LENGTH))

    def radius_at(x):
        for (x0, r0), (x1, r1) in zip(zip(TAIL_XS, TAIL_RADII), zip(TAIL_XS[1:], TAIL_RADII[1:])):
            if x1 <= x <= x0:
                return r0 + (r1 - r0) * (x0 - x) / (x0 - x1)
        return TAIL_RADII[-1]

    def top_at(x):
        return radius_at(x) * (1.0 + TAIL_TALLER * flat(x))

    def half_width_at(x):
        return radius_at(x) * (1.0 - TAIL_NARROW * flat(x))

    core = tube(scene, 'Core', [G(x, 0, 0) for x in TAIL_XS], TAIL_RADII, [(i, i + 1) for i in range(len(TAIL_XS) - 1)])
    for vertex in core.data.vertices:
        share = flat(vertex.co.x)
        vertex.co.y *= 1.0 - TAIL_NARROW * share
        vertex.co.z *= 1.0 + TAIL_TALLER * share
    meshes = [core]

    # Bands: dark rings a little bigger than the core, lifted so the underside stays pale
    for i, x in enumerate(TAIL_BANDS):
        meshes.append(ellipsoid(scene, f'Band{i}', G(x, top_at(x) * 0.22, 0), (0.1, half_width_at(x) * 1.04, top_at(x) * 0.86), 'BackSkin', subdivisions=3))

    # Scutes: two rows closing in on each other, then one crest that grows and runs out at the tip
    x = 0.22
    index = 0
    while x > -TAIL_LENGTH + 0.1:
        is_double = x > TAIL_DOUBLE_END
        share = flat(x)
        if is_double:
            height, length = 0.1 + 0.05 * share * 3.0, 0.15
        else:
            height, length = 0.3 * (1.0 - share) ** 0.6 + 0.07, 0.2 - 0.06 * share
        for side in ((-1, 1) if is_double else (0,)):
            out = side * half_width_at(x) * 0.5 * max(0.25, 1.0 - (0.22 - x) / (0.22 - TAIL_DOUBLE_END) * 0.75)
            keel = scute(scene, f'Scute{index}_{side}', length, height, 0.07 if is_double else 0.06 - 0.02 * share, lean=0.35)
            keel.data.transform(Matrix.Translation(G(x, top_at(x) * (0.93 if is_double else 0.97), out)))
            meshes.append(keel)
        x -= length * 0.92
        index += 1

    count = len(TAIL_SWAY)
    ends = [TAIL_XS[0] + (-TAIL_LENGTH - TAIL_XS[0]) * i / count for i in range(count + 1)]
    armature = rig(scene, 'Rig', [(f'Tail{i}', G(ends[i], 0, 0), G(ends[i + 1], 0, 0), f'Tail{i - 1}' if i > 0 else None) for i in range(count)])
    bind(armature, meshes)
    animate(armature, 'Flutter', FPS * 2, {
        f'Tail{i}': (AXIS_UP, lambda share, phase=-i * 0.16, degrees=TAIL_SWAY[i]: degrees * wave(share, phase)) for i in range(count)
    })


def build_crocfoot(scene):
    """
    The foot of a crocodile, the end of a sculpted limb: the ankle at the origin, the
    sole below it, toes forward (+X). A broad flat foot, four short thick toes spread
    wide, every toe ends in a short blunt claw. No pads, the sole is skin.
    """
    center = G(0.05, -0.08, 0)
    tube(scene, 'Foot', [G(0, 0, 0), center], (0.09, 0.115), [(0, 1)])
    ellipsoid(scene, 'Palm', center + G(0.05, -0.025, 0), (0.14, 0.15, 0.055), 'Skin', subdivisions=3)
    for i, (angle, length) in enumerate(FOOT_TOES):
        direction = G(math.cos(math.radians(angle)), -0.1, math.sin(math.radians(angle)))
        base = center + G(0.03, -0.02, 0) + direction.normalized() * 0.08
        _, tip, tip_direction = digit(scene, f'Toe{i}', base, direction, length, (0.055, 0.05, 0.044), curl=0.18)
        curved_claw(scene, f'Claw{i}', tip - tip_direction * 0.015, tip_direction + G(0, 0.2, 0), 0.1, 0.036, curl=0.5)


def build_croceye(scene):
    """
    The eye of a crocodile (Eye: an eyeball of radius 1 at the origin, +Z out of the
    head, +X forward): a golden eye with a vertical slit pupil, sunk into a bony bump
    of skin. A heavy brow hood lies over it and a ridge runs out towards the snout, a lower
    lid closes the bump below.
    """
    ellipsoid(scene, 'Ball', G(0, 0, 0.05), (0.95, 0.95, 0.95), 'Dark', subdivisions=3)
    ellipsoid(scene, 'Iris', G(0.04, -0.02, 0.2), (0.9, 0.9, 0.9), 'FruitGold', subdivisions=3)
    ellipsoid(scene, 'Pupil', G(0.1, -0.02, 0.66), (0.22, 0.46, 0.72), 'Pupil', subdivisions=3)
    ellipsoid(scene, 'Glint', G(0.36, 0.3, 0.98), (0.12, 0.12, 0.12), 'Glint', subdivisions=2)

    # Bump: the bony mound the eye sits in, it swallows the back half of the ball
    ellipsoid(scene, 'Bump', G(-0.15, -0.15, -0.4), (1.45, 1.1, 1.15), 'Skin', subdivisions=3)

    # Brow: a heavy hood over the eye and a ridge running out of it towards the snout
    ellipsoid(scene, 'Hood', G(0.0, 0.78, -0.12), (1.3, 1.1, 0.5), 'Skin', subdivisions=3)
    ridge = [G(0.7, 0.62, 0.1), G(1.3, 0.2, 0.0), G(2.0, -0.45, -0.25)]
    tube(scene, 'Ridge', ridge, (0.34, 0.28, 0.14), [(0, 1), (1, 2)], 'Skin', subdivisions=1, max_pieces=3)
    # Lower lid: a thinner rim under the eye
    rim = [G(1.06 * math.cos(math.radians(a)), 1.06 * math.sin(math.radians(a)), 0.12) for a in (200, 235, 270, 305, 340)]
    tube(scene, 'Lid', rim, (0.22, 0.24, 0.24, 0.24, 0.22), [(i, i + 1) for i in range(len(rim) - 1)], 'Skin', subdivisions=1, max_pieces=2)