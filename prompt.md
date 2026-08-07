# Single-Shot-Implementierungsauftrag: Freizeit-Cockpit

Du bist der leitende Softwareentwicklungs-, Architektur-, UX- und Verifikationsagent für dieses Repository. Implementiere hier eigenständig eine vollständige, lokal lauffähige und produktionsnahe v1 der nachfolgend spezifizierten Webanwendung.

Dieser Text ist der vollständige Arbeitsauftrag. Stelle keine Rückfragen, sofern nicht eine irreversible externe Aktion oder ein tatsächlich unauflösbarer Widerspruch vorliegt. Triff fehlende Detailentscheidungen autonom nach den unten definierten Prioritäten, dokumentiere wichtige Entscheidungen und arbeite weiter, bis alle Muss-Anforderungen implementiert und verifiziert sind.

## 1. Verbindliches Ergebnis

Erstelle das **Freizeit-Cockpit**, eine mandantenfähige SaaS-Webanwendung zur Planung christlicher Freizeiten.

Am Ende müssen vorhanden sein:

- eine vollständige .NET-10-/React-Anwendung mit PostgreSQL und Aspire;
- alle in diesem Auftrag beschriebenen Funktionen;
- eine modulare, getestete und nachvollziehbar dokumentierte Architektur;
- ein reproduzierbarer lokaler Entwicklungs- und Verifikations-Harness;
- produktionsgeeignete Azure-Infrastrukturdefinitionen mit azd und Bicep;
- eine deutsche, barrierearme, responsive und installierbare PWA;
- eine öffentliche deutsche Anwenderhilfe unter /hilfe/;
- automatisierte Tests, Sicherheitsprüfungen, Smoke Tests und aktuelle Fortschrittsevidenz;
- kleine lokale, grüne Git-Commits;
- keine noch offenen Pflichtarbeiten, Platzhalterseiten oder funktionslosen Attrappen.

Implementiere die Anwendung tatsächlich. Erzeuge nicht nur Pläne, Gerüste, Mockups oder Dokumentation.

## 2. Nicht verhandelbare Arbeitsregeln

### 2.1 Autonomie und Vollständigkeit

- Frage nicht nach Zustimmung zum Beginnen oder Fortfahren.
- Entscheide kleine Unklarheiten nach der einfachsten konsistenten Lösung.
- Halte dich an den vorhandenen Repository-Kontext. Bewahre fremde oder bereits vorhandene Änderungen.
- Untersuche vor Änderungen Repository, Git-Status, vorhandene AGENTS.md-Dateien, Skills, Konventionen und Tooling.
- Wenn das Repository bereits Teile der Lösung enthält, erweitere sie; beginne nicht unnötig neu.
- Hinterlasse keine TODO-, FIXME-, Coming-soon-, Dummy- oder Placeholder-Lösungen für verlangte Funktionen.
- Überspringe keine Anforderung stillschweigend. Wenn eine Anforderung technisch unmöglich ist, dokumentiere konkrete Evidenz und realisiere die beste sichere lokale Alternative.
- Verwende keine Preview-Pakete, wenn eine stabile kompatible Version existiert.
- Pinne wichtige SDK-, Tool-, Runtime- und Paketversionen zentral und committe Lockfiles.
- Führe keine destruktiven Git-Operationen aus. Pushe keine Commits.

### 2.2 Sprach- und Benennungsregeln

- Quellcode, Projekt-, Namespace-, Typ-, API-, Contract-, Tabellen- und Commit-Namen sind Englisch.
- UI-Texte, E-Mails, Fehlermeldungen für Anwender und Anwenderdokumentation sind Deutsch.
- Die ausgelieferte Sprache ist zunächst de-DE, die UI ist technisch für weitere Übersetzungen vorbereitet.
- Der fachliche Codebegriff für eine Freizeit lautet Camp.
- Verwende konsistent OrganizationId und CampId.
- Verwende für unveränderliche öffentliche fachliche Datenklassen keinen Dto-Suffix.

### 2.3 Prioritäten für Entscheidungen

Entscheide in dieser Reihenfolge:

1. fachliche Korrektheit, Mandantentrennung und Sicherheit;
2. Lesbarkeit, Wartbarkeit und überprüfbares Verhalten;
3. KISS und YAGNI;
4. SRP als echter Grund zur Änderung, nicht als Selbstzweck;
5. DRY nur bei stabiler semantischer Wiederholung;
6. tiefe Module mit kleinen, rollenorientierten Schnittstellen;
7. Performance und Komfortoptimierung auf Basis konkreter Anforderungen.

Vermeide spekulative Abstraktionen. Eine einzelne Implementierung rechtfertigt allein noch keine allgemeine Abstraktion. Die ausdrücklich verlangten austauschbaren Infrastruktur-Seams sind eine Ausnahme.

## 3. Zwingende Reihenfolge zu Beginn

Arbeite zu Beginn in dieser Reihenfolge:

1. Inspiziere Repository, Git-Status, installierte SDKs, Tools, Skills und alle anwendbaren AGENTS.md-Dateien.
2. Erzeuge als erste neue Projektdatei .azure/deployment-plan.md. Sie darf zunächst ein belastbares Gerüst sein und wird während der Implementierung konkretisiert. Sie muss das geplante azd-/Bicep-Ziel, Ressourcen, Identitäten, Parameter, Kostenentscheidungen, Sicherheitsgrenzen, lokale Validierung und den ausdrücklich ausgeschlossenen Cloud-Deploy enthalten.
3. Erzeuge eine knappe Root-AGENTS.md mit evergreen Arbeitsregeln, Befehlen, Architekturgrenzen und Definition of Done.
4. Erzeuge PROGRESS.md als wiederaufnehmbare, evidenzbasierte Slice-Liste.
5. Erzeuge CONTEXT-MAP.md und je Fachmodul ein kompaktes CONTEXT.md beziehungsweise Glossar.
6. Erzeuge die Repository-Skills .codex/skills/develop-freizeit-cockpit und .codex/skills/verify-freizeit-cockpit. Nutze vorhandenes Skill-Creator-Tooling, falls verfügbar, und validiere beide Skills.
7. Baue einen grünen Foundation-Skeleton mit Bootstrap-, Build-, Test- und Verify-Pfad.
8. Implementiere danach alle Vertical Slices outside-in testgetrieben.

Falls eine lokal verfügbare Azure-Prepare-Skill anwendbar ist, befolge ihre Regeln zur Erstellung der azd-/Bicep-Artefakte. Die Implementierung der Anwendung bleibt dennoch Teil dieses Auftrags.

## 4. Produktvision und Mandantenmodell

Das Freizeit-Cockpit unterstützt haupt- und ehrenamtliche Organisationsteams vor, während und nach christlichen Freizeiten.

### 4.1 Hierarchie

- Organization ist der Mandant beziehungsweise Veranstalter.
- Eine Organization besitzt mehrere Camps.
- Ein Nutzer kann mehreren Organizations und mehreren Camps angehören.
- Inhalte eines Camps oder einer Organization dürfen niemals mandantenübergreifend sichtbar oder veränderbar sein.
- Neue Organizations entstehen ausschließlich über eine Einladung durch einen Platform Admin.
- Camps besitzen Name, eindeutigen Slug innerhalb der Organization, Beschreibung, Start- und Enddatum, Zeitzone und Status.
- Datumsabhängige Ansichten unterscheiden zukünftig, laufend und vergangen.
- Ein Camp kann aktiv oder archiviert sein.
- Archivierte Camps sind vollständig lesbar und exportierbar, aber schreibgeschützt.
- Berechtigte Nutzer können ein archiviertes Camp reaktivieren.

### 4.2 Rollen und Rechte

Implementiere folgende Rollen mit serverseitig erzwungener Autorisierung:

