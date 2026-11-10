# Cell/Editor

The Spore-style cell editor. Colors and tweens come from `Game/Ui/EditorStyle.cs`, the gizmo and the part icons are shared with the creature editor.

| File | Content |
| --- | --- |
| `CellEditor.cs` | Coordinator: public API, creates and wires the components, routes the preview mouse input in a fixed order, per-frame updates, locale change and drag end. |
| `CellEditSession.cs` | Shared session state: player, draft, points spent, selected and picked part; pays, refunds and commits. |
| `Panel/` | The sliding panel and the part palette, see `Panel/README.md`. |
| `Preview/` | The rotatable 3D cell with ghost, gizmo poser and membrane sculpting, see `Preview/README.md`. |
| `PartIcon.cs` | Live 3D icon of one part (mini cell in its own SubViewport), plus the shared preview studio. |
| `PartGizmo.cs` | Move arrows and turn rings on the picked part, hit test and drag deltas in screen pixels. |
