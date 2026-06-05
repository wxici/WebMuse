# Project Status

## 2026-06-05 Direction Reset

2026-06-05 direction reset:
After P1.8-0, the project enters P2A Structural Alpha. P1.7.4 is postponed, not cancelled. The next implementation task is P2A-0 WebView2 Preview Shell. WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.8-direction-close was completed first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe documentation.

Reason: the old WebRebuildRecorder workflow already had a manual observation/GPT/Codex/zip loop. A meaningful alpha must now demonstrate rebuilt-architecture advantages: embedded WebView2 preview, Source Snapshot MVP, controlled Codex CLI execution, output-site/current preview, and minimal tuning/color controls.

P2A roadmap:

1. P2A-0 WebView2 Preview Shell.
2. P2A-1 Source Snapshot MVP.
3. P2A-2 Codex CLI Proof Runner.
4. P2A-3 Codex CLI Controlled Site Generation + WebView2 Preview.
5. P2A-4 Minimal Tuning / Color Controls.

## Latest P1.8-0 Early Alpha Validation Probe

Snapshot time: 2026-06-05.

WebMuse sync status: P1.8-0 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.8-0 is an early alpha validation probe. It does not execute Codex CLI, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

It also does not run any `codex` command, implement P1.7.4 failure recovery, implement a manual export fallback writer, or change UI/WebView2/Source Snapshot/ProposalPreview behavior.

Synced source:

1. `WebRebuildRecorder.App/Core/ProjectSystem/AlphaValidationProbe.cs`
2. `WebRebuildRecorder.App/Core/ProjectSystem/AlphaValidationProbeService.cs`
3. `WebRebuildRecorder.FoundationSelfTest/Program.cs`

The probe writes runtime reports under ignored `codex-task/alpha-validation/<probe-id>/alpha-validation-report.json` and `alpha-validation-report.md`.

The probe composes existing P0/P1 foundations into one explainable local report: V2 project structure, project manifest, assets/theme/content-map, observation package, construction package, task package/instructions, P1.5 readiness, P1.6 dry-run, P1.7.1 proof package, P1.7.2 approval artifacts, P1.7.3 execution precondition, manual fallback evidence, runtime artifact ignore coverage, and non-execution boundary checks.

Prototype verification before sync:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: passed in the prototype repository and printed the five required P1.8-0 verification lines.

Previous next recommendation was P1.7.4-A Failure recovery models + static policy table. The 2026-06-05 direction reset supersedes that immediate sequence: P1.7.4 is postponed, not cancelled, and the next stage is P2A Structural Alpha starting with P2A-0 WebView2 Preview Shell.

## Latest P1.7.3 Execution Precondition Service

Snapshot time: 2026-06-04 16:37 +08:00.

WebMuse sync status: P1.7.3 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.7.3 implements execution precondition aggregation only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

Repository workflow remains prototype-first:

1. `wxici/codex/WebRebuildRecorder` is the primary construction worktree.
2. `wxici/WebMuse` receives only public-safe source and status documentation after prototype verification.
3. Runtime execution reports under `codex-task/execution/` are ignored and must not be committed.

Completed in this round:

1. Added `ExecutionPrecondition.cs` with schema constants, status/severity/decision enums, report/item models, and options.
2. Added `ExecutionPreconditionService.cs` with `EvaluateAsync(...)` and `LoadLatestAsync(...)`.
3. `EvaluateAsync(...)` writes project-relative runtime reports under `codex-task/execution/<execution-id>/execution-preconditions.json` and `execution-preconditions.md`.
4. The service aggregates existing P1.5 readiness, P1.6 dry-run, P1.7.1 proof-check package, P1.7.2 approval gate, P1.4 rollback/snapshot, sandbox roots, secret/local-path scan, output-site safety, codex-workspace safety, logs writability, task package hash stability, context freshness, manual fallback availability, and non-execution boundary checks.
5. Missing real proof execution is explicitly `NotImplemented` and blocks real execution in P1.7.3.
6. Approval validation covers approved/current bindings and blocks missing, pending, rejected, or stale approvals.
7. `AllowsRealCodexExecution`, `ExecutesCodexCli`, `CallsOpenAiApi`, `CallsLocalModel`, and `GeneratesWebsite` remain `false`.
8. Root and project `.gitignore` now ignore `codex-task/execution/` runtime artifacts.
9. `WebRebuildRecorder.FoundationSelfTest` covers P1.7.3 models, persistence, required aggregation items, blocking scenarios, secret/local-path detection, gitignore coverage, and non-execution boundaries.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. FoundationSelfTest passed and printed the five required P1.7.3 execution precondition verification lines.

