# PROJECT_MEMORY_FULL.md

## WebMuse Sync Note

P1.8-0 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.8-0 is an early alpha validation probe. It does not execute Codex CLI, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

P1.7.3 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.7.3 does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

## 2026-06-05 P1.8-0 early alpha validation probe

P1.8-0 implements an early alpha validation probe only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

The purpose is to prove the P0/P1 foundation can be composed into a local explainable pipeline report before continuing deeper P1.7.4 failure recovery work.

P1.7.4 Failure recovery policy service is postponed, not cancelled.

Synced source:

- `WebRebuildRecorder.App/Core/ProjectSystem/AlphaValidationProbe.cs`
- `WebRebuildRecorder.App/Core/ProjectSystem/AlphaValidationProbeService.cs`
- expanded `WebRebuildRecorder.FoundationSelfTest/Program.cs`

Runtime report layout:

```text
codex-task/alpha-validation/<probe-id>/
  alpha-validation-report.json
  alpha-validation-report.md
```

These files are runtime artifacts and must not be committed.

The probe composes Project Directory V2, project manifest, assets manifest, theme, content map, observation package, construction package, task package, instructions, P1.5 readiness, P1.6 dry-run, P1.7.1 proof package, P1.7.2 approval artifacts, P1.7.3 execution precondition, manual fallback evidence, runtime artifact ignore coverage, and non-execution boundary checks.

Blocked-but-explainable findings can still be Alpha evidence when the critical non-execution boundary is preserved and real execution remains blocked.

FoundationSelfTest verifies model/source presence, enum string serialization, JSON and Markdown report generation, required step keys, blocked-but-explainable evidence, missing package handling, `.gitignore` coverage, no `output-site/current/index.html`, and no UI/WebView2/Source Snapshot/ProposalPreview change.

Next recommended round: P1.7.4-A Failure recovery models + static policy table.

## 2026-06-04 P1.7.3 execution precondition service

P1.7.3 implements execution precondition aggregation only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

This round lands the execution-precondition aggregation layer in the prototype repository first:

- `WebRebuildRecorder.App/Core/ProjectSystem/ExecutionPrecondition.cs`
- `WebRebuildRecorder.App/Core/ProjectSystem/ExecutionPreconditionService.cs`
- expanded `WebRebuildRecorder.FoundationSelfTest/Program.cs`

Execution precondition model coverage:

- schema constants in `ExecutionPreconditionSchema`;
- status enum: `Passed`, `Warning`, `Blocked`, `NotApplicable`, `NotImplemented`;
- severity enum: `Info`, `Warning`, `Error`;
- decision enum: `Blocked`, `ReadyForFutureProofCheckOnly`, `ReadyForFutureRealExecution`;
- report/item/options models;
- non-execution flags defaulting to false.

Execution report persistence:

```text
codex-task/execution/<execution-id>/
  execution-preconditions.json
  execution-preconditions.md
```

Execution reports are runtime artifacts, are ignored by root and project `.gitignore`, and must not be committed from real project folders.

`ExecutionPreconditionService.EvaluateAsync(...)` aggregates:

- P1.5 PreCodexDryRun readiness;
- P1.6 dry-run completion and non-execution flags;
- P1.7.1 proof-check package validation;
- missing real proof execution as `NotImplemented` and blocking;
- P1.7.2 approval gate validation;
- P1.4 rollback/safety snapshot availability;
- allowed and forbidden sandbox roots;
- secret and local-path scan results;
- `output-site/current` safety without creating `index.html`;
- `codex-workspace` safety;
- logs writability;
- task package hash stability against approval binding;
- construction context freshness;
- manual fallback input availability;
- the P1.7.3 non-execution boundary.

`ExecutionPreconditionService.LoadLatestAsync(...)` loads the newest persisted report and validates schema version.

Current P1.7.3 normal result remains blocked. Real proof execution and real Codex execution are not implemented, so `AllowsRealCodexExecution` remains `false`. The service also forces `ExecutesCodexCli`, `CallsOpenAiApi`, `CallsLocalModel`, and `GeneratesWebsite` to `false`.

FoundationSelfTest verifies:

- enum string serialization;
- JSON and Markdown report generation;
- project-relative report paths;
- required aggregation item keys;
- missing readiness, dry-run, proof package, and proof execution blocking;
- missing, pending, rejected, and stale approvals blocking;
- secret/local path blocking and report sanitization;
- execution runtime `.gitignore` coverage;
- no `output-site/current/index.html` creation;
- no Codex CLI, OpenAI API, local model, website generation, UI, or WebView2 change.

Verification on 2026-06-04:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both commands passed with 0 build warnings and 0 build errors. Next recommended round is P1.7.4 Failure recovery policy service, still without real execution or website generation.

## 2026-06-03 P1.7.2 approval gate models and persistence

P1.7.2 implements approval gate models and persistence only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

This round lands the approval-gate portion of the P1.7 design in the prototype repository first:

- `WebRebuildRecorder.App/Core/ProjectSystem/ApprovalGate.cs`
- `WebRebuildRecorder.App/Core/ProjectSystem/ApprovalGateService.cs`
- expanded `WebRebuildRecorder.FoundationSelfTest/Program.cs`

Approval gate model coverage:

- schema constants in `ApprovalGateSchema`;
- gate types for proof-check, real Codex execution, output-site writes, overwrites, export zip, and uploads;
- decision states `Pending`, `Approved`, `Rejected`, `Expired`, `Cancelled`, and `Superseded`;
- request/result/binding/validation models;
- `CannotBeBypassedByAi = true` defaults.

Approval persistence:

```text
codex-task/approvals/<approval-id>/
  approval-request.json
  approval-result.json
  approval-validation-report.json
  approval-validation-report.md
```

Approval bindings include required hashes for:

- `codex-task/task-package.json`;
- `codex-task/instructions.md`.

When present, bindings also record optional hashes for:

- latest dry-run plan;
- `codex-task/proof/proof-manifest.json`;
- future `codex-task/execution-plan.json`.

Approval validation checks schema, project id, approval id, gate type, purpose/summary/risk fields, non-bypass flag, project-relative stored paths, required binding file existence, current hashes, sensitive/local path leakage, and decision state. `ApproveAsync()` revalidates current hashes and refuses stale approvals.

Allowed transitions:

- Pending -> Approved
- Pending -> Rejected
- Pending -> Cancelled
- Pending -> Expired
- Pending -> Superseded
- Approved -> Superseded
- Approved -> Expired

Forbidden transitions such as Rejected -> Approved, Cancelled -> Approved, Expired -> Approved, Superseded -> Approved, and Approved -> Rejected throw `InvalidOperationException`.

Runtime approval artifacts are ignored by root and project `.gitignore` rules and must not be committed from real project folders.

Verification on 2026-06-03:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed. Build reported 0 warnings and 0 errors. FoundationSelfTest printed the required P1.7.2 approval gate verification lines.

Repository workflow remains prototype-first: implement and verify in `wxici/codex/WebRebuildRecorder`, then synchronize only public-safe source and documentation to `wxici/WebMuse`.

Next recommended implementation round: P1.7.3 Execution precondition service, still without real Codex CLI execution, OpenAI API calls, local model calls, website generation, or `output-site/current/index.html`.

