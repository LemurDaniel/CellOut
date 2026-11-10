# Cell/Editor

The Spore-style cell editor. Colors and tweens come from `Game/Ui/EditorStyle.cs`, the gizmo and the part icons are shared with the creature editor.

| File | Content |
| --- | --- |
| `CellEditor.cs` | Slides in from the left, greys out the paused game, icon tiles per diet dragged onto the rotatable 3D cell in its petri dish. |
| `PartIcon.cs` | Live 3D icon of one part (mini cell in its own SubViewport), plus the shared preview studio. |
| `PartGizmo.cs` | Move arrows and turn rings on the picked part, hit test and drag deltas in screen pixels. |
