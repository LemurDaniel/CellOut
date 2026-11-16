# NOTE:
#   Bird heads of the creature editor (tab Heads, model space Head): skulls with slots.
#   The beak is part of every skull, the eyes and a crest are parts of the player on the
#   slots Eye and Horn. Registered in build.models.py.
#   Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   ./tools/blender/Show-Model.ps1 -Module new_heads_bird -Call 'build_parrothead(scene)' -Name parrothead
#   ./tools/blender/Show-Model.ps1 -Module new_heads_bird -Call 'build_raptorhead(scene)' -Name raptorhead

#
#   Chapters:
#     SKULL PIECES   Ball, hooked beak, straight beak, patch on the skin, slots: all drawn around the skull center.
#     BIRD HEADS     Parrot, penguin and raptor.
#

import math

import bmesh
import bpy
from mathutils import Vector

from models.helpers import G, ellipsoid, link_mesh, slot, tube
from models.new_bird import sheet

HEAD_SCALE = 1.35                    # the skull is this much bigger than the neck stump (radius 1)
STUMP_CENTER = (0.15, 0.0, 0.0)      # the neck plug behind the skull, it sinks into the body
STUMP_SIZE = (0.6, 1.0, 1.0)         # in Blender axes of the helper: forward, out, up
BEAK_AROUND = 14                     # points of a beak cross-section
PATCH_RINGS = 6
PATCH_SPOKES = 20

# Per head, everything in skull radii around the skull center (x forward, y up, z out of the right flank):
#   center   where the skull center sits in the model space Head (forward, up)
#   size     half length, half height, half width of the skull
#   bulges   where it swells: direction (z mirrored when not 0), amount, sharpness
PARROT_SKULL = {'center': (0.85, 0.2), 'size': (1.0, 0.95, 0.93), 'bulges': (((0.5, 0.75, 0.0), 0.08, 4.0),)}
PENGUIN_SKULL = {'center': (0.8, 0.12), 'size': (1.05, 0.86, 0.84), 'bulges': (((1.0, -0.12, 0.0), 0.45, 5.0), ((-0.8, -0.4, 0.0), 0.12, 3.0))}
RAPTOR_SKULL = {'center': (0.8, 0.12), 'size': (1.05, 0.8, 0.9), 'bulges': (((1.0, -0.1, 0.0), 0.22, 5.0), ((0.55, 0.5, 0.62), 0.2, 16.0))}

# A hooked upper beak is an arc around a point of the face: its root at the angle 'start' (degrees, 90 = up),
# the tip at 'end', the dark tip begins at 'dark', a colored cere (optional) ends at 'cere'; 'thick' and 'wide' are the half sizes of the root
PARROT_HOOK = {'center': (0.62, -0.22), 'arc': 0.62, 'start': 120.0, 'dark': -28.0, 'end': -62.0, 'thick': 0.42, 'wide': 0.43}
RAPTOR_HOOK = {'center': (1.04, -0.34), 'arc': 0.46, 'start': 124.0, 'cere': 72.0, 'dark': 5.0, 'end': -68.0, 'thick': 0.32, 'wide': 0.36}

PENGUIN_BEAK_LENGTH = 1.25           # from the face to the tip
PENGUIN_BEAK_DROOP = 0.13            # the tip hangs this far below the root


# ===========================================================================
# =============================   SKULL PIECES   ============================
# ===========================================================================
#
#   to_head      Moves a mesh drawn around the skull center into the model space Head.
#   head_point   One point of the skull space in the model space Head, as a Godot tuple.
#   swell        How far the skull is pushed out in a direction.
#   ball         The skull: a ball that swells where the head bulges.
#   on_skin      The point on the skin of the skull above a spot of its side view, and the way out there.
#   patch        A thin raised area on the skin of both sides.
#   hook         A hooked upper beak with a dark tip.
#   stump        The neck plug behind the skull.
#

def to_head(mesh, skull):
    """Moves a mesh drawn in skull radii around the skull center into the model space Head (neck stump radius 1)."""
    shift = G(skull['center'][0], skull['center'][1], 0)
    for vertex in mesh.data.vertices:
        vertex.co = vertex.co * HEAD_SCALE + shift
    return mesh


