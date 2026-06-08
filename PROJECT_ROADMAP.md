# Project Roadmap

## Current Recommended Next Stage

P2A-1.2 Controlled Desktop Source Snapshot + Frontend Reconstruction Evidence Graph is implemented and verified in the prototype worktree.

P2A-1.2 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation. WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

It now uses a dedicated controlled capture window instead of the embedded `587 x 359` preview as primary evidence. It writes `rendered/first-screen.png`, fetches bounded frontend text resources, generates dependency/section/media/behavior/css/js/reconstruction evidence outputs, and writes `analysis/ai-reconstruction-brief.md`.

The AirCenter runtime package was exported outside the repository and is not included in WebMuse:

```text
aircenter-p2a-1-2-reconstruction-review_<timestamp>.zip
```

P2A-1.2-A does not execute Codex CLI, call OpenAI API, call local models, generate a website, recursively crawl, download original binary image/font/video resources, or write `output-site/current/index.html`.

Next immediate decision:

```text
If the runtime review passes: P2A-2 Codex CLI Single Page Generation.
If the runtime review does not pass: P2A-1.2-B evidence graph gap fixes.
```

P2A-0 WebView2 Preview Shell and P2A-0.1 Detached WebView2 Preview Window are implemented and verified.

P2A-1 Internal Source Snapshot MVP is implemented and locally verified.

P2A-1 captures one reference URL with bounded HTTP raw HTML and WebView2 rendered evidence. It writes structured `source-snapshot/` text, JSON, and Markdown evidence without downloading resources or recursively crawling.

P2A-1 does not execute Codex CLI, call OpenAI API or local models, generate a website, or write `output-site/current/index.html`.

A real `aircenter.space` runtime review package was generated and reviewed after P2A-1. It proved the package structure and basic capture pipeline, but it also found two blocking issues before P2A-2:

1. Rendered evidence was captured from the small embedded WebView2 viewport around `587 x 359`, not from a controlled desktop viewport.
2. `OBSERVATION_FROM_SNAPSHOT.md` was too shallow. It missed or under-described the hero Vimeo background, responsive picture/source rules, AIR SVG/preloader structure, sticky/parallax/reveal/contentAnimation behavior, image clip slider behavior, and CSS/JS relationship evidence.

Next immediate implementation round:

```text
P2A-1.2 Controlled Desktop Source Snapshot + Frontend Reconstruction Evidence Graph
```

P2A-2 Codex CLI Single Page Generation is postponed until P2A-1.2 produces a useful reconstruction evidence graph and `analysis/ai-reconstruction-brief.md`.

Updated P2A roadmap:

1. P2A-1 Internal Source Snapshot MVP - implemented.
2. P2A-1 runtime review package for `aircenter.space` - completed; found viewport and shallow-report issues.
3. P2A-1.2 Controlled Desktop Source Snapshot + Frontend Reconstruction Evidence Graph - next.
4. P2A-2 Codex CLI Single Page Generation - postponed until P2A-1.2 passes review.
5. P2A-3 Output Validation + WebView2 Preview.
6. P2A-4 Minimal Tuning / Color Controls.

P2A-1.2 must add or prepare these evidence outputs:

```text
rendered/first-screen.png
analysis/dependency-graph.json
analysis/section-map.json
analysis/media-placement-map.json
analysis/responsive-media-map.json
analysis/behavior-map.json
analysis/animation-signal-map.json
analysis/css-rule-map.json
analysis/js-behavior-reference-map.json
analysis/reconstruction-evidence-graph.json
analysis/ai-reconstruction-brief.md
```

P2A-1.2 should begin bounded fetching and analysis of text frontend resources such as CSS, JavaScript, SVG, manifest, JSON config, and source maps if present. Binary images, fonts, and videos can initially remain URL/metadata evidence, with screenshots and metadata extraction allowed.

The next Codex instruction must explicitly carry forward the known audit issues from the `aircenter.space` package review.

P1.7.4 Failure recovery policy service remains postponed, not cancelled.

## Historical P1.7/P1.8 Context

P1.7-0 defined proof-check and approval-gate design only. P1.7.1 was first implemented in `wxici/WebMuse` as commit `2d2cb82a02992103d292f16bb80d25ae1a2a94b9` and has now been backfilled into the primary prototype repository `wxici/codex/WebRebuildRecorder`.

P1.7.2 implements approval gate models and persistence only. It adds project-relative approval request/result/report files, approval binding hashes, state transitions, stale-binding validation, and FoundationSelfTest coverage.

