# CLAUDE.md – Cell Out

Evolutionsspiel in Godot 4.7 .NET (C#): (RNA-Welt als spielbare Vorstufe, `Prelude/`) → Protozelle → Einzeller → Mehrzeller → Lebewesen → Landtier. F10 öffnet ein Cheat-Menü mit Stufen-Sprüngen. Stand und Entscheidungen: `PROGRESS.md`.

## Regeln
- Code Style: Projekt-Skill `.claude/skills/code-style/` (Kopie des User-Skills, auf C#/PowerShell/JS/Python gekürzt; Header mit Usage, Kapitel-/Abschnitts-Banner, englische Kommentare und Bezeichner, `UPPER_SNAKE`-Konstanten, `is`/`has`-Bools, `null` links; C#-Regeln in `references/csharp.md`: Größenlimit, Split in echte Klassen, Balancing in Daten, Unit-Tests). Vor dem Schreiben laden.
- C#: Namespace `CellOut`, `public partial class`, Dateiname = Klassenname, eine Klasse nie über mehrere Dateien verteilen. Ab 500 Zeilen prüfen, ab 800 vor dem nächsten Feature nach Verantwortung in echte Klassen teilen (reine Logik / Node-Komponente / View), die alte Klasse bleibt Koordinator mit ihrer öffentlichen API. Shelves unter `scripts/` primär nach Ära, sekundär nach Domain: `Core` (stage-neutrale Helfer), `Game` (Loop `Main` mit `World/` und `Flow/`, `Player`, `Growth`, Cutscenes, `Ui`, alles über mehrere Ären), `Prelude` (RNA-Welt, `Chain/`, `View/`), `Cell` (Protozelle–Mehrzeller, `Editor/`), `Creature` (Lebewesen–Landtier, `Design/`, `Visual/`, `World/`, `Editor/`). Jeder Ordner mit README.
- Keine fremden Assets: Grafik prozedural in 3D (Primitive-Meshes + Shader in `shaders/`). Ausnahme: eigene, generierte Modelle aus `tools/blender/` (Builder je Familie in `models/*.py`, registriert in `build.models.py`, `Build-Models.ps1 [-Only tree/]` → `assets/models/creature/`, `cell/`, `tree/`), geladen über `Core/PartModels.cs` (Teile) bzw. `Core/TreeModels.cs` (Bäume, Multimesh), nie von Hand in Blender ändern. Neue Modell-Familie = neues Modul in `tools/blender/models/`. Logik bleibt 2D auf der Ebene, Umrechnung über `Plane3D`. Ab Landtier krümmt sich die Welt (Planet): neue Szenerie-Shader binden `shaders/planet.gdshaderinc` ein (`planet_bend` im vertex), Körper ziehen `Plane3D.Drop()` von ihrer Höhe ab.
- Balancing: Zahlen, an denen man dreht (Kosten, Schwellen, Multiplikatoren, Raten), stehen in `data/balance.json`, typisiert in `Core/Balance.cs` (`Balance.Current.<Bereich>`), der Code hält nur die Formel. Neuer Wert = `required`-Property im Record + Eintrag in `data/balance.json` und `tests/unit/balance.fixture.json`. Struktur, Look und Mathe bleiben Konstanten. Noch im Code: Spawn-/Kontaktwerte in `Game/World/` und das Küsten-Terrain.
- Neue Zell-Visuals in `CellVisual3D.BuildCell()` (Einheitsgröße, +X = vorne), Merkmal als Property in `CellBody` + in `ComputeSignature()` aufnehmen.
- Gameplay-Werte immer relativ zu `Player.BodySize()` (Spawngröße, Tempo, Schaden, Zoom). Keine absoluten Pixelwerte.
- Kopplung über `[Signal]`s, keine direkten Zugriffe von Zellen auf `Main`.
- Neues Editor-Teil: `PartKind` + Eintrag in `PartCatalog.PARTS` (Diät, Platzierung) + Kosten und Freischalt-Bereich in `data/balance.json` (`cell.parts`) + Wirkung in `CellStats.From()` (Zahl in `cell.effects`) + Modell im Generator (CELL PARTS) + Eintrag in `CellVisual3D.CELL_MODELS` + Platzierung in `CellVisual3D.BuildPlaced()` + `PART_<KIND>_NAME/_DESC` in `locale/translations.csv`.
- Neues Kreatur-Teil (Stufe Lebewesen): `CreaturePartKind` + Eintrag in `CreatureCatalog.PARTS` (Kategorie-Tab, Mount, Körpertyp) + Kosten, Gewicht, ggf. Carry/Stride/Lift in `data/balance.json` (`creature.parts`) + Wirkung in `CreatureStats.From()` (Player und Editor-Werte nutzen dieselbe Formel, Zahl in `creature.effects`) + Visual in `Creature/Visual/CreatureParts3D.BuildPart()` (an einem `AddPivot`, damit es die Schwimmwelle mitmacht; Füße und Hände (nur die Enden, sie sitzen auf der Spitze eines formbaren Glieds), Flügel, Flossen, Hirn, Stielaugen als Blender-Modell: Builder im Generator, Eintrag in `CreatureModels.MODELS`, Grundgröße in `CreatureParts3D.BASE_PART_SIZES`; Ausnahme: das formbare Glied `Limb` ist eine prozedurale Röhre durch seine Gelenke, `CreatureLimbs3D.Build`) + `CREATURE_<KIND>_NAME/_DESC` in `locale/translations.csv`.
- Neue Stufe: `Player.STAGE_KEYS` (+ Schwelle in `data/balance.json`, falls per Biomasse) + `AdvanceStage()` + Banner in `Main.OnStageChanged()`. Mehrzeller kommt über die erste `Player.Divide()`, nicht per Schwelle. Lebewesen kommt über `Player.RequestEvolve()` → `Main.StartEvolve()` (Cutscene + Kreatur-Editor) → `Player.BecomeOrganism()`.
- Biomasse immer mit `BiomassSource` (Plant/Meat/Light/Raw) vergeben, davon hängt die Ernährungs-Bar ab.
- Schrift: `fonts/CellOut.otf` (Text, Projekt-Default) und `fonts/CellOutDisplay.otf` (Titel/Banner via `GameFonts.UseDisplay()`) sind generiert (`tools/font`, `npm run build`), nie von Hand ändern. Display nur ab ~26 px. Neues Zeichen in einem Text → Skelett in `tools/font/data/glyphs.js`, neu bauen, `npm test` prüft die Abdeckung von `translations.csv`.
- UI-Texte nie hart im Code: Schlüssel in `locale/translations.csv` (en + de), übersetzt im `Hud` per `Tr()`. Titel bleibt „Cell Out“.
- Tempo nur über `CellBody.SwimSpeed()` (eine Formel für Spieler und NPCs).

## Testen
Godot .NET liegt unter `$env:USERPROFILE\Downloads\Godot_v4.7.2-stable_mono_win64\...\Godot_v4.7.2-stable_mono_win64_console.exe` (die winget-Version ohne .NET kann kein C#).
Nach jeder Code-Änderung: `./tools/Test-CellOut.ps1 -GodotPath <mono console exe>` (oder `$env:GODOT`): `dotnet build`, Unit-Tests (`tests/unit`, xUnit, ohne Godot, auch einzeln per `dotnet test tests/unit/CellOut.Tests.csproj`), Import, headless Smoke-Test.
Headless kompiliert keine Shader: bei Visual-/Shader-Änderungen zusätzlich mit `--write-movie <dir>/f.png --fixed-fps 30 --quit-after 120` rendern und Frames ansehen.
Neue reine Logik (Formeln, Regeln ohne Engine) → Unit-Test in `tests/unit/`, neue Mechanik mit Szene/Signalen → `tests/SmokeTest.cs` erweitern. Eine GitHub Action gibt es noch nicht.

## Git
Commits als `LemurDaniel <landau.daniel.1998@gmail.com>`, Conventional Commits auf Deutsch.

## Claude-Code-Tipps-Review
- Durchgeführt am 2026-11-09. Umgesetzt: CLAUDE.md verschlankt, PROGRESS.md, Smoke-Test. Abgelehnt: Referenz-Beispiele.
