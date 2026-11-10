# Scripts

C# sources of Cell Out (namespace `CellOut`), one class per file, file name = class name (Godot requirement).
Sorted by era first, by domain second. Code that spans several eras lives in `Game/`.

| Folder | Content |
| --- | --- |
| `Core/` | Helpers without game rules: 2D↔3D bridge, random, easing, verbose log, model loaders. |
| `Game/` | Game loop (`Main`), `Player`, food, scenery, cutscenes and the UI shared by all eras. |
| `Prelude/` | The playable RNA world before the protocell and its mutations. |
| `Cell/` | Protocell to multicellular: cell body, NPC cells, cell parts, diet, cell editor. |
| `Creature/` | Organism to land animal: body plan, creature parts, coast, creature editor. |
