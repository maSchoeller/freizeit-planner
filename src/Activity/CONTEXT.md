# Activity context

- Terms: `ActivityEvent` is an immutable metadata-only journal entry. `SearchDocument` is a bounded, replaceable
  projection with a source version and tombstone. Activity never owns domain objects or their soft-delete lifecycle.
- Invariants: journal rows contain only actor, organization, camp, kind, object reference, bounded title, timestamp,
  and numeric version. Never store content diffs, domain long text, tokens, secrets, or blob URLs. Search projections
  contain only bounded title, bounded search text, bounded filter metadata, source version, and projection version.
- Journal: `IActivityJournal` records and lists Created, Updated, Trashed, and Restored events. Recording requires
  `CampAction.WriteContent`; listing requires `CampAction.Read`. All queries are actor-authorized and scoped by both
  `OrganizationId` and `CampId`.
- Search: `ICampSearchIndex` provides idempotent, source-version-ordered upsert/remove operations and tenant-safe
  search with object-type and exact metadata filters. A removal writes a tombstone so stale deliveries cannot
  resurrect a projection. Fachmodule remain responsible for trashing, restoring, and purging their own objects.
- Export: `ICampExportFormatter` requires `CampAction.Export` and produces UTF-8 CSV with a BOM, German headers,
  CRLF rows, RFC-compatible quoting, and an apostrophe prefix for cells beginning with `=`, `+`, `-`, `@`, tab,
  carriage return, or line feed.
- Data/schema: owns `activity.activity_events` and `activity.search_documents`. Every row directly carries
  `organization_id` and `camp_id`; PostgreSQL RLS is enabled and forced. Search projection updates use numeric
  optimistic-concurrency versions.
- Dependencies: Identity Contracts supplies actor-aware camp authorization. Other modules may call only the narrow
  Activity Contracts and must coordinate their domain write plus event/index projection at the application boundary.
