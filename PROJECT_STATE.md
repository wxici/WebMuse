# Project State

## Project Goal

Prepare WebMuse as a clean, credible, auditable public OSS repository for an early-alpha Windows desktop workbench focused on safer Codex-assisted reference-style website reconstruction workflows.

## Technical Stack

- .NET 8
- WPF desktop application
- Windows desktop support
- JSON and Markdown project data
- GitHub Actions on `windows-latest`

## Current Architecture

WebMuse is a public OSS repository at `wxici/WebMuse`.

The repository contains selected migrated source from the historical `WebRebuildRecorder` project. Internal solution, project, namespace, and folder names still use `WebRebuildRecorder`; this is intentional for now to avoid unnecessary build risk.

## Completed Modules

- Public OSS foundation completed.
- Source migration completed.
- OSS documentation created.
- GitHub Actions workflows created and passing.
- Issue templates created.
- 8 real maintainer backlog GitHub Issues created.
- README Mermaid workflow diagram added.
- Final Codex for OSS application text created at `docs/CODEX_FOR_OSS_APPLICATION_FINAL.md`.
- Build passes.
- FoundationSelfTest passes.
- Migration audit created.
- Sensitive/generated artifact filtering completed for the initial migration.

## Unfinished Modules

- `v0.1.0-alpha` release/tag has not been prepared yet.
- Manual Codex for OSS application submission is not completed yet.
- External public-readiness review is still pending.
- README architecture details may still need review after the first external pass.

## Current Key Issues

- The project is early alpha and should not be described as mature.
- Real Codex execution is not enabled.
- OpenAI API calls are not enabled.
- Website generation is not complete.
- WebView2 preview is not complete.
- Tuning panel is not complete.
- Internal source names still use `WebRebuildRecorder`.

## Important Design Principles

- Public name: WebMuse.
- Historical internal source names may remain `WebRebuildRecorder`.
- Real AI execution comes after safety gates, not before them.
- WebMuse is not a clone, copy, imitation, or drag-and-drop website builder.
- Do not commit generated media, local runtime artifacts, secrets, customer materials, recordings, screenshots, extracted frames, zips, release binaries, or local configuration.

## Recent Modification Summary

2026-06-01: Completed OSS Readiness Round 0.2. Created 8 real maintainer backlog GitHub Issues, added a README Mermaid workflow diagram, updated status and migration audit documents, and verified Release build and FoundationSelfTest. No product features, UI changes, real Codex execution, OpenAI API calls, WebView2, release binaries, screenshots, recordings, frames, zips, customer materials, or generated site output were added.

2026-06-01: Completed OSS Readiness Round 0.3. Created `docs/CODEX_FOR_OSS_APPLICATION_FINAL.md` with final Codex for OSS application text and updated public status files lightly. No product code, UI changes, binaries, screenshots, recordings, zips, customer materials, generated sites, secrets, or private account information were added.

Round 0.2 issues:

- https://github.com/wxici/WebMuse/issues/1
- https://github.com/wxici/WebMuse/issues/2
- https://github.com/wxici/WebMuse/issues/3
- https://github.com/wxici/WebMuse/issues/4
- https://github.com/wxici/WebMuse/issues/5
- https://github.com/wxici/WebMuse/issues/6
- https://github.com/wxici/WebMuse/issues/7
- https://github.com/wxici/WebMuse/issues/8

## Next Priorities

1. Manually submit the Codex for OSS application form.
2. External public-readiness review.
3. Prepare `v0.1.0-alpha` after review.

## Known Risks

- WebMuse is early alpha.
- Real Codex execution is not enabled.
- OpenAI API calls are not enabled.
- Website generation is not complete.
- WebView2 preview is not complete.
- Tuning panel is not complete.
- Public docs must avoid overstating maturity.
