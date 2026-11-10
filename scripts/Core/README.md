# Core

Shared helpers without game rules.

| File | Content |
| --- | --- |
| `Plane3D.cs` | Bridge between the 2D logic plane and the 3D rendering, mouse projection. |
| `Rand.cs` | Random ranges, angles, chances and indices. |
| `Ease.cs` | Easing curves of the cutscenes (smoothstep, ease in-out, overshoot, beat, time window). |
| `Balance.cs` | Every tunable gameplay number, typed and checked, read from `data/balance.json` (see `data/README.md`). |
| `Log.cs` | Verbose decision log, enabled with `CELLOUT_VERBOSE=1`. |
| `PartModels.cs` | Loads the generated part models (`assets/models`), recolors them by material name, starts their loop animation. |
| `TreeModels.cs` | Loads the generated trees (`assets/models/tree`) as one mesh per species with the wind shader, trunk sizes for collisions. |
