#Requires -Version 7.0
# NOTE:
#   Requires: .NET SDK 8+ and Godot 4.7 .NET (the *_mono_win64_console.exe on Windows,
#   the normal exe prints nothing). Path via -GodotPath or $env:GODOT.
#
#   ./tools/Test-CellOut.ps1 -GodotPath 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe'

#
#   Chapters:
#     HELPERS          Runs one Godot call with timeout and output check.
#     BUILD AND TEST   Validates Godot, builds C#, runs the unit tests, imports and runs the smoke test.
#

<#
.SYNOPSIS
    Unit tests and headless smoke test for Cell Out.

.DESCRIPTION
    Builds the C# project, runs the unit tests of the pure logic (tests/unit, no Godot),
    imports the Godot project and runs tests/SmokeTest.tscn. Fails on a build error, a
    failed unit test, an exit code other than 0, SCRIPT ERROR or exceptions in the
    output, or a timeout.

.EXAMPLE
    Run with the Godot path from the environment:

    PS> $env:GODOT = 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe'
    PS> ./tools/Test-CellOut.ps1
#>
# Write-Host on purpose: an interactive test runner with colored console output.
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive test runner, colored console output.')]
[CmdletBinding()]
param (
    <#
    [Optional]
    Path to the Godot .NET console binary. Default is $env:GODOT, else 'godot' from PATH.
    #>
    [Parameter(
        Mandatory = $false
    )]
    [System.String]
    $GodotPath = $(if ($env:GODOT) { $env:GODOT } else { 'godot' }),

    <#
    [Optional]
    Root of the Godot project. Default is the parent folder of this script.
    #>
    [Parameter(
        Mandatory = $false
    )]
    [System.String]
    $ProjectPath = (Split-Path -Parent $PSScriptRoot),

    <#
    [Optional]
    Hard limit per Godot call in seconds. Default is 120.
    #>
    [Parameter(
        Mandatory = $false
    )]
    [ValidateRange(10, 600)]
    [System.Int32]
    $TimeoutSeconds = 120
)

Set-StrictMode -Version 1.0
$ErrorActionPreference = 'Stop'

$FAIL_PATTERN = 'SCRIPT ERROR|Parse Error|Failed to load script|Invalid call|Unhandled exception|System\.\w+Exception'
$SHOW_PATTERN = 'PASS|FAIL|SMOKE|ERROR|Exception'

# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   Invoke-Godot   Runs Godot in a thread job with timeout, prints the checks and fails on errors.
#

function Invoke-Godot {
    # Exe and Arguments are used via $using: inside the thread job, the analyzer misses that.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '', Justification = 'Used via $using: in Start-ThreadJob.')]
    param (
        [System.String]   $Exe,
        [System.String[]] $Arguments,
        [System.String]   $Label,
        [System.Int32]    $Timeout
    )

    Write-Host "▶ $Label" -ForegroundColor Cyan
    $job = Start-ThreadJob -ScriptBlock {
        $output = & $using:Exe @using:Arguments 2>&1 | ForEach-Object { "$_" }
        [PSCustomObject]@{ Output = $output; ExitCode = $LASTEXITCODE }
    }

    if (-NOT (Wait-Job -Job $job -Timeout $Timeout)) {
        Stop-Job -Job $job
        throw [System.TimeoutException]::new("$Label must finish within $Timeout s.")
    }
    $result = Receive-Job -Job $job
    Remove-Job -Job $job

    $result.Output
    | Where-Object { $_ -MATCH $SHOW_PATTERN }
    | ForEach-Object { Write-Host "  $_" }

    $errors = @($result.Output | Where-Object { $_ -MATCH $FAIL_PATTERN })
    if ($errors.Count -GT 0) {
        $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        throw [System.InvalidOperationException]::new("$Label must run without script errors, found $($errors.Count).")
    }
    if ($result.ExitCode -NE 0) {
        throw [System.InvalidOperationException]::new("$Label must exit with code 0, got $($result.ExitCode).")
    }
}

# ===========================================================================
# ===========================   BUILD AND TEST   ============================
# ===========================================================================
#
#   1. Validations   Fails early when Godot is missing.
#   2. Build         Compiles the C# project, Godot loads the assembly from .godot/mono.
#   3. Unit tests    Checks the pure logic against the frozen balance fixture, without Godot.
#   4. Smoke test    Imports the project, then plays through all stages headless.
#

# ---------------------------------------------------------------------------
# Validations
#   Fails early when Godot is missing.
# ---------------------------------------------------------------------------

if ($null -EQ (Get-Command -Name $GodotPath -ErrorAction SilentlyContinue)) {
    throw [System.ArgumentException]::new("GodotPath must point to the Godot .NET binary, got '$GodotPath'. Set -GodotPath or `$env:GODOT.")
}

# ---------------------------------------------------------------------------
# Build
#   Compiles the C# project, Godot loads the assembly from .godot/mono.
# ---------------------------------------------------------------------------

Write-Host '▶ Build' -ForegroundColor Cyan
dotnet build (Join-Path -Path $ProjectPath -ChildPath 'CellOut.csproj') -nologo -v quiet
if ($LASTEXITCODE -NE 0) {
    throw [System.InvalidOperationException]::new("dotnet build must succeed, got exit code $LASTEXITCODE.")
}

# ---------------------------------------------------------------------------
# Unit tests
#   Checks the pure logic against the frozen balance fixture, without Godot.
# ---------------------------------------------------------------------------

Write-Host '▶ Unit-Tests' -ForegroundColor Cyan
dotnet test (Join-Path -Path $ProjectPath -ChildPath 'tests/unit/CellOut.Tests.csproj') --nologo -v quiet
if ($LASTEXITCODE -NE 0) {
    throw [System.InvalidOperationException]::new("dotnet test must pass every unit test, got exit code $LASTEXITCODE.")
}

# ---------------------------------------------------------------------------
# Smoke test
#   Imports the project, then plays through all stages headless.
# ---------------------------------------------------------------------------

$GodotConfig = @{
    Exe     = $GodotPath
    Timeout = $TimeoutSeconds
}
Invoke-Godot @GodotConfig -Label 'Import' -Arguments @('--headless', '--path', $ProjectPath, '--import')
Invoke-Godot @GodotConfig -Label 'Smoke-Test' -Arguments @('--headless', '--path', $ProjectPath, 'res://tests/SmokeTest.tscn')

Write-Host '✅ Cell Out Unit- und Smoke-Test grün' -ForegroundColor Green
