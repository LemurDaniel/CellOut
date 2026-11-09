# World

The soup around the player: the game loop, food, scenery and effects.

| File | Content |
| --- | --- |
| `Main.cs` | Root of `scenes/main.tscn`: builds the world, camera, contacts, spawning, events. |
| `Food.cs` | Food by habitat: molecules in the soup, algae, plankton, roe and meat in the sea, fruit, mushrooms and berries on the islands. |
| `Backdrop.cs` | Sea floor with caustics and motes at several depths. |
| `Splash.cs` | Plop effect when something is eaten or pops. |
| `Cutscenes/` | Intro and evolve cutscene, see `Cutscenes/README.md`. |
| `Coast.cs` | Archipelago of the organism stage: up to 8 big islands with rolling hills (outline shared with the shaders), rocky sea floor fading into the Backdrop, beach, dunes, meadow on a hill, sea surface with waves and foam (open sea in third person), seeded props (seaweed, rocks, shells, driftwood, grass, bushes, flowers), primeval forests with clearings in chunks (trunks block the way), land checks. |