## 2026-06-03 P1.7.1-sync proof-check backfill

P1.7.1 was accidentally implemented first in the public OSS presentation repository `wxici/WebMuse`.
The source commit used for this backfill is `2d2cb82a02992103d292f16bb80d25ae1a2a94b9` with message `P1.7.1 add proof-check package models and manifest`.

This round restores the intended repository order by backfilling P1.7.1 into the primary prototype repository `wxici/codex/WebRebuildRecorder`.
Going forward, the primary construction workflow is:

1. implement and verify in `wxici/codex/WebRebuildRecorder`;
2. commit and push prototype changes;
3. synchronize only public-safe source and documentation to `wxici/WebMuse`.

Backfilled prototype source:

- `WebRebuildRecorder.App/Core/ProjectSystem/ProofCheckPackage.cs`
- `WebRebuildRecorder.App/Core/ProjectSystem/ProofCheckPackageService.cs`
- P1.7.1 proof-check coverage in `WebRebuildRecorder.FoundationSelfTest/Program.cs`

The P1.7.1 proof-check package creates and validates proof package manifests and request/result/report models, including non-execution flags:

- `ExecutesCodexCli = false`
- `CallsOpenAiApi = false`
- `GeneratesWebsite = false`
- `MustNotExecuteInThisRound = true`

FoundationSelfTest now verifies proof-check models, persistence, validation, path safety, and the non-execution boundary.

Allowed P1.7.1 source artifacts are the model/service files and self-test code. Real runtime artifacts remain forbidden:

- no real `codex-task/proof/proof-created-file.txt`;
- no real `codex-task/proof/proof-result.json`;
- no real `codex-task/proof/proof-report.md`;
- no generated `output-site/current/index.html`;
- no zips, logs, customer materials, tokens, keys, cookies, or build outputs.

Verification on 2026-06-03:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both commands passed. Build reported 0 warnings and 0 errors. FoundationSelfTest printed the required P1.7.1 proof-check verification lines.

Next recommended implementation round: P1.7.2 Approval gate models and persistence. P1.7.2 still must not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, or generate websites.

## 2026-05-31 P1.7-0 proof-check and approval-gate design

P1.7-0 defines proof-check and approval-gate design only.
It does not implement proof-check service, approval gate service, real Codex CLI execution, OpenAI API calls, or website generation.
Future real execution requires readiness, dry-run, proof-check, approval, rollback confirmation, sandbox validation, and failure recovery.

New design document:

- `docs/project-memory/P1_7_CODEX_EXECUTION_PROOF_CHECK_AND_APPROVAL_GATE_DESIGN.md`

Design contents:

- Purpose: P1.7 is not real website generation; it designs the proof-check and approval-gate system required before real Codex CLI execution.
- Execution philosophy: Codex execution is never default-enabled and requires readiness, dry-run, safety snapshot, execution plan, approval, proof-check, rollback confirmation, sandbox validation, and fallback availability.
- Proof-check definition: future proof files are planned under `codex-task/proof/`, including `proof-request.json`, `proof-instructions.md`, `proof-result.json`, `proof-report.md`, and `proof-created-file.txt`.
- Approval gate definition: future gates include approval before proof-check, real Codex execution, output-site writes, overwrites, export zip, and uploading anything.
- Rollback confirmation: real execution must be blocked unless a safety snapshot, manifest read, hash validation, restore plan, safe restore path check, and rollback report path are available.
- Execution preconditions: readiness, dry-run, proof-check, approval, safety snapshot, root validation, secret/local path scan, output/workspace safety, logs, stable hashes, context freshness, and manual fallback must all pass.
- Execution state machine: future states range from `Created` through readiness, proof-check, execution, rollback, and manual export fallback.
- Failure recovery: missing input, sandbox violation, secret detection, environment/Codex/network/quota/auth failures, proof-check failure, output missing, build failure, cancellation, timeout, and internal errors are mapped to blocking/retry/fallback/log/report rules.
- Manual construction package export fallback: still available without Codex CLI or OpenAI API and does not write `output-site/current/`.
- Implementation split: P1.7.1 through P1.7.6 break the future implementation into proof models, approval persistence, preconditions, recovery policy, fallback scaffold, and proof-check dry-run only.

Next recommended implementation round: P1.7.1 Proof-check package models and manifest. P1.7.1 still does not execute Codex CLI.

## 2026-05-31 P1.6.1 transport closure and dry-run artifact audit

P1.6 was later committed and pushed as cc28e56613d16032cea5664d23d8415a37610a86.
Older review_package text saying commit/push was pending is historical transport context from report-writing time, not the current GitHub state.

This closure round does not add new runtime behavior. It records the current GitHub transport state for P1.6, audits generated dry-run artifact paths, and updates ignore rules so runtime outputs are not committed by default.

Dry-run artifact paths confirmed from `CodexDryRun.cs` and `CodexDryRunOrchestratorService.cs`:

- `codex-task/dry-runs/<dry-run-id>/dry-run-plan.json`
- `codex-task/dry-runs/<dry-run-id>/dry-run-result.json`
- `codex-task/dry-runs/<dry-run-id>/dry-run-report.md`
- `codex-task/dry-runs/<dry-run-id>/dry-run-record.json`

Generated artifacts covered by ignore rules include `codex-task/dry-runs/`, `codex-task/runs/`, `logs/*.log`, `output-site/current/`, `versions/restore-reports/`, `versions/snapshots/readiness-probe-*`, `exports/*.zip`, source-review zips, and build outputs.

No source service code, FoundationSelfTest code, UI, WebView2, Codex CLI execution, OpenAI API calls, local model calls, generated website output, Reference Portal, Design Context, ProposalPreview, recording, or frame extraction work was performed in P1.6.1.

## 2026-05-31 P1.6 Codex CLI dry-run orchestrator

P1.6 implemented the Real Codex CLI Integration Dry-Run Orchestrator. Despite the name, this round remains dry-run only. It validates whether a future Codex construction run can be prepared and reported safely, but it does not start Codex CLI, does not run any `codex` command, does not call OpenAI API, and does not generate a website.

Implemented:

- `WebRebuildRecorder.App/Core/ProjectSystem/CodexDryRun.cs`
- `WebRebuildRecorder.App/Core/ProjectSystem/CodexDryRunOrchestratorService.cs`
- Dry-run plan/result/report/record artifacts under `codex-task/dry-runs/<dry-run-id>/`
- Integration with `ConstructionReadinessGateService` using `ConstructionReadinessMode.PreCodexDryRun`
- Task package and instructions loading/validation
- Required input file collection with relative paths and hashes
- Strict allowed write roots validation for `codex-workspace/` and `output-site/current/`
- Forbidden roots and sandbox checks
- Rollback availability check via readiness results
- Simulated dry-run steps for future execution planning
- Project/security/codex-task logs through `ProjectLogService`
- P1.6 FoundationSelfTest coverage

Dry-run artifacts must explicitly state:

- Codex CLI not executed
- OpenAI API not called
- Website not generated
- Dry-run only

The dry-run record is separate from a real Codex task run record and includes `IsDryRun = true`, plus non-execution flags set to false for actual execution/API/generation.

