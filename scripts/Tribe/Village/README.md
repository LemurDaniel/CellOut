# Tribe/Village

The tribe from the strategy view.

| File | Content |
| --- | --- |
| `TribeCommander.cs` | Component of `Main`: left click and drag box select villagers (and the player), right click orders them (walk, chop a tree, attack an enemy), idle villagers gather fruit, the stock pays huts of the blueprint next to the fire, huts raise villagers, selection rings. |
| `TribeEconomy.cs` | Pure logic: food and wood in stock, paying for huts and gifts. Tested in `tests/unit/TribeTests.cs`. |
| `HutDesign.cs` | Pure logic: the blueprint (wall, roof, add-ons) with its costs and effects from `tribe.huts`. Tested in `tests/unit/TribeTests.cs`. |
| `HutVisual3D.cs` | The look of a blueprint in unit size, for the world and the designer preview. |
| `Hut.cs` | A hut on the ground built from a blueprint: hit points, villagers it raises, smoke while one ripens, collapses under raids. |
| `HutEditor.cs` | The designer: live preview, wall and roof tabs, add-on toggles, costs and effects, apply. |