- **Platform Admin:** verwaltet nur die Plattformebene, lädt erste Organization Owner ein und kann Organizations sperren oder entsperren. Er darf keine fachlichen Mandanteninhalte durchsuchen oder einsehen.
- **Organization Owner:** Es sind mehrere Owner möglich. Owner verwalten Owner und Organization Admins, Organization-Einstellungen, Mitglieder und Löschung. Es muss immer mindestens einen aktiven Owner geben.
- **Organization Admin:** verwaltet Camps, Einladungen, Mitglieder und niedrigere Rollen, darf aber keine Owner- oder Admin-Rechte vergeben.
- **Camp Lead:** verwaltet das zugewiesene Camp, Camp-Einstellungen und Zuweisungen bereits vorhandener Mitglieder.
- **Member:** darf sämtliche Planungsinhalte der zugewiesenen Camps bearbeiten. Eine Verantwortungszuweisung schränkt diese Berechtigung nicht ein.
- **Viewer:** darf zugewiesene Inhalte nur lesen, drucken und exportieren.

Organization Owner und Organization Admins haben Zugriff auf alle Camps ihrer Organization. Camp Lead, Member und Viewer erhalten campbezogene Zuweisungen.

Schütze insbesondere:

- den letzten aktiven Organization Owner vor Entfernung, Herabstufung, Austritt und Kontolöschung;
- Rollenänderungen vor Privilegieneskalation;
- alle Lese-, Schreib-, Such-, Export- und Dateioperationen vor IDOR und Cross-Tenant-Zugriff;
- gesperrte Organizations vor weiterer Nutzung.

### 4.3 URLs und Navigation

- Verwende sprechende Pfade nach dem Muster /o/{organizationSlug}/camps/{campSlug}.
- Die Desktopansicht verwendet eine klare Seitenleiste; Mobilgeräte erhalten eine reduzierte, gut erreichbare Navigation.
- Die Navigation orientiert sich an den Fachmodulen.
- Die Startübersicht zeigt mindestens den nächsten beziehungsweise heutigen Tagesplan, eigene Verantwortungen, offenen Beschaffungsbedarf und jüngste Aktivitäten.

## 5. Authentifizierung, Einladungen und Sitzungen

### 5.1 Passwortloser Login

- Verwende ASP.NET Core Identity als anwendungseigene Identitätsbasis.
- Der Login erfolgt ausschließlich per sechsstelliger SMTP-Einmalcode.
- Speichere Einmalcodes nur kryptografisch gehasht.
- Ein Code ist zehn Minuten gültig, nur einmal nutzbar und nach fünf Fehlversuchen ungültig.
- Begrenze Anforderungen und Versuche pro E-Mail-Adresse und IP-Adresse.
- Antworte bei unbekannten E-Mail-Adressen generisch, um Enumeration zu verhindern.
- Es gibt keine Passwörter und keine Social-Login-Anbieter.
- Eine Standardsitzung gilt zwölf Stunden.
- Eine explizite Option „Angemeldet bleiben“ erlaubt eine widerrufbare Sitzung von 30 Tagen.
- Nutzer sehen ihre Sitzungen und können einzelne oder alle anderen Sitzungen widerrufen.
- Nutze sichere Same-Origin-Cookies mit HttpOnly, Secure und angemessenem SameSite.
- Schütze zustandsändernde Cookie-Anfragen mit Antiforgery.
- Validiere einen serverseitig gespeicherten Session-Identifier, damit Widerruf sofort greift.

### 5.2 Einladungen

- Platform-Admin-Einladungen an den ersten Organization Owner sind 48 Stunden gültig.
- Team-Einladungen sind sieben Tage gültig.
- Tokens sind kryptografisch stark, nur einmal nutzbar, widerrufbar und durch Neuausstellung rotierbar.
- Eine Einladung legt Organization und Zielrolle eindeutig fest.
- Ein bestehender Nutzer kann sie seinem Konto hinzufügen; ein neuer Nutzer wird über den passwortlosen Flow aufgenommen.

### 5.3 Selbstverwaltung und Löschung

Nutzer können:

- Anzeigenamen ändern;
- eine neue E-Mail-Adresse durch Verifikation übernehmen;
- aktive Sitzungen verwalten;
- zulässige Mitgliedschaften verlassen;
- ihr Konto DSGVO-orientiert zur Löschung vormerken.

Organization Owner können eine Mandantenlöschung nur nach frischer Einmalcode-Bestätigung und Eingabe des Organization-Slugs vormerken. Konto- und Mandantenlöschungen haben 30 Tage Karenz und sind in diesem Zeitraum durch Berechtigte widerrufbar. Danach werden fachliche Daten und Blobs dauerhaft gelöscht; verbleibende technische Auditdaten werden minimiert und pseudonymisiert.

Der erste Platform Admin wird ausschließlich über eine idempotente, externe Bootstrap-Konfiguration seiner E-Mail-Adresse angelegt. Es gibt keine öffentliche Bootstrap-Route.

## 6. Funktionsumfang

### 6.1 Tagesplanung

Implementiere einen freien Tages- und Wochenplan ohne fest vordefinierte Slots.

Ein Zeitplaneintrag besitzt mindestens:

- Datum beziehungsweise Beginn und Ende;
- Ganztägig-Kennzeichen;
- Titel und Beschreibung;
- Ort;
- Kategorie;
- Status;
- eine oder mehrere verantwortliche Personen;
- optionale Zielgruppe als freier Text oder Tag.

Regeln:

- Parallele und überlappende Einträge sind erlaubt.
- Überlappungen werden sichtbar, aber nur informativ markiert.
- Jedes Camp hat eine IANA-Zeitzone, standardmäßig Europe/Berlin.
- Zeitpunkte werden als UTC-Instant gespeichert; ganztägige Einträge als lokale Daten.
- Berücksichtige Sommerzeit, mehrdeutige und nicht existierende lokale Zeiten explizit und teste sie.
- Nutze FullCalendar Standard mit MIT-kompatiblen Funktionen für Tages-/Wochenansicht, Drag-and-drop und Größenänderung.
- Biete für jede Kalenderaktion eine vollständige barrierearme Agenda- und Formularalternative.
- Drag-and-drop arbeitet optimistisch, muss bei Serverfehler zurückrollen und bei Versionskonflikten verständlich reagieren.
- Nutze ETag und If-Match für konkurrierende Änderungen.

Mahlzeiten und Andachten können jeweils optional genau einem Zeitplaneintrag zugeordnet sein. Der Zeitplan bleibt die einzige Quelle für Datum, Uhrzeit und Ort. Speichere diese Werte nicht zusätzlich in den verknüpften Objekten. Biete einen atomaren Workflow, der Zeitplaneintrag und Mahlzeit beziehungsweise Andacht gemeinsam anlegt.

Beim Löschen eines verknüpften Zeitplaneintrags muss der Nutzer ausdrücklich zwischen Entkoppeln und gemeinsamem Verschieben in den Papierkorb wählen. Es gibt keine stille Kaskadenlöschung.

### 6.2 Essens- und Rezeptplanung

Implementiere:

- eine veranstalterweite Zutatenbibliothek;
- eine veranstalterweite Rezeptbibliothek;
- Mahlzeiten je Camp;
- Rezept-Snapshots je Mahlzeit;
- Portionsskalierung;
- kompatible Einheitenumrechnung;
- Einkaufsübernahme.

Zutaten haben einen normalisierten, innerhalb einer Organization eindeutigen Namen und Autocomplete. Organization Owner und Admins können Duplikate kontrolliert zusammenführen.

Ein Rezept besitzt mindestens Name, Beschreibung, Zubereitung, Basisportionen, Zutatenpositionen, optionale Ernährungs-Tags und manuelle Allergen- beziehungsweise Küchenhinweise.

Eine Mahlzeit:

- kann einen oder mehrere Rezept-Snapshots enthalten;
- übernimmt den Camp-Standard für Personenzahl, darf ihn aber überschreiben;
- verändert sich nicht still, wenn ein Bibliotheksrezept später bearbeitet wird;
- bietet eine ausdrückliche Aktualisierung auf die neue Rezeptversion;
- kann mit einem Zeitplaneintrag verknüpft werden.

Verwende Dezimalzahlen, niemals binäre Gleitkommazahlen, für fachliche Mengen. Unterstütze mindestens:

