---
name: develop-freizeit-cockpit
description: Implement and resume Freizeit-Cockpit vertical slices with repository-specific modular-monolith, tenant-isolation, German UX, TDD, documentation, verification, and local-commit rules. Use when adding or changing product behavior in this repository.
---

# Develop Freizeit-Cockpit

1. Read `AGENTS.md`, `PROGRESS.md`, `CONTEXT-MAP.md`, and the affected `src/<Module>/CONTEXT.md` completely.
2. Select the smallest pending slice whose dependencies are verified. Record precise observable acceptance criteria
   and mark it `in_progress` in `PROGRESS.md`.
3. Identify the public seam: HTTP acceptance endpoint, module Contract, or real browser journey. Add one behavior test,
   run it, and record the expected domain reason for red. Never test another module's internals or database tables.
4. Implement one complete vertical tracer: German UI, `/api/v1` API, handler/domain rule, module-owned persistence,
   authorization/RLS, validation and recoverable empty/loading/error/conflict states. Keep cross-module types immutable
   and in the callee's Contracts project.
5. Run the narrowest test until green, then affected unit, integration, architecture, frontend, and browser tests.
   Refactor only while those stay green. Standard tests remain offline and deterministic.
6. Regenerate OpenAPI client through the repository command. Update affected help, module context, architecture notes,
   and `PROGRESS.md` with exact commands and results.
7. Run `git diff --check`, inspect the intended diff, and create a small English Conventional Commit only when green.
   Record its hash, mark the slice `verified`, and immediately choose the next smallest slice.

Never push, mutate Azure, weaken tenant/security tests, expose secrets, or mark a slice verified without evidence.
