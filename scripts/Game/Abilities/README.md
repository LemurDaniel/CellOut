# Game/Abilities

The abilities of the cell parts in the running world, the same rules for the player and the NPC cells. `Main` creates the resolver and calls it once per physics frame after the contacts.

| File | Content |
| --- | --- |
| `AbilityResolver.cs` | Coordinator: keys 1 to 4, the auto abilities (bolt, poison cloud, snap, grab), the choices of the NPC cells, shoves, cloak, slime laying. The table of every ability is in its header. |
| `AbilityZones.cs` | Poison clouds and slime patches: they belong to a side, hurt or slow the other side, drawn as puffs and glossy blobs. |
| `AbilityParticles.cs` | Short-lived particles (jet, spray, vortex, glint, dash) and jagged lightning bolts. |
