# Data

Tunable numbers of the game. The code holds the formulas, this folder holds the numbers.

| File | Content |
| --- | --- |
| `balance.json` | Every balancing value, typed and checked by `scripts/Core/Balance.cs`. Loaded once at start, so a change needs a restart of the game, not a rebuild. |

## Sections of `balance.json`

| Section | Used by | Content |
| --- | --- | --- |
| `player` | `Game/Growth.cs`, `Game/Player.cs` | Body size from biomass, level curve, evolution points, health, division steps, prey size, air on land. |
| `diet` | `Cell/DietTracker.cs` | Half-life of meals and the mix of eating and parts on the diet bar. |
| `cell.parts` | `Cell/PartCatalog.cs` | Cost and unlock range on the diet bar per cell part (`minDiet`/`maxDiet`, default -1 and 1 = always). |
| `cell.effects` | `Cell/CellStats.cs` | What every cell part does. |
| `cell.combat` | `Cell/CellStats.cs` | Arcs (radians) and damage of spikes, jaws and plates. |
| `critter` | `Cell/Critter.cs` | Eat ratios, senses and speed of the NPC cells. |
| `creature.parts` | `Creature/CreatureCatalog.cs` | Cost, weight and (legs) carry and stride or (wings) lift per creature part. |
| `creature.body`, `creature.land`, `creature.effects` | `Creature/CreatureStats.cs` | Health, speed and weight of the body, walking on land, what every creature part does. |
| `prelude` | `Prelude/Mutation.cs` | Evolution points and multipliers of the RNA world mutations. |

What a single value means is the comment next to its property in `Balance.cs`.

## Rules
- Strict JSON, no comments. Keys are camelCase, part and mutation kinds are written like their enum (`"FilterMouth"`).
- Every part kind and mutation kind needs its entry. A missing entry, a missing number or an unknown key fails at load with a message that names it.
- A new value: `required` property in `Balance.cs`, entry here and in `tests/unit/balance.fixture.json` (the frozen copy the unit tests run against).
- Not in here: spawn rates and contact values of `Game/World/` and the terrain of the coast, they are still constants in the code.
