# CODEX GLOBAL RULES

These rules keep WebMuse repository work traceable across Codex sessions.

## Startup

Before changing application code or repository structure, read these files in order:

1. `CODEX_GLOBAL_RULES.md`
2. `README_FOR_CODEX.md`
3. `PROJECT_STATE.md`
4. `docs/project-memory/CODEX_CHECKPOINT.md`

If any file is missing, create a minimal accurate version before broad edits.

## Scope

For the OSS readiness round, keep work limited to public repository foundation, safe source migration, documentation, CI/build verification, and migration audit records.

Do not implement or expand:

- real Codex CLI execution
- OpenAI, Ollama, or LM Studio calls
- website generation
- WebView2 integration
- UI redesign
- tuning panels
- drag-and-drop editing
- namespace-wide or solution-wide renames

## Public Positioning

Use `WebMuse` as the public product name.

Some internal source names still use `WebRebuildRecorder`; keep those names unless a future dedicated rename task is approved.

Do not describe WebMuse as a clone, copy, imitation, or page-editor tool. It is an early-alpha workbench for safer reference-style website reconstruction workflows.

## Checkpoints

Update `docs/project-memory/CODEX_CHECKPOINT.md` during multi-file edits, migrations, verification failures, or task completion.

Do not mark work complete unless build/test status and known risks are recorded.

## Repository Hygiene

Do not commit customer materials, recordings, screenshots, extracted frames, logs, review zips, generated output sites, secrets, local machine configuration, API keys, tokens, cookies, or OpenAI/Codex login files.
