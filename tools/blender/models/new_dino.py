# NOTE:
#   New parts of the dino family of templates (T. rex, triceratops): the thick tail of a
#   dinosaur, the snout details of a tyrannosaur, the three horns and the neck frill of
#   a triceratops. Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_dino -Call 'build_thicktail(scene)' -Name thicktail
#   ./tools/blender/Show-Model.ps1 -Module new_dino -Call 'build_neckfrill(scene)' -Name neckfrill

#
#   Chapters:
#     T. REX PARTS         The thick tail and the snout details.
#     TRICERATOPS PARTS    The horns and the neck frill.
#

import math

import bmesh
import bpy

from models.helpers import AXIS_UP, FPS, G, animate, bind, ellipsoid, link_mesh, rig, tube, wave

TAIL_XS = (0.35, 0.0, -0.35, -0.7, -1.05, -1.4, -1.7, -1.9)          # the core of the tail from inside the body to its tip
TAIL_RADII = (0.3, 0.3, 0.265, 0.215, 0.165, 0.115, 0.07, 0.03)
TAIL_LENGTH = 1.9
TAIL_TALLER = 0.12                   # the tail is this much taller than wide: muscle above and below the bones
TAIL_BANDS = (-0.12, -0.5, -0.88, -1.24, -1.56)                      # dark bands over the back of the tail (BackSkin)
TAIL_SWAY = (0.0, 3.0, 5.0, 7.0)     # degrees per bone of the sway

SNOUT_NOSTRIL = (0.84, 0.8, 0.4)      # a nostril in snout space: forward, up, out of the middle
SNOUT_BUMPS = ((0.56, 1.5, 0.19), (0.66, 1.34, 0.18), (0.76, 1.1, 0.16), (0.85, 0.86, 0.14))    # rough knobs along the ridge of the nose: forward, up, size
SNOUT_RIDGE_OUT = 0.17               # the two rows of knobs sit this far off the middle

HORN_BROW_ROOT = (0.1, -0.2, 0.24)   # root of a brow horn, sunk into the head: forward, up, out of the middle
HORN_BROW_LENGTH = 1.25
HORN_BROW_RADIUS = 0.13
HORN_BROW_RISE = (62.0, 22.0)        # degrees above level at the root and at the tip: it rises, then bends forward
HORN_BROW_SPLAY = 0.14               # the tips stand this much further apart than the roots
HORN_STEPS = 7                       # points along a brow horn
HORN_NOSE = ((0.6, -1.0, 0.0), (0.7, -0.55, 0.0), (0.78, -0.22, 0.0))        # the short nose horn from deep in the snout to its tip
HORN_NOSE_RADII = (0.14, 0.115, 0.02)

FRILL_SEGMENTS = 36                  # columns of the fan from one flank over the back to the other
FRILL_RINGS = 7                      # rows from the neck out to the edge
FRILL_ARC = 104.0                    # the fan reaches this many degrees down each flank
FRILL_FADE = 40.0                    # over the last degrees it shrinks back to the neck
FRILL_INNER = 0.8                    # it leaves the neck at this share of the body ...
FRILL_OUTER = 2.5                    # ... and ends at this one above the back
FRILL_OUTER_FLANK = 2.0              # lower at the flanks: a shield that is taller than wide
FRILL_SWEEP = 0.62                   # the edge leans back by this much (model x, the game stretches x by 0.35 body lengths)
FRILL_THICKNESS = 0.03               # half thickness in model x
FRILL_STUDS = 13                     # bony points on the edge
FRILL_SCALLOP = 0.07                 # the rim bulges by this much at every stud
FRILL_FIELD = ((2, 12), (2, 6))      # the coloured field of each half: columns from the top and rows from the neck of the shield, so both sheets share their points

# ===========================================================================
# ============================   T. REX PARTS   =============================
# ===========================================================================
#
#   build_thicktail   The thick muscular tail of a dinosaur.
#   build_rexmaw      The rough ridge of the nose and the nostrils of a tyrannosaur.
#

