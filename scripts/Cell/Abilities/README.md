# Cell/Abilities

The abilities of the cell parts as pure logic (decision of the user 2026-11-17: parts are abilities, not small stat bonuses). What happens in the world is in `Game/Abilities/`.

| File | Content |
| --- | --- |
| `AbilitySet.cs` | How each part fires (key, by itself, all the time, not at all), the key slots 1 to 4 in placement order, cooldowns (faster with more parts), running effects and the shield charges of the armor plates. Tested in `tests/unit/CellTests.cs`. |