- Masse: Gramm und Kilogramm;
- Volumen: Milliliter und Liter;
- Anzahl: Stück beziehungsweise fachlich benannte Zähleinheit.

Konvertiere automatisch nur innerhalb kompatibler Dimensionen. Implementiere weder Dichteumrechnung noch automatische Packungsrundung. Vor Übernahme in eine Einkaufsliste kann der Nutzer Menge und Einheit bearbeiten. Ernährungs- und Allergenhinweise sind Planungsinformationen und dürfen nicht als medizinische Garantie dargestellt werden.

### 6.3 Materialplanung

Materialbedarf kann campweit oder mit einem Zeitplaneintrag verknüpft sein. Ein Eintrag besitzt mindestens:

- Bezeichnung und optionale Beschreibung;
- Menge und Einheit;
- verantwortliche Personen;
- Beschaffungsquelle beziehungsweise Notiz;
- Beschaffungsstatus.

Implementiere kein Inventar, keinen Lagerbestand und keine Ausleihverwaltung.

### 6.4 Gemeinsame Einkaufslisten

- Ein Camp kann mehrere benannte gemeinsame Einkaufslisten besitzen.
- Einträge entstehen aus Rezepten, aus Materialbedarf oder spontan.
- Jeder übernommene Eintrag behält eine nachvollziehbare Quellenreferenz.
- Die einheitliche Position enthält Bezeichnung, Menge, Einheit, optional Verantwortliche, Geschäft und Notiz.
- Positionen lassen sich mobil schnell abhaken und wieder öffnen.
- Speichere, wer eine Position wann abgehakt hat.
- Verwende kein getrenntes Datenmodell für Lebensmittel- und Materialeinkauf.
- Aktualisiere gemeinsame Ansichten durch einfaches Polling etwa alle 15 Sekunden und beim erneuten Fokussieren.
- Implementiere kein SignalR und keine Echtzeit-Kollaboration.

### 6.5 Geistliche Planung und Andachten

Ein Andachtsentwurf besitzt mindestens:

- Thema;
- Bibelstelle;
- Ziel beziehungsweise Kerngedanke;
- Markdown-Inhalt oder Gliederung;
- verantwortliche Personen;
- Materialhinweise;
- optionale Verknüpfung zum Zeitplan.

Integriere über eine echte Provider-Schnittstelle eine kostenfreie Bibel-API und stelle lokal einen deterministischen HTTP-Stub bereit. Unterstütze genau diese vier deutschen Übersetzungen:

- Schlachter 1951 als Standard;
- Luther 1912;
- unrevidierte Elberfelder;
- Textbibel.

Ein gespeicherter Bibeltext ist ein Snapshot mit:

- Referenz;
- Textauszug;
- technischer Übersetzungs-ID;
- Anzeigename der Übersetzung;
- Lizenz und Attribution;
- Abrufzeitpunkt.

Bereits gespeicherte Snapshots werden niemals still aktualisiert. Biete eine ausdrückliche Aktualisierung. Bei API-Ausfall bleiben vorhandene Snapshots nutzbar, und Nutzer können Referenz sowie eigenen Inhalt weiterhin manuell bearbeiten.

Respektiere die jeweiligen Lizenzen und zeige erforderliche Attribution in der UI und in THIRD_PARTY_NOTICES.md. Prüfe während der Implementierung die aktuellen Provider-Metadaten, ohne Standardtests vom Internet abhängig zu machen.

Ausgangspunkte:

- API-Dokumentation: https://bible.helloao.org/docs/guide/getting-started.html
- Luther-1912-Metadaten: https://ebible.org/Scriptures/details.php?id=deu1912
- bekannte Übersetzungskennungen: deu1951, deu1912, deuelo und deutkw

Implementiere keine KI-Generierung, keine modernen kostenpflichtigen Übersetzungen und keine vollständige Gottesdienst-, Lied- oder Liturgieplanung.

### 6.6 Gemeinsames Notizbuch

Jedes Camp besitzt ein gemeinsames Notizbuch für das gesamte zugewiesene Team.

Eine Notiz bietet:

- Titel;
- Markdown-Inhalt;
- einfache Werkzeugleiste für Überschriften, Fett, Kursiv, Listen und Links;
- Tags;
- Anheften;
- optionale Verknüpfungen zu zentralen Planungsobjekten.

Erlaube kein unsicheres Roh-HTML. Sanitize die Darstellung. Tabellen, eingebettete Editor-Bilder, private Notizen, Wiki-Funktionen und Kommentare sind nicht Teil der v1.

### 6.7 Anhänge

Anhänge sind erlaubt an:

- Zeitplaneinträgen;
- Mahlzeiten und Rezepten;
- Materialeinträgen;
- Andachten;
- Notizen.

Anhänge sind nicht an Einkaufspositionen erforderlich.

Regeln:

- Erlaubte Formate: PDF, JPEG, PNG und WebP.
- Maximal zehn MiB pro Datei.
- Maximal 100 MiB je Camp beziehungsweise je veranstalterweiter Rezeptbibliothek.
- Dateiendung, deklariertes MIME und erkannte Magic Bytes müssen konsistent sein.
- Verwende zufällige interne Blobnamen und speichere keine vertrauenswürdige Autorisierung im Dateinamen.
- Produktionsblobs sind privat.
- Auslieferung erfolgt nur nach aktueller fachlicher Autorisierung über kurzlebigen Zugriff.
- Bilder dürfen angezeigt werden; PDFs werden nur heruntergeladen.
- SVG, Office-Dateien, Archive und sonstige Formate sind verboten.
- Malware-Prüfung ist nicht Teil der v1; dokumentiere diese bewusste Grenze klar.

### 6.8 Suche, Aktivität, Papierkorb, Druck und Export

- Implementiere eine einfache campweite Suche über relevante Titel- und Textfelder aller Fachmodule.
- Biete Filter nach Typ und relevanten Metadaten.
- Verwende keinen externen Volltext-Suchdienst, kein Fuzzy Search und keine gespeicherten Suchen.
- Implementiere einen Aktivitätsfeed für Erstellen, Ändern, Löschen und Wiederherstellen mit Akteur, Zeitpunkt, Objekttyp und Titel.
- Speichere im Aktivitätsfeed keine vollständigen Inhaltsdifferenzen und keine sensiblen Inhalte.
- Fachliche Löschungen sind zunächst Soft Deletes.
- Ein Papierkorb erlaubt berechtigten Leitungs- und Adminrollen die Wiederherstellung.
- Nach 30 Tagen entfernt ein Bereinigungsprozess die Daten endgültig.
- Erzeuge zweckmäßige Druckansichten und CSV-Exporte für Zeitplan, Mahlzeiten, Material und Einkauf.
- Schütze CSV-Downloads vor Tabellenkalkulations-Formelinjektion.
- Erzeuge keine serverseitigen PDF-, Word- oder Excel-Dateien und keinen Import.

### 6.9 Verantwortlichkeiten und externe Links

- Zentrale Planungsobjekte können einer oder mehreren verantwortlichen Personen zugewiesen werden.
- Verantwortlichkeit dient Darstellung und Filterung, nicht als zusätzliche Autorisierungsgrenze.
- Erzeuge kein separates Aufgabenmodul.
- Sichere externe HTTPS-Links dürfen in passenden Textfeldern verwendet werden.
- E-Mails werden ausschließlich für Login-Codes und Einladungen versendet. Keine Änderungs-, Zuweisungs-, Push- oder Digest-Benachrichtigungen.

## 7. PWA und Offline-Verhalten

Die Webanwendung ist installierbar und besitzt Manifest, Icons, Service Worker, Update-UX und sinnvolle App-Metadaten.

Schreiben ist ausschließlich online möglich.

Offline wird nur ein klar gekennzeichneter, zuletzt synchronisierter Read-only-Snapshot des zuletzt verwendeten Camps angeboten für:

- Tagesplan;
- Speiseplan;
- Material;
- Einkaufslisten.

