# Operations and observability

## Process boundary

The Web host never migrates. `FreizeitCockpit.Migrator` obtains one PostgreSQL advisory lock and applies module
migrations in the documented Identity, Camps, Catering, Spiritual, Knowledge, Logistics, Files, Activity order.
Only Development receives deterministic sample data; the externally configured Platform Admin bootstrap remains
idempotent. `FreizeitCockpit.Cleanup` is a one-shot process and fails when a retryable retention area remains.

Web and jobs authenticate with separate user-assigned managed identities. The application then narrows database
permissions with non-login group roles:

| Principal/role        | Database capability                                                                                                         |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| jobs managed identity | creates/owns schemas and tables only for migrations                                                                         |
| `freizeit_jobs`       | cross-tenant SELECT, UPDATE and DELETE through explicit cleanup policies; no login, CREATE, INSERT, superuser or RLS bypass |
| web managed identity  | connects and may assume only `freizeit_app`                                                                                 |
| `freizeit_app`        | tenant-context RLS runtime DML; no login, superuser or RLS bypass                                                           |

Cleanup always issues `SET ROLE freizeit_jobs` through the same validated connection interceptor used by Web. The
role has three named RLS policies on every module table and deliberately has no INSERT privilege. A new module table
must grant the same bounded DML and create the three cleanup policies in its migration. The final Activity migration
provisions the current complete set, and the PostgreSQL cleanup smoke verifies every table plus the role attributes.

Production principal creation is an explicit administrator bootstrap, not an application startup side effect.
Run `scripts/bootstrap-database-principals.sql` first against the `postgres` database with the target database and
the two unique managed-identity display names as psql variables. Microsoft documents that
`pgaadauth_create_principal` must be invoked in `postgres`; the script then reconnects to the application database,
grants the migrator only database CREATE, and assigns the two non-login roles. No token or password belongs in the
script or repository. See [Manage Microsoft Entra roles](https://learn.microsoft.com/azure/postgresql/security/security-manage-entra-users)
and [Connect with managed identity](https://learn.microsoft.com/azure/postgresql/security/security-connect-with-managed-identity).

## Health and correlation

`/health` is process liveness and intentionally executes no dependency checks. `/ready` executes the tagged
PostgreSQL readiness check and returns 503 if a connection and `SELECT 1` cannot complete. Container Apps should use
the former for liveness and the latter for readiness; local smoke checks both.

ASP.NET Core, outbound HTTP, Npgsql and runtime metrics/traces are exported through OpenTelemetry to Aspire locally,
OTLP when configured, or Azure Monitor when Application Insights is configured. HTTP responses carry a validated
lowercase W3C-sized `X-Correlation-ID`; an absent or unsafe value is replaced instead of echoed. The same field scopes
Migrator and Cleanup logs and their `FreizeitCockpit.Operations` activities. Correlation values are log scope only,
never metric labels.

Telemetry records operation names, routes, durations, status and bounded aggregate cleanup counts. It must never
contain domain long text, request bodies, email addresses, identifiers for users/Organizations/Camps, tokens,
one-time codes, filenames, blob URLs or storage credentials.
