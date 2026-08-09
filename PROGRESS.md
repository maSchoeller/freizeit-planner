# Freizeit-Cockpit progress

This file is the resumable evidence ledger. Commands are run from the repository root with PowerShell 7.

## Stable test seams

- HTTP acceptance seam: versioned `/api/v1` endpoints and RFC 9457 responses.
- Module seam: role-oriented public interfaces in each `*.Contracts` project.
- Browser seam: German user journeys through the real same-origin application.
- Infrastructure seam: Aspire resource graph, Docker image build, and static Bicep/azd validation.

## Slices

| Slice                                 | Status      | Acceptance criteria                                                                         | Red evidence                                           | Green/verify evidence                                | Docs                                      | Commit    | Blocker / next smallest step           |
| ------------------------------------- | ----------- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------ | ---------------------------------------------------- | ----------------------------------------- | --------- | -------------------------------------- |
| F01 foundation                        | verified    | Pinned .NET/React/Aspire skeleton; bootstrap, build and test paths work                     | Foundation script failed on 41 absent paths            | Full verify passed in 63.3 s                         | deployment plan, AGENTS, contexts, skills | `f9f4022` | Start I01 with red HTTP test           |
| I01 passwordless identity             | verified    | Hashed six-digit code, expiry, attempts/rates, generic response, sessions and revoke        | Missing-CSRF API test returned HTTP 500                | Full verify passed in 87.0 s                         | Identity context, login/session help      | `84d5da1` | Start I02 invitation tests             |
| I02 invitations and account lifecycle | verified    | Invite rotation/revoke; memberships; reauth; verified email; 30-day account/tenant deletion | Missing lifecycle contracts caused compile failure     | Full verify passed in 86.9 s plus Aspire smoke       | Identity context, account/role help       | `98fb62d` | Start I03 authorization                |
| I03 tenant authorization              | verified    | Role matrix, last owner, suspension, IDOR protection and RLS isolation                      | Missing authorization contracts caused compile failure | Full verify 382.3 s; PostgreSQL/Aspire/browser smoke | auth/RLS docs and role help               | `dc0c859` | Start C01/T01/S01 wave                 |
| C01 camps                             | in_progress | Camp lifecycle, slugs, archive read-only/reactivate, dashboard                              | Contract acceptance test next                          | pending                                              | Camps context, help                       | pending   | module worktree active                 |
| C02 schedule                          | in_progress | Agenda/calendar CRUD, overlap, timezone/DST, all-day, ETag, atomic links                    | Missing edit/drop/resize handlers and all-day UI       | 17 Camps, 26 API, 20 React + browser green           | Camps context, schedule help              | pending   | complete responsibility assignment UI  |
| T01 ingredients and recipes           | in_progress | Normalize/merge, decimal units, recipe versions and attachments                             | Contract acceptance test next                          | pending                                              | Catering context, help                    | pending   | module worktree active                 |
| T02 meals and snapshots               | in_progress | Portion scaling, stable/refreshable snapshots and atomic schedule workflow                  | Invalid schedule link was accepted                     | 14 Catering + atomic PostgreSQL green                | Catering context, schedule help           | pending   | complete meal and snapshot UI          |
| L01 material                          | in_progress | Camp/schedule material, responsibilities and procurement status                             | Hard delete removed material immediately               | 16 Logistics + PostgreSQL cleanup green              | Logistics context, help                   | pending   | complete UI and visual verification    |
| L02 shopping                          | in_progress | Named lists, unified sourced items, editable transfer, concurrent check-off and polling     | Hard delete removed individual items immediately       | 19 Logistics + PostgreSQL cleanup green              | Logistics context, shopping help          | pending   | complete UI and visual verification    |
| S01 devotions and Bible               | in_progress | Four translations, attribution, provider/stub, resilient immutable snapshots and refresh    | Invalid schedule link was accepted                     | 24 Spiritual + atomic PostgreSQL green               | Spiritual context, schedule help          | pending   | complete devotion and Bible UI         |
| K01 notebook                          | pending     | Safe Markdown notes, tags, pins and typed links                                             | pending                                                | pending                                              | Knowledge context, help                   | pending   | after I03                              |
| F02 attachments                       | pending     | Magic-byte/MIME/extension checks, quotas, private authorized image/PDF delivery             | pending                                                | pending                                              | Files context, help                       | pending   | after module CRUD                      |
| A01 activity/trash                    | in_progress | Metadata-only feed, soft delete/restore and deterministic 30-day purge                      | Schedule hard delete bypassed trash                    | 11 Catering + 23 API + 14 React + cleanup green      | Camps/Catering contexts, trash help       | pending   | audit remaining delete paths and UI    |
| A02 search/export/print               | in_progress | Tenant-safe filtered search, CSV formula protection, German print views                     | React search test saw static local result              | filtered search and four CSV routes green            | search/activity/export help               | pending   | verify print and real RLS              |
| P01 PWA/offline                       | pending     | Install/update; read-only four-area snapshot; purge on logout/org switch                    | pending                                                | pending                                              | PWA docs/help                             | pending   | after CRUD                             |
| O01 operations                        | in_progress | Migrator lock/order, cleanup, telemetry, health and correlation without sensitive logs      | Cleanup test exposed the stub and Migrator double-open | 4 Cleanup + 29 Identity + PostgreSQL erasure green   | cleanup runbook and module contexts       | pending   | provision least-privilege DB jobs role |
| Z01 Azure/CI                          | pending     | azd/Bicep/containers/workflows locally validate; no cloud mutation                          | pending                                                | pending                                              | deployment docs                           | pending   | after F01/O01                          |
| V01 full verification                 | pending     | Format/lint/build/tests, coverage, three browsers/viewports, axe, visual inspection, smoke  | pending                                                | pending                                              | all docs/screenshots                      | pending   | after all slices                       |

