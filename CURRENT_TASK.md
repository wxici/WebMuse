# Current Active Task

## Task Name

P1.7.1 Proof-check Package Models and Manifest.

## Task Type

Foundation implementation round. Non-executing proof-check package models, persistence, validation, and self-test coverage.

## Status

Completed in WebMuse source first, then backfilled to the primary prototype repository.

Backfill status:

- WebMuse original commit: `2d2cb82a02992103d292f16bb80d25ae1a2a94b9`.
- Prototype backfill commit: `31afe67 Backfill P1.7.1 proof-check package into WebRebuildRecorder`.
- Future workflow: implement and verify in `wxici/codex/WebRebuildRecorder` first, then synchronize public-safe source/docs to `wxici/WebMuse`.

## Goal

Add the first proof-check package layer under `codex-task/proof/`:

- proof-check package schema and model classes;
- `ProofCheckPackageService.CreateNewAsync`, `LoadAsync`, and `ValidateAsync`;
- package-only manifest, request, instructions, and validation reports;
- path-safety validation for allowed and denied targets;
- FoundationSelfTest coverage for persistence, validation, path safety, and non-execution boundaries.

## Explicitly Out Of Scope

- real Codex CLI execution;
- OpenAI API calls;
- local model engine calls;
- website generation;
- WebView2 implementation;
- approval gate implementation;
- writing `output-site/current/index.html`;
- creating future runtime proof result files: `proof-created-file.txt`, `proof-result.json`, or `proof-report.md`;
- committing ignored runtime proof package files under `codex-task/proof/`.

## Verification

Required verification completed:

```powershell
dotnet build WebRebuildRecorder.slnx
dotnet run --project WebRebuildRecorder.FoundationSelfTest\WebRebuildRecorder.FoundationSelfTest.csproj
```

Result: build passed with 0 warnings and 0 errors; FoundationSelfTest passed and printed the required P1.7.1 proof-check lines.

## Completion Criteria

1. `ProofCheckPackage.cs` exists with P1.7.1 proof-check package models.
2. `ProofCheckPackageService.cs` exists with create/load/validate methods.
3. Generated package files are package/instruction/validation artifacts only.
4. Future runtime result files are declared but not generated.
5. Non-execution flags remain false.
6. FoundationSelfTest covers P1.7.1 behavior and all existing P0/P1 checks still pass.
7. Changes are committed and pushed to `wxici/WebMuse`.
8. P1.7.1 is also backfilled and pushed to the primary prototype repository.

## Next Task Boundary

Next implementation task is P1.7.2 Approval gate models and persistence, but it must start in `wxici/codex/WebRebuildRecorder`, not directly in WebMuse.
