# Creature/World

The parts of the coast (`Creature/Coast.cs`): the coast coordinates them and keeps its public API, nothing else creates them.

| File | Content |
| --- | --- |
| `IslandShape.cs` | Pure math: island outline (same formula as the shaders), land depth, the way back into the sea, landing point, placing of a neighbor island. |
| `GroundProfile.cs` | Pure math: ground height per land depth (sea floor, slope, beach, dunes, hills) from two noise values, lift above dry land. |
| `CoastTerrain.cs` | State of the terrain: islands, unit and the ground noise. Land depth, land check, `ToSea`, ground height. Shared by all components. |
| `TreeGrid.cs` | Seeded primeval trees per grid cell (forests, clearings, cycads on the dunes) and the push out of their trunks. |
| `CoastSurface.cs` | Component: one ground mesh per island and the sea surface, feeds the islands to both shaders, follows the player. |
| `CoastProps.cs` | Component: seeded props by band (seaweed, rocks, pebbles, shells, driftwood, grass, bushes, flowers) in three multimeshes. |
| `CoastForest.cs` | Component: the trees of `TreeGrid` as multimesh chunks per species around the player. |