## Current evidence

- 2026-08-07: Repository began with only `prompt.md` and empty `readme.md`; `git status --short` was clean.
- 2026-08-07: Installed stable .NET SDK `10.0.100` and runtimes `10.0.0`/`10.0.2`; Node `26.5.1`, Docker
  `29.6.1`, Git `2.51.1`, Azure CLI `2.88.0`, PowerShell `7.6.3`. `azd` and Corepack were not on PATH.
- 2026-08-07: No applicable repository `AGENTS.md` existed before this file.
- 2026-08-07: Both generated repository skills passed `quick_validate.py`.
- 2026-08-07: F01 red was observed because all 41 required foundation paths were absent. After implementation,
  `pwsh ./scripts/verify.ps1` completed in 63.3 seconds. The Release build reported zero warnings/errors, Vitest
  passed 1/1, and the PWA and VitePress builds completed. The Aspire graph includes PostgreSQL 17, Azurite,
  Mailpit, a deterministic Bible stub, migrator, cleanup job, and web host.
- 2026-08-07: I01 red evidence included the anonymous login mutation without an antiforgery token returning HTTP
  500 instead of a stable HTTP 400 Problem Detail. The final `pwsh ./scripts/verify.ps1` completed in 87.0 seconds:
  OpenAPI/client drift, locked restores, formatting, warning-free Release build, 4 Identity tests, 3 API tests,
  3 Vitest tests, lint, TypeScript strict checks, PWA and help builds all passed. The real Aspire stack additionally
  proved PostgreSQL migration and Development seed, SMTP delivery to Mailpit, login `204`, one current session,
  revoke `204`, and immediate `401` after revocation. Login and dashboard were visually inspected at 320x800,
  768x1024 and 1440x1000; mobile overflow was corrected and keyboard skip-link focus remained visible.
- 2026-08-07: I02 red was captured as compile failures for the absent invitation, tenancy-role, and lifecycle
  contracts. The implementation now covers 48-hour first-Owner and seven-day team invitations, HMAC-only single-use
  tokens, rotation/revocation, scoped assignments without role downgrade, and atomic acceptance. Self-service covers
  display name, login-code-verified email changes, memberships, 30-day account deletion, and fresh-login plus exact
  slug organization deletion. PostgreSQL 17 and Mailpit smoke proved the real migrations, SMTP flows, new-user
  acceptance, Owner membership, email change, and reversible account/tenant deletion. The account and invitation
  views were visually inspected at 390x844 and 1280x800 with no horizontal overflow and ordered keyboard focus.
  The final full repository verification passed in 86.9 seconds with zero build warnings/errors, 13 Identity tests,
  5 API tests, 5 React tests, stable generated OpenAPI client, formatting, lint, typecheck, PWA and help builds.
