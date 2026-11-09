# World

The soup around the player: the game loop, food, scenery and effects.

| File | Content |
| --- | --- |
| `Main.cs` | Root of `scenes/main.tscn`: builds the world, camera, contacts, spawning, events. |
| `Food.cs` | Drifting biomolecules, rendered as 3D molecules. |
| `Backdrop.cs` | Sea floor with caustics and motes at several depths. |
| `Splash.cs` | Plop effect when something is eaten or pops. |
| `Cutscenes/` | Intro and evolve cutscene, see `Cutscenes/README.md`. |
| `Coast.cs` | Shore of the organism stage: rocky sea floor fading into the Backdrop, beach, dunes and meadow, wobbling water line with waves and foam, seeded props (seaweed, rocks, shells, driftwood, grass, bushes, flowers), land checks. |