P1.7.2 does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`.

P1.7.3 now implements execution precondition aggregation only. It adds `ExecutionPrecondition.cs`, `ExecutionPreconditionService.cs`, `EvaluateAsync(...)`, `LoadLatestAsync(...)`, runtime execution-precondition reports, aggregation over readiness/dry-run/proof package/approval/rollback/sandbox/security/output/context/manual fallback checks, and FoundationSelfTest coverage.

P1.7.3 still does not execute Codex CLI, run any `codex` command, call OpenAI API, call local model engines, generate websites, or write `output-site/current/index.html`. In the current phase, missing real proof execution remains `NotImplemented` and blocks real execution; `AllowsRealCodexExecution` remains `false`.

Repository workflow rule: implementation and verification land first in `wxici/codex/WebRebuildRecorder`; `wxici/WebMuse` receives public-safe synchronized source and documentation after prototype verification.

P1.6 was later committed and pushed as cc28e56613d16032cea5664d23d8415a37610a86. Older review_package text saying commit/push was pending is historical transport context from the current GitHub state.

P1.6.1 closed transport/status wording and audited generated dry-run artifacts only. It does not change the next technical stage.

P1.7 design scope:

- proof-check files;
- approval gates;
- rollback confirmation;
- execution preconditions;
- future execution state machine;
- failure recovery and retry rules;
- manual construction package export fallback.

P1.6 has now implemented dry-run orchestration only, not real Codex CLI execution.

P1.6 completed:

- verify readiness;
- generate task plan;
- simulate execution plan;
- check allowed write roots;
- create dry-run record;
- write dry-run plan/result/report/logs;
- do not start Codex CLI;
- do not call OpenAI API;
- do not generate website.

P1.7 should still avoid real execution unless explicitly authorized. It should design proof checks, user approval gates, sandbox preflight, rollback confirmation, result verification, and failure recovery before any real Codex CLI execution is allowed.

Future directions that must not enter P1.7 unless explicitly authorized:

- Design Context Library implementation;
- Design Asset Pipeline implementation beyond documentation/schema reservation;
- Reference Portal UI or remote portal integration;
- Reference Site Library UI, database, crawler, submission system, or community layer;
- Frontend Effect Recipe Library implementation;
- ProposalPreview / SitePitcher implementation;
- WebView2 browsing/preview integration;
- real Codex CLI execution;
- OpenAI API calls;
- website generation;
- Local Responses Adapter implementation or adapter-backed provider execution.

Later real Codex CLI execution can be considered only after rollback, strict readiness, dry-run orchestration, proof checks, sandbox policy, run records, and failure recovery are stable.

Historical P1.7 boundary note: before the 2026-06-05 direction reset, WebView2 preview, tuning UI, color-system UI, real Codex CLI execution, and Local Responses Adapter implementation were kept out of the immediate P1.7/P1.8 foundation track. The current next stage is now P2A Structural Alpha, starting with P2A-0 WebView2 Preview Shell.

## Future AI Engine Extension: Local Responses Adapter

A future architecture direction is recorded in:

```text
PROJECT_BLUEPRINT_LOCAL_RESPONSES_ADAPTER.md
```

The Local Responses Adapter is a future AI engine extension layer. Its purpose is to let advanced users eventually route Codex-style Responses requests through a local machine adapter to other online model providers, while preserving official Codex / OpenAI as the recommended high-quality construction path.

This direction is valuable for cost control, provider diversity, and fallback workflows, but it is not a P1.7 implementation target.

Current status:

- record architecture only;
- do not implement adapter source code in P1.7;
- do not connect adapter-backed providers in P1.7;
- do not use adapter-backed construction in P1.7;
- keep the adapter as `[ALT-EXPERIMENT]` until a separate proof suite repeatedly passes.

Future adapter-backed providers must pass proof checks before they can be used for low-risk construction:

- connectivity check;
- text response check;
- sandbox proof file creation;
- small isolated source-file modification;
- build command verification;
- minimal repair after a small build failure;
- path boundary validation;
- safe report generation;
- confirmation that no writes occur outside allowed project roots.

Provider capability labels should eventually include:

```text
Not usable
Documentation only
Draft coding only
Low-risk construction only
Formal construction candidate
```

The adapter must not delay the main product path: P1.7 safety closure -> P2 professional UI shell -> WebView2 preview -> official Codex minimum construction -> basic color palette selection -> basic tuning -> export and validation.

## Future Design Asset Pipeline

A future architecture direction is recorded in:

```text
PROJECT_BLUEPRINT_DESIGN_ASSET_PIPELINE.md
```

The Design Asset Pipeline governs future external DESIGN.md files, Design Skills, Taste Skills, Agent Skills, reference design libraries, and competitor pattern notes.
