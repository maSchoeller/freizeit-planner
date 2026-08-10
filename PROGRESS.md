# Freizeit-Cockpit progress

This file is the resumable evidence ledger. Commands are run from the repository root with PowerShell 7.

## Stable test seams

- HTTP acceptance seam: versioned `/api/v1` endpoints and RFC 9457 responses.
- Module seam: role-oriented public interfaces in each `*.Contracts` project.
- Browser seam: German user journeys through the real same-origin application.
- Infrastructure seam: Aspire resource graph, Docker image build, and static Bicep/azd validation.

## Slices

| Slice                                 | Status      | Acceptance criteria                                                                             | Red evidence                                                     | Green/verify evidence                                   | Docs                                      | Commit    | Blocker / next smallest step              |
| ------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- | ------------------------------------------------------- | ----------------------------------------- | --------- | ----------------------------------------- |
| F01 foundation                        | verified    | Pinned .NET/React/Aspire skeleton; bootstrap, build and test paths work                         | Foundation script failed on 41 absent paths                      | Full verify passed in 63.3 s                            | deployment plan, AGENTS, contexts, skills | `f9f4022` | Start I01 with red HTTP test              |
| I01 passwordless identity             | verified    | Hashed six-digit code, expiry, attempts/rates, generic response, sessions and revoke            | Missing-CSRF API test returned HTTP 500                          | Full verify passed in 87.0 s                            | Identity context, login/session help      | `84d5da1` | Start I02 invitation tests                |
| I02 invitations and account lifecycle | verified    | Invite rotation/revoke; memberships; reauth; verified email; 30-day account/tenant deletion     | Missing lifecycle contracts caused compile failure               | Full verify passed in 86.9 s plus Aspire smoke          | Identity context, account/role help       | `98fb62d` | Start I03 authorization                   |
| I03 tenant authorization              | verified    | Role matrix, last owner, suspension, IDOR protection and RLS isolation                          | Missing authorization contracts caused compile failure           | Full verify 382.3 s; PostgreSQL/Aspire/browser smoke    | auth/RLS docs and role help               | `dc0c859` | Start C01/T01/S01 wave                    |
| C01 camps                             | verified    | Camp lifecycle, slugs, archive read-only/reactivate, dashboard                                  | Static dashboard examples masked the live Camp state             | 17 Camps, 29 API, 25 React + browser green              | Camps context, organization/camp help     | pending   | Start next incomplete product slice       |
| C02 schedule                          | verified    | Agenda/calendar CRUD, overlap, timezone/DST, all-day, ETag, atomic links                        | Missing minimized responsibility directory and UI                | 30 Identity, 28 API, 21 React + RLS/browser green       | Camps/Identity contexts, schedule help    | pending   | Start next incomplete product slice       |
| T01 ingredients and recipes           | verified    | Normalize/merge, decimal units, recipe versions and private attachments                         | Recipe files were unreachable from their owner                   | 14 Catering, 28 Files, 29 React + full gate/browser     | Catering/Files contexts, recipe help      | pending   | Start T02 meal and snapshot UI            |
| T02 meals and snapshots               | verified    | Portion scaling, stable/refreshable snapshots and atomic schedule workflow                      | Reviewed shopping transfer was unreachable                       | 14 Catering, 32 React + full gate/browser               | Catering/Logistics contexts, recipe help  | pending   | Start live Logistics UI                   |
| L01 material                          | verified    | Camp/schedule material, responsibilities, procurement status and private attachments            | Material details exposed no reachable attachment area            | 16 Logistics, 28 Files, 37 React + full gate/browser    | Logistics/Files contexts, material help   | pending   | Start next incomplete product slice       |
| L02 shopping                          | verified    | Named lists, unified sourced items, editable transfer, concurrent check-off and polling         | Material summaries had no reviewed transfer action               | 19 Logistics, 35 React + full gate/browser              | Logistics context, shopping help          | pending   | Start complete L01 material UI            |
| S01 devotions and Bible               | verified    | Full lifecycle, four translations, resilient attributed snapshots and private attachments       | Andacht detail exposed no private file region                    | 24 Spiritual, 28 Files, 44 React + full gate/browser    | Spiritual/Files contexts, devotion help   | pending   | Start next incomplete product slice       |
| K01 notebook                          | verified    | Shared notebook, safe Markdown, typed links, lifecycle and private files                        | Restore success status was absent                                | 13 Knowledge, 28 Files, 48 React + full gate/browser    | Knowledge/Files/Activity contexts, help   | pending   | Start F02 attachment hardening            |
| F02 attachments                       | verified    | Magic-byte/MIME/extension checks, quotas, private authorized image/PDF delivery                 | Meal and schedule files were unreachable from owners             | 28 Files, 50 React + full gate/mobile browsers          | Files/Camps/Catering contexts, help       | pending   | Complete A01 activity/trash               |
| A01 activity/trash                    | verified    | Metadata-only feed, soft delete/restore and deterministic 30-day purge                          | Restore caches stale; feed omitted actor/type in UI              | 13 Activity, 32 API, 58 React + full gate/browser       | Activity/domain contexts, trash help      | pending   | Complete A02 search/export/print          |
| A02 search/export/print               | verified    | Tenant-safe filtered search, CSV formula protection, German print views                         | Metadata filters and scoped print views absent in UI             | 62 React + PostgreSQL RLS + full gate/mobile browser    | Activity context and search/export help   | pending   | Complete P01 PWA/offline                  |
| P01 PWA/offline                       | verified    | Install/update; read-only four-area snapshot; purge on logout/org switch                        | Cold start lacked scoped complete offline projections            | 68 React + full gate/PWA/mobile browser green           | PWA architecture and offline help         | `c594fe5` | Complete O01 operations                   |
| O01 operations                        | verified    | Migrator lock/order, cleanup, telemetry, health and correlation without sensitive logs          | Liveness ran readiness; no correlation/jobs DB role              | 35 API + PostgreSQL jobs/RLS + full gate green          | operations architecture and cleanup docs  | `3caa261` | Complete Z01 Azure/CI                     |
| Z01 Azure/CI                          | verified    | azd/Bicep/containers/workflows locally validate; no cloud mutation                              | Web image required pwsh and copied broken host links             | Bicep/azd/actionlint + 3 UID-1654 images + full gate    | deployment architecture, plan and runbook | `3f07d8b` | Complete V01 verification                 |
| V01 full verification                 | verified    | Format/lint/build/tests, coverage, three browsers/viewports, axe, visual inspection, smoke      | Backend coverage was 59.25% lines / 42.45% branches              | Full verify 626.3 s; Aspire/browser/axe/smoke green     | all docs and current help screenshots     | `e953387` | Product verification complete             |
| I04 password JWT identity             | verified    | Email/password login, asymmetric access/refresh JWTs, rotation, revoke and one-time First Login | Missing password-authentication contracts caused compile failure | Full verify 636.6 s; RLS/Aspire/63 browser cases green  | Identity/auth/help                        | pending   | Start I06 transferable invitations        |
| I05 password recovery                 | verified    | Reset email, password change, reauthentication, exact lockout and global account suspension     | Reset table initially lacked runtime-role grant                  | Full verify; real Mailpit reset and refresh restart     | Identity/account/help                     | pending   | Add admin mutations with I07               |
| I06 transferable invitations          | pending     | Bearer invitation links, registration, email confirmation, reservation, revoke and rotation     | pending                                                          | pending                                                 | Identity/invitation/help                  | pending   | Start after I05 is verified               |
| I07 SuperAdmin administration         | pending     | Owner migration, SuperAdmin/OrgAdmin rights, scoped suspension, admin APIs and German UI        | pending                                                          | pending                                                 | Identity/auth/RLS/help                    | pending   | Start after I06 is verified               |

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
- 2026-08-09: C02 responsibility assignment now lists only active people who may read the Camp and exposes only
  user ID plus display name through the Identity contract and HTTP endpoint. A SECURITY-DEFINER PostgreSQL function
  keeps the general membership RLS policies strict while independently rechecking actor and candidate Camp access;
  direct runtime-role assertions exclude an unassigned organization member and an unassigned caller. The real HTTP
  test additionally proves login, endpoint wiring, EF mapping, migration and RLS together. German create/edit forms
  provide a labeled multi-select checkbox group and clarify that responsibility does not grant permissions. Red
  evidence was the missing directory contracts and checkbox. Targeted verification passes with Identity 30/30,
  API 28/28, React 21/21, strict TypeScript, lint, and the PostgreSQL RLS/atomic tests. The rendered desktop form and
  its responsibility fieldset were inspected without clipping; the existing responsive form grid and keyboard path
  remain covered by the 320/390/768/1440 C02 passes and the semantic React interaction test.
