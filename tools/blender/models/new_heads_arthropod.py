# NOTE:
#   Head models of the creature editor for arthropods (tab Heads, model space Head):
#   hard skulls with slots for the insect, the spider and the crab.
#   Registered in build.models.py. Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   ./tools/blender/Show-Model.ps1 -Module new_heads_arthropod -Call 'build_insecthead(scene)' -Name insecthead
#   ./tools/blender/Show-Model.ps1 -Module new_heads_arthropod -Call 'build_spiderhead(scene)' -Name spiderhead
#   ./tools/blender/Show-Model.ps1 -Module new_heads_arthropod -Call 'build_crabhead(scene)' -Name crabhead

#
#   Chapters:
#     HELPERS       A sphere pushed into the shape of a skull, the neck plug, one moving piece.
#     INSECT HEAD   The head of an ant or wasp with compound eyes and mandibles.
#     SPIDER HEAD   The front shield of a spider with its chelicerae.
#     CRAB HEAD     The face plate of a crab with its mouth doors.
#

import math

import bmesh
import bpy

from models.helpers import AXIS_FORWARD, AXIS_UP, FPS, G, animate, bind, cone, ellipsoid, link_mesh, rig, slot, tube, wave

STUMP_CENTER = (0.15, 0.0, 0.0)      # the neck plug behind every skull, it sinks into the body
STUMP_SIZE = (0.6, 1.0, 1.0)         # in Blender axes of the helper: forward, out, up
SKULL_BONE = (G(-0.3, 0, 0), G(0.9, 0, 0))     # the bone without keys that holds a skull still

# Insect: an egg hanging from the neck, wide at the top, the mouth end low and forward
INSECT_CENTER = (0.95, 0.0)          # forward, up
INSECT_SIZE = (0.9, 1.35, 1.3)        # half depth, half height, half width at the middle
INSECT_TAPER = 0.42                  # the head is this much wider at the top and narrower at the mouth
INSECT_TILT = 0.34                   # the mouth end sits this far in front of the crown
INSECT_EYE_AT = (0.95, 0.6, 1.2)     # center of the compound eye of the right half
INSECT_EYE_SIZE = (0.42, 0.62, 0.3)  # half length, half height, how far it bulges out
INSECT_FACETS = 84                   # facet bumps on one compound eye
INSECT_FACET_SIZE = 0.052
INSECT_SOCKET_AT = (1.72, 0.12, 0.4)             # the feeler socket on the forehead
INSECT_MANDIBLE = ((1.62, -1.02, 0.56), (2.08, -1.14, 0.6), (2.42, -1.22, 0.4), (2.56, -1.26, 0.1))     # root .. tip of the right one
INSECT_MANDIBLE_RADII = (0.2, 0.17, 0.11, 0.04)
INSECT_CHEW = 16.0                   # degrees a mandible opens

# Spider: a boxy dome with a broad upright front face
SPIDER_CENTER = (0.8, 0.12)
SPIDER_SIZE = (1.15, 1.2, 1.5)
SPIDER_BOXINESS = 0.72               # exponent on the sphere: 1 round, lower = a box with round edges
SPIDER_DOME = 0.9                    # the same for the height: the top stays a dome
SPIDER_CHELICERA_AT = (1.62, -0.82, 0.48)        # center of the right chelicera
SPIDER_CHELICERA_SIZE = (0.5, 0.74, 0.43)        # half depth, half height, half width
SPIDER_FANG = ((1.74, -1.4, 0.46), (1.86, -1.66, 0.42), (1.86, -1.9, 0.28), (1.78, -2.02, 0.1))
SPIDER_FANG_RADII = (0.17, 0.14, 0.09, 0.02)
SPIDER_FLEX = 7.0                    # degrees a chelicera swings out

# Crab: a low wide box, the front edge of the carapace on top, the mouth doors below
CRAB_CENTER = (0.5, 0.0)
CRAB_SIZE = (0.92, 1.05, 2.05)
CRAB_BOXINESS = 0.5
CRAB_TAPER = 0.16                    # the face is this much wider at the front edge than at the chin
CRAB_EYE_AT = (1.3, 0.98, 0.8)       # the eye socket on the front edge, right half
CRAB_TEETH = ((1.36, 0.95, 1.2), (1.3, 0.93, 1.5), (1.18, 0.9, 1.78))     # the notches of the edge outside the eye
CRAB_DOOR_AT = (1.45, -0.22)         # the mouth doors: forward, up
CRAB_DOOR_SIZE = (0.08, 0.55, 0.36)  # half thickness, half height, half width of one door
CRAB_CHEW = 20.0                     # degrees a door opens

# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   shaped_sphere   A sphere whose every direction is mapped to a point of the skull.
#   boxy            A direction of the unit sphere pushed towards a box.
#   neck_stump      The round plug behind a skull.
#   hinged_loop     The rig of a skull with two mirrored moving pieces and their loop.
#

def shaped_sphere(scene, name, to_point, material_name='Skin', subdivisions=4):
    """
    One closed smooth mesh without seams: an icosphere, every vertex moved to
    to_point(direction), direction and result in Godot axes (forward, up, right flank).
    """
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    for vertex in bm.verts:
        # Blender unit sphere (x forward, -y the right flank, z up) read as a direction in Godot axes
        vertex.co = G(*to_point((vertex.co.x, vertex.co.z, -vertex.co.y)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def boxy(direction, exponent):
    """The direction with every component raised to exponent (sign kept): 1 stays a sphere, 0.5 is nearly a box."""
    return tuple(math.copysign(abs(value) ** exponent, value) for value in direction)


def neck_stump(scene):
    """The neck plug of radius 1 behind the skull, reaching back to x = -0.45."""
    return ellipsoid(scene, 'Stump', G(*STUMP_CENTER), STUMP_SIZE, 'Skin', subdivisions=3)


def hinged_loop(scene, still, moving, bone, loop, axis, degrees, seconds=2):
    """
    Rig and loop of a skull with one moving piece per side:
    - still:    meshes that follow the bone Skull (no keys, it holds the head still)
    - moving:   {side: (meshes, head, tail)} for side 'Right' (z > 0) and 'Left', the bone
                <bone><side> runs from head to tail (Blender vectors)
    - loop:     name of the animation, both pieces swing by degrees about axis, mirrored
    """
    bones = [('Skull', *SKULL_BONE, None)]
    rigid = {mesh.name: 'Skull' for mesh in still}
    for side, (meshes, head, tail) in moving.items():
        bones.append((bone + side, head, tail, 'Skull'))
        rigid.update({mesh.name: bone + side for mesh in meshes})
    armature = rig(scene, 'Rig', bones)
    bind(armature, still + [mesh for meshes, _, _ in moving.values() for mesh in meshes], rigid=rigid)
    animate(armature, loop, FPS * seconds, {
        bone + 'Right': (axis, lambda share: -degrees * (0.5 - 0.5 * wave(share, 0.25))),
        bone + 'Left': (axis, lambda share: degrees * (0.5 - 0.5 * wave(share, 0.25))),
    })


# ===========================================================================
# =============================   INSECT HEAD   =============================
# ===========================================================================
#
#   insect_skin         A point on the skull of the insect.
#   compound_eye        One compound eye: a dark bulge covered with facet bumps.
#   build_insecthead    The head of an ant or wasp.
#

def insect_skin(direction):
    """The skull of the insect: an egg, wider at the crown, the mouth end low and pushed forward."""
    x, y, z = direction
    return (INSECT_CENTER[0] + INSECT_SIZE[0] * x - INSECT_TILT * y,
            INSECT_CENTER[1] + INSECT_SIZE[1] * y,
            INSECT_SIZE[2] * (1.0 + INSECT_TAPER * y) * z)


def compound_eye(scene, side):
    """The compound eye of one side: a dark bulge of the skull, its outer half covered with round facets."""
    length, height, bulge = INSECT_EYE_SIZE
    center = G(INSECT_EYE_AT[0], INSECT_EYE_AT[1], side * INSECT_EYE_AT[2])
    meshes = [ellipsoid(scene, f'Eye{side}', center, (length, bulge, height), 'Dark', subdivisions=3)]
    # Facets: evenly spread over the outer half (a sunflower spiral around the axis out of the head)
    for i in range(INSECT_FACETS):
        out = 1.0 - 0.82 * (i + 0.5) / INSECT_FACETS
        ring = math.sqrt(1.0 - out * out)
        angle = i * math.pi * (3.0 - math.sqrt(5.0))
        spot = center + G(length * ring * math.cos(angle), height * ring * math.sin(angle), side * bulge * out)
        meshes.append(ellipsoid(scene, f'Facet{side}_{i}', spot, (INSECT_FACET_SIZE,) * 3, 'Dark', subdivisions=1))
    return meshes


def build_insecthead(scene):
    """
    The head of an ant or a wasp (model space Head: origin at the neck joint, +X forward,
    +Y up, +Z the right flank, the neck stump has radius 1):

             .-===-.          a rounded triangle from the front: wide crown,
           /(@)   (@)\\         two big compound eyes as dark bulges with facets,
           \\   o o   /         the feeler sockets on the forehead (slot Horn),
            \\  ___  /          the shield over the mouth,
             >|   |<            two strong toothed mandibles closing in front

    Eyes of the player plug onto the compound eye (slot Eye), horns or feelers onto the
    socket (slot Horn), both on the right half and mirrored by the game.
    Rig: Skull holds the head still; MandibleLeft and MandibleRight open and close ("Chew", 2 s).
    """
    skull = shaped_sphere(scene, 'Skull', insect_skin)
    still = [skull, neck_stump(scene)]
    # Shield over the mouth and the two sockets of the feelers
    still.append(ellipsoid(scene, 'Shield', G(1.56, -0.9, 0), (0.44, 0.66, 0.4), 'Skin', subdivisions=3))
    for side in (-1, 1):
        still.append(ellipsoid(scene, f'Socket{side}', G(INSECT_SOCKET_AT[0], INSECT_SOCKET_AT[1], side * INSECT_SOCKET_AT[2]), (0.18, 0.21, 0.21), 'Skin', subdivisions=2))
        still.extend(compound_eye(scene, side))

    # Mandibles: a sickle of horn with three dark teeth on its inner edge
    moving = {}
    for side, name in ((1, 'Right'), (-1, 'Left')):
        points = [G(x, y, side * z) for x, y, z in INSECT_MANDIBLE]
        blade = tube(scene, f'Mandible{name}', points, INSECT_MANDIBLE_RADII, [(i, i + 1) for i in range(len(points) - 1)], 'Chitin')
        meshes = [blade]
        for i, share in enumerate((0.3, 0.55, 0.8)):
            root = points[1].lerp(points[2], share) if share < 0.7 else points[2].lerp(points[3], 0.3)
            meshes.append(cone(scene, f'Tooth{name}{i}', root, root + G(0.05, -0.02, -side * 0.24), 0.08, 'Dark'))
        moving[name] = (meshes, points[0], points[-1])

    slot(scene, 'Eye', (INSECT_EYE_AT[0] + 0.12, INSECT_EYE_AT[1], INSECT_EYE_AT[2] + INSECT_EYE_SIZE[2]), 0.24, out=(0.25, 0.05, 1.0), forward=(1.0, 0.0, 0.0))
    slot(scene, 'Horn', (INSECT_SOCKET_AT[0] + 0.05, INSECT_SOCKET_AT[1] + 0.08, INSECT_SOCKET_AT[2]), 0.9, out=(0.45, 1.0, 0.3), forward=(1.0, 0.0, 0.0))
    hinged_loop(scene, still, moving, 'Mandible', 'Chew', AXIS_UP, INSECT_CHEW)


# ===========================================================================
# =============================   SPIDER HEAD   =============================
# ===========================================================================
#
#   spider_skin        A point on the front shield of the spider.
#   build_spiderhead   The front shield of a spider with its chelicerae.
#

def spider_skin(direction):
    """The front shield: a dome with round edges, a broad upright face in front."""
    x, _, z = boxy(direction, SPIDER_BOXINESS)
    y = boxy(direction, SPIDER_DOME)[1]
    return (SPIDER_CENTER[0] + SPIDER_SIZE[0] * x, SPIDER_CENTER[1] + SPIDER_SIZE[1] * y, SPIDER_SIZE[2] * z)


def build_spiderhead(scene):
    """
    The front shield of a jumping spider or tarantula (model space Head: origin at the
    neck joint, +X forward, +Y up, +Z the right flank, the neck stump has radius 1):

            .--------.          a domed shield with a wide upright front face,
           | (O)  (O) |         the eyes sit on it (slot Eye, built for SpiderEyes:
           |__________|         the cluster spreads from the middle outwards),
             |##||##|           two fat chelicerae hang side by side under the face,
              \\/  \\/           each ends in a dark fang curving in and down

    Rig: Skull holds the head still; FangLeft and FangRight swing the chelicerae apart
    ("Flex", 3 s).
    """
    still = [shaped_sphere(scene, 'Skull', spider_skin), neck_stump(scene)]

    moving = {}
    depth, height, width = SPIDER_CHELICERA_SIZE
    for side, name in ((1, 'Right'), (-1, 'Left')):
        x, y, z = SPIDER_CHELICERA_AT
        chelicera = ellipsoid(scene, f'Chelicera{name}', G(x, y, side * z), (depth, width, height), 'Skin', subdivisions=3)
        points = [G(fx, fy, side * fz) for fx, fy, fz in SPIDER_FANG]
        fang = tube(scene, f'Fang{name}', points, SPIDER_FANG_RADII, [(i, i + 1) for i in range(len(points) - 1)], 'Chitin')
        moving[name] = ([chelicera, fang], G(x - 0.1, y + height * 0.8, side * z), G(x, y - height, side * z))

    # The eye cluster looks forward, its smaller eyes spread towards the flank and up
    face_x = SPIDER_CENTER[0] + SPIDER_SIZE[0]
    slot(scene, 'Eye', (face_x - 0.04, 0.45, 0.38), 0.32, out=(1.0, 0.08, 0.1), forward=(0.0, 0.0, -1.0))
    hinged_loop(scene, still, moving, 'Fang', 'Flex', AXIS_FORWARD, SPIDER_FLEX, seconds=3)


# ===========================================================================
# ==============================   CRAB HEAD   ==============================
# ===========================================================================
#
#   crab_skin        A point on the face plate of the crab.
#   build_crabhead   The face of a crab between its claws.
#

def crab_skin(direction):
    """The face plate: a low wide box with round edges."""
    x, y, z = boxy(direction, CRAB_BOXINESS)
    return (CRAB_CENTER[0] + CRAB_SIZE[0] * x, CRAB_CENTER[1] + CRAB_SIZE[1] * y, CRAB_SIZE[2] * (1.0 + CRAB_TAPER * y) * z)


def build_crabhead(scene):
    """
    The face of a crab (model space Head: origin at the neck joint, +X forward, +Y up,
    +Z the right flank, the neck stump has radius 1):

         /\\/\\__o_/\\/\\_o__/\\/\\      the notched front edge of the carapace, a blunt
        |      ______      |    lobe in the middle, an eye socket on each side
        |     | || || |     |    (slot Eye, built for stalk eyes standing up),
        |_____|_||_||_|_____|    below it the two mouth plates like double doors

    Short feelers plug between the eyes (slot Horn).
    Rig: Skull holds the face still; PlateLeft and PlateRight open the doors ("Chew", 2 s).
    """
    still = [shaped_sphere(scene, 'Skull', crab_skin), neck_stump(scene)]

    # Front edge: the lobe between the eyes, a rim around each socket, pointed notches outside
    still.append(ellipsoid(scene, 'Lobe', G(1.36, 0.92, 0), (0.2, 0.42, 0.16), 'Skin', subdivisions=3))
    for side in (-1, 1):
        count = 10
        rim = [G(CRAB_EYE_AT[0] + 0.2 * math.cos(math.tau * i / count), CRAB_EYE_AT[1] + 0.06, side * (CRAB_EYE_AT[2] + 0.2 * math.sin(math.tau * i / count))) for i in range(count)]
        still.append(tube(scene, f'Rim{side}', rim, [0.07] * count, [(i, (i + 1) % count) for i in range(count)], 'Skin', subdivisions=1, max_pieces=2))
        for i, (x, y, z) in enumerate(CRAB_TEETH):
            still.append(ellipsoid(scene, f'Tooth{side}_{i}', G(x, y, side * z), (0.3, 0.13, 0.12), 'Skin', subdivisions=2))

    # Mouth: a dark frame, in it two doors hinged on their outer edges
    door_x, door_y = CRAB_DOOR_AT
    thickness, height, width = CRAB_DOOR_SIZE
    still.append(ellipsoid(scene, 'Frame', G(door_x - 0.1, door_y, 0), (0.1, width * 2 + 0.16, height + 0.12), 'Dark', subdivisions=3))
    moving = {}
    for side, name in ((1, 'Right'), (-1, 'Left')):
        door = shaped_sphere(scene, f'Plate{name}', lambda d, side=side: (
            door_x + thickness * boxy(d, 0.6)[0], door_y + height * boxy(d, 0.6)[1], side * (width + 0.02) + width * boxy(d, 0.6)[2]), subdivisions=3)
        moving[name] = ([door], G(door_x, door_y - height, side * (width * 2 + 0.02)), G(door_x, door_y + height, side * (width * 2 + 0.02)))

    # Eye models stand along their local +Y: looking forward with +X towards the middle keeps a stalk upright
    slot(scene, 'Eye', CRAB_EYE_AT, 0.28, out=(1.0, 0.0, 0.25), forward=(0.0, 0.0, -1.0))
    slot(scene, 'Horn', (1.42, 0.5, 0.24), 0.45, out=(1.0, 0.35, 0.15), forward=(0.0, 1.0, 0.0))
    hinged_loop(scene, still, moving, 'Plate', 'Chew', AXIS_UP, CRAB_CHEW)