Identitäten, Administration, Notizen, Andachten, Dateien und sonstige sensible Inhalte stehen offline nicht zur Verfügung. Zeige den Zeitpunkt des Snapshots und den Offlinezustand deutlich an. Lösche lokale Snapshots beim Logout und beim Wechsel der Organization. Implementiere keine Offline-Schreibwarteschlange, keine Konfliktsynchronisation und keine Hintergrundänderungen.

## 8. UX, Design und Barrierefreiheit

Die Anwendung verwendet ausschließlich ein helles, ruhiges, freundliches Design in Petrol und Himmelblau. Sie soll für christliche Freizeitplanung angemessen, modern und warm wirken, aber nicht verspielt oder dekorativ überladen sein.

### 8.1 Frontend-Stack

Verwende stabile, kompatible Versionen von:

- Vite;
- React;
- TypeScript im Strict-Modus;
- React Router;
- TanStack Query;
- React Hook Form;
- Zod;
- Tailwind CSS;
- shadcn/ui beziehungsweise Radix-Primitives;
- Lucide Icons;
- FullCalendar Standard;
- react-i18next oder eine gleichwertige schlanke i18n-Lösung.

Verwende kein zweites konkurrierendes UI-System.

### 8.2 UX-Anforderungen

- Desktop, Tablet und Mobilgerät sind gleichwertig nutzbar.
- Unterstütze ab 320 CSS-Pixel Breite und bis 400 Prozent Zoom.
- Unterstütze die jeweils letzten zwei stabilen Versionen der großen Browser.
- Alle Funktionen sind per Tastatur erreichbar.
- Fokus ist sichtbar und logisch geführt.
- Formulare haben echte Labels, verständliche Validierung und zusammengefasste Fehler.
- Status wird nicht nur durch Farbe vermittelt.
- Modale Dialoge, Menüs, Kalender und Toasts besitzen korrekte Semantik.
- Respektiere prefers-reduced-motion.
- Verwende hinreichende Zielgrößen und Farbkontraste.
- Kalender-Drag-and-drop ist nie der einzige Bedienweg.
- Leere Zustände, Ladezustände, Fehlerzustände, Berechtigungsfehler und Konflikte sind vollständig gestaltet.

Ziel ist WCAG 2.2 AA. Verifiziere zentrale Journeys automatisiert mit axe und ergänze notwendige manuelle Tastatur- und Screenreader-nahe Checks.

## 9. Lösungsarchitektur

### 9.1 Technologie und Solution

Verwende:

- .NET 10 LTS;
- ASP.NET Core Minimal APIs;
- Entity Framework Core mit Npgsql;
- PostgreSQL 17;
- .NET Aspire;
- martinothamar/Mediator Version 3.0.2;
- React und TypeScript;
- pnpm.

Referenzen:

- .NET Support: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- Aspire Support: https://aspire.dev/support/
- Mediator: https://github.com/martinothamar/Mediator

Prüfe bei der Umsetzung stabile kompatible Versionen, pinne sie und dokumentiere die gewählte Toolchain. Keine Preview-Abhängigkeiten.

Die Lösung ist ein **modularer Monolith** mit einem auslieferbaren Web-Host. Dieser Host stellt API, gebaute SPA und Anwenderhilfe unter derselben Origin bereit.

### 9.2 Acht verbindliche Module

Erzeuge genau diese fachlich getrennten Module:

1. Identity & Tenancy
2. Camps & Schedule
3. Catering
4. Logistics
5. Spiritual
6. Knowledge
7. Files
8. Activity

Nutze kurze, konsistente englische Projektnamen.

Jedes Modul enthält mindestens:

- ein Projekt <Module>.Contracts;
- ein Projekt <Module>.Implementation;
- gezielte Modul- und Integrationstests.

<Module>.Contracts enthält:

- wenige rollenorientierte, kohäsive Schnittstellen;
- unveränderliche fachliche Eingabe-, Ergebnis- und View-Datentypen;
- nach Möglichkeit nur BCL-Abhängigkeiten;
- keine EF-Core-, ASP.NET-Core-, Npgsql- oder Implementationstypen;
- keine generischen Mega-Fassaden;
- keine mechanische Ein-Interface-pro-Methode-Struktur;
- keine Dto-Suffixe.

Fakes liegen ausschließlich in Test-Support-Projekten, nicht in Produktionsprojekten.

### 9.3 Modulgrenzen

- Jedes Modul besitzt einen eigenen EF-Core-DbContext.
- Jedes Modul besitzt ein eigenes PostgreSQL-Schema und eigene Migrationen.
- Ein Modul darf Tabellen, DbContext oder interne Typen eines anderen Moduls weder direkt lesen noch schreiben.
- Zwischen Modulen werden nur synchrone Contract-Interfaces verwendet.
- Über Modulgrenzen werden stabile IDs und fachliche Contract-Daten ausgetauscht, keine direkten Datenbank-Fremdschlüssel.
- Architekturtests verhindern unerlaubte Referenzen und Schema-Zugriffe.
- Files und Activity bleiben technische Module mit fachlich autorisierten Aufrufen; sie dürfen keine Hintertür in fremde Inhalte bilden.

### 9.4 Vertical Slices und Mediator

- Organisiere API und Implementierung innerhalb der Module nach fachlichen Vertical Slices.
- Verwende Commands, Queries und Handler mit martinothamar/Mediator.
- Registriere den Source Generator sinnvoll im äußeren Host; Implementation-Projekte hängen möglichst nur an Mediator.Abstractions.
- Verwende einen Scoped-Lifetime, weil Handler auf DbContexts zugreifen.
- Nutze gezielte Pipeline-Verhalten für Validierung, Logging, Autorisierung und Transaktionskoordination.
- Mediator-Typen sind kein unnötiger Teil öffentlicher Modul-Contracts.

### 9.5 Transaktionen

Cross-Module-Commands, die atomar sein müssen, verwenden eine gemeinsame lokale Npgsql-Transaktion über die beteiligten DbContexts.

- Kapsle die technische Transaktionskoordination außerhalb der Fachcontracts.
- Beginne, committe und rolle genau einmal zurück.
- Teste Fehler nach dem ersten beteiligten Modul und beweise den vollständigen Rollback.
- Verwende keinen Message Broker, keine Saga und keinen Outbox-Mechanismus für die v1.

### 9.6 Datenbank, Tenant Context und RLS

Alle tenantbezogenen Tabellen tragen organization_id. Campbezogene Tabellen tragen zusätzlich camp_id.

Implementiere PostgreSQL Row-Level Security als zweite Verteidigungslinie:

- Die Laufzeitrolle darf RLS nicht umgehen und besitzt kein BYPASSRLS.
- Der Tenant Context wird transaktionsgebunden sicher gesetzt und zurückgesetzt.
- Die Migrationsrolle ist getrennt und minimal privilegiert.
- Fachliche Autorisierung wird zusätzlich in der Anwendung erzwungen.
- Vertraue niemals allein einem Organization- oder Camp-Identifier aus URL oder Request.
- Prüfe Slug, Membership, Rolle und Objektzugehörigkeit gemeinsam.

Schreibe adversariale Integrationstests für fremde OrganizationIds und CampIds bei Lesen, Erstellen, Ändern, Löschen, Suche, Export, Activity und Dateien.

### 9.7 Nebenläufigkeit

- Alle veränderbaren Aggregate besitzen eine explizite numerische Version.
- Lese-APIs liefern ein ETag.
- Mutationen verlangen If-Match.
- Fehlende Vorbedingungen und Versionskonflikte liefern standardisierte Problem Details.
- Die UI bewahrt ungespeicherte Eingaben, erklärt den Konflikt und ermöglicht Neu laden sowie manuelles erneutes Anwenden.
- Es gibt kein blindes automatisches Merge.

### 9.8 API

- Versioniere unter /api/v1.
- Verwende Minimal-API-Route-Gruppen je Modul.
- Erzeuge OpenAPI.
- Nutze RFC-9457-Problem-Details mit stabilen maschinenlesbaren Fehlercodes und deutscher nutzergeeigneter Beschreibung.
- Generiere aus OpenAPI einen typisierten TypeScript-Client.
- Prüfe im Verify-Gate, dass der Client nicht vom aktuellen Schema abweicht.
- Exponiere keine internen Entity- oder Contract-Typen unreflektiert als öffentliche API.
- Implementiere Health- und Readiness-Endpunkte.

