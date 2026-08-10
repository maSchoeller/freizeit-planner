# Freizeit-Cockpit

Freizeit-Cockpit ist eine deutschsprachige, mandantenfähige Webanwendung für die gemeinsame Planung christlicher
Freizeiten. Der auslieferbare modulare Monolith kombiniert ASP.NET Core/.NET 10, PostgreSQL 17, React, eine
VitePress-Hilfe und eine begrenzte, schreibgeschützte Offline-PWA.

## Voraussetzungen

- PowerShell 7, Git und Docker Desktop beziehungsweise Docker Engine
- .NET SDK 10.0.x (durch `global.json` festgelegt)
- Node.js 24 oder neuer; pnpm 11.20.0 wird über `npm exec` verwendet
- für die lokale Deployment-Prüfung zusätzlich Azure CLI mit Bicep, Azure Developer CLI (`azd`) und `actionlint`

Eine Azure-Anmeldung ist für Entwicklung, Tests und die vollständige lokale Prüfung nicht erforderlich.

## Bootstrap und lokale Ausführung

```powershell
pwsh ./scripts/bootstrap.ps1
pwsh ./scripts/dev.ps1
```

`dev.ps1` startet die Aspire-Topologie mit PostgreSQL 17, Azurite, Mailpit, dem deterministischen Bibel-Stub,
Migrator und Web-Host. Die von Aspire ausgegebenen URLs führen zur Anwendung und zum Dashboard. Produktive
Beispieldaten werden nicht erzeugt.

## Tests und Qualitätsgates

```powershell
pwsh ./scripts/test.ps1
pwsh ./scripts/verify.ps1
pwsh ./scripts/smoke.ps1
pwsh ./scripts/validate-deployment.ps1
```

`verify.ps1` prüft generierte Artefakte, Formatierung, Build, TypeScript, Lint, alle .NET-/React-Tests, Coverage und
die Hilfe. `smoke.ps1` erwartet eine laufende lokale App auf `http://localhost:5041` und prüft Liveness, Readiness
und API. `validate-deployment.ps1` lintet und baut Bicep, parst die azd-Konfiguration, prüft die GitHub-Workflows
und baut alle drei Nicht-Root-Container. Es führt weder Login noch Azure-Deployment aus.

## Architektur

Die Anwendung ist ein deploybarer modularer Monolith. Acht Fachmodule besitzen jeweils Contracts,
Implementierung, DbContext, PostgreSQL-Schema, Migrationen und RLS-Regeln. Nur ein Contracts-Projekt darf eine
Modulgrenze überschreiten. `OrganizationId` und `CampId` begrenzen Datenzugriffe in Anwendung und Datenbank.

- [Kontext- und Modulkarte](CONTEXT-MAP.md)
- [Modularchitektur](docs/architecture/modules.md)
- [Datenhaltung](docs/architecture/data.md)
- [Autorisierung und RLS](docs/architecture/authorization-and-rls.md)
- [Offline-PWA](docs/architecture/pwa-and-offline.md)
- [Azure-Deployment](docs/architecture/deployment.md)
- [Betriebshandbuch](docs/operations/runbook.md)

Die deutsche Anwenderhilfe liegt in `src/Help/docs` und wird unter `/hilfe/` ausgeliefert.

## Häufige Probleme

- **Docker nicht erreichbar:** Docker Desktop starten und `docker version` erneut ausführen.
- **Falsches SDK/Node:** `dotnet --version` muss 10.0.x, `node --version` mindestens 24 ausgeben.
- **Ports belegt:** laufende Aspire-/Container-Prozesse beenden; die tatsächlichen URLs stehen im Aspire-Dashboard.
- **Paketsperre veraltet:** Abhängigkeiten nicht manuell im Lockfile ändern, sondern Restore im betroffenen Projekt
  ausführen und alle geänderten Lockfiles mitprüfen.
- **azd nach Installation nicht gefunden:** Terminal neu öffnen, damit der aktualisierte `PATH` gilt.
- **Readiness rot:** `/health` prüft nur den Prozess; `/ready` benötigt zusätzlich eine erreichbare PostgreSQL-
  Verbindung. Datenbank- und Aspire-Logs prüfen.

Der Anwendungscode steht unter der [MIT-Lizenz](LICENSE). Hinweise zu Drittkomponenten enthält
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
