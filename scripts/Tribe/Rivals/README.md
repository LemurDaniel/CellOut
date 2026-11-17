# Tribe/Rivals

The foreign packs of the pack stage and the neighbor tribes they become.

| File | Content |
| --- | --- |
| `RivalKeeper.cs` | Component of `Society`: settles the foreign packs around the nest (other species from the creature templates), turns them into tribes when the player's tribe is founded and settles further ones on the next islands. Friendly packs bring fruit, respectful ones pay tribute; friendly tribes send traders, hostile ones raids (never a respectful one). Resolves every fight between factions (the player's pack fights along), the raiders at the huts, gifts, relation and respect drift, kills (`KinKilled`). |
| `RivalTribe.cs` | One neighbor: a foreign pack at its den, later a tribe with fire, buildings from random templates and villagers; its species, relation and respect, its traders and raiders, `Release` hands an animal over to the player's pack. |
| `RivalDiplomacy.cs` | The path of the tribe: villagers on an errand court a tribe (pacifist) or display before it (territorial), the progress of the path and the victory banner. |