### 9.9 Reale Infrastruktur-Seams

Erzeuge kleine, echte und testbare Schnittstellen für:

- SMTP-Versand;
- privaten Blob Storage;
- Bibel-Provider;
- Uhrzeit;
- gegebenenfalls Schlüsselverwaltung beziehungsweise Data Protection.

Produktion, lokale Entwicklung und Tests erhalten jeweils passende Adapter:

- Produktion: Azure- beziehungsweise SMTP-Implementierung;
- lokale Aspire-Umgebung: Mailpit, Azurite und Bible-API-Stub;
- schnelle Tests: deterministische Fakes.

Abstrahiere nicht spekulativ jede technische Klasse.

### 9.10 Sicherheit

Implementiere mindestens:

- sichere Cookie-Authentifizierung und Antiforgery;
- konsequente serverseitige Autorisierung;
- RLS und IDOR-Schutz;
- Rate Limits für Login und Einladungsendpunkte;
- Content Security Policy und weitere zweckmäßige Security Header;
- sichere Markdown-Sanitization;
- strikte Dateiüberprüfung und Downloadautorisierung;
- Schutz vor CSV-Formelinjektion;
- sichere Fehler ohne interne Details;
- Secret-Konfiguration nur außerhalb des Codes;
- redigiertes strukturiertes Logging.

Logge niemals Einmalcodes, Session-Tokens, Einladungs-Tokens, vollständige E-Mail-Inhalte, Blob-SAS-Werte oder fachliche Langtexte. Verwende keine echten personenbezogenen Daten in Seeds oder Screenshots.

Speichere ASP.NET-Core-Data-Protection-Keys produktiv gemeinsam in privatem Blob Storage und schütze sie mit Key Vault, damit mehrere Replikate und Kaltstarts funktionieren.

## 10. Migrationen und Hintergrundarbeit

Erzeuge einen separaten Migrator:

- eigener ausführbarer Prozess beziehungsweise Container;
- wendet Modulmigrationen in fester, dokumentierter Reihenfolge an;
- verwendet eine PostgreSQL-Advisory-Lock;
- ist idempotent und bei Fehlern nicht destruktiv;
- läuft lokal vor dem Web-Host;
- läuft produktiv als Container Apps Job.

Der Web-Host führt beim Start niemals automatisch Datenbankmigrationen aus.

Erzeuge außerdem einen kontrollierten Bereinigungsjob für:

- endgültige Löschung nach 30 Tagen;
- Blob-Bereinigung;
- abgelaufene Login- und Einladungstokens;
- veraltete Sitzungen;
- weitere fachlich notwendige temporäre Daten.

Uhrzeit ist in diesen Prozessen injizierbar und deterministisch testbar.

## 11. Lokale Aspire-Umgebung

Aspire startet und verdrahtet mindestens:

- PostgreSQL 17;
- Azurite;
- Mailpit;
- einen lokalen deterministischen Bible-API-Stub;
- Migrator;
- Web-Host;
- Aspire Dashboard und OpenTelemetry.

Verwende kein Redis.

Die lokale Entwicklung benötigt für Standardabläufe keine Cloudkonten und keine externen Dienste. Standardtests benötigen keinen Internetzugang.

Erzeuge ausschließlich in Development und Testing einen deterministischen, realistischen Seed mit:

- Platform Admin;
- Organization;
- Camp über sieben Tage;
- Nutzern für alle Rollen;
- beispielhaften Zeitplaneinträgen;
- Mahlzeiten und Rezepten;
- Materialien und Einkaufslisten;
- Andacht mit lizenziertem Snapshot;
- Notizen, Aktivität und erlaubten Dateimetadaten.

Produktion startet ohne Beispieldaten; ausgenommen ist der explizite Platform-Admin-Bootstrap.

## 12. Azure-Zielarchitektur und Infrastructure as Code

### 12.1 Ressourcen

Bereite mit azd und Bicep mindestens vor:

- Azure Container Apps für den Web-Host;
- Container Apps Jobs für Migration und Bereinigung;
- Azure Container Registry Basic;
- Azure Database for PostgreSQL Flexible Server;
- Storage Account mit privatem Blob Container;
- Key Vault;
- Application Insights und Log Analytics;
- User-Assigned Managed Identities;
- GitHub-Actions-OIDC-Konfiguration, soweit deklarativ möglich;
- einfache Alerts und Action-Group-Parameter.

Die Standardregion ist Germany West Central, aber parameterisierbar.

### 12.2 Kostenprofil

Optimiere für geringe Kosten einer kleinen ehrenamtlichen Anwendung:

- Container Apps Consumption mit Scale-to-zero;
- minimale Replikate null, maximale Replikate standardmäßig drei;
- kleine angemessene CPU-/RAM-Werte;
- PostgreSQL Burstable B1ms, ungefähr 32 GiB, ohne Hochverfügbarkeit;
- sieben Tage Backup/PITR;
- StorageV2 Standard LRS, Hot;
- Key Vault Standard;
- kurze sinnvolle Log-Retention und Kostenlimit;
- Kaltstarts sind akzeptiert.

### 12.3 Identität und Netzgrenzen

- Produktion verwendet Managed Identity und Entra/RBAC.
- Web und Jobs erhalten getrennte User-Assigned Managed Identities mit minimalen Rechten.
- PostgreSQL verwendet Entra-basierte Authentifizierung; keine Produktionspasswörter im App-Config.
- Storage und Key Vault verwenden RBAC und Managed Identity.
- Datenendpunkte bleiben aus Kostengründen öffentlich adressierbar, aber TLS-, Entra- und RBAC-geschützt.
- Es gibt keine Private Endpoints und keine VNet-Isolation in v1.
- Dokumentiere insbesondere das Risiko einer nötigen öffentlichen PostgreSQL-Firewallregel bei dynamischem Container-Apps-Egress und die spätere Härtungsoption.

### 12.4 Betrieb

- Container-App-Ingress ist öffentlich und HTTPS.
- Unterstütze den Standard-Container-Apps-Hostnamen.
- Parameterisiere optional eine eigene Domain und einen Managed Certificate Flow nach externer DNS-Einrichtung.
- Verwende eine externe PublicBaseUrl für Links in E-Mails.
- Parameterisiere ImprintUrl und PrivacyUrl; erfinde keine Rechtstexte.
- Erzeuge Basisalarme für Health, erhöhte 5xx-Rate, Latenz und Datenbankprobleme.
- Dokumentiere Restore, Migration, Rollback, Löschung, Owner-Recovery, Secret-Rotation und Incident-Grundabläufe.

### 12.5 Striktes Deployment-Verbot in dieser Sitzung

Führe in diesem Single Shot **kein** Azure-Deployment und keine Cloudmutation aus.

Verboten sind insbesondere:

- az login;
- azd auth login;
- azd up;
- azd provision;
- azd deploy;
- Bicep- oder ARM-Deployments;
- Rollenvergaben;
- Ressourcenanlage oder -änderung;
- DNS-Änderungen;
- GitHub-Secret- oder Environment-Mutationen.

Erlaubt und verlangt sind ausschließlich lokale, nicht mutierende Prüfungen wie Bicep-Build, azd-Konfigurationsprüfung, statische Analyse und Tests. Falls ein Validierungsschritt Cloudzugriff oder Login verlangt, dokumentiere den manuellen Befehl im Runbook und führe ihn nicht aus.

## 13. CI/CD

Erzeuge GitHub-Actions-Workflows:

