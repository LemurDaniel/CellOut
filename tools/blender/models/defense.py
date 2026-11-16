# NOTE:
#   Defense of the organism stage (assets/models/creature/): the spines of a puffer fish
#   and the shell of a turtle. Registered in build.models.py.
#   Model space: both hug the body, so they are built around a unit sphere at the origin
#   (the spine runs along x). The game stretches y to the height and z to the width of
#   the body there and x to the stretch of spine they cover.

#
#   Chapters:
#     DEFENSE   Puffer spines and turtle shell.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import G, cone, ellipsoid, link_mesh, tube

SPINE_COUNT = 170                    # points spread over the whole ball, face and tail are left out
SPINE_STRETCH = 3.0                  # the game squeezes x by about this much against y and z
SPINE_BALL_LENGTH = 1.15             # half length of the ball in body radii (round space)
SPINE_BARREL_FRONT = 2.0             # 2 an egg, higher the fuller the ball stays towards its end: the head is round,
SPINE_BARREL_BACK = 3.6              # the body behind it stays thick up to the tail
SPINE_BULGE = 1.05                   # the body is a little thicker behind the spot the game measures
SPINE_LENGTH = 0.36                 # a spine above the skin, in body radii
SPINE_ROOT = 0.25                    # how deep a spine starts below the skin, it never floats on a slimmer body
SPINE_RADIUS = 0.075
MOUND_RADIUS = 0.09 
SPINE_FACE = 0.82                    # no spines in front of this share of the ball: the mouth stays free
SPINE_CHEEK = 0.45                   # in front of this share the cheeks stay free: room for the eyes
SPINE_TAIL = -0.9                    # none behind this share: the tail stalk


# ===========================================================================
# ===============================   DEFENSE   ===============================
# ===========================================================================
#
#   build_spines   A ball of spines all around the body, each on a little mound, face and tail stay bare.
#   build_shell    A dome around the body with knobbly scutes and a rim along its edge.
#

def build_spines(scene):
    """
    The game squeezes x much harder than y and z (a third of the spine to each side of
    the spot), so the ball is laid out in a round reference space first and every piece
    is stretched along x by SPINE_STRETCH afterwards: in the game the spines stand
    upright on the body again and stay round.
    """
    stretch = Matrix.Diagonal((SPINE_STRETCH, 1.0, 1.0, 1.0))
    golden = math.pi * (3.0 - math.sqrt(5.0))
    kept = 0
    for i in range(SPINE_COUNT):
        # Evenly spread directions (golden angle spiral around the x axis, so both flanks match)
        forward = 1.0 - 2.0 * (i + 0.5) / SPINE_COUNT
        up, out = math.cos(i * golden), math.sin(i * golden)
        is_cheek = forward > SPINE_CHEEK and abs(out) > 0.55 and -0.3 < up < 0.75
        if forward > SPINE_FACE or forward < SPINE_TAIL or is_cheek:
            continue

        # Foot on the long ball: a body is a barrel with round ends, not an egg, so the
        # ball keeps its girth for long towards the tail (superellipse). The spine stands along its normal
        barrel = SPINE_BARREL_FRONT if forward > 0.0 else SPINE_BARREL_BACK
        girth = (1.0 - abs(forward) ** barrel) ** (1.0 / barrel)
        slope = abs(forward) ** (barrel - 1.0) * girth ** (1.0 - barrel) / SPINE_BALL_LENGTH
        girth *= SPINE_BULGE if forward < 0.0 else 1.0
        foot = Vector((forward * SPINE_BALL_LENGTH, up * girth, out * girth))
        normal = Vector((math.copysign(slope, forward), up, out)).normalized()
        length = SPINE_LENGTH * (0.85 + 0.3 * ((i * 7) % 5) / 4.0) * (0.7 if up < -0.5 else 1.0)
        base, tip = foot - normal * SPINE_ROOT, foot + normal * length
        spine = cone(scene, f'Spine{kept}', G(base.x, base.y, base.z), G(tip.x, tip.y, tip.z), SPINE_RADIUS, 'Claw')
        spine.data.transform(stretch)

        # Every spine grows out of a little mound: dark on the back (the spots of a puffer), pale below
        seat = foot - normal * MOUND_RADIUS * 0.35
        ellipsoid(scene, f'Mound{kept}', G(seat.x * SPINE_STRETCH, seat.y, seat.z),
                  (MOUND_RADIUS * SPINE_STRETCH, MOUND_RADIUS, MOUND_RADIUS), 'Shell' if up > -0.15 else 'FinRay', subdivisions=2)
        kept += 1


