# Creature

Organism and land animal: the body plan, the creature parts and the coast.

| File | Content |
| --- | --- |
| `CreatureCatalog.cs` | Every creature part: category tab, cost, weight, mount (tail, head, skull, back, flank, limb, inside), body type, what legs carry and wings lift. |
| `CreatureDesign.cs` | Body plan of an organism: spine girths and heights (bending, growing at the ends), placed parts with pose and sculpted limb joints, geometry (profile, spine curve, inside test), starter and NPC plans. |
| `CreatureStats.cs` | Traits of a body plan (weight, carry, swim and land speed, health, range, brain EP, flight ratio): one formula for Player and editor. |
| `CreatureVisual3D.cs` | Procedural 3D organism: clay body lofted along the spine, fins, eyes, jaw, spines, blowhole, swim wave. |
| `Coast.cs` | Archipelago of the organism stage: up to 8 big islands with rolling hills (outline shared with the shaders), rocky sea floor fading into the Backdrop, beach, dunes, meadow on a hill, sea surface with waves and foam, seeded props, primeval forests with clearings in chunks (trunks block the way), land checks. |
| `Editor/` | The creature editor, see `Editor/README.md`. |
