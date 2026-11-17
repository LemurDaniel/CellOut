# Game/Deep

Giant cells in the background of the cell stages: far bigger than the microscopic player, they drift deep below the plane and nothing touches them. Once the player grew enough, a giant rises to the plane and joins the game as a normal cell. `World/Spawner.cs` owns and advances them.

| File | Content |
| --- | --- |
| `DeepKeeper.cs` | Spawns up to `critter.giants.count` giants outside the view on a course through it, places them below the plane, raises them and hands them into the critter layer (`Surfaced`). |
| `DeepRule.cs` | Pure logic: spawn size, depth and upright squash between plane and sea floor, when a giant may surface, the rise. Tested in `tests/unit/DeepTests.cs`. |
