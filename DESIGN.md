---
version: alpha
colors:
  workspace: "#F3F5F7"
  surface: "#FFFFFF"
  ink: "#202B36"
  muted: "#536273"
  line: "#D9E0E7"
  primary: "#176B91"
typography:
  body:
    fontFamily: "PingFang SC, Microsoft YaHei UI, Noto Sans CJK SC, sans-serif"
    fontSize: "13px"
  code:
    fontFamily: "Menlo, Cascadia Mono, DejaVu Sans Mono, monospace"
rounded:
  panel: "6px"
spacing:
  panel: "14px"
  section: "12px"
---

## Overview

NegativeEncoder is a Chinese desktop video-encoding workbench. Preserve the original
WPF workflow: input files on the left, six parameter tabs in the center, queue on the
right. This is a product surface for configuring many related encoding parameters,
not a marketing page. The familiar three-pane workbench is the signature.

The application uses Avalonia for Windows, macOS and Linux. Task states and logs reflect real execution; only successful complete pipelines publish output files. Hardware reports come only from the tool-library detector. The original WPF app is available in Git history
as the reference for terminology and field coverage.

## Colors

`NegativeEncoder.Desktop/Styles/Theme.axaml` is the runtime token owner. The colors
above map respectively to WorkspaceBrush, SurfaceBrush, InkBrush, MutedBrush,
LineBrush and AccentBrush. Update both files together. Accent identifies
the primary queue action.
Use the Fluent light theme for native control hover, pressed, disabled, focus and
scrollbar states. No separate screen-specific palettes. Dark theme is out of scope.

## Typography

UiFont and CodeFont in Theme.axaml implement the font stacks above. Body copy is
13 device-independent units, pane headings 16, auxiliary paths 11.
Use OS-installed CJK fallbacks; never bundle an unlicensed font. Code and scripts
use monospace; descriptive text remains Chinese. Keep product/codec names intact.

## Layout

Default desktop window: 1320 × 850; minimum: 1000 × 620. Side panes resize with
splitters; the center reserves space for paths and a footer action. Each tab owns
one vertically scrolling form. Encoder fields use ResponsiveFormPanel: two equal
columns at 660 units of available form width, otherwise one, with 20-unit column
gaps and 6-unit row gaps. Read/focus order is row-first; reflow keeps the same controls.
The output format and queue action share one fixed footer row. Lists virtualize through Avalonia ListBox. Long
paths wrap or offer full-value tooltips; text fields allow selection/copy.

On macOS, the system menu bar owns File, Presets and Help; the in-window menu
collapses without reserving space. Preset actions and checked preset choices live
only in the Presets menu. The title bar shows version, current preset and dirty
marker; there is no duplicate branding/preset header or preview notice. Windows and Linux retain the Fluent window menu.

## Elevation & Depth

White panes on a cool gray workspace, one-pixel separators. No decorative shadows
or animation. Native file pickers and owned modal windows provide overlay behavior.

## Shapes

Panel radius 6. Controls retain the Avalonia Fluent shape language.

## Components

Avalonia Fluent owns buttons, radio groups, authored ComboBox popups, checkboxes,
tabs, scrollbars and focus indicators. OwnedDialog owns centering and Escape for application modals; PreviewDialog owns notices/confirmation and ToolLibraryWindow owns component management.
Text fields have labels and accessible names. StatusText is the shared feedback
surface. The bottom status bar always stays on one line: truncate long messages with
an ellipsis and expose the complete message in a tooltip and accessible name. Hardware
startup success reports only the default encoder, not the detected adapter list.
Keep copy focused on controls, validation and operation results; omit
preview disclaimers, tutorial empty states and repeated implementation notes.
Unavailable operations stay disabled. Queue entries show waiting, running step, cancelled, failed or completed states; details provide selectable live diagnostics.

The desktop app icon preserves the original pink fish and yellow-green markings,
flattened onto a warm off-white macOS tile. Assets/app-icon.png is the 1024px source;
app-icon.ico serves window/Windows resources and app-icon.icns serves the macOS bundle.
The About dialog displays the PNG at 160 × 160 units to the left of the application
information, following the original Windows layout with a 20-unit gap.

## Do's and Don'ts

- Preserve all six tabs and the original parameter terminology.
- Keep parameter edits and selection when switching tabs and presets.
- Distinguish waiting tasks from running tasks. No synthetic progress or success.
- Keep platform details in About/status and migration docs, not every form field.
- Avoid a dashboard rebrand, fake video thumbnails, or decorative KPI cards.

## Tool-library manager

Use the existing light tokens and list/form controls. Owned modal: 980 × 780, minimum
820 × 640. Path, switch-only warning and detected hardware stay above the tabs; tool
and plugin lists scroll independently. Progress, result, expandable copyable diagnostics,
cancel and close stay below. During operations disable mutations, keep list scrolling
available. Unsupported distributions explain the reason beside a disabled action.
Source paths truncate with full tooltips. Keep plugin descriptions Chinese and native
component names unchanged. No new global shortcuts or custom animation.

SettingsWindow owns language preference, queue concurrency and the shared network proxy.
File and the tool-library manager open the same settings dialog. Settings and Help share
one update check, with the newer release linked from Help. Application copy remains Chinese.

## Encoder capabilities and queue

Preset and EncoderCapabilities own the per-encoder option contract. Avalonia radio
buttons and ComboBox remain the canonical owners; switching encoders selects a valid
mode/codec and hides unsupported modes. VTEnc has native ABR/Quality/CBR and macOS
26+ VBR, H.264/HEVC and matching Metal filters. Runtime session errors remain visible.
Hardware detection chooses only the initial unedited preset and defaults for new presets.

MainWindowViewModel schedules jobs in queue order up to the global concurrency limit (default 3) and owns StatusText feedback. Adding tasks starts
execution. A failure lets active jobs finish and leaves pending tasks waiting for the File menu's resume action.
Cancellation stops both sides of a pipeline. A running task cannot be removed until it
stops; removal of pending/completed entries has the existing undo action. PreviewDialog
uses a read-only, selectable, live TextBox for task diagnostics. Logs on disk retain full
output; the UI retains the latest 64,000 characters. Output paths are unique among active
jobs and never overwrite existing files. Execution uses a private staging directory,
keeps failed work for diagnosis, and publishes only after all exit codes succeed.
