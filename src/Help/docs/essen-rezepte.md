# Essen und Rezepte

Unter **Essen & Rezepte** stehen die veranstalterweite Rezeptbibliothek und die Mahlzeiten des geöffneten Camps
nebeneinander. Bibliotheksrezepte gehören der gesamten Organisation; Mahlzeiten und ihre Rezept-Snapshots gehören
zum Camp.

## Rezept anlegen

Owner und Organisations-Admins wählen **Rezept anlegen** und erfassen Name, Beschreibung, Zubereitung und
Basisportionen. Suche eine vorhandene Zutat mit mindestens zwei Zeichen und füge sie dem Rezept hinzu. Jede
Zutatenposition benötigt eine positive Dezimalmenge und eine Einheit:

- Gramm oder Kilogramm;
- Milliliter oder Liter;
- Stück;
- eine benannte Zähleinheit, zum Beispiel „Bund“.

Mehrere Ernährungs-Tags werden mit Komma oder Semikolon getrennt. Allergen- und Küchenhinweise sind manuell
gepflegte Planungshinweise und keine medizinische Garantie. **Rezept speichern** legt eine neue unveränderliche
Rezeptversion an.

## Rezept öffnen und überarbeiten

Mit **Rezept öffnen** siehst du die vollständige aktuelle Version: Beschreibung, Zubereitung, Basisportionen,
Zutatenmengen, Ernährungs-Tags sowie Allergen- und Küchenhinweise. Owner und Organisations-Admins können anschließend
**Rezept bearbeiten** wählen. Alle bisherigen Angaben werden in das Formular übernommen; Zutaten lassen sich dort
ergänzen, ändern oder entfernen.

**Neue Rezeptversion speichern** überschreibt keinen alten Stand, sondern legt eine weitere unveränderliche Version
an. Bereits in Mahlzeiten verwendete Rezept-Snapshots bleiben unverändert. Wurde das Rezept während deiner
Bearbeitung bereits von jemand anderem geändert, schließe die Bearbeitung, öffne den aktuellen Stand erneut und
prüfe deine Änderungen, bevor du sie noch einmal speicherst.

## Dateien am Rezept

Im geöffneten Rezept zeigt **Dateien** alle bereits hinterlegten Anhänge und die Belegung der gemeinsamen
100-MiB-Rezeptbibliotheksquote. Owner und Organisations-Admins können PDF-, JPEG-, PNG- oder WebP-Dateien bis
höchstens zehn MiB auswählen und hochladen. Andere Formate werden nicht akzeptiert.

**Dateiname öffnen** fordert zuerst eine kurzlebige, nur für dein Konto gültige Leseberechtigung an. Bilder werden
sicher angezeigt, PDFs nur heruntergeladen; die Produktionsdateien bleiben privat. Die Anwendung führt bewusst
keine Malware-Prüfung durch. Lade deshalb ausschließlich Dateien aus vertrauenswürdigen Quellen hoch.

## Zutaten verwalten

Owner und Organisations-Admins öffnen **Zutaten verwalten**. Neue Namen werden vereinheitlicht; Schreibweise,
Unicode-Varianten oder mehrfach gesetzte Leerzeichen erzeugen deshalb keine unbemerkten Duplikate. Beim Umbenennen
wird die angezeigte Version geprüft. Ist die Zutat inzwischen geändert worden, lade den aktuellen Stand und prüfe
deine Änderung erneut.

Für ein kontrolliertes Zusammenführen wählst du zuerst die doppelte Zutat und danach die Zielzutat. Mit
**Zusammenführung prüfen** siehst du alle aktuellen Rezepte, die eine neue Version erhalten würden. Bereits
gespeicherte Mahlzeiten-Snapshots bleiben unverändert. Erst nach der ausdrücklichen Bestätigung kann die doppelte
Zutat in die Zielzutat überführt werden.

## Mahlzeit planen und Rezeptstand aktualisieren

Mit **Mahlzeit planen** legst du eine Mahlzeit im geöffneten Camp an. Ohne Überschreibung verwendet sie die
Camp-Standardpersonenzahl. Aktiviere **Personenzahl überschreiben**, wenn diese einzelne Mahlzeit für eine andere
Gruppengröße skaliert werden soll. Optional kannst du genau einen vorhandenen Zeitplaneintrag verknüpfen und mehrere
Bibliotheksrezepte auswählen.

