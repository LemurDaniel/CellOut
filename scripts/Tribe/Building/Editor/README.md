# Tribe/Building/Editor

The village builder, opened by `Main` with B from the tribe on. The game is paused while it shows.

| File / Folder | Content |
| --- | --- |
| `BuildingEditor.cs` | Coordinator: public API (`Open`, `Finish`, `Cancel`, `Applied`, `Closed`), icon tabs per function, footer, routes the input of the stage (drop, remove, repaint). |
| `BuildingEditorSession.cs` | Pure logic: a draft per function, the function shown, the block group shown, the picked block with turn, paint and pattern, dropping, removing and repainting. Tested in `tests/unit/TribeTests.cs`. |
| `BuildingStage.cs` | The 3D stage, full screen behind the panels and see-through around its content (the editor shows the blurred game behind it, `shaders/editor_blur.gdshader`): a patch of grass on sand with the grid, orbit camera, the draft, the cell under a pixel and the ghost of the picked block. |
| `Views/` | The palette on the left, the sidebar on the right and the template window (`TemplatePicker.cs`: live previews of village, modern and library templates, also the library of the pause menu). |

A placed block carries the gizmo of the cell and creature editors (`Cell/Editor/PartGizmo.cs`): `BuildingEditorSession` holds the picked block and applies shift, turn and scale to its pose, `BuildingStage` places the gizmo and picks blocks by the boxes of their meshes, `BuildingEditor` routes the drag. The raster of the cells is not drawn any more.
