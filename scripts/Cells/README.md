# Cells

Everything that lives: the player, the NPC cells and how they look.

| File | Content |
| --- | --- |
| `CellBody.cs` | Base of every cell (movement, traits, layout) plus the cluster layout of divided cells, `CellPart`, `Organelle`. |
| `CellVisual3D.cs` | Procedural 3D body of a cell incl. placed editor parts, shared meshes and materials. |
| `Player.cs` | Player: biomass, stages, EP, diet, cell division, evolving into an organism, traits from the design or the creature. |
| `Critter.cs` | NPCs (bacterium, amoeba, hunter; creatures in the organism stage) with flee/hunt/separate steering. |
| `CreatureVisual3D.cs` | Procedural 3D organism: clay body lofted along the spine, fins, eyes, jaw, spines, blowhole, swim wave. |