Still out of scope and not implemented: failure recovery policy service, manual export fallback writer, real Codex CLI execution, any `codex` command, OpenAI API calls, Ollama/LM Studio/local model calls, website generation, `output-site/current/index.html`, proof execution result files, UI changes, WebView2, tuning UI, Reference Portal, Design Context Library, Frontend Effect Recipe Library, ProposalPreview, recording/frame extraction changes, zips, logs, customer materials, tokens, keys, and cookies.

Next recommended round: P1.7.4 Failure recovery policy service. P1.7.4 still must not execute Codex CLI or generate websites.

## Latest P1.7.2 Approval Gate Models And Persistence

Snapshot time: 2026-06-03 23:26 +08:00.

P1.7.2 implements approval gate models and persistence only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

Repository workflow remains prototype-first:

1. `wxici/codex/WebRebuildRecorder` is the primary construction worktree.
2. `wxici/WebMuse` receives only public-safe source and status documentation after prototype verification.
3. Runtime approval artifacts under `codex-task/approvals/` are ignored and must not be committed.

Completed in this round:

1. Added `ApprovalGate.cs` with schema constants, approval gate/decision enums, request/result/binding/validation models, and `CannotBeBypassedByAi = true` defaults.
2. Added `ApprovalGateService.cs` for `CreatePendingAsync`, request/result loading, approve/reject/cancel/expire/supersede transitions, binding hash validation, and validation report writing.
3. Approval files persist under project-relative `codex-task/approvals/<approval-id>/approval-request.json` and `approval-result.json`.
4. Validation reports persist under the same approval directory as `approval-validation-report.json` and `approval-validation-report.md`.
5. Approval bindings include required `codex-task/task-package.json` and `codex-task/instructions.md` hashes, plus optional dry-run plan, proof manifest, and execution-plan hashes when present.
6. `ApproveAsync()` revalidates bound hashes and blocks stale approvals.
7. Illegal state transitions throw `InvalidOperationException`.
8. Root and project `.gitignore` now ignore `codex-task/approvals/` runtime artifacts.
9. `WebRebuildRecorder.FoundationSelfTest` covers P1.7.2 models, persistence, enum string serialization, hash invalidation, state transitions, sanitizer checks, gitignore coverage, and the non-execution boundary.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. FoundationSelfTest passed and printed the five required P1.7.2 approval gate verification lines.

Still out of scope and not implemented: execution precondition service, failure recovery policy, manual export fallback, real Codex CLI execution, any `codex` command, OpenAI API calls, Ollama/LM Studio/local model calls, website generation, `output-site/current/index.html`, proof execution result files, UI changes, WebView2, tuning UI, Reference Portal, Design Context Library, Frontend Effect Recipe Library, ProposalPreview, recording/frame extraction changes, zips, logs, customer materials, tokens, keys, and cookies.

Next recommended round: P1.7.3 Execution precondition service. P1.7.3 still must not execute Codex CLI, call OpenAI API, call local model engines, or generate websites.

## Latest P1.7.1-sync Proof-Check Backfill

Snapshot time: 2026-06-03 16:05 +08:00.

P1.7.1 was first implemented in `wxici/WebMuse` as commit `2d2cb82a02992103d292f16bb80d25ae1a2a94b9` with message `P1.7.1 add proof-check package models and manifest`.
This sync round backfills that P1.7.1 proof-check package source and FoundationSelfTest coverage into the primary prototype repository `wxici/codex/WebRebuildRecorder`.

Repository workflow is now explicit:

1. `wxici/codex/WebRebuildRecorder` is the primary construction source.
2. `wxici/WebMuse` is the public OSS mirror and presentation repository.
3. Future implementation should land and verify in the prototype repository first, then synchronize only public-safe source and status documentation to WebMuse.

Completed in this sync round:

1. Added `ProofCheckPackage.cs` to the prototype repository.
2. Added `ProofCheckPackageService.cs` to the prototype repository.
3. Synced the P1.7.1 FoundationSelfTest proof-check coverage from WebMuse.
4. Tightened root and project `.gitignore` rules for future `codex-task/proof/` runtime artifacts.
5. Refreshed project task/status/memory/checkpoint and review_package documentation.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. FoundationSelfTest passed and printed the required P1.7.1 proof-check verification lines.

Still out of scope and not implemented: P1.7.2, approval gates, real Codex CLI execution, any `codex` command, OpenAI API calls, Ollama/LM Studio/local model calls, website generation, `output-site/current/index.html`, proof runtime result files, UI changes, WebView2, tuning UI, Reference Portal, Design Context Library, Frontend Effect Recipe Library, ProposalPreview, recording/frame extraction changes, zips, logs, customer materials, tokens, keys, and cookies.

Prototype Git transport is performed after this report is staged; the final assistant response records the resulting commit hash and push status.

