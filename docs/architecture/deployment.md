# Deployment architecture

`azure.yaml` maps Web, Migrator and Cleanup to Azure Container Apps. Subscription-scoped Bicep in `infra/` creates a
single low-cost production environment in parameterized regions, defaulting to Germany West Central.

The Web Container App has public HTTPS ingress, scale-to-zero and at most three replicas by default. Migrator is a
manual Container Apps Job; Cleanup is a scheduled job. All three use pinned multi-stage Dockerfiles and run as the
.NET base image's non-root user. A Basic Azure Container Registry stores images.

PostgreSQL Flexible Server uses version 17, Burstable B1ms, 32 GiB, seven-day backup and no HA. Storage is Standard
LRS/Hot with private containers. Key Vault uses RBAC, soft delete and purge protection. Log Analytics, Application
Insights, health/5xx/latency/database alerts and an optional action-group email provide the v1 operations baseline.

Web and jobs have separate user-assigned managed identities. PostgreSQL uses Entra authentication without an
application password; Storage, Key Vault and ACR use RBAC. Data endpoints remain publicly addressable in v1. The
PostgreSQL `AllowAzureServices` firewall rule is the explicit cost trade-off needed for dynamic Consumption egress;
the hardening path is stable egress, VNet integration and private endpoints.

GitHub Actions uses OIDC and an environment-scoped deployment identity. The initial infrastructure bootstrap is a
manual privileged operation. Repeatable releases run the same verify gate, deploy and execute Migrator, wait for a
successful execution, then deploy the Web revision and Cleanup image. See the
[operations runbook](../operations/runbook.md) for bootstrap, rollback and incident procedures.
