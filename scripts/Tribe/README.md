# Tribe

The social stages after the land animal: the pack (a nest with conspecifics that follow) and the tribe (the pack settles around a fire, the player commands villagers from a strategy view). `Main` creates the components, hands them the `WorldSession` and drives them once per physics frame.

| File / Folder | Content |
| --- | --- |
| `TribePanel.cs` | HUD of both stages: pack size, the F hint, the offered action button, the stock line and the build button. |
| `Pack/` | Conspecifics (`Kin`), the nest and the `PackKeeper` that spawns, steers and founds pack and tribe. |
| `Village/` | The tribe: `TribeCommander` (selection, orders, gathering, chopping, huts), `TribeEconomy` (food and wood, pure logic), `HutDesign` (blueprint with costs and effects, pure logic), `HutVisual3D` (the look of a blueprint), `Hut`, `HutEditor` (the designer). |
| `Rivals/` | Neighbor tribes: `RivalTribe` (a village with relation), `RivalKeeper` (settling, traders, raids, fights, gifts). |
