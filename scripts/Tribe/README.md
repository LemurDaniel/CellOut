# Tribe

The social stages after the land animal: the pack (a nest with conspecifics that follow) and the tribe (the pack settles around a fire, the player commands villagers from a strategy view). `Main` creates `Society`, which creates the components, hands them the `WorldSession` and drives them once per physics frame.

| File / Folder | Content |
| --- | --- |
| `Society.cs` | Coordinator of the social stages: owns pack, encounters, tribe, rivals, diplomacy, placer, village builder and the two HUD layers, wires and advances them. Main only routes input and frames to it. |
| `TribeWays.cs` | Pure logic of the three ways (court, display, hunt) and the paths they lock into (pacifist, territorial, warlike): path from the diet, strength, display contest, progress and victory. Tested in `tests/unit/TribeTests.cs`. |
| `TribePanel.cs` | HUD of both stages: pack size, the F hint or the keys of the three ways at a stranger, the offered action button, the count of picked units, a build tile per building function (live icon of its blueprint and its cost) and the rival rows. |
| `TribeHub.cs` | The hub bar bottom left from the land animal on: it lies under the editor icon of the Hud and holds the builder button, the small map (click or M: the world map), the home button (click or H: the strategy view back over the own fire) and the counters of population, food and wood. |
| `WorldMap.cs` | The map of the archipelago drawn from the coast with a relief, the fires of all tribes, the own people, the player and the outline of what the strategy camera shows. Small in the hub, large as the world map. |
| `Pack/` | Conspecifics (`Kin`), the nest and the `PackKeeper` that spawns, steers and founds pack and tribe. |
| `Village/` | The tribe: `TribeCommander` (selection, orders, gathering, chopping, buildings), `TribeEconomy` (food and wood, pure logic), `Hut` (a building in the world). |
| `Building/` | Buildings as blueprints: `BuildingDesign` (function plus cosmetic blocks on a grid, pure logic), `BlockCatalog`, `BuildingTemplates`, `BuildingVisual3D`, `BuildingIcon` and the full screen village builder in `Editor/`. |
| `Rivals/` | Neighbor tribes: `RivalTribe` (a village with relation), `RivalKeeper` (settling, traders, raids, fights, gifts, razing their buildings until a tribe is defeated). |