- 2026-08-09: C01 now has an Organization-scoped Camp list plus accessible creation and settings routes. The list
  resolves the Organization ID from the signed-in account memberships, groups Camps as upcoming, ongoing or past,
  and exposes readable archived Camps. Owner/Admin creation sends antiforgery; edits, archive and reactivation send
  antiforgery plus the latest `If-Match` and advance the local version after every response. Archived forms disable
  writes and explain that reading and exporting remain possible. Red React evidence showed both routes falling into
  the old static workspace. Targeted evidence is Camp API 10/10 and React 23/23 with strict TypeScript and lint.
  Rendered list, creation and settings pages have labeled regions and controls, coherent narrow-window layout and no
  horizontal overflow. C01 remains open until the workspace resolves the Camp dynamically and its dashboard uses
  real schedule/responsibility/procurement data instead of static examples. The complete repository gate passed in
  189.6 seconds with warning-free builds, all PostgreSQL smokes, generated artifacts, PWA and help output green.
- 2026-08-09: The Camp workspace now resolves any speaking Organization/Camp route through the authenticated
  membership list and Camp-by-slug API, then supplies the returned OrganizationId, CampId, dates, status and IANA
  zone to every child route. Schedule queries/exports use the Camp range, FullCalendar opens on its start date, and
  links preserve the actual slugs. Archived Camps disable visible mutations and announce a persistent read-only
  notice in the content area; the rendered narrow view exposed and closed the earlier mobile-hidden status gap.
  Red evidence showed a Nordlicht/Winterfreizeit URL still rendering and querying the Sonnenhöhe seed. All existing
  journeys plus the new ID/date/status assertion pass in React 24/24 with strict TypeScript and lint. C01 now only
  lacks replacement of the dashboard's static schedule, responsibility and procurement examples. The complete
  repository gate passed in 255.6 seconds, including 29 API tests and the PostgreSQL RLS/privacy smokes.
