# Game/Flow

What runs around the game instead of in it: the way into the game and the staging of its cutscenes. `Main` decides when, these classes play it and report the end through signals.

| File | Content |
| --- | --- |
| `StartFlow.cs` | Title menu over the paused soup, RNA prelude, intro and the birth of the protocell. |
| `CutsceneDirector.cs` | Stages the stage cutscene, the editor transition and the evolve cutscene and knows which one plays. |