def build_shell(scene):
    """
    The carapace of a sea turtle: a low heart-shaped dome much wider than the body (the
    body fills the unit sphere inside), covered with scutes, closed by a pale belly plate.

    The shell is laid out on a plan (u, v): u runs from the tail (-1) to the neck (1),
    v from the left edge (-1) over the keel (0) to the right edge (1).

        v = 1   +---------------------------------+   rim: small marginal plates
                |  costal  |  costal  |  costal   |   a row of four on each side
                +---.  .--------.  .--------.  .--+
        v = 0   tail  ><  vertebral  ><  ...   neck   five hexagons along the keel
                +---'  '--------'  '--------'  '--+
                |  costal  |  costal  |  costal   |
        v = -1  +---------------------------------+

    > Dome:      the dark ground between the plates (Shell), it shows as grooves.
    > Scutes:    every plate is a cushion raised out of the dome (ShellRim).
    > Plastron:  the belly plate (FinRay), a brim under the rim, then a flat pan.
    """
    HALF_WIDTH = 1.62                 # widest half width of the carapace, the body inside is about 0.86 wide
    TOP_Y, EDGE_Y, BELLY_Y = 1.02, -0.16, -0.93
    KEEL, COSTAL_END, MARGIN_START = 0.24, 0.76, 0.8       # v of the row borders
    ENDS = 0.88                       # |u| where the end plates of the rim start
    VERTEBRAL_CUTS = (-0.86, -0.52, -0.18, 0.16, 0.5, 0.84)
    GRID = 28

    def half_width(u):
        """Outline of the carapace: widest in front of the middle, blunt at the neck, a point at the tail."""
        if u >= 0.0:
            return HALF_WIDTH * math.sqrt(max(0.0, 1.0 - (u / 1.035) ** 2)) * (1.0 + 0.2 * u)
        return HALF_WIDTH * max(0.0, 1.0 - (u / 1.012) ** 2) ** 0.8 * (1.0 + 0.2 * u)

    def carapace(u, v):
        """Point of the dome over a plan point."""
        height = max(0.0, 1.0 - u * u) ** 0.6 * max(0.0, 1.0 - v * v) ** 0.75
        return G(u, EDGE_Y + (TOP_Y - EDGE_Y) * height, v * half_width(u))

    def plastron(u, v):
        """Point of the belly plate under a plan point: flat brim at the edge, then down to a flat pan."""
        inside = math.sqrt(max(0.0, (1.0 - u * u) * (1.0 - v * v)))
        drop = min(1.0, max(0.0, (inside - 0.15) / 0.68))
        return G(u, EDGE_Y + (BELLY_Y - EDGE_Y) * drop * drop * (3.0 - 2.0 * drop), v * half_width(u))

    def normal(u, v):
        """Normal of the dome, pointing out of the shell."""
        u, v = max(-0.985, min(0.985, u)), max(-0.985, min(0.985, v))
        out = (carapace(u, v + 0.01) - carapace(u, v - 0.01)).cross(carapace(u + 0.01, v) - carapace(u - 0.01, v))
        out = out.normalized()
        return out if out.z >= 0.0 else -out

    def finish(bm, name, material_name, towards_up):
        """Turns every face out of the shell (up for the back, down for the belly) and links the mesh."""
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        lean = sum(face.normal.z * face.calc_area() for face in bm.faces)
        if (lean > 0.0) != towards_up:
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        obj = link_mesh(scene, name, mesh, material_name)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        return obj

    def sheet(name, surface, material_name, towards_up):
        """A surface over the whole plan, denser towards the edges where it curves most."""
        bm = bmesh.new()
        steps = [math.sin((i / GRID - 0.5) * math.pi) for i in range(GRID + 1)]
        grid = [[bm.verts.new(surface(u, v)) for v in steps] for u in steps]
        for i in range(GRID):
            for j in range(GRID):
                bm.faces.new([grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]])
        return finish(bm, name, material_name, towards_up)

    def scute(name, corners, lift=0.05, gap=0.93):
        """
        One plate: its outline (plan points) shrinks a little, so the dome shows as a groove
        around it, then rises in rings to a flat cushion.
        """
        center_u = sum(u for u, _ in corners) / len(corners)
        center_v = sum(v for _, v in corners) / len(corners)
        outline = []
        for i, (u, v) in enumerate(corners):
            next_u, next_v = corners[(i + 1) % len(corners)]
            for piece in range(5):
                share = piece / 5
                outline.append((u + (next_u - u) * share, v + (next_v - v) * share))

        # Rings from the outline to the middle: (share of the outline, height above the dome)
        bm = bmesh.new()
        rings = []
        for scale, height in ((gap, -0.03), (gap * 0.9, lift * 0.7), (gap * 0.78, lift), (gap * 0.4, lift * 1.3)):
            ring = []
            for u, v in outline:
                at_u, at_v = center_u + (u - center_u) * scale, center_v + (v - center_v) * scale
                ring.append(bm.verts.new(carapace(at_u, at_v) + normal(at_u, at_v) * height))
            rings.append(ring)
        middle = bm.verts.new(carapace(center_u, center_v) + normal(center_u, center_v) * lift * 1.3)
        count = len(outline)
        for outer, inner in zip(rings, rings[1:]):
            for i in range(count):
                bm.faces.new([outer[i], outer[(i + 1) % count], inner[(i + 1) % count], inner[i]])
        for i in range(count):
            bm.faces.new([rings[-1][i], rings[-1][(i + 1) % count], middle])
        return finish(bm, name, 'ShellRim', towards_up=True)

    # Dome and belly plate meet along the rim
    sheet('Dome', carapace, 'Shell', towards_up=True)
    sheet('Plastron', plastron, 'FinRay', towards_up=False)

    # Vertebral scutes: hexagons along the keel, their side points reach between the costals
    mids = [(low + high) / 2 for low, high in zip(VERTEBRAL_CUTS, VERTEBRAL_CUTS[1:])]
    for i, (low, high) in enumerate(zip(VERTEBRAL_CUTS, VERTEBRAL_CUTS[1:])):
        scute(f'Vertebral{i}', [(low, -KEEL * 0.7), (mids[i], -KEEL * 1.3), (high, -KEEL * 0.7), (high, KEEL * 0.7), (mids[i], KEEL * 1.3), (low, KEEL * 0.7)])

    # Costal scutes: four on each side, from point to point of the vertebrals, the end ones reach the ends
    for side in (-1, 1):
        for i in range(len(mids) - 1):
            low = VERTEBRAL_CUTS[0] if 0 == i else mids[i]
            high = VERTEBRAL_CUTS[-1] if len(mids) - 2 == i else mids[i + 1]
            inner = [(mids[i], KEEL * 1.3), (VERTEBRAL_CUTS[i + 1], KEEL * 0.7), (mids[i + 1], KEEL * 1.3)]
            inner = ([(low, KEEL * 0.7)] if 0 == i else []) + inner + ([(high, KEEL * 0.7)] if len(mids) - 2 == i else [])
            corners = inner + [(high, COSTAL_END), (low, COSTAL_END)]
            scute(f'Costal{side}_{i}', [(u, v * side) for u, v in corners])

    # Marginal scutes: small plates all around the rim, eleven per side, three at the neck, two at the tail
    cuts = [-1.0 + i * 2.0 / 11 for i in range(12)]
    for side in (-1, 1):
        for i, (low, high) in enumerate(zip(cuts, cuts[1:])):
            scute(f'Marginal{side}_{i}', [(low, MARGIN_START * side), (high, MARGIN_START * side), (high, 0.995 * side), (low, 0.995 * side)], lift=0.04, gap=0.9)
    for name, low, high, count in (('Neck', ENDS, 0.995, 3), ('Tail', -0.995, -ENDS, 2)):
        for i in range(count):
            left, right = -MARGIN_START + i * 2 * MARGIN_START / count, -MARGIN_START + (i + 1) * 2 * MARGIN_START / count
            scute(f'{name}{i}', [(low, left), (high, left), (high, right), (low, right)], lift=0.04, gap=0.9)