def build_thicktail(scene):
    """
    The tail of a big dinosaur (Tail): root at the origin on the tail tip of the body,
    reaching back (-X), about two units long. A heavy cone as thick as the hip at its
    root that tapers evenly to a point, a little taller than wide, dark bands over its
    back. It sways slowly from side to side ("Sway", 3 s).
    """
    def share_at(x):
        return min(1.0, max(0.0, -x / TAIL_LENGTH))

    def radius_at(x):
        for (x0, r0), (x1, r1) in zip(zip(TAIL_XS, TAIL_RADII), zip(TAIL_XS[1:], TAIL_RADII[1:])):
            if x1 <= x <= x0:
                return r0 + (r1 - r0) * (x0 - x) / (x0 - x1)
        return TAIL_RADII[-1]

    core = tube(scene, 'Core', [G(x, 0, 0) for x in TAIL_XS], TAIL_RADII, [(i, i + 1) for i in range(len(TAIL_XS) - 1)])
    for vertex in core.data.vertices:
        vertex.co.z *= 1.0 + TAIL_TALLER
    meshes = [core]

    # Bands: dark saddles a little bigger than the core, lifted so the underside stays pale
    for i, x in enumerate(TAIL_BANDS):
        radius = radius_at(x)
        length = 0.12 * (1.0 - 0.45 * share_at(x))
        meshes.append(ellipsoid(scene, f'Band{i}', G(x, radius * 0.34, 0), (length, radius * 1.07, radius * (1.0 + TAIL_TALLER) * 0.84), 'BackSkin', subdivisions=3))

    count = len(TAIL_SWAY)
    ends = [TAIL_XS[0] + (-TAIL_LENGTH - TAIL_XS[0]) * i / count for i in range(count + 1)]
    armature = rig(scene, 'Rig', [(f'Tail{i}', G(ends[i], 0, 0), G(ends[i + 1], 0, 0), f'Tail{i - 1}' if i > 0 else None) for i in range(count)])
    bind(armature, meshes)
    animate(armature, 'Sway', FPS * 3, {
        f'Tail{i}': (AXIS_UP, lambda share, phase=-i * 0.14, degrees=TAIL_SWAY[i]: degrees * wave(share, phase)) for i in range(count)
    })


def build_rexmaw(scene):
    """
    The snout of a tyrannosaur (Snout: x 0 at the mouth corners .. 1 at the nose tip, z
    the width of the head). The deep jaw is carved into the head and the game stands
    the teeth on its lips (data/mouths.json), the model only adds what clay and teeth
    cannot: two rows of rough bony knobs along the ridge of the nose and the two dark
    nostrils at its tip.
    """
    for side in (-1, 1):
        for i, (forward, up, size) in enumerate(SNOUT_BUMPS):
            ellipsoid(scene, f'Knob{side}_{i}', G(forward, up, side * SNOUT_RIDGE_OUT), (size * 0.2, size, size), 'Skin', subdivisions=2)
        forward, up, out = SNOUT_NOSTRIL
        ellipsoid(scene, f'Nostril{side}', G(forward, up, side * out), (0.022, 0.085, 0.11), 'Dark', subdivisions=2)


# ===========================================================================
# =========================   TRICERATOPS PARTS   ===========================
# ===========================================================================
#
#   build_horns       Two long brow horns and a short nose horn.
#   frill_radius      How far the frill reaches at an angle.
#   frill_scallop     The bulge of the rim at an angle.
#   frill_point       A point of the frill dish.
#   frill_sheet       A piece of the dish as a closed sheet.
#   build_neckfrill   The broad bony shield behind the head.
#

def build_horns(scene):
    """
    The horns of a triceratops (Top: the origin on the back line, +X forward, +Y up):
    two long brow horns that rise out of the head and bend forward, their tips apart,
    and a short horn in front of and below them on the nose. Every root is sunk deep,
    so the horns grow out of heads of any shape; a collar of skin closes each brow root.
    """
    for side in (-1, 1):
        forward, up, out = HORN_BROW_ROOT
        point = G(forward, up, side * out)
        points = [point]
        for step in range(1, HORN_STEPS):
            share = step / (HORN_STEPS - 1)
            rise = math.radians(HORN_BROW_RISE[0] + (HORN_BROW_RISE[1] - HORN_BROW_RISE[0]) * share)
            stride = HORN_BROW_LENGTH / (HORN_STEPS - 1)
            point = point + G(math.cos(rise) * stride, math.sin(rise) * stride, side * HORN_BROW_SPLAY / (HORN_STEPS - 1))
            points.append(point)
        radii = [HORN_BROW_RADIUS * (1.0 - i / (HORN_STEPS - 1)) ** 0.8 + 0.012 for i in range(HORN_STEPS)]
        tube(scene, f'Brow{side}', points, radii, [(i, i + 1) for i in range(HORN_STEPS - 1)], 'Claw')
        ellipsoid(scene, f'Collar{side}', points[1] - G(0.02, 0.05, 0), (0.19, 0.17, 0.15), 'Skin', subdivisions=3)
    tube(scene, 'Nose', [G(*point) for point in HORN_NOSE], HORN_NOSE_RADII, [(0, 1), (1, 2)], 'Claw')


