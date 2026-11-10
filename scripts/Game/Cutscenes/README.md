# Game/Cutscenes

Animations that run while the game is paused, Main frees them when they are done, the editor transition frees itself after its color faded.

| File | Content |
| --- | --- |
| `Intro.cs` | Start animation: molecules link into chains, lipids form a shell, the membrane closes around an RNA strand, two heartbeats in a warm light. Skippable. Only without the RNA prelude (cutscene reel), after it `Prelude/View/RnaGenesis.cs` forms the cell. |
| `StageCutscene.cs` | Stage advance (single cell, multicellular, land animal): the camera glides in under a soft light, nutrients sink in, bubbles rise or pollen drifts. |
| `EditorTransition.cs` | Before an editor: the camera dives into the body, the screen fills with its color, the editor opens under it. |
| `EvolveCutscene.cs` | 64 cells divide on to 512, curl up into a hollow ball and melt into a clay blob. |
