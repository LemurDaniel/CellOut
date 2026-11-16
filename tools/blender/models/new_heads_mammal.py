# NOTE:
#   Head models of the creature editor (tab Heads, model space Head): skulls of mammals
#   with slots for the eyes, ears and horns of the player. Model space Head: origin at
#   the neck joint, +X forward, +Y up, +Z the right flank, the neck stump has radius 1.
#   Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_heads_mammal -Call 'build_horsehead(scene)' -Name horsehead
#   ./tools/blender/Show-Model.ps1 -Module new_heads_mammal -Call 'build_cathead(scene)' -Name cathead

#
#   Chapters:
#     SKULL    A skull lofted from rings along its axis, points on its skin, the neck stump.
#     HEADS    Horse, cat and rhino.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import G, ellipsoid, link_mesh, slot, tube

SKULL_AROUND = 28                    # points of one ring
SKULL_STEPS = 6                      # rings between two stations
BUMP_SUNK = 0.55                     # a bump on the skin sits this share of its height inside the skull
STUMP_SIZE = (0.62, 1.0, 1.0)        # the neck plug in Blender axes of the helper: forward, out, up
STUMP_CENTER = (0.12, 0.0, 0.0)

# Stations of a skull along its own axis: x, height of the middle, half height, half width, boxiness (0 an ellipse .. 1 a box)
HORSE_PITCH = -28.0                  # degrees the head hangs below the line of the neck
HORSE_LENGTH = 0.8                   # the drawn stations are shortened to this share ...
HORSE_GIRTH = 0.92                   # ... and slimmed to this one: the head fits a neck of radius 1
HORSE_STATIONS = (
    (-0.25, 0.0, 0.7, 0.7, 0.0),
    (0.4, -0.05, 1.22, 0.86, 0.1),   # the poll on top, the deep jowl below
    (1.15, -0.02, 1.2, 0.9, 0.15),   # forehead and eye
    (2.05, 0.14, 0.86, 0.64, 0.2),   # the cheek runs out, the nose bridge begins
    (2.95, 0.2, 0.62, 0.47, 0.25),   # the narrow bridge
    (3.65, 0.13, 0.6, 0.52, 0.3),    # the muzzle swells again
    (4.1, 0.04, 0.5, 0.47, 0.3),
    (4.34, -0.02, 0.24, 0.26, 0.2),  # the round end of the lips
)
CAT_PITCH = -8.0
CAT_STATIONS = (
    (-0.25, 0.0, 0.75, 0.75, 0.0),
    (0.4, 0.08, 1.24, 1.25, 0.1),    # broad skull
    (1.0, 0.1, 1.3, 1.42, 0.2),      # forehead, the widest at the cheek ruff
    (1.5, 0.0, 1.02, 0.98, 0.2),     # the stop: the forehead falls to the snout
    (1.9, -0.22, 0.66, 0.66, 0.35),  # a short strong snout
    (2.4, -0.26, 0.6, 0.6, 0.4),
    (2.72, -0.28, 0.48, 0.5, 0.35),
    (2.88, -0.3, 0.22, 0.26, 0.2),
)
RHINO_PITCH = -38.0
RHINO_LENGTH = 0.8
RHINO_GIRTH = 0.95
RHINO_STATIONS = (
    (-0.25, 0.0, 0.8, 0.8, 0.0),
    (0.5, 0.1, 1.26, 1.0, 0.2),      # the high crest at the back of the skull
    (1.35, 0.0, 1.16, 0.96, 0.25),
    (2.25, -0.22, 0.9, 0.8, 0.3),    # the saddle between forehead and nose
    (3.05, -0.08, 1.0, 0.86, 0.35),  # the boss the horn stands on
    (3.7, -0.24, 0.8, 0.94, 0.6),    # the wide square lip
    (4.02, -0.34, 0.52, 0.84, 0.7),
    (4.14, -0.4, 0.26, 0.5, 0.5),
)


# ===========================================================================
# ================================   SKULL   ================================
# ===========================================================================
#
#   Skull   A skull lofted from stations, pitched down around the neck joint.
#