FoundationSelfTest now verifies:

- normal dry-run success;
- `PreCodexDryRun` readiness integration;
- report generation even when readiness blocks future execution;
- missing `task-package.json` blocking;
- missing instruction boundary blocking;
- unsafe allowed write roots blocking;
- report/result/record generation;
- `ExecutedCodexCli = false`, `CalledOpenAiApi = false`, `GeneratedWebsite = false`;
- no `output-site/current/index.html` is created;
- dry-run artifact sanitizer;
- project/security/codex-task log writing.

Verification on 2026-05-31:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed with 0 build warnings and 0 build errors.

P1.6 did not implement UI, WebView2, Reference Portal UI, Design Context Library, Frontend Effect Recipe Library, ProposalPreview, recording/frame extraction changes, mouse automation, page editors, real Codex CLI execution, OpenAI API calls, local model calls, website generation, or zip generation.

## 2026-05-31 P1.5.1 blueprint memory sync and P1.5 transport closure

P1.5 was later committed and pushed as `e1161a323460afd65f8c5c22b669c9961f23ebf8`. Older review_package text saying local commit was prepared but push failed is historical transport context from the earlier environment, not the current GitHub state.

This P1.5.1 round is a documentation and memory synchronization round only. It does not modify application source, UI, WebView2, Codex CLI execution, OpenAI API usage, recording/frame extraction, Reference Portal UI, Design Context Library implementation, Frontend Effect Recipe Library implementation, ProposalPreview implementation, website generation, or zip generation.

### Blueprint files read for P1.5.1

- `PROJECT_BLUEPRINT_AI_ENGINE_AUTH_AND_EXECUTION_STRATEGY.md`
- `PROJECT_BLUEPRINT_AI_ENGINE_RELEASE_STRATEGY.md`
- `PROJECT_BLUEPRINT_DESIGN_SKILLS.md`
- `CODEX_CONSTRUCTION_RULES_DESIGN_SKILLS_ADDENDUM.md`
- `PROJECT_BLUEPRINT_DESIGN_CONTEXT_LIBRARY.md`
- `PROJECT_BLUEPRINT_REFERENCE_PORTAL.md`
- `PROJECT_BLUEPRINT_REFERENCE_SITE_LIBRARY.md`
- `PROJECT_BLUEPRINT_FRONTEND_EFFECT_RECIPE_LIBRARY.md`
- `PROJECT_MEMORY_REFERENCE_SITE_LIBRARY.md`
- Missing and not fabricated: `PROJECT_BLUEPRINT_PROPOSAL_PREVIEW.md`

### AI engine auth and release memory

- WebRebuildRecorder must not manage or store user ChatGPT/OpenAI/Codex passwords.
- Users authenticate through official Codex CLI/App, OpenAI, GitHub, Ollama, LM Studio, or provider tools. WRB detects, guides, orchestrates, generates construction packages, launches allowed execution paths, collects logs, verifies results, and recovers from failure.
- Official Codex/OpenAI is the recommended formal construction path.
- OpenAI API key mode, Ollama, LM Studio, custom OpenAI-compatible API mode, Open Lovable-style website-to-code engines, and manual construction package export are configurable or future channels. The AI backend is replaceable; the workflow is the product asset.
- Local models are auxiliary until they pass proof-file creation, tool-call behavior, and build verification. They must not become the P0/P1 automatic construction main path without proof.
- Missing Codex, missing login, quota exhaustion, invalid API key, proof-file failure, tool-call failure, build failure, timeout, or model refusal must degrade to manual construction package export.
- The global Codex `config.toml` must stay clean for official Codex. Local model tests use temporary `CODEX_HOME`, isolated profiles, or isolated test directories.
- AI execution tools and website-to-code engines are optional runtime/toolchain dependencies, not mandatory base-installer contents.

### Design skills memory

- Taste Skill / design-skills are design-quality rule sources, not a product workflow replacement.
- They may improve future construction packages through layout, typography, spacing, motion discipline, anti-slop checks, CSS variables, and stable `data-tune-id` guidance.
- Design-skill rules must follow user brand material, legal/safety/copyright constraints, observation facts, and WRB project constraints.
- They do not authorize P1/P1.6 UI implementation, WebView2, tuning panel, real Codex CLI execution, frontend engine implementation, third-party auto-update, or dependency installation.

### Design Context memory

- Design Context is a future input layer, not the current P1 mainline.
- Future reserved project files may include `design-context/`, `source-manifest.json`, `reference.DESIGN.md`, `brand.DESIGN.md`, `merged.DESIGN.md`, `design-tokens.json`, `tailwind-theme.css`, `css-variables.css`, `extraction-report.md`, and `license-notes.md`.
- Merge priority keeps usage rights, user brand, business clarity, industry trust, and available project content ahead of external design context and generic AI defaults.
- P1.5/P1.6 only keep optional awareness; heavy Design Context Library implementation is deferred.

### Reference Portal and Reference Site Library memory

- Reference Portal is a future content/discovery layer that may be remote-first with local cached fallback and optional future WebView2 display.
- The remote portal may display reference cards, industry/style groupings, discussions, submissions, finished-site showcases, and onboarding content.
- The desktop app owns execution: observation, recording, frame extraction, analysis, asset import, construction-package assembly, AI/Codex execution, preview, tuning, export, project writes, sandbox validation, logging, and recovery.
- Remote pages must not directly trigger local file operations, recording, frame extraction, analysis, AI construction, tuning, export, or deployment.
- Reference Site Library stores our own metadata around target website URLs, source traceability, industry/style/effect tags, suitability, motion complexity, observation difficulty, and copyright/brand risk. It must not copy platform screenshots, descriptions, reviews, UI, target-site assets, text, logo, code, or distinctive identity.
- P1/P1.6 do not implement the portal UI, database, crawler, submissions, community features, or WebView2 browsing layer.

### Frontend Effect Recipe Library memory

- Frontend Effect Recipe Library is a future enhancement layer for reusable HTML/CSS/JS/motion recipes.
- Recipes may include preview, applicable scenes, dependencies, performance risk, parameters, CSS/JS/HTML integration guidance, mobile fallback, accessibility notes, and validation checklist.
- It is not a random snippet market, visual animation editor, drag/drop timeline, effect marketplace, or P1/P1.6 implementation target.

### ProposalPreview boundary memory

- ProposalPreview / SitePitcher is a separate customer proposal direction.
- It may diagnose old sites, compare old/new directions, prepare 1-3 visual proposal pages, email drafts, preview links, and early interest validation.
- It is not mixed into the WebRebuildRecorder P0/P1/P1.6 core.
- Relationship: Proposal tool acquires/qualifies customers -> after deal, customer materials, old-site analysis, style choice, and content retention list can feed WebRebuildRecorder deep construction delivery.
- `PROJECT_BLUEPRINT_PROPOSAL_PREVIEW.md` was not found during P1.5.1 and must not be treated as implemented memory until an actual blueprint file exists.

### P1.6 boundary after P1.5.1

The next immediate round remains P1.6 Real Codex CLI integration dry-run orchestrator, but dry-run only:

- verify readiness;
- generate a task plan;
- simulate execution;
- check allowed write roots;
- create a run record;
- do not start Codex CLI;
- do not call OpenAI API;
- do not generate a website.

