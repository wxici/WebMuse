# CURRENT_TASK

## Current task

P2A-1 Internal Source Snapshot MVP / Deterministic Site Capture Engine

## Status

P2A-1 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

## Scope

- Capture one current reference URL only.
- Save bounded raw HTML and sanitized response headers.
- Capture WebView2 rendered DOM, visible text, viewport, element map, and style samples.
- Write deterministic resource manifests and analysis reports under `source-snapshot/`.
- Record resource URLs only; do not download site assets.
- Provide a network-free deterministic service path for FoundationSelfTest.

## Verification

- `dotnet build WebRebuildRecorder.slnx`: passed with 0 warnings and 0 errors.
- FoundationSelfTest: passed with deterministic known-HTML capture and no network/WebView2 initialization.
- UI smoke: passed for real HTTP capture, WebView2 rendered evidence, all required files, snapshot-directory action, old workflow controls, and no output-site index creation.
- WebView2 Runtime: installed and functional.

## Hard boundary

P2A-1 does not recursively crawl, bypass anti-automation, access authenticated or paid content, persist cookies/tokens/authorization headers, execute Codex CLI, call OpenAI or local models, generate a website, or write `output-site/current/index.html`.

## Next planned stage

P2A-2 Codex CLI Single Page Generation.
