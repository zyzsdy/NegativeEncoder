# Avalonia UI migration — phase 1

## Scope and architecture

The `next` branch adds a standalone .NET 10 desktop front end. The original WPF
application, its tests, encoding implementations and installer remain intact.
The legacy Windows project was already on .NET 10; the new project targets plain
`net10.0`, without a Windows TFM or WPF framework reference.

| Path | Responsibility |
|---|---|
| `NegativeEncoder.Desktop.slnx` | Cross-platform solution, desktop UI and its tests only |
| `NegativeEncoder.Desktop/Views` | Avalonia XAML, native storage pickers, owned dialogs |
| `NegativeEncoder.Desktop/ViewModels` | Session state and observable presentation model |
| `NegativeEncoder.Desktop/Models` | UI-only preset, option lists and task snapshots |
| `NegativeEncoder.Desktop/Styles/Theme.axaml` | Shared Fluent theme adapters and design tokens |
| `NegativeEncoder.Desktop.Tests` | Model tests and Avalonia headless control/binding tests |
| `NegativeEncoder.sln` | Original Windows application and tests |

The UI models preserve the original names/defaults but do not reference the old
PresetProvider, SystemOption, AppContext, tool discovery or encoding services.
The property inventory covers all 44 original preset properties: 43 in parameter
tabs and PresetName in the preset menu/dialog. Backend-specific controls retain
their original enable conditions. Their presence does not assert hardware support.

Avalonia 11.3.23 and AvaloniaEdit 11.4.1 are pinned explicitly. `global.json` selects
stable .NET 10 feature bands. No additional .NET workloads are required.

## UI coverage

| Original surface | Avalonia implementation |
|---|---|
| Main window / status bar | Three resizable panes, version/preset window title, platform/status text |
| Input list | Native multi-file picker, drag/drop, selection, removal and batch mode |
| Encoder | All original modes, quality, depth, GOP, aspect ratio, decoding, deinterlace, resize and audio controls |
| HDR | HDR output/conversion, tone mapping fields and metadata task placeholders |
| VS | AvaloniaEdit with Python highlighting, line numbers, editing and generator-option controls |
| Audio / mux / common | Path pickers, parameter forms and task draft actions |
| Preset management | Session-only create/save/copy/rename/delete with dirty marker and name validation |
| Task detail / About | Owned modal windows, Escape dismissal, initial focus and return to owner |

Encoding execution, registry integration and legacy settings access remain deferred.
Tool-library management now downloads, installs and probes private binaries and Python.
Queue progress remains zero and explicitly says the draft has not executed. Presets,
video paths and scripts remain session-local; the library path and manifest persist. Native output pickers choose a path but never open a
write stream. The test fixture named `ui-preview.mp4` contains placeholder text,
not playable media; UI import intentionally does not inspect file contents.

## Validation performed on 2026-11-16

Host: macOS ARM64. Installed stable SDK 10.0.404 in `~/.dotnet`; runtime 10.0.15.
The SDK archive was checked against the official download Content-Length and
Content-MD5. The provided local proxy completed the remaining download.

- Release build: passed, zero warnings/errors.
- `dotnet test NegativeEncoder.Desktop.slnx -c Release`: 6 passed.
- Headless test opens all six tabs at 1000 × 620, checks editor document binding,
  Python highlighting, two-way parameter bindings and QSV-only control enablement.
- Model tests cover draft snapshots, duplicate inputs, missing inputs, preset names,
  unsaved session edits, batch output collisions and selection after removal.
- `dotnet format NegativeEncoder.Desktop.slnx --verify-no-changes --no-restore`: passed.
- Framework-dependent publish for `win-x64` and `linux-x64`: passed on this Mac.
  These are packaging checks, not execution on Windows/Linux.
- Self-contained `osx-arm64` .app publish: passed. Info.plist passed `plutil -lint`;
  the apphost is a Mach-O arm64 executable.
- Actual macOS app launched and inspected: Chinese main window, encoder popup,
  VS syntax highlighting, native file import, draft creation, task detail, Escape
  dismissal/focus restoration, and saving a Chinese preset name.
- Final app bundle relaunched after all changes and left open with a fresh session.
- Design lint: zero errors/warnings. Premium static audit: zero findings.
  This web-oriented static audit does not prove native accessibility.
- `git diff --check` and shell script syntax checks passed.

Local evidence lives under ignored `artifacts/`: `test-results/desktop-ui.trx`,
`premium-audit.json`, the macOS app, and Windows/Linux publish directories.
The added CI matrix runs build and headless tests on all three OSes when pushed;
that remote workflow has not been run in this session.

## Workbench refinement — 2026-11-17

- Desktop version is `6.0.0-alpha.1`; window title and About use assembly metadata.
- Preset switching joins create/save/copy/rename/delete in the native/Fluent menu.
  Selection, renamed entries and dirty markers remain synchronized.
- Removed the branding/preset header, preview banner and nonfunctional explanation
  copy. This intentionally replaces the prior persistent-notice design contract;
  backend/persistence scope remains documented here and in README.
- Encoder fields reflow in place at 660 units of form width; related option switches
  share their value row. Output format and the queue action share one footer row.
- The new fish icon is packaged as PNG/ICO/ICNS; macOS Info.plist declares its icon
  and derives release/build metadata from the desktop project version.
- Focused checks: preset model behavior, menu lifecycle/selection, six-tab binding
  smoke test and repeated 1000/1320/1600-wide layout transitions (4 tests).
- macOS interactive checks: menu create/switch with a Chinese name, encoder popup,
  wide layout and Finder app icon. Narrow layout was verified headlessly; native
  resize automation did not resize the window. Windows/Linux interaction remains
  unverified. No legacy/full-suite tests were run for this refinement.

