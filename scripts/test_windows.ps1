<#
.SYNOPSIS
    Runs the unit tests and the native WinUI runtime tests through the Windows runner.
.DESCRIPTION
    Uses the normal solution output. Test state and reports use a unique directory.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [string]$TestFilter,
    [ValidateRange(0, 16)]
    [int]$Monitor = 0,
    [ValidateSet('Full', 'UpperHalf', 'LowerHalf')]
    [string]$WindowRegion = 'LowerHalf',
    [string]$SdkVersion,
    [string]$DotNetPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$framework = 'net10.0-windows10.0.19041'
$runtime = 'win-x64'

if (-not $IsWindows -or $PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script in native PowerShell 7 on Windows.'
}

if (-not $SkipBuild) {
    $buildScript = Join-Path $PSScriptRoot 'build_all.ps1'
    & $buildScript -Configuration $Configuration -NoRun -SdkVersion $SdkVersion -DotNetPath $DotNetPath
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$hostPath = Join-Path $repoRoot "bin\$Configuration\Flowery.WinUI.RuntimeTests.Host\$framework\$runtime\Flowery.WinUI.RuntimeTests.Host.exe"
$runnerPath = Join-Path $repoRoot "bin\$Configuration\Flowery.WinUI.RuntimeTests.WindowsRunner\$framework\$runtime\Flowery.WinUI.RuntimeTests.WindowsRunner.exe"
$unitTestsPath = Join-Path $repoRoot "bin\$Configuration\Flowery.WinUI.UnitTests\$framework\$runtime\Flowery.WinUI.UnitTests.exe"
foreach ($path in @($hostPath, $runnerPath, $unitTestsPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw ('Required executable is missing. Build the solution first: {0}' -f $path)
    }
}

$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$resultsDirectory = Join-Path $repoRoot "TestResults\$Configuration\$runId"
$logDirectory = Join-Path $repoRoot 'logs'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory "tests-$Configuration-$runId.log"
$runnerArguments = @('--report-trx', '--results-directory', $resultsDirectory)
$previousHostPath = [Environment]::GetEnvironmentVariable('FLOWERY_RUNTIME_TESTS_HOST_PATH', 'Process')
$previousResultsRoot = [Environment]::GetEnvironmentVariable('FLOWERY_TEST_RESULTS_ROOT', 'Process')
$previousTestFilter = [Environment]::GetEnvironmentVariable('FLOWERY_RUNTIME_TEST_FILTER', 'Process')
$previousMonitorDevice = [Environment]::GetEnvironmentVariable('FLOWERY_MONITOR_DEVICE', 'Process')
$previousMonitorRegion = [Environment]::GetEnvironmentVariable('FLOWERY_MONITOR_REGION', 'Process')

try {
    [Environment]::SetEnvironmentVariable('FLOWERY_RUNTIME_TESTS_HOST_PATH', $hostPath, 'Process')
    [Environment]::SetEnvironmentVariable('FLOWERY_TEST_RESULTS_ROOT', $resultsDirectory, 'Process')
    [Environment]::SetEnvironmentVariable('FLOWERY_RUNTIME_TEST_FILTER', $TestFilter, 'Process')
    if ($Monitor -gt 0) {
        $monitorDevice = '\\.\DISPLAY{0}' -f $Monitor
        $monitorRegion = switch ($WindowRegion) { 'UpperHalf' { 'upper-half' } 'LowerHalf' { 'lower-half' } default { 'full' } }
        [Environment]::SetEnvironmentVariable('FLOWERY_MONITOR_DEVICE', $monitorDevice, 'Process')
        [Environment]::SetEnvironmentVariable('FLOWERY_MONITOR_REGION', $monitorRegion, 'Process')
        Write-Host ('Window placement: {0}, {1}' -f $monitorDevice, $WindowRegion)
    }
    Write-Host ('Runtime test host: {0}' -f $hostPath)
    Write-Host ('Results: {0}' -f $resultsDirectory)
    # The filter selects runtime tests, so a filtered run skips the unit tests.
    $unitTestExitCode = 0
    if ([string]::IsNullOrWhiteSpace($TestFilter)) {
        & $unitTestsPath @runnerArguments 2>&1 | Tee-Object -FilePath $logPath
        $unitTestExitCode = $LASTEXITCODE
    }
    else {
        Write-Host 'Unit tests skipped: -TestFilter selects runtime tests.'
    }
    & $runnerPath @runnerArguments 2>&1 | Tee-Object -FilePath $logPath -Append
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -eq 0) {
        $testExitCode = $unitTestExitCode
    }
}
finally {
    [Environment]::SetEnvironmentVariable('FLOWERY_RUNTIME_TESTS_HOST_PATH', $previousHostPath, 'Process')
    [Environment]::SetEnvironmentVariable('FLOWERY_TEST_RESULTS_ROOT', $previousResultsRoot, 'Process')
    [Environment]::SetEnvironmentVariable('FLOWERY_RUNTIME_TEST_FILTER', $previousTestFilter, 'Process')
    [Environment]::SetEnvironmentVariable('FLOWERY_MONITOR_DEVICE', $previousMonitorDevice, 'Process')
    [Environment]::SetEnvironmentVariable('FLOWERY_MONITOR_REGION', $previousMonitorRegion, 'Process')
}

# Inconclusive runtime tests could not set up their scenario here, such as a window without room for it.
# They do not fail the run, but stay visible: as annotations in GitHub Actions, as warnings locally.
$reports = Get-ChildItem -LiteralPath $resultsDirectory -Filter 'runtime-tests-windows.xml' -Recurse -File |
    Where-Object { $_.Directory.Name -like 'native-runtime-*' }
foreach ($report in $reports) {
    foreach ($test in ([xml](Get-Content -LiteralPath $report.FullName -Raw -Encoding utf8)).tests.test) {
        if ($test.outcome -ne 'Inconclusive') {
            continue
        }
        $text = '{0}: {1}' -f $test.name, $test.message
        if ($env:GITHUB_ACTIONS -eq 'true') {
            $escaped = $text.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
            Write-Host ('::warning title=Inconclusive runtime test::{0}' -f $escaped)
        }
        else {
            Write-Warning $text
        }
    }
}

Write-Host ('Log: {0}' -f $logPath)
exit $testExitCode
