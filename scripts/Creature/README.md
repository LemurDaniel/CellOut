# Creature

Organism and land animal: the body plan, the creature parts and the coast.

| File | Content |
| --- | --- |
| `CreatureCatalog.cs` | Every creature part: category tab, mount (tail, head, skull, back, flank, limb, inside) and body type; cost, weight, what legs carry and wings lift come from `data/balance.json`. |
| `CreatureDesign.cs` | Coordinator of the body plan: owns the placed parts, keeps the public API, delegates to `Design/`. |
| `Design/` | Spine, placement rules, limb joints, look and ready-made plans (pure logic), see `Design/README.md`. |
| `CreatureStats.cs` | Traits of a body plan (weight, carry, swim and land speed, health, range, brain EP, flight ratio): one formula for Player and editor. |
| `CreatureVisual3D.cs` | Coordinator of the 3D organism: follows the body, rebuilds on change, clay materials, paint dots, swim phase, camera fit; delegates to `Visual/`. |
| `Visual/` | Parts on pivots, sculpted limbs, clay meshes, part models, swim wave and picking, see `Visual/README.md`. |
| `Coast.cs` | Coordinator of the archipelago of the organism stage: up to 8 big islands, keeps the public API (land checks, ground height, trees), delegates to `World/`. |
| `World/` | Island outline and ground profile (pure math), terrain, tree grid, ground and sea surface, props and forests, see `World/README.md`. |
| `Editor/` | The creature editor, see `Editor/README.md`. |
