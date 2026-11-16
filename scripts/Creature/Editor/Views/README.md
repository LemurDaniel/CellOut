# Creature/Editor/Views

The panels of the creature editor around and over the turntable. Views: each builds one panel in `Setup(session)`, reports clicks through C# events and shows the session in `Refresh()`. `CreatureEditorFrame` stacks them; the panels left and right of the stage change with the mode (`CreatureEditorSession.Mode`).

| File | Content |
| --- | --- |
| `CreatureTemplateCards.cs` | The template library of the first shaping as a grid of cards, each with a live creature, its name and what its parts cost. |
| `CreatureCategoryTabs.cs` | Index card tabs per part category, each a small 3D icon of its first part (name as tooltip) with the count of placed parts, the shown one taller. Build mode only. |
| `CreaturePartPalette.cs` | Floating icon tiles of the shown tab: name, cost, weight, selected and affordable state, hover and drag start. |
| `CreatureModeBar.cs` | The bar in the header: the four modes Build / Paint / Animate / Sculpt and undo / redo. |
| `CreaturePanel.cs` | One framed, scrolling panel beside the turntable with headlines, slider rows and swatch rows; the toolbar and the motion panel build theirs from it. |
| `CreatureToolbar.cs` | The tools in three panels, shown by mode: paint panel (color with zones and the picked part, skin, pattern with its options), detail panel (spray can, mouth), form panel (posture, hover, symmetry, vertebrae, ring shape of the picked vertebra, look of the picked limb). |
| `CreatureShapePanel.cs` | The sculpt mode: the list of own parts (clay with a spine of its own), new, delete and "put on the animal". |
| `CreatureMotionPanel.cs` | The animation mode in two panels: preview and motion of the whole animal (swim wave, its pace, breathing, all parts on or off), and the own motion of the picked part (on or off, pace, swing, offset, blinking). |
| `SkinIcon.cs` | A round button that draws a skin surface (smooth, scales, warts) in a body color: the skin icon of the toolbar and its tiles, with a pattern set the pattern icon. |
| `SymmetryIcon.cs` | A square button that draws axis symmetry (mirrored halves) or point symmetry (a ring of dots with its count). |
| `CreatureFooter.cs` | Info card of the part in focus, size slider of the picked part, points badge and the done button. |
| `ZoneIcon.cs` | A small button that shows one color zone (body, belly, head, limbs, tail end) on a little animal in the color the zone wears. |
