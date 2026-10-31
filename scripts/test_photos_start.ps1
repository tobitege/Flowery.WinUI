#requires -Version 7.0
# Starts a published Photos, such as the NativeAOT output of publish_windows.ps1 -Aot, with an isolated state
# directory. Fails when the process ends before its window opens or within the settle time, when it writes crash.log
# (unhandled and unobserved exceptions, also the ones the app survives), or when it does not close on request.
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [ValidateRange(1, 120)][int]$SettleSeconds = 10
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Run this script in native PowerShell 7 on Windows.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$executable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$stateRoot = Join-Path $repoRoot ('TestResults\PhotosStart\{0}\state' -f $runId)
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
$crashLog = Join-Path $stateRoot 'crash.log'

function Assert-NoCrashLog([string]$Stage) {
    if (Test-Path -LiteralPath $crashLog -PathType Leaf) {
        Get-Content -LiteralPath $crashLog -Raw -Encoding utf8 | Write-Host
        throw ('Photos wrote crash.log {0}: {1}' -f $Stage, $crashLog)
    }
}

$startInfo = [Diagnostics.ProcessStartInfo]::new($executable)
$startInfo.WorkingDirectory = Split-Path -Parent $executable
$startInfo.UseShellExecute = $false
$startInfo.Environment['FLOWERY_STATE_ROOT'] = $stateRoot
$process = [Diagnostics.Process]::Start($startInfo)
Write-Host ('Photos process: {0}; state: {1}' -f $process.Id, $stateRoot)
try {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        if ($process.HasExited) { Assert-NoCrashLog 'before its window opened'; throw ('Photos exited with code {0} before its window opened.' -f $process.ExitCode) }
        if ($watch.Elapsed.TotalSeconds -gt 60) { throw 'Photos opened no window within 60 seconds.' }
        Start-Sleep -Milliseconds 100
        $process.Refresh()
    }
    Write-Host ('Window after {0:N0} ms: {1}' -f $watch.Elapsed.TotalMilliseconds, $process.MainWindowTitle)

    Start-Sleep -Seconds $SettleSeconds
    if ($process.HasExited) { Assert-NoCrashLog 'while it settled'; throw ('Photos exited with code {0} within {1} seconds.' -f $process.ExitCode, $SettleSeconds) }
    Assert-NoCrashLog 'while it settled'

    $null = $process.CloseMainWindow()
    if (-not $process.WaitForExit(15000)) { throw 'Photos did not close within 15 seconds.' }
    Assert-NoCrashLog 'while it closed'
}
finally {
    if (-not $process.HasExited) { $process.Kill($true) }
    $process.Dispose()
}
Write-Host 'Photos started, ran and closed without crash.log.'
exit 0