## Latest P1.7-0 Proof-Check And Approval-Gate Design

Snapshot time: 2026-05-31 19:47 +08:00.

P1.7-0 defines proof-check and approval-gate design only.
It does not implement proof-check service, approval gate service, real Codex CLI execution, OpenAI API calls, or website generation.
Future real execution requires readiness, dry-run, proof-check, approval, rollback confirmation, sandbox validation, and failure recovery.

New design file:

```text
docs/project-memory/P1_7_CODEX_EXECUTION_PROOF_CHECK_AND_APPROVAL_GATE_DESIGN.md
```

Defined in this round:

1. Future proof-check files under `codex-task/proof/`.
2. Future approval gates and approval persistence under `codex-task/approvals/<approval-id>/`.
3. Rollback confirmation requirements.
4. Execution preconditions.
5. Future execution state machine.
6. Failure recovery and retry policy categories.
7. Manual construction package export fallback.
8. P1.7 implementation split through P1.7.1-P1.7.6.

Next recommended round: P1.7.1 Proof-check package models and manifest. P1.7.1 still does not execute Codex CLI.

Local verification for P1.7-0 passed: `dotnet build WebRebuildRecorder.slnx` completed with 0 warnings and 0 errors, and FoundationSelfTest passed.

Still out of scope and not implemented in P1.7-0: source code changes, FoundationSelfTest changes, proof-check service, approval gate service, execution precondition service, real Codex CLI execution, any `codex` command, OpenAI API calls, Ollama/LM Studio calls, website generation, `output-site/current/index.html`, UI changes, WebView2, Reference Portal, Design Context Library, ProposalPreview, recording/frame extraction changes, page editors, runtime artifacts, logs, dry-runs, zips, and secrets.

## Latest P1.6.1 Transport Closure And Dry-Run Artifact Audit

Snapshot time: 2026-05-31 19:36 +08:00.

P1.6 was later committed and pushed as cc28e56613d16032cea5664d23d8415a37610a86.
Older review_package text saying commit/push was pending is historical transport context from report-writing time, not the current GitHub state.

P1.6.1 is a closure/audit round only. It records P1.6 transport completion, audits the dry-run output surface, and tightens ignore rules for generated runtime artifacts.

Audited P1.6 dry-run outputs:

- `codex-task/dry-runs/<dry-run-id>/dry-run-plan.json`
- `codex-task/dry-runs/<dry-run-id>/dry-run-result.json`
- `codex-task/dry-runs/<dry-run-id>/dry-run-report.md`
- `codex-task/dry-runs/<dry-run-id>/dry-run-record.json`

Runtime artifacts now covered by ignore rules include dry-runs, task run records, logs, `output-site/current/`, restore reports, readiness-probe snapshots, export zips, source-review zips, and build outputs.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. The first self-test attempt exceeded the 120 second shell timeout without returning a test failure; the longer 300 second rerun passed with grouped P0/P1 through P1.6 output.

Still out of scope and not implemented in P1.6.1: source service changes, FoundationSelfTest changes, P1.7 proof-check implementation, real Codex CLI execution, any `codex` command, OpenAI API calls, Ollama/LM Studio calls, website generation, UI changes, WebView2, Reference Portal, Design Context Library, ProposalPreview, recording/frame extraction changes, page editors, and zip generation.

## Latest P1.6 Codex CLI Dry-Run Orchestrator

Snapshot time: 2026-05-31 19:25 +08:00.

P1.6 implemented the dry-run orchestrator for future Codex CLI construction runs while still forbidding real execution.

Completed in this round:

1. Added `CodexDryRun.cs` models for dry-run plan, result, report inputs, output targets, safety checks, and dry-run record.
2. Added `CodexDryRunOrchestratorService`.
3. Integrated `ConstructionReadinessGateService.CheckAsync(..., ConstructionReadinessMode.PreCodexDryRun, ...)`.
4. Validated `codex-task/task-package.json`, `codex-task/instructions.md`, required input files, allowed write roots, forbidden roots, rollback readiness, and sandbox boundaries.
5. Generated simulated dry-run steps without starting any external process.
6. Wrote dry-run artifacts under `codex-task/dry-runs/<dry-run-id>/`:
   - `dry-run-plan.json`
   - `dry-run-result.json`
   - `dry-run-report.md`
   - `dry-run-record.json`
7. Wrote project/security/codex-task logs through `ProjectLogService`.
8. Strengthened generated `instructions.md` with exact P1.6 safety boundary phrases.
9. Expanded `WebRebuildRecorder.FoundationSelfTest` for P1.6 success and blocking scenarios.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. FoundationSelfTest passed with grouped P1.6 output for orchestrator, PreCodexDryRun integration, missing task package blocking, instruction boundary checks, allowed write roots checks, dry-run reports, non-execution flags, output-site untouched, report sanitizer, and dry-run logs.

