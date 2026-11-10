# Creature/Editor/Views

The panels of the creature editor around and over the turntable. Views: each builds one panel in `Setup(session)`, reports clicks through C# events and shows the session in `Refresh()`. `CreatureEditorFrame` stacks them, the toolbar lies over the stage.

| File | Content |
| --- | --- |
| `CreatureTypeCards.cs` | Two cards of the first shaping, fish or marine mammal, each with a live example creature. |
| `CreatureCategoryTabs.cs` | Index card tabs per part category with the count of placed parts, the shown one taller. |
| `CreaturePartPalette.cs` | Floating icon tiles of the shown tab: name, cost, weight, selected and affordable state, hover and drag start. |
| `CreatureToolbar.cs` | Buttons floating over the turntable: + / - vertebrae, symmetry switch, spray can with undo and wipe, body colors, pattern pills and the second color. |
| `CreatureStatsColumn.cs` | Traits of the draft (`CreatureStats`) as values and bars: weight, carry, land speed, flight, swim speed, agility, health, range, EP. |
| `CreatureFooter.cs` | Info card of the part in focus, size slider of the picked part, points badge and the done button. |
