# Camps & Schedule context

- Terms: Camp is a dated planning space; ScheduleEntry is the only source of linked date/time/location; Agenda is
  the accessible calendar alternative; Responsibility is display/filter metadata, not authorization.
- Invariants: slug unique per Organization; IANA zone defaults to Europe/Berlin; instants use UTC and all-day entries
  local dates; overlap is allowed/informational; archived camps are read-only; every mutation checks numeric Version.
- Roles: Owner/Admin see all camps; CampLead manages assigned camp; Member edits; Viewer reads/prints/exports.
- Contracts: camp lookup/access, schedule reference, atomic-workflow participation, responsibility view.
- Data/schema: owns `camps`, Camp and ScheduleEntry rows, no foreign keys to other module schemas.
- Dependencies: Identity authorization; Files/Activity; Catering and Spiritual call narrow schedule contracts.
