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
  and is the polling/ETag value. Item `Version` protects independent item edits and check actions.
- `IShoppingTransfer` accepts reviewed catering or material lines atomically. Stored source references contain the
  exact meal/snapshot/ingredient/recipe version or material requirement/version and never change with the source.
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
- `ILogisticsRetention` is cleanup-only. It permanently removes material requirements at their deterministic
  30-day deadline in bounded batches; interactive restore also rechecks the current Camp archive state and deadline.
