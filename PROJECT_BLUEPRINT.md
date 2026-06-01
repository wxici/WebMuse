# WebRebuildRecorder Project Blueprint

## Long-Term Product Positioning

WebRebuildRecorder is evolving into a Codex CLI driven AI branded website reconstruction console and construction package generator.

It is not positioned as a screen recorder, frame extraction utility, website watcher, clone tool, general webpage editor, drag-and-drop builder, or generic AI site builder.

The long-term product value is:

- The user provides a reference website, brand material, logo, assets, copy, color preferences, and subjective aesthetic requirements.
- The software analyzes the reference site's structure, rhythm, whitespace, type scale, motion, color relationship, and image atmosphere.
- The software translates those characteristics into the user's own branded website.

Expected output:

1. A previewable finished website.
2. A generated result that supports light tuning.
3. A project that can be saved, rolled back, reopened, and modified.
4. An exportable static website delivery package.

Preferred external wording:

- Reference style generation
- Branded reconstruction
- Aesthetic analysis
- Construction package generation
- Generated result calibration
- AI site construction console

Avoid external wording:

- clone
- imitation site
- copied site
- replicated site
- webpage editor
- drag-and-drop builder

## Role Of Existing Features

Existing recording, frame extraction, observation package, GPT/Codex package generation, and related workflows must not be deleted.

Their long-term role is downgraded from main product to submodules for:

- Reference site observation
- Local correction of complex motion
- Construction package preparation

Typical use cases:

1. Codex observation is inaccurate.
2. Hover motion is inaccurate.
3. Image brightness or dark/light switching is inaccurate.
4. Crescent or mask direction is inaccurate.
5. Scroll reveal timing is inaccurate.
6. Video or image trigger effects require local frame analysis.

## Main Product Workflow

The intended closed loop is:

1. User enters all project input in one pass.
2. Software collects or organizes reference-site observation data.
3. Software copies and normalizes user assets.
4. Codex/AI generates a structured observation report and construction package.
5. Software internally assembles a Codex CLI task package.
6. Codex CLI runs as a black box inside a project sandbox.
7. The sandbox outputs a static single-page or static multi-section website.
8. Software previews the output with WebView2 or an embedded browser.
9. User lightly tunes the result through an aesthetic calibration panel.
10. Software saves `tune-overrides.css`, `theme.json`, and `content-map.json`.
11. User validates the result.
12. Software exports `output-site.zip`.
13. User can reopen the project later, modify assets, copy, or color, and regenerate.

## MVP Hard Boundaries

The first MVP supports only static single-page or static multi-section branded websites.

Do not expand the first stage into:

1. CMS
2. E-commerce
3. Database backend
4. Payment
5. Login or registration
6. Membership
7. Message backend
8. Forum
9. Complex multi-page management
10. Free-form drag-and-drop page editor
11. Animation timeline
12. Complex component marketplace

If a user requests these items during the MVP stage, record the request but do not implement it.

## Main UI Principles

1. The central WebView or embedded browser is the largest core stage.
2. The top task area controls project-level flow: new, open, input, observe, generate, tune, validate, export, settings, and run status.
3. The left workflow area handles input and preparation: reference URL, brand data, logo, copy, assets, subjective user notes, liked regions, target motion, prohibited items, and task steps.
4. The right property area handles the current element and generated result: current element tuning, section navigation, asset catalog, `content-map`, `data-tune-id`, CSS variables, and log entry points.
5. The bottom global theme/color area is collapsible by default: color system, custom colors, miniature cards, brightness, contrast, and section spacing.
6. The interface switches by mode: preparation, observation/generation, tuning, and validation/export.
7. Visibility has three levels:
   - Normal users see workflow and results.
   - Advanced users see asset diagnostics and visual parameters.
   - Developer mode shows file tree, CSS variables, `content-map`, Codex logs, and task packages.
8. The UI is not a webpage editor, IDE, or Visual Studio style development environment.

## Docking And Panel Boundaries