Still out of scope and not implemented: real Codex CLI execution, any `codex` command execution, OpenAI API calls, Ollama/LM Studio calls, remote AI service access, website generation, writing `output-site/current/index.html`, UI changes, WebView2, Reference Portal UI, Design Context Library implementation, Frontend Effect Recipe Library implementation, ProposalPreview, recording/frame extraction changes, page editors, and zip generation.

## Latest P1.6-0 Dry-Run Orchestrator Preflight Plan

Snapshot time: 2026-05-31 15:42 +08:00.

P1.6-0 is a planning-only preflight round. It does not implement `CodexDryRunOrchestratorService`, does not add service source code, does not modify `WebRebuildRecorder.FoundationSelfTest`, does not execute Codex CLI, does not call OpenAI API, does not generate a website, and does not change UI/WebView2/Reference Portal/Design Context/ProposalPreview.

The active plan is recorded in:

```text
docs/project-memory/P1_6_DRY_RUN_ORCHESTRATOR_PLAN.md
```

The future full P1.6 scope remains dry-run only:

1. verify readiness;
2. generate a dry-run task plan;
3. simulate execution;
4. check allowed write roots;
5. create or update a task run record;
6. write dry-run reports and logs;
7. stop before real Codex CLI execution.

Full P1.6 should wait for restored quota and use `gpt-5.5 xhigh`.

Verification for P1.6-0:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. Existing FoundationSelfTest passed. No source code or FoundationSelfTest files were modified.

## Latest P1.5.1 Blueprint Memory Sync And Transport Closure

Snapshot time: 2026-05-31 14:44 +08:00.

P1.5 was later committed and pushed as `e1161a323460afd65f8c5c22b669c9961f23ebf8`. Older `review_package` text saying local commit was prepared but push failed is historical transport context from the earlier environment, not the current GitHub state.

P1.5.1 is a documentation-only sync round in the true Git worktree `E:\GitHub\codex\WebRebuildRecorder`.

Completed in this round:

1. Corrected P1.5 transport status in project memory/status/review-package documents.
2. Read and indexed the current AI engine, design skills, design context, Reference Portal, Reference Site Library, and Frontend Effect Recipe Library blueprints.
3. Recorded that `PROJECT_BLUEPRINT_PROPOSAL_PREVIEW.md` is missing and must not be fabricated.
4. Updated task-type-specific startup reading rules in `CODEX_PROJECT_MEMORY.md`.
5. Clarified that P1.6 remains dry-run orchestration only.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. FoundationSelfTest passed on the longer-timeout `--no-build` rerun after the first shell invocation timed out without returning a test failure.

Still out of scope and not implemented: UI changes, WebView2, Reference Portal UI, Design Context Library implementation, Frontend Effect Recipe Library implementation, ProposalPreview implementation, real Codex CLI execution, OpenAI API calls, Ollama/LM Studio execution, AI engine integration, recording/frame-extraction enhancements, mouse automation, page editor, real website generation, and `output-site.zip`.

## Latest P1.5 Construction Readiness Gate Status

Snapshot time: 2026-05-31 14:15 +08:00.

P1.5 Construction Package Strict Readiness Gate is implemented and locally verified in the true Git worktree `E:\GitHub\codex\WebRebuildRecorder`.

P1.4 was committed and pushed as `169fd61 P1.4 add snapshot restore and rollback service`.

Completed in this round:

1. Added `ConstructionReadinessGateService`.
2. Added readiness models and modes: `Draft`, `Strict`, and `PreCodexDryRun`.
3. Added readiness reports under `codex-task/readiness/readiness-report.json` and `readiness-report.md`.
4. Aggregated package validation, secret/local-path scan, export integrity checks, rollback snapshot availability, sandbox policy checks, environment awareness, context freshness, asset/reference-site risk checks, optional Design Context / Reference Portal awareness, and GitHub Actions workflow file checks.
5. Added report sanitization so readiness reports do not expose local absolute paths, credential directories, or secret values.
6. Added readiness-probe snapshot semantics: `reason = readiness-probe` creates snapshot ids with the `readiness-probe-` prefix.
7. Added readiness project/security/codex-task logging through `ProjectLogService`.
8. Expanded `WebRebuildRecorder.FoundationSelfTest` for P1.5 and kept previous checks passing.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result after documentation refresh: build succeeded with 0 warnings and 0 errors; FoundationSelfTest passed and reported P1.5 Draft, Strict, PreCodexDryRun, report files, hash mismatch, secret/local-path, rollback availability, context freshness, readiness-probe snapshot, output surface, AI engine awareness, reference asset risk, optional awareness, sanitizer, and failure-category coverage.

