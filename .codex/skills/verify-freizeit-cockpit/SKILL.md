---
name: verify-freizeit-cockpit
description: Verify the complete Freizeit-Cockpit repository locally, including toolchain, generated artifacts, format, build, tests, coverage, architecture, security, Aspire smoke, browsers, accessibility, documentation, Azure artifacts, and actual rendered screenshot inspection. Use for full quality gates or release readiness.
---

# Verify Freizeit-Cockpit

1. Read `AGENTS.md`, `PROGRESS.md`, `.azure/deployment-plan.md`, and `scripts/verify.ps1`. Do not change product logic
   during a verification-only request.
2. Run `pwsh ./scripts/bootstrap.ps1 -CheckOnly`, then `pwsh ./scripts/verify.ps1`. Require format, analyzer, lint,
   typecheck, build, unit, integration, architecture, OpenAPI drift, coverage (80% lines/75% branches), security,
   documentation/link, Bicep/azd static, container, and secret scans to pass.
3. Start the real deterministic Aspire topology and run `pwsh ./scripts/smoke.ps1`. Distinguish an application defect
   from a missing external cloud prerequisite; never authenticate to or mutate Azure.
4. Run Playwright in Chromium, Firefox, and WebKit at mobile, tablet, and desktop viewports, including axe, keyboard,
   offline, permission, validation, conflict, loading, empty, and error journeys. Generate current help screenshots.
5. Open and actually inspect all representative screenshots. Reject clipped/overlapping text, broken controls/icons,
   unintended scrolling, poor focus/contrast, inconsistent spacing, responsive failures, overlays, and browser artifacts.
   Repeat affected automated and visual checks after fixes.
6. Confirm `git diff --check`, no secrets or personal seed data, all help routes/links/screenshots current, all required
   slices `verified`, and intentional working-tree state. Record commands, durations, coverage, pages/states/viewports,
   and artifact paths in `PROGRESS.md`.

A failed or uninspected rendered UI means the full gate is failed. Do not deploy or push.