def head_point(skull, point):
    """A point given in skull radii around the skull center (Godot axes) in the model space Head, as a Godot tuple for slot()."""
    return (point[0] * HEAD_SCALE + skull['center'][0], point[1] * HEAD_SCALE + skull['center'][1], point[2] * HEAD_SCALE)


def swell(skull, direction):
    """Factor on the radius of the skull in a direction (Godot axes, unit length): 1 plus every bulge near it."""
    factor = 1.0
    for towards, amount, sharpness in skull['bulges']:
        for side in ((1.0,) if 0.0 == towards[2] else (1.0, -1.0)):
            peak = Vector((towards[0], towards[1], towards[2] * side)).normalized()
            factor += amount * math.exp(-sharpness * (1.0 - direction.dot(peak)))
    return factor


def ball(scene, skull):
    """The skull as one smooth ball of skin: an ellipsoid of its size, pushed out along its bulges, so brow and face have no seams."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1.0)
    for vertex in bm.verts:
        # Blender unit sphere (x forward, -y the right flank, z up) read as a direction in Godot axes
        direction = Vector((vertex.co.x, vertex.co.z, -vertex.co.y))
        reach = swell(skull, direction)
        vertex.co = G(direction.x * skull['size'][0] * reach, direction.y * skull['size'][1] * reach, direction.z * skull['size'][2] * reach)
    mesh = bpy.data.meshes.new('Skull')
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, 'Skull', mesh, 'Skin')
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return to_head(obj, skull)


def on_skin(skull, x, y, lift=1.0):
    """
    The point on the right side of the skull above the spot (x, y) of its side view, in
    skull radii (Godot axes), lifted off the skin by the factor lift, and the direction
    out of the skin there.
    """
    size_x, size_y, size_z = skull['size']
    inside = max(0.0, 1.0 - (x / size_x) ** 2 - (y / size_y) ** 2)
    flat = Vector((x / size_x, y / size_y, math.sqrt(inside)))
    reach = swell(skull, flat.normalized()) if flat.length > 1e-6 else 1.0
    point = Vector((x, y, size_z * math.sqrt(inside))) * (reach * lift)
    out = Vector((point.x / size_x ** 2, point.y / size_y ** 2, point.z / size_z ** 2))
    return point, (out.normalized() if out.length > 1e-6 else Vector((0.0, 0.0, 1.0)))


def patch(scene, name, skull, center, size, puff, material_name):
    """
    A thin raised area on the skin of both sides of the skull (a bare face, an ear
    patch): an oval around center (forward, up) with the half sizes of size, puff above
    the skin in its middle, its rim sunk into the skin.
    """
    for side in (-1, 1):
        rows = []
        for ring in range(PATCH_RINGS + 1):
            share = ring / PATCH_RINGS
            row = []
            for spoke in range(PATCH_SPOKES):
                turn = math.tau * spoke / PATCH_SPOKES
                point, _ = on_skin(skull, center[0] + size[0] * share * math.cos(turn), center[1] + size[1] * share * math.sin(turn),
                                   lift=1.0 + puff * (1.0 - 1.5 * share ** 3))
                row.append(G(point.x, point.y, point.z * side))
            rows.append(row)
        area = sheet(scene, f'{name}{side}', rows, material_name)
        # An open sheet has no inside: turn its faces out of the head
        probe = max(area.data.polygons, key=lambda polygon: polygon.area)
        if probe.normal.dot(G(0, 0, side)) < 0.0:
            area.data.flip_normals()
        to_head(area, skull)


def hook(scene, skull, shape, material_name='Bone'):
    """
    A hooked upper beak: cross-sections along an arc of the face, thick and wide at the
    root, running into the point of the hook, which hangs down instead of curling back.
    The last part from the angle 'dark' is the dark tip.
    """
    def rows(start, end, steps):
        result = []
        for step in range(steps + 1):
            angle = start + (end - start) * step / steps
            share = (shape['start'] - angle) / (shape['start'] - shape['end'])      # 0 at the root .. 1 at the tip
            taper = max(0.0, 1.0 - share ** 2.2)
            radial = G(math.cos(math.radians(angle)), math.sin(math.radians(angle)), 0)
            middle = G(*shape['center'], 0) + radial * (shape['arc'] * (1.0 + 0.3 * share ** 3))
            thick = shape['thick'] * taper + 0.008
            wide = shape['wide'] * (0.25 + 0.75 * taper) * taper ** 0.35 + 0.006
            result.append([middle + radial * (thick * math.cos(turn)) + G(0, 0, wide * math.sin(turn))
                           for turn in (math.tau * i / BEAK_AROUND for i in range(BEAK_AROUND))])
        return result

    # A cere: the soft root of the beak up to the angle 'cere' has its own color
    root_end = shape.get('cere', shape['start'])
    if root_end < shape['start']:
        to_head(sheet(scene, 'Cere', rows(shape['start'], root_end, 6), 'Beak'), skull)
    to_head(sheet(scene, 'Beak', rows(root_end, shape['dark'], 18), material_name), skull)
    to_head(sheet(scene, 'BeakTip', rows(shape['dark'], shape['end'], 9), 'Dark'), skull)


def stump(scene):
    """The neck plug behind the skull: a round stump of radius 1 that sinks into the body."""
    return ellipsoid(scene, 'Stump', G(*STUMP_CENTER), STUMP_SIZE, 'Skin', subdivisions=3)


# ===========================================================================
# ==============================   BIRD HEADS   =============================
# ===========================================================================
#
#   build_parrothead    Round macaw skull, big hooked beak, bare pale face.
#   build_penguinhead   Smooth skull, slim beak with an orange stripe, ear patches.
#   build_raptorhead    Flat skull, brow ridge, short hooked beak with a cere.
#

def build_parrothead(scene):
    """
    The head of a macaw (model space Head: origin at the neck joint, +X forward, +Y up,
    +Z the right flank, the neck stump has radius 1):

            .-''''-.            round skull, the pale upper beak grows out of
          /  skull  .--.        the forehead as a deep arc, as deep as it is long
     ===(  (eye)  /     \\
          \\ face |  pale |      bare pale face around the eye, flat on the skull
           '----'\\ dark  |      dark tip of the hook over the short
                 '----' v       dark lower beak

    The eye is not part of the model: slot Eye inside the face patch. Slot Horn on the
    crown takes a crest. No rig, nothing moves.
    """
    skull = PARROT_SKULL
    ball(scene, skull)
    stump(scene)
    hook(scene, skull, PARROT_HOOK)
    # Lower beak: a short dark scoop under the root of the upper one, tucked behind the hook
    to_head(ellipsoid(scene, 'Lower', G(0.72, -0.5, 0), (0.4, 0.44, 0.36), 'Dark', subdivisions=3), skull)
    patch(scene, 'Face', skull, (0.22, 0.05), (0.54, 0.46), 0.03, 'Down')

    eye, out = on_skin(skull, 0.18, 0.2, lift=1.02)
    slot(scene, 'Eye', head_point(skull, eye), 0.21, out=tuple(out), forward=(1.0, 0.0, 0.0))
    crown, _ = on_skin(skull, -0.05, skull['size'][1] * 0.97)
    slot(scene, 'Horn', head_point(skull, (crown.x, crown.y, 0.1)), 0.9, out=(0.0, 1.0, 0.25), forward=(1.0, 0.0, 0.0))


def build_penguinhead(scene):
    """
    The head of a king penguin (model space Head: origin at the neck joint, +X forward,
    +Y up, +Z the right flank, the neck stump has radius 1):

             .-'''-.
        ===(  ear   (eye) '--.___         smooth skull running out into the beak,
            \\ patch       ___.---'==-     slim dark upper beak, bent down a little,
             '-.______.-'  orange         an orange stripe along the lower beak

    A raised drop-shaped ear patch lies on each side behind the eye. The eye is a slot.
    No rig, nothing moves.
    """
    skull = PENGUIN_SKULL
    ball(scene, skull)
    stump(scene)

    def beak_line(start, length, droop, radius, steps=8):
        points = [G(start[0] + length * share, start[1] - droop * share * share, 0) for share in (i / steps for i in range(steps + 1))]
        radii = [radius * (1.0 - 0.8 * share ** 1.5) for share in (i / steps for i in range(steps + 1))]
        return points, radii, [(i, i + 1) for i in range(steps)]

    upper = tube(scene, 'Beak', *beak_line((0.75, -0.08), PENGUIN_BEAK_LENGTH + 0.4, PENGUIN_BEAK_DROOP * 1.5, 0.3), 'Dark')
    # Lower beak: shorter, under the upper one, its flanks are the orange stripe
    lower = tube(scene, 'Stripe', *beak_line((0.75, -0.24), PENGUIN_BEAK_LENGTH + 0.1, PENGUIN_BEAK_DROOP * 0.9, 0.2), 'Beak')
    for mesh, width in ((upper, 0.72), (lower, 0.95)):
        for vertex in mesh.data.vertices:
            vertex.co.y *= width                     # the upper beak is narrow, the lower one shows beside it as the stripe
        to_head(mesh, skull)
    patch(scene, 'Ear', skull, (-0.2, 0.0), (0.3, 0.5), 0.045, 'FruitGold')

    eye, out = on_skin(skull, 0.38, 0.14, lift=1.02)
    slot(scene, 'Eye', head_point(skull, eye), 0.17, out=tuple(out), forward=(1.0, 0.0, 0.0))


def build_raptorhead(scene):
    """
    The head of an eagle (model space Head: origin at the neck joint, +X forward, +Y up,
    +Z the right flank, the neck stump has radius 1):

            ________
          /  flat   '==brow==.          flat skull, a bony brow ridge shades the eye
     ===(  skull    (eye)  cere'-.      and falls towards the beak: the fierce look
          \\______         \\_beak  |     yellow cere at the root of a short strong
                  '-.___gape_'--' v     beak, the dark hook far over the lower beak

    The eye is a slot under the brow, slot Horn on the crown takes a crest. No rig.
    """
    skull = RAPTOR_SKULL
    ball(scene, skull)
    stump(scene)
    hook(scene, skull, RAPTOR_HOOK)
    # The yellow cere is the root of the hook itself, the yellow gape runs back from it under the eye
    to_head(ellipsoid(scene, 'Lower', G(1.08, -0.42, 0), (0.3, 0.2, 0.1), 'Bone', subdivisions=3), skull)
    for side in (-1, 1):
        gape = [G(point.x, point.y, point.z * side) for point in (on_skin(skull, x, y, lift=1.0)[0] for x, y in ((1.2, -0.34), (0.9, -0.36), (0.55, -0.3)))]
        to_head(tube(scene, f'Gape{side}', gape, (0.05, 0.055, 0.03), [(0, 1), (1, 2)], 'Beak', subdivisions=1), skull)
        # Brow: a shelf of skin over the eye, high behind it and falling to the beak root
        brow = [G(point.x, point.y, point.z * side) for point in (on_skin(skull, x, y, lift=1.0)[0] for x, y in ((1.0, 0.1), (0.68, 0.32), (0.3, 0.42), (-0.1, 0.38)))]
        to_head(tube(scene, f'Brow{side}', brow, (0.1, 0.17, 0.15, 0.05), [(0, 1), (1, 2), (2, 3)], 'Skin', subdivisions=2), skull)

    eye, out = on_skin(skull, 0.42, 0.14, lift=1.0)
    slot(scene, 'Eye', head_point(skull, eye), 0.2, out=tuple(out), forward=(1.0, 0.0, 0.0))
    crown, _ = on_skin(skull, -0.2, skull['size'][1] * 0.97)
    slot(scene, 'Horn', head_point(skull, (crown.x, crown.y, 0.1)), 0.9, out=(0.0, 1.0, 0.25), forward=(1.0, 0.0, 0.0))
