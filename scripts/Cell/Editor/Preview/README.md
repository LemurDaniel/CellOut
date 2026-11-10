# Cell/Editor/Preview

The 3D side of the cell editor: the cell in its petri dish and everything that acts on it. `CellEditor` routes the mouse input, the components share the `CellEditSession`.

| File | Content |
| --- | --- |
| `CellPreview.cs` | Color swatches above the rotatable 3D cell in its petri dish: orbit camera, picking under a pixel, punch and ring burst, reports input and drops. |
| `PartGhost.cs` | Ghost of the part in hand at its snapped spot and the hover dot on the membrane. |
| `PartPoser.cs` | Keeps the gizmo (`PartGizmo`) on the picked part, shifts and turns it by a gizmo drag. |
| `MembraneSculptor.cs` | Stretches the membrane by its edge, raises or flattens it by the height handles. |
| `MembraneGeometry.cs` | Pure math: rays against the membrane, where parts and handles sit, how a pull spreads to the neighbors. |