Limited docking may be considered later, but it must not be implemented in P0.

Design principles:

1. The central WebView remains the fixed primary stage.
2. The left workflow panel can only snap back to the left.
3. The right property panel can only snap back to the right.
4. The bottom theme panel can only snap back to the bottom.
5. The top task area stays fixed and should not float.
6. Panels may close, collapse, float, and restore default layout.
7. Do not build an arbitrary multi-direction, multi-level, multi-tab docking system.
8. Start with fixed layout plus collapse/hide. Add limited floating and auto-snap only later.

## Color System Principles

1. Codex first analyzes the reference site's primary color system.
2. Represent the palette with 3 to 4 vertical swatches:
   - Main background color
   - Main text color
   - Weak text or support color
   - Accent color
3. The software extends 4 to 5 candidate palettes based on the reference site's lightness, contrast, temperature, and saturation:
   - Original reference palette
   - Same-lightness cool variant
   - Same-lightness warm variant
   - High-contrast luxury variant
   - Low-saturation natural skin-tone variant
4. Each candidate palette has a card with 3 to 4 swatches, a miniature website structure card, and a palette name.
5. The top of the module shows current selection and custom palette.
6. The default palette is the reference site's palette.
7. Clicking a candidate synchronizes it to the current selection.
8. Each swatch in the current selection can open the system color picker.
9. The custom palette area must include a live miniature website structure card.
10. Palette output must be written to:
    - `theme.json`
    - `style-constraints.md`
    - Initial CSS variables
11. Codex construction and WebView2 tuning must honor the current palette.

## Miniature Card Text Rules

Miniature cards express color atmosphere only. They are not final website previews.

Allowed structure:

- header
- hero
- image block
- text block
- button
- section
- footer

Text source priority:

1. Brand name
2. Main slogan or short tagline
3. Business keywords
4. Industry default copy
5. Generic placeholder text

Text length rules:

1. Large Chinese text: no more than 3 Chinese characters.
2. Large English text: roughly 6 to 8 visible characters.
3. Small Chinese text: no more than 7 Chinese characters.
4. Small English text: no more than 14 characters.
5. Codex may semantically extract short terms.
6. Program code must enforce final length limits, truncation, ellipsis, and display safety.

## Tuning Panel Boundary

The post-generation tuning panel is not a webpage editor.

Its role is AI-generated aesthetic calibration, also called Design Tune or theme parameter tuning.

Normal mode may adjust only high-impact parameters:

1. Page brightness
2. Page contrast
3. Main title size
4. Subtitle size
5. Section spacing
6. Image brightness
7. Image grayscale
8. Image contrast
9. Button size
10. Mobile title size

Advanced mode may additionally expose:

- font family
- font weight
- line height
- letter spacing
- opacity
- image radius
- image position
- section spacing

Developer mode may expose:

- `data-tune-id`
- CSS selector
- CSS variable
- current value
- new value
- source file
- `content-map`
- logs

Do not add early:

- free dragging
- arbitrary component insertion
- complex layers
- animation timeline
- CMS
- e-commerce
- backend

Tuning must save to `css/tune-overrides.css` or `css/theme.generated.css`, and `index.html` must load it last. Do not directly destroy the original main CSS.

## UI Icon Resource System

Future versions should establish a unified resource system for the application logo, ICO files, and button icons.

Icon style requirements:

- simple
- technology-oriented
- artistic
- bright in color
- clearly recognizable at small sizes

Resource storage:

- Prefer `Resources/Icons/` for application source resources.
- Use `assets/icons/` for project or generated website icon assets when appropriate.
- Prefer SVG as the primary source format.

The icon system should include `icons-manifest.json` to record:

1. usage
2. path
3. size
4. source
5. license or origin status when applicable

First-stage boundary: only establish the directory and specification when a future task explicitly authorizes it. Do not let icon work take priority over the P0 engineering foundation.

## Project Persistence And File Format

The core project unit is not a one-time webpage file. It is a website engineering project that can be opened, modified, regenerated, and rolled back.

