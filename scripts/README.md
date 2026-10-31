# Flowery.WinUI scripts

The active solution targets native Windows with WinUI 3 and the Windows App SDK.
All commands below run in PowerShell 7 from the repository root.

## Requirements

- Windows 10 version 2004 or later.
- The .NET SDK selected by `global.json`.
- Visual Studio with MSBuild and the Windows SDK.
- Microsoft Edge WebView2 Runtime for the shader-effect views.

The build uses the Microsoft.WindowsAppSDK version pinned in `Directory.Packages.props`.
It requires the native Windows toolchain listed above.

## Select the .NET SDK

The projects target `net10.0-windows10.0.19041`.
The SDK supplies the compiler and build tools; selecting SDK 11 does not change the application's .NET 10 target runtime.

`global.json` sets a minimum SDK of `10.0.203`, allows major-version roll-forward, and permits prerelease SDKs.
Use `-SdkVersion` to select one exact installed SDK.
Use `-DotNetPath` when that SDK belongs to a separate native `dotnet.exe` installation.
These options apply to the build, run, test, package, and publish scripts.
An explicit SDK selection fails if that version is unavailable.

```powershell
./scripts/build_all.ps1 -Configuration Release -NoRun -SdkVersion 10.0.402

$dotnetPath = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
./scripts/build_all.ps1 -Configuration Release -NoRun -SdkVersion 11.0.100-rc.2.26504.105 -DotNetPath $dotnetPath
./scripts/test_windows.ps1 -Configuration Release -SkipBuild
./scripts/test_kiosk.ps1 -Configuration Release -SkipBuild
```

With `-SkipBuild`, the test scripts run the existing binaries from the matching configuration.
Build that configuration with the selected SDK first.
The SDK 11 RC2 path is an early toolchain test, not a claim of formal Windows App SDK support for a .NET 11 target.
Native Visual Studio MSBuild, the selected SDK, and the Windows XAML compiler remain part of the build.
Each build log records the selected SDK and resolved tool paths without changing the system environment.

## Build the solution

```powershell
./scripts/build_all.ps1 -Configuration Debug -NoRun
./scripts/build_all.ps1 -Configuration Release -NoRun
```

The solution includes the core library, Kanban, Win2D, integrations, Gallery, Kiosk, and native test projects.
Builds use the normal `bin/<Configuration>/<Project>/<TargetFramework>/win-x64/` output directories.

The scripts accept `-Rebuild` and `-VerboseOutput`.
Each build creates a new log in `logs/`.
The log name includes the build type, configuration, SDK version, and timestamp.

## Build or run the Gallery

```powershell
./scripts/build_windows.ps1 -Configuration Debug -NoRun
./scripts/run-windows.ps1 -Configuration Debug
./scripts/run-windows.ps1 -Configuration Debug -NoWait
```

The build script starts the Gallery unless you specify `-NoRun`.
The run script waits for the Gallery to close unless you specify `-NoWait`.

## Build or run the Kiosk

```powershell
./scripts/build_kiosk.ps1 -Configuration Debug -NoRun
./scripts/run-kiosk.ps1 -Configuration Debug -SkipBuild
./scripts/run-kiosk.ps1 -Configuration Debug -SkipBuild -Manual
```

The Kiosk has six slides: Authentication, Data Visualization, Communication, Enhanced Controls, Runtime Scaling, and Background Patterns.
Each slide has its own theme, title, and keywords.
The progress indicator runs over the eight-second slide interval.
Autoplay advances through the slides unless you specify `-Manual`, which passes `--manual` to the executable.

