# Desktop UI contract

## Sources and scope

The desktop workbench uses .NET 10 / Avalonia on Windows, macOS and Linux,
with execution through the managed tool library. The original MainWindow.xaml and FunctionTabs/FunctionTabs.xaml
are evidence for workflow and field coverage. The prior macOS ARM64 discussion
establishes Desktop → backend separation. No billing, remote permissions or personal-data service
exists in this preview. No legacy configuration is read or written.

## Canonical UI Map

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
|---|---|---|---|---|
| Select/Listbox | Avalonia Fluent ComboBox / ListBox | DESIGN.md | authored dropdown; virtualized local list | headless binding tests and macOS popup check |
| Form | FunctionTabs.axaml + ResponsiveFormPanel + Preset | legacy parameter fields | editable presets | headless tab/binding tests |
| Scrollbar | Avalonia Fluent + Theme.axaml | DESIGN.md | form / list / code editor | minimum-size and long-form checks |
| Toast | MainWindowViewModel.Status / StatusText | this contract | persistent status, no transient toast | queue validation tests |
| CRUD | MainWindowViewModel + PreviewDialog | this contract | persistent preset operations | snapshot/name tests |
| Menu | MainWindow menu handlers + preset menu synchronization | this contract | macOS NativeMenu; Windows/Linux Fluent Menu | native macOS menu check + desktop layout smoke test |
| Dialog | PreviewDialog | this contract | notice / live task log / about with app icon / name edit / delete confirmation | Escape + initial focus on macOS |

## Workflows

- Import: native file picker or drop files into the left pane. Cancel changes nothing.
  Importing adds paths only; it does not inspect media or invoke a tool. Duplicate
  paths are ignored. Removing list entries never deletes files from disk.
- Selection: selected input is reflected in the input field; the output defaults to
  `<name>_encoded.<format>`. Batch mode uses all files and derives individual names.
- Forms: controls bind to the current preset; dependencies follow original encoder
  restrictions, with VTEnc options mapped from its own implementation. Startup selects
  macOS VTEnc or a detected Windows/Linux GPU; failed detection is reported. Options
  describe encoder features; successful execution still requires driver/session support.
- Presets: each workbench preset owns its encoding, batch, path, generator and script
  settings. Edits update a separate recovery file immediately. Restart restores the
  selected preset and each working copy. Both menus and the title prefix edited
  names with an asterisk. Saving updates the selected baseline and clears its marker. Deletion
  requires confirmation, initially focused on Cancel. Duplicate/blank names show
  an error in the name dialog. Import/export is deferred.
- Queue: Add validates inputs/tools/destinations, captures a preset/script snapshot and
  starts jobs in queue order up to the saved concurrency limit (default 3). Changes to forms do not mutate existing tasks. Both sides of
  pipelines must succeed before the staged output is published; existing files are never
  overwritten. Errors stop scheduling and preserve diagnostics; the File menu resumes
  pending tasks. Cancellation stops the complete process tree. Running entries cannot be
  removed; pending/completed entries retain Undo. Details show selectable live logs.
  Batch VS tasks rebind only the generator's explicit VIDEO_PATH declaration; scripts
  without that declaration require individual tasks.
- Script: AvaloniaEdit with Python highlighting, line numbers and indentation; execution through the managed vspipe runtime.
  The VS generator menu and resize checkbox/width/height share one compact toolbar row,
  following the Windows layout. Resize enables both dimension fields immediately.
  Every valid VS option, dimension, subtitle and selected input change regenerates the document;
  the Generate menu action also explicitly rebuilds it. Regeneration replaces manual edits.
  Switching tabs preserves the document. The editor owns its scrolling and fills the
  remaining tab space. Generated scripts reference the selected private Libs environment.
  Missing capabilities or invalid dimensions preserve the document and show an error.
  Capability refresh preserves hand edits. Subtitle backends are explicit; no silent fallback.
- Copy: no persistent preview banner, duplicate preset header or tutorial empty states.
  README documents preset recovery. Validation and actual execution states
  remain visible in the status bar and task list; action buttons contain no “草稿” suffix.
  The bottom status bar remains one line, including messages containing line breaks;
  overflowing messages use an ellipsis with full text in the tooltip and accessible name.
  Successful hardware detection reports only the default encoder. Detection failures and
  unsupported hardware still report their existing errors or guidance.
- Help offers a manual application update check. The toolkit command line remains unavailable.
  Tool-library discovery/install/update and hardware detection are implemented. Windows registry integration is intentionally absent.

## Platform and accessibility

macOS uses the system menu bar, with Command+O for import and Command+S for
preset save; Quit remains in the standard application menu. Windows and
Linux retain the in-window menu, including Linux desktops that support global menus.
Both menu presentations invoke the same handlers and preserve disabled unavailable actions.
NativeMenu is assigned directly in XAML and removed on non-macOS platforms.
Native menu inspection requires the main window to be active in the foreground.

Chinese application copy with OS-owned file picker chrome. Desktop has no URL state.
Input lists and task queues are session-local. Presets, drafts, the tool-library path and active manifest persist. Native controls own keyboard/focus behavior; no global
Enter shortcut may submit while Chinese IME is composing. Name dialogs submit only
through their save button. Modal windows have an owner, Escape dismisses, and focus
returns to the owner. UI animations are not introduced. Screen reader and Windows /
Linux interactive coverage must be reported separately from headless checks.

## Tool-library operations

Help → 工具库管理 opens one owned window. Path application validates before saving;
only switches, never moves/deletes files. Failed application keeps the active root and
shows diagnostics. Python/VS are separately visible but installed/upgraded together.
The manager links to global Settings. Saving validates language, concurrency and proxy together.
HTTP/SOCKS5 addresses persist in the existing application settings file. Blank restores system
network defaults. Validation keeps the draft and focuses the field with an inline error.
Subsequent release checks and tool downloads share the saved proxy, including redirects.
Settings and Help check the latest GitHub release on demand. Help links to the release tag
if its version is newer than the installed version.
All installs use a new generation and commit only after verification; cancellation,
checksum errors and failed probes preserve the active manifest. User-added plugins are
never automatically upgraded/deleted; file conflicts abort with details. Active operations
lock mutation controls; lists remain scrollable. Closing while busy requests cancellation
and keeps the window open until the user closes it again. Missing hardware is advisory;
missing platform/ABI packages disable installation. Errors stay in rows and copyable logs.
NVEnc/QSVEnc/VCEEnc/VTEnc use the newest non-draft GitHub release, including prereleases;
prerelease targets are identified in the existing status column. FFmpeg remains stable-only.
