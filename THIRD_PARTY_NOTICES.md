# Third-party notices

Freizeit-Cockpit includes open-source components. Their copyrights remain with their respective owners. The
repository lockfiles are the authoritative inventory of resolved versions; this notice groups the direct runtime
and shipped UI dependencies whose licenses or attribution are relevant to redistribution.

## Application and platform libraries

| Component family                                                                   | License            |
| ---------------------------------------------------------------------------------- | ------------------ |
| ASP.NET Core, Entity Framework Core, Aspire and Microsoft OpenAPI packages         | MIT                |
| Azure SDK for .NET, Azure Identity, Storage, Monitor and Data Protection providers | MIT                |
| Npgsql and Npgsql Entity Framework Core provider                                   | PostgreSQL License |
| OpenTelemetry .NET and OTLP instrumentation                                        | Apache License 2.0 |
| Mediator                                                                           | MIT                |

## Browser application and help

| Component family                                         | License |
| -------------------------------------------------------- | ------- |
| React, React DOM and React Router                        | MIT     |
| TanStack Query, React Hook Form, Zod, i18next and Luxon  | MIT     |
| FullCalendar core, React integration and bundled plugins | MIT     |
| Lucide icons                                             | ISC     |
| openapi-fetch                                            | MIT     |
| Vite, VitePress, Vue and vite-plugin-pwa                 | MIT     |
| Workbox                                                  | MIT     |

The complete license texts and copyright notices are available in the installed packages and their linked source
repositories. Build- and test-only tools are recorded in `packages.lock.json` and `pnpm-lock.yaml` and are not
part of the production application unless a generated artifact explicitly contains them.

## Bible texts

Freizeit-Cockpit does not bundle modern paid Bible translations. Every stored Bible snapshot carries its exact
translation identifier, displayed translation name, license and attribution. The UI displays those fields together
with the text and retrieval time. Supported identifiers are `deu1951`, `deu1912`, `deuelo` and `deutkw`; operators
must keep provider metadata and redistribution permissions under review. A manually entered text may only be stored
when the user has permission to use it.

This file is informational and does not replace the license text of any component or Bible source.
