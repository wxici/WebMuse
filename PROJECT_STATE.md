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

## Current Strategic State

P2A-1.3 is now inserted before P2A-2-B.

The first-stage observation package is being upgraded from a recording/frame/observation-first package into a machine-readable construction plan:

```text
Source Snapshot
  -> Section Map
  -> Asset Slot Map
  -> Design Tokens
  -> Motion Slot Map
  -> Generation Brief
  -> Codex Construction Package
```

Reference-observed assets are analysis-only by default and must not enter final export without explicit authorization and provenance.

## Current Implementation State

Documentation/memory update only. P2A-1.3 code implementation has not started.

Next allowed implementation:

```text
P2A-1.3 Asset Slot Map + Motion Slot Map + Generation Brief
```

Blocked:

```text
P2A-2-B Controlled Codex CLI Single Page Generation
```

P2A-2-B remains blocked until P2A-1.3 verification passes.

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
- P1.7.1 proof-check package models, manifest/request persistence, validation reports, path-safety checks, and FoundationSelfTest coverage completed.

## Unfinished Modules

- `v0.1.0-alpha` release/tag has not been prepared yet.
- Manual Codex for OSS application submission is not completed yet.
- External public-readiness review is still pending.
- README architecture details may still need review after the first external pass.
- P1.7.2 approval gate models and persistence are not implemented yet.
- Real proof-check execution is not implemented yet.

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
- Future observation should prioritize controlled Source Snapshot analysis before recording/frame extraction.
- Captured reference code and assets are evidence for analysis, not default export assets.
- Future generated previews should use asset slot overlays to tell users what images/media/logos are needed and let users fill slots with their own assets.
- Every Codex construction instruction must include a WebMuse OSS GitHub update block.

## Recent Modification Summary

2026-06-12: Recorded the Asset Slot Map + Motion Slot Map strategy and inserted P2A-1.3 before P2A-2-B. The new route makes controlled Source Snapshot evidence feed section maps, asset slots, design token maps, motion slots, motion variants, generation briefs, and legal-risk reporting before any real Codex generation. This is a documentation-only update; no C# source, Codex CLI execution, API call, local model call, or website generation was added.

2026-06-03: Completed P1.7.1 proof-check package models and manifest. Added `ProofCheckPackage.cs`, `ProofCheckPackageService.cs`, and FoundationSelfTest coverage for proof package creation, loading, validation, path safety, and non-execution boundaries. This round generates only package/instruction/validation files under `codex-task/proof/` at runtime; it does not execute Codex CLI, call OpenAI API, call local model engines, generate websites, write `output-site/current/index.html`, or create future runtime result files.

2026-06-03: Updated the product direction at blueprint level from recording/frame-extraction-first to controlled Source Snapshot first with rendered DOM/resource/style/code-readability analysis, asset slot map generation, clickable asset slot overlays, user-owned asset import, real-time preview replacement, and copyright-safe export policy. This is documentation-only and does not implement source crawling, WebView2, Codex execution, OpenAI API calls, website generation, recording changes, frame extraction changes, or asset slot UI.

2026-06-02: Completed Manual PoC 001 OSS presentation update. Added curated compressed public WebP evidence images, `docs/case-studies/manual-poc-001/README.md`, an asset manifest, README Manual PoC evidence section, PoC history link, roadmap future WPF floating tuning window note, public demo screenshot security rules, OSS presentation release notes, and an alpha observation demo plan. No product code, UI implementation, real Codex execution, OpenAI API calls, WebView2 preview, recording behavior changes, frame extraction behavior changes, release binaries, raw ZIPs, raw recordings, extracted frame sets, customer materials, credentials, local path configuration, or generated output-site artifacts were added. `dotnet restore`, `dotnet build`, and FoundationSelfTest passed.

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

1. Implement P2A-1.3 Asset Slot Map + Motion Slot Map + Generation Brief.
2. Verify structured sample outputs, build, FoundationSelfTest, and review package.
3. Keep P2A-2-B blocked until P2A-1.3 passes.

## Known Risks

- WebMuse is early alpha.
- Real Codex execution is not enabled.
- OpenAI API calls are not enabled.
- Website generation is not complete.
- WebView2 preview is not complete.
- Tuning panel is not complete.
- Public docs must avoid overstating maturity.
