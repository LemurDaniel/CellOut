# Tribe/Village

The tribe from the strategy view.

| File | Content |
| --- | --- |
| `TribeCommander.cs` | Component of `Main`: left click and drag box select villagers (and the player), right click orders them (walk, chop a tree, attack an enemy), idle villagers gather fruit, the stock pays buildings next to the fire from one blueprint per function (`DesignOf`, `TryBuild`), dwellings raise villagers, selection rings. |
| `TribeEconomy.cs` | Pure logic: food and wood in stock, paying for buildings and gifts. Tested in `tests/unit/TribeTests.cs`. |
| `Hut.cs` | A building on the ground built from a blueprint (`Building/BuildingDesign.cs`): hit points, villagers it raises, smoke while one ripens, collapses under raids. |