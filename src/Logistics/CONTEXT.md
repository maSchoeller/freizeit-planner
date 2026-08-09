# Logistics context

- Terms: MaterialRequirement is camp-wide or schedule-linked; ShoppingList is named; ShoppingItem unifies food,
  material and spontaneous sources; SourceReference preserves provenance.
- Invariants: no inventory/loan model; checked actor/time are recorded; incompatible units never auto-convert; list
  item state uses numeric Version for concurrent check-off.
- Roles: assigned CampLead/Member edit; Viewer reads/prints/exports; responsibility does not grant access.
- Contracts: accept reviewed source lines from Catering or Material; expose camp list summaries.
- Data/schema: owns `logistics` with organization_id and camp_id on all camp rows.
- Dependencies: Identity, Camps, Files and Activity; Catering may call its transfer Contract.

## Implemented planning slice

- `IMaterialPlanning` owns camp-wide and optional schedule-linked material requirements, including procurement
  state, notes and responsible users. Updates and soft deletes require the current material `Version`; deleted
  requirements disappear from active reads and can be listed/restored only with `CampAction.ManageCamp`.
- `IShoppingPlanning` owns multiple named lists and one unified item shape for spontaneous, catering and material
  sources. List `Version` protects structural mutations; `ChangeSequence` changes for every list or item mutation
  and is the polling/ETag value. List deletion is a 30-day soft delete that retains every item. Individual item
  deletion also preserves the full item and its check audit for 30 days. Only managers can browse or restore either
  lifecycle. Item `Version` protects independent item edits, check actions, deletion and restore.
- The Camp Logistics page reads real material summaries and shopping lists, creates named lists and opens one list
  with its unified source-aware items. Writers can add positive-decimal spontaneous items with standard or named
  custom units, optional store/note and the current list Version as `If-Match`. Check/reopen sends the independent
  item Version, updates the local list and counters immediately, and displays the checking Camp member plus server
  timestamp. List summaries and the selected detail poll every 15 seconds and refetch on window focus; archived Camp
  workspaces remain read-only.
- The Camp material UI creates camp-wide or schedule-linked requirements and edits the full contract: name,
  description, positive decimal quantity/custom unit, procurement status/source, note and responsibility-directory
  users. Create uses antiforgery; update consumes the independently loaded material Version through `If-Match` and
  chains the returned Version into a separately confirmed 30-day soft delete. Active query caches update without
  demo data, and archived Camp workspaces hide every material mutation.
- Any active sourced or spontaneous item can be revised without changing its immutable source label/reference. The
  edit UI covers name, decimal quantity/unit, optional store/note and Camp-directory responsibilities, and sends the
  independent item Version as `If-Match`. Item deletion requires a separate acknowledgement, consumes the newest item
  Version and removes only the active row after the server moves it into 30-day trash. List rename consumes the
  current structural Version; whole-list deletion has its own acknowledgement and moves the aggregate and every item
  into 30-day trash. Returned list/item versions are chained through consecutive UI actions.
- The host records each shopping-item create, update/check, trash and restore as its own metadata-only Activity event
  with actor, object type and bounded item name, while also refreshing the parent list projection. No item notes or
  other long content are copied into the immutable journal.
- `IShoppingTransfer` accepts reviewed catering or material lines atomically. Stored source references contain the
  exact meal/snapshot/ingredient/recipe version or material requirement/version and never change with the source.
- The Camp material detail resolves responsible users through the minimized Identity directory and exposes a
  reviewed material-to-list transfer. Writers choose any current Camp list and may revise name, positive decimal
  quantity/unit, store, note and responsibilities before submission. The request carries antiforgery, the target
  list Version as `If-Match` and body precondition, plus the independently loaded material Version; archived Camps
  hide the transfer mutation. The resulting shopping item retains that exact material id/version provenance.
- Material details compose the Files-owned attachment contract through public HTTP endpoints. The shared owner panel
  lists private Camp attachments and quota, uploads only allowed PDF/JPEG/PNG/WebP files up to ten MiB with
  antiforgery, and issues short-lived read grants without exposing blob URLs. A separately confirmed delete consumes
  the attachment Version through `If-Match` and moves the file into the common 30-day Camp trash; archived Camps keep
  read access but hide upload/delete. Logistics does not access Files persistence or internals.
- The host-composed meal transfer UI reads Catering-owned draft lines without exposing module internals, offers any
  current Camp list as the target, limits the unit selector to the draft's compatible units and never performs
  package rounding. The reviewed batch reaches `IShoppingTransfer` with antiforgery and the selected list Version;
  archived Camp workspaces do not expose the mutation.
- `IShoppingAudit` exposes immutable check/reopen events with server-recorded actor, timestamp and resulting item
  version. Audit rows have no update/delete runtime policy and are not cascaded from items.
- `LogisticsQuantity` stores positive `numeric(18,6)` values. Only gram/kilogram and millilitre/litre convert;
  pieces only combine with pieces, and normalized custom units only with an exact normalized match.
- The implementation consumes `ICampPlanningDefaults` for archive protection and
  `IScheduleReferenceAccess` for schedule-link validation. Identity contracts enforce CampLead/Member writes,
  Viewer reads, and validation of responsible users without granting access through responsibility.
- The `logistics` schema owns requirements, responsibilities, lists, items and audit events. Every table carries
  `organization_id` and `camp_id`; PostgreSQL policies force RLS and consult only Identity-owned authorization
  functions/tables.
- Privacy maintenance deletes all Organization-owned logistics aggregates and check events. Account erasure removes
  responsibility rows and replaces required check/audit actor identifiers with the shared pseudonymous UUID.
- `ILogisticsRetention` is cleanup-only. It permanently removes material requirements, individual shopping items,
  and shopping-list aggregates at their deterministic 30-day deadline in bounded batches. Item purge removes its
  responsibilities and immutable check audit; list purge removes the entire aggregate and audit. Both run in a
  transaction, and interactive restore rechecks the Camp archive state and deadline.
