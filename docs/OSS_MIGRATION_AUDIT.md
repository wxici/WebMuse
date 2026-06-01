# OSS Migration Audit

## Source

- Historical source repository: `https://github.com/wxici/codex/tree/master/WebRebuildRecorder`
- Local source path used for this round: `E:\GitHub\codex\WebRebuildRecorder`
- Source was treated as read-only.

## Target

- Target repository: `https://github.com/wxici/WebMuse`
- Local target path: `E:\GitHub\WebMuse`
- Verified repository metadata before migration:
  - name: `wxici/WebMuse`
  - default branch: `main`
  - visibility: `PUBLIC`

## Files migrated

- `WebRebuildRecorder.slnx`
- `WebRebuildRecorder.App/`
- `WebRebuildRecorder.FoundationSelfTest/`
- `PROJECT_BLUEPRINT.md`
- `PROJECT_ROADMAP.md`
- `PROJECT_STATUS.md`
- `CURRENT_TASK.md`
- `CODEX_CONSTRUCTION_RULES.md`
- `REVIEW_PACKAGE_RULES.md`
- `docs/project-memory/`

`README_FOR_CODEX.md` was created as a WebMuse public-repository version rather than copied verbatim from the historical source.

## Files added

- `README.md`
- `LICENSE`
- `SECURITY.md`
- `CONTRIBUTING.md`
- `ROADMAP.md`
- `CHANGELOG.md`
- `.gitignore`
- `CODEX_GLOBAL_RULES.md`
- `README_FOR_CODEX.md`
- `PROJECT_STATE.md`
- `CODEX_CHECKPOINT.md`
- `docs/ARCHITECTURE.md`
- `docs/OPEN_SOURCE_APPLICATION_NOTES.md`
- `docs/OSS_MIGRATION_AUDIT.md`
- `docs/case-studies/manual-poc-history.md`
- `.github/workflows/build.yml`
- `.github/workflows/webrebuildrecorder-foundation.yml`
- `.github/ISSUE_TEMPLATE/bug_report.md`
- `.github/ISSUE_TEMPLATE/feature_request.md`

## Files intentionally not migrated

- generated workspaces
- logs
- review package zips
- recordings
- screenshots
- extracted frames
- output-site/current
- codex-task runtime outputs
- customer/private materials
- secrets
- local machine configuration
- `.vs/`
- `bin/`
- `obj/`
- historical zip archives
- generated media files
- `ffmpeg.exe`
- `ffprobe.exe`

## Sensitive-file audit

Pre-commit scan completed before and after staging. No unignored or staged files matched the forbidden archive, video, credential-file, runtime-output, customer-material, or FFmpeg binary filename patterns.

Initial filtered migration skipped:

- build outputs under `bin/`
- intermediate outputs under `obj/`
- FFmpeg local binary files under `WebRebuildRecorder.App/Tools/ffmpeg/`
- archive/media/credential-like filename patterns

Build verification generated `bin/` and `obj/` directories. They are ignored by `.gitignore` and were not included in the unignored file list.

Content-pattern scans found only expected documentation text and self-test sentinel strings, such as fake `OPENAI_API_KEY=sk-test`, `token=abc123`, and local path examples used to verify sanitizers. No real credential pattern was found.

## Build result

Passed.

Commands run from `E:\GitHub\WebMuse`:

```powershell
dotnet restore WebRebuildRecorder.slnx
dotnet build WebRebuildRecorder.slnx
```

Result:

```text
restore passed
build passed
0 warnings
0 errors
```

Two migration-only CI fixes were required before final verification:

- the foundation workflow lookup now checks the current repository root first and falls back to the historical parent-root layout;
- GitHub Actions self-test steps now run with `--configuration Release` because the workflows build Release output before using `--no-build`.

## FoundationSelfTest result

Passed.

Command run from `E:\GitHub\WebMuse`:

```powershell
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result:

```text
Foundation self-check passed.
```

The first self-test attempt exceeded the short command timeout. A direct rerun showed a migration path issue: the harness and readiness gate were still looking for `.github/workflows/webrebuildrecorder-foundation.yml` in the parent of the repository root. The fix was limited to workflow path resolution.

After the first push, remote GitHub Actions failed because Release build output was followed by a default Debug `dotnet run --no-build`. The workflow fix was verified locally with:

```powershell
dotnet build WebRebuildRecorder.slnx --configuration Release --no-restore
dotnet run --configuration Release --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Both passed.

## Known risks

- WebMuse is still early alpha.
- Real Codex CLI execution is not enabled.
- OpenAI API calls are not enabled.
- WebView2 preview is not complete.
- Website generation is not complete.
- Tuning panel is not complete.
- Internal source names still use `WebRebuildRecorder`.
- The manual PoC history is text-only and does not publish third-party reference materials.

## Next steps

1. Run `dotnet restore WebRebuildRecorder.slnx`.
2. Run `dotnet build WebRebuildRecorder.slnx`.
3. Run `dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj`.
4. Run a pre-commit audit for generated artifacts and sensitive patterns.
5. Commit and push if verification passes.