- 2026-08-09: C01 is functionally complete. The Camp start page now loads the signed-in account, the complete Camp
  schedule, material summaries, shopping-list counts and recent activities from the dynamically resolved tenant
  route. It selects today's plan or the next available Camp day (and the last populated day for completed Camps),
  formats times in the Camp zone, counts the signed-in person's active schedule responsibilities, and combines open
  or planned material with unchecked shopping items. Each dashboard area exposes loading, failure and empty states;
  the shell profile also uses the real display name and initials. The red Nordlicht/Winterfreizeit contract failed on
  the seeded Miriam/August dashboard and is now green with all React journeys 25/25, strict TypeScript and lint. The
  rendered 610 px view had equal viewport/document width, readable cards and no clipping. The complete repository
  gate passed in 230.1 seconds with 29 API tests, PostgreSQL RLS/privacy smokes, PWA and help output green.
- 2026-08-09: T01 now has its first real UI vertical slice. Owner/Admin users can open an accessible recipe form,
  search the Organization ingredient library, add one or more autocomplete results, enter exact decimal quantities
  in mass, volume, piece or named-count units, and submit all required recipe content plus optional dietary,
  allergen and kitchen notes with antiforgery. The page also lists and filters real Organization recipes next to Camp
  meals. The red interaction test failed because the old recipe button was inert; all React journeys now pass 26/26
  with strict TypeScript and lint. The rendered 610 px form used a 595 px document width, exposed every control in
  keyboard order and showed the selected ingredient without clipping. T01 remains open for ingredient create/rename/
  merge, recipe detail/revision and attachment composition. The complete repository gate passed in 231.6 seconds
  with 14 Catering, 29 API and 26 React tests plus PostgreSQL RLS/privacy, PWA and help output green.
- 2026-08-09: T01 Owner/Admin ingredient management is now reachable from the food page. It lists the normalized
  Organization library, creates names with antiforgery, renames with antiforgery plus `If-Match`, and performs merge
  only after a server preview exposes the current source/target versions and affected recipe versions. The dangerous
  confirmation stays disabled until the user explicitly acknowledges those effects, while the UI reiterates that
  existing meal snapshots remain immutable. The red interaction failed at the missing management entry point; all
  React journeys now pass 27/27 with strict TypeScript and lint. The rendered merge preview used a 595 px document in
  a 610 px viewport without clipping and kept the acknowledgement next to the final action. T01 now remains open for
  recipe detail/revision and attachment composition. The complete repository gate passed in 205.4 seconds with 14
  Catering, 29 API and 27 React tests plus PostgreSQL RLS/privacy, PWA and help output green.
- 2026-08-09: T01 recipe cards now open the complete current version with preparation, decimal ingredient quantities,
  tags, allergen and kitchen notes. Owner/Admin users can start from those exact values, change every recipe field and
  append a new immutable version using antiforgery plus the aggregate `If-Match` value. The UI keeps existing meal
  snapshots explicitly unchanged and turns a precondition failure into an actionable German conflict message without
  discarding the form. The two red interactions failed first on the inert open button and the generic conflict error;
  all React journeys now pass 29/29 with strict TypeScript and lint. Rendered detail and edit states used a 595 px
  document in a 610 px viewport; the 563 px detail panel and 529 px form stayed unclipped. T01 remains open only for
  recipe attachment composition. The complete repository gate passed in 219.2 seconds with 14 Catering, 29 API and
  29 React tests plus PostgreSQL RLS/privacy, PWA and help output green.
