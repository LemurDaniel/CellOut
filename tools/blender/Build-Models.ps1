#Requires -Version 7.0
# NOTE:
#   Requires: Blender 5.2 (path via -BlenderPath, $env:BLENDER or the default install folder).
#
#   ./tools/blender/Build-Models.ps1
#   ./tools/blender/Build-Models.ps1 -BlenderPath 'D:\Blender\blender.exe'

#
#   Chapters:
#     BUILD MODELS   Resolves Blender, runs build.models.py headless, lists the models.
#

<#
.SYNOPSIS
    Builds the part models (.glb) of Cell Out with Blender: creature and cell parts.

.DESCRIPTION
    Runs tools/blender/build.models.py in a headless Blender with factory settings and
    writes one .glb per part to assets/models/creature/ and assets/models/cell/. Godot
    imports them on the next start or with --import.

.EXAMPLE
    PS> ./tools/blender/Build-Models.ps1
#>
[CmdletBinding()]
param (
    <#
    [Optional]
    Path to blender.exe. Defaults to $env:BLENDER, then the Blender 5.2 install folder.
    #>
    [Parameter()]
    [System.String]
    $BlenderPath = $env:BLENDER
)

Set-StrictMode -Version 1.0
$ErrorActionPreference = 'Stop'

$DEFAULT_BLENDER = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$REPO_ROOT       = Resolve-Path -Path "$PSScriptRoot/../.."
$SCRIPT_PATH     = Join-Path -Path $PSScriptRoot -ChildPath 'build.models.py'
$OUTPUT_DIR      = Join-Path -Path $REPO_ROOT -ChildPath 'assets/models'

# ===========================================================================
# ============================   BUILD MODELS   =============================
# ===========================================================================
#
#   1. Validations   Fails early without a Blender executable.
#   2. Build         Runs the generator headless, fails on a Python error.
#   3. Report        Lists the written models.
#

# ---------------------------------------------------------------------------
# Validations
#   Fails early without a Blender executable.
# ---------------------------------------------------------------------------

if ([System.String]::IsNullOrEmpty($BlenderPath)) {
    $BlenderPath = $DEFAULT_BLENDER
}
if (-NOT (Test-Path -Path $BlenderPath -PathType Leaf)) {
    throw [System.ArgumentException]::new("BlenderPath must point to blender.exe (Blender 5.2), got '$BlenderPath'.")
}

# ---------------------------------------------------------------------------
# Build
#   Runs the generator headless, fails on a Python error.
# ---------------------------------------------------------------------------

Write-Verbose "[Build-Models] Building with '$BlenderPath' into '$OUTPUT_DIR'."
$output = & $BlenderPath --background --factory-startup --python-exit-code 1 --python $SCRIPT_PATH -- $OUTPUT_DIR 2>&1
if (0 -NE $LASTEXITCODE -OR ($output -match 'Traceback')) {
    $output | Select-Object -Last 30 | ForEach-Object { Write-Warning $_ }
    throw [System.InvalidOperationException]::new("Blender must build every part without error, exit code '$LASTEXITCODE'.")
}

# ---------------------------------------------------------------------------
# Report
#   Lists the written models.
# ---------------------------------------------------------------------------

return Get-ChildItem -Path $OUTPUT_DIR -Filter '*.glb' -Recurse | Select-Object -Property Name, Length
