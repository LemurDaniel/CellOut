# Creature/Editor

The full screen creature editor, from the organism stage on. Uses `Cell/Editor/PartGizmo.cs` and `Game/Ui/EditorStyle.cs`.

| File | Content |
| --- | --- |
| `CreatureEditor.cs` | Coordinator: public API, creates session, frame, stage components and views, wires their events, routes the stage input in a fixed order. Type choice (fish or marine mammal), then sculpting the clay on a turntable. |
| `CreatureEditorSession.cs` | Shared state and rules of one sitting, no node: draft, points, phase, mode (build, paint, animate), symmetry, spray mode, selection, picked part and vertebra, undo history; paying, refunding, posing. |
| `CreatureDraftHistory.cs` | Undo and redo of the draft: snapshots of the whole design with the points spent, one step per change at rest. Pure logic. |
| `CreatureEditorFrame.cs` | Grey-out and the full-screen sliding panel with a small title and the header bar, stacks the rows and the workbench. |
| `CreatureEditorUi.cs` | Static helpers: type accent color, translation keys of types and tabs, label and pill button factories. |
| `CreatureIcon.cs` | Live 3D creature or part icon in its own studio (counterpart of `PartIcon`). |
| `Stage/` | The turntable with the clay: sculpt handles, part placer, spray can, drop ghost, see `Stage/README.md`. |
| `Views/` | Type cards, category tabs, part palette, toolbar, stats column and footer, see `Views/README.md`. |
