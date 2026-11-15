# NOTE:
#   New parts of the jelly family of templates: the bell of a jellyfish and the frilly
#   oral arms trailing under it. The crown of the bell points along +X (the nose) and
#   its opening looks back, the arms trail along -X: the template stands the animal
#   upright (pitch 90), so the bell ends on top and the arms hang down.
#   Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   ./tools/blender/Show-Model.ps1 -Module new_jelly -Call 'build_jellybell(scene)' -Name jellybell
#   ./tools/blender/Show-Model.ps1 -Module new_jelly -Call 'build_oralarms(scene)' -Name oralarms

#
#   Chapters:
#     JELLY PARTS   The bell and the oral arms of a jellyfish.
#

import math

import bmesh
import bpy
from mathutils import Quaternion

from models.helpers import FPS, G, animate, apply_modifiers, bind, link_mesh, rig, tube, wave

BELL_CROWN_X = 1.0                   # the crown, the leading point of the bell
BELL_RIM_X = -0.2                    # where the dome is widest
BELL_RADIUS = 1.5                    # at its widest, the body sphere has radius 1
BELL_END_ANGLE = 1.9                 # the profile runs from the crown (0) past the widest ring (pi / 2): the rim curls in
BELL_ROUNDNESS = 0.9                 # exponent of the profile: 1 = half sphere, lower = flatter crown with steep sides
BELL_WALL = 0.07                     # distance of the lining from the dome
BELL_RINGS = 14
BELL_SEGMENTS = 48
BELL_LAPPETS = 16                    # scallops of the rim, one stripe on each
BELL_LAPPET_DEPTH = 0.2              # how far a lappet hangs back beyond the notches
BELL_LAPPET_PINCH = 0.07             # the notches pull in by this share of the radius
BELL_LAPPET_START = 0.72             # share of the profile where the scallops begin
BELL_FLOOR_RISE = 0.5                # the underside arches into the dome by this much
BELL_STRIPE_START = 0.14             # share of the profile where a stripe begins
BELL_STRIPE_END = 0.9
BELL_STRIPE_WIDTH = 0.085            # half width of a stripe at its widest, as angle around the axis
BELL_STRIPE_STEPS = 10
BELL_STRIPE_LIFT = 0.012             # the paint lies this far above the dome
BELL_LIP_RADIUS = 0.05
BELL_PULSE_BONES = 8
BELL_PULSE_DEGREES = 9.0             # the rim closes and opens by this angle
BELL_PULSE_SECONDS = 2

ARMS_COUNT = 4
ARMS_LENGTH = 2.4                    # reach of the longest arm behind its root
ARMS_SPREAD = 0.3                    # distance of the arm tips from the axis
ARMS_WIDTH = 0.24                    # half width of a ribbon at its widest
ARMS_COLUMNS = 96
ARMS_ROWS = 4                        # across the ribbon, even: the middle row is the cord
ARMS_RUFFLES = 8                     # waves of the frilly edge along an arm
ARMS_RUFFLE_HEIGHT = 0.13
ARMS_THICKNESS = 0.035
ARMS_BONES = 3
ARMS_SWAY_SECONDS = 3


# ===========================================================================
# =============================   JELLY PARTS   =============================
# ===========================================================================
#
#   bell_point        A point on the dome of the bell.
#   build_jellybell   The bell: a see-through dome with stripes and a scalloped rim, it pulses.
#   pulse             The stroke of the bell over one loop.
#   arm_center        A point on the middle line of an oral arm.
#   build_oralarms    The oral arms: four ruffled ribbons trailing straight back, they sway.
#

def bell_point(share, angle, lift=0.0):
    """
    A point on the dome: share 0 at the crown .. 1 at the edge of the rim, angle around
    the axis (0 = up), lift pushes it off the surface. The rim is cut into lappets.
    """
    along = share * BELL_END_ANGLE
    radius = BELL_RADIUS * math.sin(along) ** BELL_ROUNDNESS
    x = BELL_RIM_X + (BELL_CROWN_X - BELL_RIM_X) * math.cos(along) if along < math.pi / 2 \
        else BELL_RIM_X + BELL_RADIUS * 0.75 * math.cos(along)
    # Lappets: round tongues between notches, growing over the last part of the profile
    edge = max(0.0, (share - BELL_LAPPET_START) / (1.0 - BELL_LAPPET_START))
    tongue = abs(math.cos(angle * BELL_LAPPETS / 2)) ** 0.5
    x -= BELL_LAPPET_DEPTH * edge ** 2 * tongue
    radius *= 1.0 - BELL_LAPPET_PINCH * edge * (1.0 - tongue)
    radius += lift
    return G(x, radius * math.cos(angle), radius * math.sin(angle))


