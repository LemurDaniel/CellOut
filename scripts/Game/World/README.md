# Game/World

The components of the running world. `Main` creates them, hands them the `WorldSession` and drives them once per frame, they report back through signals.

| File | Content |
| --- | --- |
| `WorldSession.cs` | Shared state: the player, the food and critter layer, the coast once it showed up. |
| `FollowCamera.cs` | Chase camera with environment, sun, sky and the water (`Water/`): wheel zoom, orbit, third person over the islands, planet curve, view radius, graphics tier, water colors, depth fog and depth of field. |
| `ContactResolver.cs` | Contacts per physics frame: eating food, engulfing with pseudopods (player and cells), pushing apart, the kill reward. |
| `Spawner.cs` | Fills the starting soup and the new sea, refills food and cells outside the view, meat of popped cells. |
| `CoastKeeper.cs` | Spawns the coast of the organism stage, keeps it around the player and tells the player where land is. |