def build_spinecrest(scene):
    """
    One element of the back crest (model space Row): a pointed plate standing on the back,
    its foot at the origin, +X forward, the tip up and leaning back a little. The game
    repeats it along the spine, one copy on each spot, so the row follows every bend.
    A flat blade (Shell) on a low oval pad in the skin (Skin).
    """
    import bmesh
    import bpy

    from models.helpers import G as _g, ellipsoid as _ellipsoid, link_mesh as _link_mesh

    _ellipsoid(scene, 'CrestPad', _g(0, 0.0, 0), (0.34, 0.1, 0.16), 'Skin', subdivisions=2)
    # The blade: a thin kite, thick at the foot, sharp at the tip
    length, height, thick, lean = 0.5, 0.62, 0.08, 0.16
    bm = bmesh.new()
    foot = [bm.verts.new(_g(x * length * 0.5, 0.02, z * thick * 0.5)) for x, z in ((1, 1), (1, -1), (-1, -1), (-1, 1))]
    belly = [bm.verts.new(_g(x * length * 0.42 - lean * 0.4, height * 0.45, z * thick * 0.3)) for x, z in ((1, 1), (1, -1), (-1, -1), (-1, 1))]
    tip = bm.verts.new(_g(-lean, height, 0))
    bm.faces.new(foot)
    for a, b, c, d in zip(foot, foot[1:] + foot[:1], belly[1:] + belly[:1], belly):
        bm.faces.new((a, b, c, d))
    for a, b in zip(belly, belly[1:] + belly[:1]):
        bm.faces.new((a, b, tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new('CrestBlade')
    bm.to_mesh(mesh)
    bm.free()
    _link_mesh(scene, 'CrestBlade', mesh, 'Shell')
