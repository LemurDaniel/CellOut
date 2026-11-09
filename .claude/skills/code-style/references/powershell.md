# Code Style: PowerShell
- PowerShell 7, 4 spaces. One function per file, folders by domain, the `.psm1` dot-sources the list explicitly, one `meta.json` per module. The tests live next to the module in `<Module>.Tests/`, which has the same subfolders as the module and one test file per function, see the test rule below.
  ```
  Utils/                                  the module, only what ships
    Utils.psm1                            entry point, dot-sources every function file
    meta.json
    Config/
      Get-UtilsConfig.ps1
      Set-UtilsConfig.ps1
    CloudSync/
      Sync-UtilsCloudFile.ps1
    Naming/
      Get-UtilsResourceName.ps1
  Utils.Tests/                            the tests, same subfolders as Utils/
    README.md                             shelf label, one line per test file
    BeforeAll.ps1                         shared setup, dot-sourced by every test file
    Config/
      Get-UtilsConfig.Tests.ps1           tests Config/Get-UtilsConfig.ps1
      Set-UtilsConfig.Tests.ps1           tests Config/Set-UtilsConfig.ps1
    CloudSync/
      Sync-UtilsCloudFile.Tests.ps1       tests CloudSync/Sync-UtilsCloudFile.ps1
    Naming/
      Get-UtilsResourceName.Tests.ps1     tests Naming/Get-UtilsResourceName.ps1
  ```
  ```powershell
  # Utils.psm1
  . $PSScriptRoot/Config/Get-UtilsConfig.ps1
  . $PSScriptRoot/Config/Set-UtilsConfig.ps1
  . $PSScriptRoot/CloudSync/Sync-UtilsCloudFile.ps1
  . $PSScriptRoot/Naming/Get-UtilsResourceName.ps1
  ```
- **A function file is a book:** header and table of contents above `function`, the chapter inside the body after `param` and the function-wide constants, one section per step. Only a function with a single step (map, format, delegate) gets the header alone, see `SKILL.md` → "The story".
  ```powershell
  # NOTE:
  #   Part of the Utils module, dot-sourced by Utils.psm1.
  #
  #   Get-UtilsConfig -ConfigPath ./utils.config.json

  #
  #   Chapters:
  #     READ UTILS CONFIG   Defaults, reading the file and merging both.
  #

  function Get-UtilsConfig {
      <#
      .SYNOPSIS
          Reads the Utils config file and fills in the defaults.
      #>
      [CmdletBinding()]
      param (...)

      # ===========================================================================
      # ==========================   READ UTILS CONFIG   ==========================
      # ===========================================================================
      #
      #   1. Defaults         The values every config falls back to.
      #   2. Read config      Reads the config file, a missing file counts as empty.
      #   3. Merge defaults   Fills every missing key with its default.
      #

      # ---------------------------------------------------------------------------
      # Defaults
      #   The values every config falls back to.
      # ---------------------------------------------------------------------------

      $DefaultConfig = [PSCustomObject]@{ Region = 'westeurope'; Interval = 120 }
      ...
  }
  ```
- Verb-Noun with a module prefix (`Get-UtilsConfig`, `Invoke-AzureRest`), aliases for frequent commands (`[Alias('tf')]`).
  ```powershell
  function Invoke-TerraformCommand {
      [Alias('tf')]
      [CmdletBinding()]
      param (...)
  }
  ```
- Comment-based help with `.SYNOPSIS`, `.DESCRIPTION` and `.EXAMPLE` (`PS> …`).
  ```powershell
  <#
  .SYNOPSIS
      Syncs a local file with its copy in Azure Blob Storage.

  .DESCRIPTION
      Uploads or downloads the file of a sync profile, depending on which side is newer.
      Skips the sync when the last run is still inside the interval.

  .EXAMPLE
      Sync the notes database with the default interval:

      PS> Sync-UtilsCloudFile -ProfileName notes
  #>
  ```
- Parameters in a multi-line layout, full .NET types and TAB completion where possible: `ValidateSet` for a fixed list, `ArgumentCompleter` for values only known at runtime.
  ```powershell
  <#
  [Required]
  The name of the cloud sync profile, one JSON file per profile in the profiles folder.
  #>
  [Parameter(
      Mandatory = $true
  )]
  [ArgumentCompleter({
      param ($CommandName, $ParameterName, $WordToComplete)
      Get-ChildItem -Path (Join-Path -Path $HOME -ChildPath '.utils/profiles') -Filter "$WordToComplete*.json"
      | Select-Object -ExpandProperty BaseName
  })]
  [System.String]
  $ProfileName,

  <#
  [Optional]
  The direction of the sync. Default is Both.
  #>
  [Parameter(
      Mandatory = $false
  )]
  [ValidateSet('Upload', 'Download', 'Both')]
  [System.String]
  $Direction = 'Both',
  ```
- Pipelines with a leading `|`, no backtick continuation:
  ```powershell
  $SubscriptionId = Get-AzSubscription
  | Where-Object -Property Name -EQ -Value $Name
  | Select-Object -ExpandProperty Id
  ```
- Operators and blocks in uppercase: `-EQ`, `-NOT`, `-LIKE`, `BEGIN/PROCESS/CLEAN`.
  ```powershell
  if (-NOT $IsForced -AND $item.Name -LIKE 'tmp-*') {
      continue
  }
  ```
