# Project Status

## Current Strategic State

Snapshot time: 2026-06-12.

P2A-1.3 is now inserted before P2A-2-B.

The required next implementation stage is:

```text
P2A-1.3 Asset Slot Map + Motion Slot Map + Generation Brief
```

Current implementation state:

```text
Documentation/memory update only. Code implementation has not started.
```

The first-stage evidence route is:

```text
controlled source snapshot
  -> DOM/CSS/resource analysis
  -> section map
  -> asset slot map
  -> color / typography / spacing / layout tokens
  -> motion slot map
  -> generation brief
  -> Codex construction package
  -> branded output
```

Reference-observed assets default to `reference_only`, `replacementRequired = true`, and `canExport = false`.

Motion should be extracted as semantics and WebMuse-owned variants. Source CSS/JavaScript implementation code must not be copied by default.

Blocked:

```text
P2A-2-B Controlled Codex CLI Single Page Generation remains blocked until P2A-1.3 verification passes.
```

## Latest P2A-2-A Single Page Generation Package Builder

Snapshot time: 2026-06-08.

P2A-2-A only creates a Codex single-page generation package.

P2A-2-A was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

It reads the latest P2A-1.2 Source Snapshot reconstruction evidence and writes a runtime construction package under:

```text
codex-task/single-page-generation/<package-id>/
```

Package files:

- `generation-manifest.json`
- `CONSTRUCTION_BRIEF.md`
- `PROMPT_FOR_CODEX.md`
- `OUTPUT_CONTRACT.md`
- `FORBIDDEN_CONTENT.md`
- `ASSET_SLOT_PLAN.md`
- `EVIDENCE_INDEX.md`
- `REVIEW_CHECKLIST.md`

The package explicitly filters analytics/tracking scripts, Cloudflare beacon, Mindbox tracker, recaptcha, cookie consent UI, hidden modal forms, favourites, office backend forms, server-side endpoints, and original logo/image/video/font assets from later final delivery.

P2A-2-A does not execute Codex CLI, run any `codex` command, call OpenAI API, call local models, generate website output, or write `output-site/current/index.html`.

Verification:

- `dotnet build WebRebuildRecorder.slnx`: passed with 0 warnings and 0 errors.
- `FoundationSelfTest`: passed, including P2A-2-A package-builder checks.
- UI smoke: passed against the AirCenter P2A-1.2 runtime project.
- Runtime review package: generated outside the repository as `aircenter-p2a-2-a-generation-package-review_<timestamp>.zip`; not synchronized to WebMuse.

Next planned stage:

```text
P2A-2-B Controlled Codex CLI Single Page Generation
```

## Latest P2A-1.2 Controlled Desktop Source Snapshot

Snapshot time: 2026-06-08.

P2A-1.2-A implements controlled desktop Source Snapshot capture and core reconstruction evidence graph.

It fixes the previous embedded viewport problem by using a dedicated capture window. The embedded `587 x 359` WebView2 preview is no longer used as the primary Source Snapshot evidence path.

Completed:

1. Added reconstruction evidence models and analyzer services.
2. Added a dedicated WebView2 capture window for controlled desktop viewport rendering.
3. Added Source Snapshot viewport selection in the existing WPF UI.
4. Wrote `rendered/first-screen.png` from WebView2 `CapturePreviewAsync`.
5. Added bounded frontend text-resource capture and analysis for CSS, JavaScript, SVG, manifest, JSON, and source-map style resources when present.
6. Generated dependency, section, media placement, responsive media, behavior, animation signal, CSS rule, JS behavior reference, and reconstruction evidence graph outputs.
7. Generated `analysis/ai-reconstruction-brief.md`.
8. Preserved the P2A-0/P2A-0.1 preview, recording, frame extraction, ChatGPT package, and final Codex package workflows.

AirCenter runtime verification:

- target: `https://aircenter.space/`
- viewport: `1440 x 900`
- `rendered/first-screen.png`: generated and visually confirmed as the AirCenter first screen, not a lock screen capture
- bounded text resources: 28 entries, 27 fetched
- dependency nodes: 137
- sections: 33
- media placements: 250
- behavior declarations: 170
- CSS evidence rows: 196
- JS behavior references: 5
- runtime review package: generated outside the repository as `aircenter-p2a-1-2-reconstruction-review_<timestamp>.zip`; not synchronized to WebMuse

P2A-1.2-A does not execute Codex CLI, call OpenAI API, call local models, generate a website, recursively crawl, download original binary image/font/video resources, or write `output-site/current/index.html`.

Next stage remains review-gated: if the P2A-1.2 runtime review package passes, proceed to P2A-2 Codex CLI Single Page Generation; otherwise continue with P2A-1.2-B evidence graph gap fixes.

## Latest direction correction after AirCenter runtime review

Snapshot time: 2026-06-07.

After P2A-1, a real Source Snapshot runtime review package was generated for:

```text
https://aircenter.space/
```

