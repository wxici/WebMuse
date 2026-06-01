# Review Package Rules

## Purpose

Each construction round must leave a review package so the next reviewer or Codex session can understand what changed, what was tested, what failed, and what should happen next.

The review package is local project memory, not a marketing summary.

## Required Directory

Use:

```text
review_package/
```

The directory may be replaced or updated each round, but useful historical context should not be deleted without a reason.

## Required Files

Each round must include at least:

1. `CHANGELOG_THIS_TASK.md`
2. `SELF_CHECK_REPORT.md`
3. `FILES_CHANGED.md`
4. `BUILD_AND_RUN_REPORT.md`
5. `ERRORS_AND_RISKS.md`
6. `NEXT_STEPS.md`
7. `ARCHITECTURE_DECISIONS.md`
8. `PACKING_MANIFEST.md`

## 本地长期记忆文件强制携带规则

每次生成 review_package 和源码审核 zip 时，必须包含：

- `PROJECT_MEMORY_INDEX.md`
- `docs/project-memory/PROJECT_MEMORY_FULL.md`
- `PROJECT_BLUEPRINT.md`
- `CODEX_PROJECT_MEMORY.md`
- `CODEX_CONSTRUCTION_RULES.md`
- `PROJECT_ROADMAP.md`
- `PROJECT_STATUS.md`
- `CURRENT_TASK.md`
- `REVIEW_PACKAGE_RULES.md`

如果遗漏 `PROJECT_MEMORY_INDEX.md` 或 `docs/project-memory/PROJECT_MEMORY_FULL.md`，则本轮 review_package 视为不完整，GPT 审核时应要求补包。

`PACKING_MANIFEST.md` 必须明确列出上述文件是否已包含。

## Conditional Files

If UI changed, include:

- screenshots
- screenshot index or notes
- visual QA notes

If a generated website output exists, include:

- export integrity report
- sensitive information check
- missing asset report
- preview notes

If Codex CLI was run, include:

- task package summary
- Codex run log summary
- failure classification if applicable
- recovery notes

## File Expectations

`CHANGELOG_THIS_TASK.md` must say what changed and what did not change.

`SELF_CHECK_REPORT.md` must include a checklist of scope, old behavior preservation, and verification.

`FILES_CHANGED.md` must list modified, added, deleted, generated, and packaged files.

`BUILD_AND_RUN_REPORT.md` must include exact commands, result, warnings/errors, and any run limitations.

`ERRORS_AND_RISKS.md` must list known unresolved issues and risks.

`NEXT_STEPS.md` must state the recommended next round and any prerequisites.

`ARCHITECTURE_DECISIONS.md` must record important decisions and non-decisions.

`PACKING_MANIFEST.md` must record package name, included categories, excluded categories, and validation result.

Every `PACKING_MANIFEST.md` must include this checklist:

```md
## Required project memory files

- [ ] PROJECT_MEMORY_INDEX.md
- [ ] docs/project-memory/PROJECT_MEMORY_FULL.md
- [ ] PROJECT_BLUEPRINT.md
- [ ] CODEX_PROJECT_MEMORY.md
- [ ] CODEX_CONSTRUCTION_RULES.md
- [ ] PROJECT_ROADMAP.md
- [ ] PROJECT_STATUS.md
- [ ] CURRENT_TASK.md
- [ ] REVIEW_PACKAGE_RULES.md
```

Actual packages must mark each item as included or missing.

## Packaging Rules

Source handoff zip packages must exclude:

- `bin/`
- `obj/`
- `.vs/`
- `.git/`
- `*.mp4`
- historical zip files
- FFmpeg executables
- FFmpeg archives
- large screenshots
- video caches
- API keys
- tokens
- cookies
- account/password files
- user private files

For source construction rounds, the zip should be named:

```text
WebRebuildRecorder_source-review_YYYYMMDD_HHMMSS.zip
```

## Completion Rule

Do not mark a round complete until:

1. `review_package/` is updated.
2. Build/run status is recorded.
3. Known risks are recorded.
4. Package manifest is written.
5. `CODEX_CHECKPOINT.md` has a final completion marker.
