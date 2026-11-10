# World/Prelude

Playable sections before the protocell. They run while the game is paused, Main frees them when they are done.

| File | Content |
| --- | --- |
| `RnaWorld.cs` | RNA world: a ribozyme strand copies a random template by swallowing pairing nucleotides (A-U, G-C), wrong bases stick as mutations (a rolled trait from `Evolution/Mutation.cs` plus EP, kept through the cell phase), rival strands eat nucleotides and bite chains apart (the player bites back), a lipid vesicle ends it and hands over to the Intro. Tab skips. |
