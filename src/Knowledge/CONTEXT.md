# Knowledge context

- Terms: Notebook is the single shared camp notebook; Note has title, sanitized Markdown, tags, pin and typed links.
- Invariants: no private notes, comments, wiki, raw HTML, tables, or embedded editor images; links target known planning
  object types without database foreign keys across module schemas.
- Roles: the assigned team reads the notebook; CampLead/Member edit; Viewer reads.
- Contracts: note summaries for search/dashboard and link target validation.
- Data/schema: owns `knowledge`; all notes carry organization_id and camp_id.
- Dependencies: Identity, Camps, Files and Activity.
