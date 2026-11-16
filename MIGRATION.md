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
tabs and PresetName in the preset header/dialog. Backend-specific controls retain
their original enable conditions. Their presence does not assert hardware support.

Avalonia 11.3.23 and AvaloniaEdit 11.4.1 are pinned explicitly. `global.json` selects
stable .NET 10 feature bands. No additional .NET workloads are required.

## UI coverage

| Original surface | Avalonia implementation |
|---|---|
| Main window / status bar | Three resizable panes, persistent preview notice, platform/status text |
| Input list | Native multi-file picker, drag/drop, selection, removal and batch mode |
| Encoder | All original modes, quality, depth, GOP, aspect ratio, decoding, deinterlace, resize and audio controls |
| HDR | HDR output/conversion, tone mapping fields and metadata task placeholders |
| VS | AvaloniaEdit with Python highlighting, line numbers, editing and generator-option controls |
| Audio / mux / common | Path pickers, parameter forms and task draft actions |
| Preset management | Session-only create/save/copy/rename/delete with dirty marker and name validation |
| Task detail / About | Owned modal windows, Escape dismissal, initial focus and return to owner |

No encoding, tool installation, subprocess execution, hardware probing, Python
execution, registry integration or legacy settings access occurs. Queue progress
remains zero and explicitly says the draft has not executed. Presets, paths and
scripts are session-local. Native output pickers choose a path but never open a
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
