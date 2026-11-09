# Ui/Editor

The Spore-style editors: the cell editor and, from the organism stage on, the creature editor.

| File | Content |
| --- | --- |
| `CellEditor.cs` | Slides in from the left, greys out the paused game, icon tiles per diet dragged onto the rotatable 3D cell in its petri dish. |
| `PartIcon.cs` | Live 3D icon of one part (mini cell in its own SubViewport), plus the shared preview studio. |
| `EditorStyle.cs` | Colors, style boxes of tiles, pills and buttons, bouncy scale tweens. |
| `CreatureEditor.cs` | Full screen. Type choice (fish or marine mammal), then sculpting the clay on a turntable: vertebra handles, + / - vertebrae, symmetry switch, category tabs with part tiles dragged onto the clay, live stats column (weight, carry, land speed, flight). |
| `PartGizmo.cs` | Move arrows and turn rings on the picked creature part, hit test and drag deltas in screen pixels. |