## 2026-05-27 P1.3.1 closure and P1.4 snapshot restore update

P1.3 was committed and pushed as `4fc602161c9b9f3a5ca5bf2df2db18ef865012b0`.

The P1.3 report text that said commit/push was pending is historical context from report-writing time. Current repository history confirms P1.3 is on GitHub.

P1.4 implemented snapshot restore and rollback foundations:

- `SnapshotRestoreService` supports `ListSnapshotsAsync`, `LoadSnapshotAsync`, `ValidateSnapshotAsync`, `CreateRestorePlanAsync`, and `RestoreAsync`.
- Restore models cover plans, per-file status, results, validation results, and validation items.
- Restore reports are saved to `versions/restore-reports/<restore-id>/restore-plan.json` and `restore-result.json`.
- Snapshot validation checks manifest existence, schema version, relative paths, source files, SHA-256 hashes, project-root safety, forbidden directories, blocked extensions, and allowed restore targets.
- Restore creates a before-restore safety snapshot with reason `before-restore:<snapshot-id>` before copying files.
- Restore copies only the P1.4 allowed surface: project manifest, assets/theme/content-map manifests, construction/task package manifests, task instructions, context files, observation package/bridge report, and `output-site/current/**`.
- Restore skips zip/video/log/binary-like targets and blocks `.git`, `.ssh`, `.codex`, `.openai`, `bin`, `obj`, `.vs`, traversal, and project escape attempts.
- Restore logs to project and security channels through `ProjectLogService`.
- FoundationSelfTest now verifies listing, loading, validation, hash mismatch detection, safety snapshots, restore reports, restored files, forbidden paths, skipped zip/video targets, logs, and workflow content.

Verification on 2026-05-27:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed with 0 build warnings and 0 build errors.

This round intentionally did not implement UI, WebView2 preview, real Codex CLI execution, OpenAI API calls, color-system UI, tuning panel, recording/frame extraction changes, mouse automation, page editing, zip packaging, rollback UI, or generated site construction.

## 2026-05-27 P1.3 legacy bridge and construction context update

The P1.3 round moved the P1.2 package chain from empty scaffold toward usable construction context while staying outside UI and real execution.

P1.2 was committed and pushed as `59ba12e4d20a057d58b574f359c72cb7dcef555b`.

Implemented in P1.3:

- `LegacyObservationBridgeService` bridges legacy observation files into the structured observation package.
- `observation/observation.md` headings become `ObservationSectionItem` entries, and nearby markdown body text becomes low-confidence `ObservationFindingItem` entries.
- `observation/action-log.json` is parsed conservatively for scroll/click/hover/key/note actions and generates `ObservationInteractionItem` entries.
- `observation/frame-index.json` and `observation/screenshots/frame-index.json` are registered as artifacts; parseable frame counts are summarized as findings.
- Missing legacy inputs produce warnings rather than hard failures.
- `observation/legacy-bridge-report.json` records bridge items and warnings.
- `ConstructionPackageContentBuilderService` writes `codex-task/context/project-brief.md`, `observation-summary.md`, `asset-index.md`, `theme-summary.md`, `content-map-summary.md`, `constraints.md`, `acceptance-checklist.md`, and `package-index.json`.
- `package-index.json` records relative path, kind, SHA-256, size, and created time for generated context files.
- `codex-task/construction-package.json` is updated to include generated context files as inputs.
- `CodexTaskPackageService` now includes context files in `task-package.json` inputs and writes a reading order into `instructions.md`.
- `CodexTaskRunService.MarkQueuedAsync()` explicitly supports Created -> Queued -> Running -> terminal flows.
- `PackageValidationService` now supports Draft and Strict modes. Draft tolerates partial scaffold context as warnings; Strict treats readiness gaps as errors.
- `.github/workflows/webrebuildrecorder-foundation.yml` includes `workflow_dispatch` and `timeout-minutes: 15`.
- `WebRebuildRecorder.FoundationSelfTest` verifies P1.3 legacy bridge, context builder, queued transition, strict validation, workflow content, and prior foundations.

Verification on 2026-05-27:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed with 0 build warnings and 0 build errors.

This round intentionally did not implement UI, WebView2 preview, real Codex CLI execution, OpenAI API calls, color-system UI, tuning panel, recording/frame extraction changes, mouse automation, page editing, zip packaging, or generated site construction.

## 2026-05-27 P1.2 package scaffold update

The P1.2 round established the structured package chain needed before real Codex execution:

- `ObservationPackageManifest` stores package metadata, observation artifacts, sections, interactions, findings, and warnings at `observation/observation-package.json`.
- `ObservationPackageService` can create an empty scaffold, import minimal legacy observation artifacts when present, add artifact/section/interaction/finding items, save/load with `WrbJsonOptions.Default`, and reject absolute or escaping artifact paths.
- `ConstructionPackageManifest` stores construction inputs, constraints, deliverables, required project files, and warnings at `codex-task/construction-package.json`.
- `ConstructionPackageService` references `project.wrbproj`, `observation/observation-package.json`, `input/assets/assets-manifest.json`, `theme/theme.json`, and `maps/content-map.json`, warning instead of crashing when required files are missing.
- `CodexTaskPackage` stores future task instructions, sandbox policy, input files, expected outputs, prohibited actions, and warnings at `codex-task/task-package.json`; `CodexTaskPackageService` also writes `codex-task/instructions.md`.
- Task package safety boundaries remain scaffold-only: allowed write roots are `codex-workspace/` and `output-site/current/`; forbidden areas include `.git`, source repository roots, user credential directories, system directories, and software install directories.
- `CodexTaskRunRecord` and `CodexTaskRunService` record future task run state at `codex-task/runs/<run-id>/run-record.json`, with basic transition rules and terminal-state protection.
- `TaskFailureCategory` and `TaskFailureClassifier` provide lightweight failure classification for validation, missing input, sandbox violation, secret detection, timeout, build failure, and related categories.
- `PackageValidationService` validates observation, construction, and task packages for manifest existence, schema version, required files, relative paths, project escape attempts, obvious secrets/local paths, `DataTuneId` availability, sandbox roots, and `instructions.md`.
- `.github/workflows/webrebuildrecorder-foundation.yml` runs build and FoundationSelfTest on Windows with .NET 8 for pushes and pull requests.

Verification on 2026-05-27:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed. `FoundationSelfTest` now prints distinct coverage statements for P0/P1, P1.1, and P1.2 instead of only a bare OK.

This round intentionally did not implement UI, WebView2 preview, real Codex CLI execution, OpenAI API calls, color-system UI, tuning panel, recording/frame extraction changes, mouse automation, page editing, zip packaging, or generated site construction.

## 2026-05-27 P1.1 data foundation update

The P1.1 durable data foundation round landed the project data layer needed before construction package orchestration:

- `AssetsManifestService` writes `input/assets/assets-manifest.json` and tracks asset id, kind, role, relative path, source type, safe source note, MIME, size, SHA-256, user/AI/external flags, export approval, and tags.
- `ThemeManifestService` writes `theme/theme.json`, creates a default scaffold theme, and rejects invalid hex colors outside `#RRGGBB`.
- `ContentMapService` writes `maps/content-map.json`, creates a default home/hero mapping, and rejects empty or duplicate `DataTuneId` values.
- `ProjectSnapshotService` writes `versions/snapshots/<snapshot-id>/snapshot-manifest.json`, copies selected project/output data when present, computes SHA-256 hashes, skips blocked directories/extensions, and records warnings.
- `ProjectLogService` writes layered JSONL logs under `logs/`.
- `ExportIntegrityCheckService` checks required project manifests/output, dangerous files, and secret/local-path scan results before future export.
- `SecretAndLocalPathScanService` conservatively scans known project/output files for obvious secrets and local paths.
- `WebRebuildRecorder.FoundationSelfTest` verifies the new P1.1 layer and all existing P0/P1 foundation checks.

Verification on 2026-05-27:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both commands passed.

This round is still scaffold-only for rollback UI, real export zip generation, full content-map DOM validation, full secret detection, and Codex task execution. It did not change UI, WebView2, color UI, tuning panel, OpenAI API, Codex CLI, recording/frame extraction, mouse automation, or page editor behavior.

The previous 21:45 review package recorded no commit/push because it was created from a non-Git workspace. The true Git repository later synced that closure via commit `d404465c`.

## 2026-05-26 GitHub memory sync rule update

`CODEX_CONSTRUCTION_RULES.md` now records the GitHub handoff model:

```text
GitHub = clean source + project memory + review summaries + traceable history
local review zip = complete local handoff package
GitHub Release = optional large artifacts or version bundles
ChatGPT long-term memory = navigation index, not the source of truth
```

Each completed round should commit clean source, tests/self-tests, project memory files, and `review_package` text summaries when the workspace is a real Git repository. Do not commit generated workspaces, zips, logs, recordings, extracted frames, screenshots, customer materials, API keys, tokens, SSH keys, Codex/OpenAI login files, proxy settings, local absolute-path configuration, or customer private data.

The current local directory was confirmed on 2026-05-26 to be outside a Git repository, so no commit or push was performed in this workspace.

## 2026-05-26 second round 2.1 patch update

The P0/P1 foundation patch closed the review risks found after the second foundation round:

- Manifest save flow no longer resets `ProjectState` to `ProjectCreated`.
- `ProjectManifestService.SetProjectStateAsync` is the explicit state-change method.
- `project.wrbproj` remains the new source of truth; legacy `project.json` / `project-info.json` remain compatibility copies.
- `ProjectManifestService.LoadAsync` rejects invalid `manifest.Paths`, including absolute paths, `..` traversal, sensitive directory segments, and Windows reparse point risks.
- `SandboxPathPolicy` blocks `AppContext.BaseDirectory`, related app/source roots, and Windows reparse point / symlink / junction parent chains.
- `WebRebuildRecorder.FoundationSelfTest` verifies state preservation, invalid manifest path rejection, application runtime directory blocking, reparse point blocking, legacy double-write, and enum string serialization.

The patch was verified with:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed. The patch intentionally did not implement UI redesign, WebView2, color UI, tuning panel, real Codex CLI execution, OpenAI API integration, recording enhancements, frame extraction enhancements, or broad legacy workflow refactors.

## 2026-05-26 second round implementation update

The second P0/P1 engineering foundation round landed the runtime foundation that the previous memory files described as pending:

- `ProjectManifestService` can create, load, validate, and save `project.wrbproj`.
- `WrbJsonOptions` centralizes JSON options and serializes enums as strings.
- New project creation creates V2 directories and writes both `project.wrbproj` and legacy `project.json` / `project-info.json`.
- `ProjectLockService` and `SandboxPathPolicy` have a minimum verification harness.
- `EnvironmentCheckService` exists only as a structured stub for now.
- `WebRebuildRecorder.FoundationSelfTest` verifies the foundation without introducing a heavy test framework.

The following remain out of scope and unimplemented after this round: UI redesign, WebView2, color UI, tuning panel, real Codex CLI execution, OpenAI API integration, recording enhancements, and frame-extraction enhancements.
# WebRebuildRecorder / AI 网站重构工作台｜本地长期记忆详细版

> 本文件只服务于 `WebRebuildRecorder` / AI 网站重构工作台项目。
> 它是 `PROJECT_MEMORY_INDEX.md` 的详细版，用于 GPT / Codex 在跨会话、源码审核、review_package 回测时恢复完整项目上下文。
> 每次 review_package 与源码审核 zip 必须包含本文件。

---

## 1. 项目最终定位与边界

本项目的长期定位是：

**Codex CLI 驱动的 AI 品牌化网站重构控制台 / 施工包生成器。**

它的目标不是让用户复制某个网站，而是让用户用参考站表达审美方向，再把这种结构、节奏、色彩、图片气质和动效关系转译成自己的品牌网站。

对外表达应使用：

- 参考风格生成；
- 品牌化重构；
- 审美拆解；
- 施工包生成；
- 生成结果校准；
- AI 建站控制台。

必须避免的表达：

- 仿站；
- 复刻；
- 抄站；
- clone；
- 拖拽网页编辑器。

原因：这些词会把产品误导成复制工具或通用编辑器，也会扩大版权和交付边界风险。

---

## 2. 旧项目现状与保留原则

当前源码项目名：`WebRebuildRecorder`。

当前技术栈：

- .NET 8；
- WPF 桌面应用；
- `UseWPF`；
- 部分使用 Windows Forms 能力；
- FFmpeg / FFprobe；
- JSON 和 Markdown 文件型项目资料。

当前旧功能包括：

- 新建 / 打开项目；
- `project.json` / `project-info.json` 兼容；
- 外部浏览器打开参考 URL；
- FFmpeg 录屏；
- FFprobe 读取视频信息；
- FFmpeg 抽帧；
- action-log / marker 记录；
- `observation.md` 生成；
- ChatGPT / GPT 分析包生成；
- GPT 结果导入；
- 素材需求报告；
- 最终 Codex 包生成；
- 用户主观意图与参考素材导入；
- 快捷键 / 悬浮录制窗。

这些旧功能不能删除。未来它们不再是主产品定位，而是服务于：

- 参考站观察；
- 局部复杂动效纠偏；
- 施工包准备；
- Codex 观察失败后的人工素材分析。

典型使用场景包括 hover 动效不准、图片明暗切换不准、月牙遮罩方向不准、滚动渐显节奏不准、视频触发效果需要局部抽帧分析等。

---

## 3. 当前真实施工状态

截至本长期记忆文件建立时，项目已完成：

1. 工程技术交底与项目本地长期记忆文件初步写入。
2. `PROJECT_BLUEPRINT.md`、`CODEX_PROJECT_MEMORY.md`、`CODEX_CONSTRUCTION_RULES.md`、`PROJECT_ROADMAP.md`、`PROJECT_STATUS.md`、`CURRENT_TASK.md`、`REVIEW_PACKAGE_RULES.md` 等蓝图类文件。
3. 第一轮 P0 工程底盘骨架定义层。

第一轮 P0 已新增定义层文件：

- `ProjectState`；
- `WrbProjectManifest`；
- `ProjectDirectoryV2`；
- `ProjectLock`；
- `ProjectLockService`；
- `SandboxPathPolicy`；
- `LogChannels`；
- `docs/samples/project.wrbproj.sample.json`。