Still out of scope and not implemented: UI changes, WebView2, color-system UI, tuning panel, real Codex CLI execution, OpenAI API calls, Ollama/LM Studio generation, recording/frame-extraction enhancements, mouse automation, page editor, real website generation, `output-site.zip`, Reference Portal UI, and heavy Design Context Library implementation.

## Latest P1.3.1 Closure And P1.4 Snapshot Restore Status

Snapshot time: 2026-05-27 15:58 +08:00.

P1.3.1 CI/report closure and P1.4 Snapshot Restore/Rollback Service are implemented and locally verified in the true Git worktree `E:\GitHub\codex\WebRebuildRecorder`.

P1.3 was committed and pushed as `4fc602161c9b9f3a5ca5bf2df2db18ef865012b0`. Older P1.3 `review_package` text saying commit/push was pending is historical context from report-writing time, not the current state.

Completed in this round:

1. Added `SnapshotRestoreService`.
2. Added snapshot restore/validation models.
3. Implemented `ListSnapshotsAsync`, `LoadSnapshotAsync`, `ValidateSnapshotAsync`, `CreateRestorePlanAsync`, and `RestoreAsync`.
4. Added restore reports under `versions/restore-reports/<restore-id>/`.
5. Added restore validation for schema, relative paths, source existence, SHA-256 hash mismatch, allowed restore targets, project escape, forbidden directories, and skipped zip/video/log/binary-like targets.
6. Added before-restore safety snapshot creation using `ProjectSnapshotService`.
7. Added restore project/security logging through `ProjectLogService`.
8. Expanded `WebRebuildRecorder.FoundationSelfTest` for P1.4 and kept previous checks passing.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build succeeded with 0 warnings and 0 errors; FoundationSelfTest passed and reported P1.4 snapshot listing, validation, hash mismatch detection, safety snapshot, restore result report, and forbidden path blocking coverage.

Still out of scope and not implemented: UI changes, WebView2, color-system UI, tuning panel, real Codex CLI execution, OpenAI API calls, recording/frame-extraction enhancements, mouse automation, page editor, real website generation, output-site zip generation, and rollback UI workflow.

## Latest P1.3 Legacy Bridge And Construction Context Status

Snapshot time: 2026-05-27 12:20 +08:00.

P1.3 Legacy Observation Bridge + Construction Package Content Generator is implemented and locally verified in the true Git worktree `E:\GitHub\codex\WebRebuildRecorder`.

P1.2 was committed and pushed as `59ba12e4d20a057d58b574f359c72cb7dcef555b`.

Completed in this round:

1. Added `LegacyObservationBridgeService` and `observation/legacy-bridge-report.json`.
2. Bridged legacy `observation/observation.md` into structured observation sections and findings.
3. Bridged legacy `observation/action-log.json` into structured observation interactions.
4. Bridged legacy `observation/frame-index.json` and `observation/screenshots/frame-index.json` as observation artifacts with frame metadata summaries where parseable.
5. Added `ConstructionPackageContentBuilderService`.
6. Generated `codex-task/context/project-brief.md`, `observation-summary.md`, `asset-index.md`, `theme-summary.md`, `content-map-summary.md`, `constraints.md`, `acceptance-checklist.md`, and `package-index.json`.
7. Updated construction package inputs with generated context files.
8. Strengthened `CodexTaskPackageService` so task inputs and `instructions.md` include the P1.3 context reading order.
9. Added `CodexTaskRunService.MarkQueuedAsync()` and Created -> Queued -> Running -> terminal transitions.
10. Added `PackageValidationMode.Draft` and `PackageValidationMode.Strict`.
11. Added `workflow_dispatch` and a 15 minute job timeout to the foundation GitHub Actions workflow.
12. Expanded `WebRebuildRecorder.FoundationSelfTest` for P1.3 plus previous P0/P1/P1.1/P1.2 checks.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build succeeded with 0 warnings and 0 errors; FoundationSelfTest passed and reported P1.3 legacy bridge, context builder, queued transition, and strict package validation coverage.

Still out of scope and not implemented: UI changes, WebView2, color-system UI, tuning panel, real Codex CLI execution, OpenAI API calls, recording/frame-extraction enhancements, mouse automation, page editor, real website generation, and package zip generation.

## Latest P1.2 Package Scaffold Status

Snapshot time: 2026-05-27 10:35 +08:00.

P1.2 observation package, construction package, Codex task package, task run record, failure classification, package validation, and GitHub Actions CI scaffolds are implemented and locally verified in the true Git worktree `E:\GitHub\codex\WebRebuildRecorder`.

Completed in this round:

