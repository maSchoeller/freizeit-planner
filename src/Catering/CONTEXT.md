# Catering context

- Terms: Ingredient and Recipe are organization library records; RecipeVersion is immutable source material;
  RecipeSnapshot is meal-owned; Meal may link exactly one ScheduleEntry.
- Invariants: normalized ingredient name is organization-unique; quantities are decimal; conversion only inside mass,
  volume, or count; recipe edits never silently alter snapshots; meal people default is overridable.
- Roles: Owner/Admin maintain libraries and merge ingredients; assigned CampLead/Member edit camp meals; Viewer reads.
- Contracts: meal schedule linkage and source lines suitable for an explicitly edited shopping transfer.
- Data/schema: owns `catering`; organization library rows carry `organization_id`, meal rows also `camp_id`.
- Dependencies: Identity authorization plus host-provided Camp context; Logistics transfer, Files and Activity are
  later host composition seams.

## Implemented module interface

- `IOrganizationCateringLibrary` owns ingredient autocomplete, Unicode-KC name normalization, controlled
  preview/CAS merge, and immutable recipe version creation. Merge and rename append versions for affected current
  recipes; historical versions and meal snapshots are never rewritten.
- `ICampMealPlanning` owns meal CRUD, nullable portion overrides, copied recipe snapshots and the only explicit
  snapshot refresh operation. Meal deletion is a versioned 30-day soft delete; manager-only trash browsing and
  restore recheck Camp archive state, deadline and optimistic concurrency. `ICampCateringContext` is the inbound
  host adapter for current default portions and archive state, so this module does not depend on Camps internals or
  Contracts. Every meal mutation rejects an archived Camp.
- `IMealShoppingSource` returns source-stable, editable draft lines. The host passes reviewed quantities to the
  Logistics-owned transfer interface; Catering neither selects a shopping list nor persists shopping items.
- `Quantity` uses `decimal`. Automatic conversion is limited to g/kg and ml/l. Piece is compatible only with piece;
  a named count unit only with the same normalized name. Density conversion and package rounding do not exist.

## Persistence and authorization

- `CateringDbContext` owns ingredients, recipes and immutable versions plus camp meals and immutable snapshots.
  Every row directly stores `organization_id`; every meal/snapshot row also stores `camp_id`.
- The service calls `ITenantAccessControl` for every library or meal operation. PostgreSQL policies force RLS for
  every Catering table through the non-bypass runtime role, using organization context for libraries and exact camp
  context for meals and snapshots.
- Recipe and meal aggregate versions are numeric concurrency tokens. Caller-facing mutations carry the expected
  version for `If-Match` composition in the Web host.
- Active meal reads hide deleted rows. `IMealRetention` is cleanup-only and permanently deletes due meal aggregates,
  including their immutable recipe snapshots and snapshot ingredients, in bounded batches.
- Dietary tags and manually maintained allergen/kitchen notes are planning information, never a medical guarantee.
  The Web host composes linked ScheduleEntry deletion across Camps, Catering and Spiritual and requires an explicit
  unlink-versus-common-trash decision. Attachments and the atomic linked-create workflow remain host composition
  seams.
- Privacy maintenance deletes meals/snapshots, recipes/versions and ingredients for a claimed Organization in bounded,
  idempotent batches. Catering stores no user audit identifiers that require account pseudonymization.
