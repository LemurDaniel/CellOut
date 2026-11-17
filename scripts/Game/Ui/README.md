# Game/Ui

Everything on screen that is not the world. Texts are keys in `locale/translations.csv`.

| File | Content |
| --- | --- |
| `Hud.cs` | Stage, goal, banners, action buttons, language switch; owns the dial. |
| `AbilitySlots.cs` | The key slots 1 to 4 of the cell abilities in an arc around the dial: part icon, key, recharge sweep, running ring. |
| `VitalsDial.cs` | The round dial bottom right: editor icon (DNA helix) in the middle, rings for health and progress, the diet gauge and the air arc around it. |
| `TitleMenu.cs` | Title menu over the paused soup: logo, start button, graphics tier and language switch. |
| `GameOverScreen.cs` | Game over over the paused soup: stage and biomass reached, try again or title menu. |
| `EditorStyle.cs` | Colors, style boxes of tiles, pills and buttons, bouncy scale tweens of both editors. |
| `GameFonts.cs` | The display font for titles and banners, the text font is the project default. |
| `DietBar.cs` | Herbivore / omnivore / carnivore bar with current and preview marker (the editors). |
| `CheatMenu.cs` | F10 test menu: jump forward to a stage, bring the coast, +10 EP. |
