# Cell/Editor/Panel

The 2D side of the cell editor: the sliding panel and the part palette in it. Both are views, `CellEditor` feeds them and reacts to their events.

| File | Content |
| --- | --- |
| `CellEditorPanel.cs` | Grey-out over the paused game, the panel sliding in from the left with header (title, diet bar), body and footer (info card, points badge, done button), diet tint. |
| `PartPalette.cs` | Scrolling icon tiles per diet class (locked ones dimmed), reports tile clicks and hover, starts the drag of a part. |
