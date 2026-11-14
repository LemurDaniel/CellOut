# Tribe/Building

The buildings of a village: a blueprint is a function plus blocks on a grid. The function sets cost and effect, the blocks are looks only.

| File / Folder | Content |
| --- | --- |
| `BuildingDesign.cs` | Pure logic: the blueprint. Function (`BuildingKind`), blocks on an 8 x 8 x 12 grid that drop and stack, costs and effects of the function from `tribe.buildings`. Tested in `tests/unit/TribeTests.cs`. |
| `BlockCatalog.cs` | Pure data: every block (`BlockKind`) with its palette tab, the cells it fills and its default paint, and the paints. |
| `BuildingTemplates.cs` | Pure data: ready-made layouts per function, the first one is what a new tribe builds, rival tribes pick at random. |
| `BuildingVisual3D.cs` | The look of a blueprint and of a single block in model units, for the world (`Village/Hut.cs`), the editor stage and the palette icons. |
| `Editor/` | The village builder: full screen editor with one draft per function. |