// NOTE:
//   Skeletons of every glyph of the Cell Out font in font units (1000 per em,
//   y up, baseline 0). Each glyph lists its strokes (center lines, stroked later
//   with a round, wobbling membrane), its dots (organelle bubbles, always drawn)
//   and its nuclei (bubbles inside closed bowls, only in profiles with hasNuclei).
//   The x origin does not matter, build.font.js aligns every glyph by its outline.
//
//   import GLYPHS from './data/glyphs.js'
//   const { strokes, dots } = GLYPHS['a']

//
//   Chapters:
//     METRICS       Heights every glyph aligns to.
//     LOWERCASE     a-z and ß.
//     UPPERCASE     A-Z.
//     DIGITS        0-9, the zero carries a nucleus.
//     PUNCTUATION   Marks, brackets, arrows and symbols used by the game texts.
//     UMLAUTS       ä ö ü Ä Ö Ü built from their base letters.
//

import { line, arc, ellipse, path } from '../utils/skeleton.js'

// ===========================================================================
// ===============================   METRICS   ===============================
// ===========================================================================
//
//   XH          x-height, top of a, c, e ...
//   CH          cap height, top of capitals and digits.
//   AH          ascender, top of b, d, h, k, l.
//   DH          descender, bottom of g, j, p, q, y.
//   DOT_Y       center of the dot on i and j and of lowercase umlaut dots.
//   CAP_DOT_Y   center of capital umlaut dots, drawn at CAP_DOT_SCALE.
//

const XH = 500
const CH = 700
const AH = 740
const DH = -200
const DOT_Y = XH + 175
const CAP_DOT_Y = CH + 140
const CAP_DOT_SCALE = 0.85

const NUCLEUS = 0.5         // nucleus bubble scale

/** One glyph: strokes, optional bubble dots and optional nucleus centers. */
const glyph = (strokes, dots = [], nuclei = []) => ({
    strokes,
    dots,
    nuclei: nuclei.map(([x, y]) => ({ x, y, scale: NUCLEUS })),
})

// ===========================================================================
// ==============================   LOWERCASE   ==============================
// ===========================================================================
//
//   LOWERCASE   a-z and ß, single-story a and g, round bowls of rx 200 / ry 250.
//

const LOWERCASE = {
    'a': glyph([ellipse(190, 250, 190, 250), line(380, XH, 380, 0)], [], [[190, 250]]),
    'b': glyph([line(0, AH, 0, 0), ellipse(200, 250, 200, 250)], [], [[200, 250]]),
    'c': glyph([arc(200, 250, 200, 250, 40, 320)]),
    'd': glyph([ellipse(200, 250, 200, 250), line(400, AH, 400, 0)], [], [[200, 250]]),
    'e': glyph([path(line(0, 250, 400, 250), arc(200, 250, 200, 250, 0, 315))]),
    'f': glyph([path(line(110, 0, 110, 560), arc(250, 560, 140, 150, 180, 40)), line(0, XH, 250, XH)]),
    'g': glyph([ellipse(190, 270, 190, 230), path(line(380, XH, 380, -40), arc(190, -40, 190, 160, 0, -160))], [], [[190, 270]]),
    'h': glyph([line(0, AH, 0, 0), path(arc(200, 300, 200, 200, 180, 0), line(400, 300, 400, 0))]),
    'i': glyph([line(0, XH, 0, 0)], [{ x: 0, y: DOT_Y }]),
    'j': glyph([path(line(150, XH, 150, -40), arc(30, -40, 120, 150, 0, -150))], [{ x: 150, y: DOT_Y }]),
    'k': glyph([line(0, AH, 0, 0), line(330, XH, 0, 170), line(110, 250, 350, 0)]),
    'l': glyph([path(line(0, AH, 0, 90), arc(90, 90, 90, 90, 180, 300))]),
    'm': glyph([
        line(0, XH, 0, 0),
        path(arc(150, 320, 150, 180, 180, 0), line(300, 320, 300, 0)),
        path(arc(450, 320, 150, 180, 180, 0), line(600, 320, 600, 0)),
    ]),
    'n': glyph([line(0, XH, 0, 0), path(arc(200, 300, 200, 200, 180, 0), line(400, 300, 400, 0))]),
    'o': glyph([ellipse(210, 250, 210, 250)], [], [[210, 250]]),
    'p': glyph([line(0, XH, 0, DH), ellipse(200, 250, 200, 250)], [], [[200, 250]]),
    'q': glyph([ellipse(200, 250, 200, 250), path(line(400, XH, 400, DH + 20), line(400, DH + 20, 470, DH + 70))], [], [[200, 250]]),
    'r': glyph([line(0, XH, 0, 0), arc(220, 280, 220, 220, 180, 60)]),
    's': glyph([path(arc(190, 375, 175, 125, 20, 270), arc(190, 125, 185, 125, 90, -160))]),
    't': glyph([path(line(100, AH - 110, 100, 90), arc(200, 90, 100, 90, 180, 300)), line(0, XH, 270, XH)]),
    'u': glyph([path(line(0, XH, 0, 200), arc(200, 200, 200, 200, 180, 360), line(400, 200, 400, XH)), line(400, XH, 400, 0)]),
    'v': glyph([path(line(0, XH, 200, 0), line(200, 0, 400, XH))]),
    'w': glyph([path(line(0, XH, 150, 0), line(150, 0, 300, 360), line(300, 360, 450, 0), line(450, 0, 600, XH))]),
    'x': glyph([line(0, XH, 400, 0), line(0, 0, 400, XH)]),
    'y': glyph([line(0, XH, 210, 40), path(line(410, XH, 150, DH + 20), arc(80, DH + 20, 70, 50, 0, -120))]),
    'z': glyph([path(line(0, XH, 380, XH), line(380, XH, 0, 0), line(0, 0, 380, 0))]),
    'ß': glyph([path(
        line(0, 0, 0, 560),
        arc(170, 560, 170, 160, 180, -50),
        line(279, 437, 160, 370),
        arc(170, 185, 200, 185, 90, -140),
    )]),
}

