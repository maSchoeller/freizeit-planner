# Data architecture

PostgreSQL 17 is the transactional system of record. Each domain module owns a schema and an EF Core migration
history. Tenant tables carry `organization_id`; camp-owned tables additionally carry `camp_id`. Application filters
provide normal query scoping and forced PostgreSQL row-level security remains the independent final boundary.

The Web role assumes `freizeit_app` per opened connection and sets the current actor, organization and camp with
transaction-local settings. It cannot bypass RLS. The Cleanup host assumes the non-login `freizeit_jobs` role,
which has only the cross-tenant SELECT, UPDATE and DELETE policies required for retention. Migrator owns schema
evolution and is never used by the Web host.

Private attachments are stored as opaque blob names in the private `files` container. Metadata and authorization
remain in the Files schema; downloads require a current, actor-bound, single-use grant. ASP.NET Core Data Protection
keys use a separate private blob container and are wrapped by a Key Vault key in production.

Soft-deleted camp content has a deterministic 30-day purge deadline. Account and organization deletion have a
30-day grace period before the idempotent Cleanup job erases data module by module. Audit references that must
remain are pseudonymized with the shared non-identifying empty UUID. Backups are an operational recovery mechanism,
not a way to bypass deletion policy; restored environments must rerun due cleanup before serving traffic.
