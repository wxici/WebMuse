# WebMuse Asset Slot Map + Motion Slot Map Blueprint Addendum

## Purpose

This addendum defines the required evidence layer between controlled Source Snapshot analysis and any real Codex website generation.

The first-stage observation package must become a machine-readable construction plan. Recording, frame extraction, and screenshots remain useful, but they are supporting evidence for motion, video, hover, scroll, canvas, WebGL, masks, and other interactions that cannot be explained reliably by the controlled source evidence.

## Required Strategic Pipeline

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

The reference site is a sample of structure, aesthetic parameters, asset requirements, and motion semantics. It is not a reusable asset library or a default source-code base.

## Reference-Observed Default Policy

Reference images, videos, logos, fonts, SVG files, copy, brand names, and proprietary CSS/JavaScript implementations are analysis evidence only by default.

```json
{
  "sourceKind": "reference_observed",
  "safeUsage": "reference_only",
  "replacementRequired": true,
  "canExport": false
}
```

The system may analyze dimensions, position, aspect ratio, colors, brightness, contrast, visual role, crop behavior, safe text-overlay areas, video duration, looping, muted state, and motion intensity. It must not treat that analysis as permission to copy the source material into final output.

## Required Stage Gate

`P2A-1.3 Asset Slot Map + Motion Slot Map + Generation Brief` must be completed before `P2A-2-B Controlled Codex CLI Single Page Generation`.

P2A-1.3 is an evidence-package implementation stage. It must not:

- execute Codex CLI or any `codex` command;
- call OpenAI API;
- call local model engines;
- generate a website;
- write `output-site/current/index.html`;
- recursively crawl a reference site;
- download original binary reference images, videos, or fonts.

P2A-2-B remains blocked until P2A-1.3 passes:

- solution build;
- FoundationSelfTest;
- review package verification;
- sample structured-output verification.

## Asset Slot Map v1

Asset Slot Map describes what the branded output needs, not what the reference site owns.

It serves three purposes:

1. Constrain later page generation.
2. Tell ordinary users what assets to upload.
3. Define boundaries for future AI-generated image or video replacements.

Minimum v1 slot:

```json
{
  "slotId": "home.hero.background.01",
  "type": "image | video | logo | svg | icon | background | pattern | unknown",
  "sectionId": "home.hero",
  "sourceUrl": "",
  "sourceKind": "reference_observed | user_upload | ai_generated | stock | unknown",
  "safeUsage": "reference_only | user_authorized | ai_generated | stock_licensed",
  "replacementRequired": true,
  "canExport": false,
  "renderedSize": {
    "width": 0,
    "height": 0
  },
  "aspectRatio": "",
  "position": {
    "x": 0,
    "y": 0,
    "width": 0,
    "height": 0
  },
  "cssFit": "cover | contain | fill | none | unknown",
  "dominantColors": [],
  "brightness": "dark | medium | light | unknown",
  "contrast": "low | medium | high | unknown",
  "visualRole": "hero background | product image | service card | decorative | logo | video background | unknown",
  "contentGuess": "",
  "textOverlaySafe": false,
  "cropRisk": "low | medium | high | unknown",
  "userHint": "",
  "aiPrompt": "",
  "notes": []
}
```

Rules:

- `reference_observed` defaults to `canExport = false`.
- `reference_observed` defaults to `replacementRequired = true`.
- `userHint` uses plain language to explain the required upload.
- `aiPrompt` defines size, aspect ratio, tone, composition, whitespace, crop safety, and visual role for a future generated replacement.
- Unknown facts must be recorded as `unknown` or left empty according to schema rules. Do not invent certainty.
- A user-authorized replacement must have explicit provenance before export eligibility changes.

## Design Token Maps v1

Observed CSS, font, spacing, radius, shadow, and color relationships must be normalized into structured maps rather than left to unconstrained AI interpretation.

Required files:

```text
maps/color-tokens.json
maps/typography-tokens.json
maps/spacing-tokens.json
maps/layout-rules.json
```

Minimum color token fields:

```json
{
  "backgroundBase": "",
  "surfaceBase": "",
  "primaryText": "",
  "secondaryText": "",
  "accent": "",
  "buttonPrimary": "",
  "buttonPrimaryText": "",
  "borderSubtle": "",
  "observedPalette": [],
  "confidence": "low | medium | high | unknown"
}
```

Minimum typography token fields:

```json
{
  "headingFontFamily": "",
  "bodyFontFamily": "",
  "h1SizeRange": "",
  "h2SizeRange": "",
  "bodySizeRange": "",
  "fontWeightPattern": "",
  "lineHeightPattern": "",
  "confidence": "low | medium | high | unknown"
}
```

Minimum spacing token fields:

```json
{
  "sectionPaddingY": "",
  "sectionPaddingX": "",
  "cardGap": "",
  "gridGap": "",
  "heroHeight": "",
  "density": "compact | balanced | spacious | unknown",
  "confidence": "low | medium | high | unknown"
}
```

Minimum layout rule fields:

```json
{
  "sectionOrder": [],
  "heroPattern": "",
  "leftRightComposition": "",
  "cardGridPattern": "",
  "mediaTextRelationship": "",
  "desktopMobileDifferences": [],
  "confidence": "low | medium | high | unknown"
}
```

