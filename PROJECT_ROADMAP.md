# Project Roadmap

## Current Recommended Next Stage

After P1.6, continue strengthening proof-check and approval foundations before entering real Codex execution, WebView2, or website generation.

P1.7-0 has now defined proof-check and approval-gate design only. It does not implement proof-check service, approval gate service, real Codex CLI execution, OpenAI API calls, or website generation.

Next immediate implementation round should be:

1. P1.7.1 Proof-check package models and manifest.

P1.7.1 still does not execute Codex CLI.

P1.6 was later committed and pushed as cc28e56613d16032cea5664d23d8415a37610a86. Older review_package text saying commit/push was pending is historical transport context from report-writing time, not the current GitHub state.

P1.6.1 closed transport/status wording and audited generated dry-run artifacts only. It does not change the next technical stage.

P1.7 design scope:

- proof-check files;
- approval gates;
- rollback confirmation;
- execution preconditions;
- future execution state machine;
- failure recovery and retry rules;
- manual construction package export fallback.

P1.6 has now implemented dry-run orchestration only, not real Codex CLI execution.

P1.6 completed:

- verify readiness;
- generate task plan;
- simulate execution plan;
- check allowed write roots;
- create dry-run record;
- write dry-run plan/result/report/logs;
- do not start Codex CLI;
- do not call OpenAI API;
- do not generate website.

P1.7 should still avoid real execution unless explicitly authorized. It should design proof checks, user approval gates, sandbox preflight, rollback confirmation, result verification, and failure recovery before any real Codex CLI execution is allowed.

Future directions that must not enter P1.7 unless explicitly authorized:

- Design Context Library implementation;
- Reference Portal UI or remote portal integration;
- Reference Site Library UI, database, crawler, submission system, or community layer;
- Frontend Effect Recipe Library implementation;
- ProposalPreview / SitePitcher implementation;
- WebView2 browsing/preview integration;
- real Codex CLI execution;
- OpenAI API calls;
- website generation.

Later real Codex CLI execution can be considered only after rollback, strict readiness, dry-run orchestration, proof checks, sandbox policy, run records, and failure recovery are stable.

Still do not start WebView2 preview, tuning UI, color-system UI, or real Codex CLI execution.

## P0: Engineering Foundation

Goal: establish the safe project base before product expansion.

Scope:

- product positioning
- project directory model
- `project.wrbproj`
- `schemaVersion`
- `ProjectState` state machine
- `project.lock`
- sandbox boundaries
- project status files
- user environment detection
- Codex dependency notice
- record UI icon resource direction if needed, without prioritizing icon implementation over the engineering foundation

Out of scope:

- WebView2 preview implementation unless explicitly authorized
- Codex CLI full execution
- color-system UI
- tuning panel
- icon asset production beyond directory/specification notes
- drag-and-drop editor

## P1: Input, Assets, Theme, Mapping, Versions, Logs

Goal: make project input and durable project data reliable.

Scope:

- one-pass input form
- asset copy pipeline
- `assets-manifest.json`
- basic `theme.json` color system
- `content-map` and `data-tune-id` rules
- `current/versions` snapshot foundation
- layered logs

## P2: Main UI Skeleton, Preview, Color Candidates, Task Package

Goal: shape the product shell around the central preview stage and construction package generation.

Scope:

- main UI skeleton
- WebView2 preview
- candidate color palettes
- miniature color cards
- construction package generator
- Codex CLI task orchestration foundation
- failure classification foundation

## P3: Codex Execution, Validation, Tuning, Fallback

Goal: stabilize black-box generation and light result calibration.

Scope:

- Codex CLI black-box execution
- output validation
- lightweight aesthetic tuning panel
- save `tune-overrides.css`
- content-map validation
- observation failure fallback

## P4: Export, Validation Reports, Correction Tools, Templates, Archives

Goal: prepare reliable delivery and review workflows.

Scope:

- export integrity check
- sensitive information check
- automatic validation report
- local recording/frame extraction correction
- industry template packs
- sample project tests
- `.wrb` / `.wrbpkg` archive packages
- version comparison foundation

## P5: Productization And Extension

Goal: complete the professional product surface.

Scope:

- normal/advanced/developer mode polish
- plugin manifest foundation
- more industry packs
- local section regeneration
- multi-version comparison UI
- full production hardening

## Historical Recommended Stage

The original blueprint handoff recommended the first formal P0 construction round:

```text
第 1 轮正式施工指令：P0 工程底盘迁移与总控文件建立
```

P0, P1.1, P1.2, P1.3, P1.4, and P1.5 foundation rounds have now landed as scaffold/data-flow/data-safety/readiness source and self-test coverage.