这些文件的真实状态是“骨架层 / 定义层”，不是完整运行时能力。

尚未完成：

- 新建项目流程尚未真正创建 `project.wrbproj`；
- 新建项目流程尚未真正创建 V2 项目目录；
- `ProjectState` 尚未接入持久化和 UI 流程；
- `project.lock` 尚未锁定真实任务；
- 日志分层尚未迁移到真实日志系统；
- WebView2 尚未接入；
- Codex CLI 尚未接入；
- `theme.json`、`content-map.json`、`tune-overrides.css` 尚未实现。

下一步默认方向是继续 P0/P1 工程底盘落地，不进入 UI。

优先事项：

- `ProjectManifestService`；
- 统一 JSON options；
- `JsonStringEnumConverter`；
- `ProjectDirectoryV2Service`；
- `project.wrbproj` 与旧 `project.json` / `project-info.json` 双写；
- 新建项目时创建 V2 目录；
- `ProjectLockService` 最小测试或 harness；
- `SandboxPathPolicy` 最小测试或 harness；
- `EnvironmentCheckService` stub；
- 保持旧功能兼容。

仍然禁止：

- WebView2；
- 色系 UI；
- 调参面板；
- Codex CLI 真执行；
- 主界面重构；
- 磁吸面板；
- 图标批量生成。

---

## 4. 未来完整主流程

目标闭环：

1. 用户一次性输入资料；
2. 软件采集或组织参考站观察数据；
3. 软件复制并整理用户素材；
4. AI / Codex 生成结构化观察报告和施工包；
5. 软件内部组装 Codex CLI 任务包；
6. Codex CLI 在项目沙盒中黑盒执行；
7. 输出静态单页或静态多区块网站；
8. 软件通过 WebView2 / 内置浏览器预览；
9. 用户通过审美校准器轻量调参；
10. 保存 `tune-overrides.css`、`theme.json`、`content-map.json`；
11. 验收；
12. 导出 `output-site.zip`；
13. 下次可重新打开项目，修改素材、文案、色系后再次生成。

普通用户的体验应收敛为：

```text
输入资料 -> 生成中 -> 预览 -> 调参 -> 导出
```

高级 / 开发模式才展示施工包、日志、content-map、CSS 变量和中断恢复信息。

---

## 5. MVP 硬边界

第一阶段 MVP 只支持：

- 静态单页；
- 静态多区块品牌网站；
- 可导出静态网站包。

第一阶段明确禁止：

- CMS；
- 商城；
- 数据库后台；
- 支付；
- 登录注册；
- 会员系统；
- 留言后台；
- 论坛；
- 复杂多页面管理；
- 自由拖拽网页编辑器；
- 动画时间轴；
- 复杂组件市场。

如用户提出上述需求，本阶段只记录，不施工。

---

## 6. P0 承重柱与不可越界项

P0 优先级高于 UI。

P0 必须先解决：

1. 项目目录结构；
2. `project.wrbproj`；
3. `schemaVersion`；
4. `ProjectState` 状态机；
5. `project.lock`；
6. 沙盒路径安全；
7. 项目状态文件；
8. 用户环境检测；
9. 日志分层；
10. 回测包机制；
11. 旧项目格式兼容；
12. Codex 依赖提示。

P0/P1 阶段不能先做：

- WebView2；
- 色系 UI；
- 调参面板；
- Codex CLI 真执行；
- 主界面大重构。

这条原则的理由是：如果目录、状态、锁、沙盒、日志和回测机制没有落地，后续 UI 和 Codex 调用会缺少安全边界和可恢复性。

---

## 7. 项目文件格式与目录标准

工作期采用：

**项目文件夹 + `project.wrbproj`**

备份 / 迁移采用：

**`.wrb` / `.wrbpkg`**

交付 / 上线采用：

**`output-site.zip`**

`output-site.zip` 只包含最终网站，不包含工程历史、日志、施工包、内部资料。

标准项目目录：

```text
project.wrbproj
PROJECT_STATUS.md
input/
assets/
assets/original/
assets/selected/
theme/
observation/
observation/screenshots/
observation/dom/
codex-task/
output-site/
output-site/current/
output-site/versions/
tune/
maps/
exports/
logs/
review/
runtime/
codex-workspace/
```

`project.wrbproj` 类似 `.sln` / `.csproj`，应记录：

- `schemaVersion`；
- `appVersion`；
- `projectId`；
- `projectName`；
- `referenceUrl`；
- `state`；
- `currentOutputVersion`；
- `paths`；
- `createdAt`；
- `updatedAt`；
- current theme；
- current content-map；
- Codex 执行状态；
- 版本历史索引。

---

## 8. 版本快照与回滚

每次重新生成前，必须先保存当前版本快照。

推荐结构：

```text
output-site/current/
output-site/versions/v001/
output-site/versions/v002/
output-site/versions/v003/
```

回滚不能只回滚 `output-site`，还要恢复对应的主题、文案映射、项目输入、素材清单、调参覆盖和施工任务上下文。

每个版本快照必须保存：

- `VERSION_INFO.json`；
- `CHANGELOG.md`；
- `output-site` 文件；
- `theme.snapshot.json`；
- `content-map.snapshot.json`；
- `project-input.snapshot.json`；
- `assets-manifest.snapshot.json`；
- `tune-overrides.snapshot.css`；
- `codex-task` 快照。

素材不要每个版本重复复制。`assets/original` 保存原始素材，版本中记录素材 ID 和使用关系；只有生成图、裁剪图、处理图等必要派生物才进入快照。

---

## 9. ProjectState 状态机

项目不能只靠口头阶段推进，必须有 `ProjectState`。

最低状态集合：

- `ProjectCreated`；
- `InputCompleted`；
- `AssetsCopied`；
- `ThemeSelected`；
- `ObservationReady`；
- `TaskPackageGenerated`；
- `CodexRunning`；
- `CodexFailed`；
- `CodexCompleted`；
- `OutputValidated`；
- `PreviewReady`；
- `TuneDirty`；
- `TuneSaved`；
- `ExportReady`；
- `Exported`；
- `Archived`。

每个状态未来必须定义：

- 进入条件；
- 退出条件；
- 允许操作；
- 禁止操作；
- 失败回退点；
- 状态文件写入内容。

---

## 10. project.lock 与并发安全

Codex 运行或项目生成时必须锁定项目，防止用户同时改素材、换色系、重复点击生成。

`project.lock` 应记录：

- `taskId`；
- `taskType`；
- `startedAt`；
- `processId`；
- `status`；
- `allowCancel`；
- `recoveryHint`；
- `abnormalExit` 标记。

下一步应增加：

- stale lock 检测；
- 进程存活检测；
- `TryAcquireLock`；
- `LockResult`；
- 最小测试或 harness。

---

## 11. Codex CLI 黑盒边界与任务编排

不能把 Codex CLI 当成稳定函数调用。它是外部进程，可能断流、卡死、超时、认证失败、额度不足、半生成、输出不完整，甚至返回完成但页面损坏。

必须按任务编排处理：