// ===========================================================================
// ==============================   UPPERCASE   ==============================
// ===========================================================================
//
//   UPPERCASE   A-Z, geometric and rounded, no serifs.
//

const UPPERCASE = {
    'A': glyph([path(line(0, 0, 260, CH), line(260, CH, 520, 0)), line(95, 240, 425, 240)]),
    'B': glyph([
        line(0, 0, 0, CH),
        path(line(0, CH, 220, CH), arc(220, 530, 170, 170, 90, -90), line(220, 360, 0, 360)),
        path(line(0, 360, 250, 360), arc(250, 180, 180, 180, 90, -90), line(250, 0, 0, 0)),
    ]),
    'C': glyph([arc(320, 350, 320, 350, 45, 315)]),
    'D': glyph([line(0, 0, 0, CH), path(line(0, CH, 180, CH), arc(180, 350, 320, 350, 90, -90), line(180, 0, 0, 0))]),
    'E': glyph([path(line(430, CH, 0, CH), line(0, CH, 0, 0), line(0, 0, 430, 0)), line(0, 360, 340, 360)]),
    'F': glyph([path(line(430, CH, 0, CH), line(0, CH, 0, 0)), line(0, 360, 340, 360)]),
    'G': glyph([path(arc(320, 350, 320, 350, 45, 340), line(621, 230, 621, 330), line(621, 330, 420, 330))]),
    'H': glyph([line(0, 0, 0, CH), line(500, 0, 500, CH), line(0, 360, 500, 360)]),
    'I': glyph([line(0, 0, 0, CH)]),
    'J': glyph([path(line(330, CH, 330, 200), arc(165, 200, 165, 200, 0, -165))]),
    'K': glyph([line(0, 0, 0, CH), line(450, CH, 0, 240), line(160, 390, 470, 0)]),
    'L': glyph([path(line(0, CH, 0, 0), line(0, 0, 400, 0))]),
    'M': glyph([path(line(0, 0, 0, CH), line(0, CH, 300, 250), line(300, 250, 600, CH), line(600, CH, 600, 0))]),
    'N': glyph([path(line(0, 0, 0, CH), line(0, CH, 500, 0), line(500, 0, 500, CH))]),
    'O': glyph([ellipse(330, 350, 330, 350)], [], [[330, 350]]),
    'P': glyph([line(0, 0, 0, CH), path(line(0, CH, 230, CH), arc(230, 520, 180, 180, 90, -90), line(230, 340, 0, 340))]),
    'Q': glyph([ellipse(330, 350, 330, 350), line(400, 180, 660, -60)], [], [[310, 380]]),
    'R': glyph([
        line(0, 0, 0, CH),
        path(line(0, CH, 230, CH), arc(230, 520, 180, 180, 90, -90), line(230, 340, 0, 340)),
        line(220, 340, 460, 0),
    ]),
    'S': glyph([path(arc(250, 525, 230, 175, 20, 270), arc(250, 175, 245, 175, 90, -160))]),
    'T': glyph([line(0, CH, 520, CH), line(260, CH, 260, 0)]),
    'U': glyph([path(line(0, CH, 0, 260), arc(260, 260, 260, 260, 180, 360), line(520, 260, 520, CH))]),
    'V': glyph([path(line(0, CH, 270, 0), line(270, 0, 540, CH))]),
    'W': glyph([path(line(0, CH, 190, 0), line(190, 0, 380, 470), line(380, 470, 570, 0), line(570, 0, 760, CH))]),
    'X': glyph([line(0, CH, 500, 0), line(0, 0, 500, CH)]),
    'Y': glyph([path(line(0, CH, 250, 330), line(250, 330, 500, CH)), line(250, 330, 250, 0)]),
    'Z': glyph([path(line(0, CH, 480, CH), line(480, CH, 0, 0), line(0, 0, 480, 0))]),
}

