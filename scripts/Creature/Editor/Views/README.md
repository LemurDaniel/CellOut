# Creature/Editor/Views

The panels of the creature editor around and over the turntable. Views: each builds one panel in `Setup(session)`, reports clicks through C# events and shows the session in `Refresh()`. `CreatureEditorFrame` stacks them, the tool column stands right of the stage.

| File | Content |
| --- | --- |
| `CreatureTemplateCards.cs` | The template library of the first shaping as a grid of cards, each with a live creature, its name and what its parts cost. |
| `CreatureCategoryTabs.cs` | Index card tabs per part category with the count of placed parts, the shown one taller. |
| `CreaturePartPalette.cs` | Floating icon tiles of the shown tab: name, cost, weight, selected and affordable state, hover and drag start. |
| `CreatureToolbar.cs` | The tool column right of the turntable: an icon each for body color, skin surface and paint pattern (a click unfolds the choices), spray can with undo and wipe, posture and hover sliders, the two symmetry icons, - / + vertebrae. |
| `SkinIcon.cs` | A round button that draws a skin surface (smooth, scales, warts) in a body color: the skin icon of the toolbar and its tiles, with a pattern set the pattern icon. |
| `SymmetryIcon.cs` | A square button that draws axis symmetry (mirrored halves) or point symmetry (a ring of dots with its count). |

| `CreatureFooter.cs` | Info card of the part in focus, size slider of the picked part, points badge and the done button. |
