# Tribe/Village

The tribe from the strategy view.

| File | Content |
| --- | --- |
| `TribeCommander.cs` | Component of `Main`: left click and drag box select villagers (and the player), right click orders them (walk, chop a tree, attack an enemy), idle villagers gather fruit, the stock pays buildings from one blueprint per function (`DesignOf`), placed where the player chose within the build radius of the fire (`CanBuildAt`, `TryBuildAt`; `TryBuild` takes the next free slot), dwellings raise villagers, selection rings. |
| `BuildingPlacer.cs` | Component of `Main`: after a build tile is pressed the building hangs at the mouse as a see-through preview, a left click builds it there, a right click or Escape lets go. |
| `TribeEconomy.cs` | Pure logic: food and wood in stock, paying for buildings and gifts. Tested in `tests/unit/TribeTests.cs`. |
| `Hut.cs` | A building on the ground built from a blueprint (`Building/BuildingDesign.cs`): hit points, villagers it raises, smoke while one ripens, collapses under raids. |