Review result:

```text
Package structure: passed.
P2A-1 basic deterministic capture ability: passed.
Direct readiness for P2A-2 Codex CLI Single Page Generation: not passed.
```

The package confirmed that the current program can capture and package raw HTML, rendered DOM, visible text, element map, resource manifest, reports, and runtime metadata without using Codex browsing or AI calls.

However, the review found blocking issues:

1. Rendered evidence was captured from the embedded WebView2 surface with a small viewport around `587 x 359`.
2. The capture included rotate-device / small-screen evidence, so it is not reliable desktop reconstruction evidence.
3. `OBSERVATION_FROM_SNAPSHOT.md` existed, but it was too shallow for webpage reconstruction.
4. It missed the target page's first-screen dynamic video background, responsive `picture/source` rules, Vimeo iframe background, AIR SVG/preloader structure, sticky/parallax/reveal/contentAnimation behavior, image clip slider behavior, and CSS/JS relationship evidence.
5. It did not provide a real Codex-ready `analysis/ai-reconstruction-brief.md`.

Corrected next stage:

```text
P2A-1.2 Controlled Desktop Source Snapshot + Frontend Reconstruction Evidence Graph
```

P2A-2 is postponed until P2A-1.2 produces a useful reconstruction evidence graph and AI reconstruction brief.

New companion documents:

```text
PROJECT_MEMORY_VAULT/WEBMUSE_SOURCE_SNAPSHOT_RECONSTRUCTION_EVIDENCE.md
WebRebuildRecorder/PROJECT_BLUEPRINT_RECONSTRUCTION_EVIDENCE_GRAPH.md
```

Key principle:

```text
Source Snapshot is not a shallow style report. It is the foundation for reconstruction: structure, sections, media, CSS, JS behavior declarations, animation signals, screenshots, and AI reconstruction instructions must be mapped together.
```

The next Codex instruction must carry forward the known audit issues from the AirCenter review.

## Latest P2A-1 Internal Source Snapshot MVP

Snapshot time: 2026-06-07.

P2A-1 implements Internal Source Snapshot MVP.

It captures one current reference URL only. It performs bounded HTTP raw HTML capture and WebView2 rendered evidence capture, then writes deterministic text, JSON, and Markdown evidence under `source-snapshot/`.

Completed:

1. Added `SourceSnapshotModels.cs` and `SourceSnapshotService.cs`.
2. Added `source-snapshot/raw`, `rendered`, `resources`, and `analysis` paths to Project Directory V2 and `WrbProjectPaths`.
3. Added a 4MB raw HTML limit and clear truncation warning.
4. Added sanitized response-header persistence without cookie, auth, token, key, secret, or session headers/values.
5. Added resource URL manifests for CSS, JavaScript, images, fonts, videos, and other links without downloading those resources.
6. Added WebView2 rendered DOM, visible text, viewport, element map, and style-signal extraction.
7. Added deterministic layout, color, typography, asset-slot candidate, JSON report, and Markdown report output.
8. Added MainWindow generation and snapshot-directory controls while preserving P2A-0, P2A-0.1, recording, frame extraction, ChatGPT package, and final Codex package workflows.
9. Added a network-free FoundationSelfTest path using known HTML and fake rendered evidence.

Verification:

- Solution build passed with 0 warnings and 0 errors.
- FoundationSelfTest passed, including sensitive-header filtering, resource classification, required file persistence, no resource downloads, and no output-site index generation.
- UI smoke captured `https://example.com/` successfully with HTTP 200 and rendered evidence, generated all 18 required files, opened the snapshot directory action, and did not create `output-site/current/index.html`.
- WebView2 Runtime is installed and functional.

P2A-1 does not download site assets, recursively crawl, bypass anti-automation, access authenticated/paid content, execute Codex CLI, call OpenAI API or local models, generate a website, or write `output-site/current/index.html`.

Next recommended stage is no longer P2A-2 directly. The corrected next stage is P2A-1.2.

## Latest P2A-0.1 Detached WebView2 Preview Window

Snapshot time: 2026-06-07.

P2A-0.1 adds a detached adjustable WebView2 preview window.

It solves the fixed 360-height embedded preview limitation where some reference sites detect a small or vertical viewport.

Completed:

1. Added a separate `DetachedPreviewWindow` with its own WebView2 instance.
2. Added standard title-bar drag, maximize, minimize, and free resize behavior.
3. Added `1366x768`, `1440x900`, `1920x1080`, `1024x768`, and `390x844` presets.
4. Added address navigation, refresh, external-browser fallback, and live window-size status.
5. Added one MainWindow launch button that reuses the existing detached window and prefers the current embedded-preview URI.
6. Added project-close and main-window-close cleanup.
7. Preserved the P2A-0 embedded preview and old recording/frame extraction/ChatGPT package/final Codex package workflow.
8. Narrowly extended the historical P1.8 scope guard for only the four explicit P2A-0.1 UI files.

Verification:

