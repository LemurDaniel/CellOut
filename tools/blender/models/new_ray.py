# NOTE:
#   New parts of the ray family of templates (manta ray, lobster): the huge wing of a
#   manta, the whip of a ray and the fan of hard plates at the end of a lobster tail.
#   Registered in build.models.py.
#   Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   ./tools/blender/Show-Model.ps1 -Module new_ray -Call 'build_mantawing(scene)' -Name mantawing
#   ./tools/blender/Show-Model.ps1 -Module new_ray -Call 'build_whiptail(scene)' -Name whiptail
#   ./tools/blender/Show-Model.ps1 -Module new_ray -Call 'build_tailfan(scene)' -Name tailfan

#
#   Chapters:
#     MANTA PARTS     The wing with its rig and the whip.
#     LOBSTER PARTS   The tail fan.
#

import math

import bmesh
import bpy
from mathutils import Matrix

from models.helpers import AXIS_FORWARD, AXIS_OUT, AXIS_UP, FPS, G, animate, bind, ellipsoid, join_by_material, link_mesh, rig, tube, wave

WING_SPAN = 2.6                      # from the flank to the tip, in body heights (the game scales by the part size on top)
WING_SINK = 0.35                     # the root reaches this far into the body
WING_ROOT_FRONT = 1.1                # the root chord: from here at the head ...
WING_ROOT_BACK = -1.5                # ... to here at the hip
WING_TIP_X = -0.3                    # the tip lies this far behind the middle of the root: swept back
WING_HOOK = 0.28                     # the last part of the wing bends back by this much more: the hooked tip
WING_HOLLOW = 0.5                    # the trailing edge runs this far forward of a straight line: hollow
WING_ROOT_THICKNESS = 0.9            # as thick as most of the body at the root ...
WING_TIP_THICKNESS = 0.035           # ... a blade at the tip
WING_BELLY = 0.55                    # the underside bulges this share of the top: flatter below
WING_ARCH = 0.12                     # the wing rises by this much in the middle of its span ...
WING_DROOP = 0.2                     # ... and the tip hangs this far down
WING_PALE_TIP = 0.87                 # from this share of the span on the top is pale like the belly
WING_MARK_SPAN = (0.15, 0.46)        # the pale mark on the shoulder: shares of the span it covers
WING_MARK_CHORD = 0.2                # share of the chord its middle lies at next to the body ...
WING_MARK_SLANT = 0.2                # ... and how far it runs back towards its outer end
WING_MARK_WIDTH = 0.085              # half its width, share of the chord
WING_MARK_RAISED = 0.012             # it lies this far above the skin
WING_ROWS = 26                       # rows from the root to the tip
WING_COLUMNS = 20                    # columns from the leading to the trailing edge
WING_BONES = ((-WING_SINK, 0.35), (0.35, 1.1), (1.1, 1.85), (1.85, WING_SPAN))      # span of every bone, root first
WING_BEAT = (0.0, 9.0, 10.0, 9.0)    # degrees per bone of the Beat, they add up towards the tip
WING_BEAT_LAG = 0.11                 # every bone beats this share of the loop later than the one before
WING_BEAT_SECONDS = 2.5

WHIP_LENGTH = 2.3
WHIP_XS = (0.62, 0.3, 0.0, -0.25, -0.6, -1.0, -1.45, -1.9, -WHIP_LENGTH)
WHIP_RADII = (0.2, 0.2, 0.12, 0.065, 0.04, 0.03, 0.022, 0.015, 0.008)
WHIP_SWAY = (0.0, 3.0, 5.0, 7.0, 8.0)                                              # degrees per bone of the Sway

FAN_THICKNESS = 0.035                # half the thickness of a plate of the tail fan
FAN_NARROW_ROOT = 0.62               # a plate is this much narrower at its root than at its round end
FAN_DROOP = 0.12                     # the end of a plate hangs down by this share of its length
FAN_MIDDLE = (0.56, 0.44)            # the middle plate: length, width
FAN_SIDES = ((0.1, 27.0, 0.58, 0.44, 5.0), (0.17, 54.0, 0.54, 0.42, 9.0))            # side plates, inner first: root out of the middle, angle, length, width, degrees of the Fan


