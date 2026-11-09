// NOTE:
//   Turns glyph skeletons into filled outlines. Every stroke becomes a chain of
//   tapered capsules (two circles and their outer tangents). The stroke radius
//   wobbles along the stroke and swells into droplets at the ends like a membrane.
//   With a tube share the stroke gets hollow: a thinner copy of the same stroke is
//   cut out, crossing strokes leave connected channels. Vacuoles punch small round
//   holes along the strokes, alternating left and right of the center. Dots become organelle
//   bubbles with a highlight hole. polygon-clipping merges everything into clean
//   contours (outer rings counter-clockwise, holes clockwise).
//
//   import { glyphOutline } from '../utils/outline.js'
//   const multiPolygon = glyphOutline({ strokes, dots, seed: 97, profile: PROFILES.text })

//
//   Chapters:
//     SHAPES          Circle and tapered capsule rings.
//     STROKE RADIUS   Membrane wobble and droplet ends along a stroke.
//     GLYPH OUTLINE   Strokes, tubes, vacuoles and bubbles merged into one multipolygon.
//

import polygonClipping from 'polygon-clipping'

const CIRCLE_STEPS = 28                // segments of a full circle
const BUBBLE_SCALE = 1.3               // bubble radius in stroke radii
const HIGHLIGHT_RADIUS = 0.3           // share of the bubble radius
const HIGHLIGHT_OFFSET = 0.32          // share of the bubble radius, up and left

// ===========================================================================
// ================================   SHAPES   ===============================
// ===========================================================================
//
//   circleRing    Closed ring around a center, counter-clockwise.
//   capsuleRing   Convex hull of two circles: a tapered stroke segment.
//

/**
 * Closed circle ring, counter-clockwise (y up), first point repeated at the end.
 *
 * @param {number} cx  (Required) Center x.
 * @param {number} cy  (Required) Center y.
 * @param {number} r   (Required) Radius.
 * @returns {number[][]} Ring as [x, y] pairs.
 */
export function circleRing(cx, cy, r) {
    const ring = []
    for (let i = 0; i <= CIRCLE_STEPS; i++) {
        const angle = (i % CIRCLE_STEPS) / CIRCLE_STEPS * Math.PI * 2
        ring.push([cx + Math.cos(angle) * r, cy + Math.sin(angle) * r])
    }
    return ring
}

/*
  Tapered capsule between circle 0 (p0, r0) and circle 1 (p1, r1):
  - theta is the direction from p0 to p1, phi = acos((r0 - r1) / d) the angle of
    the tangent points measured from theta.
  - The ring runs around the back of circle 0 (theta + phi .. theta + 2pi - phi),
    jumps along the lower tangent to circle 1 and around its front
    (theta - phi .. theta + phi), then closes along the upper tangent.
  - One circle inside the other (d <= |r0 - r1|) -> just the bigger circle.
*/
function capsuleRing(p0, r0, p1, r1) {
    const d = Math.hypot(p1.x - p0.x, p1.y - p0.y)
    if (d <= Math.abs(r0 - r1) + 0.01) {
        return r0 >= r1 ? circleRing(p0.x, p0.y, r0) : circleRing(p1.x, p1.y, r1)
    }

    const theta = Math.atan2(p1.y - p0.y, p1.x - p0.x)
    const phi   = Math.acos((r0 - r1) / d)
    const ring  = []
    const steps = Math.ceil(CIRCLE_STEPS / 2) + 2

    // Back of circle 0
    for (let i = 0; i <= steps; i++) {
        const angle = theta + phi + (2 * Math.PI - 2 * phi) * i / steps
        ring.push([p0.x + Math.cos(angle) * r0, p0.y + Math.sin(angle) * r0])
    }
    // Front of circle 1
    for (let i = 0; i <= steps; i++) {
        const angle = theta - phi + 2 * phi * i / steps
        ring.push([p1.x + Math.cos(angle) * r1, p1.y + Math.sin(angle) * r1])
    }
    ring.push(ring[0])
    return ring
}

// ===========================================================================
// ============================   STROKE RADIUS   ============================
// ===========================================================================
//
//   strokeRadii   Radius per point: two overlaid wobble waves plus droplet ends.
//

/**
 * Radius per stroke point. Closed strokes (first = last point) get no droplet ends.
 * Two sine waves of different length make the wobble look irregular, not periodic.
 *
 * @param {{x: number, y: number}[]} points  (Required) The stroke.
 * @param {number} seed                     (Required) Shifts the wobble per glyph.
 * @param {object} profile                  (Required) Font profile, see data/profiles.js.
 * @returns {number[]} One radius per point.
 */
function strokeRadii(points, seed, profile) {
    const lengths = [0]
    for (let i = 1; i < points.length; i++) {
        lengths.push(lengths[i - 1] + Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y))
    }
    const total    = lengths[lengths.length - 1]
    const first    = points[0]
    const last     = points[points.length - 1]
    const isClosed = Math.hypot(first.x - last.x, first.y - last.y) < 1

    return lengths.map(s => {
        const wave   = 0.7 * Math.sin(s / profile.wobbleLength + seed)
            + 0.3 * Math.sin(s / (profile.wobbleLength * 0.62) + seed * 1.7)
        const toEnd  = Math.min(s, total - s)
        const bulge  = isClosed ? 0 : profile.endBulge * Math.max(0, 1 - toEnd / profile.bulgeLength) ** 1.5
        return profile.strokeRadius * (1 + profile.wobble * wave + bulge)
    })
}

