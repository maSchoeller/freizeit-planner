# Camps & Schedule context

- Terms: Camp is a dated planning space; ScheduleEntry is the only source of linked date/time/location; Agenda is
  the accessible calendar alternative; Responsibility is display/filter metadata, not authorization.
- Invariants: slug unique per Organization; IANA zone defaults to Europe/Berlin; instants use UTC and all-day entries
  local half-open date ranges; overlap is allowed/informational; archived camps are read-only except reactivation;
  DefaultPortions is positive; every existing-aggregate mutation checks numeric Version.
- Roles: Owner/Admin see all camps; CampLead manages assigned camp; Member edits; Viewer reads/prints/exports.
- Contracts: `ICampManagement` owns camp list/lifecycle, `ICampPlanningDefaults` exposes the narrow catering default,
  `ISchedulePlanning` owns agenda CRUD, and `IScheduleReferenceAccess` validates stable references for atomic
  Catering/Spiritual workflows. Contract views expose numeric Version for HTTP ETags. They never contain Meal or
  Devotion internals; those modules store their optional ScheduleEntryId.
- Time: timed input is an unspecified local DateTime resolved with the Camp IANA zone. DST gaps fail with
  `local_time_nonexistent`; repeated local times require explicit EarlierOffset/LaterOffset. Timed persistence is
  UTC `DateTimeOffset`; all-day persistence contains only local StartDate/EndDateExclusive. Ranges are half-open, so
  adjacent entries do not overlap.
- Data/schema: `CampsDbContext` owns `camps.camps`, `camps.schedule_entries`, and
  `camps.schedule_responsibilities`. Every row carries `organization_id`; schedule/responsibility rows also carry
  `camp_id`. There are no foreign keys to other module schemas. Forced PostgreSQL RLS uses Identity security-definer
  access functions and keeps Platform Admin, foreign Organization and unassigned Camp rows invisible.
- Authorization: application checks use `Identity.Contracts.ITenantAccessControl`; responsibility candidates must
  themselves have Camp read access. CampLead can manage its Camp, Member can edit schedule content, and Viewer is
  read-only. Archived Camp schedule writes and LinkForWrite reference checks fail with `camp_archived`.
- Concurrency/errors: updates, archive/reactivation, soft deletes and restores require ExpectedVersion; persistence
  uses EF concurrency tokens. Stable `CampsRuleException.ErrorCode` values map to German RFC-9457 responses in the
  host.
- UI: FullCalendar renders in the Camp IANA zone through its named-time-zone adapter. Drop and resize share the
  versioned update path with the accessible agenda form, update optimistically, and revert plus refetch on every
  failed response. The agenda form covers timed and all-day ranges without requiring pointer gestures. Create and
  edit forms select one or more responsibility candidates from Identity's minimized camp-readable directory;
  responsibilities affect presentation only and are revalidated by Camps on every write.
- Atomic workflows: the shared host begins one local Npgsql transaction, creates the ScheduleEntry, then gives its
  identifier to Catering or Spiritual, whose host adapters revalidate it through `IScheduleReferenceAccess` with
  `LinkForWrite`. A failure after Camps has persisted is rolled back across every enlisted DbContext. Linked deletion
  is composed outside Camps: the user must choose unlink or common trash first; the Web host performs the selected
  Catering/Spiritual changes before moving the ScheduleEntry to trash in the same request transaction. Camps never
  cascades into another module.
- Trash: active schedule reads hide soft-deleted entries. `ListTrashAsync` and restore require
  `CampAction.ManageCamp`; restore rejects archived Camps and elapsed 30-day deadlines. `IScheduleRetention` is
  cleanup-only and permanently deletes due entries plus responsibility rows in bounded batches.
- Dependencies: Identity authorization; Files/Activity; Catering and Spiritual call narrow schedule contracts.
- Privacy maintenance: the cleanup-only `IDataErasure` implementation deletes Organization-owned Camp aggregates in
  bounded batches and removes responsibility rows for an erased account. It has no interactive authorization seam.
