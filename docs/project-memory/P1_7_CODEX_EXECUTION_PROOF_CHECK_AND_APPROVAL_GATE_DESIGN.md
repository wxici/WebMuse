# P1.7-0 Codex Execution Proof-Check And Approval-Gate Design

## 1. Purpose

P1.7 is not real website generation.
P1.7 designs the proof-check and approval-gate system that must exist before any real Codex CLI execution can be allowed.

P1.7-0 is design only. It does not implement a proof-check service, approval gate service, execution precondition service, Codex process runner, OpenAI API integration, local model integration, or generated website output.

The goal is to define the safety contract for a later implementation so real execution cannot begin merely because a task package exists.

## 2. Execution Philosophy

Real Codex CLI execution is not a default-enabled capability.

Before every future real execution:

1. The P1.5 readiness gate must pass.
2. The P1.6 dry-run must pass.
3. A safety snapshot must be created.
4. An execution plan must be generated and persisted.
5. A user approval gate must be explicitly approved.
6. A proof-check must pass.
7. Rollback availability must be confirmed.
8. Sandbox write roots and forbidden roots must be revalidated.
9. Failure recovery and manual fallback must be available.
10. The process must be able to degrade to manual construction package export.

Forbidden assumptions:

1. Codex may not write source directories, `.git`, system directories, install directories, credential directories, or arbitrary absolute paths.
2. WebRebuildRecorder may not manage or store user OpenAI, ChatGPT, Codex, GitHub, or third-party AI passwords.
3. Local models may not become the default automatic construction path without passing proof-file, tool-call, build, and sandbox checks.
4. A successful dry-run is not proof that real execution is safe.
5. A user approval cannot be bypassed by AI output, task instructions, config files, or generated package contents.

## 3. Proof-Check Definition

The future proof-check is a small, isolated execution test that proves the selected executor can perform only the allowed file operations before any construction run is allowed.

Planned proof files:

```text
codex-task/proof/proof-request.json
codex-task/proof/proof-instructions.md
codex-task/proof/proof-result.json
codex-task/proof/proof-report.md
codex-task/proof/proof-created-file.txt
```

Proof-check goals:

1. Verify the executor can create a file inside an allowed project directory.
2. Verify the executor cannot write `.git`.
3. Verify the executor cannot write outside the project root.
4. Verify the executor cannot write system directories.
5. Verify the executor cannot access user credential directories.
6. Verify the executor can read the task package.
7. Verify the executor can write to `codex-workspace/`.
8. Verify the executor can write only to a test subdirectory under `output-site/current/` without overwriting production files.
9. Verify executor output can be hash-checked.
10. Verify executor logs can be recorded.

Important boundary: P1.7-0 only designs these files. It does not run Codex CLI and does not create proof runtime artifacts.

Suggested proof result fields:

```text
schemaVersion
projectId
proofRunId
createdAt
completedAt
engineId
executorPath
isOk
createdFileRelativePath
createdFileSha256
deniedWriteChecks
readChecks
logRelativePaths
blockingReasons
warnings
```

## 4. Approval Gate Definition

Approval gates are explicit user decisions required before risky execution phases. They are not model-generated recommendations and cannot be bypassed by AI.

Required gates:

```text
ApprovalRequiredBeforeProofCheck
ApprovalRequiredBeforeRealCodexExecution
ApprovalRequiredBeforeWritingOutputSite
ApprovalRequiredBeforeOverwritingExistingOutput
ApprovalRequiredBeforeExportZip
ApprovalRequiredBeforeUploadingAnything
```

Every approval gate must include:

1. `GateId`
2. `Purpose`
3. `RequiredSummary`
4. `RiskWarning`
5. `UserAction`
6. `Timestamp`
7. `Result`
8. `CannotBeBypassedByAi`
9. `StoredRelativePath`

Suggested approval files:

```text
codex-task/approvals/<approval-id>/approval-request.json
codex-task/approvals/<approval-id>/approval-result.json
```

Approval result values:

```text
Pending
Approved
Rejected
Expired
Cancelled
Superseded
```

Approval rules:

1. Approval must be tied to the exact task package hash and execution plan hash.
2. If the task package, construction context, instructions, allowed write roots, or execution plan changes, the approval becomes invalid.
3. Approval cannot be inferred from previous runs.
4. Approval must be stored as a project-relative file.
5. Approval files must not contain secrets, account tokens, cookies, local absolute paths, or credential paths.