// ===========================================================================
// ===============================   DIGITS   ================================
// ===========================================================================
//
//   DIGITS   0-9 at cap height, the 0 holds a nucleus bubble to tell it from O.
//

const DIGITS = {
    '0': glyph([ellipse(230, 350, 230, 350)], [{ x: 230, y: 350, scale: 0.9 }]),
    '1': glyph([path(line(30, 540, 230, CH), line(230, CH, 230, 0))]),
    '2': glyph([path(arc(215, 500, 205, 200, 160, -30), line(392, 400, 0, 0), line(0, 0, 430, 0))]),
    '3': glyph([path(arc(210, 530, 200, 170, 150, -90), arc(210, 185, 220, 185, 90, -150))]),
    '4': glyph([path(line(330, 0, 330, CH), line(330, CH, 0, 220), line(0, 220, 450, 220))]),
    '5': glyph([path(line(410, CH, 50, CH), line(50, CH, 30, 400), arc(220, 230, 220, 230, 130, -150))]),
    '6': glyph([line(370, CH, 60, 320), ellipse(230, 230, 220, 230)], [], [[230, 230]]),
    '7': glyph([path(line(0, CH, 440, CH), line(440, CH, 130, 0))]),
    '8': glyph([ellipse(220, 535, 180, 165), ellipse(220, 185, 220, 185)]),
    '9': glyph([ellipse(210, 470, 210, 230), line(410, 400, 100, 0)], [], [[210, 470]]),
}

// ===========================================================================
// =============================   PUNCTUATION   =============================
// ===========================================================================
//
//   PUNCTUATION   Dots are bubbles, everything else plain strokes.
//

const DOT_LOW = 60          // center of a period bubble
const MID_Y = 300           // vertical center of dashes, arrows and operators

