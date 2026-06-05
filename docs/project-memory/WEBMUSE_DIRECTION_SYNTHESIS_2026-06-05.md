# WEBMUSE_DIRECTION_SYNTHESIS_2026-06-05

Purpose: record the 2026-06-05 direction reset after reviewing current progress, old-project capabilities, and market-validation pressure.

## 1. Why this synthesis exists

After P1.8-0, the project entered a temporary direction fog.

P0/P1 foundation work created real engineering value: V2 project structure, project manifests, state files, sandbox path policy, locks, rollback, readiness, dry-run, proof package, approval gate, execution precondition service, and alpha validation probe.

However, market validation pressure exposed a gap: these foundation services are mostly invisible in the running application.

The direction question is therefore not whether the foundation work was useful. It was useful. The question is whether the next round should keep deepening invisible foundation services, replay the old manual workflow, or expose a rebuilt-product advantage that can be validated in the visible workflow.

## 2. Current completed foundation

The current completed foundation includes:

- P0/P1 foundation.
- P1.5 readiness.
- P1.6 dry-run.
- P1.7.1 proof-check package.
- P1.7.2 approval gate.
- P1.7.3 execution precondition service.
- P1.8-0 alpha validation probe.

These components make the future construction workflow safer and more explainable.

They establish project structure, package contracts, validation gates, blocked-but-explainable reporting, and a non-execution boundary.

These components do not yet prove market value by themselves because they do not expose the structural advantages of the rebuilt product in the visible workflow.

## 3. Old project capability that must not be ignored

The old WebRebuildRecorder workflow already supported a manual loop:

```text
recording/frame extraction/observation package
-> manual GPT analysis
-> Codex construction package
-> generated website zip.
```

This matters because a new Alpha cannot claim product progress if it only repeats this old manual path with newer internal documents.

The old workflow already proved that a human can collect evidence, ask GPT/Codex for analysis, package instructions, and obtain a website zip.

Therefore, repeating this old manual loop is not a meaningful alpha validation of the rebuilt architecture.

## 4. Why pure P1.7.4 is not the next best move

P1.7.4 failure recovery remains important, but continuing it immediately would deepen the foundation while delaying visible structural validation.

The next risk is not that the foundation has no failure policy; the next risk is that the rebuilt product still has not demonstrated capabilities that the old project did not already provide.

Failure recovery policy should still be implemented later, because real Codex execution will need classified recovery, retry, abort, manual fallback, and readable failure reporting.

But the immediate validation pressure is different: the project must show that the rebuilt application can make the workflow structurally better, not merely safer on paper.

## 5. Why old-UI-only alpha is also insufficient

A minimal old UI bridge that only exposes readiness/dry-run/proof/approval reports is not enough.

Without WebView2 preview, Source Snapshot, controlled Codex CLI execution, output-site/current preview, color controls, or WPF tuning, the alpha experience remains too close to the old manual workflow.

An old-UI-only Alpha would prove that reports can be displayed, but not that the rebuilt product creates a materially better website reconstruction and delivery workflow.

It would still ask the user to think in terms of documents, packages, manual AI work, and zip movement instead of the target product loop: observe, generate, preview, tune, validate, export.

## 6. Structural Alpha decision

Decision:

Pause P1.7.4-A.

Do not continue old-UI-only alpha validation.

Move next into P2A Structural Alpha.

The goal is to expose the rebuilt architecture's structural value:

- embedded WebView2 preview;
- Source Snapshot MVP;
- controlled Codex CLI proof;
- controlled Codex CLI site generation into output-site/current;
- WebView2 preview of generated output;
- minimal color/tuning controls.

This does not mean the safety foundation is abandoned.

It means the next visible proof should connect the safety foundation to a product workflow that the old manual process did not already provide.

## 7. New immediate roadmap

The immediate P2A roadmap is:

1. P2A-0 WebView2 Preview Shell.
2. P2A-1 Source Snapshot MVP.
3. P2A-2 Codex CLI Proof Runner.
4. P2A-3 Codex CLI Controlled Site Generation + WebView2 Preview.
5. P2A-4 Minimal Tuning / Color Controls.

P2A-0:

Embed a minimal WebView2 preview shell into the existing WPF application. It can open reference URLs and local output-site/current/index.html. Do not rebuild the full UI.

P2A-1:

Implement minimal Source Snapshot: fetch HTML, capture rendered/resource metadata where feasible, save source-snapshot reports, and reduce reliance on recording/frame extraction.

P2A-2:

Implement a controlled Codex CLI proof runner. It only verifies Codex CLI can operate inside the sandbox and create a proof file. It must not generate a website.

P2A-3:

Implement controlled Codex CLI site generation. The software passes a bounded task package to Codex CLI, Codex writes output-site/current inside the project sandbox, and WebView2 previews index.html. Manual zip import becomes fallback only.

P2A-4:

Implement minimal tuning/color controls through WPF-side controls or a small floating tool window. It should write tune-overrides.css/json or CSS variables without turning the app into a visual editor.

## 8. Priority ranking

Immediate:

1. WebView2 preview shell.
2. Source Snapshot MVP.
3. Codex CLI proof runner.
4. Controlled Codex CLI site generation.
5. Minimal color/tuning controls.

Postponed:

- P1.7.4 failure recovery policy service.
- Design Skill / DESIGN.md asset library implementation.
- Reference site cards / Reference Portal.
- ProposalPreview / SitePitcher.
- complex color palette recommendation system.
- full asset-slot overlay system.
- full new main UI redesign.
- marketplace / CMS / CRM / mass email.

## 9. What is explicitly postponed

P2A is structural alpha, not a finished product.

It must remain static-site focused.

No CMS, database backend, payment, login, membership, e-commerce, forum, or complex page editor.

The following are postponed, not cancelled:

- P1.7.4 failure recovery policy service.
- Full failure recovery UI.
- Design Skill / DESIGN.md asset library implementation.
- Reference Portal.
- ProposalPreview / SitePitcher.
- complex visual editor features.
- broad marketplace, CMS, CRM, or mass-email features.

## 10. Repository workflow rule

WebRebuildRecorder remains the prototype source of truth.

WebMuse remains the OSS-safe result extraction repository.

Implement and verify in wxici/codex/WebRebuildRecorder first.

Only after verification, synchronize public-safe results to wxici/WebMuse.

Repository memory should continue to record which work belongs in the prototype repository and which work may be mirrored to the OSS-safe extraction repository.

## 11. Next Codex task

Next implementation task:

P2A-0 WebView2 Preview Shell.

Scope:

- add minimal WebView2 preview shell to existing WPF app;
- support opening reference URL;
- support opening local output-site/current/index.html;
- show preview status;
- do not remove old recording/frame extraction/ChatGPT package workflow;
- do not implement Source Snapshot, Codex CLI execution, tuning, color system, or full UI redesign in P2A-0.

P2A-0 should be treated as the smallest visible structural proof, not as a redesign of the application.

It should make preview capability visible while keeping the old recording/frame extraction/observation/package workflow intact.

## 12. Decision statement

Final decision:

The project exits the direction fog by moving from pure foundation work into structural alpha.

P1.7.4 is postponed, not cancelled.

The immediate goal is to prove rebuilt-project value that the old project did not already provide.

Current route:

```text
P1.8-0 complete
-> P1.8-direction-close documentation reset
-> P2A-0 WebView2 Preview Shell
-> P2A Structural Alpha sequence
```

This decision does not authorize uncontrolled execution, broad UI redesign, website generation in P2A-0, or scope creep into CMS/editor/business-platform features.