- Pull Requests und main führen denselben vollständigen Verify-Gate aus.
- Nach erfolgreichem Merge auf main darf der Deployment-Workflow per OIDC bauen, Images veröffentlichen, den Migrationsjob ausführen und anschließend die Anwendung deployen.
- Verwende keine langfristigen Azure-Credentials oder Client Secrets.
- Trenne Infrastruktur-Bootstrap von wiederholbaren Anwendungsdeployments.
- Erlaube den produktiven Workflow erst nach dokumentiertem manuellem Erstaufbau.
- Nutze Concurrency-Gruppen, minimale Permissions und nachvollziehbare Artefakte.
- Ein fehlgeschlagener Migrationsjob verhindert die neue Apprevision.

Der Workflow wird erstellt und lokal soweit möglich geprüft, aber in dieser Sitzung weder gepusht noch ausgeführt.

## 14. Agent-Harness

### 14.1 Repository-Skills

Erzeuge zwei ausführbare, knappe und repositoryspezifische Skills:

**develop-freizeit-cockpit**

- liest AGENTS.md, PROGRESS.md, CONTEXT-MAP.md und das passende Modulglossar;
- wählt den kleinsten unvollständigen Vertical Slice;
- erzwingt Red-Green-Refactor;
- führt zielgerichtete Tests aus;
- aktualisiert Dokumentation und Fortschritt;
- erzeugt nur nach grüner Verifikation einen lokalen Commit.

**verify-freizeit-cockpit**

- prüft Toolchain und generierte Artefakte;
- führt Format, Lint, Build, Unit-, Integrations-, Architektur-, Browser-, Accessibility-, Coverage-, Smoke- und Dokumentationsprüfungen aus;
- startet bei jeder vollständigen Verifikation zwingend die echte Anwendung und prüft die gerenderte UI visuell in allen festgelegten Viewports;
- sammelt verständliche Evidenz;
- unterscheidet echte Fehler von fehlenden externen Cloudvoraussetzungen;
- verändert bei reiner Verifikation keine Produktlogik.

Wenn eine Skill-Creator-Skill verfügbar ist, verwende und validiere sie. Halte die Skill-Inhalte repositoryspezifisch; dupliziere nicht die gesamte Spezifikation.

### 14.2 AGENTS.md und Kontextdokumente

Die Root-AGENTS.md dokumentiert dauerhaft:

- Architekturgrenzen;
- Sprachregeln;
- TDD-Loop;
- Standardbefehle;
- Sicherheitsregeln;
- Commitregeln;
- Definition of Done.

Erzeuge verschachtelte AGENTS.md nur für Backend, Frontend oder Infrastruktur, wenn dort wirklich abweichende Regeln gelten. Vermeide redundante Anweisungen.

CONTEXT-MAP.md erklärt Beziehungen und erlaubte Abhängigkeiten der acht Module. Jedes Modulglossar dokumentiert:

- Fachbegriffe;
- Invarianten;
- Rollen und Berechtigungen;
- öffentliche Contracts;
- Besitz von Daten und Schema;
- erlaubte ausgehende und eingehende Abhängigkeiten.

### 14.3 Fortschrittstracking

PROGRESS.md ist die zuverlässige Wiederaufnahmequelle nach Kontextkomprimierung oder Agentwechsel.

Führe pro Slice:

- stabilen Namen;
- Status pending, in_progress oder verified;
- Akzeptanzkriterien;
- zuerst rot gewordene Tests;
- grüne Test- und Verify-Befehle;
- betroffene Dokumentation;
- Commit-Hash nach erfolgreichem Commit;
- Blocker mit konkreter Evidenz;
- nächsten kleinsten Schritt.

Aktualisiere PROGRESS.md nach jedem grünen Slice und vor jedem Kontextwechsel. Markiere nichts als verified, solange Tests oder Dokumentation fehlen.

### 14.4 PowerShell-Harness

Erzeuge portable PowerShell-7-Skripte, mindestens:

- scripts/bootstrap.ps1
- scripts/dev.ps1
- scripts/test.ps1
- scripts/verify.ps1
- scripts/smoke.ps1

Anforderungen:

- fail-fast und aussagekräftige Exitcodes;
- nicht-interaktiv für CI;
- gleiches Verhalten lokal und in GitHub Actions;
- prüft notwendige Toolversionen verständlich;
- pnpm über Corepack beziehungsweise klar gepinnte Version;
- keine globalen, undokumentierten Toolabhängigkeiten;
- keine Bash-only-Skripte als einziger Pfad;
- sichere Prozessbereinigung;
- Smoke kann einen lokal gestarteten Aspire-Stack verifizieren.

Dokumentiere schnelle zielgerichtete Befehle und den vollständigen Gate-Befehl.

## 15. Testgetriebener Agent-Loop

Implementiere jeden Vertical Slice outside-in:

1. Wähle den kleinsten fachlich wertvollen Slice.
2. Formuliere präzise Akzeptanzkriterien in PROGRESS.md.
3. Schreibe zuerst einen roten Schnittstellentest: Browser-/UI-Test, HTTP-Akzeptanztest oder Modul-Contract-Test.
4. Führe ihn aus und bestätige, dass er aus dem erwarteten fachlichen Grund fehlschlägt.
5. Ergänze bei Bedarf kleinere Domain- oder Komponententests.
6. Implementiere nur das Minimum, das den Slice fachlich vollständig macht.
7. Führe die zielgerichteten Tests bis grün aus.
8. Führe alle betroffenen Integrations- und Architekturtests aus.
9. Refaktoriere bei weiterhin grünen Tests.
10. Aktualisiere OpenAPI-Client, Anwenderhilfe, Architektur- und Kontextdokumente.
11. Erfasse Befehle und Evidenz in PROGRESS.md.
12. Erzeuge einen kleinen lokalen Commit in englischem Conventional-Commit-Stil.
13. Fahre unmittelbar mit dem nächsten Slice fort.

Ein „Slice“ umfasst UI, API, Autorisierung, Persistenz, Validierung, Fehlerfälle, Tests und Dokumentation. Baue nicht erst alle Backends und später alle Oberflächen.

## 16. Kontrollierte Parallelisierung

Nach einem vollständig grünen Foundation-Skeleton und stabilen ersten Contracts darfst du maximal drei Subagenten gleichzeitig einsetzen.

Regeln:

- Jeder Subagent arbeitet in einem eigenen Git-Worktree und eigenen Branch.
- Der Hauptagent besitzt gemeinsame Dateien, Contracts, PROGRESS.md, Root-Dokumentation, CI und Integration.
- Vor einer Parallelisierungswelle werden gemeinsame Contracts und Konventionen eingefroren.
- Jeder Subagent erhält genau ein klar abgegrenztes Modul oder eine unabhängige Aufgabe.
- Subagenten ändern keine gemeinsamen Contracts ohne vorherige Rückgabe an den Hauptagenten.
- Jeder Subagent arbeitet TDD, aktualisiert seine Modulkontexte, führt zielgerichtete Tests aus und erzeugt ausschließlich grüne Commits.
- Der Hauptagent prüft Diff und Tests, cherry-pickt bewusst, löst Konflikte, führt Architektur- und Gesamtprüfungen aus und aktualisiert PROGRESS.md.
- Entferne Worktrees erst nach bestätigter Integration und nur mit sicheren, expliziten Pfaden.
- Pushe keine Branches.

Eine sinnvolle Reihenfolge ist:

1. Hauptagent: Foundation, Identität, Mandantentrennung, Autorisierung und gemeinsame Sicherheitsmechanismen.
2. Welle: Camps & Schedule, Catering, Spiritual.
3. Welle: Logistics, Knowledge, Files.
4. Hauptagent beziehungsweise letzte Welle: Activity, Suche, Exporte, Offline-PWA, Hilfe, Azure und End-to-End-Härtung.

Passe die Aufteilung an tatsächliche Abhängigkeiten an. Parallelisierung ist ein Beschleuniger, kein Grund für instabile Contracts oder unvollständige Integration.

## 17. Verifikation und Quality Gates

### 17.1 Backend

Verwende:

- xUnit v3;
- eingebaute Assertions oder eine zulässig lizenzierte schlanke Alternative;
- Domain- und Handler-Tests;
- API- und Modul-Integrationstests;
- Aspire.Hosting.Testing für echte lokale Ressourcen;
- reales PostgreSQL und Azurite in relevanten Integrationspfaden;
- Mailpit beziehungsweise SMTP-Testpfade;
- ArchUnitNET für Modulgrenzen.

