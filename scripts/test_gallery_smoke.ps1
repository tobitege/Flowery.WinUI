#requires -Version 7.0
# Runs the Gallery's smoke run (--smoke-test): it opens every category, scrolls through it and reports per category
# an exception, a page that does not load, or a localized text ({Binding [Key]}) that stays empty. A category that ends
# the Gallery fails with the entries of crash.log, and the run goes on behind it in a new process, so one run names every
# such category. Use -ExecutablePath for a published Gallery, such as the output of publish_windows.ps1.
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
$crashLog = Join-Path $resultsRoot 'state\crash.log'

# Runs the Gallery once, behind the first $Skip categories, and returns the categories it started and the cases it
# reported. The Gallery saves its report after every category, so a crash keeps the categories checked before it.
function Invoke-GalleryRun([int]$Skip, [int]$Part) {
    $partPath = Join-Path $resultsRoot ('gallery-smoke-part{0}.xml' -f $Part)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executable
    $startInfo.WorkingDirectory = Split-Path -Parent $executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('--smoke-test')
    $startInfo.ArgumentList.Add($partPath)
    if ($Skip -gt 0) {
        $startInfo.ArgumentList.Add('--smoke-skip')
        $startInfo.ArgumentList.Add([string]$Skip)
    }
    $startInfo.Environment['FLOWERY_STATE_ROOT'] = Join-Path $resultsRoot 'state'
    if ($Monitor -gt 0) {
        $startInfo.Environment['FLOWERY_MONITOR_DEVICE'] = '\\.\DISPLAY{0}' -f $Monitor
        $startInfo.Environment['FLOWERY_MONITOR_REGION'] = switch ($WindowRegion) { 'UpperHalf' { 'upper-half' } 'LowerHalf' { 'lower-half' } default { 'full' } }
    }
    $crashLines = if (Test-Path -LiteralPath $crashLog -PathType Leaf) { @(Get-Content -LiteralPath $crashLog -Encoding utf8).Count } else { 0 }
    $process = [Diagnostics.Process]::Start($startInfo)
    Write-Host ('Gallery process: {0}; skipped categories: {1}; report: {2}' -f $process.Id, $Skip, $partPath)
    try {
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(600000)) {
            $process.Kill($true)
            throw ('Gallery smoke run timed out after 10 minutes; process {0} was ended.' -f $process.Id)
        }
        $exitCode = $process.ExitCode
        $output = $stdoutTask.GetAwaiter().GetResult() + $stderrTask.GetAwaiter().GetResult()
        $output | Add-Content -LiteralPath $logPath -Encoding utf8
    }
    finally { $process.Dispose() }
    $cases = @()
    if (Test-Path -LiteralPath $partPath -PathType Leaf) {
        [xml]$part = Get-Content -LiteralPath $partPath -Raw -Encoding utf8
        $cases = @($part.tests.test)
    }
    # crash.log also lists first-chance exceptions; the other entries of this run name what ended the Gallery. A failure
    # in native layout leaves only first-chance entries: then the last one that is no COMException names the cause (such
    # as a NotSupportedException of a NativeAOT build), or else the last one.
    $entries = [Collections.Generic.List[string[]]]::new()
    if (Test-Path -LiteralPath $crashLog -PathType Leaf) {
        $entry = $null
        foreach ($line in @(Get-Content -LiteralPath $crashLog -Encoding utf8 | Select-Object -Skip $crashLines)) {
            if ($line -match '^\[\d{4}-' -or $null -eq $entry) {
                if ($null -ne $entry) { $entries.Add($entry.ToArray()) }
                $entry = [Collections.Generic.List[string]]::new()
            }
            $entry.Add($line)
        }
        if ($null -ne $entry) { $entries.Add($entry.ToArray()) }
    }
    $fatalEntries = @($entries | Where-Object { $_[0] -notmatch '\] AppDomain\.FirstChanceException: ' })
    if ($fatalEntries.Count -eq 0 -and $entries.Count -gt 0) {
        $causes = @($entries | Where-Object { $_[0] -notmatch 'FirstChanceException: System\.Runtime\.InteropServices\.COMException' })
        $fatalEntries = @(, $(if ($causes.Count -gt 0) { $causes[$causes.Count - 1] } else { $entries[$entries.Count - 1] }))
    }
    $fatal = @($fatalEntries | ForEach-Object { $_ } | Select-Object -First 40)
    $started = @($output -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'Checking *' } | ForEach-Object { $_.Substring(9) })
    [pscustomobject]@{ ExitCode = $exitCode; Started = $started; Cases = $cases; Fatal = $fatal }
}