class Skull:
    """
    A skull as a loft of rings along its own axis (x), turned down around the neck
    joint by pitch. It knows the point on its skin for every x and angle, so details
    and slots are set on the surface instead of guessed in space.

    skull = Skull(scene, HORSE_STATIONS, HORSE_PITCH, HORSE_LENGTH, HORSE_GIRTH)
    eye = skull.skin(1.2, 30)             # Godot axes: on the right side, 30 degrees above the middle
    """

    def __init__(self, scene, stations, pitch, length=1.0, girth=1.0):
        self.scene = scene
        # The drawn stations, shortened and slimmed: x of every later call is still given on the drawn axis
        self._length = length
        self.stations = tuple((x * length, middle * girth, half_height * girth, half_width * girth, box) for x, middle, half_height, half_width, box in stations)
        turn = math.radians(pitch)
        self._cos, self._sin = math.cos(turn), math.sin(turn)
        self._build()
        ellipsoid(scene, 'Stump', G(*STUMP_CENTER), STUMP_SIZE, 'Skin', subdivisions=3)

    def _station(self, x):
        """Height of the middle, half height, half width and boxiness at an x: a Catmull-Rom curve through the stations."""
        stations = self.stations
        last = len(stations) - 1
        if x <= stations[0][0]:
            return stations[0][1:]
        for i in range(last):
            x1, x2 = stations[i][0], stations[i + 1][0]
            if x <= x2:
                t = (x - x1) / (x2 - x1)
                p0, p1, p2, p3 = stations[max(i - 1, 0)][1:], stations[i][1:], stations[i + 1][1:], stations[min(i + 2, last)][1:]
                return tuple(
                    0.5 * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t ** 3)
                    for a, b, c, d in zip(p0, p1, p2, p3))
        return stations[-1][1:]
    def turned(self, x, y, z):
        """A point of the unpitched skull (Godot axes) turned down around the neck joint."""
        return (x * self._cos - y * self._sin, x * self._sin + y * self._cos, z)

    def skin(self, x, angle, lift=0.0):
        """The point on the skin at an x of the axis and an angle around it in degrees (0 the right side, 90 the top, -90 the chin), lift pushes it out. Godot axes."""
        x *= self._length
        middle, half_height, half_width, box = self._station(x)
        power = 2.0 / (2.0 + 4.0 * max(0.0, box))                              # a superellipse: 1 for an ellipse, less for a box
        cos_a, sin_a = math.cos(math.radians(angle)), math.sin(math.radians(angle))
        z = math.copysign(abs(cos_a) ** power, cos_a) * (half_width + lift)
        y = middle + math.copysign(abs(sin_a) ** power, sin_a) * (half_height + lift)
        return self.turned(x, y, z)

    def _build(self):
        first, last = self.stations[0][0] / self._length, self.stations[-1][0] / self._length
        count = (len(self.stations) - 1) * SKULL_STEPS
        bm = bmesh.new()
        rings = []
        for i in range(count + 1):
            x = first + (last - first) * i / count
            rings.append([bm.verts.new(G(*self.skin(x, 360.0 * k / SKULL_AROUND))) for k in range(SKULL_AROUND)])
        for ring, following in zip(rings, rings[1:]):
            for k in range(SKULL_AROUND):
                bm.faces.new((ring[k], ring[(k + 1) % SKULL_AROUND], following[(k + 1) % SKULL_AROUND], following[k]))
        # Round ends: the back hides in the stump, the front is the tip of the nose
        for ring, x, bulge in ((rings[0], first, -0.3), (rings[-1], last, 0.16)):
            middle = self._station(x * self._length)[0]
            center = bm.verts.new(G(*self.turned(x * self._length + bulge, middle, 0.0)))
            for k in range(SKULL_AROUND):
                bm.faces.new((ring[k], ring[(k + 1) % SKULL_AROUND], center))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new('Skull')
        bm.to_mesh(mesh)
        bm.free()
        obj = link_mesh(self.scene, 'Skull', mesh, 'Skin')
        obj.modifiers.new('Smooth', 'SUBSURF').levels = 1
        depsgraph = bpy.context.evaluated_depsgraph_get()
        baked = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
        obj.modifiers.clear()
        obj.data = baked
        for polygon in obj.data.polygons:
            polygon.use_smooth = True

    def bump(self, name, x, angle, size, material_name='Skin', sink=0.0):
        sink += size[2] * BUMP_SUNK
        """An ellipsoid on the skin of both halves (or one on the midline at 90 or -90 degrees): size is along the axis, around the skull, out of it."""
        sides = (1,) if abs(abs(angle) - 90.0) < 1e-6 else (1, -1)
        for side in sides:
            px, py, pz = self.skin(x, angle, -sink)
            nx, ny, nz = self.skin(x, angle, 1.0 - sink)
            out = Vector((nx - px, ny - py, nz - pz)).normalized()
            along = Vector(self.turned(1.0, 0.0, 0.0))
            around = out.cross(along).normalized()
            obj = ellipsoid(self.scene, f'{name}{side}', Vector((0, 0, 0)), size, material_name, subdivisions=3)
            # Columns: the local axes of the ellipsoid (along, around, out) in Blender space
            frame = Matrix((G(*along), G(*around), G(*out))).transposed().to_4x4()
            obj.data.transform(Matrix.Translation(G(px, py, pz)) @ frame)
            if side < 0:
                obj.data.transform(Matrix.Diagonal((1.0, -1.0, 1.0, 1.0)))
                obj.data.flip_normals()

    def line(self, name, spots, radius, material_name='Dark', lift=0.01):
        """A thin line on the skin through (x, angle) spots: a lip line, a nostril slit."""
        points = [G(*self.skin(x, angle, lift)) for x, angle in spots]
        fine = []
        for start, end in zip(points, points[1:]):
            fine.extend(start.lerp(end, step / 3) for step in range(3))
        fine.append(points[-1])
        return tube(self.scene, name, fine, [radius] * len(fine), [(i, i + 1) for i in range(len(fine) - 1)], material_name, subdivisions=1)

    def slot(self, name, x, angle, size, out_tilt=(0.0, 0.0, 0.0), forward=(1.0, 0.0, 0.0)):
        """A slot on the skin at an x and angle: out is the skin normal there plus out_tilt, forward is given along the unpitched skull."""
        px, py, pz = self.skin(x, angle)
        nx, ny, nz = self.skin(x, angle, 1.0)
        out = Vector((nx - px, ny - py, nz - pz)).normalized() + Vector(self.turned(*out_tilt))
        slot(self.scene, name, (px, py, max(pz, 0.02)), size, out=tuple(out), forward=self.turned(*forward))