Verwende keine kommerziell problematische Assertion-Lizenz.

### 17.2 Frontend

Verwende:

- Vitest;
- Testing Library;
- MSW;
- TypeScript Strict;
- typgeprüftes ESLint;
- Prettier;
- Playwright.

Vermeide any. Falls eine schmale Ausnahme technisch unvermeidbar ist, dokumentiere und kapsle sie unmittelbar.

### 17.3 Browser und Accessibility

Playwright prüft zentrale Journeys in Chromium, Firefox und WebKit. Verifiziere mindestens drei Viewports:

- Mobilgerät;
- Tablet;
- Desktop.

Erzeuge aktuelle Screenshots als Testartefakte und für die Anwenderhilfe. Verwende keine fragilen Pixel-Golden-Tests als einziges Qualitätsgate. Integriere axe für zentrale Seiten und Flows.

### 17.3.1 Verpflichtende visuelle UI-Prüfung

Jede vollständige Verifikation muss zwingend die tatsächlich gerenderte Oberfläche prüfen. Erfolgreiche DOM-, Unit-, Playwright- und axe-Tests allein reichen nicht aus.

- Starte den realen lokalen Aspire-Stack und öffne die Anwendung mit deterministischen Seed-Daten.
- Navigiere durch alle zentralen Seiten und repräsentative Dialoge, Formulare, Tabellen, Kalender, Menüs sowie Leer-, Lade-, Fehler-, Offline- und Konfliktzustände.
- Erzeuge Screenshots mindestens in den definierten Mobil-, Tablet- und Desktop-Viewports.
- Inspiziere diese Screenshots tatsächlich mit einer verfügbaren Browser-, Screenshot- oder Bildansicht. Das bloße Erzeugen der Dateien gilt nicht als Prüfung.
- Prüfe insbesondere auf fehlende, doppelte oder funktionslose Controls, Browser-Standardartefakte, kaputte Icons, abgeschnittene Texte, unerwartete Scrollbereiche, Überlagerungen, fehlerhafte Z-Index-Reihenfolgen, Layoutsprünge, unlesbare Kontraste, inkonsistente Abstände, falsche Responsive-Umbrüche und Inhalte außerhalb des Viewports.
- Prüfe fokussierte, deaktivierte, aktive, ausgewählte, validierte und fehlerhafte Zustände der wichtigsten Controls.
- Behandle jeden sichtbaren Defekt als fehlgeschlagene Verifikation, korrigiere ihn und wiederhole die betroffenen Browser-, Accessibility- und visuellen Prüfungen.
- Dokumentiere in PROGRESS.md, welche Seiten, Zustände und Viewports visuell geprüft wurden, und verlinke beziehungsweise benenne die zugehörigen Screenshot-Artefakte.

Automatisierte Screenshot-Vergleiche dürfen unterstützen, ersetzen aber niemals die tatsächliche visuelle Inspektion. Wenn die Anwendung nicht gestartet oder die Oberfläche nicht inspiziert werden kann, darf das vollständige Verify-Gate nicht als erfolgreich gelten.

### 17.4 Coverage

Erzwinge, getrennt sinnvoll ausgewertet für Backend und Frontend:

- mindestens 80 Prozent Line Coverage;
- mindestens 75 Prozent Branch Coverage.

Ausgenommen werden dürfen nur nachvollziehbar:

- generierter Code;
- EF-Migrationen;
- triviale Bootstrap-Verdrahtung.

Kritische Sicherheits- und Domänenregeln müssen vollständig szenariobasiert getestet sein, unabhängig von Prozentwerten.

### 17.5 Statische Qualität

- .NET Nullable ist aktiviert.
- Compilerwarnungen und relevante Analyzerwarnungen sind Fehler.
- Verwende Central Package Management.
- dotnet format beziehungsweise äquivalente Formatprüfung muss sauber sein.
- TypeScript ist strict.
- ESLint verwendet typbewusste Regeln.
- Prettier läuft im Check-Modus.
- Generierte Dateien werden nicht manuell editiert.
- OpenAPI-Client-Drift lässt das Gate fehlschlagen.
- Lockfiles und Toolmanifeste sind aktuell.

### 17.6 Zeitziele

- Ein typischer schneller TDD-Lauf soll unter zwei Minuten bleiben.
- Das vollständige lokale und CI-Verify-Gate soll unter 15 Minuten bleiben.
- Schichte Tests so, dass Entwickler schnell gezielt prüfen können, ohne Sicherheits- und Integrationsabdeckung im Gesamtgate zu opfern.

Wenn die lokale Hardware ein Ziel nachvollziehbar überschreitet, optimiere den Harness und dokumentiere gemessene Zeiten sowie Ursache. Deaktiviere keine Pflichtprüfungen, nur um das Ziel künstlich zu erreichen.

## 18. Verbindliche Testszenarien

Implementiere mindestens folgende automatisierte Szenarien.

### 18.1 Identität und Rollen

- Login-Code Happy Path, Ablauf, Wiederverwendung, fünf Fehlversuche und Rate Limit.
- Generische Antwort bei unbekannter E-Mail.
- Standard- und Langzeitsitzung sowie Widerruf.
- Einladung neu/bestehend, Ablauf, Widerruf und Rotation.
- Jede Rollenmatrix mit erlaubten und verbotenen Aktionen.
- Schutz des letzten Owners.
- Platform Admin kann keine fachlichen Inhalte lesen.
- Gesperrte Organization ist blockiert.
- frische Reauth und Slug-Bestätigung bei Tenant-Löschung.

### 18.2 Mandantentrennung

- RLS für jeden DbContext.
- Cross-Tenant-ID bei Read, List, Update, Delete, Search, Export, Activity und Files.
- erratene Slugs und IDs.
- manipulierte Organization-/Camp-Parameter.
- Laufzeitrolle kann RLS nicht umgehen.
- Tenant Context leakt nicht zwischen gepoolten Verbindungen oder Requests.

### 18.3 Tagesplan

- parallele Einträge und Überlappungsanzeige;
- Drag-and-drop, Resize und Tastatur-/Formularalternative;
- optimistischer Rollback;
- Zeitzonenwechsel, DST-Lücke und doppelte lokale Uhrzeit;
- Ganztagseinträge;
- ETag- und If-Match-Konflikte;
- verknüpfte Mahlzeit beziehungsweise Andacht;
- atomare gemeinsame Anlage und Rollback;
- Löschung mit Entkoppeln oder gemeinsamem Papierkorb.

### 18.4 Catering und Einkauf

- Rezept-Snapshot bleibt nach Bibliotheksänderung unverändert;
- explizite Snapshot-Aktualisierung;
- Portionsskalierung mit Dezimalgenauigkeit;
- g/kg- und ml/l-Umrechnung;
- Ablehnung inkompatibler Einheiten;
- Camp-Standard und Mahlzeitenüberschreibung;
- Zutaten-Normalisierung und kontrolliertes Merge;
- Übernahme in eine frei gewählte Einkaufsliste;
- nachträgliche Mengenbearbeitung;
- Quellenverfolgung und spontaner Eintrag;
- konkurrierendes Abhaken.

### 18.5 Spiritual

- Schlachter 1951 ist Standard;
- alle vier Übersetzungen und Attributionen;
- Speichern eines Snapshots;
- keine stille Aktualisierung;
- explizites Refresh;
- Bible-API-Ausfall und Timeout;
- lokale Weiterarbeit mit bestehendem Snapshot;
- optionaler, nicht gate-relevanter Live-Contract-Test ist separat markiert.

### 18.6 Files, Löschen und Datenschutz

- erlaubte Formate;
- falsche Erweiterung, MIME- und Magic-Byte-Kombinationen;
- Größenlimit pro Datei;
- Quota je Camp beziehungsweise Bibliothek;
- Cross-Tenant-Download;
- abgelaufener kurzlebiger Zugriff;
- Bilder versus PDF-Download;
- Soft Delete, Restore und 30-Tage-Purge;
- Blob-Purge;
- Archiv schreibgeschützt und Reaktivierung;
- Konto- und Tenant-Karenz inklusive Abbruch.