- 2026-08-07: I03 red was captured as compile failures for the absent organization/camp action contracts and
  authorization service. The completed role matrix denies Platform Admin tenant access, blocks suspended tenants,
  protects the last Owner and Admin boundaries, scopes Camp Leads, and uses application-managed versions with ETag
  and If-Match. PostgreSQL 17 runs API requests under the `freizeit_app` `NOLOGIN NOBYPASSRLS` role with forced RLS
  and transaction-local user/organization/camp/operation context. `scripts/test-rls.ps1` proved own-row access,
  foreign read/write denial, lower-role row minimization, Platform Admin metadata-only access, suspension, and pooled
  context cleanup. The real Aspire stack proved five-member Owner access, versioned role change, Platform Admin `403`
  for tenant content, and suspend/unsuspend enforcement. Member and platform administration were visually inspected
  at 390x844 and 1280x800 with no horizontal overflow or clipped controls. The corrected full gate executed rather
  than skipping tests and passed in 382.3 seconds: 28 Identity, 7 API, 7 React, real PostgreSQL RLS, warning-free
  build, OpenAPI/client drift, formatting, lint, typecheck, PWA and VitePress builds.
- 2026-08-09: Activity host integration now enlists the Activity schema in the shared request transaction and
  writes bounded metadata events plus source-versioned search projections for Camp updates, schedule entries,
  meals, material, shopping lists, devotions, notes, and Camp attachments. HTTP red evidence first showed an empty
  journal and then HTTP 500 when Activity failed; the final behavior returns a stable Problem Detail and rolls back
  every response with status 400 or greater. Search uses the real camp-scoped endpoint with type and metadata
  filters. Schedule, meal, material, and all-shopping-list CSV routes use the formula-safe formatter. Release build
  completed with zero warnings/errors; Activity passed 13/13, API 18/18, and React 12/12 tests. Aggregate trash UI,
  actual PostgreSQL RLS smoke, and final visual verification remain required before A01/A02 can be marked verified.
- 2026-08-09: The cleanup executable now performs a bounded one-shot pass instead of printing a placeholder. It
  removes expired identity artifacts, due note trash, attachment read grants, and due blobs plus metadata through
  public maintenance contracts. Blob failures leave metadata retryable and fail the job for scheduler retry; logs
  expose aggregate counts only. Cleanup coordinator tests passed 2/2. Organization/account hard deletion and the
  dedicated production database-principal bootstrap remain before O01 can be marked verified.
- 2026-08-09: Due account and Organization erasure now uses a resumable two-phase protocol. Identity atomically
  claims cases after 30 days; claimed Organizations are inaccessible and non-cancellable. Seven module-owned
  `IDataErasure` implementations delete their own tenant data, delete blobs before metadata, remove responsibilities,
  and pseudonymize retained actor fields with the empty UUID. Identity finalizes only after the exact required module
  set reports completion. The new PostgreSQL 17 smoke test initially exposed and then verified the fix for a Migrator
  double-open bug; it proves real Organization deletion and cross-module account pseudonymization. Cleanup tests pass
  4/4 and Identity tests 29/29. Production jobs-role provisioning remains before O01 is verified.
- 2026-08-09: The authenticated aggregate Camp trash now merges deleted notes, devotions, and attachments in
  chronological order and returns versioned restore paths. The German React view shows deterministic purge dates,
  restores with antiforgery plus `If-Match`, and disables mutation offline. Direct-route acceptance tests exposed
  and closed Member-level restore bypasses for devotions and attachments. Cleanup now also purges 30-day-old
  devotions and all their Bible snapshots transactionally; PostgreSQL 17 proved the real FK-safe deletion. Targeted
  evidence is Files 28/28, Spiritual 21/21, Cleanup 4/4, API 20/20, and React 13/13. Remaining hard-delete domain
  objects still need the shared lifecycle before A01 is verified.
- 2026-08-09: Material requirement deletion is now a versioned soft delete. Active reads hide deleted rows; only
  `ManageCamp` actors can browse or restore them, and restore rejects archived Camps and elapsed deadlines. The root
  trash and German UI include material with its module restore path, antiforgery and `If-Match`. A generated EF
  migration adds deletion/purge timestamps and an indexed cleanup deadline. The bounded Logistics retention service
  permanently deletes due rows; PostgreSQL 17 proved the real cascade. Logistics passes 16/16 targeted tests; shopping
  lists/items remain the next hard-delete path.
- 2026-08-09: Shopping-list deletion now preserves the complete list and all items for 30 days. Active list reads hide
  it; manager-only trash browsing and versioned restore recheck archive state and deadline. The aggregate Camp trash,
  German UI and activity projection use the list restore route. Cleanup deletes due list aggregates and associated
  immutable check audit transactionally; PostgreSQL 17 proves the real deadline purge. Logistics passes 18/18 tests.
  Individual item deletion remains the final Logistics hard-delete path.
