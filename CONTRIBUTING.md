# Contributing

Thanks for your interest in WebMuse.

WebMuse is early alpha. The project currently prioritizes safety foundations, documentation, package validation, sandbox boundaries, dry-runs, proof-checks, approval gates, and rollback readiness.

## Before contributing

Please read:

- `README.md`
- `ROADMAP.md`
- `SECURITY.md`
- `docs/ARCHITECTURE.md`

## Contribution scope

Good first contribution areas:

- documentation improvements;
- build fixes;
- test coverage;
- issue triage;
- package schema review;
- safety boundary review;
- dry-run/proof-check design review.

Avoid for now:

- real Codex execution;
- OpenAI API integration;
- WebView2 implementation;
- UI redesign;
- website generation;
- drag-and-drop editing;
- CMS/ecommerce features.

## Development

```powershell
dotnet restore WebRebuildRecorder.slnx
dotnet build WebRebuildRecorder.slnx
dotnet run --no-build --project WebRebuildRecorder.FoundationSelfTest/WebRebuildRecorder.FoundationSelfTest.csproj
```

## Security

Do not include customer files, recordings, screenshots, tokens, keys, generated sites, or local private configuration in issues or pull requests.

