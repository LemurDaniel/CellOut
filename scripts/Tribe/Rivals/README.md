# Tribe/Rivals

The foreign packs of the pack stage and the neighbor tribes they become.

| File | Content |
| --- | --- |
| `RivalKeeper.cs` | Component of `Society`: settles the foreign packs around the nest (other species from the creature templates), turns them into tribes when the player's tribe is founded and settles further ones on the next islands. Friendly packs bring fruit, respectful ones pay tribute; friendly tribes send traders, hostile ones raids (never a respectful one). Resolves every fight between factions (the player's pack fights along), the raiders at the huts, gifts, relation and respect drift (an ally and the respect of a tribe stay for good), kills (`KinKilled`). `Everyone` adds the lone beasts of the `LonerKeeper` for encounters and fights. |
| `RivalTribe.cs` | One neighbor: a foreign pack at its den, later a tribe with fire, buildings from random templates and villagers; its species, relation and respect, its traders and raiders, `Release` hands an animal over to the player's pack. |
| `PackManners.cs` | Component of `Society`: how the foreign packs and the lone beast behave by their mood. Any animal looks at the leader that comes close; a hostile pack defends its den and chases up to the leash (a courtship calms one animal, a won display sends all home); friendly animals bow and carry the fruit of their pack to the leader in person, respectful ones duck and carry the tribute. |
| `LonerKeeper.cs` | Component of `Society`: the lone beast of the island, a `RivalTribe` of one big animal of a predator species (`IsLoner`) with its own lair. It prowls to the player's nest in the pack stage; the three ways work on it (hunt: a feast, display: `Humbled`, court: it joins the pack). Saved with the society. |
| `RivalDiplomacy.cs` | The path of the tribe: villagers on an errand court a tribe (pacifist) or display before it (territorial), the progress of the path and the victory banner. |
