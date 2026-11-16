---
version: alpha
colors:
  workspace: "#F3F5F7"
  surface: "#FFFFFF"
  ink: "#202B36"
  muted: "#536273"
  line: "#D9E0E7"
  primary: "#176B91"
  notice: "#EAF3F8"
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

The next branch introduces a separate Avalonia UI preview. It must never imply an
encoder ran, hardware was detected, or an output file was created. The old WPF app
remains the reference for terminology and field coverage.

## Colors

`NegativeEncoder.Desktop/Styles/Theme.axaml` is the runtime token owner. The colors
above map respectively to WorkspaceBrush, SurfaceBrush, InkBrush, MutedBrush,
LineBrush, AccentBrush and NoticeBrush. Update both files together. Accent identifies
the primary queue action; the notice surface explains the current preview boundary.
Use the Fluent light theme for native control hover, pressed, disabled, focus and
scrollbar states. No separate screen-specific palettes. Dark theme is out of scope.

## Typography

UiFont and CodeFont in Theme.axaml implement the font stacks above. Body copy is
13 device-independent units, section headings 16, app title 22, auxiliary paths 11.
Use OS-installed CJK fallbacks; never bundle an unlicensed font. Code and scripts
use monospace; descriptive text remains Chinese. Keep product/codec names intact.

## Layout

Default desktop window: 1320 × 850; minimum: 1000 × 620. Side panes resize with
splitters; the center reserves space for paths and a footer action. Each tab owns
one vertically scrolling form. Lists virtualize through Avalonia ListBox. Long
paths wrap or offer full-value tooltips; text fields allow selection/copy.

On macOS, the system menu bar owns File, Presets and Help; the in-window menu
collapses without reserving space. Windows and Linux retain the Fluent window menu.

## Elevation & Depth

White panes on a cool gray workspace, one-pixel separators. No decorative shadows
or animation. Native file pickers and owned modal windows provide overlay behavior.

## Shapes

Panel radius 6; notice radius 4. Controls retain the Avalonia Fluent shape language.

## Components

Avalonia Fluent owns buttons, radio groups, authored ComboBox popups, checkboxes,
tabs, scrollbars and focus indicators. PreviewDialog owns application modals.
Text fields have labels and accessible names. StatusText is the shared feedback
surface. Disabled operations include a visible explanation, not just a tooltip.

## Do's and Don'ts

- Preserve all six tabs and the original parameter terminology.
- Keep parameter edits and selection when switching tabs and presets.
- Distinguish task drafts from running tasks. No synthetic progress or success.
- Keep platform details in About/status and migration docs, not every form field.
- Avoid a dashboard rebrand, fake video thumbnails, or decorative KPI cards.