# A category that ends the Gallery fails with what crash.log names; the run goes on behind it in a new process.
$results = [Collections.Generic.List[object]]::new()
$skip = 0
$exitCode = 0
for ($part = 1; ; $part++) {
    $run = Invoke-GalleryRun -Skip $skip -Part $part
    foreach ($case in $run.Cases) {
        $results.Add([pscustomobject]@{ Name = $case.name; Outcome = $case.outcome; Message = $case.message })
    }
    if ($run.Started.Count -le $run.Cases.Count) {
        if ($run.Cases.Count -eq 0) {
            $run.Fatal | Write-Host
            throw ('Gallery did not write its report; exit code {0}. Log: {1}' -f $run.ExitCode, $logPath)
        }
        $exitCode = $run.ExitCode
        if ($exitCode -notin 0, 1) {
            $message = @("The Gallery ended with exit code $exitCode after its last category.") + $run.Fatal
            $results.Add([pscustomobject]@{ Name = 'GalleryExit'; Outcome = 'Failed'; Message = $message -join "`n" })
        }
        break
    }
    $crashed = $run.Started[$run.Cases.Count]
    $message = @("The Gallery ended with exit code $($run.ExitCode) while checking this category.") + $run.Fatal
    $results.Add([pscustomobject]@{ Name = 'Category.' + $crashed; Outcome = 'Failed'; Message = $message -join "`n" })
    Write-Host ('Gallery ended while checking {0}; going on behind it.' -f $crashed)
    $exitCode = 1
    $skip += $run.Cases.Count + 1
}

$total = $results.Count
$failedCases = @($results | Where-Object { $_.Outcome -eq 'Failed' })
$inconclusive = @($results | Where-Object { $_.Outcome -eq 'Inconclusive' }).Count
$failed = $failedCases.Count
$passed = $total - $failed - $inconclusive
$document = [Xml.Linq.XDocument]::new([Xml.Linq.XElement]::new('tests',
    [Xml.Linq.XAttribute]::new('total', $total), [Xml.Linq.XAttribute]::new('passed', $passed),
    [Xml.Linq.XAttribute]::new('failed', $failed), [Xml.Linq.XAttribute]::new('inconclusive', $inconclusive),
    [object[]]@($results | ForEach-Object {
        [Xml.Linq.XElement]::new('test', [Xml.Linq.XAttribute]::new('name', $_.Name), [Xml.Linq.XAttribute]::new('outcome', $_.Outcome),
            [Xml.Linq.XElement]::new('message', $_.Message))
    })))
$document.Save($reportPath)
if ($total -lt 1) { throw 'Gallery smoke report has no entries.' }
Write-Host ('Gallery: {0} passed, {1} failed, {2} inconclusive. Report: {3}' -f $passed, $failed, $inconclusive, $reportPath)
foreach ($case in $failedCases) {
    $text = '{0}: {1}' -f $case.Name, (($case.Message -split "`n" | Select-Object -First 3) -join ' ')
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host ('::error title=Gallery smoke run::{0}' -f $text.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')) }
    else { Write-Host $text }
}
if ($exitCode -ne 0 -or $failed -ne 0) { exit 1 }
exit 0
