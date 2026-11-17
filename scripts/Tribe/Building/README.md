# Tribe/Building

The buildings of a village: a blueprint is a function plus blocks on a grid. The function sets cost and effect, the blocks are looks only.

| File / Folder | Content |
| --- | --- |
| `BuildingDesign.cs` | Pure logic: the blueprint. Function (`BuildingKind`, six of them with the warrior hut), blocks on an 12 x 12 x 16 grid that drop and stack, costs and effects of the function from `tribe.buildings`. Tested in `tests/unit/TribeTests.cs`. |
| `BlockCatalog.cs` | Pure data: the 44 blocks by era (`BlockEra`: village, modern, space travel locked) and group (`BlockGroup`) of the palette, `BlockKind`, the cells a block fills, its default paint, the paints and the number of patterns. |
| `BuildingTemplates.cs` | Pure data: ready-made layouts per function, the first one is what a new tribe builds, rival tribes pick at random. |
| `BuildingVisual3D.cs` | The look of a blueprint and of a single block: the generated Blender model of the block (`assets/models/building/`, `tools/blender/models/buildings.py`) in its paint and pattern (`shaders/building_paint.gdshader`). For the world (`Village/Hut.cs`), the editor stage and the icons. |
| `BuildingIcon.cs` | Live 3D icon of a whole blueprint: the tabs of the builder and the build tiles of the HUD. |
| `Editor/` | The village builder: full screen editor with one draft per function. |