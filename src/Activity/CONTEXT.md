# Activity context

- Terms: ActivityEvent is metadata-only; SearchDocument is a minimal authorized projection; TrashEntry coordinates
  restoration and purge without owning source content.
- Invariants: never store complete diffs, long content, tokens, or sensitive values; search/export/feed always filter
  organization and camp; soft-deleted items purge after 30 days; CSV cells beginning formula markers are escaped.
- Roles: assigned users see camp activity/search; leadership/admin roles restore; Viewer may search/export/print.
- Contracts: record event, update/remove search projection, list activity/search, register trash callback metadata.
- Data/schema: owns `activity`; all tenant records carry organization_id and camp_id where relevant.
- Dependencies: Identity for current access; all modules may call narrow write Contracts.
