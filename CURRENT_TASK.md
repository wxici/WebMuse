# CURRENT_TASK

## Current task

P2A-1.3 Documentation Preparation: Asset Slot Map + Motion Slot Map + Generation Brief

This round records the strategic direction and prepares the repository memory/blueprint for the next implementation round.

No code implementation is authorized in this documentation round.

## Documentation scope

This round:

- adds the Asset Slot Map + Motion Slot Map blueprint addendum;
- records the machine-readable observation pipeline;
- updates roadmap, current task, project state, project status, long-term memory, and changelog;
- creates a small documentation review package;
- commits and pushes the documentation update.

This round does not:

- modify C# source, services, models, or tests;
- execute Codex CLI;
- call OpenAI API;
- call local models;
- generate a website;
- write `output-site/current/index.html`;
- crawl reference sites or download reference images, videos, or fonts;
- modify generated output, logs, binaries, or zip artifacts.

## Next implementation task

P2A-1.3 Asset Slot Map + Motion Slot Map + Generation Brief implementation.

The next implementation round should add schema/service/test/report support for:

- `asset-slots.json`
- `motion-slots.json`
- `motion-variants.json`
- `color-tokens.json`
- `typography-tokens.json`
- `spacing-tokens.json`
- `layout-rules.json`
- `generation-brief.md`
- `motion-brief.md`
- `legal-risk-report.md`

It should also produce or preserve:

- `section-map.json`
- `interaction-map.json`
- evidence references and confidence values
- explicit `reference_observed` export restrictions
- sample structured outputs for review

Existing `media-placement-map.json`, `behavior-map.json`, `animation-signal-map.json`, and `ai-reconstruction-brief.md` evidence should be reused, extended, or transformed instead of duplicated.

It must not execute Codex CLI, call OpenAI API, call local models, generate a website, or write `output-site/current/index.html`.

## Hard boundary

P2A-2-B Controlled Codex CLI Single Page Generation is blocked until P2A-1.3 passes.