## 5. Rollback Confirmation

Before real execution, rollback must be confirmed as available.

Required checks:

1. Safety snapshot creation succeeded.
2. Snapshot manifest is readable.
3. Snapshot hash validation is available.
4. Restore plan can be generated.
5. Restore plan does not write dangerous paths.
6. Rollback report can be written.
7. User can see a clear `rollback available` state.
8. Real execution is blocked when rollback is unavailable.

Suggested rollback confirmation record:

```text
codex-task/execution/<execution-id>/rollback-confirmation.json
```

Suggested fields:

```text
schemaVersion
projectId
executionId
safetySnapshotId
snapshotManifestRelativePath
restorePlanRelativePath
isRollbackAvailable
checkedAt
blockingReasons
warnings
```

## 6. Execution Preconditions

All conditions must pass before real Codex CLI execution:

```text
P1.5 PreCodexDryRun readiness passed
P1.6 dry-run passed
proof-check passed
approval gate approved
safety snapshot created
allowed write roots verified
forbidden roots verified
secret scan clean
local path scan clean
output-site/current safe
codex-workspace safe
logs writable
task package hash stable
context freshness valid
manual export fallback available
```

Any failed condition blocks real execution.

Suggested precondition result:

```text
codex-task/execution/<execution-id>/execution-preconditions.json
```

Each item should include:

```text
key
severity
status
message
relativePath
blocksExecution
failureCategory
```

## 7. Execution State Machine

P1.7-0 defines a future execution state machine only. It does not modify source enums in this round.

Suggested states:

```text
Created
ReadinessChecking
ReadinessBlocked
DryRunReady
ProofCheckPendingApproval
ProofCheckRunning
ProofCheckFailed
ProofCheckPassed
ExecutionPendingApproval
ExecutionRunning
ExecutionSucceeded
ExecutionFailed
ExecutionCancelled
RollbackPending
RollbackRunning
RollbackSucceeded
RollbackFailed
ManualExportFallback
```

Allowed transitions:

```text
Created -> ReadinessChecking
ReadinessChecking -> ReadinessBlocked
ReadinessChecking -> DryRunReady
ReadinessBlocked -> ManualExportFallback
DryRunReady -> ProofCheckPendingApproval
ProofCheckPendingApproval -> ProofCheckRunning
ProofCheckPendingApproval -> ManualExportFallback
ProofCheckRunning -> ProofCheckPassed
ProofCheckRunning -> ProofCheckFailed
ProofCheckRunning -> ExecutionCancelled
ProofCheckFailed -> ManualExportFallback
ProofCheckFailed -> ProofCheckPendingApproval
ProofCheckPassed -> ExecutionPendingApproval
ExecutionPendingApproval -> ExecutionRunning
ExecutionPendingApproval -> ManualExportFallback
ExecutionRunning -> ExecutionSucceeded
ExecutionRunning -> ExecutionFailed
ExecutionRunning -> ExecutionCancelled
ExecutionFailed -> RollbackPending
ExecutionFailed -> ManualExportFallback
ExecutionCancelled -> RollbackPending
RollbackPending -> RollbackRunning
RollbackRunning -> RollbackSucceeded
RollbackRunning -> RollbackFailed
RollbackSucceeded -> ManualExportFallback
RollbackFailed -> ManualExportFallback
```

Terminal or guarded states:

1. `ExecutionSucceeded` may move only to validation/export flows in later design.
2. `RollbackFailed` must not silently retry without user awareness.
3. `ManualExportFallback` does not write generated website output.
4. A completed approval must not move a changed execution plan back to `ExecutionRunning`.

## 8. Failure Recovery Rules

Failure categories to support:

1. `MissingInput`
2. `SandboxViolation`
3. `SecretDetected`
4. `EnvironmentMissing`
5. `CodexUnavailable`
6. `NetworkUnavailable`
7. `QuotaExceeded`
8. `AuthenticationFailed`
9. `ProofCheckFailed`
10. `OutputMissing`
11. `BuildFailed`
12. `UserCancelled`
13. `Timeout`
14. `UnknownInternalError`

Each failure policy must define:

```text
Category
Blocks execution?
Can retry?
Requires user action?
Fallback
Log channel
Report path
```

Draft policy table:

| Category | Blocks execution | Can retry | Requires user action | Fallback | Log channel | Report path |
| --- | --- | --- | --- | --- | --- | --- |
| MissingInput | Yes | Yes, after input repair | Yes | Manual export if package can be built | project | `codex-task/execution/<id>/failure-report.json` |
| SandboxViolation | Yes | No until policy/input fixed | Yes | Manual export | security | `codex-task/execution/<id>/failure-report.json` |
| SecretDetected | Yes | No until cleaned | Yes | None until cleaned | security | `codex-task/execution/<id>/failure-report.json` |
| EnvironmentMissing | Yes | Yes, after setup | Yes | Manual export | project | `codex-task/execution/<id>/failure-report.json` |
| CodexUnavailable | Yes | Yes, after Codex repair | Yes | Manual export | codex-task | `codex-task/execution/<id>/failure-report.json` |
| NetworkUnavailable | Yes for cloud modes | Yes | Yes | Manual export or local auxiliary mode | codex-task | `codex-task/execution/<id>/failure-report.json` |
| QuotaExceeded | Yes | Later only | Yes | Manual export | codex-task | `codex-task/execution/<id>/failure-report.json` |
| AuthenticationFailed | Yes | Yes, after official login | Yes | Manual export | security | `codex-task/execution/<id>/failure-report.json` |
| ProofCheckFailed | Yes | Yes, after repair | Yes | Manual export | security | `codex-task/execution/<id>/failure-report.json` |
| OutputMissing | Yes | Yes, after inspection | Maybe | Rollback then manual export | validation | `codex-task/execution/<id>/failure-report.json` |
| BuildFailed | Yes for export | Yes | Maybe | Rollback then manual export | validation | `codex-task/execution/<id>/failure-report.json` |
| UserCancelled | Yes | Yes, with new approval | Yes | Rollback or manual export | project | `codex-task/execution/<id>/failure-report.json` |
| Timeout | Yes | Yes, with fresh plan | Maybe | Rollback then manual export | codex-task | `codex-task/execution/<id>/failure-report.json` |
| UnknownInternalError | Yes | No until reviewed | Yes | Rollback then manual export | error | `codex-task/execution/<id>/failure-report.json` |

## 9. Manual Construction Package Export Fallback

Manual construction package export is the required fallback when real execution is unavailable or blocked.

Fallback rules:

1. If Codex is unavailable, the user can still export the construction package.
2. The user can give the package to external Codex, ChatGPT, Claude, Cursor, or a human developer.
3. Fallback does not write `output-site/current/`.
4. Fallback does not require OpenAI API.
5. Fallback does not require Codex CLI.
6. Fallback must preserve project-relative paths and avoid secrets/local absolute paths.

Fallback package should include:

```text
codex-task/construction-package.json
codex-task/task-package.json
codex-task/instructions.md
codex-task/context/
input/assets/assets-manifest.json
theme/theme.json
maps/content-map.json
observation/observation-package.json
codex-task/readiness/readiness-report.json
codex-task/readiness/readiness-report.md
codex-task/dry-runs/<dry-run-id>/dry-run-report.md
```

The fallback may be prepared as a manifest and file list first. Zip/package creation remains a later explicit task.

## 10. P1.7 Implementation Split

Recommended future implementation rounds:

```text
P1.7.1 Proof-check package models and manifest
P1.7.2 Approval gate models and persistence
P1.7.3 Execution precondition service
P1.7.4 Failure recovery policy service
P1.7.5 Manual construction package export fallback scaffold
P1.7.6 Proof-check dry-run only
```

Boundaries for the split:

1. P1.7.1 still does not execute Codex CLI.
2. P1.7.2 still does not approve real execution automatically.
3. P1.7.3 still blocks real execution when any precondition fails.
4. P1.7.4 only classifies and routes failures.
5. P1.7.5 exports or describes packages only; it does not generate websites.
6. P1.7.6 may simulate proof-check behavior only; real proof execution requires a later explicit task.

Full real Codex execution should not begin until proof-check, approval gates, rollback confirmation, sandbox validation, execution preconditions, failure recovery, and manual export fallback are all implemented and verified.

Recommended Codex model for future full implementation: `gpt-5.5 xhigh`.
Reason: P1.7 implementation will touch security, sandbox, state transitions, rollback readiness, and execution orchestration.
