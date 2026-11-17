# Tribe/Pack

The pack of the land animal.

| File | Content |
| --- | --- |
| `PackKeeper.cs` | Component of `Society`: spawns wild kin on the island (they answer the call until the pack is full, also after the nest), steers them, lets the members share their meals with the leader, raises a pup at the nest as the pack grows, knows the offered action (call, nest, found the tribe), performs it on Space or the HUD button, F lets the pack stay or follow, a dead leader lives on in a member. |
| `PackFormation.cs` | Component of the `PackKeeper`: the places of the followers on the flanks of the leader (never behind it, the camera sits there) and their walk to food close by. |
| `PackEncounter.cs` | Keys 1 to 3 while leading a pack: court, display or hunt the closest animal of a foreign pack or the lone beast; a courtship calms a hostile animal, a pack lets only one animal go to the player; the hunt also lunges at the prey of the islands (`Game/World/Wildlife.cs`); `DisplayOdds` for the meter of the HUD, `DropFood` for meat and carried gifts. |
| `Kin.cs` | A conspecific: the player's body plan in a shade of its color, walks on land only, wild / following / staying / villager, pups grow up. Also every other land animal: the animals of foreign packs, the lone beast and prey (`Toughen`, `Pace`), with `Watch` and `Calm` for their body language. |
| `Fallen.cs` | The body of a land animal that just died: it tips onto its side, sinks together and frees itself. Pure look, put next to the kin by `Kin.Hurt`. |
| `Nest.cs` | The nest on the ground: mound, sticks, grass; the fire of the tribe once lit. |
