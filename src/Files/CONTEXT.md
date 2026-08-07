# Files context

- Terms: Attachment metadata points to a random private BlobName; AccessGrant is short-lived and scope-bound.
- Invariants: only PDF/JPEG/PNG/WebP; extension, declared MIME and magic bytes agree; 10 MiB each; 100 MiB per camp
  or organization recipe library; images may render, PDFs download; authorization is never encoded in blob names.
- Roles: authorization is delegated to the current owning domain object; Files cannot browse domain content.
- Contracts: validate/upload metadata, authorize download, delete/purge blobs, quota query.
- Data/schema: owns `files`; metadata includes organization_id and optional camp_id plus owner type/id.
- Dependencies: Identity for current actor/tenant state; called by content modules; may emit Activity metadata.
