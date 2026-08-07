# Freizeit-Cockpit progress

This file is the resumable evidence ledger. Commands are run from the repository root with PowerShell 7.

## Stable test seams

- HTTP acceptance seam: versioned `/api/v1` endpoints and RFC 9457 responses.
- Module seam: role-oriented public interfaces in each `*.Contracts` project.
- Browser seam: German user journeys through the real same-origin application.
- Infrastructure seam: Aspire resource graph, Docker image build, and static Bicep/azd validation.

## Slices

| Slice                                 | Status   | Acceptance criteria                                                                         | Red evidence                                       | Green/verify evidence                          | Docs                                      | Commit    | Blocker / next smallest step |
| ------------------------------------- | -------- | ------------------------------------------------------------------------------------------- | -------------------------------------------------- | ---------------------------------------------- | ----------------------------------------- | --------- | ---------------------------- |
| F01 foundation                        | verified | Pinned .NET/React/Aspire skeleton; bootstrap, build and test paths work                     | Foundation script failed on 41 absent paths        | Full verify passed in 63.3 s                   | deployment plan, AGENTS, contexts, skills | `f9f4022` | Start I01 with red HTTP test |
| I01 passwordless identity             | verified | Hashed six-digit code, expiry, attempts/rates, generic response, sessions and revoke        | Missing-CSRF API test returned HTTP 500            | Full verify passed in 87.0 s                   | Identity context, login/session help      | `84d5da1` | Start I02 invitation tests   |
| I02 invitations and account lifecycle | verified | Invite rotation/revoke; memberships; reauth; verified email; 30-day account/tenant deletion | Missing lifecycle contracts caused compile failure | Full verify passed in 86.9 s plus Aspire smoke | Identity context, account/role help       | `d14249c` | Start I03 authorization      |
| I03 tenant authorization              | pending  | Role matrix, last owner, suspension, IDOR protection and RLS isolation                      | pending                                            | pending                                        | auth/RLS docs                             | pending   | after I01                    |
| C01 camps                             | pending  | Camp lifecycle, slugs, archive read-only/reactivate, dashboard                              | pending                                            | pending                                        | Camps context, help                       | pending   | after I03                    |
| C02 schedule                          | pending  | Agenda/calendar CRUD, overlap, timezone/DST, all-day, ETag, atomic links                    | pending                                            | pending                                        | schedule help                             | pending   | after C01                    |
| T01 ingredients and recipes           | pending  | Normalize/merge, decimal units, recipe versions and attachments                             | pending                                            | pending                                        | Catering context, help                    | pending   | after I03                    |
| T02 meals and snapshots               | pending  | Portion scaling, stable/refreshable snapshots and atomic schedule workflow                  | pending                                            | pending                                        | Catering context, help                    | pending   | after T01/C02                |
| L01 material                          | pending  | Camp/schedule material, responsibilities and procurement status                             | pending                                            | pending                                        | Logistics context, help                   | pending   | after C02                    |
| L02 shopping                          | pending  | Named lists, unified sourced items, editable transfer, concurrent check-off and polling     | pending                                            | pending                                        | shopping help                             | pending   | after T02/L01                |
| S01 devotions and Bible               | pending  | Four translations, attribution, provider/stub, resilient immutable snapshots and refresh    | pending                                            | pending                                        | Spiritual context/notices/help            | pending   | after C02                    |
| K01 notebook                          | pending  | Safe Markdown notes, tags, pins and typed links                                             | pending                                            | pending                                        | Knowledge context, help                   | pending   | after I03                    |
| F02 attachments                       | pending  | Magic-byte/MIME/extension checks, quotas, private authorized image/PDF delivery             | pending                                            | pending                                        | Files context, help                       | pending   | after module CRUD            |
| A01 activity/trash                    | pending  | Metadata-only feed, soft delete/restore and deterministic 30-day purge                      | pending                                            | pending                                        | Activity context, help                    | pending   | after module CRUD            |
| A02 search/export/print               | pending  | Tenant-safe filtered search, CSV formula protection, German print views                     | pending                                            | pending                                        | help                                      | pending   | after A01                    |
| P01 PWA/offline                       | pending  | Install/update; read-only four-area snapshot; purge on logout/org switch                    | pending                                            | pending                                        | PWA docs/help                             | pending   | after CRUD                   |
| O01 operations                        | pending  | Migrator lock/order, cleanup, telemetry, health and correlation without sensitive logs      | pending                                            | pending                                        | runbook/architecture                      | pending   | after persistence            |
| Z01 Azure/CI                          | pending  | azd/Bicep/containers/workflows locally validate; no cloud mutation                          | pending                                            | pending                                        | deployment docs                           | pending   | after F01/O01                |
| V01 full verification                 | pending  | Format/lint/build/tests, coverage, three browsers/viewports, axe, visual inspection, smoke  | pending                                            | pending                                        | all docs/screenshots                      | pending   | after all slices             |

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