1. Added `ObservationPackageManifest` and `ObservationPackageService` for `observation/observation-package.json`.
2. Added `ConstructionPackageManifest` and `ConstructionPackageService` for `codex-task/construction-package.json`.
3. Added `CodexTaskPackage` and `CodexTaskPackageService` for `codex-task/task-package.json` and `codex-task/instructions.md`.
4. Added `CodexTaskRunRecord`, `CodexTaskRunStatus`, and `CodexTaskRunService` for future run records under `codex-task/runs/<run-id>/run-record.json`.
5. Added `TaskFailureCategory` and `TaskFailureClassifier`.
6. Added `PackageValidationService` for observation, construction, and task packages.
7. Extended `SecretAndLocalPathScanService` to include P1.2 package files and tightened the `sk-` detector to avoid matching ordinary scaffold IDs.
8. Added `.github/workflows/webrebuildrecorder-foundation.yml`.
9. Expanded `WebRebuildRecorder.FoundationSelfTest` for P1.2 creation/save/load, path rejection, run-state transitions, enum string serialization, failure classification, and package validation scenarios.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build succeeded with 0 warnings and 0 errors; FoundationSelfTest passed and reported P1.2 scaffold coverage.

Still out of scope and not implemented: UI changes, WebView2, color-system UI, tuning panel, real Codex CLI execution, OpenAI API calls, recording/frame-extraction enhancements, mouse automation, page editor, generated website construction, and package zip generation.

## Latest P1.1 Data Foundation Status

Snapshot time: 2026-05-27 10:05 +08:00.

P1.1 durable project data foundations are implemented and verified in the true Git worktree `E:\GitHub\codex\WebRebuildRecorder`.

Completed in this round:

1. Added `AssetsManifestService` and `input/assets/assets-manifest.json` model support for create/load/save/add-or-update, safe project-relative asset paths, file size/hash capture, and safe source-note handling.
2. Added `ThemeManifestService` and `theme/theme.json` model support with default theme generation and minimal `#RRGGBB` color validation.
3. Added `ContentMapService` and `maps/content-map.json` model support with a default page/section/element map and unique non-empty `DataTuneId` validation.
4. Added `ProjectSnapshotService` scaffold for `versions/snapshots/<snapshot-id>/snapshot-manifest.json`, copying selected project data/output files when present, hashing copied files, and warning on missing optional files.
5. Added `ProjectLogService` scaffold for layered JSONL logs including `app.log`, `project.log`, `observation.log`, `codex-task.log`, `export.log`, and `security.log`.
6. Added `ExportIntegrityCheckService` scaffold for required project data/output checks, dangerous output-file detection, and secret/local-path scan integration.
7. Added `SecretAndLocalPathScanService` scaffold for conservative detection of `sk-`, API-key/token/password markers, Windows absolute paths, `/home/`, `.ssh`, `.codex`, and `.openai` in known manifest/output files.
8. Extended `WebRebuildRecorder.FoundationSelfTest` to cover the new P1.1 foundations while keeping existing P0/P1 checks.
9. Confirmed the prior 21:45 non-Git review package was later synchronized to GitHub by commit `d404465c`.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build succeeded with 0 warnings and 0 errors; FoundationSelfTest passed.

Still scaffold-only: snapshot restore/rollback UI, full export zip generation, complete content-map DOM validation, full secret detection/NLP, full license workflow, and real layered migration of all legacy logs.

Still out of scope and not implemented: UI changes, WebView2, color-system UI, tuning panel, real Codex CLI execution, OpenAI API calls, recording/frame-extraction enhancements, mouse automation enhancements, page editor, and large legacy workflow refactors.

## Latest Documentation Status

Snapshot time: 2026-05-26 21:40 +08:00.

The second round 2.1 P0/P1 foundation patch was re-verified, and GitHub submission / project memory sync rules were added.

Completed in this documentation closure:

1. Re-ran `dotnet build WebRebuildRecorder.slnx`; build succeeded with 0 warnings and 0 errors.
2. Re-ran `dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj`; foundation self-check passed.
3. Added GitHub submission and project memory sync rules to `CODEX_CONSTRUCTION_RULES.md`.
4. Tightened `.gitignore` to keep generated archives, logs, recordings, frames, screenshots, and credential-like files out of Git.
5. Confirmed the current local directory is not a Git repository, so no commit or push was performed from this workspace.

GitHub policy now recorded: GitHub should contain clean source, project memory files, review summaries, and traceable history. Local review zips stay local by default; large artifacts should use GitHub Releases if they must be preserved remotely.

## Latest Patch Status

Snapshot time: 2026-05-26 21:35 +08:00.

Second round 2.1 P0/P1 foundation patch is implemented and verified.