Right, Space, or N advances to the next slide.
Left or P returns to the previous slide.
The run script waits for the application unless you specify `-NoWait`.
Like the test scripts, it places the window on a display only when you pass `-Monitor` (see [Run native tests](#run-native-tests)).

After the matching solution build, run the native Kiosk smoke checks:

```powershell
./scripts/test_kiosk.ps1 -Configuration Debug -SkipBuild
./scripts/test_kiosk.ps1 -Configuration Release -SkipBuild
```

The script launches the native Kiosk with an isolated state directory and an absolute XML report path.
Reports are in `TestResults/Kiosk/<Configuration>/<run>/kiosk-smoke.xml`.
Logs are in `logs/kiosk-tests-<Configuration>-<run>.log`.
The script checks process exit, unique case names, report counts, and individual outcomes.
Inconclusive checks are listed as warnings and do not fail the run.
A scaling checkpoint is inconclusive when only its zoom panel overflows the slide, which depends on the session's text measurement.

To check a published Kiosk, such as the NativeAOT output, pass its executable; the script then builds nothing:

```powershell
./scripts/publish_windows.ps1 -Configuration Release -Application Kiosk -Aot -OutputDir publish-kiosk
./scripts/test_kiosk.ps1 -Configuration Release -ExecutablePath publish-kiosk/Flowery.WinUI.Kiosk.exe
```

`-Configuration` then only names the report and log folders.

## Run the Gallery smoke run

```powershell
./scripts/test_gallery_smoke.ps1 -Configuration Debug -SkipBuild
./scripts/test_gallery_smoke.ps1 -Configuration Release -ExecutablePath publish/Flowery.WinUI.Gallery.exe
```

The script starts the Gallery with `--smoke-test` and an isolated state directory.
The Gallery opens every category in English, scrolls through it and records per category an exception, a page that does not load, and a localized text (`{Binding [Key]}`) that stays empty.
It also records a `NotSupportedException` on the UI thread, even a caught one: a NativeAOT build raises it for missing marshalling or binding code, which WinUI often reports only as `E_FAIL` or not at all.
WinUI raises `DebugSettings.BindingFailed` only with a debugger attached, so other failed bindings do not show up in the report.
A category that ends the Gallery fails with the entries of `crash.log` that name the cause, and the script starts the Gallery again behind it (`--smoke-skip <count>`), so one run names every such category.
It leaves the Credential Manager alone and exits with the result.
Reports are in `TestResults/GallerySmoke/<Configuration>/<run>/gallery-smoke.xml`, logs in `logs/gallery-smoke-<Configuration>-<run>.log`.
`-Monitor` and `-WindowRegion` place the window as for the other test scripts.

## Run native tests

```powershell
./scripts/test_windows.ps1 -Configuration Debug
./scripts/test_windows.ps1 -Configuration Release
```

If the matching solution build is complete, use `-SkipBuild`:

```powershell
./scripts/test_windows.ps1 -Configuration Debug -SkipBuild
```

Window placement is optional; without `-Monitor`, Windows places the window as usual.
To use one display, pass its number with `-Monitor` and a region with `-WindowRegion`: `Full`, `UpperHalf` or `LowerHalf` (the default).
`test_kiosk.ps1` and `run-kiosk.ps1` accept the same options. For example, for the lower half of display 2:

```powershell
./scripts/test_windows.ps1 -Configuration Debug -SkipBuild -Monitor 2 -WindowRegion LowerHalf
```

With `-Monitor`, the runtime test host resolves the Windows display device before it shows the window.
If the requested device is absent, the run fails without a fallback to another display.

The Windows runner starts `Flowery.WinUI.RuntimeTests.Host`, a WinUI application that only runs the runtime tests, and the tests run on its WinUI dispatcher.
The host loads the Gallery resources, assets and fonts; the Gallery itself contains no test code.
The suite includes the original control construction and global neumorphic tests.
It also includes the existing runtime tests and native resource tests.

The test script uses the matching host and runner executables from the normal build output.
It sets `FLOWERY_RUNTIME_TESTS_HOST_PATH` and a separate `FLOWERY_TEST_RESULTS_ROOT` for the runner.
The script writes test reports under `TestResults/` and process logs under `logs/`.

To run one group of native tests, pass a name substring with `-TestFilter`:

```powershell
./scripts/test_windows.ps1 -Configuration Release -SkipBuild -TestFilter Given_HighContrast
```

The filter matches the full class and method name, without case sensitivity.
A filter with no matches fails the run.
Omit the filter to run the complete suite.
For contrast-theme acceptance, apply each Windows contrast theme before the focused run.
The focused run does not replace the complete suite in the normal Windows theme.

## Create NuGet packages

```powershell
./scripts/build_nuget.ps1 -Configuration Release
```

This script builds the solution and creates packages in `nupkg/`.
It creates packages for Flowery.WinUI, Flowery.WinUI.Kanban and Flowery.WinUI.Win2D.
Use `-OutputDir` to select another package directory.
After a successful build of the same configuration, use `-SkipBuild` to package those binaries.

## Publish a Windows application

```powershell
./scripts/publish_windows.ps1 -Configuration Release
./scripts/publish_windows.ps1 -Configuration Release -Application Kiosk -OutputDir publish-kiosk
./scripts/publish_windows.ps1 -Configuration Release -Application Photos -OutputDir publish-photos
```

`-Application` accepts `Gallery`, `Kiosk` and `Photos`; the default is `Gallery`.
The default Gallery output is `publish/`.
The Kiosk example writes native Windows x64 output to `publish-kiosk/`.
The default output includes the .NET runtime and the Windows App SDK runtime.
The publish script uses the native XAML and PRI resource pipeline.

### NativeAOT

```powershell
./scripts/publish_windows.ps1 -Configuration Release -Aot -OutputDir publish
./scripts/publish_windows.ps1 -Configuration Release -Application Kiosk -Aot -OutputDir publish-kiosk
./scripts/publish_windows.ps1 -Configuration Release -Application Photos -Aot -OutputDir publish-photos
```

`-Aot` compiles the Gallery, the Kiosk or Photos ahead of time into one native executable, without the .NET runtime and JIT.
The releases publish all three applications this way.
The script passes `FloweryPublishAot=true`, which the application project turns into `PublishAot`; the referenced libraries stay normal builds.
The native symbols stay in the build output (`bin/Release/<Application>/.../native/`) and are not published.
A NativeAOT publish needs the Visual Studio C++ build tools (workload "Desktop development with C++") for the native linker.

A NativeAOT build reports a missing marshalling or binding entry only when the code runs, so check the published Gallery with the smoke run (`test_gallery_smoke.ps1 -ExecutablePath publish/Flowery.WinUI.Gallery.exe`, see above) and the published Photos by starting it:

```powershell
./scripts/test_photos_start.ps1 -ExecutablePath publish-photos/Flowery.WinUI.Photos.exe
```

The script starts Photos with an isolated state directory under `TestResults/PhotosStart/<run>/`.
It fails when the process ends before or within ten seconds after its window opens (`-SettleSeconds`), when it writes `crash.log`, or when it does not close on request.

## Build the Photos installer

```powershell
$nsis = ./scripts/get_nsis.ps1 -Directory "$env:LOCALAPPDATA/Flowery/Tools"
./scripts/build_photos_installer.ps1 -AppDirectory publish-photos -OutputDirectory final -NsisPath $nsis
./scripts/test_photos_installer.ps1 -InstallerPath final/Flowery.WinUI.Photos-Setup-1.4.1-x64.exe -AppDirectory publish-photos
```

`get_nsis.ps1` downloads the pinned NSIS 3.13 archive, checks its SHA-256 hash and returns the path of `makensis.exe`.
`build_photos_installer.ps1` compiles `installer/Flowery.WinUI.Photos.nsi` with the published Photos files.
Without `-Version`, it uses the version from `Directory.Build.props`.
The installer installs per user to `%LOCALAPPDATA%\Programs\Flowery Photos`, without administrator rights.
It creates a Start menu shortcut with the application icon and an entry in the Windows app list.
An update replaces the files in one transaction and restores the previous files if a file is locked.
The uninstaller removes only the files that the installer recorded; user files and settings remain.

`test_photos_installer.ps1` installs, updates and uninstalls into `TestResults/PhotosInstaller/<run>`.
It refuses to run if Flowery Photos is already installed for the current user.
Use `-LaunchGui` to also start the installed application once.

## Continuous integration

Run the NuGet security audit locally with the same native toolchain:

```powershell
./scripts/audit_nuget.ps1 -Configuration Release -SdkVersion 10.0.402
```

The audit restores every tracked C# project, including the tools and standalone unit-test project outside the solution.
It checks direct and transitive packages and fails on known vulnerabilities or unavailable vulnerability data.
The NuGet Audit workflow runs for pull requests, pushes to `main`, and weekly.
It has read-only repository access.

Dependabot checks NuGet and GitHub Actions weekly, with a limit of ten version-update PRs per ecosystem.
Related MSTest packages and their test-platform extensions update together.
Other package families and unrelated major upgrades remain separate.
The Dependency Graph workflow restores all C# projects on Windows and reports resolved NuGet dependencies from the default branch.
Only its graph update job has repository write access. The workflow does not run for pull requests.
This workflow does not publish NuGet packages. Only the manually started Release workflow publishes packages.
The repository's built-in automatic submission remains disabled because its Linux restore fails for these native Windows projects.

The CI workflow is configured to build and check Gallery and Kiosk in Debug and Release with SDK `10.0.402` and `11.0.100-rc.2.26504.105`, including the Gallery smoke run.
It also publishes the Kiosk and Photos with NativeAOT, runs the Kiosk smoke checks on the NativeAOT Kiosk and starts the NativeAOT Photos.
The release workflow creates the native packages, Gallery output, `Flowery.WinUI.Kiosk-Windows-x64.zip`, `Flowery.WinUI.Photos-Windows-x64.zip` and the Photos installer `Flowery.WinUI.Photos-Setup-<version>-x64.exe`.
Release publication remains a manual workflow action, with SDK `10.0.402` selected by default and SDK 11 RC2 available as an option.

The supported projects and scripts target native Windows only.
The kiosk workflow and shader-effect examples have native Windows host implementations with their functions preserved.
See [the migration record](../PORTING.md) for dated build results and runtime verification scope.
