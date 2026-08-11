# Anmelden und erste Einrichtung

Melde dich unter **Anmelden** mit deiner E-Mail-Adresse und deinem Passwort an. Beide Felder unterstützen
Passwortmanager. Mit **Passwort anzeigen** kannst du die Eingabe vor dem Absenden prüfen.

Ein Passwort muss 15 bis 128 Zeichen lang sein. Leerzeichen und Unicode-Zeichen sind erlaubt. Nach zehn falschen
Versuchen wird das Konto für genau 15 Minuten gesperrt. Die Fehlermeldung verrät nicht, ob eine E-Mail-Adresse
registriert ist.

## Angemeldet bleiben

Ohne Auswahl endet die Sitzung spätestens nach zwölf Stunden. Mit **Auf diesem Gerät angemeldet bleiben** wird die Sitzung bei
aktiver Nutzung jeweils um 30 Tage verlängert. Das kurze Zugriffstoken bleibt nur im Arbeitsspeicher des Browsers;
das widerrufbare Erneuerungstoken liegt in einem geschützten HttpOnly-Cookie. Aktive Sitzungen kannst du unter
**Konto → Sicherheit** einzeln oder gesammelt widerrufen.

## Passwort vergessen oder ändern

Über **Passwort vergessen?** forderst du einen Reset-Link an. Die Bestätigung ist absichtlich immer gleich, damit
niemand darüber gültige Konten erkennen kann. Der Link kann nur einmal verwendet werden und läuft nach 60 Minuten
ab. Nach einem erfolgreichen Reset werden alle vorhandenen Sitzungen beendet.

Wenn du dein aktuelles Passwort kennst, änderst du es unter **Konto → Sicherheit**. Auch danach meldest du dich
auf allen Geräten neu an. Für besonders sensible Verwaltungsaktionen kann das Freizeit-Cockpit das Passwort erneut
abfragen; diese Bestätigung gilt höchstens zehn Minuten.

## Ersten Superadmin anlegen

Bei einer leeren Installation zeigt die Anmeldung den Link **Erste Einrichtung**. Gib Vorname, Nachname, E-Mail-Adresse und zweimal das
gewünschte Passwort ein. Diese Seite legt genau ein erstes Superadmin-Konto an und meldet es direkt an. Sobald ein
Konto existiert, ist die Ersteinrichtung dauerhaft geschlossen und verweist zur normalen Anmeldung.

Teile Passwörter und Sitzungs-Cookies niemals mit anderen Personen. Bei einem vermuteten Zugriff widerrufst du die
betroffene Sitzung sofort.
