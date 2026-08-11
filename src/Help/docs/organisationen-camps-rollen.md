# Organisationen, Camps und Rollen

Eine **Organisation** ist der Veranstalter und kann mehrere **Camps** planen. Zugriff entsteht ausschließlich durch
einen weitergebbaren Einladungslink. Der Link zeigt vor der Annahme, welche Rolle, Organisation und gegebenenfalls
welches Camp du erhältst.

- Superadmin-Links gelten eine Stunde, Orgadmin-Links 48 Stunden und Camp-Links sieben Tage.
- Ein Link ist nur einmal nutzbar. Admins können offene Links widerrufen oder sicher neu ausstellen.
- Wenn du bereits ein Konto hast, meldest du dich zuerst an und fügst die Rolle diesem Konto hinzu.
- Ohne Konto gibst du E-Mail-Adresse, Vorname, Nachname und ein neues Passwort an. Erst der Bestätigungslink aus der
  E-Mail schließt Registrierung und Einladung gemeinsam ab. Währenddessen ist der Link höchstens eine Stunde für
  dich reserviert.

Ein Link ist ein Zugangsschlüssel. Teile ihn nur mit der vorgesehenen Person und widerrufe ihn, falls er in falsche
Hände geraten sein könnte. Zustände wie **abgelaufen**, **verwendet**, **widerrufen** oder **reserviert** werden auf
der Einladungsseite verständlich erklärt.

Orgadmins sehen alle Camps der Organisation. Camp-Leitungen, Mitglieder und Personen mit
Lesezugriff werden einzelnen Camps zugeordnet. Eine Camp-Einladung verringert niemals eine bereits höhere
Organisationsrolle.

## Rollen

- **Superadmins** verwalten organisationsübergreifend Konten, Organizations und Rechte. Camp-Inhalte sehen sie erst
  mit einer zusätzlichen Orgadmin-Zuweisung.
- **Orgadmins** verwalten innerhalb ihrer Organisation Einstellungen, Löschung, Camps, Mitglieder, weitere
  Orgadmins, Einladungen und Camp-Zuweisungen.
- **Camp-Leitungen** verwalten ihre zugewiesenen Camps.
- **Mitglieder** bearbeiten Planungsinhalte ihrer Camps.
- **Lesender Zugriff** erlaubt Lesen, Drucken und Exportieren.

Eine Organisation darf bewusst ohne Orgadmin bestehen. Ein Orgadmin kann sie nach einer frischen Anmeldung und der
exakten Eingabe des Organisations-Slugs zur Löschung vormerken; dabei gilt eine Karenz von 30 Tagen.

## Camps anlegen und verwalten

Öffne unter **Mein Konto → Organisationen** die Camp-Liste einer Organisation. Dort stehen zukünftige, laufende und
vergangene Camps getrennt. Orgadmins können **Camp anlegen** wählen und Name, eindeutigen Slug,
Beschreibung, Zeitraum, IANA-Zeitzone und Standardportionen festlegen.

![Camp-Liste mit leerem Zustand und der Aktion „Camp anlegen“](/screenshots/freizeiten-desktop.png)

Über **Einstellungen** lassen sich diese Angaben mit Versionsschutz ändern. **Camp archivieren** macht das gesamte
Camp schreibgeschützt; Lesen, Drucken und Exportieren bleiben möglich. Berechtigte Personen können es in denselben
Einstellungen mit **Camp reaktivieren** wieder für Änderungen öffnen. Wird zwischenzeitlich eine neuere Version
gespeichert, lade die Seite neu und wiederhole deine Änderung auf Basis des aktuellen Stands.

Beim Öffnen eines Camps übernimmt der Arbeitsbereich automatisch dessen Zeitraum und Zeitzone. Ein archiviertes Camp
zeigt auf jeder Fachseite den Hinweis **Archiviert · nur lesen** und deaktiviert dort Änderungen; über
**Camp-Einstellungen** kann es mit ausreichender Berechtigung reaktiviert werden.

Die **Übersicht** zeigt den heutigen oder nächsten befüllten Tagesplan in der Camp-Zeitzone. Außerdem fasst sie deine
aktiven Zeitplan-Verantwortungen, noch offenes beziehungsweise geplantes Material, ungeprüfte Einkaufspositionen und
die jüngsten Aktivitäten zusammen. Bei bereits beendeten Camps wird der letzte befüllte Tag angezeigt.

![Camp-Übersicht mit Tagesplan, Verantwortungen, Beschaffung und Aktivitäten](/screenshots/uebersicht-desktop.png)

## Mitglieder verwalten

Orgadmins öffnen unter **Mein Konto → Organisationen** die Benutzerverwaltung. Eine Rolle
wird direkt in der Zeile der Person geändert; **Entfernen** beendet deren Mitgliedschaft. Zwischenzeitliche
Änderungen werden erkannt und müssen nach erneutem Laden wiederholt werden. Orgadmins können weitere Orgadmins
ernennen, sperren und entfernen sowie Mitglieder konkreten Camps zuweisen. Camp-Leitungen dürfen vorhandene
Mitglieder nur im eigenen Camp verwalten.

## Superadmin-Verwaltung

Superadmins finden unter **Mein Konto** die Benutzer- und Organisationsverwaltung. Sie können Konten global sperren,
temporäre Anmeldesperren aufheben, Superadmin- und Orgadmin-Rechte vergeben sowie Organizations sperren oder
entsperren. Eine globale Kontosperre beendet alle Sitzungen sofort; eine Organisationssperre wirkt nur innerhalb
dieser Organization. Ohne zusätzliche Orgadmin-Zuweisung bleiben fachliche Camp-Inhalte auch für Superadmins
unzugänglich. Der letzte aktive Superadmin kann weder gesperrt noch herabgestuft werden.
