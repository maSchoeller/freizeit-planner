# Freizeit-Cockpit agent rules

- Read `prompt.md`, `PROGRESS.md`, `CONTEXT-MAP.md`, and the affected module `CONTEXT.md` before editing.
- Code, namespaces, APIs, schema, and commits are English; user-visible text and help are German.
- Use `Camp`, `OrganizationId`, and `CampId` consistently. Public immutable domain records have no `Dto` suffix.
- Preserve the modular monolith: only `<Module>.Contracts` crosses a module boundary. Never access another
  module's DbContext, schema, entities, or internals. Tenant tables carry `organization_id`; camp tables also
  carry `camp_id`. Enforce authorization in code and PostgreSQL RLS.
- Work outside-in in one vertical slice: acceptance test red for the expected reason, minimum implementation,
  targeted green tests, affected integration/architecture tests, documentation, then a small local Conventional
  Commit. Do not push and do not perform destructive Git operations.
- Use `pwsh ./scripts/bootstrap.ps1`, `pwsh ./scripts/dev.ps1`, `pwsh ./scripts/test.ps1`,
  `pwsh ./scripts/verify.ps1`, and `pwsh ./scripts/smoke.ps1`. Standard tests must not need the Internet.
- Treat warnings as errors, keep .NET nullable and TypeScript strict, and never hand-edit generated clients.
- Never log or commit secrets, tokens, one-time codes, full email bodies, blob access URLs, or domain long text.
- Mutations require antiforgery and `If-Match`; reads return ETags. Archived camps are read-only.
- Never deploy to Azure in this repository session. Only local static validation is permitted.
- A slice is done only when UI, API, persistence, authorization, failures, accessibility, tests, help/context,
  `PROGRESS.md`, and a green local commit are complete. The product is done only when `scripts/verify.ps1` and
  `scripts/smoke.ps1` pass and rendered UI screenshots have been inspected at mobile, tablet, and desktop sizes.
