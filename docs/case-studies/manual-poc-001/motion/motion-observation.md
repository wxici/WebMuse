# Motion Observation - Manual PoC 001

This file records motion evidence from the Manual PoC 001 video observation package.

## Why Motion Evidence Matters

Static screenshots can show first-screen composition, but they cannot validate interaction-heavy website reconstruction.

Motion evidence is needed for:

- menu reveal rhythm;
- hover or menu behavior;
- scroll pacing;
- dark/light section transitions;
- image mood changes;
- card movement;
- timing and easing;
- live visual tuning;
- whether tuning changes can be previewed immediately.

Video is the raw source. Frame strips and observation notes are engineering evidence.

## Source Videos

| File | Purpose | Public boundary |
| --- | --- | --- |
| `01-reference-motion-observation-original-speed.mp4` | Reference motion observation | Observation only; not a clone target. |
| `02-codex-first-draft-motion-original-speed.mp4` | Codex first draft motion | Shows broad direction and remaining issues. |
| `03-tuned-result-motion-original-speed.mp4` | Tuned result motion | Shows improved interaction rhythm and brand expression. |
| `04-live-tuning-preview-original-speed.mp4` | Live tuning preview | Shows manual PoC validation variables, not enabled product UI. |

## 01 Reference Motion - Observation Only

Observed patterns:

- large white hero typography;
- sticky CTA;
- right-side menu reveal;
- dark/light mood changes;
- scroll-driven section transitions;
- service lists and image cards;
- large final brand/closing sections.

Boundary:

This reference motion is used only to study layout rhythm, whitespace, typography scale, image mood, menu behavior, scroll pacing, and interaction direction.

It is not a clone target.

## 02 Codex First Draft Motion

Observed result:

- captures broad motion direction;
- includes hero, menu, dark/light state, scroll sections, cards, and footer;
- menu overlay is visually heavy;
- section rhythm is uneven;
- dark/light transition needs refinement;
- image/card treatment is still rough;
- typography scale and information density need review.

This proves that Codex can generate a first direction, but not a final delivery-quality motion system without structured observation and tuning.

## 03 Tuned Result Motion

Observed improvement:

- stronger and more stable black/white split hero;
- more controlled menu state;
- cleaner scroll rhythm;
- clearer section structure;
- more coherent image mood;
- stronger brand-specific expression.

This shows the value of manual tuning and future WebMuse tuning overrides.

## 04 Live Tuning Preview

Observed evidence:

- the Design Tune panel exposes visual variables during live preview;
- variables include background, text color, opacity, content width, section spacing, padding, and image behavior;
- the in-page overlay proves parameterized tuning is valuable;
- the same overlay also proves why future tuning should move into an external WPF floating tuning window.

The tuning overlay shown here is a manual PoC validation tool, not enabled product UI.

## Future Workflow

The intended future role is:

```text
video / interaction recording
  -> key frame extraction
  -> motion evidence strip
  -> motion-observation.md
  -> Codex construction or repair instructions
  -> generated-site motion validation
```

WebMuse should treat motion evidence as part of the project evidence chain, not as a temporary workaround.
