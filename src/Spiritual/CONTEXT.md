# Spiritual context

- Terms: Devotion is a camp draft; BibleSnapshot is immutable fetched/manual text plus provider metadata.
- Invariants: Schlachter 1951 is default; only `deu1951`, `deu1912`, `deuelo`, `deutkw`; snapshots refresh only on
  explicit action; provider outage never blocks manual work or existing snapshots; one optional schedule link.
- Roles: assigned CampLead/Member edit; Viewer reads. Responsibilities are display/filter metadata only.
- Contracts: devotion schedule linkage and summaries used by camp dashboard/search.
- Data/schema: owns `spiritual` and snapshot attribution in schema `spiritual`.
- Dependencies: Identity, Camps, Files and Activity; outbound BibleProvider seam has live and deterministic stub adapters.
