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

Owner und Organisations-Admins sehen alle Camps der Organisation. Camp-Leitungen, Mitglieder und Personen mit
Lesezugriff werden einzelnen Camps zugeordnet. Eine Camp-Einladung verringert niemals eine bereits höhere
Organisationsrolle.

## Rollen

- **Owner** verwalten Organisationseinstellungen, Owner, Admins, Mitglieder und Löschung.
- **Organisations-Admins** verwalten Camps, Einladungen und niedrigere Rollen.
- **Camp-Leitungen** verwalten ihre zugewiesenen Camps.
- **Mitglieder** bearbeiten Planungsinhalte ihrer Camps.
- **Lesender Zugriff** erlaubt Lesen, Drucken und Exportieren.

Es bleibt immer mindestens ein aktiver Owner. Die Organisation kann nur ein Owner nach einer frischen Anmeldung und
der exakten Eingabe des Organisations-Slugs zur Löschung vormerken; auch hier gilt eine Karenz von 30 Tagen.

## Camps anlegen und verwalten

Öffne unter **Mein Konto → Organisationen** die Camp-Liste einer Organisation. Dort stehen zukünftige, laufende und
vergangene Camps getrennt. Owner und Organisations-Admins können **Camp anlegen** wählen und Name, eindeutigen Slug,
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

Owner und Organisations-Admins öffnen unter **Mein Konto → Organisationen** die Mitgliederverwaltung. Eine Rolle
wird direkt in der Zeile der Person geändert; **Entfernen** beendet deren Mitgliedschaft. Zwischenzeitliche
Änderungen werden erkannt und müssen nach erneutem Laden wiederholt werden. Organisations-Admins können weder Owner
noch weitere Organisations-Admins ernennen, ändern oder entfernen. Camp-Leitungen dürfen bereits vorhandene
Mitglieder nur ihrem eigenen Camp zuweisen.

## Plattformverwaltung

Platform Admins finden unter **Mein Konto** die Plattformverwaltung. Sie sehen Namen, Slug und Status der
Organizations und können eine Organization sperren oder entsperren. Eine Sperre blockiert die weitere Nutzung. Die
Plattformverwaltung zeigt bewusst keine Mitglieder, Camps oder Planungsinhalte an; Platform Admins können diese
fachlichen Mandanteninhalte auch nicht über direkte Links aufrufen.
