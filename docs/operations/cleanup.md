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

Blob deletion always precedes metadata deletion. A storage failure leaves metadata in place, increments the
retryable-failure count, and makes the process exit unsuccessfully so the scheduler retries it. Logs contain only
aggregate counts, never identifiers, email addresses, tokens, filenames, blob names, or content.

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
