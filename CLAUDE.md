# CLAUDE.md – Cell Out

Evolutionsspiel in Godot 4.7 .NET (C#): (RNA-Welt als spielbare Vorstufe, `Prelude/`) → Protozelle → Einzeller → Mehrzeller → Lebewesen → Landtier. F10 öffnet ein Cheat-Menü mit Stufen-Sprüngen. Stand und Entscheidungen: `PROGRESS.md`.

## Regeln
- Code Style: Projekt-Skill `.claude/skills/code-style/` (Kopie des User-Skills, auf C#/PowerShell/JS/Python gekürzt; Header mit Usage, Kapitel-/Abschnitts-Banner, englische Kommentare und Bezeichner, `UPPER_SNAKE`-Konstanten, `is`/`has`-Bools, `null` links). Vor dem Schreiben laden.
- C#: Namespace `CellOut`, `public partial class`, Dateiname = Klassenname. Shelves unter `scripts/` primär nach Ära, sekundär nach Domain: `Core` (stage-neutrale Helfer), `Game` (Loop, `Player`, Cutscenes, `Ui`, alles über mehrere Ären), `Prelude` (RNA-Welt), `Cell` (Protozelle–Mehrzeller, `Editor/`), `Creature` (Lebewesen–Landtier, `Editor/`). Jeder Ordner mit README.
- Keine fremden Assets: Grafik prozedural in 3D (Primitive-Meshes + Shader in `shaders/`). Ausnahme: eigene, generierte Modelle aus `tools/blender/` (Builder je Familie in `models/*.py`, registriert in `build.models.py`, `Build-Models.ps1 [-Only tree/]` → `assets/models/creature/`, `cell/`, `tree/`), geladen über `Core/PartModels.cs` (Teile) bzw. `Core/TreeModels.cs` (Bäume, Multimesh), nie von Hand in Blender ändern. Neue Modell-Familie = neues Modul in `tools/blender/models/`. Logik bleibt 2D auf der Ebene, Umrechnung über `Plane3D`. Ab Landtier krümmt sich die Welt (Planet): neue Szenerie-Shader binden `shaders/planet.gdshaderinc` ein (`planet_bend` im vertex), Körper ziehen `Plane3D.Drop()` von ihrer Höhe ab.
- Neue Zell-Visuals in `CellVisual3D.BuildCell()` (Einheitsgröße, +X = vorne), Merkmal als Property in `CellBody` + in `ComputeSignature()` aufnehmen.
- Gameplay-Werte immer relativ zu `Player.BodySize()` (Spawngröße, Tempo, Schaden, Zoom). Keine absoluten Pixelwerte.
- Kopplung über `[Signal]`s, keine direkten Zugriffe von Zellen auf `Main`.
- Neues Editor-Teil: `PartKind` + Eintrag in `PartCatalog.PARTS` (Diät, Kosten, Platzierung, Freischalt-Bereich) + Wirkung in `Player.ApplyDesign()` + Modell im Generator (CELL PARTS) + Eintrag in `CellVisual3D.CELL_MODELS` + Platzierung in `CellVisual3D.BuildPlaced()` + `PART_<KIND>_NAME/_DESC` in `locale/translations.csv`.
- Neues Kreatur-Teil (Stufe Lebewesen): `CreaturePartKind` + Eintrag in `CreatureCatalog.PARTS` (Kategorie-Tab, Kosten, Gewicht, Mount, Körpertyp, ggf. Carry/Stride/Lift) + Wirkung in `CreatureStats.From()` (Player und Editor-Werte nutzen dieselbe Formel) + Visual in `CreatureVisual3D.BuildPart()` (an einem `AddPivot`, damit es die Schwimmwelle mitmacht; Füße und Hände (nur die Enden, sie sitzen auf der Spitze eines formbaren Glieds), Flügel, Flossen, Hirn, Stielaugen als Blender-Modell: Builder im Generator, Eintrag in `CreatureVisual3D.MODELS`, Grundgröße in `BASE_PART_SIZES`; Ausnahme: das formbare Glied `Limb` ist eine prozedurale Röhre durch seine Gelenke, `CreatureVisual3D.BuildLimb`) + `CREATURE_<KIND>_NAME/_DESC` in `locale/translations.csv`.
- Neue Stufe: `Player.STAGE_KEYS` (+ `STAGE_THRESHOLDS`, falls per Biomasse) + `AdvanceStage()` + Banner in `Main.OnStageChanged()`. Mehrzeller kommt über die erste `Player.Divide()`, nicht per Schwelle. Lebewesen kommt über `Player.RequestEvolve()` → `Main.StartEvolve()` (Cutscene + Kreatur-Editor) → `Player.BecomeOrganism()`.
- Biomasse immer mit `BiomassSource` (Plant/Meat/Light/Raw) vergeben, davon hängt die Ernährungs-Bar ab.
- Schrift: `fonts/CellOut.otf` (Text, Projekt-Default) und `fonts/CellOutDisplay.otf` (Titel/Banner via `GameFonts.UseDisplay()`) sind generiert (`tools/font`, `npm run build`), nie von Hand ändern. Display nur ab ~26 px. Neues Zeichen in einem Text → Skelett in `tools/font/data/glyphs.js`, neu bauen, `npm test` prüft die Abdeckung von `translations.csv`.
- UI-Texte nie hart im Code: Schlüssel in `locale/translations.csv` (en + de), übersetzt im `Hud` per `Tr()`. Titel bleibt „Cell Out“.
- Tempo nur über `CellBody.SwimSpeed()` (eine Formel für Spieler und NPCs).

## Testen
Godot .NET liegt unter `$env:USERPROFILE\Downloads\Godot_v4.7.2-stable_mono_win64\...\Godot_v4.7.2-stable_mono_win64_console.exe` (die winget-Version ohne .NET kann kein C#).
Nach jeder Code-Änderung: `./tools/Test-CellOut.ps1 -GodotPath <mono console exe>` (oder `$env:GODOT`): `dotnet build`, Import, headless Smoke-Test.
Headless kompiliert keine Shader: bei Visual-/Shader-Änderungen zusätzlich mit `--write-movie <dir>/f.png --fixed-fps 30 --quit-after 120` rendern und Frames ansehen.
Neue Mechanik → `tests/SmokeTest.cs` erweitern. Eine GitHub Action gibt es noch nicht.

## Git
Commits als `LemurDaniel <landau.daniel.1998@gmail.com>`, Conventional Commits auf Deutsch.

## Claude-Code-Tipps-Review
- Durchgeführt am 2026-11-09. Umgesetzt: CLAUDE.md verschlankt, PROGRESS.md, Smoke-Test. Abgelehnt: Referenz-Beispiele.
