# Game

Everything that spans the eras: the game loop, the player, the soup around it, cutscenes and the UI.

| File | Content |
| --- | --- |
| `Main.cs` | Root of `scenes/main.tscn`: builds the world, camera, contacts, spawning, events. |
| `Player.cs` | Player: biomass, stages, EP, diet, cell division, evolving into an organism, traits from the design or the creature. |
| `Food.cs` | Food by habitat: molecules in the soup, algae, plankton, roe and meat in the sea, fruit, mushrooms and berries on the islands. |
| `Backdrop.cs` | Sea floor with caustics and motes at several depths. |
| `Splash.cs` | Plop effect when something is eaten or pops. |
| `Cutscenes/` | Intro, stage, editor and evolve cutscenes, see `Cutscenes/README.md`. |
| `Ui/` | HUD, menus, diet bar and the shared editor style, see `Ui/README.md`. |
