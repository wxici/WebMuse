# Codex Construction Rules

## Scope Control

Each construction round must work only on the task written in `CURRENT_TASK.md`.

Do not perform opportunistic refactors, UI redesigns, WebView2 integration, Codex CLI integration, color-system work, tuning-panel work, or business-logic changes unless `CURRENT_TASK.md` explicitly authorizes them.

If a user requests out-of-phase features, record the request and explain that it is outside the current stage.

## Required Startup Flow

## 每轮开工前必读文件

每轮 Codex 施工前必须先读取：

1. `PROJECT_MEMORY_INDEX.md`
2. `docs/project-memory/PROJECT_MEMORY_FULL.md`
3. `PROJECT_BLUEPRINT.md`
4. `CODEX_PROJECT_MEMORY.md`
5. `CODEX_CONSTRUCTION_RULES.md`
6. `PROJECT_ROADMAP.md`
7. `PROJECT_STATUS.md`
8. `CURRENT_TASK.md`
9. `REVIEW_PACKAGE_RULES.md`

不得只凭聊天上下文施工。
如果这些文件缺失，应先报告缺失并停止施工，不要继续写代码。

Every round starts with:

1. Read `CODEX_GLOBAL_RULES.md`.
2. Read `README_FOR_CODEX.md`.
3. Read `PROJECT_STATE.md`.
4. Read `CODEX_CHECKPOINT.md`.
5. Read `PROJECT_MEMORY_INDEX.md`.
6. Read `docs/project-memory/PROJECT_MEMORY_FULL.md`.
7. Read `PROJECT_BLUEPRINT.md`.
8. Read `CODEX_PROJECT_MEMORY.md`.
9. Read `CODEX_CONSTRUCTION_RULES.md`.
10. Read `PROJECT_ROADMAP.md`.
11. Read `PROJECT_STATUS.md`.
12. Read `CURRENT_TASK.md`.
13. Read `REVIEW_PACKAGE_RULES.md`.
14. Decide whether the previous task is complete.
15. Decide whether the current project can run.
16. Only then start code changes.

If the previous task is unfinished, partially finished, interrupted, untested, or unsafe, continue it first or explicitly record why it cannot continue.

## Checkpoint Discipline

Update `CODEX_CHECKPOINT.md` during work, not only at the end.

Required checkpoint fields:

```md
## 当前检查点

- 更新时间：
- 当前任务：
- 当前阶段：
- 已完成：
- 正在处理：
- 未完成：
- 修改过的文件：
- 新增的文件：
- 删除的文件：
- 当前是否可以运行：
- 是否已测试：
- 测试结果：
- 当前错误/警告：
- 如果中断，下次应从哪里继续：
- 备注：
```

Write checkpoints whenever:

1. multiple files are being modified
2. a migration starts
3. a refactor starts
4. a batch replacement starts
5. a UI change starts
6. a feature is added
7. a build/test/run error appears
8. project state becomes unclear

## Error And Interruption Handling

If network interruption, build failure, run failure, test failure, page failure, behavior anomaly, file-state confusion, unexpected scope growth, or risk to old behavior appears, stop expanding changes and write:

```md
## 中断 / 错误记录

- 发生时间：
- 发生阶段：
- 正在处理的文件：
- 已完成部分：
- 未完成部分：
- 错误现象：
- 错误信息：
- 初步判断：
- 下次继续前必须先检查：
- 建议下一步：
```

When state is unclear, do not continue broad modifications.

## Completion Requirements

At task end, append to `CODEX_CHECKPOINT.md`:

```md
## 任务完成标志

- 状态：已完成 / 部分完成 / 未完成
- 本次完成内容：
- 本次修改文件：
- 本次新增文件：
- 本次删除文件：
- 是否保留原有功能：
- 是否存在已知问题：
- 是否已运行项目：
- 是否已通过测试：
- 下一步建议：
```

Do not write "已完成" if required verification was not run.

Do not write "未影响旧功能" unless old behavior was checked or source was not touched.

If any unresolved error remains, mark the task as partial or unfinished.

## Review Package Requirement

Every completed construction round must update or create `review_package/` according to `REVIEW_PACKAGE_RULES.md`.

At minimum, include:

- `CHANGELOG_THIS_TASK.md`
- `SELF_CHECK_REPORT.md`
- `FILES_CHANGED.md`
- `BUILD_AND_RUN_REPORT.md`
- `ERRORS_AND_RISKS.md`
- `NEXT_STEPS.md`
- `ARCHITECTURE_DECISIONS.md`
- `PACKING_MANIFEST.md`

If UI changed, include screenshots.

If a website output was generated, include an export check report.

## Packaging Requirement

Every completed construction round must create a source package named according to the current task.

For the P0 engineering foundation round, the name is:

```text
WebRebuildRecorder_source-review_YYYYMMDD_HHMMSS.zip
```

Exclude:

