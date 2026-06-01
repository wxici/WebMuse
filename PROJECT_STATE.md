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

The public repository was just cloned from `wxici/WebMuse` and initially contained only a short `README.md`.

This round will migrate selected source and project memory from the historical `WebRebuildRecorder` project while preserving internal source names.

## Completed Modules

- GitHub repository exists as `wxici/WebMuse`.
- Default branch is `main`.
- Repository visibility is public.

## Unfinished Modules

- Source migration is not complete.
- OSS documentation is not complete.
- CI workflow is not complete.
- Build and FoundationSelfTest verification have not yet run in this repository.
- No commit or push has been made for this round.

## Current Key Issues

- The target repository starts nearly empty.
- The historical source includes generated and private-risk categories that must not be migrated.
- Internal source names still use `WebRebuildRecorder` and should remain unchanged in this round.

## Important Design Principles

- Public name: WebMuse.
- Historical internal source names may remain `WebRebuildRecorder`.
- Real AI execution comes after safety gates, not before them.
- WebMuse is not a clone, copy, imitation, or drag-and-drop website builder.
- Do not migrate generated media, local runtime artifacts, secrets, or customer/private materials.

## Recent Modification Summary

2026-06-01: Started OSS Readiness Round 0. Confirmed `wxici/WebMuse` is public, default branch is `main`, cloned the repository, created target-local Codex continuity files, migrated filtered source files, wrote public OSS documentation, added CI and issue templates, and verified restore/build/FoundationSelfTest.

## Next Priorities

1. Audit staged files for generated artifacts and sensitive content.
2. Commit with `Initialize WebMuse OSS repository foundation`.
3. Push to `origin/main`.
4. Create 6-10 real GitHub Issues.
5. Prepare `v0.1.0-alpha` tag/release after external review.

## Known Risks

- WebMuse is early alpha.
- Real Codex execution is not enabled.
- OpenAI API calls are not enabled.
- WebView2 preview is not implemented.
- Website generation is not implemented.
- Tuning panel is not implemented.
- Public docs must avoid overstating maturity.
