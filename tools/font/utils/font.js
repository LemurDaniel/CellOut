// NOTE:
//   Assembles a Cell Out font variant from the glyph skeletons: lets every glyph
//   bounce and tilt a little, outlines it with the profile, aligns it by its
//   outline with equal side bearings and packs it into an OpenType font (CFF
//   outlines) with opentype.js.
//
//   import { buildFont } from './utils/font.js'
//   const font = buildFont(PROFILES.display)
//   fs.writeFileSync('CellOutDisplay.otf', Buffer.from(font.toArrayBuffer()))

//
//   Chapters:
//     FONT METRICS   Em size, spacing and the reference stroke of the skeletons.
//     MOVE GLYPHS    Bounce, tilt and lifted dots per glyph.
//     BUILD FONT     Outlines, spacing and the opentype.js font.
//

import opentype from 'opentype.js'

import GLYPHS from '../data/glyphs.js'
import { glyphOutline } from './outline.js'

// ===========================================================================
// ============================   FONT METRICS   =============================
// ===========================================================================
//
//   UNITS_PER_EM       Font units per em, the glyph data uses the same scale.
//   SIDE_BEARING       Space left and right of every outline, grows with the stroke.
//   SPACE_WIDTH        Advance of the space.
//   SKELETON_STROKE    Stroke radius the skeletons were drawn for.
//   DOT_LINE           Dots above this height sit over a letter and move up with thicker strokes.
//

const UNITS_PER_EM = 1000
const SIDE_BEARING = 0.95           // in stroke radii
const SPACE_WIDTH = 4.4             // in stroke radii
const NOTDEF_WIDTH = 500
const SKELETON_STROKE = 56
const DOT_LINE = 560
const DOT_LIFT = 1.9                // dot lift per extra stroke radius unit
const TILT_PIVOT_Y = 300

// ===========================================================================
// =============================   MOVE GLYPHS   =============================
// ===========================================================================
//
//   moveGlyph   Bounce and tilt from the char code, so a letter always moves the same way.
//

/**
 * Moves a skeleton: dots over letters are lifted to keep their gap, then the whole
 * glyph is rotated around its center by up to profile.tilt degrees and shifted
 * by up to profile.bounce units. Both come from the char code, so every "e" is the same.
 *
 * @param {object} skeleton   (Required) Strokes, dots and nuclei of the glyph.
 * @param {number} code       (Required) Unicode code point of the glyph.
 * @param {object} profile    (Required) Font profile, see data/profiles.js.
 * @returns {{strokes: object[][], dots: object[]}} Moved strokes and all bubbles to draw.
 */
function moveGlyph(skeleton, code, profile) {
    const lift    = Math.max(0, profile.strokeRadius - SKELETON_STROKE) * DOT_LIFT
    const bubbles = [...skeleton.dots, ...(profile.hasNuclei ? skeleton.nuclei : [])]
        .map(dot => (dot.y > DOT_LINE ? { ...dot, y: dot.y + lift } : dot))

    const xs      = skeleton.strokes.flat().map(point => point.x).concat(bubbles.map(dot => dot.x))
    const pivotX  = (Math.min(...xs) + Math.max(...xs)) / 2
    const angle   = profile.tilt * Math.sin(code * 2.11) * Math.PI / 180
    const shift   = profile.bounce * Math.sin(code * 1.37)
    const move    = point => ({
        ...point,
        x: pivotX + (point.x - pivotX) * Math.cos(angle) - (point.y - TILT_PIVOT_Y) * Math.sin(angle),
        y: TILT_PIVOT_Y + (point.x - pivotX) * Math.sin(angle) + (point.y - TILT_PIVOT_Y) * Math.cos(angle) + shift,
    })

    return {
        strokes: skeleton.strokes.map(points => points.map(move)),
        dots:    bubbles.map(move),
    }
}

// ===========================================================================
// =============================   BUILD FONT   ==============================
// ===========================================================================
//
//   1. Outlines   Moved strokes and bubbles of every glyph merged into contours.
//   2. Spacing    Shifts each outline so its left edge sits at the side bearing.
//   3. Font       .notdef, space and all glyphs in one opentype.js font.
//

/**
 * Builds one font variant in memory.
 *
 * @param {object} profile  (Required) Font profile from data/profiles.js.
 * @returns {opentype.Font} The font, write it with font.toArrayBuffer().
 *
 * @example
 * const font = buildFont(PROFILES.text)
 * font.charToGlyph('a').advanceWidth   // > 0
 */
export function buildFont(profile) {

    // ---------------------------------------------------------------------------
    // Outlines
    //   Moved strokes and bubbles of every glyph merged into contours.
    // ---------------------------------------------------------------------------

    const outlines = Object.entries(GLYPHS)
        .map(([char, skeleton]) => {
            const code  = char.codePointAt(0)
            const moved = moveGlyph(skeleton, code, profile)
            return { char, multiPolygon: glyphOutline({ ...moved, seed: code, profile }) }
        })

    // ---------------------------------------------------------------------------
    // Spacing
    //   Shifts each outline so its left edge sits at the side bearing.
    // ---------------------------------------------------------------------------

    const sideBearing = SIDE_BEARING * profile.strokeRadius
    const glyphs = outlines.map(({ char, multiPolygon }) => {
        const xs        = multiPolygon.flat(2).map(([x]) => x)
        const minX      = Math.min(...xs)
        const maxX      = Math.max(...xs)
        const shift     = sideBearing - minX
        const glyphPath = new opentype.Path()
        for (const ring of multiPolygon.flat(1)) {
            ring.forEach(([x, y], index) => {
                const command = 0 === index ? 'moveTo' : 'lineTo'
                glyphPath[command](Math.round(x + shift), Math.round(y))
            })
            glyphPath.close()
        }
        return new opentype.Glyph({
            name:         `uni${char.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')}`,
            unicode:      char.codePointAt(0),
            advanceWidth: Math.round(maxX - minX + 2 * sideBearing),
            path:         glyphPath,
        })
    })

    // ---------------------------------------------------------------------------
    // Font
    //   .notdef, space and all glyphs in one opentype.js font.
    // ---------------------------------------------------------------------------

    const notdef = new opentype.Glyph({ name: '.notdef', unicode: 0, advanceWidth: NOTDEF_WIDTH, path: new opentype.Path() })
    const space  = new opentype.Glyph({ name: 'space', unicode: 32, advanceWidth: Math.round(SPACE_WIDTH * profile.strokeRadius), path: new opentype.Path() })
    return new opentype.Font({
        familyName:  profile.familyName,
        styleName:   'Regular',
        unitsPerEm:  UNITS_PER_EM,
        ascender:    profile.ascender,
        descender:   profile.descender,
        designer:    'Cell Out',
        description: 'Organic, membrane-like game font of Cell Out, generated by tools/font.',
        glyphs:      [notdef, space, ...glyphs],
    })
}