def build_jellybell(scene):
    """
    The bell of a jellyfish (model space Hug: around a unit sphere, the spine along x):
    a see-through dome wider than the body, the crown leading, sixteen painted stripes running
    from the crown down the lappets of a scalloped rim with a soft lip, and an arched
    underside. The rim closes and opens in two seconds ("Pulse").
    """
    bm = bmesh.new()
    # Lining: a second wall just inside the dome. Its faces come first, so the game draws it
    # before the dome and the two see-through walls add up to a dense, glassy jelly
    lining = [[bm.verts.new(bell_point(ring / BELL_RINGS, segment / BELL_SEGMENTS * math.tau, lift=-BELL_WALL)) for segment in range(BELL_SEGMENTS)]
              for ring in range(1, BELL_RINGS)]
    lining_crown = bm.verts.new(G(BELL_CROWN_X - BELL_WALL, 0, 0))
    for segment in range(BELL_SEGMENTS):
        following = (segment + 1) % BELL_SEGMENTS
        bm.faces.new((lining_crown, lining[0][segment], lining[0][following]))
        for inner, outer in zip(lining, lining[1:]):
            bm.faces.new((inner[segment], outer[segment], outer[following], inner[following]))

    # Dome: rings from the crown to the rim
    rings = [[bm.verts.new(bell_point(ring / BELL_RINGS, segment / BELL_SEGMENTS * math.tau)) for segment in range(BELL_SEGMENTS)]
             for ring in range(1, BELL_RINGS + 1)]
    crown = bm.verts.new(G(BELL_CROWN_X, 0, 0))
    # Underside: from the rim back in, arching into the dome
    rim_x = BELL_RIM_X + BELL_RADIUS * 0.75 * math.cos(BELL_END_ANGLE)
    rim_radius = BELL_RADIUS * math.sin(BELL_END_ANGLE) ** BELL_ROUNDNESS
    for share in (0.8, 0.55, 0.3):
        x = rim_x + BELL_FLOOR_RISE * (1.0 - share ** 2)
        rings.append([bm.verts.new(G(x, rim_radius * share * math.cos(segment / BELL_SEGMENTS * math.tau), rim_radius * share * math.sin(segment / BELL_SEGMENTS * math.tau)))
                      for segment in range(BELL_SEGMENTS)])
    floor = bm.verts.new(G(rim_x + BELL_FLOOR_RISE, 0, 0))
    for segment in range(BELL_SEGMENTS):
        following = (segment + 1) % BELL_SEGMENTS
        bm.faces.new((crown, rings[0][segment], rings[0][following]))
        for inner, outer in zip(rings, rings[1:]):
            bm.faces.new((inner[segment], outer[segment], outer[following], inner[following]))
        bm.faces.new((rings[-1][segment], floor, rings[-1][following]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Dome')
    bm.to_mesh(mesh)
    bm.free()
    dome = link_mesh(scene, 'Dome', mesh, 'Fin')
    for polygon in dome.data.polygons:
        polygon.use_smooth = True
    meshes = [dome]

    # Stripes: painted wedges lying on the dome, from near the crown down the middle of every lappet
    bm = bmesh.new()
    for stripe in range(BELL_LAPPETS):
        angle = stripe / BELL_LAPPETS * math.tau
        rows = []
        for step in range(BELL_STRIPE_STEPS + 1):
            share = BELL_STRIPE_START + (BELL_STRIPE_END - BELL_STRIPE_START) * step / BELL_STRIPE_STEPS
            # A wedge: a point at both ends, widest two thirds down
            half = BELL_STRIPE_WIDTH * math.sin(math.pi * (step / BELL_STRIPE_STEPS) ** 1.6) ** 0.6
            rows.append([bm.verts.new(bell_point(share, angle + across * half, lift=BELL_STRIPE_LIFT)) for across in (-1.0, 0.0, 1.0)])
        for near, far in zip(rows, rows[1:]):
            for column in range(2):
                bm.faces.new((near[column], far[column], far[column + 1], near[column + 1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Stripes')
    bm.to_mesh(mesh)
    bm.free()
    stripes = link_mesh(scene, 'Stripes', mesh, 'Skin')
    for polygon in stripes.data.polygons:
        polygon.use_smooth = True
    meshes.append(stripes)

    # Lip: a soft roll along the scalloped edge
    count = BELL_LAPPETS * 4
    points = [bell_point(1.0, step / count * math.tau) for step in range(count)]
    meshes.append(tube(scene, 'Lip', points, [BELL_LIP_RADIUS] * count, [(i, (i + 1) % count) for i in range(count)], 'BackSkin', subdivisions=1, max_pieces=1))

    # Rig: the crown holds still, a ring of bones swings the rim in and out
    bones = [('Crown', G(BELL_RIM_X, 0, 0), G(BELL_CROWN_X, 0, 0), None)]
    curves = {}
    for bone in range(BELL_PULSE_BONES):
        angle = bone / BELL_PULSE_BONES * math.tau
        bones.append((f'Rim{bone}', bell_point(0.45, angle), bell_point(1.0, angle), 'Crown'))
        # Around the tangent of the rim there: a positive turn swings the edge towards the axis
        tangent = G(0, -math.sin(angle), math.cos(angle))
        curves[f'Rim{bone}'] = (tangent, lambda share: BELL_PULSE_DEGREES * pulse(share))
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes)
    animate(armature, 'Pulse', FPS * BELL_PULSE_SECONDS, curves)


def pulse(share):
    """The stroke of a bell over one loop, -0.4 (flared) .. 1 (closed): a quick squeeze, a slow opening."""
    squeeze = math.sin(math.pi * share ** 0.6) ** 2
    return squeeze * 1.4 - 0.4


def arm_center(arm, share):
    """
    Middle line of an oral arm, share 0 at the root .. 1 at the tip: straight back along
    -X (down on an upright jellyfish), the arms in a ring around that axis, snaking.
    """
    around = (arm + 0.5) / ARMS_COUNT * math.tau
    length = ARMS_LENGTH * (1.0 - 0.12 * (arm % 2))
    phase = arm * 1.9
    spread = ARMS_SPREAD * share ** 0.7 + 0.07 * share * math.sin(share * math.tau * 1.3 + phase)
    swirl = around + 0.35 * share * math.sin(share * math.tau * 1.1 + phase * 1.7)
    return G(-length * share, spread * math.cos(swirl), spread * math.sin(swirl))


def build_oralarms(scene):
    """
    The oral arms of a jellyfish (model space Belly: root at the origin under the belly):
    four frilly ribbons trailing straight back along -X in a ring around that axis (an
    upright jellyfish, pitch 90, lets them hang down), each a cord with ruffled see-through
    edges that taper to the tip, twisted so that every view shows frills.
    They sway in three seconds ("Sway").
    """
    meshes = []
    bones = []
    curves = {}
    for arm in range(ARMS_COUNT):
        # Ribbon: a strip around the middle line, twisting, its edges ruffled
        bm = bmesh.new()
        grid = []
        for column in range(ARMS_COLUMNS + 1):
            share = column / ARMS_COLUMNS
            center = arm_center(arm, share)
            tangent = (arm_center(arm, min(1.0, share + 0.01)) - arm_center(arm, max(0.0, share - 0.01))).normalized()
            reference = G(0, 1, 0) if 0 == arm % 2 else G(0, 0, 1)
            side = (reference - tangent * reference.dot(tangent)).normalized()
            side = Quaternion(tangent, 1.6 * share + arm * 0.5) @ side
            normal = tangent.cross(side)
            width = ARMS_WIDTH * min(1.0, 0.3 + share * 5.0) * (1.0 - share) ** 0.45
            row_points = []
            for row in range(ARMS_ROWS + 1):
                across = row / ARMS_ROWS * 2.0 - 1.0
                ruffle = ARMS_RUFFLE_HEIGHT * abs(across) ** 1.5 * min(1.0, share * 6.0) * (1.0 - share) ** 0.3
                ruffle *= math.sin(share * math.tau * ARMS_RUFFLES + arm + (math.pi if across < 0 else 0.0))
                row_points.append(bm.verts.new(center + side * across * width + normal * ruffle))
            grid.append(row_points)
        for column in range(ARMS_COLUMNS):
            for row in range(ARMS_ROWS):
                bm.faces.new([grid[column][row], grid[column + 1][row], grid[column + 1][row + 1], grid[column][row + 1]])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new(f'Frill{arm}')
        bm.to_mesh(mesh)
        bm.free()
        frill = link_mesh(scene, f'Frill{arm}', mesh, 'Feather')
        solidify = frill.modifiers.new('Thick', 'SOLIDIFY')
        solidify.thickness = ARMS_THICKNESS
        solidify.offset = 0.0
        apply_modifiers(frill)
        for polygon in frill.data.polygons:
            polygon.use_smooth = True
        meshes.append(frill)

        # Cord: the fleshy middle of the arm
        shares = [i / 7 for i in range(8)]
        points = [arm_center(arm, share * 0.97) for share in shares]
        radii = [0.085 * (1.0 - share) ** 0.7 + 0.018 for share in shares]
        meshes.append(tube(scene, f'Cord{arm}', points, radii, [(i, i + 1) for i in range(len(points) - 1)], 'FinRay', subdivisions=1, max_pieces=3))

        # Bones: a chain along the arm, swaying more towards the tip
        for bone in range(ARMS_BONES):
            name = f'Arm{arm}_{bone}'
            bones.append((name, arm_center(arm, bone / ARMS_BONES), arm_center(arm, (bone + 1) / ARMS_BONES), f'Arm{arm}_{bone - 1}' if bone > 0 else None))
            curves[name] = (G(0, 0, 1) if 0 == arm % 2 else G(0, 1, 0),
                            lambda share, bone=bone, arm=arm: (4.0 + 3.0 * bone) * wave(share, arm * 0.23 - bone * 0.18))

    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes)
    animate(armature, 'Sway', FPS * ARMS_SWAY_SECONDS, curves)
