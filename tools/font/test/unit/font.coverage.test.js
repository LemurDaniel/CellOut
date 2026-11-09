// NOTE:
//   Checks both generated fonts against the game: every character of the game texts
//   has a glyph with a real outline, and the metrics hold every bubble and hook.
//
//   npm test             (in tools/font)

import assert from 'node:assert'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import PROFILES from '../../data/profiles.js'
import { buildFont } from '../../utils/font.js'

const TRANSLATIONS_FILE = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../locale/translations.csv')
const DIGITS_AND_SIGNS = '0123456789%/·→'      // numbers and separators the HUD formats in at runtime

// ===========================================================================
// ============================   FONT COVERAGE   ============================
// ===========================================================================
//
//   covers every character of the game texts   No game text falls back to another font.
//   draws an outline for every visible glyph   No glyph is empty apart from the space.
//   keeps every glyph inside the metrics       Bubbles stay under the ascender, hooks above the descender.
//

/** Every visible glyph of a font, without .notdef and space. */
const visibleGlyphs = font => Object.values(font.glyphs.glyphs)
    .filter(glyph => !['.notdef', 'space'].includes(glyph.name))

for (const profile of Object.values(PROFILES)) {
    describe(`${profile.familyName} font`, () => {
        const font = buildFont(profile)

        it('covers every character of the game texts', () => {
            // KEY,"en","de" per line -> only the en and de columns count
            const texts = fs.readFileSync(TRANSLATIONS_FILE, 'utf-8')
                .split('\n')
                .slice(1)
                .flatMap(row => row.split('","').map(cell => cell.replace(/^[A-Z_0-9]+,"/, '').replace(/"$/, '')))
                .join('') + DIGITS_AND_SIGNS
            const missing = [...new Set(texts)]
                .filter(char => '\r' !== char && ' ' !== char)
                .filter(char => 0 === font.charToGlyphIndex(char))

            assert.deepStrictEqual(missing, [])
        })

        it('draws an outline for every visible glyph', () => {
            const empty = visibleGlyphs(font)
                .filter(glyph => 0 === glyph.path.commands.length)
                .map(glyph => String.fromCodePoint(glyph.unicode))

            assert.deepStrictEqual(empty, [])
        })

        it('keeps every glyph inside the metrics', () => {
            const outside = visibleGlyphs(font)
                .filter(glyph => {
                    const box = glyph.getBoundingBox()
                    return box.y2 > font.ascender || box.y1 < font.descender
                })
                .map(glyph => String.fromCodePoint(glyph.unicode))

            assert.deepStrictEqual(outside, [])
        })
    })
}