### 18.7 PWA, Suche und Exporte

- installierbares Manifest und Service Worker;
- Read-only-Snapshot für genau die erlaubten vier Bereiche;
- Offline-Schreibversuch wird verständlich verhindert;
- Snapshot-Zeitpunkt;
- Cache-Löschung bei Logout und Organization-Wechsel;
- campweite Suche und Filter;
- CSV-Encoding, Sonderzeichen und Schutz vor Formelinjektion;
- Druckansichten.

### 18.8 UX und Dokumentation

- WCAG-Axe-Prüfung zentraler Journeys;
- vollständige Tastaturbedienung;
- Fokus nach Navigation, Dialog und Validierungsfehler;
- 320-Pixel-Layout und 400-Prozent-Zoom-nahe Prüfung;
- Mobil-, Tablet- und Desktop-Smoke;
- Hilfe-Routen und interne Links;
- Screenshot-Aktualität;
- Anwenderdokumentation passt zum implementierten Verhalten.

## 19. Anwenderhilfe und technische Dokumentation

Erzeuge eine deutsche VitePress-Anwenderhilfe und liefere sie unter /hilfe/ aus.

Sie enthält mindestens:

- Einstieg und Anmeldung;
- Organizations, Camps, Rollen und Einladungen;
- Tagesplanung einschließlich zugänglicher Alternative;
- Essen und Rezepte;
- Material und Einkaufslisten;
- Andachten und Bibeltexte;
- Notizen und Dateien;
- Suche, Papierkorb, Druck und CSV;
- Offline-Nutzung;
- Konto, Sitzungen und Löschung;
- Datenschutz-, Lizenz- und bekannte Produktgrenzen.

Erzeuge die verwendeten Screenshots automatisiert mit Playwright aus deterministischen Seed-Daten. Prüfe Links und erkenne veraltete beziehungsweise fehlende Screenshots im Verify-Gate.

Erzeuge außerdem:

- README.md mit Voraussetzungen, Bootstrap, lokaler Ausführung, Tests, Architekturüberblick und Troubleshooting;
- docs/architecture mit Modul-, Daten-, Auth-, RLS-, PWA- und Deployment-Erklärung;
- wenige ADRs nur für dauerhafte, überraschende und schwer rückgängig zu machende Entscheidungen;
- THIRD_PARTY_NOTICES.md;
- LICENSE mit MIT-Lizenz für den Anwendungscode;
- ein Operations-Runbook;
- .azure/deployment-plan.md;
- CONTEXT-MAP.md und Modulglossare.

Dokumentation ist Teil jedes Slices und nicht eine Abschlussaufgabe.

## 20. Observability

- Instrumentiere ASP.NET Core, HTTP, Npgsql und wichtige Mediator-Slices mit OpenTelemetry.
- Lokal werden Traces, Metriken und Logs im Aspire Dashboard sichtbar.
- Produktion verwendet Azure Monitor OpenTelemetry und Application Insights.
- Korrelations-IDs laufen durch API und relevante Hintergrundprozesse.
- Definiere sinnvolle Health- und Readiness-Prüfungen.
- Metriken und Logs enthalten keine fachlichen Langtexte, Tokens, Einmalcodes, Datei-URLs oder unnötige personenbezogene Daten.
- Erzeuge keine hochkardinalen Labels mit Nutzer-, Organization- oder Camp-IDs.

## 21. Ausdrücklich nicht im Umfang

Implementiere nicht:

- Teilnehmer- oder Elternportal;
- Billing, Tarife oder Abonnements;
- Inventar, Lager oder Ausleihe;
- generisches Aufgabenmodul;
- Kommentare, Erwähnungen oder private Notizen;
- Push-, Änderungs-, Zuweisungs- oder Digest-E-Mails;
- SignalR oder echte Live-Kollaboration;
- externen Volltext-Suchdienst;
- Offline-Schreiben oder Offline-Konfliktsynchronisation;
- feste Tages-Slots;
- Staging- oder PR-Umgebung;
- Private Endpoints oder VNet-Isolation;
- Malware-Scanning;
- Dark Mode;
- moderne kostenpflichtige Bibelübersetzungen;
- serverseitige PDF-, Word- oder Excel-Erzeugung;
- Datenimporte;
- mandantenspezifisches Branding;
- medizinische Teilnehmer- oder Ernährungsprofile;
- Dichteumrechnung oder automatische Packungsrundung;
- vollständige Gottesdienst-, Lied- oder Liturgieplanung;
- KI-Generierung oder KI-Training;
- Azure-Deployment in dieser Sitzung.

Diese Ausschlüsse sind Produktgrenzen. Baue dafür keine vorbereitenden Frameworks.

## 22. Definition of Done

Ein Slice ist erst fertig, wenn:

- sein zuerst geschriebener Akzeptanztest rot beobachtet wurde;
- UI, API, Domänenregeln, Persistenz und Autorisierung vollständig sind;
- Fehler-, Leer-, Lade- und Konfliktzustände funktionieren;
- zielgerichtete und betroffene Tests grün sind;
- Modulgrenzen eingehalten werden;
- OpenAPI und TypeScript-Client aktuell sind;
- relevante Anwender- und Architekturdokumentation aktuell ist;
- Accessibility berücksichtigt und geprüft ist;
- PROGRESS.md konkrete Evidenz enthält;
- ein kleiner grüner lokaler Commit existiert.

Die gesamte v1 ist erst fertig, wenn:

- alle Anforderungen dieses Auftrags implementiert sind;
- alle Standardtests ohne Internet laufen;
- Format, Lint und Builds ohne Warnungen erfolgreich sind;
- Architekturtests grün sind;
- Coverage mindestens 80 Prozent Lines und 75 Prozent Branches erreicht;
- die kritischen Sicherheits- und Domänenszenarien vollständig abgedeckt sind;
- Playwright in Chromium, Firefox und WebKit erfolgreich ist;
- axe für zentrale Journeys keine ungeklärten AA-Verstöße meldet;
- die echte gerenderte UI in allen drei Viewports visuell inspiziert wurde und keine fehlerhaften Controls, Layoutfehler oder sichtbaren Artefakte enthält;
- PROGRESS.md Seiten, Zustände, Viewports und Screenshot-Evidenz dieser visuellen Prüfung dokumentiert;
- der PWA-Offline-Snapshot nachweislich korrekt und begrenzt ist;
- Smoke Tests über die lokale Aspire-Umgebung erfolgreich sind;
- VitePress-Hilfe und Screenshots aktuell sind;
- azd-/Bicep-Artefakte lokal validiert sind;
- keine Secrets oder personenbezogenen Testdaten committed sind;
- git diff --check sauber ist;
- PROGRESS.md alle Pflicht-Slices als verified ausweist;
- der Arbeitsbaum nur absichtlich verbleibende Änderungen enthält;
- kein Azure-Deployment ausgeführt wurde.

## 23. Abschlussbericht

Beende deine Arbeit erst nach vollständiger lokaler Verifikation.

Der Abschlussbericht ist knapp, ergebnisorientiert und enthält:

- was vollständig implementiert wurde;
- Architektur- und Sicherheitsentscheidungen, die für die Übergabe wichtig sind;
- exakte lokale Startbefehle;
- ausgeführte Verify- und Smoke-Befehle mit Resultaten und gemessenen Zeiten;
- Coverage-Ergebnisse;
- erzeugte lokale Commit-Hashes;
- Bestätigung, dass kein Azure-Deployment und kein Push erfolgte;
- ausschließlich bewusste, in diesem Auftrag ausdrücklich erlaubte Produktgrenzen.

Liste keine „nächsten Schritte“ für noch fehlende Muss-Arbeit auf. Wenn Muss-Arbeit fehlt, ist die Aufgabe noch nicht beendet: arbeite weiter.
