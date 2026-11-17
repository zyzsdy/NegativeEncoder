# Desktop UI contract

## Sources and scope

The current request scopes this work to UI on .NET 10 / Avalonia, targeting Windows,
macOS and Linux. The original MainWindow.xaml and FunctionTabs/FunctionTabs.xaml
are evidence for workflow and field coverage. The prior macOS ARM64 discussion
establishes future Desktop → Core → backend separation; it does not require backend
implementation in this phase. No billing, remote permissions or personal-data service
exists in this preview. No legacy configuration is read or written.

## Canonical UI Map

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
|---|---|---|---|---|
| Select/Listbox | Avalonia Fluent ComboBox / ListBox | DESIGN.md | authored dropdown; virtualized local list | headless binding tests and macOS popup check |
| Form | FunctionTabs.axaml + ResponsiveFormPanel + Preset | legacy parameter fields | editable session drafts | headless tab/binding tests |
| Scrollbar | Avalonia Fluent + Theme.axaml | DESIGN.md | form / list / code editor | minimum-size and long-form checks |
| Toast | MainWindowViewModel.Status / StatusText | this contract | persistent status, no transient toast | queue validation tests |
| CRUD | MainWindowViewModel + PreviewDialog | this contract | in-memory preset operations | snapshot/name tests |
| Menu | MainWindow menu handlers + preset menu synchronization | this contract | macOS NativeMenu; Windows/Linux Fluent Menu | native macOS menu check + desktop layout smoke test |
| Dialog | PreviewDialog | this contract | notice / about with app icon / name edit / delete confirmation | Escape + initial focus on macOS |

## Workflows

- Import: native file picker or drop files into the left pane. Cancel changes nothing.
  Importing adds paths only; it does not inspect media or invoke a tool. Duplicate
  paths are ignored. Removing list entries never deletes files from disk.
- Selection: selected input is reflected in the input field; the output defaults to
  `<name>_encoded.<format>`. Batch mode uses all files and derives individual names.
- Forms: controls bind to the current preset; dependencies follow original encoder
  restrictions. UI options are not evidence of detected hardware capabilities.
- Presets: create, rename, copy, save and delete operate only in memory. Switching
  presets retains edits. Switching and management share the Presets menu; checked
  entries track creation, rename, deletion and selection in both menu surfaces.
  Save clears the dirty marker for this session. Deletion
  requires confirmation, initially focused on Cancel. Duplicate/blank names show
  an error in the name dialog. Import/export and persistent storage are deferred.
- Queue: Add creates a snapshot with zero progress and an explicit unexecuted status.
  Missing inputs/outputs show a stable status message. Changes to the current preset
  do not mutate earlier task snapshots. Remove offers Undo in the queue pane.
- Script: AvaloniaEdit with Python highlighting, line numbers and indentation; no execution.
  The VS generator menu and resize checkbox/width/height share one compact toolbar row,
  following the Windows layout. Resize enables both dimension fields immediately.
  Every VS option, dimension, subtitle and selected input change regenerates the document;
  the Generate menu action also explicitly rebuilds it. Regeneration replaces manual edits.
  Switching tabs preserves the document. The editor owns its scrolling and fills the
  remaining tab space. Generated scripts use plugins installed in the execution environment,
  without assuming the legacy Windows Libs directory exists.
- Copy: no persistent preview banner, duplicate preset header or tutorial empty states.
  README documents the session-only boundary. Task statuses continue to identify
  unexecuted drafts; validation and operation results remain visible in the status bar.
- External functions (updates, toolkit command line, encoding and library discovery)
  are visibly unavailable. Windows registry integration is intentionally absent.

## Platform and accessibility

macOS uses the system menu bar, with Command+O for import and Command+S for
session preset save; Quit remains in the standard application menu. Windows and
Linux retain the in-window menu, including Linux desktops that support global menus.
Both menu presentations invoke the same handlers and preserve disabled unavailable actions.
NativeMenu is assigned directly in XAML and removed on non-macOS platforms.
Native menu inspection requires the main window to be active in the foreground.

Chinese application copy with OS-owned file picker chrome. Desktop has no URL state.
All state is session-local. Native controls own keyboard/focus behavior; no global
Enter shortcut may submit while Chinese IME is composing. Name dialogs submit only
through their save button. Modal windows have an owner, Escape dismisses, and focus
returns to the owner. UI animations are not introduced. Screen reader and Windows /
Linux interactive coverage must be reported separately from headless checks.
