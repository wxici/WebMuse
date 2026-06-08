# CURRENT_TASK

## Current task

P2A-1.2 Controlled Desktop Source Snapshot + Frontend Reconstruction Evidence Graph

## P2A-1.2-A implementation status

P2A-1.2-A implements controlled desktop Source Snapshot capture and core reconstruction evidence graph.

It fixes the previous embedded viewport problem by using a dedicated capture window instead of treating the embedded `587 x 359` WebView2 surface as the primary evidence.

It writes `rendered/first-screen.png`.

It begins bounded frontend text-resource capture and analysis for CSS, JavaScript, SVG, manifest, JSON, and source-map style resources when present.

It generates dependency/section/media/behavior/css/js/reconstruction evidence outputs.

It generates `analysis/ai-reconstruction-brief.md`.

It does not execute Codex CLI, call OpenAI API, call local models, generate a website, or write `output-site/current/index.html`.

Runtime verification was performed against:

```text
https://aircenter.space/
```

P2A-1.2 was implemented and verified first in `wxici/codex/WebRebuildRecorder`, then synchronized here as public-safe source and documentation.

WebMuse remains an OSS-safe result extraction repository, not the primary construction worktree.

The runtime review package was exported outside the repository and is not included in WebMuse:

```text
aircenter-p2a-1-2-reconstruction-review_<timestamp>.zip
```

The package is a runtime artifact and must not be committed to Git.

## Why this task replaced P2A-2

P2A-1 Internal Source Snapshot MVP is implemented, verified, committed, and pushed to `wxici/codex` as `cdff880`. Public-safe source and documentation were synchronized to `wxici/WebMuse` as `81aa087`.

After P2A-1, a real runtime review package was generated for:

```text
https://aircenter.space/
```

The review found that P2A-1 can capture and package basic evidence, but the output is not yet reliable enough for P2A-2 Codex CLI Single Page Generation.

Blocking findings:

1. Rendered evidence used the small embedded WebView2 viewport, around `587 x 359`, not a controlled desktop viewport.
2. The package captured a rotate-device warning and small-screen evidence, so desktop layout evidence is unreliable.
3. `OBSERVATION_FROM_SNAPSHOT.md` existed but was too shallow for reconstruction.
4. The report missed key raw-HTML evidence, including the hero Vimeo video background, responsive `picture/source` rules, preloader/AIR SVG structure, sticky/parallax/reveal/contentAnimation behavior, image-clip sliders, and CSS/JS relationship signals.
5. The package lacked a true `analysis/ai-reconstruction-brief.md` that can guide Codex generation.

## Scope

P2A-1.2 must upgrade Source Snapshot from shallow observation to a reconstruction evidence graph.

Required direction:

- support controlled desktop viewport capture such as `1440x900`, `1920x1080`, and `1366x768`;
- generate reliable viewport evidence;
- add first-screen screenshot evidence;
- start bounded fetching and analysis of frontend text resources such as CSS, JavaScript, SVG, manifest, JSON config, and source maps if present;
- map sections, media, responsive variants, CSS classes, JS/data-plugin behavior declarations, animation signals, and rebuild guidance;
- generate a real AI reconstruction brief for later P2A-2.

Expected future outputs include:

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

## Required runtime capability review package

P2A-1.2 cannot be marked complete after build/self-test alone.

After implementation, Codex must run the updated program against a real target such as:

```text
https://aircenter.space/
```

and generate a runtime review package outside the repository, for example:

```text
aircenter-p2a-1-2-reconstruction-review_<timestamp>.zip
```

The review package must include at least:

```text
RUN_REPORT.md
CAPABILITY_REVIEW.md or OBSERVATION_FROM_RECONSTRUCTION_EVIDENCE.md
PACKAGE_MANIFEST.md
SHA256SUMS.txt
source-snapshot/**
rendered/viewport.json
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

The package report must explicitly answer whether these previous audit issues were fixed:

1. controlled desktop viewport was used;
2. small embedded viewport is no longer the primary evidence;
3. first-screen screenshot exists;
4. AirCenter hero Vimeo background was detected;
5. responsive `picture/source` rules were detected;
6. preloader/AIR SVG structure was detected;
7. sticky/parallax/reveal/contentAnimation declarations were mapped;
8. DOM class -> CSS file/rule -> JS behavior/effect relationships were mapped;
9. a Codex-ready AI reconstruction brief was generated.

The runtime review package must not be committed to Git. It is for manual user/GPT review.

## Must-carry audit issues for next Codex instruction

The next Codex instruction must explicitly include the following known audit issues:

1. P2A-1 package structure passed, but desktop evidence failed.
2. Embedded viewport `587 x 359` is unacceptable for desktop reconstruction.
3. `OBSERVATION_FROM_SNAPSHOT.md` missed the `aircenter.space` first-screen dynamic video background.
4. It missed responsive placeholder/source rules and Vimeo iframe background details.
5. It missed text/section animation behavior declarations such as `data-plugin`, `data-parallax-pattern`, `data-content-animation-animations`, `data-reveal`, `data-scroll-sticky`, and `data-scroll-snap-point`.
6. It missed mapping between DOM classes, CSS files, JS files, and visible effects.
7. It did not generate a Codex-ready reconstruction brief.
8. Copyright/brand risk should not weaken local analysis depth; it should only constrain final output and asset replacement.

## Hard boundary

P2A-1.2 is still a Source Snapshot / evidence-analysis task.

It must not:

- execute Codex CLI;
- call OpenAI API;
- call local models;
- generate a website;
- write `output-site/current/index.html`;
- implement full tuning/color controls;
- implement CMS/backend/e-commerce/login;
- turn into a general crawler or bypass anti-automation.

## Next planned stage after P2A-1.2

P2A-2 Codex CLI Single Page Generation, but only after P2A-1.2 produces a useful reconstruction evidence graph and `analysis/ai-reconstruction-brief.md` that pass review.
