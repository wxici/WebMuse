# Roadmap

WebMuse is early alpha. The roadmap is safety-first: real AI execution comes after project-state, sandbox, dry-run, proof-check, approval, and rollback foundations.

## P0: Project foundation and safety boundaries

Goal: establish the safe project base before product expansion.

Scope:

- project positioning;
- project folder model;
- `project.wrbproj`;
- schema versioning;
- project state machine;
- project lock;
- sandbox path boundaries;
- environment detection;
- basic logs;
- public OSS documentation.

Out of scope:

- real Codex execution;
- WebView2 preview;
- website generation;
- tuning UI;
- drag-and-drop editing.

## P1: Durable packages, manifests, logs, dry-run, rollback readiness

Goal: make project input and construction preparation reliable.

Scope:

- asset manifest;
- theme manifest;
- content map;
- observation package;
- construction package;
- Codex task package;
- dry-run records;
- rollback readiness;
- failure classification;
- manual construction package fallback.

Out of scope:

- real external AI calls;
- automatic website generation;
- visual editor features.

## P2: Preview shell, observation workflow, construction package UX

Goal: shape the product shell around reference observation, package generation, and preview.

Scope:

- main workflow shell;
- preview shell planning;
- observation workflow;
- construction package UX;
- candidate color/theme display;
- validation report surface.

Out of scope until approved:

- complex page editor;
- CMS;
- ecommerce;
- backend generation.

## P3: Controlled Codex execution, proof-checks, approval gates, tuning

Goal: enable controlled AI execution only after safety gates are stable.

Scope:

- execution preconditions;
- proof-check service;
- approval gate service;
- rollback confirmation;
- controlled Codex execution;
- output validation;
- lightweight tuning overrides.

Principle:

```text
Real AI execution comes after safety gates, not before them.
```

### Future tuning architecture

Early manual PoC evidence showed that webpage-embedded tuning overlays are useful for validation but unsuitable as final product UI because they obstruct the preview, pollute screenshots, and must be excluded from exports.

Future WebMuse tuning should use an external WPF floating tuning window connected to the WebView2 preview through controlled runtime CSS variable injection or a temporary override stylesheet. Confirmed values should persist to `tune-overrides.css`, `tune-overrides.json`, or the generated theme layer instead of destructively editing source CSS on every slider movement.

## P4: Export validation, templates, reference library, release packaging

Goal: prepare reliable delivery and public releases.

Scope:

- export integrity check;
- sensitive information scan;
- validation reports;
- sample projects;
- release packaging;
- optional reference-site library;
- optional design context library;
- industry templates.

## Long-term direction

WebMuse aims to become a safer AI-assisted website reconstruction workbench for developers, solo builders, and small-business workflows.

It should remain focused on reference-style reconstruction, brand-owned output, project safety, validation, and delivery workflows.
