# Knowledge context

- Terms: Notebook is the single shared camp notebook; Note has title, sanitized Markdown, tags, pin and typed links.
- Invariants: no private notes, comments, wiki, raw HTML, tables, or embedded editor images; links target known planning
  object types without database foreign keys across module schemas.
- Roles: the assigned team reads the notebook; CampLead/Member edit; Viewer reads.
- Contracts: note summaries for search/dashboard and link target validation.
- Data/schema: owns `knowledge`; all notes carry organization_id and camp_id.
- Dependencies: Identity, Camps, Files and Activity.

## Implemented module interface

- `ICampNotebook` is the role-oriented interface for list/get/create/revise, move-to-trash and restore. A Camp has
  one implicit shared notebook; there is no private-note owner, separate Notebook aggregate or per-field mutation.
- `IKnowledgeCampContext` is a host-provided inbound adapter carrying actor, Organization and Camp. It resolves the
  current archive state without allowing Knowledge to access Camps internals. Archived Camps remain readable, but
  every note mutation including trash and restore is rejected.
- `INoteLinkTargetResolver` validates typed planning-object references through root-owned adapters. Knowledge stores
  only type, stable ID and a trusted title snapshot; there are no cross-schema foreign keys. Note-to-note links are
  intentionally absent because wiki behaviour is outside v1.
- `INotebookRetention` is a cleanup-host-only interface. It permanently removes batches only after the 30-day
  soft-delete period and is never exposed as a Web endpoint. `KnowledgeRetentionService` is the privileged,
  authorization-free implementation used by that host; interactive code continues through `KnowledgeService`.

## Markdown and content rules

- The supported Markdown subset is headings, bold, italic, ordered/unordered lists and HTTPS links. The parser
  rejects raw HTML nodes, tables, editor images and unsafe link schemes before persistence.
- Rendering encodes all text and then applies a second element/attribute allowlist. Returned `RenderedHtml` is
  derived from the validated Markdown; callers never supply trusted HTML.
- Titles and tags use Unicode KC normalization plus collapsed whitespace. Tags are case-insensitively unique.
  Aggregate revision replaces title, Markdown, tags, pin and links together under one numeric Version.

## Authorization, retention and persistence

- Active reads use `CampAction.Read`; create/revise/trash use `WriteContent`; trash browsing and restore require
  `ManageCamp`. Responsibility is not an authorization boundary. All requests carry ActorId, OrganizationId and
  CampId, and all object lookups use the complete scope.
- `KnowledgeDbContext` owns notes, tags and links. Every row directly stores `organization_id` and `camp_id`;
  composite foreign keys prevent a child row from referencing a differently scoped note.
- PostgreSQL forces RLS on every Knowledge table through the non-bypass runtime role, using authoritative
  Organization access and exact transaction-local Camp context. The retention job is a separate composition root
  and uses the jobs database identity rather than the interactive runtime role.
- Every mutation carries an expected Version for host-level `If-Match`; trash records actor/time and a deterministic
  purge timestamp, while restore clears deletion metadata and increments Version.
- Activity/Search receive metadata or bounded summaries only. Markdown is never written to activity or diagnostic
  logs. Files attachments remain a later host/Files composition seam.
- Privacy maintenance deletes an Organization's complete notebook and pseudonymizes required creator/updater/trash
  audit fields with the non-identifying empty UUID before Identity finalizes an erased account.