- 2026-08-09: T01 recipe details now compose the Files-owned private attachment flow. Authorized readers see the
  recipe's current files and open one only after an antiforgery-protected, actor-bound single-use read grant. Owner/
  Admin users also see the shared 100 MiB recipe-library quota and can upload PDF, JPEG, PNG or WebP multipart files
  up to ten MiB with antiforgery; the browser keeps its generated multipart boundary. Loading, failure, empty, pop-up
  and upload failures are visible in German, while the malware-scanning exclusion is repeated next to the control.
  The red interaction failed on the missing owner-scoped file region; all React journeys pass 29/29 with strict
  TypeScript and lint. A rendered 610 px view kept its 595 px document and the 529 px file panel, including long file
  names, within the viewport. The complete repository gate passed in 237.4 seconds with 14 Catering, 28 Files, 29 API
  and 29 React tests plus PostgreSQL RLS/privacy, PWA and help output green; T01 is verified.
- 2026-08-09: T02 meal planning is now reachable from the Camp food page. Members can keep the Camp default portions
  or enter one positive override, optionally choose one current ScheduleEntry, select multiple library recipes and
  create their immutable snapshots with antiforgery. Meal details expose effective/default portions, scaled decimal
  quantities, source/latest recipe versions and allergen planning notes. An outdated snapshot changes only through
  its explicit antiforgery plus `If-Match` refresh action, with an actionable concurrency fallback. The red journey
  failed on the inert meal button; all React journeys now pass 30/30 with strict TypeScript and lint. At 610 px the
  563 px detail panel stayed inside the 595 px document; the create form uses one column at this width. T02 remains
  open for meal edit/delete, adding/removing snapshots after creation and the reviewed shopping-list transfer UI. The
  complete repository gate passed in 333 seconds with 14 Catering, 29 API and 30 React tests plus atomic PostgreSQL,
  RLS/privacy, PWA and help output green.
- 2026-08-09: T02 existing-meal management now keeps all aggregate changes reachable in one detail panel. Users can
  edit the name, portion override and ScheduleEntry link, add another current library recipe as an immutable snapshot,
  remove a snapshot and move the meal into the 30-day trash only after explicit acknowledgement. Antiforgery and
  `If-Match` protect every mutation; each returned aggregate replaces the cached detail so a sequence uses the server
  versions 4, 5, 6 and 7 instead of stale assumptions. The red journey failed first on the missing edit action; all
  React journeys now pass 31/31 with strict TypeScript and lint. At 610 px the 563 px management panel, long recipe
  choice and all destructive controls stayed within the 595 px document. T02 remains open for the reviewed shopping
  transfer. The complete repository gate passed in 220.6 seconds with 14 Catering, 29 API and 31 React tests plus
  PostgreSQL RLS/privacy, PWA and help output green.
- 2026-08-09: T02 now closes with the reviewed Catering-to-Logistics transfer. A meal writer can load its immutable
  shopping draft, freely choose any current Camp shopping list, include or exclude individual source lines and edit
  each positive decimal amount with only the source-compatible units. The dialog explicitly states that no package
  rounding occurs. The atomic request retains meal, snapshot, ingredient and recipe-version provenance and carries
  antiforgery plus the chosen list Version as both `If-Match` and transfer precondition; archived Camps expose no
  action. The red interaction failed on the missing transfer button, then all React journeys passed 32/32. A rendered
  610 px view kept the 595 px document, 563 px meal panel and 529 px transfer form inside the viewport; target change,
  decimal edit, line exclusion and success feedback were exercised in the Browser. The complete repository gate
  passed in 195.1 seconds with 14 Catering, 19 Logistics, 29 API and 32 React tests plus PostgreSQL RLS/privacy,
  cleanup, PWA and help output green; T02 is verified.
- 2026-08-09: The L02 Camp UI now replaces its shopping demos with real named-list summaries and one source-aware
  list detail. Writers can create a list, add a positive-decimal spontaneous item with standard or custom unit plus
  optional store/note, and quickly check or reopen each item. List structure uses the current list `If-Match`; check
  actions use the independent item Version and update local counters without waiting for the next poll. The checked
  row displays the Camp member resolved through the minimized responsibility directory and the server timestamp.
  Summaries and details poll every 15 seconds and refetch on focus, while archived Camps expose read-only data. The
  red interaction first failed because the server list was absent from the static UI; all 33 React tests are green.
  At 610 px the 595 px document, 563 px list detail and 525 px spontaneous-item form stayed inside the viewport;
  rendered checking showed Miriam Muster and the server time. The complete gate passed under high local system load
  in 505 seconds with 19 Logistics, 29 API and 33 React tests plus PostgreSQL RLS/privacy, cleanup, PWA and Help green.
  L02 remains open for post-transfer item editing/deletion, list rename/trash and material-to-list UI composition.
