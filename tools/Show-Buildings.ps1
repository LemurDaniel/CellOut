#Requires -Version 7.0
# NOTE:
#   Requires: Godot 4.7 .NET console binary (path via -GodotPath or $env:GODOT) and a built project (dotnet build).
#
#   ./tools/Show-Buildings.ps1                         # every template of every function
#   ./tools/Show-Buildings.ps1 -What Dwelling          # the templates of one function
#   ./tools/Show-Buildings.ps1 -What blocks -Page 1    # twelve single blocks per page
#   ./tools/Show-Buildings.ps1 -What group:Roof -Import

<#
.SYNOPSIS
    Renders the buildings or the blocks of the village builder into one picture.

.DESCRIPTION
    Runs tests/BuildingShot.tscn with the movie writer and keeps the last frame: every
    template of every building function side by side, the templates of one function, a
    page of single blocks or the blocks of one palette group. For looking at the models
    after a change in tools/blender/models (run with -Import once after Build-Models.ps1).

.EXAMPLE
    PS> ./tools/Show-Buildings.ps1 -What group:Trade -OutFile C:\Temp\trade.png
#>
[CmdletBinding()]
param (
    <#
    [Optional]
    What to show: 'templates', 'blocks', a building function (Dwelling, Market ...) or 'group:<BlockGroup>'. Defaults to 'templates'.
    #>
    [Parameter()]
    [System.String]
    $What = 'templates',

    <#
    [Optional]
    With 'blocks': the page of twelve blocks, from 0. Defaults to 0.
    #>
    [Parameter()]
    [ValidateRange(0, 20)]
    [System.Int32]
    $Page = 0,

    <#
    [Optional]
    File the picture is written to. Defaults to cellout-shots/buildings.<what>.<page>.png in the temp folder.
    #>
    [Parameter()]
    [System.String]
    $OutFile = (Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "cellout-shots/buildings.$($What -replace ':', '-').$Page.png"),

    <#
    [Optional]
    Path to the Godot .NET console binary. Defaults to $env:GODOT.
    #>
    [Parameter()]
    [System.String]
    $GodotPath = $env:GODOT,

    <#
    [Optional]
    Imports the project first, needed once after models changed.
    #>
    [Parameter()]
    [switch]
    $Import
)

Set-StrictMode -Version 1.0
$ErrorActionPreference = 'Stop'

$REPO_ROOT = Resolve-Path -Path "$PSScriptRoot/.."

if ([System.String]::IsNullOrEmpty($GodotPath) -OR -NOT (Test-Path -Path $GodotPath)) {
    throw [System.ArgumentException]::new("GodotPath must point to the Godot .NET console binary (or set `$env:GODOT), got '$GodotPath'.")
}

if ($Import) {
    Write-Verbose "[Show-Buildings] Importing the project."
    $null = & $GodotPath --path $REPO_ROOT --headless --import 2>&1
}

# Render into a folder of its own, keep the last frame
$frames = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "cellout-shots/frames-buildings-$PID"
$null   = New-Item -ItemType Directory -Force -Path $frames
$env:CELLOUT_SHOT_BUILDINGS = $What
$env:CELLOUT_SHOT_PAGE      = "$Page"
$output = & $GodotPath --path $REPO_ROOT res://tests/BuildingShot.tscn --write-movie "$frames/f.png" --fixed-fps 30 --resolution 1920x1080 2>&1
$last   = Get-ChildItem -Path $frames -Filter '*.png' | Sort-Object -Property Name | Select-Object -Last 1
if ($null -EQ $last) {
    $output | Select-Object -Last 15 | Write-Host
    throw [System.InvalidOperationException]::new("The render of '$What' must write frames, got none. See the Godot output above.")
}
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Path $OutFile -Parent)
Move-Item -Path $last.FullName -Destination $OutFile -Force
Remove-Item -Path $frames -Recurse -Force

return (Resolve-Path -Path $OutFile).Path
