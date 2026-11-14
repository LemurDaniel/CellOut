# Tribe

The social stages after the land animal: the pack (a nest with conspecifics that follow) and the tribe (the pack settles around a fire, the player commands villagers from a strategy view). `Main` creates the components, hands them the `WorldSession` and drives them once per physics frame.

| File / Folder | Content |
| --- | --- |
| `TribePanel.cs` | HUD of both stages: pack size, the F hint, the offered action button, the stock line and the build button. |
| `Pack/` | Conspecifics (`Kin`), the nest and the `PackKeeper` that spawns, steers and founds pack and tribe. |
| `Village/` | The tribe: `TribeCommander` (selection, orders, gathering, huts), `TribeEconomy` (the stock, pure logic), `Hut`. |
