# Organisationen, Freizeiten und Rollen

Eine **Organisation** ist der Veranstalter und kann mehrere **Freizeiten** planen. Zugriff entsteht ausschließlich durch
einen weitergebbaren Einladungslink. Der Link zeigt vor der Annahme, welche Rolle, Organisation und gegebenenfalls
welche Freizeit du erhältst.

- Superadmin-Links gelten eine Stunde, Organisationsadmin-Links 48 Stunden und Freizeit-Links sieben Tage.
- Ein Link ist nur einmal nutzbar. Admins können offene Links widerrufen oder sicher neu ausstellen.
- Wenn du bereits ein Konto hast, meldest du dich zuerst an und fügst die Rolle diesem Konto hinzu.
- Ohne Konto gibst du E-Mail-Adresse, Vorname, Nachname und ein neues Passwort an. Erst der Bestätigungslink aus der
  E-Mail schließt Registrierung und Einladung gemeinsam ab. Währenddessen ist der Link höchstens eine Stunde für
  dich reserviert.

Ein Link ist ein Zugangsschlüssel. Teile ihn nur mit der vorgesehenen Person und widerrufe ihn, falls er in falsche
Hände geraten sein könnte. Zustände wie **abgelaufen**, **verwendet**, **widerrufen** oder **reserviert** werden auf
der Einladungsseite verständlich erklärt.

Organisationsadmins sehen alle Freizeiten der Organisation. Freizeit-Leitungen, Mitarbeitende und Personen mit
Lesezugriff werden einzelnen Freizeiten zugeordnet. Eine Freizeit-Einladung verringert niemals eine bereits höhere
Organisationsrolle.

## Rollen

- **Superadmins** verwalten organisationsübergreifend Konten, Organisationen und Rechte. Freizeit-Inhalte sehen sie erst
  mit einer zusätzlichen Organisationsadmin-Zuweisung.
- **Organisationsadmins** verwalten innerhalb ihrer Organisation Einstellungen, Löschung, Freizeiten, Mitglieder,
  weitere Organisationsadmins, Einladungen und Freizeit-Zuweisungen.
- **Freizeit-Leitungen** verwalten ihre zugewiesenen Freizeiten.
- **Mitglieder** bearbeiten Planungsinhalte ihrer Freizeiten.
- **Lesender Zugriff** erlaubt Lesen, Drucken und Exportieren.

Eine Organisation darf bewusst ohne Organisationsadmin bestehen. Ein Organisationsadmin kann sie nach einer frischen Anmeldung und der
exakten Eingabe des Organisations-Slugs zur Löschung vormerken; dabei gilt eine Karenz von 30 Tagen.

## Freizeiten anlegen und verwalten

Öffne über **Meine Organisationen** oder **Verwaltung → Freizeiten** die Freizeit-Liste einer Organisation. Dort stehen zukünftige, laufende und
vergangene Freizeiten getrennt. Organisationsadmins können **Freizeit anlegen** wählen und Name, eindeutigen Slug,
Beschreibung, Zeitraum, IANA-Zeitzone und Standardportionen festlegen.

![Camp-Liste mit leerem Zustand und der Aktion „Camp anlegen“](/screenshots/freizeiten-desktop.png)

Über **Einstellungen** lassen sich diese Angaben mit Versionsschutz ändern. **Freizeit archivieren** macht die gesamte
Freizeit schreibgeschützt; Lesen, Drucken und Exportieren bleiben möglich. Berechtigte Personen können sie in denselben
Einstellungen mit **Freizeit reaktivieren** wieder für Änderungen öffnen. Wird zwischenzeitlich eine neuere Version
gespeichert, lade die Seite neu und wiederhole deine Änderung auf Basis des aktuellen Stands.

Beim Öffnen einer Freizeit übernimmt der Arbeitsbereich automatisch deren Zeitraum und Zeitzone. Eine archivierte Freizeit
zeigt auf jeder Fachseite den Hinweis **Archiviert · nur lesen** und deaktiviert dort Änderungen; über
**Einstellungen** kann sie mit ausreichender Berechtigung reaktiviert werden.

Die **Übersicht** zeigt den heutigen oder nächsten befüllten Tagesplan in der Camp-Zeitzone. Außerdem fasst sie deine
aktiven Zeitplan-Verantwortungen, noch offenes beziehungsweise geplantes Material, ungeprüfte Einkaufspositionen und
die jüngsten Aktivitäten zusammen. Bei bereits beendeten Camps wird der letzte befüllte Tag angezeigt.

![Camp-Übersicht mit Tagesplan, Verantwortungen, Beschaffung und Aktivitäten](/screenshots/uebersicht-desktop.png)

## Team und Rechte verwalten

Organisationsadmins erreichen **Verwaltung → Team & Rechte** von jeder angemeldeten Seite aus. Die Ergebnisliste
zeigt zunächst nur Identität, Status und Rollenübersicht. Nach Auswahl einer Person sind Organisation und Freizeiten
getrennt bearbeitbar. Routineänderungen werden direkt gespeichert. Eine Sperre erhält einen eigenen Dialog, der ihre
Wirkung genau benennt. Zwischenzeitliche Änderungen werden erkannt und müssen nach erneutem Laden wiederholt werden.

## Superadmin-Verwaltung

Superadmins öffnen **Plattform verwalten** in der globalen Kopfzeile. Die persistente Navigation wechselt mit einem
Klick zwischen **Organisationen** und **Benutzer**. Neue Organisationen und Personen werden in fokussierten Dialogen
eingerichtet. In der Benutzeransicht sind Konto, Plattformrolle, Organisationen und Freizeiten getrennt. Superadmins
können Konten global sperren, temporäre Anmeldesperren aufheben sowie Superadmin- und Organisationsadmin-Rechte
vergeben. Globale Sperren und der Entzug einer Superadmin-Rolle müssen mit ihrer genauen Wirkung bestätigt werden.
Ohne zusätzliche Organisationsadmin-Zuweisung bleiben fachliche Freizeit-Inhalte unzugänglich. Der letzte aktive
Superadmin kann weder gesperrt noch herabgestuft werden.
