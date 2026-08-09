# Material und Einkauf

Unter **Material & Einkauf** stehen Materialbedarf und gemeinsame Einkaufslisten des geöffneten Camps. Die Bereiche
verwenden dieselben servergespeicherten Planungsdaten wie Übersicht, Suche und Export; Beispielpositionen werden
nicht lokal erzeugt.

## Einkaufslisten öffnen und aktualisieren

Ein Camp kann mehrere benannte Einkaufslisten besitzen. **Einkaufsliste anlegen** erstellt eine weitere Liste.
Jede Listenkarte zeigt offene und erledigte Positionen. Mit **Liste öffnen** lädst du die vollständigen Positionen
einschließlich Menge, Einheit, Geschäft, Notiz und ihrer Quelle.

Die geöffnete Liste wird ungefähr alle 15 Sekunden und erneut beim Fokussieren des Browserfensters geladen. Das ist
bewusst einfaches Polling; es gibt keine Echtzeitverbindung. Änderungen anderer Teammitglieder werden dadurch mit
kurzer Verzögerung sichtbar.

## Spontane Position hinzufügen

In einer geöffneten Liste legst du unter **Spontane Position** eine Bezeichnung und eine positive Dezimalmenge fest.
Zur Auswahl stehen Gramm, Kilogramm, Milliliter, Liter, Stück und eine benutzerdefinierte Einheit. Bei der
benutzerdefinierten Einheit ist zusätzlich ihr Name erforderlich. Geschäft und Notiz sind optional.

Die Position erhält automatisch die Quelle „Spontan“. Aus Mahlzeiten übernommene Positionen zeigen stattdessen ihre
Rezeptquelle; diese Herkunft bleibt erhalten. Wird die Liste gleichzeitig verändert, prüfe den neu geladenen Stand
und wiederhole die Anlage bewusst.

## Position nachträglich bearbeiten

Mit **Position bearbeiten** kannst du Bezeichnung, Dezimalmenge, Einheit, Geschäft und Notiz später ändern. Außerdem
lassen sich die im Camp auswählbaren Personen als Verantwortliche zuordnen. Die angezeigte Quelle wird dabei nicht
verändert. Speichern prüft ausschließlich die Version dieser Position; bei einem Konflikt öffnest du den aktuellen
Stand erneut und vergleichst deine Angaben.

**Position löschen** öffnet zuerst eine Sicherheitsabfrage. Nach der ausdrücklichen Bestätigung wird die Position in
den Camp-Papierkorb verschoben, bleibt dort 30 Tage wiederherstellbar und verschwindet aus der aktiven Liste.

## Mobil abhaken und wieder öffnen

Aktiviere die Checkbox einer Position, sobald sie eingekauft ist. Jede Position besitzt eine eigene Version, sodass
gleichzeitiges Abhaken nicht die gesamte Liste überschreibt. Die Anwendung zeigt anschließend, wer die Position zu
welcher Serverzeit abgehakt hat. Dieselbe Checkbox öffnet die Position wieder.

Gemeinsame Ansichten bleiben in einem archivierten Camp lesbar, erlauben dort aber keine Anlage und kein Abhaken.

## Liste umbenennen oder löschen

Eine geöffnete Liste kann mit **Umbenennen** einen neuen Namen erhalten. **Liste löschen** verlangt eine gesonderte
Bestätigung und verschiebt die komplette Liste einschließlich ihrer Positionen für 30 Tage in den Papierkorb. Beide
Aktionen prüfen die aktuelle Listen-Version. Ein archiviertes Camp zeigt diese Aktionen nicht.
