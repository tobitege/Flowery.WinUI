#requires -Version 7.0
# Runs the Gallery's smoke run (--smoke-test): it opens every category, scrolls through it and reports per category
# an exception, a page that does not load, or a bound text that stays empty. Use -ExecutablePath for a published
# Gallery, such as the NativeAOT output of publish_windows.ps1 -Aot.
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [ValidateRange(0, 16)][int]$Monitor = 0,
    [ValidateSet('Full', 'UpperHalf', 'LowerHalf')][string]$WindowRegion = 'LowerHalf',
    [string]$SdkVersion,
    [string]$DotNetPath,
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Run this script in native PowerShell 7 on Windows.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
if ($ExecutablePath) {
    $executable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
}
else {
    if (-not $SkipBuild) {
        $buildScript = Join-Path $PSScriptRoot 'build_all.ps1'
        & $buildScript -Configuration $Configuration -NoRun -SdkVersion $SdkVersion -DotNetPath $DotNetPath
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    $relative = 'bin\{0}\Flowery.WinUI.Gallery\net10.0-windows10.0.19041\win-x64\Flowery.WinUI.Gallery.exe' -f $Configuration
    $executable = Join-Path $repoRoot $relative
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw ('Gallery executable is missing. Build the solution first: {0}' -f $executable) }
$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$resultsRoot = Join-Path $repoRoot ('TestResults\GallerySmoke\{0}\{1}' -f $Configuration, $runId)
$logRoot = Join-Path $repoRoot 'logs'
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$reportPath = Join-Path $resultsRoot 'gallery-smoke.xml'
$logPath = Join-Path $logRoot ('gallery-smoke-{0}-{1}.log' -f $Configuration, $runId)
$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $executable
$startInfo.WorkingDirectory = Split-Path -Parent $executable
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.ArgumentList.Add('--smoke-test')
$startInfo.ArgumentList.Add($reportPath)
$startInfo.Environment['FLOWERY_STATE_ROOT'] = Join-Path $resultsRoot 'state'
if ($Monitor -gt 0) {
    $startInfo.Environment['FLOWERY_MONITOR_DEVICE'] = '\\.\DISPLAY{0}' -f $Monitor
    $startInfo.Environment['FLOWERY_MONITOR_REGION'] = switch ($WindowRegion) { 'UpperHalf' { 'upper-half' } 'LowerHalf' { 'lower-half' } default { 'full' } }
}
$process = [Diagnostics.Process]::Start($startInfo)
Write-Host ('Gallery process: {0}; report: {1}' -f $process.Id, $reportPath)
try {
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(600000)) {
        $process.Kill($true)
        throw ('Gallery smoke run timed out after 10 minutes; process {0} was ended.' -f $process.Id)
    }
    $exitCode = $process.ExitCode
    $output = $stdoutTask.GetAwaiter().GetResult() + $stderrTask.GetAwaiter().GetResult()
    $output | Set-Content -LiteralPath $logPath -Encoding utf8
}
finally { $process.Dispose() }
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw ('Gallery did not write its report; exit code {0}. Log: {1}' -f $exitCode, $logPath) }
[xml]$report = Get-Content -LiteralPath $reportPath -Raw -Encoding utf8
$cases = @($report.tests.test)
$total = [int]$report.tests.total
$passed = [int]$report.tests.passed
$failed = [int]$report.tests.failed
$inconclusive = [int]$report.tests.inconclusive
if ($total -lt 1 -or $cases.Count -ne $total -or $passed + $failed + $inconclusive -ne $total) { throw 'Gallery smoke report has invalid counts.' }
$failedCases = @($cases | Where-Object { $_.outcome -eq 'Failed' })
Write-Host ('Gallery: {0} passed, {1} failed, {2} inconclusive. Report: {3}' -f $passed, $failed, $inconclusive, $reportPath)
foreach ($case in $failedCases) {
    $text = '{0}: {1}' -f $case.name, (($case.message -split "`n" | Select-Object -First 3) -join ' ')
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host ('::error title=Gallery smoke run::{0}' -f $text.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')) }
    else { Write-Host $text }
}
if ($exitCode -ne 0 -or $failed -ne 0 -or $failedCases.Count -ne $failed) { exit 1 }
exit 0