- 2026-08-09: L02 now supports the remaining shopping-item and list lifecycle in the Camp UI. Writers can edit a
  sourced or spontaneous item without changing its immutable provenance, including positive decimal quantity,
  compatible or custom unit, store, note and responsibility. The update chains item Version 2 to 3 before the
  separate item-trash request uses `If-Match: "3"`; list rename similarly chains list Version 6 to 7 before list
  trash uses `If-Match: "7"`. Both confirmations explain the 30-day recovery period, and archived Camps remain
  read-only. The red interaction first failed on the missing sourced-item edit action. A rendered desktop journey
  verified the complete edit form and item-trash confirmation; the shared responsive list rules had already kept
  the same controls inside the 610 px viewport without a fixed minimum width. The full gate passed in 192.5 seconds
  with 19 Logistics and 34 React tests plus build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green.
  L02 remains open only for composing planned material into a shopping list and its final audit.
- 2026-08-09: L02 closes with the reviewed material-to-shopping-list composition. A material summary now opens its
  full server detail with description, decimal quantity/unit, procurement source, note, status and Camp-directory
  responsibilities. Writers can choose any current Camp list and edit name, positive decimal quantity/unit, store,
  note and responsibilities before transfer; archived Camps expose no action. The atomic request carries
  antiforgery, list Version 5 in `If-Match` and the body, and independently loaded material Version 3. The resulting
  item preserves that exact material id/version provenance. The red interaction first failed on the missing material
  open action. All 35 React tests pass. In the rendered 610 px browser journey, the 578 px detail and 539.8 px form
  remained inside the document; changing the quantity to six and submitting increased the live list counter from
  zero to one and showed the German success status. The complete gate passed in 219.5 seconds with 19 Logistics plus
  build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green; L02 is verified.
- 2026-08-09: L01 now has a live Camp material lifecycle instead of read-only summaries. Writers create camp-wide
  or schedule-linked requirements with name, optional description, positive decimal standard/custom quantity,
  procurement status/source, note and Camp-directory responsibilities. The same complete form edits a loaded
  requirement with its current `If-Match`; the returned Version 2 is then used by a separately acknowledged delete
  that explains the 30-day trash period. Query caches reflect create, update and removal immediately, while archived
  Camps keep only the readable detail. The red journey first failed on the missing creation action, then proved the
  schedule and responsibility request, update `If-Match: "1"` and delete `If-Match: "2"`. All 36 React tests pass.
  At 610 px the 595 px document, 562.8 px material panel and 524.6 px form stayed inside the viewport; the rendered
  schedule selector and responsibility checkbox were exercised. The full gate passed in 236.8 seconds with 16
  Logistics plus build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green. L01 remains open for
  reachable material attachments and its final audit.
- 2026-08-09: L01 closes with private attachments on the loaded material detail. The shared owner panel uses only
  Files HTTP contracts: it lists the Camp quota and allowed PDF/JPEG/PNG/WebP metadata, validates the ten-MiB client
  limit, uploads with antiforgery and `ownerType=MaterialRequirement`, and opens content only through a short-lived
  read grant. Camp attachments add an independent 30-day trash confirmation; deletion consumes file Version 2 via
  `If-Match` and removes the active row. Archived Camps keep read access while upload/delete disappear. The red
  journey first failed because the material detail had no file region, then proved upload and trash while the
  existing recipe-file journey stayed green. All 37 React tests pass. At 610 px the 595 px document, 562.8 px detail,
  524.6 px file region and 465.4 px confirmation remained within the viewport, including a long filename. The full
  gate passed in 229.6 seconds with 16 Logistics, 28 Files plus build, format, lint, PostgreSQL RLS/privacy, cleanup,
  PWA and Help green; L01 is verified.
