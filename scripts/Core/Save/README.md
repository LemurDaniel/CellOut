# Core/Save

Saved games (one JSON per planet, `user://saves/<PLANET>.json`). Settings and the progress across games live elsewhere (`Core/UserSettings.cs`, `Core/PlanetProgress.cs`).

| File | Content |
| --- | --- |
| `SaveGame.cs` | Plain records of a saved game (meta, world, player with designs, society) and the conversions of Godot types, creature parts and blueprints. |
| `SaveCodec.cs` | JSON in and out: camel case, infinity allowed, the layout version stamped, older layouts migrated, newer ones refused. |
| `SaveSlots.cs` | The files: path per planet, read (broken reads as none), atomic write over a temp file, delete. Headless writes nothing, `CELLOUT_SAVE_DIR` moves the folder for renders. |