Observed font family names may be recorded as evidence, but protected or unlicensed font files must not be copied into final output.

## Motion Slot Map v1

Motion Slot Map captures motion meaning and visible behavior. It does not authorize copying source CSS or JavaScript implementation code.

Required transformation:

```text
analyze source motion semantics
  -> create Motion Slot Map
  -> select or derive WebMuse-owned motion presets
```

Minimum v1 motion slot:

```json
{
  "motionId": "hero.title.reveal",
  "targetSlot": "hero.title",
  "trigger": "page-load | scroll | hover | click | loop | unknown",
  "motionRole": "primary reveal | supporting reveal | hover feedback | background ambience | transition | unknown",
  "observedPattern": "fade-up | fade-in | slide-left | scale-in | parallax | sticky | mask-wipe | video-background | unknown",
  "direction": "up | down | left | right | none | unknown",
  "distancePx": 0,
  "durationMs": 0,
  "delayMs": 0,
  "easing": "",
  "staggerMs": 0,
  "intensity": "low | medium | high | unknown",
  "implementationSafety": "safe | derived | risky",
  "sourceEvidence": [],
  "notes": []
}
```

Safety classes:

- `safe`: Generic behavior such as fade-up, fade-in, hover lift, scale-in, or ordinary parallax. Use a WebMuse-owned preset.
- `derived`: Preserve the motion meaning but rewrite the implementation. Examples include scroll triggers, section pinning, mask reveals, or staggered cards.
- `risky`: Highly distinctive motion such as original WebGL/canvas effects, brand logo animation, complex SVG path animation, or paid-template signature timelines. Record only high-level intent and do not reproduce identifying details.

## Motion Variants v1

Each motion slot should generate two or three WebMuse-owned alternatives where practical.

```json
{
  "motionId": "hero.title.reveal",
  "variants": [
    {
      "variantId": "calm-fade-up",
      "label": "Calm business fade-up",
      "durationMs": 700,
      "distancePx": 20,
      "easing": "ease-out",
      "intensity": "low"
    },
    {
      "variantId": "premium-blur-reveal",
      "label": "Premium blur reveal",
      "durationMs": 950,
      "distancePx": 12,
      "blurPx": 8,
      "easing": "cubic-bezier(0.16,1,0.3,1)",
      "intensity": "medium"
    },
    {
      "variantId": "dynamic-slide-in",
      "label": "Dynamic slide-in",
      "durationMs": 850,
      "direction": "left",
      "distancePx": 48,
      "intensity": "high"
    }
  ]
}
```

Variants must remain compatible with reduced-motion handling and must not depend on copied source implementation code.

## Generation Brief Requirements

`analysis/generation-brief.md` must convert the evidence maps into bounded construction instructions. It should include:

- page and section order;
- layout and responsive rules;
- content and interaction hierarchy;
- required asset slots and replacement status;
- color, typography, spacing, and layout token summaries;
- selected motion slots and allowed variant choices;
- evidence references and confidence;
- explicit unknowns and unresolved gaps;
- final-output restrictions for reference-observed assets, copy, logos, fonts, and code;
- required user-owned, authorized, stock-licensed, or AI-generated replacements.

The brief must not paste proprietary source code or claim unsupported certainty.

`analysis/motion-brief.md` must summarize motion roles, triggers, intensity, safety classification, reduced-motion expectations, and recommended WebMuse-owned variants.

## Legal Risk Report Requirements

`analysis/legal-risk-report.md` must identify:

- all `reference_observed` assets;
- export-ineligible images, videos, logos, fonts, SVG files, copy, and brand identifiers;
- proprietary CSS/JavaScript implementation risks;
- risky or highly distinctive motion patterns;
- replacement requirements;
- missing provenance or authorization;
- blockers that must be cleared before export.

The report is an engineering boundary record, not legal advice.

## P2A-1.3 Structured Output

The next implementation round should produce:

```text
maps/
  section-map.json
  asset-slots.json
  color-tokens.json
  typography-tokens.json
  spacing-tokens.json
  layout-rules.json
  motion-slots.json
  motion-variants.json
  interaction-map.json

analysis/
  generation-brief.md
  motion-brief.md
  legal-risk-report.md
```

Existing files should be reused, extended, or transformed instead of duplicated:

```text
analysis/media-placement-map.json
analysis/behavior-map.json
analysis/animation-signal-map.json
analysis/ai-reconstruction-brief.md
```

## Expected Review Package

The P2A-1.3 implementation review should include:

```text
review_package/p2a-1-3-asset-motion-generation-brief/
  summary.md
  changed-files.txt
  schema-and-output-check.md
  sample-output-verification.md
  build-and-self-test.md
  legal-boundary-check.md
  next-codex-implementation-entry.md
```

It must record:

- exact build and FoundationSelfTest results;
- sample output paths and schema checks;
- whether existing evidence files were reused;
- whether any reference-observed resource became exportable;
- confirmation that no Codex CLI, API, local model, or website generation ran;
- the remaining gate before P2A-2-B.

## Next Codex Implementation Instruction

The next implementation round must start:

```text
P2A-1.3 Asset Slot Map + Motion Slot Map + Generation Brief
```

Do not start P2A-2-B yet.
