# Identity & Tenancy context

- Terms: Organization is the tenant; Membership grants an organization role; CampAssignment grants a camp role;
  Session is a revocable server-side JWT refresh family; Invitation is single-use.
- Invariants: organizations are invitation-created, at least one active Owner remains, Platform Admin cannot read
  tenant content, suspended organizations cannot operate, long sessions last 30 days and standard sessions 12 hours.
- Roles: PlatformAdmin is platform-only; Owner manages owners/admins/deletion; Admin manages lower roles; CampLead,
  Member, and Viewer are camp-scoped. Privilege escalation is rejected server-side.
- Contracts: current actor/access checks, member summaries, responsibility candidate lookup, organization status.
  `ICampMemberDirectory` returns only user ID and display name for active members who can read the requested Camp;
  it never exposes email addresses and is available to every actor who may read that Camp.
- Data/schema: owns `identity`; tenant rows include `organization_id`; memberships and assignments are authoritative.
- Lifecycle: Platform Admins create organizations only through 48-hour first-Owner invitations; team invitations
  last seven days. Invitation tokens and email-change codes are stored only as HMAC hashes. Account and organization
  deletion use a reversible 30-day schedule; the last active Owner invariant applies to leaving and account deletion.
- Password authentication: ASP.NET Core Identity hashes passwords. Ten failures lock the account for exactly 15
  minutes. Access JWTs are asymmetric, expire after 15 minutes and remain only in browser memory. The strict,
  HttpOnly refresh cookie rotates against an HMAC hash in the revocable server session. Reuse revokes that family.
  A standard session lasts at most 12 hours; an opted-in session slides by 30 days.
- Password maintenance: reset requests are non-disclosing. A random reset token is stored only as an HMAC hash,
  expires after 60 minutes and is single-use. Reset and authenticated password change replace the security stamp
  and revoke every session. Password reauthentication updates only the current active session and is valid for ten
  minutes. A globally suspended account is rejected by login, refresh and stateful access-token validation.
- First Login: `GET/POST /api/v1/auth/first-login` and `/erste-einrichtung` are available only while no user exists.
  PostgreSQL uses a transaction advisory lock so exactly one initial confirmed SuperAdmin can be created. There is
  no bootstrap password in configuration, logs, migrations, or source control.
- Runtime persistence: ASP.NET Core Identity users, invitations, memberships, assignments, rate
  events and revocable sessions live in PostgreSQL through `IdentityDbContext`; the web host never migrates. The
  migrator uses an advisory lock and owns the only Development seed. Deterministic fakes live only in test projects.
- Retention: `IIdentityMaintenance` is available only to the cleanup composition root. Each bounded run deletes
  expired or consumed email challenges and invitations, expired or revoked sessions, and rate-limit events
  older than one day. After 30 days it atomically claims due account and Organization erasures. Claimed Organizations
  enter `Erasing` and become inaccessible; cancellation and status changes are then rejected. Identity records are
  finalized only after every registered Fachmodule reports its own deletion/pseudonymization complete. The job never
  logs token values, email addresses, IP addresses, identifiers, or other identity payloads.
- Authorization: `ITenantAccessControl` answers organization/camp decisions; `ITenantAdministration` owns role and
  assignment changes; `IPlatformAdministration` exposes only organization metadata and suspension. All mutations
  use version tokens. The request transaction sets user, organization, camp, and operation context with transaction-
  local PostgreSQL settings before queries execute under the `freizeit_app` `NOBYPASSRLS` role.
- Dependencies: may emit metadata-only Activity events. All modules may depend on its Contracts, never implementation.
- Offline session boundary: explicit logout, revocation of the current browser session, leaving the active
  Organization, and switching Organizations purge the single local Camp planning snapshot. Identity, account,
  session and administration routes never render authenticated cached content while offline.
