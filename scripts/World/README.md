# World

The soup around the player: the game loop, food, scenery and effects.

| File | Content |
| --- | --- |
| `Main.cs` | Root of `scenes/main.tscn`: builds the world, camera, contacts, spawning, events. |
| `Food.cs` | Drifting biomolecules, rendered as 3D molecules. |
| `Backdrop.cs` | Sea floor with caustics and motes at several depths. |
| `Splash.cs` | Plop effect when something is eaten or pops. |
| `Cutscenes/` | Intro and evolve cutscene, see `Cutscenes/README.md`. |
| `Coast.cs` | Shore of the organism stage: sea floor rising to beach and meadow, water surface and foam, land checks. |
