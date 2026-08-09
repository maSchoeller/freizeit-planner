# Cleanup job

`FreizeitCockpit.Cleanup` is a one-shot process intended for the scheduled Container Apps Job and for local
operations. It exits successfully only after every configured retention area completes. The implementation is
idempotent: rerunning it after a partial failure processes only remaining due records.

## Current retention areas

- expired, used, or revoked login challenges, email-change challenges, invitations, and sessions;
- login rate events older than one day;
- notes whose deterministic 30-day trash deadline has passed;
- expired attachment read grants;
- attachment blobs and metadata whose deterministic 30-day trash deadline has passed.
- account erasure after the 30-day grace period, including pseudonymization/removal of actor and responsibility fields;
- complete Organization erasure after the 30-day grace period across all seven domain persistence areas.

Blob deletion always precedes metadata deletion. A storage failure leaves metadata in place, increments the
retryable-failure count, and makes the process exit unsuccessfully so the scheduler retries it. Logs contain only
aggregate counts, never identifiers, email addresses, tokens, filenames, blob names, or content.

Due erasures use two phases. Identity first claims a bounded candidate set. A claimed Organization enters the
non-interactive `Erasing` state, which cannot be cancelled or changed. Activity, Camps, Catering, Files, Knowledge,
Logistics, and Spiritual then erase only their own schema through `IDataErasure`. Account references that must remain
for audit are replaced with the common non-identifying empty UUID. Identity removes the account or Organization row
only when every required area reports that no records remain. Missing module registrations stop the process before
it claims candidates; partial runs are safe to retry.

## Configuration and local execution

The process uses `ConnectionStrings:freizeit`, either `ConnectionStrings:blobs` (Aspire/Azurite) or
`Storage:BlobServiceUri` (Azure), and the managed-identity settings shared by the other hosts. `Cleanup:BatchSize`
defaults to 100 and accepts values from 1 through 500.

Run the complete local stack through `pwsh ./scripts/dev.ps1`. To execute only one pass, provide the database and
blob configuration and run:

```powershell
dotnet run --project src/FreizeitCockpit.Cleanup/FreizeitCockpit.Cleanup.csproj
```

The scheduled job must use the dedicated jobs identity. It must never run as the interactive `freizeit_app` role,
because forced RLS intentionally prevents cross-tenant retention scans.

`pwsh ./scripts/test-cleanup.ps1` creates a temporary PostgreSQL 17 database, migrates it, executes both a due
Organization erasure and a due account erasure, and asserts domain deletion plus retained-audit pseudonymization.
