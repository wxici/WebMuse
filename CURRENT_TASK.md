# CURRENT_TASK

## Current task

P2A-0.1 Detached WebView2 Preview Window / Adjustable Preview Surface

## Status

Implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

## Completed scope

- Added a separate resizable and maximizable WebView2 preview window with its own WebView2 control.
- Kept the existing P2A-0 embedded preview unchanged.
- Added desktop, tablet, and phone window-size presets: `1366x768`, `1440x900`, `1920x1080`, `1024x768`, and `390x844`.
- Reuses the current embedded-preview URI when available, otherwise uses the current project reference URL.
- Added address navigation, refresh, external-browser fallback, and live window-size status.
- Closes the detached preview when the project or main window closes.
- Preserved the old recording, frame extraction, ChatGPT package, and final Codex package workflow.

## Verification

- `dotnet build WebRebuildRecorder.slnx`: passed with 0 warnings and 0 errors.
- FoundationSelfTest: passed after a narrow P2A-0.1 allowlist update to the historical P1.8 scope guard.
- UI smoke: passed for app startup, embedded-preview preservation, detached-window launch, five size presets, maximize/free resize, WebView2 Runtime initialization, reference URL navigation, refresh, external-browser fallback, project-close cleanup, main-window cleanup, and old workflow controls.

## Hard boundary

P2A-0.1 does not implement Source Snapshot, Codex CLI execution, OpenAI API calls, local model calls, site generation, tuning, color controls, asset-slot overlays, a Docking framework, or full UI redesign.

P2A-0.1 adds a detached adjustable WebView2 preview window with desktop/tablet/mobile size presets. It does not implement Source Snapshot, Codex CLI execution, site generation, tuning, color controls, asset-slot overlays, or full Docking UI.

## Next planned stage

P2A-1 Internal Source Snapshot MVP.