# ===========================================================================
# =============================   MANTA PARTS   =============================
# ===========================================================================
#
#   wing_row          Leading point, trailing point, height and thickness of the wing at a share of its span.
#   wing_point        A point on the top or the underside of the wing.
#   is_pale           Whether a face of the top is pale: the tip.
#   wing_surface      One side of the wing as a mesh: the top or the underside, the faces of one color.
#   finish_surface    A bmesh as smooth mesh object.
#   shoulder_mark     The pale mark on the shoulder, a sheet on the top of the wing.
#   bind_by_span      Skin weights by the place along the span, a soft blend between two bones.
#   build_mantawing   The wing of a manta (FlankPair): a huge swept triangle, dark above, pale below.
#   build_whiptail    The whip of a ray (Tail): long and thin, a tiny fin on its root.
#

def wing_row(share):
    """The wing at a share of its span (0 inside the body, 1 the tip): (out, lead x, trail x, height, thickness)."""
    out = -WING_SINK + (WING_SPAN + WING_SINK) * share
    hook = WING_HOOK * max(0.0, (share - 0.7) / 0.3) ** 2
    lead = WING_ROOT_FRONT + (WING_TIP_X - WING_ROOT_FRONT) * share ** 1.3 - hook
    trail = WING_ROOT_BACK + (WING_TIP_X - WING_ROOT_BACK) * share + WING_HOLLOW * math.sin(share * math.pi) ** 1.2 - hook
    trail = min(trail, lead)
    height = WING_ARCH * math.sin(share * math.pi) - WING_DROOP * share * share
    thickness = WING_TIP_THICKNESS + (WING_ROOT_THICKNESS - WING_TIP_THICKNESS) * (1.0 - share) ** 1.5
    # The tip runs out into an edge
    thickness *= min(1.0, (lead - trail) / 0.3 + 0.15)
    return out, lead, trail, height, thickness


def wing_point(span_share, chord_share, side, raised=0.0):
    """A point on the top (side 1) or the underside (-1) of the wing, raised off the surface by a little for a mark."""
    out, lead, trail, height, thickness = wing_row(span_share)
    # A lens whose thickest spot lies in the front third, like the wing of a plane
    bulge = math.sin(chord_share ** 0.75 * math.pi) ** 0.6
    lift = thickness * 0.5 * bulge * (1.0 if side > 0 else -WING_BELLY) + raised * side
    return G(lead + (trail - lead) * chord_share, height + lift, out)


def is_pale(row, column):
    """Whether a face of the top is pale like the belly: the tip of the wing."""
    return row >= round(WING_PALE_TIP * WING_ROWS)


def wing_surface(scene, name, side, material_name, keep):
    """One side of the wing (side 1 the top, -1 the underside), only the faces keep(row, column) wants."""
    bm = bmesh.new()
    grid = [[bm.verts.new(wing_point(row / WING_ROWS, column / WING_COLUMNS, side)) for column in range(WING_COLUMNS + 1)] for row in range(WING_ROWS + 1)]
    for row in range(WING_ROWS):
        for column in range(WING_COLUMNS):
            if not keep(row, column):
                continue
            corners = [grid[row][column], grid[row][column + 1], grid[row + 1][column + 1], grid[row + 1][column]]
            bm.faces.new(corners if side > 0 else corners[::-1])
    bmesh.ops.delete(bm, geom=[vertex for vertex in bm.verts if not vertex.link_faces], context='VERTS')
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    return finish_surface(scene, name, bm, material_name)


def finish_surface(scene, name, bm, material_name):
    """Turns the bmesh of a surface into a smooth mesh object."""
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    surface = link_mesh(scene, name, mesh, material_name)
    for polygon in surface.data.polygons:
        polygon.use_smooth = True
    return surface


