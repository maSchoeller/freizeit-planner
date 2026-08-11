# Operations runbook

This runbook covers the single v1 production environment. Replace every `<PLACEHOLDER>` deliberately and record the
change ticket, operator, UTC time, commit and result. Never paste tokens, one-time codes, email bodies, blob URLs or
domain long text into tickets or logs.

## Safety boundary

This repository session performs no Azure login, deployment, role assignment, DNS change or GitHub environment
mutation. The commands marked **cloud/manual** are hand-off procedures only. Local release evidence is produced with:

```powershell
pwsh ./scripts/verify.ps1
pwsh ./scripts/validate-deployment.ps1
pwsh ./scripts/test-browser.ps1 -GoogleChrome -Grep '@smoke'
```

The complete verify gate covers Chromium, Firefox and WebKit. The additional command runs the tagged sign-in,
invitation and administration journeys with the locally installed Google Chrome channel at 390x844, 834x1112 and
1440x1000. `scripts/test-browser.ps1` starts the real Aspire/PostgreSQL/Mailpit stack when necessary and invokes
`scripts/smoke.ps1` before Playwright.

The deployment plan is [`.azure/deployment-plan.md`](../../.azure/deployment-plan.md). `azd provision` is reserved
for controlled infrastructure changes; normal application releases use the manual GitHub production workflow.

## Manual first bootstrap

Prerequisites: an approved Azure subscription, an Entra PostgreSQL administrator group, a GitHub `production`
environment with required reviewers, and an operator allowed to create identities and RBAC assignments.

1. **Cloud/manual:** authenticate interactively, create an azd environment and set all values declared by
   `infra/main.parameters.json`. Secret values are entered only into the local ignored azd environment or an approved
   secret source.
2. Run `azd provision --no-prompt` from the reviewed commit. Do not use `azd up`, because application publication is
   a separate gated operation.
3. Connect to the Flexible Server's `postgres` database as its configured Entra administrator and run:

   ```powershell
   psql "host=<SERVER>.postgres.database.azure.com dbname=postgres sslmode=require" `
     --set=database_name=freizeit `
     --set=web_principal_name=<WEB_IDENTITY_NAME> `
     --set=jobs_principal_name=<JOBS_IDENTITY_NAME> `
     --file scripts/bootstrap-database-principals.sql
   ```

4. Verify the two Entra principals can connect, `freizeit_app` and `freizeit_jobs` remain `NOLOGIN`/
   `NOBYPASSRLS`, and the jobs principal has no direct INSERT or DDL path.
5. Configure GitHub environment variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
   `AZURE_ENV_NAME` and `AZURE_LOCATION`. Use the federated deployment identity created for the exact repository and
   `production` environment; do not create a client secret.
6. Configure external DNS before setting a custom domain. Bicep never changes DNS. Set real `PublicBaseUrl`,
   `ImprintUrl` and `PrivacyUrl`; the repository contains no invented legal text.
7. Trigger **Produktion veröffentlichen** from `main`, type `DEPLOY`, approve the environment and retain the produced
   deployment evidence.
8. Open `/erste-einrichtung` once, create the initial Superadmin with a password-manager-generated password, and
   verify successful sign-in. The route closes permanently through the database initialization marker. Do not place
   the email address or password in deployment configuration, tickets, logs, or command history.

Before bootstrap, manually check regional provider availability and quota. The non-mutating local substitute is not
authoritative for a subscription. **Cloud/manual, do not run during repository implementation:**

```powershell
az provider show --namespace Microsoft.App --query registrationState
az postgres flexible-server list-skus --location germanywestcentral
az deployment sub validate --location germanywestcentral --template-file infra/main.bicep --parameters infra/main.parameters.json
az deployment sub what-if --location germanywestcentral --template-file infra/main.bicep --parameters infra/main.parameters.json
```

## Release and migration

The production workflow is manual, OIDC-only and restricted to `main`. It runs the complete verify gate, publishes
the Migrator image, starts the manual job and polls the exact execution. Only `Succeeded` permits a Web deployment.
The Web host never applies migrations. Cleanup is deployed last and keeps its configured schedule.

Migrations must be forward-compatible with the currently active Web revision: add before use, backfill in bounded
steps and remove only in a later release. The Migrator serializes all module migrations with one PostgreSQL advisory
lock. On failure, preserve logs and execution ID, fix the migration or restore; never mark a failed execution as
successful and never deploy Web manually around the gate.

## Health and incident triage

- `/health` proves that the Web process responds and deliberately ignores dependencies.
- `/ready` includes PostgreSQL and must be healthy before a revision receives traffic.
- Application Insights and Log Analytics receive OpenTelemetry; correlation IDs are lowercase W3C trace IDs.
- Alerts cover synthetic health, increased 5xx, latency and failed PostgreSQL connections.

Incident sequence:

