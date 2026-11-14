# Tribe/Building/Editor

The village builder, opened by `Main` with B from the tribe on. The game is paused while it shows.

| File / Folder | Content |
| --- | --- |
| `BuildingEditor.cs` | Coordinator: public API (`Open`, `Finish`, `Cancel`, `Applied`, `Closed`), function tabs, footer, routes the input of the stage. |
| `BuildingEditorSession.cs` | Pure logic: a draft per function, the function shown, the picked block with turn and paint, dropping and removing. Tested in `tests/unit/TribeTests.cs`. |
| `BuildingStage.cs` | The 3D stage: orbit camera, ground with the grid, the draft, the cell under a pixel and the ghost of the picked block. |
| `Views/` | The palette on the left and the sidebar on the right. |