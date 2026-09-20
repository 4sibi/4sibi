# Building 4sibi

Requirements: Windows x64 and the .NET 8 SDK.

From the repository root, run `./build.ps1` in PowerShell. The self-contained executable is written to `artifacts/win-x64/4sibi.exe`.

To run the isolated checks, launch the executable with `--self-test <output-directory>` and wait for it to exit. The output directory contains `result.txt` and rendered UI snapshots. Run on a Windows desktop session. Tests do not send OS input or change your normal settings. Live game behavior is not covered.

The Windows build workflow builds, tests, and uploads an unsigned artifact for each main-branch change, version tag, or manual run. GitHub Actions artifacts are developer build outputs, not automatically published Releases.

## Source layout

- `src/4sibi/Core.cs`: settings, input adapter, scheduler, and foreground restrictions.
- `Data.cs`: profiles, transfer validation, and test measurements.
- `Ui.cs`, `Compact.cs`, `Features.cs`: window, navigation, and settings workflows.
- `PillOverlay.cs`, `Branding.cs`, `Assets/`: overlay, icons, and branding.
- `Program.cs`: entry point and isolated self-tests.

## Version history

The first public release is named 1.0. Earlier local builds used internal versions up to 1.3.2. This source normalizes metadata to 1.0.0; it does not retroactively change the existing uploaded executable. Publish future changes as a new version instead of moving an existing release tag. A build from this source is not claimed to be byte-for-byte identical to the earlier release.