// ===========================================================================
// ============================   GLYPH OUTLINE   ============================
// ===========================================================================
//
//   strokePieces   Capsule rings of all strokes at a radius scale.
//   vacuoleRings   Small round holes along the strokes, away from the droplet ends.
//   glyphOutline   Strokes (hollow when tubed) plus bubbles, minus the bubble highlights.
//

function strokePieces(strokes, seed, profile, radiusScale) {
    const pieces = []
    for (const points of strokes) {
        const radii = strokeRadii(points, seed, profile)
        for (let i = 1; i < points.length; i++) {
            pieces.push([capsuleRing(points[i - 1], radii[i - 1] * radiusScale, points[i], radii[i] * radiusScale)])
        }
    }
    return pieces
}

/*
  Vacuoles sit every vacuoleEvery units of arc length, starting half a step in,
  never inside the droplet ends (bulgeLength * 0.6 from an open end), and swing
  25% of the stroke radius left and right of the center line, so they look
  scattered instead of lined up.
*/
function vacuoleRings(strokes, seed, profile) {
    if (profile.vacuoleEvery <= 0) {
        return []
    }
    const rings = []
    for (const points of strokes) {
        const radii   = strokeRadii(points, seed, profile)
        const lengths = [0]
        for (let i = 1; i < points.length; i++) {
            lengths.push(lengths[i - 1] + Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y))
        }
        const total    = lengths[lengths.length - 1]
        const isClosed = Math.hypot(points[0].x - points[points.length - 1].x, points[0].y - points[points.length - 1].y) < 1
        const margin   = isClosed ? 0 : profile.bulgeLength * 0.6

        let side = 1
        for (let s = profile.vacuoleEvery / 2; s < total; s += profile.vacuoleEvery) {
            if (s < margin || s > total - margin) {
                continue
            }
            const i      = Math.max(1, lengths.findIndex(length => length >= s))
            const t      = (s - lengths[i - 1]) / Math.max(lengths[i] - lengths[i - 1], 0.001)
            const x      = points[i - 1].x + (points[i].x - points[i - 1].x) * t
            const y      = points[i - 1].y + (points[i].y - points[i - 1].y) * t
            const radius = radii[i - 1] + (radii[i] - radii[i - 1]) * t
            const angle  = Math.atan2(points[i].y - points[i - 1].y, points[i].x - points[i - 1].x) + Math.PI / 2
            const swing  = side * 0.25 * radius
            rings.push([circleRing(x + Math.cos(angle) * swing, y + Math.sin(angle) * swing, radius * profile.vacuoleRadius)])
            side = -side
        }
    }
    return rings
}

/**
 * Builds the filled outline of one glyph.
 *
 * @param {object} glyph                               (Required) The skeleton.
 * @param {{x: number, y: number}[][]} glyph.strokes   (Required) Strokes, may be empty.
 * @param {{x: number, y: number, scale?: number}[]} glyph.dots  (Required) Bubble centers, may be empty.
 * @param {number} glyph.seed                          (Required) Wobble seed, e.g. the char code.
 * @param {object} glyph.profile                       (Required) Font profile, see data/profiles.js.
 * @returns {number[][][][]} MultiPolygon: polygons of rings of [x, y] pairs.
 *
 * @example
 * glyphOutline({ strokes: [line(0, 0, 0, 500)], dots: [{ x: 0, y: 670 }], seed: 105, profile })   // an i
 */
export function glyphOutline({ strokes, dots, seed, profile }) {

    // Strokes, hollowed out by a thinner copy of themselves when tubed, then the vacuoles
    let shape = []
    const outer = strokePieces(strokes, seed, profile, 1)
    if (outer.length > 0) {
        shape = polygonClipping.union(...outer)
        if (profile.tube > 0) {
            shape = polygonClipping.difference(shape, ...strokePieces(strokes, seed, profile, profile.tube))
        }
        const vacuoles = vacuoleRings(strokes, seed, profile)
        if (vacuoles.length > 0) {
            shape = polygonClipping.difference(shape, ...vacuoles)
        }
    }

    // Bubbles: round dots with a highlight hole
    const bubbles    = []
    const highlights = []
    for (const dot of dots) {
        const radius = profile.strokeRadius * BUBBLE_SCALE * (dot.scale ?? 1)
        bubbles.push([circleRing(dot.x, dot.y, radius)])
        highlights.push([circleRing(
            dot.x - radius * HIGHLIGHT_OFFSET,
            dot.y + radius * HIGHLIGHT_OFFSET,
            radius * HIGHLIGHT_RADIUS)])
    }
    if (0 === bubbles.length) {
        return shape
    }
    const merged = shape.length > 0 ? polygonClipping.union(shape, ...bubbles) : polygonClipping.union(...bubbles)
    return polygonClipping.difference(merged, ...highlights)
}