- `bin/`
- `obj/`
- `.vs/`
- `.git/`
- `*.mp4`
- historical zip files
- FFmpeg executables
- FFmpeg archives
- large screenshots and video caches
- API keys
- tokens
- cookies
- account/password files
- user private files

Every source review zip must include these project memory files:

- `PROJECT_MEMORY_INDEX.md`
- `docs/project-memory/PROJECT_MEMORY_FULL.md`
- `PROJECT_BLUEPRINT.md`
- `CODEX_PROJECT_MEMORY.md`
- `CODEX_CONSTRUCTION_RULES.md`
- `PROJECT_ROADMAP.md`
- `PROJECT_STATUS.md`
- `CURRENT_TASK.md`
- `REVIEW_PACKAGE_RULES.md`

If any of these files are missing from `review_package/` or the source review zip, the package is incomplete and must be regenerated before GPT review.

## GitHub Submission And Project Memory Sync

GitHub should be treated as the clean project fact source when this working tree is a real Git repository or when a GitHub remote is available.

Each completed construction round should prepare these tracked contents for GitHub:

- source code changes under `src/`, application source projects, and test or self-test projects
- project memory files:
  - `PROJECT_MEMORY_INDEX.md`
  - `PROJECT_BLUEPRINT.md`
  - `CODEX_PROJECT_MEMORY.md`
  - `CODEX_CONSTRUCTION_RULES.md`
  - `PROJECT_ROADMAP.md`
  - `PROJECT_STATUS.md`
  - `CURRENT_TASK.md`
  - `REVIEW_PACKAGE_RULES.md`
- review package text files:
  - `review_package/CHANGELOG_THIS_TASK.md`
  - `review_package/SELF_CHECK_REPORT.md`
  - `review_package/FILES_CHANGED.md`
  - `review_package/BUILD_AND_RUN_REPORT.md`
  - `review_package/ERRORS_AND_RISKS.md`
  - `review_package/NEXT_STEPS.md`
  - `review_package/ARCHITECTURE_DECISIONS.md`
  - `review_package/PACKING_MANIFEST.md`

Do not commit generated or sensitive content:

- `bin/`
- `obj/`
- `.vs/`
- `.git/`
- `*.user`
- `*.suo`
- temporary zip files
- large logs
- screen recordings
- extracted frame output
- temporary screenshots
- FFmpeg output
- real customer materials
- API keys, tokens, cookies, SSH keys, proxy settings, Codex/OpenAI login files, and local credential files
- local absolute-path configuration
- customer private data

Use this end-of-round Git flow only when the current directory is inside a Git worktree:

```powershell
git status
dotnet build WebRebuildRecorder.slnx
git add .
git status
git commit -m "P0.2.1: concise task description"
git push
git log --oneline -5
```

If build fails, do not commit as a normal completion. A failure recovery commit is allowed only when the commit message, `PROJECT_STATUS.md`, `CURRENT_TASK.md`, and `review_package/SELF_CHECK_REPORT.md` clearly mark the state as failed or partial.

Before committing, verify:

1. `git status` has no unexpected large files.
2. `git diff` has no keys, tokens, account data, customer private data, or local credential paths.
3. `PROJECT_STATUS.md` and `CURRENT_TASK.md` are updated.
4. `review_package/` is updated.
5. `SELF_CHECK_REPORT.md` records build, self-test, risks, and unfinished work.

If the current directory is not a Git repository, record that fact in the checkpoint and review package. Do not pretend that commit or push was performed.

Review zips may remain local. If a complete binary or large review artifact must be preserved remotely, prefer a GitHub Release attachment instead of committing it into the source tree.

The intended ownership model is:

```text
GitHub = clean source + project memory + review summaries + traceable history
local review zip = complete local handoff package
GitHub Release = optional large artifacts or version bundles
ChatGPT long-term memory = navigation index, not the source of truth
```

## Build And Run Verification

For documentation-only rounds, run at least:

```powershell
dotnet build WebRebuildRecorder.slnx
```

For source or UI changes, also perform the minimal run or targeted functional check that matches the changed area.

If a check cannot be run, record the reason in `BUILD_AND_RUN_REPORT.md` and the final checkpoint.

## Existing Feature Preservation

Preserve:

- project creation/opening compatibility with `project.json` and older `project-info.json`
- recording first, then opening target URL
- automatic and manual recording flows
- FFmpeg/FFprobe configuration and fallback behavior
- frame extraction and index generation
- ChatGPT/GPT package generation
- user intent and selected asset packaging
- global hotkey handling and conflict reporting

Do not delete or replace old logic without explicit instruction and a written reason.

## Future Codex CLI Safety

When future tasks implement Codex CLI execution:

- Treat Codex CLI as an unreliable external process, not a stable function call.
- Use a project sandbox.
- Use `project.lock` while running.
- Save layered logs.
- Classify failures.
- Verify outputs.
- Allow retry.
- Never expose credential files or write outside authorized project directories.
