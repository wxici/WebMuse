# README FOR CODEX

## Project Summary

WebMuse, formerly WebRebuildRecorder, is an early-alpha open-source Windows desktop workbench for safer Codex-assisted reference-style website reconstruction workflows.

It helps organize observation packages, construction packages, sandbox validation, dry-run checks, approval gates, rollback readiness, and manual fallback workflows before real AI execution.

## Current Round

Current task:

```text
OSS Readiness Round 0: Public Repository Foundation
```

This round is limited to public repository initialization, minimal source migration, OSS documentation, CI/build setup, safety boundary documentation, migration audit, and verification.

## Run Method

Expected commands after source migration:

```powershell
dotnet restore WebRebuildRecorder.slnx
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.App/WebRebuildRecorder.App.csproj
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest/WebRebuildRecorder.FoundationSelfTest.csproj
```

The application is Windows-specific and requires the .NET 8 SDK with WPF desktop support.

## Directory Structure

Expected public repository paths include:

- `WebRebuildRecorder.App/`
- `WebRebuildRecorder.FoundationSelfTest/`
- `docs/`
- `docs/project-memory/`
- `docs/case-studies/`
- `.github/workflows/`
- `.github/ISSUE_TEMPLATE/`

## Existing Behavior To Preserve

When source is migrated, preserve existing behavior unless a future task explicitly authorizes product changes:

- legacy project creation/opening compatibility
- recording and frame extraction behavior
- observation package generation
- GPT/Codex package preparation
- sandbox and project manifest foundation checks
- foundation self-test harness

## Current Project Special Rules

- Keep public branding as WebMuse.
- Keep internal `WebRebuildRecorder` source names for now.
- Do not migrate generated workspaces, recordings, screenshots, extracted frames, logs, zips, secrets, customer materials, or local configuration.
- Do not implement real AI execution or website generation in this round.
- Record migration findings in `docs/OSS_MIGRATION_AUDIT.md`.

