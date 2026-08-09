# PWA and offline boundary

The React frontend is built as an installable progressive web app by `vite-plugin-pwa`. Its German manifest defines
the application identity, standalone display mode, scope, colors, categories, and any/maskable application icons.
Workbox precaches the compiled application shell, excludes API and Help navigation from fallback handling, and
removes obsolete caches. Registration uses prompt mode: the UI announces both initial offline readiness and an
available application update; the user decides when a new service worker reloads the page.

## Deliberately bounded data snapshot

The service-worker cache is not an API data store. Authenticated planning data is copied only into one versioned
local-storage snapshot after successful online reads. The snapshot is scoped by `OrganizationId` and `CampId`,
records its synchronization timestamp and contains exactly four projections:

- schedule entries;
- meal-plan entries;
- material summaries and complete requirements;
- shopping-list summaries and complete lists/items.

Only the most recently used Camp is retained. Activating a different Organization clears both the snapshot and its
previous tenant association before any new Camp data is loaded. Explicit logout, revocation of the current session,
and leaving the active Organization also purge the snapshot. A cold offline start resolves the speaking Organization
and Camp slugs only from this scoped workspace envelope; it never invents tenant identifiers.

## Read-only enforcement

Offline mode disables all data queries except reads from the local snapshot. Its navigation exposes only schedule,
meals, material, and shopping. Every mutation control in these views is disabled or absent and no write queue,
background synchronization, or conflict reconciliation exists. The shell displays the offline state and exact
snapshot timestamp.

Routes for authentication, account/session management, Organization or Camp administration, notes, devotions,
files, search, activity, and other sensitive content render an explicit online-only state instead of cached content.
This client boundary complements server authorization and PostgreSQL RLS; it is not an authorization mechanism.
