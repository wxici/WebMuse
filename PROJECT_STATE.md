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

WebMuse is now a public OSS repository at `wxici/WebMuse`.

The repository contains selected migrated source from the historical `WebRebuildRecorder` project. Internal solution, project, namespace, and folder names still use `WebRebuildRecorder`; this is intentional for now to avoid unnecessary build risk.

## Completed Modules

- Public OSS foundation completed.
- Source migration completed.
- OSS documentation created.
- GitHub Actions workflows created and passing.
- Issue templates created.
- Build passes.
- FoundationSelfTest passes.
- Migration audit created.
- Sensitive/generated artifact filtering completed for the initial migration.

## Unfinished Modules

- 6-10 public GitHub issues have not been created yet.
- README architecture diagram has not been added yet.
- `v0.1.0-alpha` release/tag has not been prepared yet.
- Codex for OSS application answer is not finalized yet.
- External public-readiness review is still pending.

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
- Do not commit generated media, local runtime artifacts, secrets, customer materials, recordings, screenshots, extracted frames, zips, or local configuration.

## Recent Modification Summary

2026-06-01: Completed OSS Readiness Round 0. Migrated filtered source files, created public OSS documentation, added CI and issue templates, fixed standalone repository workflow path checks, fixed Release CI self-test configuration, verified local Release build and FoundationSelfTest, and confirmed latest remote GitHub Actions runs are green.

Completed commits:

```text
0a59fa5 Initialize WebMuse OSS repository foundation
fb5ea37 Fix WebMuse CI self-test configuration
```

## Next Priorities

1. Create 6-10 GitHub issues.
2. Add a README architecture diagram.
3. Prepare `v0.1.0-alpha` after review.
4. Finalize the Codex for OSS application answer.
5. Request an external public-readiness review.

## Known Risks

- WebMuse is early alpha.
- Real Codex execution is not enabled.
- OpenAI API calls are not enabled.
- Website generation is not complete.
- WebView2 preview is not complete.
- Tuning panel is not complete.
- Public docs must avoid overstating maturity.