Jedes ausgewählte Rezept wird beim Speichern als eigener unveränderlicher Snapshot in die Mahlzeit kopiert. Öffne
eine Mahlzeit, um die wirksame Personenzahl und die passend skalierten Dezimalmengen zu sehen. Ist inzwischen eine
neuere Bibliotheksversion verfügbar, bleibt der verwendete Stand zunächst erhalten. Erst mit **Rezeptname auf Version
… aktualisieren** wird ausdrücklich ein neuer Snapshot aus dem aktuellen Bibliotheksrezept erzeugt.

In den Mahlzeitdetails kannst du außerdem Name, Personenzahl und Zeitplanverknüpfung bearbeiten. Weitere aktuelle
Bibliotheksrezepte lassen sich als Snapshot hinzufügen; nicht mehr benötigte Snapshots kannst du aus der Mahlzeit
entfernen. Jede Änderung prüft die angezeigte Mahlzeitenversion. Bei einem Konflikt öffnest du den aktuellen Stand
erneut. **Mahlzeit in Papierkorb verschieben** verlangt eine zusätzliche Bestätigung; dort bleibt sie 30 Tage
wiederherstellbar.

## Dateien an der Mahlzeit

Im geöffneten Mahlzeitdetail zeigt **Dateien** alle privaten Anhänge und die Belegung der 100-MiB-Campquote.
Camp-Mitglieder mit Schreibrecht können PDF-, JPEG-, PNG- oder WebP-Dateien bis höchstens zehn MiB hochladen und
über die ausdrückliche Bestätigung in den gemeinsamen Camp-Papierkorb verschieben. Dort bleiben sie 30 Tage
wiederherstellbar. In einem archivierten Camp kannst du vorhandene Dateien weiterhin lesen, aber nicht verändern.

**Dateiname öffnen** fordert für den aktuellen Benutzer eine einmalige, kurzlebige Leseberechtigung an. Die Datei
bleibt privat; Bilder werden sicher angezeigt, PDFs heruntergeladen. Eine Malware-Prüfung ist nicht enthalten. Lade
daher nur vertrauenswürdige Dateien hoch.

## Mahlzeit in eine Einkaufsliste übernehmen

Öffne eine Mahlzeit und wähle **In Einkaufsliste übernehmen**. Der Entwurf verwendet die unveränderlichen
Rezept-Snapshots und zeigt zu jeder Position die nachvollziehbare Quelle. Wähle eine der vorhandenen Einkaufslisten
des Camps frei als Ziel. Einzelne Positionen kannst du vor der Übernahme abwählen.

Prüfe Menge und Einheit jeder ausgewählten Position. Es werden nur Einheiten derselben fachlichen Dimension
angeboten, also Gramm/Kilogramm, Milliliter/Liter oder die jeweilige Zähleinheit. Die Anwendung rechnet weder über
Dimensionen hinweg noch rundet sie auf Packungsgrößen. Eine gewünschte Umrechnung oder Einkaufsmenge trägst du
deshalb ausdrücklich selbst ein. **Position übernehmen** beziehungsweise **Positionen übernehmen** speichert alle
ausgewählten Zeilen gemeinsam in der gewählten Liste und erhält ihre Mahlzeiten-, Snapshot-, Zutaten- und
Rezeptversionsquelle.

Wurde die Einkaufsliste inzwischen von jemand anderem verändert, lade den Entwurf neu und prüfe Ziel, Mengen und
Einheiten noch einmal. In einem archivierten Camp ist die Übernahme nicht verfügbar.

## Bibliothek und Camp-Snapshots

Die Suche oberhalb der Bibliothek filtert Rezepte nach ihrem Namen. Wird ein Bibliotheksrezept einer Mahlzeit
hinzugefügt, entsteht dort ein Snapshot. Spätere Bibliotheksänderungen verändern diesen Snapshot nicht automatisch;
eine neuere Version muss ausdrücklich übernommen werden.

Ein archiviertes Camp bleibt lesbar, erlaubt in seinem Arbeitsbereich aber keine Änderungen. Zutaten und Rezepte
können nur mit ausreichender Organisationsrolle verwaltet werden.
