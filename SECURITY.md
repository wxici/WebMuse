# Security Policy

WebMuse is an early-alpha OSS project for safer Codex-assisted reference-style website reconstruction workflows.

Security and privacy boundaries are part of the core product design.

## Do not commit sensitive material

Never commit:

- API keys;
- OpenAI or Codex login files;
- tokens;
- cookies;
- SSH keys;
- certificates;
- proxy settings;
- local absolute-path configuration;
- customer materials;
- private brand assets;
- recordings;
- screenshots;
- extracted frames;
- generated output sites;
- review package zips;
- runtime logs.

## AI execution boundary

Current public builds do not enable real Codex CLI execution, OpenAI API calls, Ollama calls, LM Studio calls, or automatic website generation.

Future real execution must be gated by:

1. readiness checks;
2. dry-run plans;
3. sandbox path validation;
4. allowed write-root validation;
5. forbidden-root checks;
6. proof-check artifacts;
7. approval gates;
8. rollback confirmation;
9. failure recovery rules.

## Sandbox rule

AI-generated or AI-modified output must never write outside the allowed project workspace. The application must reject writes to installation directories, system directories, credential directories, source repository roots outside the selected project, and other unsafe locations.

## Customer material rule

Customer materials, logos, screenshots, recordings, and generated delivery packages should stay out of the public repository.

## Public demo screenshots

Curated public demo screenshots are allowed only when they are sanitized, low-risk, non-customer, non-secret, and used to explain the workflow.

Do not commit raw recordings, extracted frame sets, customer materials, credentials, local path configuration, full generated output-site artifacts, or third-party proprietary assets.

Reference-site screenshots, if used, must be low-risk and must be presented only as observation or layout-rhythm evidence, not as a clone target.

## Reporting security issues

For now, use GitHub Issues for non-sensitive security concerns.

Do not post secrets, tokens, private customer files, or exploitable details publicly. If a sensitive report is needed, open a minimal issue asking for a private contact path without disclosing the sensitive content.
