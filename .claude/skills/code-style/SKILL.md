---
name: code-style
description: Storyline code style of Cell Out (copy of the user skill code-style, cut to the languages of this repo) - file headers, chapter/section banners, guard clauses, naming, docs, error handling, logging, verification, file naming. Correctness first, then readability. Load BEFORE writing, editing or reviewing code in this repo (C# under scripts/ and tests/, PowerShell under tools/, JavaScript / Node.js under tools/font, Python under tools/blender, shaders), even for a small fix or a single new function, and before /code-review or /simplify. Triggers on "code style", "style guide", "banner", "comments", "naming", "review", "clean up", "refactor".
---

# Code Style

Write code so it reads top to bottom like a story.
The conventions in `CLAUDE.md` (namespace, shelves, Godot specifics) win. The rules here fill the gaps.
Source: copy of the user skill `code-style`, only the parts relevant for this repo (Terraform, Bicep, YAML, Compose, Kubernetes, C++ and OpenSCAD removed).

## Scope
- **New and changed code** always follows these rules. A new file counts as a change to its folder: it goes on the right shelf, and a folder it overfills gets shelves (see [The story](#the-story) → "Shelve as the code grows").
- **A bug fix or feature stays focused:** untouched code around it is not restyled on the side.
- **Refactoring, clean-up and review** put existing code in scope. Before restructuring beyond the requested part (adding chapters, renaming, splitting or moving files), ask the user how far to go:
  - only the touched function or block
  - the whole file
  - the whole module or folder
- When unsure whether a task counts as refactoring, ask instead of assuming.

## Priorities
1. **Correctness is the boundary.** The code does exactly what it claims, verified, not assumed. No rule and no gain in readability justifies dropping an edge case.
2. **Readability is the goal.** Among correct solutions, the one a new reader follows top to bottom without jumping wins.
3. **Everything else** (brevity, cleverness, micro-performance) only after 1 and 2.

- **Readable never means wrong.** A shorter version that drops an edge case is not more readable, it is a bug.
  ```js
  const timeout = options.timeout ?? DEFAULT_TIMEOUT_MS
  ```
  Not like this: `const timeout = options.timeout || DEFAULT_TIMEOUT_MS` (a timeout of `0` silently becomes the default).
- **The rules serve readability.** When a rule makes one specific spot harder to read, readability wins. One comment says why the spot deviates.
  ```powershell
  # Nested on purpose: every check needs the open connection, guard clauses would close it early.
  ```

## Language-specific rules
Before writing or reviewing code in one of these languages, read the matching file from `references/` next to this file with the Read tool (once per session is enough):

| Language | File |
| --- | --- |
| PowerShell (`tools/*.ps1`) | `references/powershell.md` |
| JavaScript / Node.js (`tools/font`) | `references/javascript.md` |

C# (`scripts/`, `tests/`), Python (`tools/blender`) and Godot shaders (`shaders/`): only the general rules below, plus the language's own conventions and `CLAUDE.md`. The existing files (e.g. `scripts/Core/Plane3D.cs`, `tools/blender/build.models.py`) are the reference.
- C#: file header and table of contents above the `using`s, chapter banners inside the class body, `///` XML doc comments with `(Required)`/`(Optional)`, PascalCase members, `UPPER_SNAKE` constants.

# General

## The story
A codebase is a library, a folder is a shelf, a file is a book. Every rule below gives one part of the library its place in the code:

| Library | Code | Form |
| --- | --- | --- |
| Library | Codebase | README at the entrance, see [Structure](#structure) |
| Shelf | Folder, cut by domain | Every shelf with more than one book has a label, see below |
| Shelf label | Entry point or folder README | `Utils.psm1`, `main.tf`, `main.bicep`, else `README.md` |
| Call number | File name `<kind>.<topic>[.<variant>]` | Sorts the books on the shelf, see [Structure](#structure) |
| Book | File | Header, chapters, epilogue |
| Blurb | File header | Usage and prerequisites, see [Reading flow](#reading-flow) |
| Table of contents | Chapter list below the header | Own comment block, in every file with chapters, also with only one |
| Cast list | Imports and module-wide constants | Right below the header, before the first chapter |
| Chapter | Chapter | Three lines of `=`, title in UPPERCASE, see [Comments](#comments) |
| Chapter intro | Chapter description | Numbered steps or one entry per unit, right below the frame |
| Section | Section | `-` frame around the title and a one-line description |
| Paragraph | Group comment | One comment line inside a unit, no banner |
| Epilogue | Cleanup | Context reset, outputs, exports: always the last chapter or section |
| Proofreading | Verification | Parser, linter and tests before the code counts as done, see [Errors, logging, tests](#errors-logging-tests) |

The library, its shelves and the call numbers:
```
platform/                            Library: README.md at the entrance
  README.md
  scripts/                           Shelf: one domain
    Utils.psm1                       Shelf label: entry point, dot-sources every book
    Config/Get-UtilsConfig.ps1       Book, call number in Verb-Noun (PowerShell convention)
  pipelines/                         Shelf
    README.md                        Shelf label: no entry point, so a README lists the files
    deploy.infra.dev.yaml            Book, call number <kind>.<topic>.<variant>
    tmpl.terraform.infra.deploy.yaml
```

One book from blurb to epilogue, the part names on the left:
```
Blurb              # NOTE:
                   #   Requires: Az.Storage and a logged in context (Connect-AzAccount).
                   #
                   #   ./Move-StorageBlob.ps1 -Source st-old -Target st-new -WhatIf

                   #
Table of contents  #   Chapters:
                   #     LOAD AND VALIDATE BLOBS   Validations and the blobs of the source.
                   #     MOVE BLOBS                Copies every blob and reports the count.
                   #

                   param (...)

                   Set-StrictMode -Version 1.0
                   $ErrorActionPreference = 'Stop'

Cast list          $CONTAINER_NAME = 'backups'

Chapter            # ===========================================================================
                   # =======================   LOAD AND VALIDATE BLOBS   =======================
                   # ===========================================================================
Chapter intro      #
                   #   1. Validations   Fails early on equal storage accounts.
                   #   2. Blobs         Loads all blobs of the source container.
                   #

Section            # ---------------------------------------------------------------------------
                   # Validations
                   #   Fails early on equal storage accounts.
                   # ---------------------------------------------------------------------------

                   if ($Source -EQ $Target) {
                       throw [System.ArgumentException]::new('Source and Target must be different storage accounts.')
                   }

                   # ---------------------------------------------------------------------------
                   # Blobs
                   #   Loads all blobs of the source container.
                   # ---------------------------------------------------------------------------
                   ...

                   # ===========================================================================
                   # =============================   MOVE BLOBS   ==============================
                   # ===========================================================================
                   #
                   #   1. Copy     Copies every blob into the target container.
                   #   2. Report   Returns the number of copied blobs.
                   #

                   # ---------------------------------------------------------------------------
                   # Copy
                   #   Copies every blob into the target container.
                   # ---------------------------------------------------------------------------

                   $CopiedCount = 0     # reported in Report

                   foreach ($blob in $Blobs) {
                       $CopyConfig = @{
Paragraph                  # Source
                           SrcContainer  = $CONTAINER_NAME
                           SrcBlob       = $blob.Name
                           Context       = $sourceContext
                           # Target
                           DestContainer = $CONTAINER_NAME
                           DestContext   = $targetContext
                       }
                       $null = Start-AzStorageBlobCopy @CopyConfig
                       $CopiedCount++
                   }

Epilogue           # ---------------------------------------------------------------------------
                   # Report
                   #   Returns the number of copied blobs.
                   # ---------------------------------------------------------------------------

                   return [PSCustomObject]@{ Copied = $CopiedCount }
```

Proofreading happens after the book is written, outside the file: `Verified: parser, Invoke-ScriptAnalyzer, Pester (12 passed)`.

A book has exactly three levels: chapter (`=` frame), section (`-` frame) and paragraph (one group comment inside a unit). There is no fourth level: what feels like a sub-chapter becomes a section, what feels like a sub-section becomes a paragraph.

- **The book is the file.**
- **Every shelf has a label:** A folder with more than one file has an entry point where the language has one (`Utils.psm1`, `main.tf`, `main.bicep`, the thin pipeline that calls the templates, the header of a `.cpp`). Where it has none (JavaScript, OpenSCAD, a folder of YAML files), a short `README.md` in the folder lists the files with one line each. Whoever opens the folder knows what stands on the shelf without opening a book.
  ```markdown
  # PowerShell examples
  | File | Content |
  | --- | --- |
  | `Get-UtilsConfig.ps1` | One function with one chapter: defaults, read, merge. |
  | `Move-StorageBlob.ps1` | A script with chapters, WhatIf and a CLEAN block. |
  ```
- **Shelve as the code grows:** Before adding a file, check its shelf. A folder with more than five source files besides its label, or with several roles lying flat side by side (entry point, views, commands, data access, helpers), gets shelves by role or domain, each with its label, before the next file goes in. A proof of concept is no exception, it is the code that grows fastest.
  ```
  Not like this:                   Like this:
  extension.js                     src/
  parser.banners.js                  extension.js
  cache.banners.js                   views/
  decorations.banners.js               README.md
  codelens.banners.js                  view.decorations.js
  statusbar.banners.js                 view.codelens.js
  commands.banners.js                  view.statusbar.js
                                     commands/commands.banners.js
                                     data/banners.js
                                     utils/banners.parse.js
  ```

- **Every file with sections has chapters:** sections only live inside a chapter, so a file with a single phase gets one chapter around its sections (a Bicep module, `Get-UtilsConfig.ps1`). More chapters as soon as it tells more than one phase (load, validate, write) or holds more than one kind of unit (helpers plus a main flow).
- **Only a tiny file goes without banners:** tiny means one unit that does one step, a `.bicepparam`, a values override, a workflow with one job, a function that maps, formats or delegates in a few lines. Its header still says what it does. One unit per file is no reason to skip the chapter: a function that runs through several steps (validate, mount, read) is a book of its own and gets one chapter with a section per step, see the function file in the language file.
- **A section outside a chapter gets a chapter, it is never removed:** the step structure is what the reader needs, the missing frame around it is the mistake.
- **Chapter order follows the plot:** setup (settings, helpers, context) → validations → the action → epilogue (cleanup, outputs, exports). Helpers either come first as a collection chapter, so the reader meets them before they act, or live in their own file. Never between two steps of the action.
- **No character without an introduction:** a variable or config value is declared in the chapter where it first acts, right before its first use. Not collected at the top of the file and not in a later chapter. The exception is the cast list: imports and module-wide constants (`COMMAND_NAME`, `DEFAULT_TIMEOUT_MS`) stand together below the header, because the whole book uses them.
  ```powershell
  # ---------------------------------------------------------------------------
  # Files
  #   Loads every non-empty file of the source folder.
  # ---------------------------------------------------------------------------

  $Files = Get-ChildItem -Path $Source -File
  | Where-Object -Property Length -GT -Value 0

  $UploadedCount = 0     # counted in UPLOAD FILES, reported at the end
  $SkippedCount  = 0
  ```

## Reading flow
- **File header with usage and prerequisites:** Whoever opens the file sees at the top how to call it and what has to exist first. Every file has one, also a file with one function and a doc comment: the doc comment documents the parameters inside the unit, the header names prerequisites and the call before the reader reaches it.
  ```js
  // NOTE:
  //  node .\day09-part1.js [INPUT|TEST]
  //
  //    'node .\day09-part1.js INPUT'   to process the input from 'day09-input.txt'.
  //    'node .\day09-part1.js TEST'    to process the test input from 'day09-input-test.txt'.
  ```
  ```powershell
  # NOTE:
  #   Requires: Az.Accounts, Az.Storage and a logged in context (Connect-AzAccount).
  #
  #   ./Move-StorageBlob.ps1 -Source st-old -Target st-new -WhatIf
  ```
- **Table of contents below the header:** A file with chapters lists them in its own comment block right below the header, separated by an empty line and framed by an empty comment line above and below. In a YAML file it follows the closing line of the header. The chapters stand in file order, with the exact chapter title on the left and one line on the right. Also a single chapter, so the reader never has to guess whether the list is missing or not required. The banner check compares the list with the chapters.
  ```cpp
  // NOTE:
  //   Implementation of MiniServer.h.

  //
  //   Chapters:
  //     HELPERS      Status codes and the prefix check.
  //     LIFECYCLE    begin and stop.
  //     ROUTING      Route registration and request handling.
  //     MIDDLEWARE   Middleware registration.
  //
  ```
- **Concept before code:** For algorithms, data structures or unfamiliar mechanisms a short explanation comes first, then the implementation.
  ```js
  // A heap is a binary tree like structure that stores the min (or max) value as root node.
  // Insertion:
  //      Every new value gets inserted at the bottom, then traversed up to its correct spot.
  // Each node can be found via:
  //      - Parent     = (childIndex - 1) / 2
  //      - LeftChild  = parentIndex * 2 + 1
  //      - RightChild = parentIndex * 2 + 2
  class BaseHeap { ... }
  ```
- **Describe first, then implement:** Non-trivial sections start with a block comment holding the decision tree. The code below follows it 1:1.
  ```
  > When the file does NOT exist
      - If cloud data exists, create the local file.
      - Otherwise, do nothing.
  > When the file exists
      - Skip if the last sync is inside the interval.
      - Otherwise upload when no cloud data exists.
  ```
  ```powershell
  # When the file does NOT exist
  if (-NOT (Test-Path -Path $FilePath)) {
      # If cloud data exists, create the local file.
      if ($null -NE $cloudData) {
          $null = Set-Content -Path $FilePath -Value $cloudData
      }
      # Otherwise, do nothing.
      return
  }

  # When the file exists
  # Skip if the last sync is inside the interval.
  if ($lastSync.AddSeconds($Interval) -GT [System.DateTime]::Now) {
      return
  }
  ...
  ```
- **Split files into chapters:** Chapter and section banners structure the file, the format is described under [Comments](#comments), when and in which order under [The story](#the-story).
  The outline of `Move-StorageBlob.ps1` from [The story](#the-story), read from top to bottom:
  ```
  LOAD AND VALIDATE BLOBS   Chapter    setup and validations
    Validations             Section
    Blobs                   Section
  MOVE BLOBS                Chapter    the action
    Copy                    Section
    Report                  Section    the epilogue
  ```
- **Flat main path:** Guard clauses and early `return` instead of nesting.
  ```js
  if (!user) return
  if (!user.isActive) return

  grantAccess(user)
  ```
  Not like this:
  ```js
  if (user) {
      if (user.isActive) {
          grantAccess(user)
      }
  }
  ```
- **What belongs together stays together:** State, configuration and functions of a feature sit next to each other, not sorted by visibility or type.
  ```js
  // Cache
  #cache = new Map()
  #cacheTimeout = CACHE_TIMEOUT_MS
  getCached(key) { ... }
  clearCache() { ... }

  // Projects
  #projects = null
  get projects() { ... }
  async reloadProjects() { ... }
  ```

## Comments
Comments look the same in every language, only the comment character changes. A language file may restrict the format (e.g. one chapter per file), but never introduce its own banner style.

| Comment character | Languages |
| --- | --- |
| `//` | C#, JavaScript / Node.js, Godot shaders |
| `#` | PowerShell, Python |

- **Two levels of banners:**
  - **Chapter:**
    - Three lines of `=`. The title sits in UPPERCASE centered in the middle line, with 3 spaces on each side.
    - The title describes the content, short and as an activity (`RENDER PINNED REPOS AS CARDS`, `SOLVE TREE PUZZLE`). Not generic like `MAIN` or `CODE`. Collection chapters may be called `HELPERS` or `SETTINGS`.
    - A chapter without sections is fine.
    - **Description:** Every chapter has one. Right below the frame it says what happens in the chapter. Empty comment lines above and below set it apart.
      - A **flow chapter** runs top to bottom, its sections are steps in a fixed order. So the steps are numbered. The section name is on the left, exactly as in the section banner, a one-line explanation on the right, columns aligned. Continuation lines indent to the explanation column. The banner check compares the numbered names with the sections of the chapter.
        ```powershell
        # ===========================================================================
        # =======================   LOAD AND VALIDATE BLOBS   =======================
        # ===========================================================================
        #
        #   1. Validations   Fails early on equal storage accounts.
        #   2. Blobs         Loads all blobs of the source container,
        #                    only after the validations passed.
        #
        ```
      - A **collection chapter** holds units that stand on their own (helpers, settings, variables, outputs). Nothing runs in order, each unit acts only when it is called. So there is one entry per function or section, without numbers.
        ```js
        // ===========================================================================
        // ===============================   HELPERS   ===============================
        // ===========================================================================
        //
        //   parseGrid        Turns the raw input into a grid of tree heights.
        //   getViewingLine   Collects the trees from one tree to the edge.
        //
        ```
        Rule of thumb: "what happens first?" is answered by a flow chapter, "what is in here?" by a collection chapter.
      - Something that applies to all steps (e.g. "files are only written on change") goes into its own paragraph below.
  - **Section:**
    - Two lines of `-` inside a chapter. Between them the title in normal case and below it the description, indented by 3 spaces.
    - **Description:** Every section has one, one line on what the section does, continuation lines only when it does not fit. In a flow chapter it repeats the explanation of the numbered step, so both read the same. In a file header (YAML, Compose, Kubernetes) the description holds usage and prerequisites, blocks separated by an empty comment line.
      ```powershell
      # ---------------------------------------------------------------------------
      # Validations
      #   Fails early on equal storage accounts.
      # ---------------------------------------------------------------------------

      if ($Source -EQ $Target) {
      ```
    - **After the closing line** an empty line, then the code. In YAML (pipelines, Compose, Kubernetes / Helm) the key follows directly, so the frame sticks to the job, service or value it describes. The header of a YAML file keeps its empty line.
      ```yaml
      # ---------------------------------------------------------------------------
      # Plan
      #   Publishes the plan as an artifact, so the approver can read it.
      # ---------------------------------------------------------------------------
      plan:
        needs: config
      ```
    - Sections only live inside a chapter. The one section-shaped block outside is the header of a YAML file (pipelines, Compose, Kubernetes) in the first line, its table of contents follows as its own block below the closing line.
  - **Both levels:**
    - Banners stand on the level of the file or of the body of its unit (a function, a script block level like `BEGIN`/`PROCESS`), indented like the code there. Never inside a loop, a branch, a `try` or a callback handed to another command: such a block is one section, its steps inside are group comments.
    - Every line is 75 characters long, plus the comment character and one space.
    - No other banner styles, so no `/* #### */` boxes, `<# #### #>` blocks, `/*---- * Title *` blocks, `////` or `#####` lines.
- **Comments in the code:**
  - **Group comments inside a unit:** Inside a class, resource block or parameter list there are no banners. The exception is a class that fills a whole chapter (a C++ header), its groups are sections. A single comment line in normal case above the group structures it (`# General resource attributes`, `// Get Index`).
  - **Block comments:** A short comment per block says *what* it does. The *why* only appears where it is not obvious.
  - **Block comment syntax** (`/* */`, `/** */`, `<# #>`) is only for doc comments and multi-line explanations above a block, never for banners.
  - **Cross-references name the chapter:** When a block depends on or undoes something from another chapter, the comment says which one, with the chapter title as written in its banner (`# Restores the context switched in LOAD AND VALIDATE FILES.`). Across files the file name comes first (`// see MiniServer.h, section Routing`).

```js
// NOTE:
//  node ./day08.js

//
//  Chapters:
//    HELPERS             Grid parsing and viewing lines.
//    SOLVE TREE PUZZLE   Parses the input and solves both parts.
//

// ===========================================================================
// ===============================   HELPERS   ===============================
// ===========================================================================
//
//   parseGrid        Turns the raw input into a grid of tree heights.
//   getViewingLine   Collects the trees from one tree to the edge.
//

function parseGrid(input) { ... }
function getViewingLine(grid, x, y, direction) { ... }

// ===========================================================================
// ==========================   SOLVE TREE PUZZLE   ==========================
// ===========================================================================
//
//   1. Parse input   Reads the input file into a grid.
//   2. Part 1        Counts the trees visible from outside the grid.
//   3. Part 2        Finds the highest scenic score of a single tree.
//                    Ties are resolved by the first tree found.
//

// ---------------------------------------------------------------------------
// Parse input
//   Reads the input file into a grid.
// ---------------------------------------------------------------------------

// Split the input into one grid row per line.
const grid = parseGrid(fs.readFileSync(INPUT_FILE, 'utf-8'))

// ---------------------------------------------------------------------------
// Part 1
//   Counts the trees visible from outside the grid.
// ---------------------------------------------------------------------------

const visibleTrees = countVisibleTrees(grid)
```

```powershell
# ===========================================================================
# ======================   LOAD AND VALIDATE CONFIG   =======================
# ===========================================================================
#
#   1. Config        Loads the owner and resolves the region.
#   2. Validations   Fails early on a missing owner or region.
#

# ---------------------------------------------------------------------------
# Config
#   Loads the owner and resolves the region.
# ---------------------------------------------------------------------------

$Config = Get-Content -Path $ConfigPath -Raw | ConvertFrom-Json
```

## Structure
- **One thing per file or unit.** The file name matches the main content, folders are cut by domain.
  ```
  Utils/
    Config/
      Get-UtilsConfig.ps1
      Set-UtilsConfig.ps1
    CloudSync/
      Sync-UtilsCloudFile.ps1
  ```
- **File names as a dotted hierarchy** `<kind>.<topic>[.<variant>].<ext>`, so they group themselves in the explorer.
  The kind comes first even when the folder already names it (`routes/routes.status.js`). That keeps the file name
  unique, also in an editor tab or a search. Plural when a file bundles several units (`routes.`,
  `tools.`), singular for one unit per file (`command.`, `main.`), consistent within a folder. Exceptions:
  - entry points (`main.tf`, `main.bicep`, `Utils.psm1`) and collection files (`_index.js`)
  - files whose name a tool expects (`docker-compose.yaml`, `package.json`, `.clang-format`)
  - languages with their own naming convention (PowerShell `Verb-Noun.ps1`, C++ class names)
  - exceptions named in a language file (e.g. `utils/` and `data/` in JavaScript)

  | File | Kind | Topic | Variant |
  | --- | --- | --- | --- |
  | `main.vnet.tf` | `main` | `vnet` | |
  | `locals.generate.tf` | `locals` | `generate` | |
  | `locals.pattern.tf` | `locals` | `pattern` | |
  | `deploy.infra.dev.yaml` | `deploy` | `infra` | `dev` |
  | `deploy.infra.prod.yaml` | `deploy` | `infra` | `prod` |
  | `service.nginx-reverse-proxy.yaml` | `service` | `nginx-reverse-proxy` | |
  | `routes.status.js` | `routes` | `status` | |
  | `command.repository.download.js` | `command` | `repository` | `download` |

  In the explorer every `deploy.*` and every `locals.*` file sorts next to its siblings on its own.
- **Disable instead of delete:** What should temporarily not load gets a suffix or prefix (`main.search.tf.disabled`, `#Get-UtilsCacheFile.ps1`).
- **Templates with placeholders** for recurring files (`tmpl.*`, `blueprint.js` with `${{DAY}}`).
  ```js
  // blueprint.js
  const input = fs.readFileSync(`day${{DAY}}-input.txt`, 'utf-8')
  ```
- **One entry point per library** that bundles and exports the modules.
  ```powershell
  # Utils.psm1
  . $PSScriptRoot/Config/Get-UtilsConfig.ps1
  . $PSScriptRoot/Config/Set-UtilsConfig.ps1
  . $PSScriptRoot/CloudSync/Sync-UtilsCloudFile.ps1
  ```
- **One real implementation:** The logic lives in one place. Overloads, shortcuts and wrappers hold no logic of their own, they fill in values and delegate to it.
  ```powershell
  # The one real implementation: knows the naming pattern.
  function Get-UtilsResourceName {
      param ([System.String] $Type, [System.String] $Environment)
      return "$Type-euwe-$Environment-01"
  }

  # Shortcut: only fills in the type and delegates.
  function Get-UtilsVnetName {
      param ([System.String] $Environment)
      return Get-UtilsResourceName -Type vnet -Environment $Environment
  }
  ```
  Not like this: `Get-UtilsVnetName` builds `"vnet-euwe-$Environment-01"` itself. When the pattern changes, the shortcut stays on the old one.
- **No duplicated logic:** Repeated blocks move into a helper.
  ```js
  // Instead of building the same backup path in three places:
  function getBackupPath({ profile, date }) {
      return path.join(BACKUP_ROOT, profile, `${date.toISOString().slice(0, 10)}.zip`)
  }
  ```
- **Explicit over magic:** Imports and registrations are written out, types are unambiguous, magic numbers are named constants.
  ```js
  const MAX_LIFETIME_EXTENSION_DAYS = 14

  if (extension > MAX_LIFETIME_EXTENSION_DAYS) return
  ```
  Not like this: `if (extension > 14) return`
- **Data in a file or variable, not in logic:** Long lists (IP ranges, mappings) and concrete environment values are not hard-coded. Scaffolds contain placeholders instead of real names.
  ```hcl
  locals {
    allowed_ip_ranges = yamldecode(file("${path.module}/data/allowed-ip-ranges.yaml"))
  }

  variable "subscription_id" {
    description = "(Required) The subscription to deploy into, e.g. '<SUBSCRIPTION_ID>'."
    type        = string
  }
  ```
- **Reused parameter sets** are bundled once as an object or config instead of being written out several times.
  ```powershell
  $UploadConfig = @{
      Container = 'backups'
      Context   = $storageContext
      Force     = $true
  }

  Set-AzStorageBlobContent @UploadConfig -File $databaseFile -Blob 'notes.db'
  Set-AzStorageBlobContent @UploadConfig -File $configFile   -Blob 'notes.config.json'
  ```
- **Pass a context object:** A parent module hands down a reference (name, location, tags, naming) as one object instead of five single values.
  ```hcl
  module "storage_account" {
    source = "./modules/storage/storage-account"

    resource_group_reference = module.resource_group.reference
    storage_account          = { sku = "Standard_LRS" }
  }
  ```

## Processing data
- **Fallback chains as a list with one comment per step.** The order of precedence goes above, one line per step below.
  ```hcl
  /*
    Order of precedence:
    - Match to <resourceType>.<id>
    - Match to <resourceType>.<kind>
    - Match to default
    - Fail with an error, if no pattern is found
  */
  pattern_selected = coalesce([
    # Match to <resourceType>.<id>
    lookup(lookup(local.patterns_provider, local.resource.type, {}), var.naming_id, null),
    # Match to <resourceType>.<kind>
    lookup(lookup(local.patterns_provider, local.resource.type, {}), local.resource.kind, null),
    # Match to default
    lookup(local.patterns_provider, "default", null),
  ]...)
  ```
  ```powershell
  # Order of precedence: parameter > profile > config file > default
  $region = @(
      $Region                         # parameter
      $SyncProfile.Region             # profile
      (Get-UtilsConfig).Region        # config file
      'westeurope'                    # default
  ) | Where-Object { -NOT [System.String]::IsNullOrEmpty($_) } | Select-Object -First 1
  ```
- **Transform in named stages:** Every step gets its own name that says what it delivers. No monster expression, no names like `_2`.
  ```hcl
  pattern_compacted  = ...   # raw pattern split into components
  pattern_components = ...   # components with metadata (isRequired, isParameter)
  name_generated     = ...   # components resolved to values
  name_formatted     = ...   # values formatted per index
  name_final         = ...   # joined and lower-cased
  ```
  ```js
  const lines       = input.split('\n')
  // '1000\n2000\n\n3000' => [[1000, 2000], [3000]]
  const elfCalories = lines.join(',').split(',,').map(group => group.split(',').map(Number))
  const elfTotals   = elfCalories.map(calories => calories.reduce((a, b) => a + b, 0))
  const maxCalories = Math.max(...elfTotals)
  ```
- **Show the shape change:** A non-obvious parse or reshape (split, slice, regex, flatten) gets one comment with a sample input and what comes out. The reader sees the result without running the expression in their head. Several variants get one line each.
  ```hcl
  # Takes the resource type without provider and kind.
  # - azurerm_storage_account::data_lake  => storage_account
  # - azurerm_disk                        => disk
  type = join("_", slice(split("_", local.resource_without_kind), 1, length(split("_", local.resource_without_kind))))
  ```

## API and naming
- **Build for the caller:** short, telling entry points, sensible defaults and autocompletion where the language supports it.
  ```powershell
  # The caller only has to name the profile, everything else has a default and completes via TAB.
  Sync-UtilsCloudFile -ProfileName notes
  ```
- Verbs first, domain as prefix or namespace (`Get-UtilsConfig`, `Http::Router`).
- Booleans carry an `is`/`has` prefix and the real bool type.
  ```js
  const isActive = true          // not: const active = 'yes'
  const hasChildren = node.children.length > 0
  ```
- Constants in `UPPER_SNAKE`, otherwise the language convention applies. Casing stays consistent within a file.
  ```js
  const API_VERSION = '7.1'
  const DEFAULT_TIMEOUT_MS = 30000
  ```
- `null` goes on the left in comparisons (`$null -EQ $x`, `null == node`). In PowerShell this is mandatory, because `$array -EQ $null` filters instead of comparing. Other literals go on the right (`$status -EQ 'Completed'`).
  ```powershell
  if ($null -EQ $Subscription) { ... }   # not: if ($Subscription -EQ $null)
  ```
- Comments and identifiers in English, without typos. A misspelled name spreads to every caller and is hard to rename later.

## Documentation
- **Every public unit is documented:** purpose, parameters (with default and whether required or optional), return value and an example. The format is the language's usual one.
  ```js
  /**
   * Downloads all repositories of a project into the local workspace.
   *
   * @param {string}  project              (Required) Name of the Azure DevOps project.
   * @param {boolean} [isForced=false]     (Optional) Overwrites existing folders.
   * @returns {Promise<string[]>} The local paths of all downloaded repositories.
   *
   * @example
   * await downloadRepositories('Platform', true)
   */
  ```
- **Required or optional always comes first**, consistent per language: `[Required]`/`[Optional]` in PowerShell, `(Required)`/`(Optional)` everywhere else.
  ```hcl
  description = "(Optional) The SKU of the container registry. Defaults to 'Standard'."
  ```
- **No empty doc skeletons:** Either fill them in or leave them out.
  Not like this:
  ```powershell
  <#
  .SYNOPSIS
  .DESCRIPTION
  .EXAMPLE
  #>
  ```
- Parameter docs sit right at the parameter, not collected somewhere else.
  ```powershell
  <#
  [Required]
  The name of the cloud sync profile.
  #>
  [Parameter(Mandatory = $true)]
  [System.String]
  $ProfileName,
  ```

## Errors, logging, tests
- **Never swallow errors silently.** Only catch what you can handle and rethrow the rest.
  ```powershell
  try {
      $config = Get-Content -Path $ConfigPath -Raw -ErrorAction Stop | ConvertFrom-Json
  }
  catch [System.Management.Automation.ItemNotFoundException] {
      # Handled: a missing config simply means the defaults apply.
      Write-Verbose "[Get-UtilsConfig] Config '$ConfigPath' not found, using defaults."
      return $DefaultConfig
  }
  # Everything else (e.g. invalid JSON) bubbles up unchanged.
  ```
  Not like this: `catch { }`
- **Error messages state the expectation:** `"X must be a full resource ID in the format: /subscriptions/{id}/..."`
  ```powershell
  throw [System.ArgumentException]::new("Interval must be between 60 and 3600 seconds, got '$Interval'.")
  ```
- Validate input early, but only after the variables are set. Several rules are grouped in their own `Validations` section.
  ```powershell
  # ---------------------------------------------------------------------------
  # Validations
  #   Fails early on equal or missing storage accounts.
  # ---------------------------------------------------------------------------

  if ($Source -EQ $Target) {
      throw [System.ArgumentException]::new('Source and Target must be different storage accounts.')
  }
  if ($null -EQ (Get-AzStorageAccount | Where-Object -Property StorageAccountName -EQ -Value $Target)) {
      throw [System.ArgumentException]::new("Target storage account '$Target' must exist before moving blobs.")
  }
  ```
- The condition must check exactly what the error message claims.
  ```js
  // Message says "must be a positive number" -> condition checks type AND sign.
  if (typeof count !== 'number' || count <= 0) {
      throw new Error(`count must be a positive number, got '${count}'`)
  }
  ```
- **Logs as a narrative trail:** A debug/verbose log at every decision point, prefixed with the function or component (`[Sync-UtilsCloudFile] …`).
  ```powershell
  Write-Verbose "[Sync-UtilsCloudFile] Local file missing, cloud data found -> creating '$FilePath'."
  Write-Verbose "[Sync-UtilsCloudFile] Last sync at $lastSync is inside the interval -> skipping."
  ```
- Mutating operations support dry-run or WhatIf where possible. Changed context is reset at the end.
  ```powershell
  if ($PSCmdlet.ShouldProcess($file.Name, "Upload to '$Container'")) {
      $null = Set-AzStorageBlobContent @UploadConfig -File $file.FullName -Blob $file.Name
  }
  ```
- **Tests check concrete expected values**, not just "runs through".
  ```powershell
  It 'generates the expected name' {
      Get-UtilsResourceName -Type vnet -Environment tst | Should -Be 'vnet-euwe-tst-01'
  }
  ```
  Not like this: `{ Get-UtilsResourceName -Type vnet } | Should -Not -Throw`
- **Verify before done (proofreading):** After writing or changing code, run every installed check of the language: parser or compiler, linter, formatter check, existing tests. The `Verify` line of each language file names them. Fix every finding before calling the task done, and name every check that could not run.
  ```
  Verified:     terraform fmt -check, terraform validate, terraform test (3 passed)
  Not verified: bicep build (Bicep CLI not installed)
  ```
- **A test file says what it covers:** It opens with a chapter whose title names the tested behavior, the description is a collection with one entry per test case (see [Comments](#comments)). Whoever opens the file sees the cases without reading the asserts.
  ```hcl
  # ===========================================================================
  # ==========================   RESOLVE PATTERNS   ===========================
  # ===========================================================================
  #
  #   provider_default_pattern   Falls back to the provider default pattern.
  #   kind_pattern               Prefers the pattern of the kind (disk::os).
  #
  ```

## Format
- The defaults of the language's usual formatter. Indentation and deviations are in the language's file.
- Break long chains line by line, with the operator at the start of the line.
  ```js
  const isDeployable = isApproved
      && hasPassingTests
      && !isFrozen
  ```
  ```hcl
  name = (
    var.is_global
    ? "${local.prefix}-${var.name}"
    : "${local.prefix}-${var.location_short}-${var.name}"
  )
  ```
- License or project headers only go where the codebase already does it.

# Review checklist
Use this list for reviews, `/code-review` and `/simplify`. Go through every group in order, each group mirrors one section of the rules and each item one rule. Report each finding with the rule it breaks and a concrete fix, ordered by [Priorities](#priorities): correctness first, then readability, then the rest. Respect [Scope](#scope): flag untouched code only in a refactoring or when asked.

- **[Priorities](#priorities)**
  - [ ] **Correct:** the code does what it claims, edge cases (empty, `null`, `0`, missing file) are handled, nothing is assumed untested.
  - [ ] **Readable:** a new reader follows the file top to bottom without jumping. Where not, that is the first finding after correctness.
  - [ ] **Deviations:** a spot that breaks a rule on purpose carries a comment saying why.
- **[The story](#the-story)**
  - [ ] **Shelf label:** a folder with more than one file has an entry point or a `README.md` listing the files.
  - [ ] **Shelves:** no flat folder with more than five source files or several roles side by side, every new file stands on its shelf.
  - [ ] **Chapters:** every file with sections has at least one chapter, a file with several phases or kinds of units has one per phase or kind, only a tiny file (one unit, one step) has no banner. A file with one function that runs several steps has one chapter with a section per step.
  - [ ] **Plot order:** setup → validations → action → epilogue, helpers first or in their own file, never between two steps.
  - [ ] **Introductions:** every variable is declared in the chapter where it first acts, imports and module-wide constants stand as cast list below the header.
- **[Reading flow](#reading-flow)**
  - [ ] **Header:** usage and prerequisites at the top of every file, also above a single function with doc comment.
  - [ ] **Table of contents:** every file with chapters lists them in its own block below the header.
  - [ ] **Describe first:** algorithms and unfamiliar mechanisms get a short concept, non-trivial blocks a decision tree, and the code follows it 1:1.
  - [ ] **Flat main path:** guard clauses and early `return` instead of nesting.
  - [ ] **Together:** state, configuration and functions of a feature sit next to each other, not sorted by visibility or type.
- **[Comments](#comments)**
  - [ ] **Banners:** chapters with `=` frame and centered UPPERCASE title, sections with `-` frame, title and description, 75 characters, no other banner styles.
  - [ ] **Chapter description:** every chapter has one, numbered steps in flow chapters match the section titles, one entry per unit in collection chapters, columns aligned.
  - [ ] **Section description:** every section has one line below the title, indented by 3 spaces, closed by the second `-` line. An empty line follows, in YAML the key follows directly.
  - [ ] **Comments in the code:** group comments instead of banners inside a unit, one short *what* per block, the *why* only where it is not obvious, block comment syntax only for docs.
  - [ ] **Cross-references:** comments that point elsewhere name the chapter or file.
- **[Structure](#structure)**
  - [ ] **One thing per file:** the file name matches the content, folders cut by domain, one entry point per library.
  - [ ] **File names:** dotted `<kind>.<topic>[.<variant>]`, or the exception of the language.
  - [ ] **One implementation:** no duplicated logic, shortcuts and wrappers only delegate.
  - [ ] **Explicit:** no magic numbers, imports and registrations written out, no hard-coded lists or environment values.
  - [ ] **Bundled values:** reused parameter sets as one object, a context object instead of single values.
- **[Processing data](#processing-data)**
  - [ ] **Fallback chains:** the order of precedence above, one comment per step.
  - [ ] **Named stages:** no monster expressions, every step named after what it delivers.
  - [ ] **Shape change:** a non-obvious parse or reshape shows a sample input and its output.
- **[API and naming](#api-and-naming)**
  - [ ] **Caller:** short entry points, sensible defaults, autocompletion where the language has it.
  - [ ] **Names:** verbs first, `is`/`has` booleans with a real bool type, `UPPER_SNAKE` constants, consistent casing, English without typos.
  - [ ] **`null` on the left** in comparisons.
- **[Documentation](#documentation)**
  - [ ] **Public units:** purpose, parameters with default, return value and an example.
  - [ ] **Doc format:** `(Required)`/`(Optional)` first, no empty skeletons, parameter docs at the parameter.
- **[Errors, logging, tests](#errors-logging-tests)**
  - [ ] **Errors:** nothing swallowed, messages state the expectation, the condition matches the message.
  - [ ] **Validations:** early but after the variables are set, several rules grouped in a `Validations` section.
  - [ ] **Logs:** `[Component]` prefix at every decision point.
  - [ ] **Safety:** mutating operations support WhatIf or dry-run, changed context is reset.
  - [ ] **Tests:** assert concrete expected values, every test file opens with a chapter that lists its cases.
  - [ ] **Verified:** parser, linter and tests of the language ran, every check that could not run is named.
- **[Format](#format)**
  - [ ] **Formatter:** the defaults of the language formatter, deviations only as the language file says.
  - [ ] **Long chains:** broken line by line, the operator at the start of the line.
  - [ ] **License headers:** only where the codebase already has them.
- **[Language-specific rules](#language-specific-rules)**
  - [ ] **Language file:** the rules of the matching `references/*.md` are met.
