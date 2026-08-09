# Activity context

- Terms: `ActivityEvent` is an immutable metadata-only journal entry. `SearchDocument` is a bounded, replaceable
  projection with a source version and tombstone. Activity never owns domain objects or their soft-delete lifecycle.
- Invariants: journal rows contain only actor, organization, camp, kind, object reference, bounded title, timestamp,
  and numeric version. Never store content diffs, domain long text, tokens, secrets, or blob URLs. Search projections
  contain only bounded title, bounded search text, bounded filter metadata, source version, and projection version.
- Journal: `IActivityJournal` records and lists Created, Updated, Trashed, and Restored events. Recording requires
  `CampAction.WriteContent`; listing requires `CampAction.Read`. All queries are actor-authorized and scoped by both
  `OrganizationId` and `CampId`.
- The Web host enriches listed events with the current display name from Identity's minimized Camp member directory.
  A pseudonymized actor is shown as `Gelöschtes Konto`; an actor no longer in the current directory is shown as
  `Ehemaliges Teammitglied`. The journal remains the source for actor id, time, object type and bounded title.
- Search: `ICampSearchIndex` provides idempotent, source-version-ordered upsert/remove operations and tenant-safe
  search with object-type and exact metadata filters. A removal writes a tombstone so stale deliveries cannot
  resurrect a projection. The German UI exposes relevant, type-specific exact filters instead of accepting an
  arbitrary metadata expression. Fachmodule remain responsible for trashing, restoring, and purging their own objects.
- Export: `ICampExportFormatter` requires `CampAction.Export` and produces UTF-8 CSV with a BOM, German headers,
  CRLF rows, RFC-compatible quoting, and an apostrophe prefix for cells beginning with `=`, `+`, `-`, `@`, tab,
  carriage return, or line feed.
- Data/schema: owns `activity.activity_events` and `activity.search_documents`. Every row directly carries
  `organization_id` and `camp_id`; PostgreSQL RLS is enabled and forced. Search projection updates use numeric
  optimistic-concurrency versions. The real PostgreSQL verification inserts own and foreign-camp feed/search rows
  and asserts that the runtime role can neither read nor update the foreign projection.
- Print: the browser client offers scoped German print views next to schedule, meals, material and shopping. Print
  media rules keep the relevant read model and camp heading while removing navigation, forms and controls. No
  server-side office or PDF document is generated.
- Dependencies: Identity Contracts supplies actor-aware camp authorization. Other modules may call only the narrow
  Activity Contracts and must coordinate their domain write plus event/index projection at the application boundary.
- Trash composition: the Web root combines manager-authorized deleted summaries from Knowledge, Spiritual, Files, and Logistics,
  orders them chronologically, and returns versioned module restore paths. Activity does not acquire lifecycle ownership.
- The aggregate trash UI calls the returned module path with antiforgery and `If-Match`. On success it removes the
  item, announces its title in German and invalidates trash, search, activity plus the restored module's active
  query. The root endpoint maps Camps, Catering, Knowledge, Spiritual, Files and Logistics rule failures to a stable
  Problem Detail instead of leaking an unhandled server error. Activity still does not own or mutate lifecycle state.
- Privacy maintenance removes all journal/search rows for a claimed Organization. For account erasure in a retained
  Organization, immutable event actors are minimized to the shared pseudonymous UUID; titles and event semantics stay
  intact for the remaining tenant.
