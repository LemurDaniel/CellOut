# C# (Godot .NET)

Rules for `scripts/` and `tests/`. The general rules of `SKILL.md` apply, this file adds what
is specific to C# in a Godot project. Project rules on where a feature goes are in `CLAUDE.md`.

Verify: `./tools/Test-CellOut.ps1 -GodotPath <mono console exe>` (dotnet build with warnings as
errors, unit tests, import, headless smoke test).

## File and class
- **Header and table of contents above the `using`s**, chapter banners inside the class body,
  `///` XML doc comments with `(Required)`/`(Optional)`, PascalCase members, `UPPER_SNAKE` constants.
- **One class, one file, same name.** `public partial class` exists only because the Godot source
  generators need it. A class is never spread over several files (`Foo.Preview.cs`, `Foo.Build.cs`):
  that hides the size of the class instead of reducing it.
- **Small types stay with their owner:** an enum or record that serves one class lives in its file
  in a `TYPES` chapter (`CreatureCatalog.cs`). It moves to its own file once a second class needs it
  as its main input.

## Size: when a book gets too thick
| Lines | Meaning |
| --- | --- |
| up to 500 | Fine. |
| 500 to 800 | Check before adding a chapter: does the new chapter belong to this class at all? |
| over 800 | Split before the next feature goes in. |

Exceptions: a test file that lists cases (`tests/SmokeTest.cs`) and a pure data table.

- **Split by responsibility, not by line count.** A responsibility shows itself by:
  - a chapter whose state fields no other chapter writes
  - fields that share a prefix (`_spray*`, `_drag*`, `_camera*`)
  - a mode flag that switches a whole block of input handling (`_isSpraying`)
  - a `Build*` method that only assembles nodes of one panel
- **The original class stays as coordinator:** it keeps its public API, creates the extracted
  classes, wires their signals and holds no detail logic. Callers and scenes do not change.
- **Refactoring changes no behavior:** tests green before, tests green after, no fix or feature in
  the same step. A bug found on the way is noted and fixed separately.

## Three kinds of extracted class
| Kind | Form | Talks to its owner via | Tested in |
| --- | --- | --- | --- |
| Pure logic | `static class` or `sealed class`, no `Node` base | return values | `tests/unit` (xUnit) |
| Node component | `partial class X : Node` / `Node3D` / `Control` | `[Signal]` or C# `event` | `tests/SmokeTest.cs` |
| View | `partial class X : Control`, builds one panel | signals for clicks, `Refresh(...)` in | `tests/SmokeTest.cs` |

- **Pure logic calls no engine:** no `Node`, `GD.*`, `Input`, `FileAccess`, `ResourceLoader`, no
  scene tree. `Mathf` and the value structs (`Vector2`, `Vector3`, `Color`, `Transform3D`) are
  managed code and allowed. That is what makes the class testable without Godot.
  ```csharp
  // Pure: geometry of the spine, usable from the visual, the editor and a unit test
  public static class SpineCurve
  {
      public static Vector3 PointAt(IReadOnlyList<float> heights, float t) { ... }
  }
  ```
- **A component never reaches up:** no `GetParent()`, no cast of the owner, no reference to the
  coordinator handed in as `this`. It gets what it needs through a `Setup(...)` call or init
  properties before `AddChild`, and reports through signals.
  ```csharp
  // Coordinator
  _spray = new SprayCan();
  _spray.Setup(_session, _preview);
  _spray.Painted += OnPainted;
  AddChild(_spray);
  ```
  Not like this: `((CreatureEditor)GetParent()).RefreshStats();`
- **Input is routed, not shared:** the coordinator receives the event and asks its components in a
  fixed order (`if (_gizmo.Handle(@event)) return;`). Two classes never read the same event on
  their own.

## State
- **State lives in the class that changes it.** Others read it through properties or hear about a
  change through a signal.
- **Shared state is one small session object,** not five fields passed around: the draft design,
  the points left, the selection. The coordinator creates it and hands it to every component,
  a C# `event Changed` tells the views to refresh.
- **No shortcuts:** no public mutable fields, no `internal` setter that exists so a sibling can
  write into the state of another class.

## Balancing in data
- **The code holds the formula, the data holds the number.** Values a designer tunes (costs,
  thresholds, multipliers, rates, gameplay durations) live in `data/balance.json`, typed by
  `scripts/Core/Balance.cs`.
  ```csharp
  NextLevelAt = NextLevelAt * Balance.Current.Player.LevelGrowth + Balance.Current.Player.LevelStep;
  ```
  Not like this: `private const float LEVEL_GROWTH = 2.1f;`
- **Constants stay in code** when they are structure (stage indices, array sizes shared with a
  shader), look (mesh segments, colors, animation timing) or math.
- **A class reads its section through one property,** not through copies in constants:
  `private static PlayerBalance Tune => Balance.Current.Player;`. A number used by several
  classes is read from `Balance.Current` in each of them, never re-exported as a public constant.
- **A catalog keeps what a thing is, the data what it costs:** the table in code lists kind,
  category and mount, a small `Part(...)` helper fills in the numbers of the kind.
- **A new value** is a `required` property in the matching record of `Balance.cs` plus the entry in
  `data/balance.json` and in `tests/unit/balance.fixture.json`. A missing entry, an unknown key or
  a part kind without entry fails at load with a message that names it.
- **Parsing calls no engine:** `Balance.Parse` and `Balance.Use` take the JSON text. Only the
  default read of the game goes through Godot (`FileAccess`), so it also works from an exported pack.

## Tests
- **Pure logic gets unit tests** in `tests/unit/<Topic>Tests.cs` (xUnit) with concrete expected
  values. They run in seconds without Godot: `dotnet test tests/unit/CellOut.Tests.csproj`.
- **Engine behavior** (scene tree, signals, input, spawning) is covered by `tests/SmokeTest.cs`.
- **Tests pin the formula, not the tuning:** the unit tests load the frozen
  `tests/unit/balance.fixture.json`, so retuning `data/balance.json` never breaks them. One test
  checks that the real file of the game still loads.
- **A formula inside a node moves out before it gets a test:** `Player` asks `Growth` for the
  level curve, the test asks `Growth` too. No test builds a `Node` outside Godot.