Fixed in this patch:

1. `ProjectService.ApplyProjectToManifest()` no longer resets `manifest.State` to `ProjectCreated` during save.
2. `ProjectManifestService.SetProjectStateAsync()` provides an explicit safe state update path.
3. `ProjectManifestService.LoadAsync()` now validates `manifest.Paths` and rejects absolute paths, `..` traversal, sensitive segments, and reparse point risks.
4. `project.wrbproj` remains the new source of truth when manifest and legacy files coexist; open flow still applies manifest core fields over legacy project files.
5. `SandboxPathPolicy` now rejects paths under `AppContext.BaseDirectory`, related application/source roots, and Windows reparse point / symlink / junction parent chains.
6. `WebRebuildRecorder.FoundationSelfTest` now verifies state preservation, invalid manifest path rejection, application runtime directory blocking, reparse point blocking, legacy double-write, and string enum serialization.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build succeeded with 0 warnings and 0 errors; foundation self-check passed.

Source review package:

```text
E:\Work\WebRebuildRecorder\WebRebuildRecorder_source-review_20260526_213932.zip
```

Package validation: 102 entries, missing required files 0, forbidden entries 0.

Still out of scope and not implemented: UI redesign, WebView2, color UI, tuning panel, real Codex CLI execution, OpenAI API integration, recording/frame-extraction enhancements, and broad legacy workflow refactors.

## Latest Round Status

Snapshot time: 2026-05-26 21:04 +08:00.

Second round P0/P1 engineering foundation landing is implemented and verified.

Completed in this round:

1. Added `ProjectManifestService` for `project.wrbproj` create/load/save with schema validation, created/updated timestamps, project id, project root marker, relative project paths, and clear JSON/schema errors.
2. Added unified `WrbJsonOptions` with `JsonStringEnumConverter` and moved project JSON writers/readers to the shared options.
3. Added `ProjectDirectoryV2Service` and wired new project creation to create the V2 project directory structure idempotently.
4. New project creation now writes `project.wrbproj` and still writes legacy `project.json` / `project-info.json` for compatibility. The new manifest is the forward source of truth; legacy files remain a transition layer.
5. Expanded `ProjectLockService` with readable lock JSON fields, duplicate lock detection, release/reacquire behavior, and corrupt lock handling.
6. Expanded `SandboxPathPolicy` to validate project roots, path traversal, sensitive folders such as `.git`, `.ssh`, `.codex`, system/install roots, source-repo roots, and allowed Codex write areas.
7. Added `EnvironmentCheckService` as a structured stub. WebView2, Codex CLI, and login checks are intentionally skipped/stubbed in this round.
8. Added `WebRebuildRecorder.FoundationSelfTest`, a lightweight console harness that verifies V2 directories, manifest writing, enum string serialization, legacy double-write, project lock behavior, sandbox allow/deny cases, and environment-check serialization.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build succeeded with 0 warnings and 0 errors; foundation self-check passed.

Source review package:

```text
E:\Work\WebRebuildRecorder\WebRebuildRecorder_source-review_20260526_210730.zip
```

Package validation: 100 entries, missing required files 0, forbidden entries 0.

Still out of scope and not implemented: UI redesign, WebView2, color UI, tuning panel, real Codex CLI execution, OpenAI API integration, recording/frame-extraction enhancements, and broad legacy workflow refactors.

## Snapshot Time

2026-05-26 17:20 +08:00

## Current Overall State

WebRebuildRecorder is still a .NET 8 WPF desktop application. Its working production capability is external-browser reference-site opening, FFmpeg screen recording, frame extraction, observation Markdown generation, GPT/ChatGPT package generation, and final Codex package preparation.

The current application is not yet a complete AI website construction console. It does not currently include embedded WebView2 preview, Codex CLI execution, OpenAI API calls, color-system UI, a tuning panel, CMS, e-commerce, database backend, login, or a drag-and-drop page editor.

## Product Direction

The long-term target is a Codex CLI driven AI branded website reconstruction console and construction package generator. Existing recording, frame extraction, observation, GPT package, and Codex package features remain preserved as support modules for reference-site observation, local complex-effect correction, and construction package preparation.

## Completed Modules

- WPF desktop shell and startup/recent-project workflow.
- New/open project flow using legacy `project.json` and `project-info.json`.
- External browser opening.
- FFmpeg screen recording.
- FFprobe video metadata reading.
- FFmpeg frame extraction and frame indexes.
- Observation Markdown generation.
- ChatGPT/GPT analysis package generation.
- GPT analysis import, material requirement report, and final Codex package generation.
- User intent and selected asset material capture.
- Global hotkey and floating recorder support.
- P0 documentation memory files from the blueprint handoff.
- Local long-term memory index and detailed memory file:
  - `PROJECT_MEMORY_INDEX.md`
  - `docs/project-memory/PROJECT_MEMORY_FULL.md`
