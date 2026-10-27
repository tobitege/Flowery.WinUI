#requires -Version 7.0
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$OutputDir = './nupkg',
    [switch]$SkipBuild,
    [string]$SdkVersion,
    [string]$DotNetPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'build_core.ps1')
$repoRoot = Get-RepoRoot
if (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $repoRoot $OutputDir
}
if (-not $SkipBuild) {
    $buildScript = Join-Path $PSScriptRoot 'build_all.ps1'
    & $buildScript -Configuration $Configuration -NoRun -SdkVersion $SdkVersion -DotNetPath $DotNetPath
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Initialize-BuildEnvironment -HeadName 'packages' -LogFileName 'nuget-pack' -Configuration $Configuration -SdkVersion $SdkVersion -DotNetPath $DotNetPath
$projects = @('Flowery.WinUI', 'Flowery.WinUI.Kanban', 'Flowery.WinUI.Win2D')
foreach ($name in $projects) {
    $project = Join-Path $repoRoot ('{0}\{0}.csproj' -f $name)
    $packArgs = @($project, '-t:Pack', '-nologo', '-nodeReuse:false', '-m:1', '-p:NoBuild=true',
        ('-p:Configuration={0}' -f $Configuration), ('-p:PackageOutputPath={0}' -f $OutputDir), '-p:Platform=x64')
    if (-not (Invoke-MSBuildOperation -Title ('Pack {0}' -f $name) -Arguments $packArgs)) {
        Write-BuildFailure $name
        exit 1
    }
}
Write-BuildSummary
Write-Host ('Packages: {0}' -f $OutputDir)
