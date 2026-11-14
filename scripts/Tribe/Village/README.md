# Tribe/Village

The tribe from the strategy view.

| File | Content |
| --- | --- |
| `TribeCommander.cs` | Component of `Main`: left click and drag box select villagers (and the player), right click orders them, idle villagers gather fruit, the stock pays huts next to the fire, huts raise villagers, selection rings. |
| `TribeEconomy.cs` | Pure logic: the food stock, hut cost, paying for huts. Tested in `tests/unit/TribeTests.cs`. |
| `Hut.cs` | A hut on the ground: clay wall, grass roof, door; raises villagers over time. |
