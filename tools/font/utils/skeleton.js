// NOTE:
//   Building blocks for glyph skeletons: lines, arcs and full ellipses as point lists
//   in font units (y up, baseline 0). The outline step strokes them into shapes.
//
//   import { line, arc, ellipse, path } from '../utils/skeleton.js'
//   const strokeOfL = path(line(0, 700, 0, 0), line(0, 0, 400, 0))

//
//   Chapters:
//     SKELETON PRIMITIVES   Lines, arcs, ellipses and joined paths.
//

const LINE_STEP = 30      // sample distance on lines, keeps the wobble smooth
const ARC_STEP_DEG = 6    // sample angle on arcs

// ===========================================================================
// ========================   SKELETON PRIMITIVES   ==========================
// ===========================================================================
//
//   line      Straight stroke, sampled every LINE_STEP units.
//   arc       Elliptic arc from one angle to another, either direction.
//   ellipse   Closed ellipse, first point repeated at the end.
//   path      Joins several pieces into one stroke, dropping duplicate joints.
//

/**
 * Straight stroke between two points.
 *
 * @param {number} x0  (Required) Start x.
 * @param {number} y0  (Required) Start y.
 * @param {number} x1  (Required) End x.
 * @param {number} y1  (Required) End y.
 * @returns {{x: number, y: number}[]} Points from start to end.
 *
 * @example
 * line(0, 0, 0, 700)   // a stem
 */
export function line(x0, y0, x1, y1) {
    const length = Math.hypot(x1 - x0, y1 - y0)
    const steps  = Math.max(1, Math.ceil(length / LINE_STEP))
    const points = []
    for (let i = 0; i <= steps; i++) {
        const t = i / steps
        points.push({ x: x0 + (x1 - x0) * t, y: y0 + (y1 - y0) * t })
    }
    return points
}

/**
 * Elliptic arc. Angles in degrees, 0 = right, 90 = up. From > to runs clockwise.
 *
 * @param {number} cx    (Required) Center x.
 * @param {number} cy    (Required) Center y.
 * @param {number} rx    (Required) Horizontal radius.
 * @param {number} ry    (Required) Vertical radius.
 * @param {number} from  (Required) Start angle in degrees.
 * @param {number} to    (Required) End angle in degrees.
 * @returns {{x: number, y: number}[]} Points from start to end angle.
 *
 * @example
 * arc(200, 250, 200, 250, 40, 320)   // the bowl of a c
 */
export function arc(cx, cy, rx, ry, from, to) {
    const steps  = Math.max(2, Math.ceil(Math.abs(to - from) / ARC_STEP_DEG))
    const points = []
    for (let i = 0; i <= steps; i++) {
        const angle = (from + (to - from) * i / steps) * Math.PI / 180
        points.push({ x: cx + Math.cos(angle) * rx, y: cy + Math.sin(angle) * ry })
    }
    return points
}

/**
 * Closed ellipse, starting at the top so the wobble seam hides in a curve.
 *
 * @param {number} cx  (Required) Center x.
 * @param {number} cy  (Required) Center y.
 * @param {number} rx  (Required) Horizontal radius.
 * @param {number} ry  (Required) Vertical radius.
 */
export function ellipse(cx, cy, rx, ry) {
    return arc(cx, cy, rx, ry, 90, 450)
}

/**
 * Joins pieces into one continuous stroke. Each piece should start where the
 * previous one ends, the duplicate joint point is dropped.
 *
 * @param {...{x: number, y: number}[]} pieces  (Required) Lines and arcs in drawing order.
 */
export function path(...pieces) {
    return pieces
        .flatMap((piece, index) => (index > 0 ? piece.slice(1) : piece))
}
