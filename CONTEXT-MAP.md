# Context map

Freizeit-Cockpit is one deployable modular monolith. The arrows below are synchronous calls through the target
module's public Contracts project; implementations and database schemas never cross boundaries.

| Module                          | Owns                                                                       | May call                                    |
| ------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------- |
| Identity & Tenancy (`Identity`) | users, sessions, organizations, memberships, roles, invitations            | Activity                                    |
| Camps & Schedule (`Camps`)      | camps, schedule entries, responsibility assignments                        | Identity, Activity, Files                   |
| Catering (`Catering`)           | ingredients, recipes, recipe snapshots, meals                              | Identity, Camps, Logistics, Activity, Files |
| Logistics (`Logistics`)         | material requirements, shopping lists and unified items                    | Identity, Camps, Activity, Files            |
| Spiritual (`Spiritual`)         | devotions and Bible snapshots                                              | Identity, Camps, Activity, Files            |
| Knowledge (`Knowledge`)         | shared camp notes, tags, typed links                                       | Identity, Camps, Activity, Files            |
| Files (`Files`)                 | attachment metadata, quotas, private blob lifecycle and access grants      | Identity for current access decisions       |
| Activity (`Activity`)           | metadata-only activity, cross-module search projection, trash coordination | Identity for current access decisions       |

Shared technical composition in the Web host coordinates cross-module transactions without becoming a ninth
domain module. Cross-module values are stable IDs and immutable contract records, never database foreign keys.
Each module owns an EF Core DbContext, PostgreSQL schema, migrations, and RLS policies. The runtime role cannot
bypass RLS; request-scoped tenant context is set transaction-locally.

Operations are a technical composition boundary, not a domain module. Migrator owns ordered schema evolution under
one advisory lock. Cleanup calls only public retention/erasure contracts and assumes the non-login `freizeit_jobs`
role, whose explicit cross-tenant policies allow SELECT/UPDATE/DELETE but no INSERT, DDL, or RLS bypass.

Atomic workflows: Camps+Catering creates schedule entry and meal; Camps+Spiritual creates schedule entry and
devotion; deletion of a linked entry requires an explicit unlink-or-trash choice. Catering/Logistics transfers
source-traceable positions into Logistics shopping lists. Files and Activity require prior/current domain
authorization and are not generic content-reading back doors.
