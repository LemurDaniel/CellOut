// NOTE:
//   The two font variants built from the same skeletons. "text" stays readable down
//   to 13 px, "display" goes all in for titles and banners: fat strokes full of
//   vacuole holes like cytoplasm, droplet ends, nuclei in the bowls, bouncing and
//   tilting letters.
//
//   import PROFILES from './data/profiles.js'
//   buildFont(PROFILES.display)

//
//   Chapters:
//     FONT PROFILES   One entry per generated font file.
//

// ===========================================================================
// ============================   FONT PROFILES   ============================
// ===========================================================================
//
//   text      Body font: bold round strokes, visible wobble and droplet ends, slight bounce.
//   display   Title font: fat strokes with vacuoles, strong wobble, nuclei, bounce and tilt.
//
//   Fields:
//     familyName / fileName   Name in Godot and the file in fonts/.
//     strokeRadius            Half the stroke width in font units.
//     wobble / wobbleLength   Relative radius change along a stroke and its wave length.
//     endBulge / bulgeLength  Extra radius at stroke ends (droplets) and how far it reaches.
//     bounce                  Max baseline shift per glyph in font units.
//     tilt                    Max rotation per glyph in degrees.
//     tube                    Inner channel radius as share of the stroke, 0 = solid.
//     vacuoleEvery            Arc length between vacuole holes in a stroke, 0 = none.
//     vacuoleRadius           Vacuole radius as share of the local stroke radius.
//     hasNuclei               Draws the nucleus bubbles listed in the glyph data.
//     ascender / descender    Vertical metrics, must hold the tallest bubble and deepest hook.
//

export default {
    text: {
        familyName:   'Cell Out',
        fileName:     'CellOut.otf',
        strokeRadius: 64,
        wobble:       0.11,
        wobbleLength: 38,
        endBulge:     0.32,
        bulgeLength:  95,
        bounce:       14,
        tilt:         0,
        tube:         0,
        vacuoleEvery: 0,
        vacuoleRadius: 0,
        hasNuclei:    false,
        ascender:     960,
        descender:    -340,
    },
    display: {
        familyName:   'Cell Out Display',
        fileName:     'CellOutDisplay.otf',
        strokeRadius: 96,
        wobble:       0.16,
        wobbleLength: 70,
        endBulge:     0.38,
        bulgeLength:  120,
        bounce:       55,
        tilt:         10,
        tube:         0,
        vacuoleEvery: 230,
        vacuoleRadius: 0.32,
        hasNuclei:    true,
        ascender:     1080,
        descender:    -430,
    },
}
