#Requires -Version 7.0
# NOTE:
#   Requires: Godot 4.7 .NET (path via -GodotPath or $env:GODOT) and a built project
#   (dotnet build). Does not build: several runs may go on at the same time.
#
#   ./tools/Show-Creature.ps1 -Template SHARK
#   ./tools/Show-Creature.ps1 -Template SHARK -OutFile C:\Temp\shark.png -Import
#   ./tools/Show-Creature.ps1 -Template SHARK -OutFile docs/creatures/shark.png -Hero

<#
.SYNOPSIS
    Renders one creature template from four sides into a PNG.

.DESCRIPTION
    Runs tests/CreatureShot.tscn with the movie writer and keeps the last frame: the
    template of data/creatures seen from the side, three quarters from the front, from
    the top and from the front. With -Import the project is imported first (needed after
    a model in assets/models changed); imports are serialized by a mutex, because two
    imports at once corrupt the import cache.

.EXAMPLE
    PS> ./tools/Show-Creature.ps1 -Template CRAB
    C:\...\Temp\cellout-shots\CRAB.png
#>
[CmdletBinding()]
param (
    <#
    [Required]
    Key of a template file in data/creatures, e.g. SHARK.
    #>
    [Parameter(Mandatory = $true)]
    [System.String]
    $Template,

    <#
    [Optional]
    Where the PNG is written. Defaults to the folder cellout-shots in the temp folder.
    #>
    [Parameter()]
    [System.String]
    $OutFile = (Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "cellout-shots/$Template.png"),

    <#
    [Optional]
    Path to the Godot .NET console binary. Defaults to $env:GODOT.
    #>
    [Parameter()]
    [System.String]
    $GodotPath = $env:GODOT,

    <#
    [Optional]
    Imports the project before the render, needed after a .glb changed.
    #>
    [Parameter()]
    [switch]
    $Import,

    <#
    [Optional]
    One big view three quarters from the front instead of the four sides, for galleries.
    #>
    [Parameter()]
    [switch]
    $Hero
)

Set-StrictMode -Version 1.0
$ErrorActionPreference = 'Stop'

$REPO_ROOT    = Resolve-Path -Path "$PSScriptRoot/.."
$IMPORT_MUTEX = 'Global\CellOutGodotImport'

if ([System.String]::IsNullOrEmpty($GodotPath) -OR -NOT (Test-Path -Path $GodotPath)) {
    throw [System.ArgumentException]::new("GodotPath must point to the Godot .NET console binary (or set `$env:GODOT), got '$GodotPath'.")
}

# Import: one at a time across every running script
if ($Import) {
    $mutex = [System.Threading.Mutex]::new($false, $IMPORT_MUTEX)
    $null  = $mutex.WaitOne()
    try {
        Write-Verbose "[Show-Creature] Importing the project."
        $null = & $GodotPath --path $REPO_ROOT --headless --import 2>&1
    }
    finally {
        $mutex.ReleaseMutex()
        $mutex.Dispose()
    }
}

# Render into a folder of its own, keep the last frame
$frames = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "cellout-shots/frames-$Template-$PID"
$null   = New-Item -ItemType Directory -Force -Path $frames
$env:CELLOUT_SHOT_TEMPLATE = $Template
$env:CELLOUT_SHOT_VIEW     = $Hero ? 'hero' : ''
$output = & $GodotPath --path $REPO_ROOT res://tests/CreatureShot.tscn --write-movie "$frames/f.png" --fixed-fps 30 2>&1
$last   = Get-ChildItem -Path $frames -Filter '*.png' | Sort-Object -Property Name | Select-Object -Last 1
if ($output | Select-String -Pattern 'CREATURE SHOT FAILED' -Quiet) {
    $message = ($output | Select-String -Pattern 'CREATURE SHOT FAILED' | Select-Object -First 1).Line
    Remove-Item -Path $frames -Recurse -Force -ErrorAction SilentlyContinue
    throw [System.InvalidOperationException]::new("The template '$Template' must build: $message")
}
if ($null -EQ $last) {
    $output | Select-Object -Last 15 | Write-Host
    throw [System.InvalidOperationException]::new("The render of '$Template' must write frames, got none. See the Godot output above.")
}
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Path $OutFile -Parent)
Move-Item -Path $last.FullName -Destination $OutFile -Force
Remove-Item -Path $frames -Recurse -Force

return (Resolve-Path -Path $OutFile).Path