Working state uses a project folder plus `project.wrbproj`.

Do not use a PSD/AI-like monolithic opaque binary file.

Recommended project structure:

```text
project.wrbproj
PROJECT_STATUS.md
input/
assets/
theme/
observation/
codex-task/
output-site/
tune/
maps/
exports/
logs/
versions/
```

`project.wrbproj` is similar to `.sln` or `.csproj` and records:

1. `schemaVersion`
2. `appVersion`
3. `projectId`
4. `projectName`
5. `referenceUrl`
6. `state`
7. `currentOutputVersion`
8. `paths`
9. `createdAt`
10. `updatedAt`
11. current theme
12. current content map
13. Codex execution status
14. version history index

Three output forms:

1. Development/editing state: project folder plus `project.wrbproj`
2. Backup/migration state: `.wrb` or `.wrbpkg`, which is a zip archive
3. Delivery/deployment state: `output-site.zip`, containing only final website files and no engineering history, logs, or construction package

## Rollback Mechanism

Before each regeneration, the current version must be snapshotted.

Recommended layout:

```text
output-site/current/
output-site/versions/v001/
output-site/versions/v002/
output-site/versions/v003/
```

Rollback must not restore only `output-site`.

Each version snapshot must save:

1. `VERSION_INFO.json`
2. `CHANGELOG.md`
3. output-site files
4. `theme.snapshot.json`
5. `content-map.snapshot.json`
6. `project-input.snapshot.json`
7. `assets-manifest.snapshot.json`
8. `tune-overrides.snapshot.css`
9. codex-task snapshot

Do not duplicate original assets for every version. Keep originals in `assets/original`. Version snapshots should record asset IDs and usage relationships, saving generated, cropped, or processed images only when needed.

## Project State Machine

The project cannot rely on verbal stages only. It must use a `ProjectState` state machine.

Minimum states:

- `ProjectCreated`
- `InputCompleted`
- `AssetsCopied`
- `ThemeSelected`
- `ObservationReady`
- `TaskPackageGenerated`
- `CodexRunning`
- `CodexFailed`
- `CodexCompleted`
- `OutputValidated`
- `PreviewReady`
- `TuneDirty`
- `TuneSaved`
- `ExportReady`
- `Exported`
- `Archived`

Each state must eventually define:

1. Entry conditions
2. Exit conditions
3. Allowed operations
4. Forbidden operations
5. Failure rollback point
6. State-file write content

## Project Lock

When Codex is running or a project is generating, the project must be locked to prevent simultaneous asset edits, palette switching, or repeated generate clicks.

`project.lock` must record:

1. `taskId`
2. `taskType`
3. `startedAt`
4. `processId`
5. `status`
6. `allowCancel`
7. `recoveryHint`
8. `abnormalExit`

## Codex CLI Black-Box Boundary

Normal users must not manually copy complex Markdown or prompts.

The software internally assembles a Codex CLI task package from user input, assets, observation rules, brand constraints, copy mapping, asset diagnostics, and validation requirements.

Normal user view:

```text
input -> generating -> preview -> tune -> export
```

Advanced/developer view may show:

- observation package
- construction package
- `content-map.json`
- `tune-overrides.css`
- Codex logs
- interruption and recovery Markdown

Product documentation must clearly state that Codex CLI calls depend on the user's own OpenAI/Codex account, quota, login state, network environment, and remote service stability.

If the process disconnects, hangs, times out, runs out of quota, or fails authentication, the software must show clear error text, retry entry points, and responsibility boundaries.

## Codex Task Orchestration And Failure Classification

Do not treat Codex CLI as a stable function call.

Handle it as task orchestration:

1. Create task.
2. Write task package.
3. Lock sandbox.
4. Start process.
5. Watch output.
6. Check periodically.
7. Stop on timeout.
8. Save logs.
9. Verify result.
10. Classify failure.
11. Allow retry.

Future interfaces may include:

