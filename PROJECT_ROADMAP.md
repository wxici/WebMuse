# Project Roadmap

## WebMuse Sync Note

P1.8-0 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.8-0 is an early alpha validation probe. It does not execute Codex CLI, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

P1.7.3 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.7.3 does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

## Current Recommended Next Stage

After P1.8-0, continue strengthening failure recovery policy before entering real Codex execution, WebView2, or website generation.

Next immediate implementation round should be:

1. P1.7.4-A Failure recovery models + static policy table.

P1.7.4-A still must not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

P1.7-0 defined proof-check and approval-gate design only. P1.7.1 was first implemented in `wxici/WebMuse` as commit `2d2cb82a02992103d292f16bb80d25ae1a2a94b9` and has now been backfilled into the primary prototype repository `wxici/codex/WebRebuildRecorder`.

P1.7.2 implements approval gate models and persistence only. It adds project-relative approval request/result/report files, approval binding hashes, state transitions, stale-binding validation, and FoundationSelfTest coverage.

P1.7.2 does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

P1.7.3 now implements execution precondition aggregation only. It adds `ExecutionPrecondition.cs`, `ExecutionPreconditionService.cs`, `EvaluateAsync(...)`, `LoadLatestAsync(...)`, runtime execution-precondition reports, aggregation over readiness/dry-run/proof package/approval/rollback/sandbox/security/output/context/manual fallback checks, and FoundationSelfTest coverage.

P1.7.3 still does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`. In the current phase, missing real proof execution remains `NotImplemented` and blocks real execution; `AllowsRealCodexExecution` remains `false`.

Repository workflow rule: implementation and verification land first in `wxici/codex/WebRebuildRecorder`; `wxici/WebMuse` receives public-safe synchronized source and documentation after prototype verification.

P1.6 was later committed and pushed as cc28e56613d16032cea5664d23d8415a37610a86. Older review_package text saying commit/push was pending is historical transport context from the current GitHub state.

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
- Design Asset Pipeline implementation beyond documentation/schema reservation;
- Reference Portal UI or remote portal integration;
- Reference Site Library UI, database, crawler, submission system, or community layer;
- Frontend Effect Recipe Library implementation;
- ProposalPreview / SitePitcher implementation;
- WebView2 browsing/preview integration;
- real Codex CLI execution;
- OpenAI API calls;
- website generation;
- Local Responses Adapter implementation or adapter-backed provider execution.

Later real Codex CLI execution can be considered only after rollback, strict readiness, dry-run orchestration, proof checks, sandbox policy, run records, and failure recovery are stable.

Still do not start WebView2 preview, tuning UI, color-system UI, real Codex CLI execution, or Local Responses Adapter implementation.

## Future AI Engine Extension: Local Responses Adapter

A future architecture direction is recorded in:

```text
PROJECT_BLUEPRINT_LOCAL_RESPONSES_ADAPTER.md
```

The Local Responses Adapter is a future AI engine extension layer. Its purpose is to let advanced users eventually route Codex-style Responses requests through a local machine adapter to other online model providers, while preserving official Codex / OpenAI as the recommended high-quality construction path.

This direction is valuable for cost control, provider diversity, and fallback workflows, but it is not a P1.7 implementation target.

Current status:

- record architecture only;
- do not implement adapter source code in P1.7;
- do not connect adapter-backed providers in P1.7;
- do not use adapter-backed construction in P1.7;
- keep the adapter as `[ALT-EXPERIMENT]` until a separate proof suite repeatedly passes.

Future adapter-backed providers must pass proof checks before they can be used for low-risk construction:

- connectivity check;
- text response check;
- sandbox proof file creation;
- small isolated source-file modification;
- build command verification;
- minimal repair after a small build failure;
- path boundary validation;
- safe report generation;
- confirmation that no writes occur outside allowed project roots.

Provider capability labels should eventually include:

```text
Not usable
Documentation only
Draft coding only
Low-risk construction only
Formal construction candidate
```

The adapter must not delay the main product path: P1.7 safety closure -> P2 professional UI shell -> WebView2 preview -> official Codex minimum construction -> basic color palette selection -> basic tuning -> export and validation.

## Future Design Asset Pipeline

A future architecture direction is recorded in:

```text
PROJECT_BLUEPRINT_DESIGN_ASSET_PIPELINE.md
```

The Design Asset Pipeline governs future external DESIGN.md files, Design Skills, Taste Skills, Agent Skills, reference design libraries, and competitor pattern notes.

Current status:

- record architecture only;
- do not build the full asset library in P1.7;
- do not build a design asset UI in P1.7;
- do not bulk-import external design files in P1.7;
- do not offer direct customer-facing brand-template selection in P1.7;
- keep all external design assets as candidates until classified, normalized, and reviewed.

Future design assets should move through:

```text
collect -> classify -> normalize -> risk review -> derive -> project-context injection
```

The long-term goal is to derive WebMuse-owned neutral style profiles and rulesets, then inject only safe merged project context such as `merged.DESIGN.md`, `style-profile.json`, `quality-rules.md`, `forbidden-patterns.md`, `asset-slot-guide.md`, and `license-risk-report.md` into construction packages.

The pipeline must not delay the main product path: P1.7 safety closure -> P2 professional UI shell -> WebView2 preview -> official Codex minimum construction -> basic color palette selection -> basic tuning -> export and validation.

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
- optional awareness of future design-context and design-asset-pipeline directories only

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
- project-level design-context directory stubs when explicitly authorized
- small WebMuse-owned profile stubs when explicitly authorized

## P3: Codex Execution, Validation, Tuning, Fallback

Goal: stabilize black-box generation and light result calibration.

Scope:

- Codex CLI black-box execution
- output validation
- lightweight aesthetic tuning panel
- save `tune-overrides.css`
- content-map validation
- observation failure fallback

Future optional extension after the official construction route is stable:

- Local Responses Adapter experimental awareness;
- adapter-backed provider proof checks;
- provider capability labels;
- adapter-backed documentation or draft-coding mode only after proof success;
- external DESIGN.md or Skill import after design-asset-pipeline review.

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
- design acceptance checks using WebMuse-owned rulesets when the design asset pipeline is mature

## P5: Productization And Extension

Goal: complete the professional product surface.

Scope:

- normal/advanced/developer mode polish
- plugin manifest foundation
- more industry packs
- local section regeneration
- multi-version comparison UI
- full production hardening
- future AI Engine settings for official Codex, OpenAI API mode, Local Responses Adapter, Ollama, LM Studio, and manual construction package export
- future Design Asset Registry UI and profile/version management after the core delivery path is stable

## Historical Recommended Stage

The original blueprint handoff recommended the first formal P0 construction round:

```text
第 1 轮正式施工指令：P0 工程底盘迁移与总控文件建立
```

P0, P1.1, P1.2, P1.3, P1.4, and P1.5 foundation rounds have now landed as scaffold/data-flow/data-safety/readiness source and self-test coverage.
