# 🦠 Cell Out

*Inspired by Spore. 🌱👾*

> [!WARNING]
> **⚠️🤖 WARNING: THIS IS AN AI SLOP GAME 🤖⚠️**
>
> This game was 99% splattered together by an AI. The remaining 1% is the human who said "make it more fun" 47 times. 🔁
>
> Just like the primordial soup your cell swims in, this code **emerged spontaneously, is chaotic and probably not viable**. Bugs aren't errors, they're **mutations**. 🧬 They're part of evolution.
>
> Play at your own risk. For risks and side effects, consult your prompt or your GPU. 🔥🖥️

> [!CAUTION]
> **🚧 EARLY ALPHA 🚧**
>
> Work in progress: features are incomplete, balancing is rough, there is no saving yet, and stages and controls can change or break at any time. 🧪

> From primordial soup to multicellular life. An evolution game in Godot 4 (C#), realistically inspired, cartoony in 3D.

<p align="center"><img src="docs/images/cell.hero.png" alt="A cell built in the editor: humped back with two spikes, jaws, eyes, a flagellum and cilia" width="720"></p>

You start as a **protocell**, a lipid bubble with a floating DNA helix, collecting biomolecules. As you grow you develop a nucleus, eat smaller cells and mutate. Once you are big enough you **divide**: every cell splits, 1 → 2 → 4 → … → 64 cells stick together as one growing cluster.

## 📸 Gallery

| | |
|---|---|
| ![Herbivore with chloroplasts, filter mouth, armour plates and cilia](docs/images/cell.herbivore.png) | ![Predator with double jaws, a crown of spikes and venom vacuoles](docs/images/cell.carnivore.png) |
| 🌿 **Herbivore**: chloroplasts, filter mouth, armour plates, cilia | 🩸 **Predator**: double jaws, spike crown, venom vacuoles |
| ![Scout with five eyes, two flagella and cilia](docs/images/cell.scout.png) | ![Protocell: a lipid bubble with a DNA helix](docs/images/cell.protocell.png) |
| 🔭 **Scout**: five eyes, two flagella, cilia | 🧬 **Protocell**: a lipid bubble with a DNA helix |

Every part is placed freely on the membrane in the cell editor (around, on top, underneath), and the cell itself can be stretched and raised into any shape.

## 🐾 Creature gallery

Shaped in the creature editor: bend and stretch the spine, pull limbs out by their joints, branch them, stick feet and hands on the tips, paint patterns and spray your own. Results may vary. 🧪

The editor opens with a library of ready-made animals, each one built from the same parts you can buy yourself. Four of them:

| | |
|:---:|:---:|
| ![Sea turtle](docs/creatures/turtle.png) | ![Angler fish](docs/creatures/angler.png) |
| **Sea turtle** 🐢<br>A shell of honest hexagons, paddles for long calm strokes, the face of someone who has seen it all. | **Angler fish** 🏮<br>Mostly mouth. Brings its own night light, which is also the last thing its dinner sees. |
| ![Shark](docs/creatures/shark.png) | ![Puffer fish](docs/creatures/puffer.png) |
| **Shark** 🦈<br>Grey on top, white below, a grin with too many teeth. Everyone knows what the fin means. | **Puffer fish** 🐡<br>A ball of spikes with a permanent pout. Not fast, but nobody has ever asked twice. |

## ▶️ Getting started

There is no download build yet, so testers and developers both run the game from source.

> [!IMPORTANT]
> The game is written in C#. You need the **.NET** build of Godot (winget: `GodotEngine.GodotEngine.Mono`). The standard build (`GodotEngine.GodotEngine`) opens the project but cannot run it.

### 🧪 For testers: just play

1. Install Godot 4.7.2 .NET and the .NET SDK 8:

   ```powershell
   winget install GodotEngine.GodotEngine.Mono
   winget install Microsoft.DotNet.SDK.8
   ```

   On other systems: [Godot 4.7 **.NET**](https://godotengine.org/download) and the [.NET SDK 8+](https://dotnet.microsoft.com/download).
2. Get the code: `git clone` this repo, or **Code → Download ZIP** on GitHub and unpack it.
3. Start Godot, choose **Import** and select `project.godot` in this folder.
4. Press **F5**. The first start builds the C# project and imports the assets, which takes a moment.

**F10** opens a cheat menu that jumps straight to any stage, handy for testing the later ones.

### 🛠️ For developers

Same installs as above, plus [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) for the scripts in `tools/`. Open a new terminal afterwards so `dotnet` is on the PATH.

Build and run without the editor:

```powershell
# winget unpacks Godot here. Its "godot" alias only exists after an install with admin rights.
$godotDir = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64"

dotnet build
& "$godotDir\Godot_v4.7.2-stable_mono_win64.exe" --path .
```

Run the checks after every change (build, unit tests, import, headless smoke test):

```powershell
$env:GODOT = "$godotDir\Godot_v4.7.2-stable_mono_win64_console.exe"
./tools/Test-CellOut.ps1
```

Use the `*_console.exe` on Windows, the normal exe prints no output. The unit tests alone need no Godot: `dotnet test tests/unit/CellOut.Tests.csproj`.

Only needed when you change generated assets:

| Asset | Tool | Command |
|---|---|---|
| Fonts (`fonts/`) | Node.js | `npm install`, then `npm run build` in `tools/font` |
| Models (`assets/models/`) | Blender | `./tools/blender/Build-Models.ps1`, see `tools/blender/README.md` |

## 🎮 Controls

| Action | Key |
|---|---|
| Swim | `WASD` / arrow keys / hold left mouse button |
| Zoom | Mouse wheel |
| Cell editor (on every level-up) | Pick a part on the left, left click places, right click removes |
| Language DE/EN | Button top right (starts with the system language) |

Smaller cells you touch get engulfed and digested, bigger ones engulf you. Swim away before your health runs out: at zero it's **game over**.

**Diet bar** (top right): molecules and photosynthesis pull to the left (herbivore), eaten cells to the right (carnivore), editor parts in their own direction. The position unlocks parts: armour plate and slime coat on the left, cilia and camouflage pigment in the middle, jaws, suckers, venom vacuole and nematocysts on the right.

## 🧬 Stages

| Stage | From biomass | What changes |
|---|---|---|
| Protocell | 0 | Only absorb molecules (sugars, amino acids, lipids), no nucleus |
| Single cell | 40 | Nucleus, eat smaller cells, cell editor: place parts freely on the membrane (position matters) |
| Multicellular | first division | From 100 biomass you may divide (Space or the button), every cell splits and the cluster keeps its size. Each further division needs 2.5× the biomass, up to 64 cells. Every cell carries your editor design. Perks: a deadly hit only bursts a quarter of the cells, and each division lets you swallow 5% bigger prey |

Enemies and food scale with your size and the camera zooms out with you. That way the world gets "bigger" as you do.

## 🗂️ Structure

```
project.godot / CellOut.csproj               # Godot 4.7 .NET, C# (net8.0)
scenes/main.tscn                             # Entry scene, root script Game/Main.cs
scripts/                                     # By era first, domain second
  Core/    Plane3D, Rand, Ease, Log,         # 2D↔3D bridge, randomness, easing,
           PartModels, TreeModels            # verbose log, model loaders
  Game/    Main, Player, Food, Backdrop,     # Game loop, player, food, scenery,
           Cutscenes/, Ui/                   # cutscenes, HUD and menus
  Prelude/ RnaWorld, Mutation                # Playable RNA world
  Cell/    CellBody, CellVisual3D, Critter,  # Protocell to multicellular,
           PartCatalog, CellDesign, Editor/  # cell parts, cell editor
  Creature/ CreatureDesign, Coast, Editor/   # Organism to land animal, creature editor
shaders/   membrane, ocean, greyout,         # Cell membrane, ocean floor,
           editor_panel                      # editor grey-out and panel
locale/    translations.csv                  # All UI texts, German + English
fonts/     CellOut, CellOutDisplay           # Own game fonts: text + titles (generated)
data/      balance.json                      # Every tunable gameplay number
tests/     SmokeTest, unit/                  # Headless run through all stages, xUnit tests of the pure logic
tools/     Test-CellOut.ps1                  # Build + unit tests + import + smoke test
           font/                             # Node generator of the game font
```

No external assets: game logic in 2D, rendering in real 3D, everything procedural from primitives and shaders. Even the fonts are generated (`tools/font`, `npm run build`): a readable text font with wobbling membrane strokes and organelle-bubble dots, and a display font for titles with fat cytoplasm strokes full of vacuoles, nuclei in the bowls and bouncing letters.

## 🚀 Roadmap

- [ ] Stage 4: Organism (body plan, organs instead of single cells)
- [ ] Environment zones (deep sea, hydrothermal vent, light zone with photosynthesis bonus)
- [ ] Sound & music
- [ ] Save / load
- [ ] Mutations with visible trade-offs (e.g. armour = slower)
- [ ] GitHub Action: smoke test + Godot export for Windows (web export doesn't work with C#)
