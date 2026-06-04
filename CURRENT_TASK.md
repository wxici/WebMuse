# Current Task

## P1.7.3 Execution Precondition Service

Status: synchronized to WebMuse as public-safe source and documentation after prototype verification and push.

P1.7.3 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

This round implements execution precondition aggregation only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

## Required Repository Workflow

1. Implement and verify in `wxici/codex/WebRebuildRecorder`.
2. Commit and push prototype changes from `E:\GitHub\codex`.
3. Synchronize only public-safe source and documentation to `wxici/WebMuse`.

`WebMuse` remains an OSS-safe result extraction repository, not the primary construction worktree.

## Scope

Allowed:

- Add `ExecutionPrecondition.cs`.
- Add `ExecutionPreconditionService.cs`.
- Aggregate existing P1.5 readiness, P1.6 dry-run, P1.7.1 proof-check package, P1.7.2 approval gate, P1.4 rollback/snapshot, sandbox, secret/local-path scan, output safety, task package hash stability, context freshness, and manual fallback availability.
- Write runtime reports under `codex-task/execution/<execution-id>/execution-preconditions.json` and `.md`.
- Extend `WebRebuildRecorder.FoundationSelfTest`.
- Update project memory/status and `review_package`.
- Push prototype first, then sync public-safe source/docs to WebMuse.

Forbidden:

- Do not execute Codex CLI.
- Do not run any `codex` command.
- Do not call OpenAI API.
- Do not call Ollama, LM Studio, local model engines, or remote AI services.
- Do not generate a website.
- Do not write `output-site/current/index.html`.
- Do not create real proof execution result files.
- Do not implement a real Codex runner.
- Do not implement failure recovery policy service.
- Do not implement a manual export fallback writer.
- Do not export a construction package.
- Do not change UI, WebView2, tuning UI, Reference Portal, Design Context Library, Frontend Effect Recipe Library, or ProposalPreview.
- Do not commit runtime artifacts, logs, dry-runs, proof files, approval runtime files, execution reports, zip/bin/obj, materials, tokens, keys, cookies, or secrets.

## Expected Result

The normal P1.7.3 report should block real execution because real proof execution and the real Codex runner are intentionally not implemented yet.

`AllowsRealCodexExecution` must remain `false` unless a future explicitly authorized round implements and verifies all required real-execution gates.

## Completed Result

Implemented:

- `ExecutionPrecondition.cs`.
- `ExecutionPreconditionService.cs`.
- `EvaluateAsync(...)`.
- `LoadLatestAsync(...)`.
- Runtime report persistence under `codex-task/execution/<execution-id>/execution-preconditions.json` and `.md`.
- Aggregation of readiness, dry-run, proof package validation, missing proof execution, approval state, rollback/safety snapshot, sandbox roots, secret/local path scan, output-site safety, codex-workspace safety, logs writability, task package hash stability, context freshness, manual fallback availability, and the P1.7.3 non-execution boundary.
- FoundationSelfTest P1.7.3 coverage.

Verification before transport:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors. FoundationSelfTest passed and printed the five required P1.7.3 verification lines.

## Next Recommended Round

P1.7.4 Failure recovery policy service.

P1.7.4 still must not execute Codex CLI or generate websites.
