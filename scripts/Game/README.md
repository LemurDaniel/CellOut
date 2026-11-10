# Game

Everything that spans the eras: the game loop, the player, the soup around it, cutscenes and the UI.

| File | Content |
| --- | --- |
| `Main.cs` | Root of `scenes/main.tscn`, coordinator: builds the world, creates and wires the components of `World/` and `Flow/`, routes input and frames, reacts to player events (editors, stage changes, death), evolve flow, cheats. |
| `Player.cs` | Player: biomass, stages, EP, diet, cell division, evolving into an organism, traits from the design or the creature. |
| `Growth.cs` | Growth rules as plain formulas (pure logic): body size from biomass, level curve, division steps, prey size, lost cells. Numbers from `data/balance.json`. |
| `Food.cs` | Food by habitat: molecules in the soup, algae, plankton, roe and meat in the sea, fruit, mushrooms and berries on the islands. |
| `Splash.cs` | Plop effect when something is eaten or pops. |
| `Water/` | Sea floor, motes, water colors per stage and the lens with light shafts, see `Water/README.md`. |
| `World/` | Camera, contacts, spawning and the coast keeper around the player, see `World/README.md`. |
| `Flow/` | Start flow (menu, prelude, intro) and the cutscene director, see `Flow/README.md`. |
| `Cutscenes/` | Intro, stage, editor and evolve cutscenes, see `Cutscenes/README.md`. |
| `Ui/` | HUD, menus, diet bar and the shared editor style, see `Ui/README.md`. |
