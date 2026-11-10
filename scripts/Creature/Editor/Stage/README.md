# Creature/Editor/Stage

The 3D turntable of the creature editor and everything that acts on the clay. Node components: they get session and stage through `Setup(...)`, report through C# events and read no input on their own, `CreatureEditor` routes it.

| File | Content |
| --- | --- |
| `CreatureStage.cs` | SubViewport with orbit camera, turntable and the clay of the draft: press, orbit and zoom, the part, body point or joint under a pixel, landing marker and spray nozzle, punch and ring burst. |
| `CreatureSculptHandles.cs` | Vertebra handles, joint handles of the picked limb and the gizmo on the picked vertebra or part: hit tests, wheel, grab, drag, release, hover and the placement every frame. |
| `CreaturePartPlacer.cs` | Places parts on body and limbs and removes them: pays through the session, picks the new part, ring burst, verbose log of refused parts; what a click or a drop on the clay means. |
| `CreatureSprayCan.cs` | Spray can: nozzle size, strokes, mirrored dots and its input while the can is on. |
| `CreatureDropGhost.cs` | Drag and drop of palette tiles: the part at the cursor, the ghost where it would land, the drop reported to the placer. |