# ===========================================================================
# ================================   HEADS   ================================
# ===========================================================================
#
#   build_horsehead   The long wedge skull of a horse or a giraffe.
#   build_cathead     The broad skull of a big cat or a wolf with a short snout.
#   build_rhinohead   The long heavy skull of a rhino with a square lip.
#

def build_horsehead(scene):
    """
    The wedge head of a horse or a giraffe (Head), hanging below the line of the neck:

          ear slot  horn slot (forehead, for ossicones)
             \\  _/_
        ====(  poll  '--.__              narrow forehead, long straight nose bridge
            | (eye)        '--.__
            |  jowl     .---.    '--.    the round cheek bone, the head thins to the bridge
             \\________/     '--._  o )  soft wide muzzle: nostril, a closed lip line
                                  '--'

    Slots (right half): Eye high on the side, Ear on top at the back standing up,
    Horn on the forehead between the ears. No rig.
    """
    skull = Skull(scene, HORSE_STATIONS, HORSE_PITCH, HORSE_LENGTH, HORSE_GIRTH)
    skull.bump('Jowl', 0.85, -38, (0.66, 0.62, 0.12), sink=0.03)        # the round cheek bone
    skull.bump('Brow', 1.2, 42, (0.3, 0.2, 0.1), sink=0.03)
    skull.bump('Chin', 3.75, -90, (0.3, 0.3, 0.14), sink=0.04)
    skull.bump('NostrilRim', 4.02, 32, (0.2, 0.17, 0.09), sink=0.03)
    skull.bump('Nostril', 4.12, 30, (0.12, 0.08, 0.06), 'Dark', sink=-0.03)
    # The closed lips: from the corner of the mouth around the front of the muzzle
    for side in (1, -1):
        skull.line(f'Lip{side}', [(3.5, -90 + side * 52), (3.9, -90 + side * 56), (4.22, -90 + side * 62), (4.36, -90 + side * 40), (4.4, -90)], 0.022)

    skull.slot('Eye', 1.22, 28, 0.17, out_tilt=(0.15, 0.1, 0.0))
    skull.slot('Ear', 0.3, 62, 0.5, forward=(0.2, -1.0, 0.0))         # the ear part reaches back along -X: it stands up here
    skull.slot('Horn', 0.75, 86, 0.75)


