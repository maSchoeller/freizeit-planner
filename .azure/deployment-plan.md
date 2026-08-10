# Freizeit-Cockpit deployment plan

Status: Ready for Cloud Validation

## Scope and deployment boundary

This repository is prepared for Azure Developer CLI (`azd`) with Bicep. The target is a
single production environment in parameterized Azure regions, defaulting to `germanywestcentral`.
This implementation session must not authenticate to Azure, provision resources, assign roles,
change DNS, publish images, or deploy the application. Only local builds, static validation, and
tests are permitted.

## Workloads

- `web`: public HTTPS Azure Container App serving the ASP.NET Core API, React SPA, and `/hilfe/`.
- `migrator`: Container Apps Job applying module migrations under a PostgreSQL advisory lock.
- `cleanup`: scheduled Container Apps Job purging expired and soft-deleted data after retention.
- Images are built from pinned Dockerfiles and stored in an Azure Container Registry Basic tier.

Container Apps use the Consumption profile, scale from zero to three replicas, and deliberately
accept cold starts. Web and jobs use separate user-assigned managed identities with least privilege.

## Azure resources

- Container Apps managed environment, web app, migration job, and cleanup job
- Azure Container Registry Basic
- PostgreSQL Flexible Server, Burstable B1ms, 32 GiB, seven-day backup, no HA
- StorageV2 Standard LRS account with private application and data-protection blob containers
- Key Vault Standard using RBAC
- Log Analytics with short retention and a daily cap, plus Application Insights
- separate user-assigned identities for web and jobs
- action group parameters and baseline availability, 5xx, latency, and database alerts
- declarative GitHub Actions OIDC/federated-identity inputs where Azure permits it

## Identity, networking, and secrets

Production uses managed identity and Entra authentication for PostgreSQL, Storage, Key Vault, and
ACR. No production database password, storage key, client secret, SMTP credential, or signing key is
committed or emitted as an ordinary output. Data endpoints remain public to control v1 costs but
require TLS, Entra, and RBAC. PostgreSQL may require a public firewall rule because Container Apps
Consumption does not provide stable outbound addresses; this is an accepted, documented v1 risk.
The later hardening path is workload-profile networking with stable egress and private endpoints.

ASP.NET Core Data Protection keys are stored in the private blob container and protected with Key
Vault. Blob data is private and downloads are issued only after current domain authorization.

## Parameters and outputs

Parameters include environment name, location, PostgreSQL administrator principal, GitHub
repository/environment, public base URL, optional custom domain, imprint URL, privacy URL, SMTP
settings references, log retention/cap, alert recipients, replica/capacity settings, and resource
tags. Outputs contain only non-secret resource names, endpoints, and identity client IDs.

The default Container Apps hostname is supported. Custom-domain and managed-certificate parameters
remain disabled until external DNS has been configured; this repository never mutates DNS.

## azd layout

- `azure.yaml` declares `web`, `migrator`, and `cleanup` container services/jobs.
- `infra/main.bicep` is the subscription entry point with environment-scoped modules.
- `infra/modules/` owns monitoring, identity, registry, database, storage, secrets, and Container Apps.
- `.azure/<environment>/.env` is local-only and ignored; no environment values are committed.
- GitHub workflows use OIDC and separate initial infrastructure bootstrap from repeatable releases.

## Cost decisions

The v1 favors Container Apps scale-to-zero, ACR Basic, PostgreSQL B1ms without HA, LRS Hot storage,
Key Vault Standard, and capped short-retention telemetry. It intentionally excludes Redis, VNet
integration, private endpoints, staging environments, and per-PR environments.

## Local validation

The full local gate:

1. build Bicep to ARM JSON without deployment;
2. validate `azure.yaml`, parameters, Dockerfiles, and workflow syntax statically;
3. build all container images locally;
4. run unit, integration, architecture, browser, accessibility, and smoke tests;
5. start the Aspire topology with PostgreSQL 17, Azurite, Mailpit, Bible stub, migrator, and web host;
6. verify health/readiness and inspect rendered UI screenshots.

On 2026-08-09, the final `pwsh ./scripts/validate-deployment.ps1` completed in 166.2 seconds with Azure CLI 2.88.0,
Bicep 0.46.1, azd 1.30.0, actionlint 1.7.12, Docker 29.6.1, and all three images configured for non-root UID 1654. `az bicep lint`, `az bicep build`, azd parsing, both workflow files, locked restores, frontend/help builds,
container publication and image metadata checks passed locally. The Web container build first exposed and then
verified the Linux pnpm-link ordering fix. No Azure authentication was used.

Cloud-backed subscription quota/provider checks, `az deployment ... validate`/`what-if`, role-assignment checks,
OIDC creation, and PostgreSQL Entra administrator validation are documented in the operations runbook and remain
manual first-bootstrap checks. They must not run in this repository implementation session.

## Operational safety

The web host never applies migrations. Releases run the idempotent migrator job first; a failed job
blocks the new web revision. Restore, rollback, owner recovery, deletion, secret rotation, and
incident procedures are maintained in the operations runbook. Production seeds no sample data; the
platform administrator is bootstrapped idempotently from external configuration only.

The hand-off is ready for the documented manual cloud validation and first bootstrap only after the repository's
complete application Verify and Aspire smoke gates are also green. Do not run `azd up`, `azd provision`, `azd
deploy`, or any Azure mutation as part of repository preparation.
