# Prelude

Playable sections before the protocell. They run while the game is paused, Main frees them when they are done.

| File | Content |
| --- | --- |
| `RnaWorld.cs` | RNA world: a ribozyme strand copies a random template by swallowing pairing nucleotides (A-U, G-C), wrong bases stick as mutations (a rolled trait from `Mutation.cs` plus EP, kept through the cell phase), rival strands eat nucleotides and bite chains apart (the player bites back), a lipid vesicle ends it and hands over to the Intro. Tab skips. |
| `Mutation.cs` | Traits of the RNA prelude mutations (health, speed, pull range, digestion, good or bad), the roll, EP per mutation. They stay on the player through the cell phase. |
