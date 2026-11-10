# Prelude/View

The components of the RNA prelude (`Prelude/RnaWorld.cs`): what is drawn and moved. `RnaWorld` creates them, steers them and decides who swallows or bites what.

| File | Content |
| --- | --- |
| `RnaPalette.cs` | Colors of the bases and strands, the shared glowing materials (per base, good and bad mutation) and the ring in the wanted color. |
| `RnaStrand.cs` | One strand, player or rival: head with eyes (and ring), beads and bonds of its `RnaChain`, swimming, breaking, the wander and bite rest of a rival. |
| `RnaSoup.cs` | The free nucleotides: beads with their letter, drifting and bobbing, the nearest one to a point. |
| `RnaVesicle.cs` | The lipid vesicle of the goal: pops up, wobbles and flashes, tells whether a point is inside. |
| `RnaHud.cs` | View: banner that fades out, goal, wanted base with the next ones and one line per mutation, all via `Tr()`. |
