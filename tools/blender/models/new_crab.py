# NOTE:
#   New part of the crab template, not wired into the game yet: the wide flat carapace
#   of a rock crab. Model space Hug (around a unit sphere at the origin, the spine
#   runs along x, the game stretches y to the height and z to the width of the body).
#   The shell is wider than the body on purpose: its flanges hang over the hips.
#
#   ./tools/blender/Show-Model.ps1 -Module new_crab -Call 'build_carapace(scene)' -Name carapace

#
#   Chapters:
#     CRAB PARTS   The carapace of a crab.
#

import math

import bmesh
import bpy

from models.helpers import G, ellipsoid, link_mesh, tube

CARAPACE_HALF_LENGTH = 1.0           # nose and tail edge, the body sphere has radius 1
CARAPACE_HALF_WIDTH = 1.42           # at the middle, the front corners reach further out
CARAPACE_HEIGHT = 0.97               # top of the dome, just above the back
CARAPACE_RIM_Y = -0.12               # the edge hangs a little below the spine
CARAPACE_SQUARENESS = 0.72           # exponent of the outline: 1 = ellipse, lower = boxier
CARAPACE_FLARE = 0.16                # the front is wider than the rear by this share
CARAPACE_RINGS = 12
CARAPACE_SEGMENTS = 48


# ===========================================================================
# =============================   CRAB PARTS   ==============================
# ===========================================================================
#
#   carapace_edge    A point on the outline of the carapace at an angle.
#   build_carapace   The carapace: a flat wide dome with a rim, edge teeth and knobs.
#

def carapace_edge(angle):
    """Outline seen from above as (forward, out): a boxy oval, wider at the front, 0 = nose."""
    cos, sin = math.cos(angle), math.sin(angle)
    forward = math.copysign(abs(cos) ** CARAPACE_SQUARENESS, cos) * CARAPACE_HALF_LENGTH
    out = math.copysign(abs(sin) ** CARAPACE_SQUARENESS, sin) * CARAPACE_HALF_WIDTH
    return forward, out * (1.0 + CARAPACE_FLARE * forward)


def build_carapace(scene):
    """
    The carapace of a rock crab around the body: a low dome, wider than long and widest
    near the front, a rolled rim, three blunt teeth on each front corner, two eye
    notches bumps at the front edge and a handful of knobs on the back.
    """
    # Dome: rings from the center of the back out to the edge, then a lip turned in below
    bm = bmesh.new()
    rings = []
    profile = [(ring / CARAPACE_RINGS, None) for ring in range(1, CARAPACE_RINGS + 1)]
    profile += [(0.93, CARAPACE_RIM_Y - 0.1), (0.6, CARAPACE_RIM_Y - 0.06)]
    for share, lip_y in profile:
        # Flat on top, steep at the edge
        up = lip_y if lip_y is not None else CARAPACE_RIM_Y + (CARAPACE_HEIGHT - CARAPACE_RIM_Y) * (1.0 - share ** 2.3) ** 0.8
        ring = []
        for segment in range(CARAPACE_SEGMENTS):
            forward, out = carapace_edge(segment / CARAPACE_SEGMENTS * math.tau)
            ring.append(bm.verts.new(G(forward * share, up, out * share)))
        rings.append(ring)
    crown = bm.verts.new(G(0, CARAPACE_HEIGHT, 0))
    for segment in range(CARAPACE_SEGMENTS):
        following = (segment + 1) % CARAPACE_SEGMENTS
        bm.faces.new((crown, rings[0][segment], rings[0][following]))
        for inner, outer in zip(rings, rings[1:]):
            bm.faces.new((inner[segment], outer[segment], outer[following], inner[following]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new('Dome')
    bm.to_mesh(mesh)
    bm.free()
    dome = link_mesh(scene, 'Dome', mesh, 'ShellRim')
    for polygon in dome.data.polygons:
        polygon.use_smooth = True

    # Rim: a rolled edge all around
    points = [G(forward, CARAPACE_RIM_Y, out) for forward, out in (carapace_edge(step / 40 * math.tau) for step in range(41))]
    tube(scene, 'Rim', points, [0.085] * len(points), [(i, i + 1) for i in range(len(points) - 1)], 'Shell', subdivisions=1)

    # Teeth: three blunt points on each front corner, the biggest at the corner
    for side in (-1, 1):
        for i, (degrees, size) in enumerate(((36, 0.12), (52, 0.16), (70, 0.13))):
            forward, out = carapace_edge(math.radians(degrees) * side)
            base = G(forward * 0.97, CARAPACE_RIM_Y + 0.02, out * 0.97)
            tip = base + G(0.55, 0.1, side * 0.85).normalized() * size * 1.5
            tube(scene, f'Tooth{side}_{i}', [base, tip], (size, size * 0.3), [(0, 1)], 'Shell', subdivisions=1)

    # Brow: two bumps at the front edge, the eyes sit beside them
    for side in (-1, 1):
        ellipsoid(scene, f'Brow{side}', G(0.97, CARAPACE_RIM_Y + 0.06, side * 0.26), (0.13, 0.2, 0.13), 'Shell', subdivisions=2)

    # Knobs: the bumpy back of a crab, mirrored (forward, out, size)
    for i, (forward, out, size) in enumerate(((0.42, 0.34, 0.15), (0.2, 0.92, 0.13), (-0.42, 0.52, 0.14))):
        share = max(abs(forward) / CARAPACE_HALF_LENGTH, abs(out) / CARAPACE_HALF_WIDTH)
        up = CARAPACE_RIM_Y + (CARAPACE_HEIGHT - CARAPACE_RIM_Y) * (1.0 - share ** 2.3) ** 0.8
        for side in (-1, 1):
            ellipsoid(scene, f'Knob{side}_{i}', G(forward, up - size * 0.25, side * out), (size, size * 1.5, size * 0.55), 'Shell', subdivisions=2)
