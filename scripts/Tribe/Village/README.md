# Tribe/Village

The tribe from the strategy view.

| File | Content |
| --- | --- |
| `TribeCommander.cs` | Component of `Main`: left click and drag box select villagers (and the player), right click orders them (walk, chop a tree, attack an enemy), idle villagers gather fruit, the stock pays buildings from one blueprint per function (`DesignOf`), placed where the player chose within the build radius of the fire (`CanBuildAt`, `TryBuildAt`; `TryBuild` takes the next free slot), dwellings raise villagers, selection rings. |
| `BuildingPlacer.cs` | Component of `Main`: after a build tile is pressed the building hangs at the mouse as a see-through preview, a left click builds it there, a right click or Escape lets go. |
| `TribeEconomy.cs` | Pure logic: food and wood in stock, paying for buildings and gifts. Tested in `tests/unit/TribeTests.cs`. |
| `Settlement.cs` | Pure logic: the fixed ground plan of a settlement, plots on two rings around the fire, the foundation, the ways (place, ring way, ways through the gates) and the radius of the wall. |
| `SettlementPlots.cs` | The staked out markers of the free plots of the player, owns the wall of the player. |
| `SettlementWall.cs` | The low wall of field stones and stakes around a settlement with a gate at every gate angle, looks only. |
| `Hut.cs` | A building on the ground built from a blueprint (`Building/BuildingDesign.cs`): hit points, villagers it raises, smoke while one ripens, collapses under raids. |