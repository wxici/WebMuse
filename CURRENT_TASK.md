# Current Active Task

## Task Name

OSS Readiness Round 0: Public Repository Foundation.

## Task Type

Public OSS repository initialization, safe source migration, documentation, CI setup, verification, migration audit, commit, and push.

## Status

In progress on 2026-06-01.

## Goal

Make `wxici/WebMuse` a clean, credible, auditable public OSS repository for the early-alpha WebMuse project.

This round is not product feature development. It does not implement real Codex execution, OpenAI API calls, website generation, WebView2 preview, tuning UI, UI redesign, or namespace-wide rename.

## Allowed Changes

1. Clone and verify `wxici/WebMuse`.
2. Migrate only allowed source and project memory files from `E:\GitHub\codex\WebRebuildRecorder`.
3. Keep internal `WebRebuildRecorder` names unchanged.
4. Create public OSS docs:
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
5. Create GitHub workflow and issue templates.
6. Run restore, build, and FoundationSelfTest.
7. Audit staged files for generated artifacts and sensitive content.
8. Commit and push to `main` if verification passes.

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

## Required Verification

```powershell
Set-Location "E:\GitHub\WebMuse"
git status
dotnet restore WebRebuildRecorder.slnx
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

## Completion Criteria

1. Required OSS docs exist.
2. Allowed source files are migrated.
3. Forbidden/generated/sensitive file categories are not migrated.
4. Build passes.
5. FoundationSelfTest passes.
6. Git staged diff contains no generated artifacts, customer/private materials, local configuration, keys, tokens, or login files.
7. Commit `Initialize WebMuse OSS repository foundation` is pushed to `origin/main`.
8. Final OSS Readiness Round 0 report is returned.

## Next Recommended Tasks After Completion

1. Create 6-10 real GitHub Issues.
2. Create a `v0.1.0-alpha` tag/release.
3. Add a README architecture diagram.
4. Finalize the under-500-character OSS application answer.
5. Ask for an external review of the public repository.
6. Decide whether to submit the Codex for OSS application.

