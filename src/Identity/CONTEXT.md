# Identity & Tenancy context

- Terms: Organization is the tenant; Membership grants an organization role; CampAssignment grants a camp role;
  Session is a revocable server-side login; LoginChallenge stores only a code hash; Invitation is single-use.
- Invariants: organizations are invitation-created, at least one active Owner remains, Platform Admin cannot read
  tenant content, suspended organizations cannot operate, long sessions last 30 days and standard sessions 12 hours.
- Roles: PlatformAdmin is platform-only; Owner manages owners/admins/deletion; Admin manages lower roles; CampLead,
  Member, and Viewer are camp-scoped. Privilege escalation is rejected server-side.
- Contracts: current actor/access checks, member summaries, responsibility candidate lookup, organization status.
- Data/schema: owns `identity`; tenant rows include `organization_id`; memberships and assignments are authoritative.
- Lifecycle: Platform Admins create organizations only through 48-hour first-Owner invitations; team invitations
  last seven days. Invitation tokens and email-change codes are stored only as HMAC hashes. Account and organization
  deletion use a reversible 30-day schedule; the last active Owner invariant applies to leaving and account deletion.
- Runtime persistence: ASP.NET Core Identity users, hashed challenges, invitations, memberships, assignments, rate
  events and revocable sessions live in PostgreSQL through `IdentityDbContext`; the web host never migrates. The
  migrator uses an advisory lock and owns the only Development seed. Deterministic fakes live only in test projects.
- Dependencies: may emit metadata-only Activity events. All modules may depend on its Contracts, never implementation.
