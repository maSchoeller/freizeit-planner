# Files context

- Terms: an `Attachment` is owned by a strong `AttachmentOwnerReference`; metadata points to a cryptographically random,
  extension-free private `BlobName`. A read grant is an actor-bound, single-use capability whose hash, never its token, is
  persisted.
- Owner types: schedule entry, meal, recipe, material requirement, devotion, and note. Recipes use the organization recipe
  library (`camp_id IS NULL`); every other owner type is camp-scoped. Shopping items are deliberately not attachment owners.
- Contracts: `IAttachmentCatalog` lists, uploads, trashes/restores, and reports quota; `IAttachmentReader` issues and redeems
  read grants; `IAttachmentMaintenance` purges expired data; `IAttachmentOwnerAuthorization` is the mandatory callback into
  the owning module for current object existence, archive state, scope, and actor authorization.
- Upload invariants: only PDF/JPEG/PNG/WebP are accepted, with exact extension, declared MIME type, and magic bytes. The
  actual stream is capped at 10 MiB and must match any declared length. Each camp and organization recipe library has an
  independent 100 MiB quota. Pending reservations count toward quota, and PostgreSQL advisory transaction locks prevent
  concurrent uploads from oversubscribing it.
- Read invariants: blobs remain private and are streamed through the same-origin application. Images use inline disposition;
  PDFs use attachment disposition. Grants expire after 60 seconds, are bound to one actor, and are consumed atomically only
  after current owner authorization succeeds.
- Lifecycle: deleting moves metadata to the trash, revokes grants, and schedules purge after 30 days. Restore requires the
  current version and an unexpired purge deadline. Maintenance deletes the blob before hard-deleting metadata and leaves
  failed blob deletions retryable. `AttachmentMaintenanceService` is the cleanup-host-only implementation and also
  removes expired single-use read grants in bounded batches.
- Persistence/security: owns PostgreSQL schema `files`. Tenant rows carry `organization_id`; camp rows also carry `camp_id`.
  Forced RLS covers attachments and grants, rejects platform administrators from tenant content, and permits grants only to
  their actor. Pending-upload compensation may use the runtime delete policy; scheduled hard purge requires the privileged
  cleanup connection.
- Storage seam: production uses `AzurePrivateBlobStorage`, local development uses the same adapter with Azurite, and tests may
  use an in-memory `IPrivateBlobStorage`. Containers are explicitly private and writes are create-only.
- Dependencies: Files reads Identity tables only from its RLS security-definer function. Domain access remains behind the
  contracts callback; Files never reads another module's schema or domain content.
- Explicit exclusion: malware scanning is not part of this slice; format validation is not a malware scanner.