1. 创建任务；
2. 写入任务包；
3. 锁定沙盒；
4. 启动进程；
5. 监听输出；
6. 定时检查；
7. 超时中断；
8. 保存日志；
9. 校验结果；
10. 失败分类；
11. 允许重试。

未来可拆为：

- `ICodexTaskOrchestrator`；
- `ICodexProcessRunner`；
- `ICodexOutputWatcher`；
- `ICodexResultVerifier`；
- `ICodexFailureClassifier`。

软件必须提示：Codex CLI 调用依赖用户自己的 OpenAI / Codex 账户、额度、登录状态、网络环境和远端服务稳定性。

---

## 12. 沙盒权限

Codex 只能在项目沙盒目录内操作。

允许写入：

```text
projects/{projectId}/codex-workspace/
projects/{projectId}/output-site/current/
projects/{projectId}/logs/
```

禁止写入：

- 软件安装目录；
- 源码仓库根目录；
- 其他项目目录；
- 用户系统目录；
- OpenAI / Codex 凭证目录；
- 插件目录；
- 任意未授权绝对路径。

必须防止：

- `../` 路径穿越；
- 中文路径异常；
- 空格路径异常；
- 长路径异常；
- 反斜杠 / 斜杠混乱；
- OneDrive 路径权限问题。

`SandboxPathPolicy` 已有骨架，下一步需要最小测试或 harness。

---

## 13. content-map / data-tune-id

页面生成必须建立 `content-map` 和稳定 `data-tune-id`。

示例：

```text
hero.title
hero.subtitle
hero.cta.primary
section.services.card.01.title
footer.contact.phone
```

必须校验：

1. `content-map` 中的 ID 是否在当前 DOM 中存在；
2. DOM 中是否出现未映射元素；
3. 旧 ID 是否失效；
4. 文案映射是否错位。

未来可建立：

- `ContentMapValidator`；
- `TuneIdScanner`；
- `MissingMappingReporter`。

---

## 14. 色彩系统

色彩模块不是复杂调色器，而是：

**色系方向选择 + 少量手动微调。**

流程：

1. Codex 分析目标站主体色系；
2. 用 3-4 个竖向色块表示主背景、主文字、弱文字/辅助色、强调色；
3. 基于目标站明度、对比度、冷暖、饱和度扩展 4-5 个候选色系；
4. 顶部显示当前选择 / 自定义色系；
5. 点击候选色系后同步到当前选择；
6. 当前选择中的每个色块可点击系统颜色窗口进行修改；
7. 自定义色系旁边或下方必须有实时变化的微缩网页结构卡片；
8. 色系结果写入 `theme.json`、`style-constraints.md` 和 CSS 变量初始值；
9. Codex 施工和 WebView2 调参必须遵守当前色系。

候选色系包括：

- 目标站原始色系；
- 同明度冷色变体；
- 同明度暖色变体；
- 高对比轻奢变体；
- 低饱和自然肤感变体。

---

## 15. 微缩卡片规则

微缩卡片只表达色彩氛围，不是真实最终网页。

结构可包含：header、hero、图片块、文字块、按钮、section、footer。

文字来源优先级：

1. 品牌名称；
2. 大口号 / 短标语；
3. 业务关键词；
4. 行业默认文案；
5. 通用占位文字。

文字长度规则：

- 大文字中文不超过 3 个汉字；
- 大文字英文约 6-8 个字符视觉长度；
- 小文字中文不超过 7 个汉字；
- 小文字英文不超过 14 个字符；
- Codex 可以负责语义提炼短词；
- 程序必须负责最终长度限制、截断、省略号和显示安全。

---

## 16. 调参面板边界

调参面板不是网页编辑器，而是：

**AI 生成后的审美校准器 / Design Tune / 主题参数面板。**

普通模式只调：

- 页面亮度；
- 页面对比度；
- 主标题大小；
- 副标题大小；
- 区块间距；
- 图片亮度；
- 图片灰度；
- 图片对比度；
- 按钮大小；
- 移动端标题大小。

高级模式可增加：

- 字体；
- 字重；
- 行高；
- 字距；
- 透明度；
- 图片圆角；
- 图片位置；
- section 间距。

开发模式才显示：

- `data-tune-id`；
- CSS selector；
- CSS variable；
- current value；
- new value；
- source file；
- content-map；
- 日志。

禁止早期加入自由拖拽、任意加组件、复杂图层、动画时间轴、CMS、商城、后台。

调参保存到 `css/tune-overrides.css` 或 `css/theme.generated.css`，并在 `index.html` 最后引入，不能直接破坏原始主 CSS。

---

## 17. 主界面原则

主界面按以下方案设计：

- 中央 WebView / 内置浏览器最大；
- 顶部任务区；
- 左侧流程输入区；
- 右侧属性 / 元素调整区；
- 底部可折叠全局主题 / 色系区；
- 准备、观察/生成、调参、验收/导出模式切换；
- 普通 / 高级 / 开发三层可见性。

P0/P1 不做磁吸，先保持固定布局和折叠隐藏。未来如规划有限磁吸面板，只允许左侧回左侧、右侧回右侧、底部回底部，顶部任务栏固定，且必须有恢复默认布局。

---

## 18. UI 图标资源系统

后续建立统一图标资源体系。

建议目录：

```text
Resources/Icons/
assets/icons/
```

优先使用 SVG。必要时导出 PNG / ICO。

第一批图标包括新建项目、打开项目、保存、开始观察、开始生成、暂停 / 停止、重试、导入素材、色系选择、预览、调参、验收、导出、打包、日志、设置、恢复默认、成功、警告、错误。

图标要求：简洁、科技感、艺术感、色彩明快、小尺寸可辨识、不复杂、不抢中央 WebView 注意力。

P0/P1 只建目录和 manifest。P2/P3 再批量生成与应用。

---

## 19. 素材与版权

素材不是简单上传。软件 / AI 应判断素材是否过粉、过绿、过亮、促销感过强、廉价，或与黑白灰高级感、品牌色系冲突。

`assets-manifest.json` 应记录：

```text
sourceType:
user_upload / ai_generated / stock / reference_observed

licenseStatus:
user_confirmed / unknown / restricted

canExport:
true / false
```

参考站图片、文案、Logo、独特图形不能默认复制。用户需确认素材授权。AI 生成素材应标记来源。

---

## 20. 观察失败降级

参考站观察可能遇到 Cloudflare、登录墙、地区限制、Cookie 弹窗、懒加载、视频无法播放、跨域限制、移动端差异、反自动化检测。

必须设计降级路径：

```text
自动观察失败
-> 用户手动截图 / 录屏 / 粘贴图片
-> 用户标记喜欢区域
-> Codex 用人工素材分析
```

喜欢区域支持图片粘贴，应作为观察失败 fallback 的一部分。

---

## 21. 导出前检查

导出前必须检查：

- `index.html` 是否存在；
- CSS 是否存在；
- JS 是否存在；
- images 是否缺失；
- `theme.json` 是否存在；
- `content-map.json` 是否存在；
- `tune-overrides.css` 是否存在并已引入；
- `README_DEPLOY.md` 是否存在；
- 是否包含本地绝对路径；
- 是否包含 API Key、Token、Cookie、账号密码；
- 是否包含用户隐私数据；
- 是否引用 `C:\Users\xxx`、`E:\Work\xxx` 等本地路径。

