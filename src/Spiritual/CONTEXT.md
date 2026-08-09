# Spiritual context

- Terms: Devotion is a camp draft; BibleSnapshot is immutable fetched/manual text plus provider metadata.
- Invariants: Schlachter 1951 is default; only `deu1951`, `deu1912`, `deuelo`, `deutkw`; snapshots refresh only on
  explicit action; provider outage never blocks manual work or existing snapshots; one optional schedule link.
- Roles: assigned CampLead/Member edit; Viewer reads. Responsibilities are display/filter metadata only.
- Contracts: devotion schedule linkage and summaries used by camp dashboard/search.
- Data/schema: owns `spiritual` and snapshot attribution in schema `spiritual`.
- Dependencies: Identity, Camps, Files and Activity; outbound BibleProvider seam has live and deterministic stub adapters.
- Interface: `IDevotionPlanning` owns versioned CRUD, soft-delete/restore, the curated translation catalog, explicit
  provider refresh, and explicit manual snapshots. `IBiblePassageProvider` is the single outbound provider seam;
  expected reference, timeout, and availability failures are typed results and never erase an existing snapshot.
- Trash: `ListTrashAsync` and restore require `CampAction.ManageCamp`; deleted summaries expose deterministic 30-day
  deadlines to the root aggregate trash. `IDevotionRetention` is cleanup-only and permanently removes due devotions
  plus every related immutable Bible snapshot in one transaction without materializing or validating domain content.
- `IDevotionCampContext` is a host-provided Camps adapter. Every mutation rechecks the authoritative archive state;
  archived Camps remain readable but cannot create, edit, trash, restore, or refresh devotion content. Create,
  update, and restore also validate every optional ScheduleEntry through the narrow writable-reference adapter, so
  missing, deleted, foreign, and archived-Camp links are rejected. The host atomically creates a ScheduleEntry and
  linked Devotion in one local Npgsql transaction and defaults an empty responsibility selection to the actor.
- Persistence: `SpiritualDbContext` stores versioned devotions and append-only Bible snapshots in schema `spiritual`.
  Both tables carry `organization_id` and `camp_id`; forced PostgreSQL RLS uses request-local Identity context, denies
  Platform Admin content access, and grants the runtime role no snapshot update/delete privilege.
- Provider metadata checked 2026-08-07: Free Use Bible API documents chapter JSON at
  `https://bible.helloao.org/api/{translation}/{book}/{chapter}.json`. Current upstream IDs are `deu_sch`, `deu_l12`,
  `deu_elo`, and `deu_tkw`, privately mapped to stable IDs `deu1951`, `deu1912`, `deuelo`, and `deutkw`.
  Schlachter 1951 is CC BY 4.0 with copyright attribution to Genfer Bibelgesellschaft; the other three eBible
  metadata pages mark the texts Public Domain. Sources: `https://bible.helloao.org/docs/reference/` and
  `https://ebible.org/Scriptures/details.php?id={stableId}`.
- Privacy maintenance deletes devotion and snapshot content for a claimed Organization and removes erased users from
  responsibility arrays. Processing is bounded and idempotent, and never calls the external Bible provider.
- The Camp Andachts page opens real versioned details and renders the current immutable snapshot together with
  technical translation id, display name, license, attribution, origin and retrieval date. Provider refresh is an
  explicit online-only action with antiforgery and the current devotion Version through `If-Match`; only a successful
  server response replaces the cached detail. Reference-not-found, unavailable and timeout statuses explain that the
  existing snapshot remains usable. No module persistence crosses the HTTP/Contracts boundary.