- 2026-08-09: S01 now opens a real versioned Andachts detail and renders its immutable Bible snapshot with reference,
  text excerpt, technical/display translation ids, license, attribution, provider/manual origin and retrieval date.
  No background request mutates the snapshot. The deliberately labelled refresh uses antiforgery and devotion
  `If-Match: "3"`; only the successful server response replaces the cached detail with Version 4. Typed
  reference-not-found, unavailable and timeout results explain that the existing snapshot remains usable. The red
  journey first failed on the missing open action. All 38 React tests pass. A 610 px browser journey exposed a cramped
  flex header, which was corrected to stack its 524.6 px title and toolbar; the 595 px document and 524.6 px snapshot
  then stayed inside the viewport with the long attribution. The rendered refresh changed text/date and showed the
  German success status while keeping license metadata. The full gate passed in 232.5 seconds with 24 Spiritual plus
  build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green. S01 remains open for full CRUD, manual
  snapshot fallback, provider-failure UX and private attachments.
- 2026-08-09: S01 now creates a provider-independent Andacht from the UI with the Contracts-owned four-translation
  catalog, optional ScheduleEntry link, Camp responsibility candidates, core message, Markdown content and material
  notes. The create call uses antiforgery and deliberately stores no Bible text. The returned Version 1 detail is
  opened directly without a racing refetch. Manual fallback reuses its reference and translation, sends antiforgery
  plus `If-Match: "1"`, and replaces the detail only with the server-attributed Version 2 immutable snapshot. The red
  journey first failed because **Andacht entwerfen** exposed no Thema field. All 39 React tests pass. At 610 px the
  595 px document, 562.8 px form/detail and 524.6 px snapshot stayed inside the viewport; the browser completed the
  full create/manual journey and rendered Public Domain plus **Manuell gespeichert**. The full gate passed in 228.3
  seconds with 24 Spiritual plus build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green. S01 remains
  open for edit/trash, explicit provider-failure journeys and private attachments.
- 2026-08-09: S01 now edits every field of an opened Andacht and chains the returned Version from detail into its
  summary. The update journey sent `If-Match: "2"`; only the returned Version 3 could enter the confirmed 30-day Camp
  trash. The active card and detail cache disappear only after the lifecycle endpoint returns success. The red journey
  first failed because the loaded detail offered no **Andacht bearbeiten** action. All 40 React tests pass. At 610 px
  the 595 px document, 562.8 px detail and 524.6 px edit/confirmation regions stayed inside the viewport. The browser
  rendered the warning, required its checkbox, removed the card and showed the German completion status. After a
  formatting-only first-gate failure in this ledger was corrected, the complete gate passed in 210.5 seconds with 24
  Spiritual plus build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green. S01 remains open for
  explicit provider-failure journeys and private attachments.
- 2026-08-09: S01 closes with explicit React journeys for reference-not-found, provider-unavailable and timeout; every
  result retains the existing text, attribution and retrieval date. The opened Andacht now composes the private Files
  routes with `ownerType=Devotion`: Camp quota, PDF/JPEG/PNG/WebP multipart upload, short-lived read grant and
  versioned 30-day attachment trash remain behind current owner authorization. Archived Camps keep reads but expose
  no file writes. The red journey first failed because **Dateien zu Licht der Welt** was absent. All 44 React tests
  pass. At 610 px the 595 px document, 562.8 px detail and 524.6 px snapshot/file regions remained within the viewport
  with a long filename. The browser rendered a provider-unavailable status while preserving the snapshot and showed
  quota, open/delete controls and upload help. The full gate passed in 240.1 seconds with 24 Spiritual, 28 Files plus
  build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green; S01 is verified.
- 2026-08-09: K01 starts with the shared Camp notebook creating pinned, comma-tagged notes through antiforgery and the
  versioned Knowledge HTTP contract. A compact toolbar inserts only the supported heading, bold, italic, list and
  HTTPS-link Markdown forms. The returned Version 1 detail opens from cache and renders only server-produced
  `RenderedHtml`; the local search narrows summaries by title, excerpt or tag. The red journey first failed because
  **Notiz anlegen** exposed no Titel field. All 45 React and 13 Knowledge tests pass. At 610 px the 595 px document and
  562.8 px form/detail stayed within the viewport; the browser rendered the pin, tags, heading and bold text without
  artifacts. The full gate passed in 244.4 seconds with build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and
  Help green. K01 remains open for revision, typed planning links, trash and private attachments.
- 2026-08-09: K01 now revises title, Markdown, tags, pin and the preserved link set atomically through `If-Match`,
  retaining the edit draft when the server reports a conflict. Moving an opened note to trash requires a named
  acknowledgement, uses the revised ETag and removes list/detail caches only after the server confirms the
  deterministic 30-day soft delete. The red journey first failed because **Notiz bearbeiten** was absent. All 46
  React and 13 Knowledge tests pass. At 610 px the 595 px document, revision form and trash confirmation stayed
  within the viewport; the browser showed the revised pinned card, safe Markdown and final German trash status. The
  full gate passed in 237.1 seconds with build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green. K01
  remains open for typed planning links, Camp trash restore and private note attachments.
