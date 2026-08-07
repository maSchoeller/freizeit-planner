# Catering context

- Terms: Ingredient and Recipe are organization library records; RecipeVersion is immutable source material;
  RecipeSnapshot is meal-owned; Meal may link exactly one ScheduleEntry.
- Invariants: normalized ingredient name is organization-unique; quantities are decimal; conversion only inside mass,
  volume, or count; recipe edits never silently alter snapshots; meal people default is overridable.
- Roles: Owner/Admin maintain libraries and merge ingredients; assigned CampLead/Member edit camp meals; Viewer reads.
- Contracts: meal schedule linkage and source lines suitable for an explicitly edited shopping transfer.
- Data/schema: owns `catering`; organization library rows carry `organization_id`, meal rows also `camp_id`.
- Dependencies: Identity, Camps, Logistics transfer contract, Files and Activity.