- 2026-08-09: Individual shopping-item deletion now uses the same versioned 30-day lifecycle. Active list views and
  counts hide deleted items; managers see each item in the aggregate Camp trash and can restore it only while its
  parent list is active, the Camp is writable, and the deadline has not elapsed. Cleanup removes due items,
  responsibilities and immutable check audit transactionally while retaining the active parent list. The generated
  migration, PostgreSQL 17 cleanup test, Logistics 19/19, API 21/21, and Cleanup 4/4 tests are green. Logistics no
  longer has an interactive hard-delete path; UI completion and rendered visual verification still remain.
- 2026-08-09: Schedule-entry deletion is now a versioned soft delete. Active calendar and agenda reads hide deleted
  entries; manager-only aggregate trash browsing and restore recheck the Camp archive state and deadline. Cleanup
  permanently deletes due entries and responsibility rows in a bounded pass; PostgreSQL 17 proves the real cascade.
  The generated migration, Camps 17/17, API 21/21 and Cleanup 4/4 targeted tests are green. The next schedule slice
  must enforce the explicit unlink-versus-common-trash choice for linked meals and devotions.
- 2026-08-09: Meal deletion now preserves complete recipe-snapshot aggregates for 30 days, exposes manager-only
  aggregate trash/restore with ETag concurrency, and has a bounded retention service whose PostgreSQL 17 smoke test
  proves cascading purge of snapshots and snapshot ingredients. Deleting a linked ScheduleEntry now requires the
  explicit unlink-versus-common-trash choice in API and German agenda UI; both branches update Activity/Search in the
  shared request transaction. Red evidence included immediate meal hard deletion and a missing linked-content choice.
  The full test path passes with Catering 11/11, Camps 17/17, API 23/23, Cleanup 4/4, React 14/14, RLS and cleanup
  smoke. Keyboard operation with Tab/Space/Enter and rendered 1440x1000, 768x1024, 390x844 and 320x800 layouts were
  inspected; the 320px pass exposed and then verified a fix for page-wide mobile navigation overflow. The complete
  repository verify gate passed in 316.2 seconds with warning-free .NET builds and synchronized generated clients.
- 2026-08-09: ScheduleEntry plus Meal or Devotion can now be created through one German accessible form and one
  host-composed Npgsql transaction. Both child modules validate optional schedule identifiers on create, update, and
  restore without crossing module boundaries; the Andacht workflow assigns the actor when no responsibility was
  supplied. Red evidence included missing HTTP routes, absent UI choices, and accepted invalid schedule references.
  A real PostgreSQL 17 acceptance test deliberately fails Meal creation after Camps persisted and proves the
  ScheduleEntry rollback with a direct database query. The same run exposed and fixed OpenAPI generation detection
  that had mistaken every WebApplicationFactory host for the document tool. Keyboard operation and rendered
  1440x1000, 768x1024, 390x844 and 320x800 layouts were inspected. The tablet pass exposed and then verified a fix
  for overlapping date/time fields; all sizes are free of page-wide horizontal overflow. The complete repository
  verify gate passed in 155.5 seconds with Camps 17/17, Catering 14/14, Spiritual 24/24, API 26/26, React 16/16,
  PostgreSQL RLS/rollback/cleanup, warning-free builds, generated clients, and help output green.
- 2026-08-09: Schedule updates now have one accessible agenda form and one shared optimistic update path for
  FullCalendar drag and resize. Every path sends antiforgery plus `If-Match`; rejected writes revert immediately,
  while version conflicts explain the reload. Timed and all-day creation/editing cover description, location,
  category, status, and audience. FullCalendar resolves the Camp IANA zone through the v6 Luxon adapter rather than
  the device zone. Red tests proved the previously inert edit button, absent drop/resize callbacks, and missing
  all-day creation. Rendered 1440x1000, 768x1024, 390x844 and 320x800 passes found and fixed collapsed date-field
  spacing; the final layouts have no page-wide horizontal overflow, 48 px labeled checkbox/button targets, and
  ordered keyboard focus. The final full repository gate passed in 191.5 seconds with 20/20 React tests and a
  667.66 KiB production bundle after replacing the 1,388.37 KiB Moment adapter. Responsibility assignment remains
  the final C02 UI gap.
