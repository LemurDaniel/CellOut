# Game

Everything that spans the eras: the game loop, the player, the soup around it, cutscenes and the UI.

| File | Content |
| --- | --- |
| `Main.cs` | Root of `scenes/main.tscn`, coordinator: builds the world, creates and wires the components of `World/` and `Flow/`, routes input and frames, reacts to player events (editors, stage changes, death), evolve flow, cheats. |
| `Player.cs` | Player: biomass, stages, EP, diet, cell division, evolving into an organism, land, pack and tribe. Holds its traits as one `PlayerTraits`. |
| `PlayerTraits.cs` | Pure logic: every trait the player takes from the parts of its cell (plus mutations) or the body plan of its creature, and what spikes, jaws and plates do towards a direction. Tested in `tests/unit/PlayerTraitsTests.cs`. |
| `Growth.cs` | Growth rules as plain formulas (pure logic): body size from biomass, level curve, division steps, prey size, lost cells. Numbers from `data/balance.json`. |
| `Food.cs` | Food by habitat: molecules in the soup, algae, plankton, roe and meat in the sea, fruit, mushrooms and berries on the islands. |
| `Water/` | Sea floor, motes, the wake of swimming cells, water colors per stage and the lens with light shafts, see `Water/README.md`. |
| `Abilities/` | The abilities of the cell parts in the world: keys 1 to 4, auto abilities, NPC choices, clouds, slime, bolts, see `Abilities/README.md`. |
| `Deep/` | Giant cells deep in the background that rise and join the game once the player grew, see `Deep/README.md`. |
| `World/` | Camera, contacts, spawning and the coast keeper around the player, see `World/README.md`. |
| `Flow/` | Start flow (menu, prelude, intro) and the cutscene director, see `Flow/README.md`. |
| `Cutscenes/` | Intro, stage, editor and evolve cutscenes, see `Cutscenes/README.md`. |
| `Ui/` | HUD, menus, diet bar and the shared editor style, see `Ui/README.md`. |
