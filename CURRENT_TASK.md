# Current Task

## P1.8-0 Early Alpha Validation Probe / Local Pipeline Probe

Status: synchronized to WebMuse as public-safe source and documentation after prototype verification and push.

P1.8-0 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

P1.8-0 is an early alpha validation probe.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

The purpose is to prove the P0/P1 foundation can be composed into a local explainable pipeline report before continuing deeper P1.7.4 failure recovery work.

P1.7.4 Failure recovery policy service is postponed, not cancelled.

## Synced Source

- `WebRebuildRecorder.App/Core/ProjectSystem/AlphaValidationProbe.cs`
- `WebRebuildRecorder.App/Core/ProjectSystem/AlphaValidationProbeService.cs`
- `WebRebuildRecorder.FoundationSelfTest/Program.cs`

## Synced Documentation

- `CURRENT_TASK.md`
- `PROJECT_STATUS.md`
- `PROJECT_ROADMAP.md`
- `docs/project-memory/PROJECT_MEMORY_FULL.md`

## Runtime Artifacts

Alpha validation reports are runtime artifacts under:

```text
codex-task/alpha-validation/<probe-id>/
  alpha-validation-report.json
  alpha-validation-report.md
```

They are ignored and must not be committed.

## Next Recommended Round

P1.7.4-A Failure recovery models + static policy table.

P1.7.4-A still must not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.
