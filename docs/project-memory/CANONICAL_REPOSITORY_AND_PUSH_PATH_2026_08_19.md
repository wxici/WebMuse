# Canonical Repository and Push Path Hard Rule

Updated: 2026-08-19
Status: AUTHORITATIVE REPOSITORY GOVERNANCE

## Canonical identity

```text
Local root: E:\GitHub\WebMuse
Git top-level: E:/GitHub/WebMuse
Remote: https://github.com/wxici/WebMuse.git
Branch: main
```

For all future normal WebRebuildRecorder/WebMuse construction after the one-time cutover, this is the only authorized source/commit/push path.

The source-code/project identity may continue to use historical `WebRebuildRecorder` names where a rename would be unrelated scope.

## Mandatory gate before any future source-changing task

Every ChatGPT/Codex task must verify, before editing/building/testing/committing/pushing:

```text
root = E:/GitHub/WebMuse
origin fetch = https://github.com/wxici/WebMuse.git
origin push = https://github.com/wxici/WebMuse.git
branch = main
HEAD = fixed expected task baseline
origin/main = fixed expected remote baseline unless explicitly handling a verified local-ahead state
worktree = expected task state
```

Any mismatch is a hard stop.

## Legacy paths

These locations/remotes are legacy for WebRebuildRecorder/WebMuse normal construction:

```text
E:\GitHub\codex
E:\GitHub\codex\WebRebuildRecorder
E:\GitHub\codex-worktrees\WebRebuildRecorder-stage1
https://github.com/wxici/codex.git
```

Do not change the shared monorepo `origin` to WebMuse. That repository contains unrelated projects.

After cutover, no new WebRebuildRecorder/WebMuse production task may commit or push to `wxici/codex` unless a separately authorized archival/migration task explicitly says so.

## In-flight exception

At adoption time, A1D-6R-2 was already running from:

```text
E:\GitHub\codex-worktrees\WebRebuildRecorder-stage1
```

That task is the only authorized legacy-worktree exception and may finish only its already-dispatched scope.

No new production task may start from that legacy worktree.

## Required cutover before the next production task

The current `E:\GitHub\WebMuse` source tree predates the latest Stage 1 work. Therefore the repository is the target canonical location but is not yet the current Stage 1 source baseline.

After A1D-6R-2 returns, one bounded source-promotion/cutover task must occur before any new production task. It must:

1. independently review the final accepted legacy source HEAD and A1D-6R-2 result;
2. promote only the accepted WebRebuildRecorder/WebMuse project content into this repository;
3. not import DotNetMCP or unrelated monorepo projects;
4. preserve this canonical repository/push rule;
5. validate build/tests from `E:\GitHub\WebMuse`;
6. commit to `main`;
7. push only to `https://github.com/wxici/WebMuse.git`;
8. verify `HEAD == origin/main` and clean worktree;
9. record the accepted cutover commit as the new source baseline.

## Future push rule

Normal future push authority is:

```text
git push origin main
```

but only after root + remote + branch + HEAD + worktree gates pass in `E:\GitHub\WebMuse`.

A bare push command is not authority by itself.
