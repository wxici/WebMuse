# P1.6 Dry-Run Orchestrator Implementation Plan

## P1.6 Goal

P1.6 should add a dry-run orchestrator that proves WebRebuildRecorder can prepare, validate, simulate, record, and report a future Codex construction run without starting Codex CLI.

The goal is to bridge P1.5 readiness gating into a safe orchestration layer:

1. verify readiness;
2. load the construction and task packages;
3. build a dry-run task plan;
4. simulate allowed write targets;
5. create or update a Codex task run record;
6. write dry-run reports and logs;
7. classify blockers;
8. stop before external execution.

P1.6 is a dry-run confidence layer, not a generation layer.

## P1.6 Explicit Non-Goals

P1.6 must not:

- execute Codex CLI;
- call OpenAI API;
- call Ollama or LM Studio;
- implement AI engine integration;
- generate a website;
- write generated HTML/CSS/JS output;
- modify the WPF UI;
- integrate WebView2;
- implement Reference Portal UI;
- implement Design Context Library;
- implement Frontend Effect Recipe Library;
- implement ProposalPreview;
- modify recording, frame extraction, mouse automation, or browser automation;
- generate `output-site.zip`;
- bypass P1.5 readiness checks;
- write outside project-safe dry-run report/log areas.

## Planned New Files

Recommended implementation files for the full P1.6 round:

- `WebRebuildRecorder.App/Core/ProjectSystem/CodexDryRunOrchestrator.cs`
- `WebRebuildRecorder.App/Core/ProjectSystem/CodexDryRunOrchestratorService.cs`

Recommended generated project files:

- `codex-task/dry-run/dry-run-plan.json`
- `codex-task/dry-run/dry-run-report.json`
- `codex-task/dry-run/dry-run-report.md`

No P1.6-0 source files are created in this planning round.

## Planned Modified Files

Recommended implementation-round modifications:

- `WebRebuildRecorder.FoundationSelfTest/Program.cs`
- `CURRENT_TASK.md`
- `CODEX_CHECKPOINT.md`
- `PROJECT_STATUS.md`
- `PROJECT_MEMORY_INDEX.md`
- `docs/project-memory/PROJECT_MEMORY_FULL.md`
- `review_package/*.md`

P1.6-0 only updates planning/status/review Markdown and does not modify source or FoundationSelfTest.

## Planned New Models

Suggested model names for full P1.6:

```csharp
public sealed class CodexDryRunPlan
{
    public string SchemaVersion { get; init; } = "";
    public string ProjectId { get; set; } = "";
    public string TaskPackageId { get; set; } = "";
    public string RunId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public List<CodexDryRunStep> Steps { get; set; } = new();
    public List<CodexDryRunWriteTarget> PlannedWriteTargets { get; set; } = new();
    public List<CodexDryRunInput> RequiredInputs { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class CodexDryRunStep
{
    public string StepId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public string? BlockingReason { get; set; }
}

public sealed class CodexDryRunWriteTarget
{
    public string RelativePath { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool IsAllowed { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class CodexDryRunInput
{
    public string RelativePath { get; set; } = "";
    public string Role { get; set; } = "";
    public bool Exists { get; set; }
    public bool Required { get; set; }
}

public sealed class CodexDryRunReport
{
    public string SchemaVersion { get; init; } = "";
    public string ProjectId { get; set; } = "";
    public string TaskPackageId { get; set; } = "";
    public string RunId { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public bool IsOk { get; set; }
    public List<CodexDryRunStep> Steps { get; set; } = new();
    public List<CodexDryRunWriteTarget> PlannedWriteTargets { get; set; } = new();
    public List<CodexTaskFailureItem> Failures { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

All enum-like fields should serialize as stable strings or stable text. Use `WrbJsonOptions.Default`.

## Planned New Service

Recommended service:

```csharp
public sealed class CodexDryRunOrchestratorService
{
    public Task<CodexDryRunReport> RunAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}
```

Expected responsibilities:

1. run P1.5 `ConstructionReadinessGateService` in `PreCodexDryRun`;
2. stop on blocking readiness failures;
3. load `codex-task/task-package.json`;
4. load `codex-task/construction-package.json`;
5. verify `codex-task/instructions.md`;
6. verify `codex-task/context/package-index.json`;
7. create or load a `CodexTaskRunRecord`;
8. mark the run `Queued`, then `Running`, then dry-run `Succeeded` or `Failed`;
9. simulate read order and write targets;
10. validate each planned write target with `SandboxPathPolicy` or equivalent helpers;
11. write JSON and Markdown dry-run reports;
12. write project/security/codex-task logs through existing log services;
13. avoid external process execution.

Do not split this into a real process runner in P1.6. Process runner work belongs to a later phase after the dry-run contract is stable.

## FoundationSelfTest Plan

Full P1.6 should extend existing FoundationSelfTest only after source implementation begins.

Planned checks:

- P0/P1/P1.1/P1.2/P1.3/P1.4/P1.5 still pass.
- Dry-run succeeds for a prepared project with readiness passing.
- Dry-run creates `dry-run-plan.json`.
- Dry-run creates `dry-run-report.json`.
- Dry-run creates `dry-run-report.md`.
- Dry-run creates or updates a task run record.
- Run transition covers `Created -> Queued -> Running -> Succeeded`.
- Missing task package becomes failure.
- Missing instructions becomes failure.
- Blocking readiness result stops dry-run.
- Absolute planned write path is rejected.
- `..` traversal planned write path is rejected.
- `.git` planned write path is rejected.
- Empty allowed write roots fail.
- Forbidden roots remain enforced.
- Reports contain no local absolute paths, secrets, token markers, or credential directory references.
- No Codex CLI process is started.
- No OpenAI API is called.
- No website output is generated.

## Dry-Run Report Structure

Recommended generated files:

```text
codex-task/dry-run/dry-run-plan.json
codex-task/dry-run/dry-run-report.json
codex-task/dry-run/dry-run-report.md
```

Recommended JSON sections:

- schemaVersion;
- projectId;
- taskPackageId;
- runId;
- startedAt/completedAt;
- readiness summary;
- input file checks;
- planned read order;
- planned write targets;
- sandbox validation results;
- run-record transition summary;
- warnings;
- failures;
- final `isOk`.

Recommended Markdown sections:

- Summary;
- Readiness;
- Inputs;
- Planned Steps;
- Planned Writes;
- Sandbox Checks;
- Run Record;
- Failures;
- Non-Execution Confirmation.

## Failure Scenarios

The full P1.6 implementation should classify at least:

- readiness gate blocking failure;
- missing `project.wrbproj`;
- missing construction package;
- missing task package;
- missing instructions;
- missing context package index;
- missing required context file;
- stale or mismatched package-index hash;
- secret/local path detected in planned context;
- allowed write roots empty;
- forbidden roots empty;
- write target outside project root;
- absolute write target;
- traversal write target;
- forbidden directory write target;
- `.git` write target;
- output target missing or not writable;
- run-record transition rejected;
- internal report write failure.

Prefer existing `TaskFailureCategory` values:

- `ValidationError`
- `MissingInput`
- `SandboxViolation`
- `SecretDetected`
- `OutputMissing`
- `InternalError`

Do not add duplicate failure enums unless the existing category set is demonstrably insufficient.

## Sandbox Checkpoints

P1.6 dry-run must check:

- project root is valid and inside the active project;
- every input path is project-relative;
- every planned output path is project-relative;
- planned writes stay under `codex-workspace/` or `output-site/current/`;
- report writes stay under `codex-task/dry-run/`;
- log writes use existing log service/channels;
- no planned write targets `.git`, `.ssh`, `.codex`, `.openai`, `.vs`, `bin`, `obj`, system directories, install directories, or user credential directories;
- no absolute Windows or Unix paths in package references;
- no `..` traversal;
- no path separator confusion;
- no direct source repository writes;
- no large generated artifacts, zip files, videos, screenshots, or logs are staged for Git.

## Run Record Design

Dry-run should use the existing `CodexTaskRunService` instead of creating a parallel run-record system.

Recommended flow:

```text
Create run if needed
-> MarkQueuedAsync
-> MarkRunningAsync
-> MarkSucceededAsync if dry-run validates
```

Failure flow:

```text
Create run if needed
-> MarkQueuedAsync
-> MarkRunningAsync
-> MarkFailedAsync with classified failures
```

Dry-run is not real Codex execution, so `Succeeded` means "orchestration dry-run succeeded", not "website construction succeeded".

The run record should include the dry-run report relative paths in `OutputRelativePaths`.

## Log Design

Use existing logging services and channels. Do not create a parallel logging system.

Recommended log events:

- dry-run started;
- readiness mode used;
- readiness blocked or passed;
- task package loaded;
- construction package loaded;
- input checks completed;
- planned write validation completed;
- run record created/updated;
- dry-run report written;
- dry-run succeeded/failed.

Recommended channels:

- `logs/project.log`
- `logs/security.log`
- `logs/codex-task.log`

Security log should receive sandbox violations, forbidden path attempts, secret detections, and local-path detections.

## Recommended Model For Full P1.6

Full P1.6 should use `gpt-5.5 xhigh`.

Reason: the full implementation will touch orchestration, run-record state transitions, readiness integration, sandbox safety, report generation, and failure classification. A bad edit could create a false sense of execution safety before real Codex integration.

This P1.6-0 planning round can remain documentation-only and does not need high-risk code work.