- `ICodexTaskOrchestrator`
- `ICodexProcessRunner`
- `ICodexOutputWatcher`
- `ICodexResultVerifier`
- `ICodexFailureClassifier`

Failures to recognize:

- disconnection
- hang
- timeout
- authentication failure
- quota shortage
- partial generation
- incomplete output
- returned success with broken page

## Sandbox Permissions And File Safety

Codex may operate only inside the project sandbox.

Allowed write areas:

```text
projects/{projectId}/codex-workspace/
projects/{projectId}/output-site/current/
projects/{projectId}/logs/
```

Forbidden write areas:

1. Software installation directory
2. Source repository root
3. Other project directories
4. User system directories
5. OpenAI/Codex credential directories
6. Plugin directories
7. Any unauthorized absolute path

Must prevent:

- `../` path traversal
- Chinese path issues
- spaces in paths
- long path issues
- slash/backslash confusion
- OneDrive permission issues

## Content Map And Data Tune ID Stability

Page generation must produce `content-map` and stable `data-tune-id` values.

Example IDs:

```text
hero.title
hero.subtitle
hero.cta.primary
section.services.card.01.title
footer.contact.phone
```

Required validation:

1. Whether IDs in `content-map` exist in the current DOM.
2. Whether the DOM contains unmapped generated elements.
3. Whether old IDs are invalid.
4. Whether copy mapping is misplaced.

Future components may include:

- `ContentMapValidator`
- `TuneIdScanner`
- `MissingMappingReporter`

## Assets And Copyright

Assets are not simple uploads. The software and AI should assess whether assets are too pink, too green, too bright, too promotional, too cheap-looking, or in conflict with black/white/gray premium style or the current brand palette.

`assets-manifest.json` should record:

```json
{
  "sourceType": "user_upload | ai_generated | stock | reference_observed",
  "licenseStatus": "user_confirmed | unknown | restricted",
  "canExport": true
}
```

Reference-site images, copy, logos, and distinctive graphics must not be copied by default. The user must confirm authorization. AI-generated assets must be marked with their source.

## Observation Failure Fallback

Reference-site observation may fail because of:

- Cloudflare
- login walls
- regional restrictions
- cookie popups
- lazy loading
- unplayable video
- cross-origin restrictions
- mobile/desktop differences
- anti-automation checks

Fallback path:

```text
automatic observation failed
-> user manually screenshots, records, or pastes images
-> user marks liked regions
-> Codex analyzes artificial material
```

Liked-region image paste support is part of the observation failure fallback.

## Pre-Export Integrity And Secret Checks

Before export, verify:

1. `index.html` exists.
2. CSS exists.
3. JS exists when needed.
4. Images are not missing.
5. `theme.json` exists.
6. `content-map.json` exists.
7. `tune-overrides.css` exists and is loaded.
8. `README_DEPLOY.md` exists.
9. No local absolute paths are included.
10. No API keys, tokens, cookies, account names, or passwords are included.
11. No private user data is included.
12. No paths such as `C:\Users\...` or `E:\Work\...` are referenced.

## Log Layers

Do not use a single `log.txt`.

Minimum logs:

- `app.log`
- `codex-run.log`
- `build.log`
- `validation.log`
- `export.log`
- `error.log`

Normal users see summaries. Advanced/developer mode can show full logs.

## Environment Detection

At startup or before first execution, detect:

1. WebView2 Runtime
2. Codex CLI
3. OpenAI/Codex login state
4. Network connectivity
5. Project directory write permission
6. Disk space
7. File path length
8. System proxy state

## Plugin Boundary

Use a stable core plus interface layers and pluggable extensions.

Core interfaces may include:

- `IProjectService`
- `IAssetService`
- `IObserverService`
- `IPromptBuilder`
- `ICodexRunner`
- `IResultValidator`
- `ITuneService`
- `IExportService`
- `ILogService`

Prioritize plugin-like boundaries for high-change modules:

1. Reference-site observer
2. AI executor
3. Asset analyzer
4. Exporter
5. Validator
6. Industry template packs
7. Tuning parameter packs

