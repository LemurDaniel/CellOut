// NOTE:
//   Builds both Cell Out font variants (text and display) and writes them to
//   fonts/ in the Godot project.
//   Requires: Node 18+ and `npm install` in tools/font.
//
//   npm run build        (in tools/font)
//   node build.font.js

import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import PROFILES from './data/profiles.js'
import { buildFont } from './utils/font.js'

const OUTPUT_DIR = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../fonts')

try {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true })
    for (const profile of Object.values(PROFILES)) {
        const font       = buildFont(profile)
        const outputFile = path.join(OUTPUT_DIR, profile.fileName)
        fs.writeFileSync(outputFile, Buffer.from(font.toArrayBuffer()))
        console.log(`[build.font] '${profile.familyName}' with ${font.glyphs.length} glyphs written to '${outputFile}'.`)
    }
} catch (exception) {
    console.error(exception.stack)
    process.exitCode = 1
}
