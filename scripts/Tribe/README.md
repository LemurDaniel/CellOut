# Tribe

The social stages after the land animal: the pack (a nest with conspecifics that follow) and the tribe (the pack settles around a fire, the player commands villagers from a strategy view). `Main` creates the components, hands them the `WorldSession` and drives them once per physics frame.

| File / Folder | Content |
| --- | --- |
| `TribePanel.cs` | HUD of both stages: pack size, the F hint, the offered action button, the stock line, a build button per building function, the design button and the rival rows. |
| `Pack/` | Conspecifics (`Kin`), the nest and the `PackKeeper` that spawns, steers and founds pack and tribe. |
| `Village/` | The tribe: `TribeCommander` (selection, orders, gathering, chopping, buildings), `TribeEconomy` (food and wood, pure logic), `Hut` (a building in the world). |
| `Building/` | Buildings as blueprints: `BuildingDesign` (function plus cosmetic blocks on a grid, pure logic), `BlockCatalog`, `BuildingTemplates`, `BuildingVisual3D` and the full screen village builder in `Editor/`. |
| `Rivals/` | Neighbor tribes: `RivalTribe` (a village with relation), `RivalKeeper` (settling, traders, raids, fights, gifts). |
