# Building from source

This is optional. Most users should simply download the prebuilt executable from the
[latest release](https://github.com/Freenitial/Disk_Space_Tracker/releases/latest). This document is
for those who want to compile the application themselves.

## Prerequisites

- **Windows 10 version 1809 (build 17763) or later**, 64-bit.
- **.NET 11 SDK.**
- For the single-file `Release` publish only: **Visual Studio 2022 Build Tools** (or the full IDE)
  with the **Desktop development with C++** workload and a **Windows 10/11 SDK**. The NativeAOT
  linker needs the MSVC toolchain. The `Debug` build does **not** require it.

NuGet sources are already configured in [`NuGet.Config`](NuGet.Config): the application depends on an
Avalonia 12.1 build from the Avalonia nightly feed, which `dotnet restore` pulls automatically.

## Get the code

```powershell
git clone https://github.com/Freenitial/Disk_Space_Tracker.git
cd Disk_Space_Tracker
```

## Build, run and test

```powershell
# Restore + build (Debug)
dotnet build

# Run
dotnet run --project DiskSpaceTracker.csproj

# Run the test suite
dotnet test
```

## Publish the single-file executable

The NativeAOT publish invokes the MSVC linker, so run it from a **Developer PowerShell for VS 2022**
(its environment puts the C++ toolchain and `vswhere.exe` on `PATH`):

```powershell
dotnet publish DiskSpaceTracker.csproj -c Release -r win-x64
```

Output:

```
bin\Release\net11.0-windows10.0.17763.0\win-x64\publish\DiskSpaceTracker.exe
```

A standalone, single-file application (~27 MB) that needs no .NET runtime on the target machine.

> The published file is named `DiskSpaceTracker.exe`. If you upload it as a GitHub release asset,
> rename it to `Disk_Space_Tracker.exe` to match the download link used in the README.

If the publish ever fails with `MSB3073` mentioning `vswhere.exe`, it means the MSVC environment was
not on `PATH` — run the command from the Developer PowerShell as noted above. A transient `MSB3061`
"access denied" on the output `.exe` is usually antivirus briefly locking the freshly written file;
re-run the publish.

## Project structure

```
DiskSpaceTracker.slnx          Solution
DiskSpaceTracker.csproj        Application project (WinExe, NativeAOT)
Directory.Packages.props       Central package versions
NuGet.Config                   Package sources (nuget.org + Avalonia nightly)
App.axaml(.cs), Program.cs     Application bootstrap
app.manifest                   Win32 application manifest

Assets/                        Application icons
Composition/                   Dependency-injection registration
Controls/                      Custom Avalonia controls
Converters/                    Value converters for bindings
Helpers/                       Pure helpers (parsing, formatting, path/wildcard utilities)
Interop/                       P/Invoke (Windows PDH performance counters)
Json/                          Source-generated JSON serialization context
Models/                        Domain types and enums
Services/                      Application services and their interfaces
Styles/                        Theme and control styles
ViewModels/                    MVVM view-models
Views/                         Windows and views (XAML + code-behind)
Tests/                         xUnit test project
```

## Technical notes

- **MVVM** with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) source generators.
- **AOT/trim friendly**: no runtime reflection; JSON uses a source-generated context.
- Performance counters use the PDH API via `[LibraryImport]` rather than
  `System.Diagnostics.PerformanceCounter`, which is not NativeAOT-compatible.
- Skia and HarfBuzz are statically linked and rendering uses native WGL, producing a single `.exe`.
