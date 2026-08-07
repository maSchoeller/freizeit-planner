# Logistics context

- Terms: MaterialRequirement is camp-wide or schedule-linked; ShoppingList is named; ShoppingItem unifies food,
  material and spontaneous sources; SourceReference preserves provenance.
- Invariants: no inventory/loan model; checked actor/time are recorded; incompatible units never auto-convert; list
  item state uses numeric Version for concurrent check-off.
- Roles: assigned CampLead/Member edit; Viewer reads/prints/exports; responsibility does not grant access.
- Contracts: accept reviewed source lines from Catering or Material; expose camp list summaries.
- Data/schema: owns `logistics` with organization_id and camp_id on all camp rows.
- Dependencies: Identity, Camps, Files and Activity; Catering may call its transfer Contract.