---

## 22. 日志分层与环境检测

至少应有：

- `app.log`；
- `codex-run.log`；
- `build.log`；
- `validation.log`；
- `export.log`；
- `error.log`。

普通用户看摘要。高级 / 开发模式看完整日志。

软件启动或首次执行前应检测：

- WebView2 Runtime；
- Codex CLI；
- OpenAI / Codex 登录状态；
- 网络连通性；
- 项目目录写权限；
- 磁盘空间；
- 文件路径长度；
- 系统代理状态。

下一步可先建立 `EnvironmentCheckService` stub。

---

## 23. 插件化边界

采用：

**稳定内核 + 接口分层 + 可插拔扩展。**

优先插件化：

- 参考站观察器；
- AI 执行器；
- 素材分析器；
- 导出器；
- 验收器；
- 行业模板包；
- 调参参数包。

不建议早期插件化：

- 项目状态管理；
- 文件目录结构；
- 安全沙盒；
- 基础日志；
- 中断恢复；
- 核心 UI 流程。

第一阶段只做接口、配置、服务注册。不要早期支持第三方 DLL 热加载、插件执行任意代码、插件改 UI 主流程或插件调用系统命令。

---

## 24. P0-P5 路线图

P0：

- 产品定位；
- 项目目录；
- `project.wrbproj`；
- `schemaVersion`；
- `ProjectState`；
- `project.lock`；
- 沙盒边界；
- 项目状态文件；
- 用户环境检测；
- Codex 依赖提示；
- 回测包机制。

P1：

- 一次性输入资料表单；
- 素材复制与 `assets-manifest`；
- `theme.json` 色系系统基础；
- `content-map` / `data-tune-id` 规则；
- `current` / `versions` 快照；
- 日志分层。

P2：

- 主界面骨架；
- WebView2 预览；
- 色系候选与微缩卡片；
- 施工包生成器；
- Codex CLI 任务编排与失败分类。

P3：

- Codex CLI 黑盒执行稳定化；
- 输出校验；
- 轻量审美调参器；
- `tune-overrides.css` 保存；
- `content-map` 校验；
- 观察失败降级。

P4：

- 导出完整性检查；
- 敏感信息检查；
- 自动验收报告；
- 局部录屏抽帧纠偏；
- 行业模板包；
- 样例项目测试；
- `.wrb` / `.wrbpkg` 归档与版本对比。

P5：

- 完整产品化；
- 普通 / 高级 / 开发模式完善；
- 插件 manifest；
- 更多行业包；
- 局部区块再生成；
- 多版本对比 UI。

---

## 25. 每轮施工与回测规则

每轮施工约 5 小时。

每轮开工前必须先读：

- `PROJECT_MEMORY_INDEX.md`；
- `docs/project-memory/PROJECT_MEMORY_FULL.md`；
- `PROJECT_BLUEPRINT.md`；
- `CODEX_PROJECT_MEMORY.md`；
- `CODEX_CONSTRUCTION_RULES.md`；
- `PROJECT_ROADMAP.md`；
- `PROJECT_STATUS.md`；
- `CURRENT_TASK.md`；
- `REVIEW_PACKAGE_RULES.md`。

每轮只做 `CURRENT_TASK.md` 明确的任务。不得越界做未授权功能。

每轮完成后必须生成 `review_package`，并打包源码给 GPT 审核。

每轮 `review_package` 至少包含：

- `CHANGELOG_THIS_TASK.md`；
- `SELF_CHECK_REPORT.md`；
- `FILES_CHANGED.md`；
- `BUILD_AND_RUN_REPORT.md`；
- `ERRORS_AND_RISKS.md`；
- `NEXT_STEPS.md`；
- `ARCHITECTURE_DECISIONS.md`；
- `PACKING_MANIFEST.md`。

每轮源码 zip 必须包含：

- `PROJECT_MEMORY_INDEX.md`；
- `docs/project-memory/PROJECT_MEMORY_FULL.md`；
- `PROJECT_BLUEPRINT.md`；
- `CODEX_PROJECT_MEMORY.md`；
- `CODEX_CONSTRUCTION_RULES.md`；
- `PROJECT_ROADMAP.md`；
- `PROJECT_STATUS.md`；
- `CURRENT_TASK.md`；
- `REVIEW_PACKAGE_RULES.md`；
- `review_package/`；
- 当前源码。

如果遗漏 `PROJECT_MEMORY_INDEX.md` 或 `docs/project-memory/PROJECT_MEMORY_FULL.md`，本轮回测包视为不完整。

---

## 26. 当前下一轮任务建议

下一轮不进入 UI。

建议任务：

**P0/P1 工程底盘落地：`project.wrbproj` 双写、V2 目录创建、锁与沙盒最小验证。**

必须做：

1. `ProjectManifestService`；
2. 统一 JSON options；
3. `JsonStringEnumConverter`；
4. `ProjectDirectoryV2Service`；
5. 新建项目时同时生成旧 `project.json` / `project-info.json` / 新 `project.wrbproj`；
6. 新建项目时创建 V2 目录；
7. `ProjectLockService` 最小测试或 harness；
8. `SandboxPathPolicy` 最小测试或 harness；
9. `EnvironmentCheckService` stub；
10. `review_package`。

仍然禁止：

- WebView2；
- 色系 UI；
- 调参面板；
- Codex CLI 真执行；
- 主界面重构；
- 磁吸面板；
- 图标批量生成。
# 2026-05-31 P1.5 Construction Readiness Gate Update

P1.5 implemented the strict readiness gate that must pass before future dry-run or real Codex execution.

Key additions:

- `ConstructionReadinessGateService`.
- `ConstructionReadinessMode`: `Draft`, `Strict`, `PreCodexDryRun`.
- `ConstructionReadinessResult` and readiness item/report models.
- Readiness reports at `codex-task/readiness/readiness-report.json` and `codex-task/readiness/readiness-report.md`.
- Aggregated checks for project manifest, V2 directories, data manifests, observation package, construction package, construction context/package-index, task package/instructions, strict package validation, secret/local-path scan, export integrity, rollback snapshot availability, sandbox policy, output surface, environment awareness, optional Design Context / Reference Portal awareness, GitHub Actions workflow file, and blocking-reason failure category mapping.
- Readiness report sanitizer for local absolute paths, credential directories, and secret values.
- `ProjectSnapshotService` readiness probe prefix: `reason = readiness-probe` creates snapshot ids starting with `readiness-probe-`.
- FoundationSelfTest P1.5 coverage for positive Draft/Strict/PreCodexDryRun readiness and negative hash mismatch, secret/local-path, missing package-index, missing output surface, expanded task allowed roots, reference-site asset approval, and report sanitizer scenarios.

Final verification after documentation refresh:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed on 2026-05-31 with 0 build warnings and 0 build errors.

Still not implemented: UI changes, WebView2, color UI, tuning panel, real Codex CLI execution, OpenAI API calls, Ollama/LM Studio generation, recording/frame extraction/mouse automation enhancement, page editor, real website generation, `output-site.zip`, Reference Portal UI, and heavy Design Context Library implementation.
