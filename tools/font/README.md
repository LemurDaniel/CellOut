# Font generator

Builds the two game fonts from the same glyph skeletons, one profile each in `data/profiles.js`:

- `fonts/CellOut.otf` (text): round, wobbling membrane strokes with droplet ends, readable at 13 px.
- `fonts/CellOutDisplay.otf` (titles, banners): fat strokes full of vacuole holes, nuclei in the bowls, bouncing and tilting letters.

Dots become organelle bubbles with a highlight. Never edit the `.otf` files by hand, change skeletons or profiles and rebuild.

```powershell
npm install      # once
npm run build    # writes ../../fonts/CellOut.otf
npm test         # every game text character has a glyph
```

| File | Content |
| --- | --- |
| `build.font.js` | Entry: builds the font and writes the `.otf` into the Godot project. |
| `data/glyphs.js` | Skeleton per glyph (strokes, bubble dots, nuclei) and the vertical metrics. |
| `data/profiles.js` | The two font variants: stroke, wobble, droplets, bounce, tilt, vacuoles, nuclei. |
| `utils/` | Skeleton primitives, stroke outlines and the font assembly, see its README. |
| `test/unit/font.coverage.test.js` | Both fonts: coverage of `locale/translations.csv`, outlines, metrics. |
