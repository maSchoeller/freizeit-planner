# Authorization and row-level security

Identity exposes role-oriented interfaces. `ITenantAccessControl` decides organization and camp actions,
`ITenantAdministration` changes memberships and camp assignments, `ISuperAdminOrganizationAdministration` changes
organization status, and `IUserAdministration` manages global and scoped account rights. HTTP handlers never infer permission from responsibility or
from a client-supplied role. Every mutable membership, assignment, invitation, and organization row has an
application-managed version used through `ETag` and `If-Match`.

Each API request opens one database transaction after cookie authentication. The middleware stores `app.user_id`,
`app.organization_id`, `app.camp_id`, and a narrow operation name with transaction-local `set_config` calls. The
connection interceptor selects the `freizeit_app` runtime role, which is created as `NOLOGIN NOBYPASSRLS`.
Transaction-local values prevent pooled-connection context leakage.

PostgreSQL forces RLS on organizations, memberships, camp assignments, and invitations. Security-definer predicate
functions read the authoritative user, membership, organization-status, and camp-assignment rows. They deny globally
suspended accounts and suspended organizations, restrict lower roles to their own membership and camp assignment,
and permit broader rows only for OrganizationAdmin or the current camp's CampLead. SuperAdmin status alone never
grants tenant-content access; an explicit active Organization membership remains mandatory. Named `platform_admin`
operations allow current SuperAdmins to administer identity metadata across tenants. Invitation acceptance uses a
narrow endpoint-specific policy exception.

`scripts/test-rls.ps1` creates an isolated PostgreSQL 17 container, migrates and seeds it, asserts that the runtime
role has neither superuser nor bypass-RLS capability, then proves own-row visibility, foreign read/write denial,
SuperAdmin content isolation, global and organization suspension, administration boundaries, and transaction-context cleanup.

The separate `freizeit_jobs` non-login role also cannot bypass RLS. It receives no INSERT or DDL rights; named
per-table policies permit only cross-tenant SELECT, UPDATE, and DELETE for deterministic retention and erasure. The
Cleanup executable explicitly assumes that role, while the migration identity retains schema ownership without
being used by the interactive Web process.