- 2026-08-09: K01 now offers typed, multi-select links to authorized schedule entries, meals, recipes, material,
  shopping lists and devotions while a note form is open. Writes carry only target type and stable ID; the server
  validates scope and returns the trusted title snapshot, which the detail renders with a German type label. Existing
  selections reopen checked for atomic revision, and links explicitly grant no additional authorization. The red
  journey first failed because **Tagesplan: Morgenandacht** was absent. All 47 React and 13 Knowledge tests pass. At
  610 px the 595 px document, selector and linked detail remained within the viewport; the browser showed both
  selected targets without clipping or horizontal overflow. After the gate caught and corrected ledger formatting
  plus an unsafe test assertion, the complete run passed in 218.7 seconds with build, format, lint, PostgreSQL
  RLS/privacy, cleanup, PWA and Help green. K01 remains open for Camp trash restore and private note attachments.
- 2026-08-09: K01 now composes private Camp files below an opened note through `ownerType=Note`: quota, strict-format
  multipart upload, current-authorized short-lived open and versioned 30-day trash are available without crossing
  Knowledge/Files persistence boundaries. Archived Camps retain reads and hide file writes. The red journey first
  failed because **Dateien zu Teamabsprachen** was absent. All 48 React, 13 Knowledge and 28 Files tests pass. At 610
  px the 595 px detail and file region remained within the viewport with a long filename. Visual inspection caught
  the native English file-picker artifact; the shared control now renders the accessible German **Datei auswählen /
  Keine Datei ausgewählt** state. The full gate passed in 236.1 seconds with build, format, lint, PostgreSQL
  RLS/privacy, cleanup, PWA and Help green. K01 remains open only for a complete Camp trash restore journey.
- 2026-08-09: K01 closes with a complete aggregate Camp-trash journey for notes. Restore uses the module-provided
  path, antiforgery and current `If-Match`; after server confirmation the row disappears, a German title status is
  announced and both trash plus active notebook queries are refreshed. The red journey first failed because
  **Packliste wurde wiederhergestellt.** was absent. All 48 React, 13 Knowledge, 28 Files and 13 Activity tests pass.
  At 610 px the 595 px search/trash page showed the restored status and empty state without clipping or overflow. The
  full gate passed in 237.5 seconds with build, format, lint, PostgreSQL RLS/privacy, cleanup, PWA and Help green. K01
  is verified.
- 2026-08-09: P01 now builds an installable German PWA with manifest identity/icons, generated service worker,
  obsolete-cache cleanup, an explicit offline-ready notice and a user-controlled update prompt. The sole versioned
  browser snapshot is scoped to one Organization/Camp, timestamps and stores exactly schedule, meal plan, complete
  material, and complete shopping projections; a cold start resolves only that stored workspace. Offline routes
  perform no API reads or writes, expose only those four read-only planning areas, and replace identity,
  administration, notes, devotions, files, search and activity with an explicit unavailable state. Logout, current
  session revocation, Organization switch and leaving the active Organization purge the snapshot. The red journeys
  first exposed an unscoped partial snapshot, network-dependent workspace resolution and missing logout/update UX.
  All 68 React tests pass. At 610 px, Tagesplan, Logistik and a blocked note route stayed within the viewport; visual
  inspection corrected a false polling claim and a joined material summary. The full gate passed in 328.5 seconds
  with warning-free builds, PostgreSQL RLS/privacy, PWA manifest/service worker and Help green; P01 is verified.
- 2026-08-09: O01 closes the production operations boundary. `/health` is dependency-free liveness while `/ready`
  executes a PostgreSQL connection/query and returns 503 when its tagged dependency fails. Every HTTP response now
  carries a validated W3C-sized correlation id; unsafe input is replaced, and the same low-cardinality operation
  activities/scope correlate Migrator and Cleanup logs without domain identifiers. The final ordered migration
  provisions `freizeit_jobs` as NOLOGIN, NOSUPERUSER and NOBYPASSRLS with no database CREATE or INSERT. Explicit
  policies grant only cross-tenant SELECT/UPDATE/DELETE on all eight module schemas, and Cleanup always narrows its
  managed-identity connection through `SET ROLE`. An idempotent, manual Entra-admin SQL bootstrap creates the web and
  jobs principals according to current Microsoft Flexible Server guidance; no Azure command was executed. The red
  tests first showed liveness returning 503 with a failed readiness check, missing correlation headers, and an absent
  jobs role. The real PostgreSQL 17 cleanup test proves exact role attributes/privileges/policies and completes all
  retention plus privacy erasure. The full gate passed in 231.2 seconds with 35 API, 68 React, all module tests,
  warning-free builds, RLS/privacy, PWA and Help green; O01 is verified.