1. Declare owner, severity, start time and affected capability; protect evidence and stop unrelated changes.
2. Check Azure Resource Health, active Container App revision, `/health`, `/ready`, PostgreSQL state and recent
   Migrator/Cleanup executions.
3. Query by time window and correlation ID. Do not add user IDs, Organization/Camp IDs or long text as telemetry
   dimensions. Container system/console streams may be viewed with `az containerapp logs show`; job execution status
   is available through `az containerapp job execution show`.
4. Contain with a known-good revision, disabled schedule or credential rotation as appropriate. Do not weaken RLS,
   firewall, TLS or RBAC to regain service.
5. Confirm recovery with health, readiness and a synthetic sign-in/planning journey. Record cause, impact, data
   exposure assessment and follow-up without sensitive payloads.

## Rollback

Container Apps single-revision mode keeps the old revision serving until the new revision passes probes. For an
application-only regression, redeploy the last known-good commit through the same production workflow. Alternatively,
an authorized operator may copy/activate the prior inactive revision after confirming its image digest.

Do not roll application code behind an incompatible schema. If the migration was additive, leave it in place and
roll back only the image. If it changed data incompatibly, stop writes, perform point-in-time restore to a new server,
validate it, update the reviewed infrastructure/configuration to the restored host, rerun current Migrator/Cleanup,
then restore traffic. Never run automatic EF down-migrations in production.

## PostgreSQL point-in-time restore

Flexible Server PITR creates a new server in the same region; it does not overwrite the source. Select a UTC restore
time before the incident and keep the source isolated for evidence. **Cloud/manual:**

```powershell
az postgres flexible-server restore `
  --resource-group <RESOURCE_GROUP> `
  --name <NEW_SERVER_NAME> `
  --source-server <SOURCE_SERVER_NAME> `
  --restore-time <UTC_ISO8601>
```

Reapply/verify Entra administrator, managed identity, diagnostics and firewall configuration on the restored server.
Run principal bootstrap against `postgres`, verify RLS and migrations in the `freizeit` database, then run all due
cleanup before exposing the restored copy. Validate attachment metadata against blob existence. Keep the former
server until recovery acceptance and retention approval; deletion is a separate reviewed action.

## Deletion and retention

User-initiated account and Organization deletion has a 30-day grace period. Cleanup claims bounded candidates and
erases every module idempotently. Blob deletion precedes attachment metadata deletion; transient storage failures
leave metadata for retry and make the job fail. See [cleanup.md](cleanup.md).

For a production teardown, first export legally required operational evidence, confirm retention/deletion approval,
disable ingress and schedules, then remove the dedicated resource group through the reviewed infrastructure process.
Key Vault purge protection and service backup retention intentionally prevent immediate physical purge. Never use
`azd down` as an unreviewed convenience command.

## Administrator recovery

Recovery uses an active Superadmin and the versioned application APIs. Verify the requester's identity out of band
with two authorized people. Restore an Orgadmin only when the organization still exists and is not `Erasing`; do not
alter database membership rows, disable RLS or impersonate a user. Record the target organization, approvers and
resulting audit event without copying personal content. The last active Superadmin cannot be suspended or demoted.
If its credentials are unavailable, use the password-reset flow after verifying mailbox control; First Login never
reopens and no hidden recovery account is seeded.

## Secret and key rotation

Managed identities have no client secrets. Rotation scope is limited to authentication-token pepper, invitation-token pepper,
SMTP credential and the Data Protection wrapping key.

- For peppers, create a new Key Vault secret version and deploy/restart Web. Existing one-time codes or invitations
  become invalid by design; notify operators, not individual token values.
- For SMTP, rotate at the provider first, write a new Key Vault version, restart Web and send a synthetic message.
- Rotate the Key Vault `data-protection` key by creating a new version. Keep all old versions enabled because existing
  key-ring entries still require them for unwrap. Update to a versionless key URI before enabling automatic rotation.
- Restart affected revisions after a secret update; Container Apps secrets are application-scoped and running
  revisions do not automatically reload process configuration.

If compromise is suspected, rotate immediately, revoke the exposed credential at its source, inspect access logs and
perform the incident/data-exposure assessment before disabling old cryptographic material.

## Network risk and hardening path

Consumption Container Apps has dynamic egress, so v1 uses the PostgreSQL `AllowAzureServices` firewall rule. TLS,
Entra authentication, least-privilege database roles and forced RLS remain mandatory, but the public endpoint and
broad Azure-source reachability are residual risks. Storage and Key Vault are likewise public endpoints protected by
TLS, RBAC and managed identity.

The later hardening path is a workload-profiles environment with VNet integration, stable controlled egress, private
DNS and private endpoints for PostgreSQL, Storage and Key Vault. This is an explicit v1 product boundary, not work to
simulate with application-level IP allowlists.
