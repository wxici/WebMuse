# Current Active Task

## Task Name

P1.7.2 Approval gate models and persistence.

## Task Type

Prototype-first foundation implementation round. This is not a UI, WebView2, tuning, AI execution, or website generation round.

## Status

P1.7.2 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

The previous P1.7.1 prototype backfill was completed and pushed as:

```text
31afe67 Backfill P1.7.1 proof-check package into WebRebuildRecorder
```

## Repository Relationship

Primary construction source / prototype repository:

```text
wxici/codex/WebRebuildRecorder
```

Public OSS presentation repository:

```text
wxici/WebMuse
```

P1.7.2 must be implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized to WebMuse as public-safe source and documentation only.

## Allowed Changes

1. Add approval gate models under `WebRebuildRecorder.App/Core/ProjectSystem/`.
2. Add approval gate persistence and validation service under `WebRebuildRecorder.App/Core/ProjectSystem/`.
3. Persist approval request/result/report files under project-relative `codex-task/approvals/<approval-id>/`.
4. Bind approvals to `codex-task/task-package.json` and `codex-task/instructions.md` hashes, plus optional dry-run/proof hashes when present.
5. Extend `WebRebuildRecorder.FoundationSelfTest` to verify approval models, persistence, hash invalidation, state transitions, sanitization, and non-execution boundaries.
6. Tighten `.gitignore` for approval runtime artifacts.
7. Update project status, memory, roadmap, checkpoint, and review package Markdown files.
8. Build, run FoundationSelfTest, commit, and push `wxici/codex`.
9. After prototype push succeeds, synchronize only public-safe source/self-test/status documentation to `wxici/WebMuse`.

## Explicitly Out Of Scope

- Real Codex CLI execution.
- Running any `codex` command.
- OpenAI API calls.
- Ollama, LM Studio, or other local model calls.
- Website generation.
- Writing `output-site/current/index.html`.
- Creating proof execution result files.
- Execution precondition service implementation.
- Failure recovery policy implementation.
- Manual export fallback implementation.
- UI changes.
- WebView2 changes.
- Tuning UI changes.
- Reference Portal implementation.
- Design Context Library implementation.
- Frontend Effect Recipe Library implementation.
- ProposalPreview implementation.
- Runtime artifacts, logs, dry-runs, proof runtime files, approval runtime files, zips, bin/obj, materials, tokens, keys, cookies, or credentials.

## Required Verification

```powershell
Set-Location "E:\GitHub\codex\WebRebuildRecorder"
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

If the full self-test times out without a failure result, rerun:

```powershell
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

## Completion Criteria

1. `ApprovalGate.cs` exists and defines schema constants, gate/decision enums, request/result/binding/validation models.
2. `ApprovalGateService.cs` exists and supports create/load/approve/reject/cancel/expire/supersede/validate.
3. Approval files use `WrbJsonOptions.Default` and serialize enum values as strings.
4. Approval files are project-relative and kept under `codex-task/approvals/<approval-id>/`.
5. Approval creation fails when required task package or instructions files are missing.
6. Approval approval fails when bound hashes are stale.
7. Illegal state transitions throw `InvalidOperationException`.
8. Validation reports detect stale bindings, unsafe paths, local absolute paths, and sensitive markers.
9. FoundationSelfTest prints the required P1.7.2 verification lines.
10. Build passes.
11. FoundationSelfTest passes.
12. No approval runtime artifacts, generated website output, logs, zips, bin/obj, or secrets are committed.
13. Prototype commit/push succeeds.
14. WebMuse receives only public-safe source/self-test/status documentation sync after prototype push.

## Boundary Statement

P1.7.2 implements approval gate models and persistence only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

## Next Recommended Task After Completion

P1.7.3 Execution precondition service.

P1.7.3 still must not execute Codex CLI, call OpenAI API, call local model engines, or generate websites.
