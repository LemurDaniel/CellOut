# Cell

Protocell, single cell and multicellular: the cell body, the NPC cells, the cell parts and the diet.

| File | Content |
| --- | --- |
| `CellBody.cs` | Base of every cell (movement, traits, layout) plus the cluster layout of divided cells, `CellPart`, `Organelle`. |
| `CellVisual3D.cs` | Procedural 3D body of a cell incl. placed editor parts, shared meshes and materials. |
| `Critter.cs` | NPCs (bacterium, amoeba, hunter, colony; creatures in the organism stage) with flee/hunt/separate steering, colonies burst cells on deadly hits. |
| `ColonyRule.cs` | Pure rules of the NPC colonies: cell count relative to the player, size after a burst. |
| `PartCatalog.cs` | Every cell editor part: diet class, cost, placement, unlock range. |
| `Abilities/` | The abilities of the parts as pure logic, see `Abilities/README.md`. |
| `CellForms.cs` | Pure logic: body forms of the NPC cells (rods, commas, teardrops, lobes, beans, discs) per kind, wobbled. Tested in `tests/unit/CellTests.cs`. |
| `CellDesign.cs` | Placed parts of a cell (every cell of the cluster carries them), placement rules, random designs of the NPC cells. |
| `CellStats.cs` | What a cell keeps from its parts besides the abilities: thrust of the flagella, light of the sails, pull of the filter mouths and the directions of spikes, jaws, plates, stings and suckers. One formula for Player and NPC cells. |
| `DietTracker.cs` | Plant vs. meat meals, fading, mixed with the part lean. |
| `Editor/` | The cell editor, see `Editor/README.md`. |
