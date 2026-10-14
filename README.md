# Set-Finder

Eine Web-App fürs Handy: Foto der ausliegenden **Set**-Karten aufnehmen, die App erkennt die Karten
und zeigt alle Sets an, oder meldet, dass drei Karten nachgelegt werden müssen.

- **.NET 10 (LTS), Blazor WebAssembly**: läuft vollständig im Browser.
- **Keine Server-Logik, kein KI-Dienst**: Die Bilderkennung ist klassische Bildverarbeitung in C#.
  Das Foto verlässt das Gerät nie.
- **Nur statische Dateien**: Jeder Webserver (Nginx, Apache, …) genügt.
- **Offline-fähig (PWA)**: Nach dem ersten Besuch auch ohne Netz nutzbar und zum Startbildschirm hinzufügbar.

## Spielregel in einem Satz

Drei Karten bilden ein Set, wenn jede der vier Eigenschaften (Anzahl, Farbe, Füllung, Form) bei allen
dreien gleich oder bei allen dreien verschieden ist. Gibt es in der Auslage kein Set, werden drei Karten
nachgelegt. Siehe [Wikipedia](https://de.wikipedia.org/wiki/Set_(Spiel)) und
[Brettspielwelt](https://www.brettspielwelt.de/Hilfe/Anleitungen/Set/).

## Aufbau

| Pfad | Inhalt |
| --- | --- |
| `src/SetGameSolver.Core/Model` | Karte und ihre vier Eigenschaften |
| `src/SetGameSolver.Core/Solving` | Set-Suche (alle Sets in einer Auslage beliebiger Größe) |
| `src/SetGameSolver.Core/Vision` | Bilderkennung: Karten finden, entzerren, klassifizieren |
| `src/SetGameSolver.Core/Synthetic` | Erzeugt künstliche Kartenfotos für Tests und Beispielbilder |
| `src/SetGameSolver.Web` | Blazor-WebAssembly-Oberfläche |
| `tests/SetGameSolver.Core.Tests` | xUnit-Tests für Regeln und Erkennung |
| `deploy/nginx` | Beispielkonfiguration für Nginx (Apache: `.htaccess` wird mit veröffentlicht) |

## Entwickeln

Voraussetzung: .NET SDK 10.

```bash
dotnet test                                  # alle Tests
dotnet watch --project src/SetGameSolver.Web # lokaler Entwicklungsserver
```

Zum Testen mit dem Handy im selben WLAN: `dotnet run --project src/SetGameSolver.Web --urls http://0.0.0.0:5000`.
Die Kamera-Schaltfläche öffnet die Kamera-App über ein normales Datei-Eingabefeld und funktioniert daher
auch ohne HTTPS; der Offline-Modus braucht dagegen HTTPS.

## Veröffentlichen

```bash
dotnet publish src/SetGameSolver.Web -c Release -o publish
```

Der Inhalt von `publish/wwwroot` (inklusive der versteckten Datei `.htaccess`) ist die komplette App.
Er kann in das Wurzelverzeichnis der Domain **oder in einen beliebigen Unterordner** kopiert werden,
ohne etwas anzupassen. Der GitHub-Actions-Workflow erzeugt denselben Ordner als Artefakt `set-finder-wwwroot`.

### Nginx

`deploy/nginx/set-finder.conf` als Vorlage nehmen, `server_name` und `root` anpassen. Wichtig sind der
MIME-Typ `application/wasm`, `gzip_static on` für die vorkomprimierten Dateien und lange Cache-Zeiten
nur für `_framework/` (die Dateinamen dort enthalten einen Hash).

### Apache

Die mitgelieferte `.htaccess` setzt MIME-Typen, liefert die vorkomprimierten `.br`/`.gz`-Dateien aus und
regelt das Caching. Nötig sind `AllowOverride All` für das Verzeichnis sowie `mod_rewrite` und `mod_headers`
(`a2enmod rewrite headers`).

## Wie die Erkennung funktioniert

1. Das Foto wird im Browser gedreht (EXIF) und auf höchstens 1280 Pixel Kantenlänge verkleinert.
2. **Karten finden**: Kartenpapier ist hell und ungesättigt. Ein automatisch bestimmter Schwellwert
   (Otsu) auf dem kleinsten Farbkanal trennt Karten vom Tisch. Zusammenhängende Flächen mit
   Rechteckform und passendem Seitenverhältnis gelten als Karten; die Ecken werden über an die Kanten
   angepasste Geraden bestimmt, damit auch schräg fotografierte Karten passen.
3. **Entzerren**: Jede Karte wird per Homographie in ein Hochformat-Bild von 160 × 250 Pixeln umgerechnet.
4. **Klassifizieren** (nach Weißabgleich auf das Kartenpapier):
   - *Anzahl*: Zahl der Symbol-Silhouetten.
   - *Form*: Konvexität (Wellen sind nicht konvex) und Flächenanteil im umgebenden Rechteck (Rauten ≈ 50 %, Ovale ≈ 90 %).
   - *Füllung*: Farbanteil im Inneren der Symbole relativ zum Umriss (voll ≈ 1, gestreift ≈ 0,4, leer ≈ 0).
   - *Farbe*: Farbton der kräftig gefärbten Pixel (Grün, Lila, sonst Rot).
5. Die Set-Suche prüft jedes Kartenpaar und schlägt die eindeutig passende dritte Karte nach.

Unsichere Erkennungen werden gestrichelt markiert; jede Karte lässt sich in der App per Antippen korrigieren
oder entfernen, Karten können auch von Hand hinzugefügt werden.

### Grenzen und nächste Schritte

Die Schwellwerte sind bisher nur an **synthetischen** Fotos eingestellt (siehe `Synthetic/`). Echte Fotos
bringen Unschärfe, Spiegelungen, Schatten und abweichende Druckfarben mit. Nächster Schritt: echte Fotos
mit bekannter Auslage als Testdaten aufnehmen und die Schwellwerte in `CardClassifier` daran nachjustieren.

Gut funktioniert die Erkennung, wenn die Karten auf einem dunklen, einfarbigen Untergrund liegen, sich nicht
berühren und von oben ohne Blitz-Spiegelung fotografiert werden.
