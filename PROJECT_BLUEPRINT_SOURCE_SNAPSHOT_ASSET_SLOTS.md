# PROJECT_BLUEPRINT_SOURCE_SNAPSHOT_ASSET_SLOTS.md
# WebMuse Source Snapshot + Asset Slot Overlay Blueprint

## Purpose

This blueprint records the updated structural direction for WebMuse after the June 2026 product strategy discussion.

WebMuse should not remain centered on recording and frame extraction as the first observation path. The future observation pipeline should prioritize source capture and structured analysis first, then use screenshots, interaction evidence, and local recording only when source capture cannot explain the reference site accurately.

The product goal remains branded reconstruction, not cloning. Source capture is a way to understand a reference site, infer structure, derive style rules, and identify required user-owned assets. It is not permission to copy third-party code, logos, photography, illustrations, copy, or protected brand material into commercial output.

## Updated Positioning

WebMuse is a reference-site analysis, asset-preparation, Codex-assisted construction, and validation workbench.

It should help a user move from:

```text
reference inspiration
  -> source snapshot
  -> structure/style/asset-slot analysis
  -> construction package
  -> safe Codex task package
  -> branded output
  -> user asset replacement
  -> tuning
  -> validation
  -> export
```

It is still not:

* a website clone tool;
* a scraper for republishing third-party sites;
* a tool for bypassing copyright or license rules;
* a visual drag-and-drop page editor;
* a CMS, ecommerce system, or backend generator in the first stages.

## Core Direction Change

Old main mental model:

```text
record reference site -> extract frames -> ask AI to infer structure -> build new site
```

New main mental model:

```text
capture source snapshot -> analyze rendered DOM/assets/styles/code quality -> infer structure/style/asset slots -> record only missing interaction evidence -> build new branded site -> let user fill asset slots -> tune and validate
```

Recording and frame extraction remain valuable, but they move from the default observation path to a targeted fallback for complex motion and interaction evidence.

## Source Snapshot Scope

The first serious implementation should be a controlled source snapshot system, not an unrestricted crawler.

Default limits:

* same-origin pages only by default;
* bounded depth;
* bounded page count;
* bounded asset count;
* bounded asset size;
* cancellable capture;
* no login-wall bypass;
* no anti-bot circumvention;
* no scraping of private or authenticated user data;
* all writes stay inside the project sandbox.

The snapshot should collect enough material for AI analysis:

* raw HTML;
* rendered DOM;
* CSS;
* JavaScript references and downloadable files where allowed;
* images;
* SVG;
* fonts;
* media references;
* link graph;
* meta tags;
* desktop screenshot;
* mobile screenshot;
* resource manifest;
* capture quality report.

## Recommended Project Output Paths

Future project directories should reserve these paths:

```text
observation/source-snapshot/
  raw-html/
  rendered-dom/
  assets/
    images/
    svg/
    css/
    js/
    fonts/
    media/
  screenshots/
  reports/
    source-snapshot-report.md
    capture-quality-report.md
    copyright-risk-report.md
  source-manifest.json
  resource-request-log.json

maps/
  structure-map.json
  style-profile.json
  color-palette.json
  typography-profile.json
  asset-slot-map.json
  interaction-gap-report.md

assets/
  user-assets/
  selected/
  generated/
  reference-observed/
```

`reference-observed` assets are analysis inputs only by default. They must not flow into final export unless the user explicitly confirms authorization and the system records that confirmation.

## Capture Quality Classification

The system should classify captured sites before deciding the next observation step.

Suggested classes:

```text
A: Clear source. Rendered DOM, text, structure, and CSS are readable enough for high-confidence analysis.
B: Compressed but usable. Structure, colors, typography, and asset needs can still be inferred.
C: SPA/build-output heavy. Some structure is recoverable from rendered DOM, but source code is not a reliable construction guide.
D: Motion/canvas/video/interaction dependent. Source snapshot is not enough; targeted screenshots or recording are required.
E: Blocked or incomplete. User screenshots, manual recording, or pasted evidence are required.
```

The system should generate `interaction-gap-report.md` whenever the snapshot cannot explain hover, click, scroll, theme switching, overlay reveal, video behavior, masking, canvas, or WebGL effects.

## Source Code Use Boundary

Captured code is reference evidence, not the construction base.

Forbidden default flow:

```text
capture original site -> directly edit original HTML/CSS/JS -> change logo -> export
```

Required flow:

```text
capture original site -> analyze structure/style/assets -> generate construction rules -> create a new branded output site -> replace with user-owned or authorized assets -> validate and export
```

The construction package must state that source code, proprietary copy, logos, and unique brand assets from the reference site are not final delivery assets.

## Asset Slot Map

A central output of analysis should be an asset slot map.

The map describes what the generated site needs, not just what the reference site contains.

Each slot should include:

* stable `slotId`;
* human label;
* section reference;
* asset type: logo, image, icon, background, video, SVG, pattern, etc.;
* required/optional status;
* count;
* recommended ratio;
* minimum dimensions;
* preferred visual mood;
* preferred color tone;
* brightness and contrast needs;
* suggested content type: product, person, space, service scene, abstract texture, etc.;
* replacement mode: cover, contain, background-cover, inline, icon, video-poster;
* export policy;
* current asset id, if filled;
* fallback behavior.

Example:

```json
{
  "slotId": "home.hero.image",
  "label": "Home hero main image",
  "sectionId": "home.hero",
  "type": "image",
  "required": true,
  "count": 1,
  "recommendedRatio": "16:9",
  "minWidth": 1600,
  "minHeight": 900,
  "preferredTone": ["clean", "premium", "calm"],
  "preferredColors": ["warm white", "soft gray", "muted gold"],
  "suggestedContent": "wide service or environment photo with simple composition",
  "replacementMode": "background-cover",
  "currentAssetId": null,
  "exportPolicy": "user-owned-or-authorized-only"
}
```

## Asset Slot Overlay

For image and media regions that lack user-provided assets, the generated preview should not rely on random preset photography by default.

Preferred behavior:

* keep the layout area in place;
* show a clean overlay in the original visual position;
* display required size, ratio, tone, count, and purpose;
* make the region clickable;
* send the clicked `slotId` from WebView2 JavaScript to the WPF host;
* let WPF open the native file picker;
* copy the selected file into the project `assets/user-assets/` directory;
* update `asset-slot-map.json` and `asset-map.json`;
* notify the WebView2 preview to replace the slot immediately;
* preserve project state and rollback compatibility.

The overlay is a preparation aid, not a page editor.

It can show concise text such as:

```text
Hero image
16:9 | 1600 x 900+
Tone: clean / premium / calm
Need: 1 image
Click to upload
```

## Preset Asset Policy

The default strategy should avoid bundled preset photography.

Allowed minimal placeholders:

* text brand name as temporary logo;
* simple SVG logo mark placeholder;
* neutral CSS color blocks;
* gradient or blur blocks;
* wireframe icons;
* geometric SVG placeholders.

Avoid early reliance on bundled stock photos because they create license management, visual misdirection, and product-maintenance burden.

## User Asset Matching

Future `AssetMatchingService` should help assign uploaded assets to slots by analyzing:

* dimensions;
* aspect ratio;
* orientation;
* file type;
* transparent background;
* brightness;
* dominant colors;
* whether it looks like a logo, product, face/person, space, screenshot, texture, or icon;
* quality and resolution;
* whether the asset fits the slot's tone and ratio.

The system may suggest mappings, but the user should be able to confirm or replace them.

## Copyright And Export Policy

Captured reference-site assets should default to:

```text
analysis-only / reference-observed / not-exportable
```

Export-safe assets are:

* user-uploaded assets with user confirmation;
* project-owned generated assets;
* explicitly authorized assets;
* simple system-created placeholders that are safe to distribute.

Before `output-site.zip`, validation must check that final output does not contain unauthorized `reference-observed` assets, third-party logos, protected brand copy, local absolute paths, tokens, cookies, or private customer files.

## Service Concepts

Future implementation can reserve these concepts:

```text
SourceSnapshotService
SourceSnapshotOptions
SourceSnapshotResult
RenderedDomCaptureService
SourceResourceCollector
CaptureQualityAnalyzer
CodeReadabilityAnalyzer
ReferenceAssetRiskAnalyzer
StructureMapBuilder
StyleProfileBuilder
AssetSlotMapBuilder
AssetSlotOverlayBuilder
AssetImportService
AssetMatchingService
InteractionGapAnalyzer
InteractionEvidencePlanner
```

## Phase Placement

This blueprint changes the future direction but must not interrupt current foundation work.

Immediate P1.7 work remains safety/proof/approval foundation unless explicitly changed.

Suggested placement:

* P1: reserve data models, manifests, and paths for source snapshots and asset slots.
* P2: add WebView2 preview shell and minimal asset slot overlay interaction.
* P3: add controlled source snapshot service and construction-package injection.
* P4: add interaction evidence fallback through local screenshots/recording/frame extraction.
* P5: improve asset matching, batch slot filling, quality hints, and productized workflows.

Do not implement unrestricted crawling, login scraping, anti-bot bypass, or direct clone/export workflows.

## Codex Instruction Implication

Every future Codex construction instruction for this repository should include the WebMuse OSS update block:

1. Keep GitHub as the project fact source.
2. Update relevant blueprint, memory, roadmap, status, current-task, and review-package summary files when direction changes.
3. Commit source and documentation changes to the WebMuse repository.
4. Do not commit `bin/`, `obj/`, generated output, local logs, screenshots, recordings, extracted frames, zips, customer materials, secrets, tokens, cookies, or local absolute-path configuration.
5. Push to `wxici/WebMuse` on the correct branch when the working tree is clean and the task state is accurately recorded.