def frill_radius(degrees):
    """The reach of the frill at an angle from the top (degrees): tallest above the back, fading out down the flanks."""
    fade = min(1.0, (FRILL_ARC - abs(degrees)) / FRILL_FADE) ** 0.6
    reach = FRILL_OUTER_FLANK + (FRILL_OUTER - FRILL_OUTER_FLANK) * math.cos(math.radians(min(90.0, abs(degrees)))) ** 2
    return FRILL_INNER + (reach - FRILL_INNER) * fade


def frill_scallop(degrees):
    """The bulge of the rim at an angle: one bulge per stud."""
    return FRILL_SCALLOP * abs(math.cos(math.radians(degrees) * (FRILL_STUDS - 1) / 2 * 180.0 / FRILL_ARC))


def frill_point(degrees, share, shift):
    """A point of the dish: at an angle from the top, a share of the way from the neck to the rim, shifted along the body."""
    radius = FRILL_INNER + (frill_radius(degrees) - FRILL_INNER) * share + frill_scallop(degrees) * share ** 5
    lean = -FRILL_SWEEP * share ** 1.4 * (radius - FRILL_INNER) / (FRILL_OUTER - FRILL_INNER) / max(share, 1e-6)
    return G(lean + shift, radius * math.cos(math.radians(degrees)), radius * math.sin(math.radians(degrees)))


def frill_sheet(scene, name, angles, shares, columns, rows, thickness, shift, material_name):
    """A piece of the dish between two angles and two shares as a closed sheet with a round edge."""
    bm = bmesh.new()
    loops = []
    for column in range(columns + 1):
        degrees = angles[0] + (angles[1] - angles[0]) * column / columns
        # Thin at the first and last column: the sheet closes into an edge instead of a cut
        closing = math.sin(math.pi * column / columns) ** 0.35 if 0 < column < columns else 0.0
        loop = []
        for sign in (1, -1):
            for row in (range(rows + 1) if 1 == sign else range(rows, -1, -1)):
                share = shares[0] + (shares[1] - shares[0]) * row / rows
                edge = math.sin(math.pi * row / rows) ** 0.35 if 0 < row < rows else 0.0
                loop.append(bm.verts.new(frill_point(degrees, share, shift + sign * thickness * closing * edge)))
        loops.append(loop)
    for loop, following in zip(loops, loops[1:]):
        count = len(loop)
        for i in range(count):
            bm.faces.new((loop[i], loop[(i + 1) % count], following[(i + 1) % count], following[i]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    sheet = link_mesh(scene, name, mesh, material_name)
    for polygon in sheet.data.polygons:
        polygon.use_smooth = True
    return sheet


def build_neckfrill(scene):
    """
    The neck frill of a triceratops (Hug: around a unit sphere at the origin, the game
    stretches x along the spine, y to the height and z to the width of the body): a
    broad bony dish that grows out of the neck over the back and down both flanks, open
    below, leaning back over the shoulders. Its rim is scalloped with a bony stud on
    every bulge, each half of its face carries a coloured field (BackSkin).
    """
    frill_sheet(scene, 'Shield', (-FRILL_ARC, FRILL_ARC), (0.0, 1.0), FRILL_SEGMENTS, FRILL_RINGS, FRILL_THICKNESS, 0.0, 'Skin')
    step = 2.0 * FRILL_ARC / FRILL_SEGMENTS
    (first_column, last_column), (first_row, last_row) = FRILL_FIELD
    for side in (-1, 1):
        angles = (side * first_column * step, side * last_column * step)
        shares = (first_row / FRILL_RINGS, last_row / FRILL_RINGS)
        frill_sheet(scene, f'Field{side}', angles, shares, last_column - first_column, last_row - first_row, FRILL_THICKNESS * 0.6, FRILL_THICKNESS * 0.9, 'BackSkin')

    # Studs: one on every bulge of the rim, smaller towards the flanks
    for index in range(FRILL_STUDS):
        degrees = -FRILL_ARC + 2.0 * FRILL_ARC * index / (FRILL_STUDS - 1)
        if abs(degrees) > FRILL_ARC - 12.0:
            continue
        size = 0.085 * (1.0 - 0.3 * abs(degrees) / FRILL_ARC)
        center = frill_point(degrees, 1.0, 0.0)
        ellipsoid(scene, f'Stud{index}', center, (size * 0.5, size * 1.1, size * 1.1), 'Bone', subdivisions=2)
