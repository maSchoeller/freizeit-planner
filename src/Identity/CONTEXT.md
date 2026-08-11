# Identity & Tenancy context

- Terms: Organization is the tenant; Membership grants an organization role; CampAssignment grants a camp role;
  Session is a revocable server-side JWT refresh family; Invitation is single-use.
- Invariants: organizations are invitation-created and may exist without an Orgadmin. A SuperAdmin receives global
  administration but no tenant-content access without an explicit active membership. Suspended organizations cannot
  operate; long sessions last 30 days and standard sessions 12 hours.
- Roles: SuperAdmin is global. OrganizationAdmin manages settings, deletion, Camps, members, Orgadmins and
  invitations inside one Organization. CampLead, Member, and Viewer are camp-scoped. Privilege escalation is
  rejected server-side; only the last active global SuperAdmin is protected from suspension or demotion.
- Contracts: current actor/access checks, member summaries, responsibility candidate lookup, organization status.
  `ICampMemberDirectory` returns only user ID and display name for active members who can read the requested Camp;
  it never exposes email addresses and is available to every actor who may read that Camp.
- Data/schema: owns `identity`; tenant rows include `organization_id`; memberships and assignments are authoritative.
- Lifecycle: accounts and Organizations are created through transferable bearer links. Invitation tokens and
  email-change codes are stored only as HMAC hashes. Account and organization deletion use a reversible 30-day
  schedule. Memberships may be left or removed even when no Orgadmin remains.
- Password authentication: ASP.NET Core Identity hashes passwords. Ten failures lock the account for exactly 15
  minutes. Access JWTs are asymmetric, expire after 15 minutes and remain only in browser memory. The strict,
  HttpOnly refresh cookie rotates against an HMAC hash in the revocable server session. Reuse revokes that family.
  A standard session lasts at most 12 hours; an opted-in session slides by 30 days.
- Password maintenance: reset requests are non-disclosing. A random reset token is stored only as an HMAC hash,
  expires after 60 minutes and is single-use. Reset and authenticated password change replace the security stamp
  and revoke every session. Password reauthentication updates only the current active session and is valid for ten
  minutes. A globally suspended account is rejected by login, refresh and stateful access-token validation.
- First Login: `GET/POST /api/v1/auth/first-login` and `/erste-einrichtung` are available only before the persistent
  initialization marker exists. PostgreSQL uses a transaction advisory lock so exactly one initial confirmed
  SuperAdmin can be created. The marker survives later account deletion, and migration marks every existing
  installation as initialized. There is no bootstrap identity or password in configuration, logs, migrations, or
  source control. The migrator deliberately leaves a new non-Development database empty for First Login.
- Transferable invitations: links contain no email address and are stored only as HMAC hashes. SuperAdmin links last
  one hour, Orgadmin links 48 hours, and Camp-role links seven days. A new registration reserves its link for one
  hour and is completed only after the one-time email confirmation; existing confirmed users attach the grant to
  their single global account. Rotation/revocation uses versions. The React client shows grant and terminal state
  before collecting first name, last name, email and password.
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
  assignment changes; `ISuperAdminOrganizationAdministration` exposes organization metadata and suspension, while
  `IUserAdministration` owns paged global/tenant user administration. All mutations
  use version tokens. The request transaction sets user, organization, camp, and operation context with transaction-
  local PostgreSQL settings before queries execute under the `freizeit_app` `NOBYPASSRLS` role.
- Dependencies: may emit metadata-only Activity events. All modules may depend on its Contracts, never implementation.
- Offline session boundary: explicit logout, revocation of the current browser session, leaving the active
  Organization, and switching Organizations purge the single local Camp planning snapshot. Identity, account,
  session and administration routes never render authenticated cached content while offline.
