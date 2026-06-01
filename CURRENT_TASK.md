# Current Active Task

## Task Name

OSS Readiness Round 0: Public Repository Foundation.

## Task Type

Public OSS repository initialization, safe source migration, documentation, CI setup, verification, migration audit, commit, and push.

## Status

Completed on 2026-06-01.

## Completed Commits

```text
0a59fa5 Initialize WebMuse OSS repository foundation
fb5ea37 Fix WebMuse CI self-test configuration
```

Latest full commit:

```text
fb5ea3776b42721a5ff483ad49368dbac9f149e5
```

## Completed Result

OSS Readiness Round 0 established `wxici/WebMuse` as a public OSS repository foundation.

Completed:

1. Verified `wxici/WebMuse` as a public GitHub repository with default branch `main`.
2. Migrated allowed source files and project memory from the historical `WebRebuildRecorder` project.
3. Kept internal `WebRebuildRecorder` source names unchanged.
4. Created public OSS documentation:
   - `README.md`
   - `LICENSE`
   - `SECURITY.md`
   - `CONTRIBUTING.md`
   - `ROADMAP.md`
   - `CHANGELOG.md`
   - `docs/ARCHITECTURE.md`
   - `docs/OPEN_SOURCE_APPLICATION_NOTES.md`
   - `docs/OSS_MIGRATION_AUDIT.md`
   - `docs/case-studies/manual-poc-history.md`
5. Created GitHub Actions workflows and issue templates.
6. Skipped forbidden generated, sensitive, binary, customer/private, recording, screenshot, frame, zip, and local configuration files.
7. Verified local restore, Release build, and FoundationSelfTest.
8. Fixed standalone repository workflow path checks.
9. Fixed GitHub Actions Release self-test configuration.
10. Confirmed latest remote GitHub Actions runs are green for:
    - `build`
    - `webrebuildrecorder-foundation`

## Explicitly Out Of Scope

- real Codex CLI execution
- OpenAI API calls
- Ollama calls
- LM Studio calls
- website generation
- WebView2 integration
- UI redesign
- tuning panel
- Reference Portal implementation
- Design Context Library implementation
- ProposalPreview / SitePitcher implementation
- recording behavior changes
- frame extraction changes
- mouse automation changes
- page editor
- drag-and-drop editor
- automatic model downloader
- installer or dependency bootstrapper
- customer acquisition features
- commercial customer workflow
- namespace-wide rename
- solution-wide rename
- mass refactor

## Verification Result

Local verification passed:

```powershell
dotnet build WebRebuildRecorder.slnx --configuration Release --no-restore
dotnet run --configuration Release --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Remote verification passed:

```text
GitHub Actions build: green
GitHub Actions webrebuildrecorder-foundation: green
```

Earlier red workflow runs belong to the initial `0a59fa5` commit and are historical only.

## Next Recommended Tasks

1. Create 6-10 GitHub issues.
2. Add a README architecture diagram.
3. Prepare `v0.1.0-alpha` after review.
4. Finalize the Codex for OSS application answer.