const PUNCTUATION = {
    '.': glyph([], [{ x: 0, y: DOT_LOW }]),
    ',': glyph([line(30, 40, -40, -130)], [{ x: 30, y: DOT_LOW }]),
    ':': glyph([], [{ x: 0, y: DOT_LOW }, { x: 0, y: 420 }]),
    ';': glyph([line(30, 40, -40, -130)], [{ x: 30, y: DOT_LOW }, { x: 30, y: 420 }]),
    '!': glyph([line(0, CH, 0, 250)], [{ x: 0, y: DOT_LOW }]),
    '?': glyph([path(arc(200, 520, 190, 180, 160, -60), line(295, 364, 200, 280), line(200, 280, 200, 230))], [{ x: 200, y: DOT_LOW }]),
    '\'': glyph([line(0, CH, 0, 520)]),
    '"': glyph([line(0, CH, 0, 520), line(160, CH, 160, 520)]),
    '-': glyph([line(0, MID_Y, 260, MID_Y)]),
    '–': glyph([line(0, MID_Y, 500, MID_Y)]),
    '—': glyph([line(0, MID_Y, 900, MID_Y)]),
    '+': glyph([line(0, MID_Y, 400, MID_Y), line(200, 100, 200, 500)]),
    '=': glyph([line(0, 200, 400, 200), line(0, 400, 400, 400)]),
    '/': glyph([line(0, -60, 340, CH + 20)]),
    '\\': glyph([line(0, CH + 20, 340, -60)]),
    '(': glyph([arc(270, 320, 270, 460, 125, 235)]),
    ')': glyph([arc(0, 320, 270, 460, 55, -55)]),
    '[': glyph([path(line(200, CH + 40, 0, CH + 40), line(0, CH + 40, 0, -120), line(0, -120, 200, -120))]),
    ']': glyph([path(line(0, CH + 40, 200, CH + 40), line(200, CH + 40, 200, -120), line(200, -120, 0, -120))]),
    '%': glyph([ellipse(110, 570, 100, 120), ellipse(430, 130, 100, 120), line(20, 0, 520, CH)]),
    '·': glyph([], [{ x: 0, y: MID_Y, scale: 0.8 }]),
    '…': glyph([], [{ x: 0, y: DOT_LOW }, { x: 230, y: DOT_LOW }, { x: 460, y: DOT_LOW }]),
    '→': glyph([line(0, MID_Y, 560, MID_Y), path(line(400, 460, 560, MID_Y), line(560, MID_Y, 400, 140))]),
    '←': glyph([line(0, MID_Y, 560, MID_Y), path(line(160, 460, 0, MID_Y), line(0, MID_Y, 160, 140))]),
    '×': glyph([line(0, 120, 360, 480), line(0, 480, 360, 120)]),
    '<': glyph([path(line(400, 520, 0, MID_Y), line(0, MID_Y, 400, 80))]),
    '>': glyph([path(line(0, 520, 400, MID_Y), line(400, MID_Y, 0, 80))]),
    '_': glyph([line(0, -100, 500, -100)]),
    '*': glyph([line(150, 420, 150, CH), line(30, 630, 270, 490), line(30, 490, 270, 630)]),
    '#': glyph([line(140, 0, 200, CH), line(360, 0, 420, CH), line(0, 220, 520, 220), line(30, 480, 550, 480)]),
    '°': glyph([ellipse(0, 600, 95, 100)]),
}

// ===========================================================================
// ===============================   UMLAUTS   ===============================
// ===========================================================================
//
//   withDots   Copies a base glyph and adds bubble dots above it.
//   UMLAUTS    ä ö ü at DOT_Y, Ä Ö Ü smaller at CAP_DOT_Y.
//

function withDots(base, dots) {
    return { strokes: base.strokes, dots: [...base.dots, ...dots], nuclei: base.nuclei }
}

const UMLAUTS = {
    'ä': withDots(LOWERCASE['a'], [{ x: 95, y: DOT_Y }, { x: 300, y: DOT_Y }]),
    'ö': withDots(LOWERCASE['o'], [{ x: 105, y: DOT_Y }, { x: 315, y: DOT_Y }]),
    'ü': withDots(LOWERCASE['u'], [{ x: 95, y: DOT_Y }, { x: 305, y: DOT_Y }]),
    'Ä': withDots(UPPERCASE['A'], [{ x: 165, y: CAP_DOT_Y, scale: CAP_DOT_SCALE }, { x: 355, y: CAP_DOT_Y, scale: CAP_DOT_SCALE }]),
    'Ö': withDots(UPPERCASE['O'], [{ x: 215, y: CAP_DOT_Y, scale: CAP_DOT_SCALE }, { x: 445, y: CAP_DOT_Y, scale: CAP_DOT_SCALE }]),
    'Ü': withDots(UPPERCASE['U'], [{ x: 165, y: CAP_DOT_Y, scale: CAP_DOT_SCALE }, { x: 355, y: CAP_DOT_Y, scale: CAP_DOT_SCALE }]),
}

export default {
    ...LOWERCASE,
    ...UPPERCASE,
    ...DIGITS,
    ...PUNCTUATION,
    ...UMLAUTS,
}