def shoulder_mark(scene, name):
    """
    The pale mark on the shoulder of a manta: a drop lying on the top of the wing, blunt
    next to the body, running out and back to a point. A thin sheet just above the skin.
    """
    STEPS, ACROSS = 16, 4
    start, end = WING_MARK_SPAN
    bm = bmesh.new()
    lines = []
    for step in range(STEPS + 1):
        share = step / STEPS
        span_share = start + (end - start) * share
        middle = WING_MARK_CHORD + WING_MARK_SLANT * share * share
        half = WING_MARK_WIDTH * math.sin(share ** 0.55 * math.pi) ** 0.8
        lines.append([bm.verts.new(wing_point(span_share, middle + half * (2.0 * across / ACROSS - 1.0), 1, WING_MARK_RAISED)) for across in range(ACROSS + 1)])
    for line, following in zip(lines, lines[1:]):
        for across in range(ACROSS):
            bm.faces.new((line[across], line[across + 1], following[across + 1], following[across]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    return finish_surface(scene, name, bm, 'Skin')


def bind_by_span(armature, meshes, bones):
    """
    Parents the meshes to the rig, every vertex weighted by its place along the span
    (out of the flank): bones is a list of (name, from, to). Inside a bone the vertex
    follows it alone, around the border to the next one both share it. The distance
    weights of helpers.bind do not fit a wing whose chord is longer than its bones.
    """
    BLEND = 0.22
    for mesh_obj in meshes:
        groups = {name: mesh_obj.vertex_groups.new(name=name) for name, _, _ in bones}
        for vertex in mesh_obj.data.vertices:
            out = vertex.co.dot(AXIS_OUT)
            weights = {}
            for index, (name, start, end) in enumerate(bones):
                is_first, is_last = 0 == index, len(bones) - 1 == index
                rise = 1.0 if is_first else min(1.0, max(0.0, (out - start + BLEND) / (2 * BLEND)))
                fall = 1.0 if is_last else min(1.0, max(0.0, (end + BLEND - out) / (2 * BLEND)))
                weights[name] = rise * fall
            total = sum(weights.values())
            for name, weight in weights.items():
                if weight > 0.0:
                    groups[name].add([vertex.index], weight / total, 'REPLACE')
        mesh_obj.parent = armature
        mesh_obj.modifiers.new('Armature', 'ARMATURE').object = armature
    join_by_material(meshes)


def build_mantawing(scene):
    """
    The wing of a manta ray (FlankPair): origin on the skin of the right flank, the wing
    reaches out (+Z), 2.6 body heights at size 1. A swept triangle: the root is as long as
    the whole body and almost as thick, so it melts into it; the leading edge runs back
    to a pointed, hooked tip, the trailing edge is hollow. The top takes the color of the
    painted back (BackSkin), the underside and the tip the clay of the body (Skin).
    It beats slowly up and down, the tip trailing behind ("Beat", 2.5 s).
    """
    meshes = [
        wing_surface(scene, 'Top', 1, 'BackSkin', lambda row, column: not is_pale(row, column)),
        wing_surface(scene, 'TopPale', 1, 'Skin', is_pale),
        shoulder_mark(scene, 'Mark'),
        wing_surface(scene, 'Under', -1, 'Skin', lambda row, column: True),
    ]

    def spot(out):
        share = (out + WING_SINK) / (WING_SPAN + WING_SINK)
        _, lead, trail, height, _ = wing_row(share)
        return G(lead + (trail - lead) * 0.4, height, out)

    bones = [(f'Wing{i}', start, end) for i, (start, end) in enumerate(WING_BONES)]
    armature = rig(scene, 'Rig', [(name, spot(start), spot(end), f'Wing{i - 1}' if i > 0 else None) for i, (name, start, end) in enumerate(bones)])
    bind_by_span(armature, meshes, bones)
    animate(armature, 'Beat', round(FPS * WING_BEAT_SECONDS), {
        f'Wing{i}': (AXIS_FORWARD, lambda share, phase=-i * WING_BEAT_LAG, degrees=WING_BEAT[i]: degrees * wave(share, phase)) for i in range(len(bones))
    })


def build_whiptail(scene):
    """
    The whip of a ray (Tail): root at the origin on the tail tip of the body, reaching
    back (-X), 2.3 units long. As thick as the end of the body at its root, then a thin
    cord that runs out to nothing. A tiny back fin stands on the root, like on a manta.
    All in the color of the painted back. It sways a little ("Sway", 3 s).
    """
    whip = tube(scene, 'Whip', [G(x, 0, 0) for x in WHIP_XS], WHIP_RADII, [(i, i + 1) for i in range(len(WHIP_XS) - 1)], 'BackSkin', max_pieces=6)
    fin = tube(scene, 'Fin', [G(0.14, 0.06, 0), G(0.0, 0.2, 0), G(-0.14, 0.28, 0)], (0.08, 0.05, 0.012), [(0, 1), (1, 2)], 'BackSkin')
    for vertex in fin.data.vertices:
        vertex.co.y *= 0.45

    count = len(WHIP_SWAY)
    ends = [WHIP_XS[0] + (-WHIP_LENGTH - WHIP_XS[0]) * i / count for i in range(count + 1)]
    armature = rig(scene, 'Rig', [(f'Whip{i}', G(ends[i], 0, 0), G(ends[i + 1], 0, 0), f'Whip{i - 1}' if i > 0 else None) for i in range(count)])
    bind(armature, [whip, fin], rigid={'Fin': 'Whip0'})
    animate(armature, 'Sway', FPS * 3, {
        f'Whip{i}': (AXIS_UP, lambda share, phase=-i * 0.14, degrees=WHIP_SWAY[i]: degrees * wave(share, phase)) for i in range(count)
    })


# ===========================================================================
# ============================   LOBSTER PARTS   ============================
# ===========================================================================
#
#   fan_plate       One hard plate of the fan: a flat paddle with a keel, turned around its root.
#   build_tailfan   The fan at the end of a lobster tail (Tail): five plates, they spread and close.
#

def fan_plate(scene, name, root, angle, length, width):
    """
    One plate of the tail fan with its root at a point (Blender vector), reaching back
    (-X) turned by angle degrees towards +Z (Godot, out of the right flank). A flat
    paddle that gets wider towards its round end and hangs down a little, a keel along
    its middle. Returns its meshes (plate, keel).
    """
    plate = ellipsoid(scene, name, G(-length / 2, 0, 0), (length / 2, width / 2, FAN_THICKNESS), 'Skin', subdivisions=3)
    for vertex in plate.data.vertices:
        share = min(1.0, max(0.0, -vertex.co.x / length))
        vertex.co.y *= FAN_NARROW_ROOT + (1.0 - FAN_NARROW_ROOT) * share ** 0.7
        vertex.co.z -= FAN_DROOP * length * share * share
    spine = [G(-length * share, FAN_THICKNESS * 0.6 - FAN_DROOP * length * share * share, 0) for share in (0.0, 0.3, 0.6, 0.88)]
    keel = tube(scene, 'Keel' + name, spine, (0.03, 0.028, 0.022, 0.012), [(0, 1), (1, 2), (2, 3)], 'ShellRim', subdivisions=1, max_pieces=3)
    # Godot +Z is Blender -Y: a turn about Blender Z by +angle swings -X towards -Y
    turn = Matrix.Translation(root) @ Matrix.Rotation(math.radians(angle), 4, 'Z')
    for mesh in (plate, keel):
        mesh.data.transform(turn)
    return [plate, keel]


def build_tailfan(scene):
    """
    The fan at the end of a lobster tail (Tail): root at the origin on the tail tip of
    the body, reaching back (-X). The last ring of the tail armor closes the body, behind
    it five hard plates lie flat: the middle one straight back, two on each side fanned
    out. Body clay with darker keels. The side plates spread and close slowly ("Fan", 3 s).
    """
    ring = ellipsoid(scene, 'Ring', G(0.12, 0, 0), (0.24, 0.26, 0.17), 'Skin', subdivisions=3)
    meshes = [ring] + fan_plate(scene, 'Middle', G(-0.02, 0, 0), 0.0, FAN_MIDDLE[0], FAN_MIDDLE[1])
    bones = [('Root', G(0.3, 0, 0), G(0, 0, 0), None), ('Middle', G(0, 0, 0), G(-FAN_MIDDLE[0], 0, 0), 'Root')]
    rigid = {'Ring': 'Root', 'Middle': 'Middle', 'KeelMiddle': 'Middle'}
    curves = {}
    for side in (-1, 1):
        for index, (out, angle, length, width, swing) in enumerate(FAN_SIDES):
            name = f'Side{index}{"R" if side > 0 else "L"}'
            root = G(0.0, -0.01 * (index + 1), side * out)
            meshes += fan_plate(scene, name, root, side * angle, length, width)
            reach = Matrix.Rotation(math.radians(side * angle), 4, 'Z') @ G(-length, 0, 0)
            bones.append((name, root, root + reach, 'Root'))
            rigid[name] = name
            rigid['Keel' + name] = name
            curves[name] = (AXIS_UP, lambda share, degrees=side * swing: degrees * wave(share))
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes, rigid=rigid)
    animate(armature, 'Fan', FPS * 3, curves)
