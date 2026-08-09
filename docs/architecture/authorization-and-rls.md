# Authorization and row-level security

Identity exposes three role-oriented interfaces. `ITenantAccessControl` decides organization and camp actions,
`ITenantAdministration` changes memberships and camp assignments, and `IPlatformAdministration` lists only
organization metadata and changes suspension status. HTTP handlers never infer permission from responsibility or
from a client-supplied role. Every mutable membership, assignment, invitation, and organization row has an
application-managed version used through `ETag` and `If-Match`.

Each API request opens one database transaction after cookie authentication. The middleware stores `app.user_id`,
`app.organization_id`, `app.camp_id`, and a narrow operation name with transaction-local `set_config` calls. The
connection interceptor selects the `freizeit_app` runtime role, which is created as `NOLOGIN NOBYPASSRLS`.
Transaction-local values prevent pooled-connection context leakage.

PostgreSQL forces RLS on organizations, memberships, camp assignments, and invitations. Security-definer predicate
functions read the authoritative user, membership, organization-status, and camp-assignment rows. They deny tenant
access to Platform Admin accounts, deny suspended organizations, restrict lower roles to their own membership and
camp assignment, and permit broader rows only for Owner/Admin or the current camp's Camp Lead. Platform operations
can read organization metadata only after an independent Platform Admin check and never receive membership or camp
rows. Invitation acceptance and first-Organization creation use named, endpoint-specific policy exceptions.

`scripts/test-rls.ps1` creates an isolated PostgreSQL 17 container, migrates and seeds it, asserts that the runtime
role has neither superuser nor bypass-RLS capability, then proves own-row visibility, foreign read/write denial,
Platform Admin isolation, suspension, platform metadata boundaries, and transaction-context cleanup.

The separate `freizeit_jobs` non-login role also cannot bypass RLS. It receives no INSERT or DDL rights; named
per-table policies permit only cross-tenant SELECT, UPDATE, and DELETE for deterministic retention and erasure. The
Cleanup executable explicitly assumes that role, while the migration identity retains schema ownership without
being used by the interactive Web process.