def build_cathead(scene):
    """
    The head of a big cat, with the longer snout of a wolf in mind (Head):

            ear slot
           _.-'''-._
        =(  broad    '.          broad skull, a clear stop at the forehead
         |  skull (eye)\\__
         | ruff      pad  (#)    a short strong snout: whisker pads, a dark nose pad,
          \\____.-'-.__.-'       the mouth line runs down from the nose and along the lips
                 chin

    The skull is widest at the cheeks: the ruff. Slots (right half): Eye looking forward, Ear on
    top to the side. No rig.
    """
    skull = Skull(scene, CAT_STATIONS, CAT_PITCH)
    skull.bump('Brow', 1.38, 48, (0.3, 0.28, 0.12), sink=0.04)
    skull.bump('Pad', 2.5, -22, (0.36, 0.3, 0.16), sink=0.05)         # whisker pads
    skull.bump('Chin', 2.42, -90, (0.34, 0.34, 0.16), sink=0.05)
    skull.bump('Bridge', 2.2, 90, (0.5, 0.3, 0.1), sink=0.04)
    # Nose pad on the tip, the line down from it and along the lips to the corners of the mouth
    nx, ny, nz = skull.turned(2.93, -0.1, 0.0)
    ellipsoid(scene, 'Nose', G(nx, ny, nz), (0.15, 0.24, 0.15), 'Dark', subdivisions=3)
    for side in (1, -1):
        skull.line(f'Lip{side}', [(3.02, -90), (2.9, -90 + side * 30), (2.6, -90 + side * 52), (2.2, -90 + side * 58), (1.9, -90 + side * 66)], 0.024)
    px, py, pz = skull.turned(2.98, -0.24, 0.0)
    qx, qy, qz = skull.turned(3.0, -0.48, 0.0)
    tube(scene, 'Philtrum', [G(px, py, pz), G(qx, qy, qz)], [0.022, 0.022], [(0, 1)], 'Dark', subdivisions=1)

    skull.slot('Eye', 1.5, 34, 0.19, out_tilt=(0.7, 0.0, 0.0))        # turned to the front: a hunter
    skull.slot('Ear', 0.55, 52, 0.42, forward=(0.25, -1.0, 0.0))


def build_rhinohead(scene):
    """
    The head of a white rhino (Head), carried low:

          ear slot
         _/_
        =(  crest '-.___      _ horn slot on the boss of the nose
          |             '-.__/ \\__
          |    (eye)   saddle     |   a long heavy skull, a saddle between forehead and nose
           \\__  low               |   small eye low on the side, folds around it
               '--.__  nostril  __|   the wide square lip, a closed mouth line
                     '--------'

    Slots (right half): Eye, Ear (small, on top at the back), Horn on the nose on the
    midline. No rig.
    """
    skull = Skull(scene, RHINO_STATIONS, RHINO_PITCH, RHINO_LENGTH, RHINO_GIRTH)
    skull.bump('Boss', 3.05, 90, (0.5, 0.42, 0.14), sink=0.05)        # the horn stands on it
    skull.bump('Lid', 1.95, 4, (0.26, 0.2, 0.08), sink=0.03)          # folds around the small eye
    skull.bump('NostrilRim', 3.86, 30, (0.22, 0.16, 0.08), sink=0.03)
    skull.bump('Nostril', 3.95, 30, (0.12, 0.05, 0.05), 'Dark', sink=0.0)
    for side in (1, -1):
        skull.line(f'Lip{side}', [(3.3, -90 + side * 62), (3.8, -90 + side * 60), (4.08, -90 + side * 52), (4.22, -90 + side * 30), (4.26, -90)], 0.028)

    skull.slot('Eye', 1.95, 2, 0.1)
    skull.slot('Ear', 0.42, 60, 0.36, forward=(0.2, -1.0, 0.0))
    skull.slot('Horn', 3.05, 88, 0.9)
