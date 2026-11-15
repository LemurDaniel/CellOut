#Requires -Version 7.0
# NOTE:
#   Requires: Blender 5.2 or newer (path via -BlenderPath, $env:BLENDER or the default install folders).
#
#   ./tools/blender/Show-Model.ps1 -Module mouths -Call 'build_jaw(scene)' -Name jaw
#   ./tools/blender/Show-Model.ps1 -Module limbs -Call "build_foot(scene, 'paw')" -Name paw -OutDir C:\Temp\prev

<#
.SYNOPSIS
    Renders one model of the Blender generator from three sides, before the game knows it.

.DESCRIPTION
    Runs tools/blender/preview.model.py in a headless Blender: builds the model with the
    given call of a builder of tools/blender/models/<Module>.py and writes
    <Name>.side.png, <Name>.front.png and <Name>.top.png. Nothing is exported, the
    models of the game are built with Build-Models.ps1.

.EXAMPLE
    PS> ./tools/blender/Show-Model.ps1 -Module organs -Call 'build_lure(scene)' -Name lure
#>
[CmdletBinding()]
param (
    <#
    [Required]
    Module below tools/blender/models without .py, e.g. mouths.
    #>
    [Parameter(Mandatory = $true)]
    [System.String]
    $Module,

    <#
    [Required]
    Python call of the builder, the scene is the variable scene, e.g. 'build_jaw(scene)'.
    #>
    [Parameter(Mandatory = $true)]
    [System.String]
    $Call,

    <#
    [Required]
    File name of the previews without extension.
    #>
    [Parameter(Mandatory = $true)]
    [System.String]
    $Name,

    <#
    [Optional]
    Folder the previews are written to. Defaults to cellout-models in the temp folder.
    #>
    [Parameter()]
    [System.String]
    $OutDir = (Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'cellout-models'),

    <#
    [Optional]
    Path to blender.exe. Defaults to $env:BLENDER, then the newest install below Program Files.
    #>
    [Parameter()]
    [System.String]
    $BlenderPath = $env:BLENDER
)

Set-StrictMode -Version 1.0
$ErrorActionPreference = 'Stop'

$SCRIPT_PATH = Join-Path -Path $PSScriptRoot -ChildPath 'preview.model.py'

if ([System.String]::IsNullOrEmpty($BlenderPath)) {
    $BlenderPath = Get-ChildItem -Path 'C:\Program Files\Blender Foundation' -Filter 'blender.exe' -Recurse -ErrorAction SilentlyContinue
    | Sort-Object -Property FullName -Descending
    | Select-Object -First 1 -ExpandProperty FullName
}
if ([System.String]::IsNullOrEmpty($BlenderPath) -OR -NOT (Test-Path -Path $BlenderPath)) {
    throw [System.ArgumentException]::new("BlenderPath must point to blender.exe (or set `$env:BLENDER), got '$BlenderPath'.")
}

$output = & $BlenderPath -b --factory-startup -P $SCRIPT_PATH -- $OutDir $Name $Module $Call 2>&1
if (0 -NE $LASTEXITCODE -OR ($output | Select-String -Pattern 'Traceback|Error:' -Quiet)) {
    $output | Select-Object -Last 25 | Write-Host
    throw [System.InvalidOperationException]::new("The preview of '$Call' must render, Blender reported an error (see above).")
}
$output | Select-String -Pattern 'Bounds' | ForEach-Object { Write-Verbose $_.Line }

return Get-ChildItem -Path $OutDir -Filter "$Name.*.png" | Select-Object -ExpandProperty FullName