## Next phase

Extract a platform-neutral Core and connect the desktop presentation model through
an explicit service boundary. Model backend capabilities separately before adding
VideoToolbox; do not translate NVENC/QSV rate-control parameters by name alone.
Then migrate persistence, task lifecycle/progress/logs and cancellation, followed by
tool distribution and VapourSynth integration. The old Windows implementation is
available as the behavior reference throughout that work.

Still unverified: interactive Windows/Linux startup, Intel Mac startup, VoiceOver/
screen reader coverage, IME composition beyond direct Chinese text entry, drag/drop
on all OSes, and high-contrast behavior. This is a local UI preview, not a signed or
notarized release. New model/backend integration tests should be added when real
media processing is introduced.

References: [Avalonia desktop platform support](https://v11.docs.avaloniaui.net/docs/get-started/supported-platforms)
and [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit).

## Tool-library management — 2026-11-18

- `ToolLibrary/` owns platform/source resolution, pinned runtime catalog, safe extraction,
  SHA256 downloads, subprocess diagnostics, actual VS frame probes and atomic activation.
- Help opens an owned manager with tool/plugin tabs, editable persistent location,
  hardware hints, install/update, cancellation and copyable logs. No system-tool fallback.
- Python and VS are separate rows backed by one environment generation. Managed files
  live below `.managed`; explicitly added files are preserved, collisions abort activation.
- Generator selects an explicit subtitle backend; Subtext and Bwdif join the available
  options. Missing capabilities and invalid dimensions block generation without replacing
  the existing document. Capability refresh does not overwrite hand edits.
- macOS ARM64: real independent Python 3.13.18 / VS R79 install and repeat upgrade passed;
  L-SMASH-Works, VIVTC, Subtext, Bwdif, QTGMC and native vspipe frame checks passed.
  Upgrade retained old generations and manually added Python scripts.
- FFmpeg 9.0.2 stable ZIPs: real download, SHA256, ffprobe/version and lavfi frame check passed.
- Native macOS UI: Help menu, hardware discovery, version rows, plugin states, switching to
  the installed test library, invalid-path error, restoring default path and Escape verified.
- Windows/Linux runtime execution and screen-reader speech have not been verified on this Mac.
  Every target installation must pass its own subprocess/frame checks before activation.
- Explicit opt-in verification harness: `scripts/ToolLibrary.Verify`; metadata refresh script:
  `scripts/refresh-vs-catalog.py`. No automatic network installs in the unit tests.

- Focused ToolLibraryTests/VapourSynthTests: 23 passed (no full-suite run). Covers SHA256,
  failed process/cancel activation, archive traversal, path persistence, user-file collision,
  unsupported ABI, explicit subtitle selection, invalid dimensions and minimum window layout.
- Generator output for source, IVTC, Bwdif, QTGMC and Subtext passed native vspipe execution
  against a synthetic clip. Windows/Linux x64 framework-dependent publish passed; no native
  Windows/Linux execution is claimed. Strict premium audit: zero findings.


## 2026-11-18：主功能接入工具库

`NegativeEncoder.Desktop/Encoding` contains the shell-free command plans, original
Windows argument port, VTEnc-specific capability/argument mapping and asynchronous
process/pipeline execution. No legacy WPF assembly or qaac dependency is introduced.
Main actions now execute through the current tool library with usage leases, real logs,
cancellation and serial scheduling. See README for platform and VTEnc audio constraints.

Targeted regression checks:

```sh
dotnet test NegativeEncoder.Desktop.Tests/NegativeEncoder.Desktop.Tests.csproj --filter 'FullyQualifiedName~EncodingTests|FullyQualifiedName~DesktopTests|FullyQualifiedName~WorkbenchTests'
dotnet run --project scripts/Encoding.Verify -- ABSOLUTE_LIBS ABSOLUTE_NEW_OUTPUT_DIRECTORY
```

The verifier generates a one-second video/audio fixture with non-ASCII paths and checks
real encode, subtitle, pipe, audio, mux and HEVC-tag results by decoding them. VS is checked
when its managed runtime is installed. `--all-encoders` covers detected Windows GPUs;
`--install-encoder` explicitly installs missing detected encoders in the supplied Libs.
GPU-less containers exercise audio/extraction/mux and explicitly report video as untested.

Native Linux UI verification (no GPU detection or encoding): publish
`scripts/Desktop.Verify` for the container architecture, then run `Desktop.Verify`
under Xvfb. This uses the real X11 backend and checks all six tabs at 1000/1320/1600
widths, form/preset/script bindings, dropdown opening and missing-input validation.
The test program never invokes the hardware detector.

Verification in this change: macOS Apple Silicon completed the real-media flows and a
native import → encode → completed UI interaction. Linux used a disposable Ubuntu
26.04 ARM64 Docker container with Xvfb; no GPU detection/encoding was invoked.
Windows uses the user-provided `tedwin` machine and an isolated test checkout at
`H:\Workspace\NegativeEncoder`, with .NET 10.0.404 and the requested loopback SOCKS5
proxy for tool downloads. The main checkout's SDK policy remains unchanged.

Windows real-media results: NVEnc 9.41 on RTX 4090 and QSVEnc 8.38 on UHD 770 both
passed direct video, no-audio/AAC, ASS subtitles, all three FFmpeg-pipe variants and
SDR/HDR10/HLG HEVC metadata tagging. Shared FFmpeg AAC/extraction and MP4/MKV/TS
muxing passed. Windows VS execution was not tested because the isolated Libs has no
managed VS runtime; macOS exercised VS. No AMD GPU was available for live VCE tests.
Integration also caught and fixed 256 MiB solid-7z extraction and case-insensitive
manual-tool alias detection on Windows.