- `dotnet build WebRebuildRecorder.slnx`: passed with 0 warnings and 0 errors.
- FoundationSelfTest: passed.
- UI smoke test: passed.
- WebView2 Runtime: installed and functional.

The current desktop work area can clamp presets larger than the available screen while preserving the requested preset action and resizable window behavior.

P2A-0.1 does not implement Source Snapshot, Codex CLI, site generation, tuning, color controls, asset-slot overlays, or full Docking UI.

## Latest P2A-0 WebView2 Preview Shell

Snapshot time: 2026-06-06.

P2A-0 implements a minimal embedded WebView2 preview shell.

It can open the reference URL and local `output-site/current/index.html` when present.

It does not implement Source Snapshot, Codex CLI execution, site generation, tuning, color controls, asset-slot overlays, JavaScript injection, DOM reading, screenshots, or full UI redesign.

The old recording/frame extraction/ChatGPT package/final Codex package workflow remains intact.

Completed:

1. Added official `Microsoft.Web.WebView2` package version `1.0.3967.48`.
2. Added the WebView2 WPF XAML namespace.
3. Added a fixed 360-height P2A-0 preview card above the existing workflow cards.
4. Added reference URL preview, local output preview, refresh, and external-browser fallback controls.
5. Added WebView2 Runtime initialization and navigation status reporting.
6. Added missing `output-site/current/index.html` handling without creating the file or crashing.
7. Added project reset/button-state integration.
8. Narrowly updated the historical P1.8 self-test scope guard so only the explicit P2A-0 files are allowed while Source Snapshot, ProposalPreview, and broader UI scope remain blocked.

Verification:

- `dotnet build WebRebuildRecorder.slnx`: passed with 0 warnings and 0 errors.
- FoundationSelfTest: passed.
- UI smoke test: passed.
- WebView2 Runtime: installed and functional.

## 2026-06-05 Direction Reset

P1.8-0 completed alpha validation probe, but the project will not continue immediately into P1.7.4-A. The next stage is P2A Structural Alpha because the old project already had a manual GPT/Codex/zip loop, and the rebuilt project must now demonstrate structural advantages: WebView2 preview, Source Snapshot, controlled Codex CLI, output-site/current preview, and minimal tuning/color controls.

Current decision:

- P1.7.4-A is postponed, not cancelled.
- Old-UI-only alpha validation is not sufficient.
- The project should stop only deepening P0/P1 foundation work and move into visible structural alpha validation.

P2A-0 remains a minimal preview shell task. It should not remove the old recording/frame extraction/ChatGPT package workflow and should not implement Source Snapshot, Codex CLI execution, tuning, color system, full UI redesign, CMS, database backend, payment, login, membership, e-commerce, forum, or a complex page editor.

## Latest P1.8-0 Early Alpha Validation Probe

Snapshot time: 2026-06-05 18:51 +08:00.

P1.8-0 implements an early alpha validation probe only.

It does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

The purpose is to prove the P0/P1 foundation can be composed into a local explainable pipeline report before continuing deeper P1.7.4 failure recovery work.

P1.7.4 Failure recovery policy service is postponed, not cancelled.

Repository workflow remains prototype-first:

1. `wxici/codex/WebRebuildRecorder` is the primary construction worktree.
2. `wxici/WebMuse` receives only public-safe source and status documentation after prototype verification.
3. Runtime alpha validation reports under `codex-task/alpha-validation/` are ignored and must not be committed.

Completed in this round:

1. Added `AlphaValidationProbe.cs` with schema constants, step status enum, report model, and step model.
2. Added `AlphaValidationProbeService.cs` with `RunAsync(...)` and `LoadLatestAsync(...)`.
3. `RunAsync(...)` writes project-relative runtime reports under `codex-task/alpha-validation/<probe-id>/alpha-validation-report.json` and `alpha-validation-report.md`.
4. The probe composes existing P0/P1 foundations: V2 project structure, manifest, assets/theme/content-map, observation package, construction package, task package/instructions, P1.5 readiness, P1.6 dry-run, P1.7.1 proof package validation, P1.7.2 approval artifacts, P1.7.3 execution precondition, manual fallback evidence, runtime artifact ignore coverage, and non-execution boundary checks.
5. Blocked-but-explainable findings are allowed as Alpha evidence when the critical non-execution boundary and real-execution block are preserved.
6. `ExecutesCodexCli`, `CallsOpenAiApi`, `CallsLocalModel`, and `GeneratesWebsite` remain `false`.
7. Root and project `.gitignore` now ignore `codex-task/alpha-validation/` runtime artifacts.
8. `WebRebuildRecorder.FoundationSelfTest` covers P1.8-0 models, persistence, local pipeline steps, blocked-but-explainable evidence, missing package handling, string enum JSON, `.gitignore` coverage, non-execution boundaries, no `output-site/current/index.html`, and no UI/WebView2/Source Snapshot/ProposalPreview diff.

Verification:
