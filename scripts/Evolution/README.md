# Evolution

Rules of the cell and creature editors and the diet, no nodes.

| File | Content |
| --- | --- |
| `PartCatalog.cs` | Every editor part: diet class, cost, placement, unlock range. |
| `CellDesign.cs` | Placed parts of a cell (every cell of the cluster carries them), placement rules, random designs of the NPC cells. |
| `CellStats.cs` | Traits from the placed cell parts (speed, health, digestion, spikes, jaws, plates): one formula for Player and NPC cells. |
| `DietTracker.cs` | Plant vs. meat meals, fading, mixed with the part lean. |
| `Mutation.cs` | Traits of the RNA prelude mutations (health, speed, pull range, digestion, good or bad), the roll, EP per mutation. |
| `CreatureCatalog.cs` | Every creature part: category tab, cost, weight, mount (tail, head, skull, back, flank, limb, inside), body type, what legs carry and wings lift. |
| `CreatureStats.cs` | Traits of a body plan (weight, carry, swim and land speed, health, range, brain EP, flight ratio): one formula for Player and editor. |
| `CreatureDesign.cs` | Body plan of an organism: spine girths and heights (bending, growing at the ends), placed parts with pose and sculpted limb joints, geometry (profile, spine curve, inside test), starter and NPC plans. |
