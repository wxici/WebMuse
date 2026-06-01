# Architecture

WebMuse is an early-alpha Windows desktop workbench for safer Codex-assisted reference-style website reconstruction workflows.

Some internal source names still use the historical `WebRebuildRecorder` name.

## Design principle

Real AI execution comes after safety gates, not before them.

## Main concepts

### Project folder

A WebMuse project is expected to be stored as a project folder, not just a single file.

### project.wrbproj

The project manifest records schema version, project metadata, state, and durable references.

### ProjectState

Project state tracks the workflow stage and is intended to prevent unsafe transitions.

### project.lock

The lock file prevents conflicting project access or unsafe concurrent writes.

### SandboxPathPolicy

The sandbox policy validates allowed and forbidden write locations.

### Observation package

Observation packages organize reference-site evidence and user intent.

### Construction package

Construction packages translate observations into safer implementation instructions.

### Codex task package

Codex task packages are intended to provide bounded instructions for future controlled AI execution.

### Dry-run

Dry-runs validate planned execution steps without starting real Codex execution.

### Proof-check

Proof-check artifacts are planned validation outputs for confirming whether generated work met required conditions.

### Approval gate

Approval gates are planned checkpoints before risky actions.

### Rollback readiness

Rollback readiness ensures the project can recover from failed or unwanted AI-generated changes.

### Manual construction package fallback

Manual fallback allows the project to export instructions without requiring automatic Codex execution.

## Current implementation status

### Implemented

- .NET 8 WPF desktop application project under `WebRebuildRecorder.App/`.
- Foundation self-test harness under `WebRebuildRecorder.FoundationSelfTest/`.
- `project.wrbproj` manifest model and service.
- Project Directory V2 folder creation service.
- `ProjectState` enum.
- `project.lock` model and file service.
- `SandboxPathPolicy` for project-root, manifest-path, Codex write-root, sensitive segment, forbidden root, and Windows reparse point checks.
- Shared JSON serialization options.
- Basic environment-check service stub.
- Assets, theme, content-map, observation-package, construction-package, Codex task package, snapshot, restore, export integrity, secret/local-path scan, readiness-gate, and dry-run foundation services.
- Foundation self-test coverage for the current project-system foundations.
- Legacy WPF workflows for opening reference URLs, recording, frame extraction, observation Markdown, and package generation remain present.

### Partially implemented

- New project creation writes both legacy project files and `project.wrbproj`, but the older WPF workflow still carries much of the historical behavior.
- Observation and construction package services exist, but the product experience is not yet a polished public workflow.
- Dry-run orchestration exists as a safety foundation, but it intentionally does not execute Codex CLI or generate a website.
- Logging foundations exist, but the full product UI for layered logs is not complete.
- Rollback and snapshot foundations exist, but full user-facing version management is not complete.

### Design only

- Proof-check UI and full proof review experience.
- Approval-gate UI and persistent human approval flow.
- WebView2 preview shell.
- Tuning panel.
- Reference Portal, Design Context Library, and ProposalPreview/SitePitcher concepts.

### Future

- Controlled real Codex CLI execution.
- Output validation after real execution.
- Lightweight tuning override workflow.
- Export-ready generated static site validation.
- Release packaging and sample projects.

## Out of scope for current alpha

- real Codex CLI execution;
- OpenAI API calls;
- website generation;
- WebView2 preview;
- tuning panel;
- CMS;
- ecommerce;
- drag-and-drop editing.