- 2026-08-09: Z01 prepares the parameterized, low-cost Azure target with azd/Bicep, Container Apps Web plus
  Migrator/Cleanup jobs, ACR Basic, PostgreSQL 17 B1ms/32 GiB/seven-day PITR, private LRS blob containers, Key Vault
  RBAC, separate web/jobs managed identities, Application Insights/Log Analytics and baseline alerts. Production
  Data Protection now persists a shared key ring in Blob Storage and wraps it with Key Vault through the explicit
  web managed identity; missing production settings fail at startup, and the newly introduced transitive XML
  cryptography package is pinned to patched 10.0.10. The PR/main workflow executes the same full gate. The separately
  approved manual production workflow uses OIDC only, publishes/executes Migrator first, waits for `Succeeded`, and
  only then releases Web and Cleanup with an evidence artifact. The first real Web image build failed because its
  slim Node stage had no PowerShell; the next exposed Windows pnpm links overwriting valid Linux links. Direct
  workspace builds after a clean in-image install fixed both failures. `pwsh ./scripts/validate-deployment.ps1`
  passed finally in 166.2 seconds with Azure CLI 2.88.0, Bicep 0.46.1, azd 1.30.0, actionlint 1.7.12 and Web/Migrator/Cleanup
  images all running as UID 1654. The final full application gate passed in 233.8 seconds with 39 API tests including seven
  operations/production cases and 68 React tests. Vendor chunking removed the final Vite size warning. README,
  MIT/third-party notices, module/data/deployment architecture, German help and the operations runbook cover first
  bootstrap, migration, rollback, restore, deletion, owner recovery, rotation and incident handling. No Azure login,
  cloud mutation, deployment, GitHub environment change, push or DNS change occurred; Z01 is verified.
- 2026-08-10: V01 closes the complete local verification harness. The initial coverage gate exposed only 59.25%
  backend lines and 42.45% branches; relational SQLite lifecycle tests, authenticated OpenAPI-driven endpoint
  coverage, error-mapping matrices, authorization regressions and boundary tests raised the final result to 91.09%
  lines and 75.02% branches (1,928/2,570). Frontend coverage is 87.14% lines and 75.10% branches. The real
  Aspire.Hosting.Testing path verifies AppHost health/readiness and Mailpit, while ArchUnitNET verifies module
  boundaries. `pwsh ./scripts/verify.ps1` passed in 626.3 seconds with zero build warnings/errors, generated-client
  drift, format, ESLint, TypeScript strict, all unit/API/integration/architecture tests, PostgreSQL RLS and privacy
  cleanup, PWA and VitePress builds green. Playwright completed 55 applicable cases with eight deliberate
  engine-specific skips across Chromium, Firefox and WebKit at 390x844, 834x1112 and 1440x1000; central routes,
  passwordless Mailpit login, keyboard access, axe, loading, server error, permission denial, genuine offline mode
  and version conflict passed. Current `logistik-{browser}-{viewport}.png` artifacts were visually inspected; the
  final mobile compact navigation, tablet single-column cards and desktop two-column workspace show no clipping,
  overflow, overlap, browser artifacts or broken controls. Deterministic desktop screenshots for Anmeldung,
  Freizeiten, Übersicht and Tagesplan were copied into the German help. The verify run invoked
  `scripts/smoke.ps1` against the real Aspire stack successfully. No Azure login, deployment or push occurred; V01
  is verified.
- 2026-08-10: I04/I05 replace the production passwordless entry path with an email/password flow backed by ASP.NET
  Core Identity password hashing and stateful asymmetric access/refresh JWTs. Access tokens remain in React memory;
  refresh families rotate, detect reuse and are revoked by logout, password reset, password change and global account
  suspension. Ten failed logins lock an account for exactly 15 minutes. Password reset uses a generic response and a
  single-use 60-minute Mailpit link. The first SuperAdmin is now created only through the one-time
  `/api/v1/auth/first-login` / `/erste-einrichtung` path, protected by a PostgreSQL advisory transaction lock; no
  bootstrap secret configuration is required. `pwsh ./scripts/verify.ps1` passed in 636.6 seconds with 90.8% backend
  line / 75% branch coverage and 86.33% frontend line / 75% branch coverage. PostgreSQL RLS/privacy cleanup, Aspire,
  generated-client drift, strict builds, Mailpit reset and 63 Playwright entries across Chromium, Firefox and WebKit
  passed (47 executed, 16 deliberate project-specific skips), including persistent refresh in a fresh Chromium
  browser context. No Azure deployment or push occurred.