- Review-package rules now require every review package and source review zip to include the required memory files.
- This round's P0 foundation types:
  - `ProjectState`
  - `ProjectFileManifest` / `WrbProjectManifest`
  - `ProjectDirectoryV2`
  - `ProjectLock`
  - `ProjectLockService`
  - `SandboxPathPolicy`
  - `LogChannels` / `LoggingPlan`
  - `docs/samples/project.wrbproj.sample.json`

## Missing Modules

- Embedded WebView2 browser and preview.
- Codex CLI execution.
- OpenAI API integration.
- AI task orchestration and failure classification.
- Environment detection UI.
- Real DOM-level observation path.
- Color palette UI and miniature cards.
- Lightweight tuning panel.
- Durable `theme.json`, `content-map.json`, `assets-manifest.json`, and `tune-overrides.css` runtime workflows.
- Version snapshot and rollback implementation.
- Export integrity and secret checks.
- Automated tests.

## Current P0 Architecture Gaps

This round defines several P0 foundation pieces, but does not fully wire them into runtime flows.

Resolved as foundation definitions:

- `project.wrbproj` schema draft model.
- `schemaVersion` and `appVersion` constants.
- `ProjectState` state machine enum.
- `project.lock` model and basic file service.
- Project Directory V2 constants.
- Codex sandbox write policy.
- Layered log channel names.
- Sample `project.wrbproj` file.

Still not implemented as runtime behavior:

- Creating `project.wrbproj` when a legacy project is created.
- Migrating old projects to Project Directory V2.
- Enforcing `ProjectState` in the existing UI flow.
- Enforcing `project.lock` around real generation tasks.
- Persisting `output-site/current` and version snapshots.
- Creating `theme.json`, `content-map.json`, `assets-manifest.json`, or `tune-overrides.css` in the new V2 flow.
- Running Codex in a sandbox.
- Layering existing logs into all new channels.
- WebView2 preview.
- Codex CLI integration.

## This Round Completion Status

Current documentation-only memory task is complete:

- `PROJECT_MEMORY_INDEX.md` was created.
- `docs/project-memory/PROJECT_MEMORY_FULL.md` was created.
- `REVIEW_PACKAGE_RULES.md`, `CODEX_CONSTRUCTION_RULES.md`, `CODEX_PROJECT_MEMORY.md`, `PROJECT_BLUEPRINT.md`, `PROJECT_STATUS.md`, `CURRENT_TASK.md`, `PROJECT_STATE.md`, `CODEX_GLOBAL_RULES.md`, and `README_FOR_CODEX.md` were updated or synchronized to require long-term memory files in every `review_package` and source review zip.
- Business source code has not been modified in this task.
- `dotnet build WebRebuildRecorder.slnx` succeeded with 0 warnings and 0 errors.
- Final source review package target: `E:\Work\WebRebuildRecorder\WebRebuildRecorder_source-review_20260526_172928.zip`.

## Previous Round Completion Status

P0 engineering foundation source and documentation have been added without changing old business workflow wiring.

Verification:

```powershell
dotnet build WebRebuildRecorder.slnx
```

Result: success, 0 warnings, 0 errors.

Startup smoke check: `WebRebuildRecorder.App.exe` stayed alive after 5 seconds and was stopped after verification. No manual UI workflow, recording, frame extraction, or package generation was exercised.

Source review package:

```text
E:\Work\WebRebuildRecorder\WebRebuildRecorder_source-review_20260526_162934.zip
```

## Known Risks

1. P0 models are currently definitions only; old project creation still writes `project.json` / `project-info.json`.
2. No automated tests protect `ProjectLockService` or `SandboxPathPolicy` yet.
3. The repository root still contains generated workspaces and historical zips, so packaging rules must stay strict.
4. `rg.exe` is unavailable in this environment due to Access denied; searches used PowerShell.
5. Old checkpoint and README content contains terminal encoding mojibake.
6. FFmpeg/FFprobe dependencies still affect current recording and frame extraction workflows.
7. Main-window orchestration remains large and risky for later edits.

## Next Recommended Task

Continue with P0/P1 foundation rather than UI expansion:

1. Add tests or a small verification harness for `SandboxPathPolicy` and `ProjectLockService`.
2. Decide whether new projects should write both legacy files and `project.wrbproj`.
3. Add safe V2 directory creation behind existing project creation without breaking legacy compatibility.
4. Add environment detection documentation or service stubs.
5. Keep WebView2, Codex CLI, color UI, and tuning panel out of scope until P0 data foundations are wired and tested.