Do not pluginize early:

1. Project state management
2. File directory structure
3. Security sandbox
4. Basic logging
5. Interruption recovery
6. Core UI flow

First stage target is quasi-pluginization only: interfaces, configuration, and service registration.

Do not support early:

- third-party DLL hot loading
- arbitrary plugin code execution
- plugin modification of the main UI flow

## P0-P5 Roadmap

P0: establish project directories, `project.wrbproj`, `schemaVersion`, `appVersion`, `ProjectState`, `project.lock`, sandbox boundaries, environment detection notes, and Codex dependency warnings.

P1: implement durable input material, asset copy pipeline, `assets-manifest.json`, `theme.json`, `content-map`, stable `data-tune-id`, version snapshot foundation, and layered logs.

P2: build the main UI skeleton around the central preview stage, WebView2 preview, miniature palette cards, construction package generation, and Codex task orchestration foundation.

P3: stabilize Codex CLI black-box execution, output validation, lightweight tuning, `tune-overrides.css`, content-map validation, and observation failure fallback.

P4: add export integrity checks, sensitive information checks, automated validation reports, local recording/frame correction workflows, industry template packs, sample projects, and archive packages.

P5: complete product polish, plugin manifests, local section regeneration, multi-version comparison, and production hardening.

## Project Directory V2

Future AI reconstruction projects should use this directory standard:

```text
project.wrbproj
input/
assets/
assets/original/
assets/selected/
theme/
observation/
observation/screenshots/
observation/dom/
codex-task/
output-site/
output-site/current/
output-site/versions/
tune/
maps/
exports/
logs/
review/
runtime/
codex-workspace/
```

This round only defines the V2 standard. It does not migrate existing `project.json` projects or delete legacy folders.

## P0 Manifest And Lock Rules

`project.wrbproj` must be a readable JSON project manifest with `schemaVersion`, `appVersion`, `projectId`, `projectName`, `referenceUrl`, `state`, `currentOutputVersion`, `paths`, `features`, `lastCodexTask`, and `lastExportPath`.

`project.lock` protects long-running generation tasks. When Codex or a generator is running, the app must prevent repeated generate clicks, concurrent asset edits, and unsafe state switching. A lock records `taskId`, `taskType`, `startedAt`, `processId`, `status`, `allowCancel`, `recoveryHint`, and abnormal-exit information.

## Layered Logs

The minimum log channels are:

- `app.log`
- `codex-run.log`
- `build.log`
- `validation.log`
- `export.log`
- `error.log`

Normal users see concise summaries. Advanced and developer modes may expose full logs.

## Round Construction Rules

Every Codex construction round should stay around a 5 hour budget and must have:

1. `CURRENT_TASK.md`
2. `review_package/`
3. `SELF_CHECK_REPORT.md`
4. `FILES_CHANGED.md`
5. build verification
6. a statement on whether legacy behavior was changed
7. a source package excluding generated workspaces, binaries, historical zips, secrets, and private data

Do not mark a round fully complete without verification evidence.
- plugin system-command execution

## Local Long-Term Memory And Review Package Rule

Every future Codex construction round and GPT review must carry the local long-term memory files. The required files are:

- `PROJECT_MEMORY_INDEX.md`
- `docs/project-memory/PROJECT_MEMORY_FULL.md`
- `PROJECT_BLUEPRINT.md`
- `CODEX_PROJECT_MEMORY.md`
- `CODEX_CONSTRUCTION_RULES.md`
- `PROJECT_ROADMAP.md`
- `PROJECT_STATUS.md`
- `CURRENT_TASK.md`
- `REVIEW_PACKAGE_RULES.md`

Every `review_package` and source review zip must include these files. If `PROJECT_MEMORY_INDEX.md` or `docs/project-memory/PROJECT_MEMORY_FULL.md` is missing, the package is incomplete and should not be treated as review-ready.

`PACKING_MANIFEST.md` must explicitly list whether each required memory file is included or missing.