- `$null = …` to suppress output, splatting hashtables (`@UploadConfig`) for parameter sets.
  ```powershell
  $null = New-Item -ItemType Directory -Path $backupPath

  $UploadConfig = @{
      Container = 'backups'
      Context   = $storageContext
      Force     = $true
  }
  Set-AzStorageBlobContent @UploadConfig -File $FilePath -Blob (Split-Path $FilePath -Leaf)
  ```
- `Write-Verbose "[FunctionName] …"` at every decision point.
  ```powershell
  Write-Verbose "[Sync-UtilsCloudFile] Cloud copy is newer ($cloudDate > $localDate) -> downloading."
  ```
- Named loops (`:MOVE_BLOBS`) for `continue`/`break` out of inner loops.
  ```powershell
  :MOVE_BLOBS foreach ($blob in $blobs) {
      foreach ($pattern in $ExcludePatterns) {
          if ($blob.Name -LIKE $pattern) {
              Write-Verbose "[Move-StorageBlob] '$($blob.Name)' matches '$pattern' -> skipping."
              continue MOVE_BLOBS
          }
      }
      ...
  }
  ```
- `SupportsShouldProcess` for mutating operations. Reset changed context in `CLEAN` (PowerShell 7.3+, also runs on errors and Ctrl+C). Below 7.3 use `try`/`finally` instead. In a file with chapters, every block holds its own chapters and `CLEAN` is the epilogue chapter, so no section stands outside a chapter.
  ```powershell
  [CmdletBinding(SupportsShouldProcess = $true)]
  param (...)

  BEGIN {
      $previousContext = Get-AzContext
      $null = Set-AzContext -Subscription $SubscriptionId
  }
  PROCESS {
      if ($PSCmdlet.ShouldProcess($blob.Name, "Move to '$Target'")) {
          ...
      }
  }
  CLEAN {
      $null = Set-AzContext -Context $previousContext
  }
  ```
- `throw [System.ArgumentException]::new('…')` with a concrete type, a bare `throw` only to rethrow.
  ```powershell
  try {
      $config = Get-Content -Path $ConfigPath -Raw | ConvertFrom-Json
  }
  catch {
      Write-Verbose "[Get-UtilsConfig] Failed to read '$ConfigPath'."
      throw
  }

  if ($null -EQ $config.Profiles) {
      throw [System.InvalidOperationException]::new("Config '$ConfigPath' must contain a 'Profiles' section.")
  }
  ```
- Parameters and central values in PascalCase, local helper values in camelCase.
  ```powershell
  param ([System.String] $ProfileName)

  $SyncProfile = Get-UtilsCloudSyncProfile -Name $ProfileName
  $localDate   = (Get-Item -Path $SyncProfile.Path).LastWriteTime
  ```
- **Tests in their own folder next to the module:** Pester tests live in `<Module>.Tests/`, not inside the module, so the module folder holds only what ships. The test folder mirrors the subfolders of the module, one test file per function (`Naming/Get-UtilsResourceName.ps1` → `Naming/Get-UtilsResourceName.Tests.ps1`), see the tree at the top of this file. A new function file gets its test file at the same relative path. The shared setup (module import, fixtures in `TestDrive`) lives once in `BeforeAll.ps1` at the root of the test folder, `AfterAll.ps1` next to it when cleanup is needed. Every test file opens with its chapter, see `SKILL.md` → "Errors, logging, tests".
  ```powershell
  # Utils.Tests/BeforeAll.ps1
  Import-Module (Join-Path -Path $PSScriptRoot -ChildPath '../Utils') -Force
  ```
  ```powershell
  # Utils.Tests/Naming/Get-UtilsResourceName.Tests.ps1
  BeforeAll {
      . $PSScriptRoot/../BeforeAll.ps1
  }

  Describe 'Get-UtilsResourceName' {
      It 'generates the expected name for <Type>' -ForEach @(
          @{ Type = 'vnet'; Expected = 'vnet-euwe-tst-01' }
          @{ Type = 'kv';   Expected = 'kv-euwe-tst-01' }
      ) {
          Get-UtilsResourceName -Type $Type -Environment tst | Should -Be $Expected
      }
  }
  ```
- **Strict from the first line:** every script and every `.psm1` sets `Set-StrictMode -Version 1.0` and `$ErrorActionPreference = 'Stop'` right after `param` (in `BEGIN` when the script uses blocks). Function files inherit both from the `.psm1`. Version 1.0 catches misspelled variables, `Latest` would also throw on missing properties and break checks like `$null -EQ $Config.Profiles`. `Stop` does not cover native commands, check `$LASTEXITCODE` after them.
  ```powershell
  param (...)

  Set-StrictMode -Version 1.0
  $ErrorActionPreference = 'Stop'

  terraform init -input=false
  if ($LASTEXITCODE -NE 0) {
      throw [System.InvalidOperationException]::new("terraform init must succeed, got exit code $LASTEXITCODE.")
  }
  ```
- **Verify** per `SKILL.md` → "Errors, logging, tests": parse every changed file, run `Invoke-ScriptAnalyzer -Severity Warning, Error` on the module and its tests, then the Pester tests of the module.
  ```powershell
  Invoke-ScriptAnalyzer -Path ./Utils -Recurse -Severity Warning, Error
  Invoke-ScriptAnalyzer -Path ./Utils.Tests -Recurse -Severity Warning, Error
  Invoke-Pester -Path ./Utils.Tests
  ```
