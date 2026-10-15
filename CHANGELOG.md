# Versionen

Die fertige App jeder Version liegt als `set-finder-webserver-<Version>.zip` unter
[Releases](https://github.com/Muetzenfrosch/setsolver/releases). Den Inhalt des Zips in das Wurzelverzeichnis
der Domain oder in einen Unterordner kopieren; für Apache liegt eine `.htaccess` bei, für Nginx steht eine
Vorlage unter `deploy/nginx`.

## 1.0 (15.10.2026)

- „Beispiel“ legt bei jedem Tippen 12 neue, verschiedene Karten aus, mit den Symbolen der echten Karten, mal hochkant (3 × 4), mal quer (4 × 3).
- README mit Projektbeschreibung, Bedienung und Rechtehinweis; der Quellcode steht unter der MIT-Lizenz.

## 0.9 (15.10.2026)

- Screenshots aus Set-Apps werden erkannt, auch im Dunkelmodus mit schwarzen Karten.
- Kräftige Umrisse der Symbole stören die Erkennung der Füllung nicht mehr.
- Alle 108 Karten auf 9 Screenshots richtig; insgesamt 609 von 621 Karten.

## 0.8 (15.10.2026)

- „Erkannte Karten“ zeigt die Karten im selben Raster wie auf dem Foto (3 × 4, 4 × 3, 5 × 3 usw.), quer liegende Karten auch quer. Nicht erkannte Karten bleiben als freier Platz „?“ sichtbar und lassen sich dort eintragen.
- Helle Fliesen werden nicht mehr für eine riesige Karte gehalten; am Bildrand abgeschnittene Karten gelten als unsicher.
- 15 Tageslicht-Fotos: insgesamt 501 von 513 Karten auf 42 Fotos richtig, das Raster stimmt bei allen.

## 0.7 (14.10.2026)

- Schräg fotografierte Karten werden gefunden.
- Schatten auf den Karten werden vor der Auswertung herausgerechnet.
- 12 weitere Kunstlicht-Fotos: insgesamt 322 von 333 Karten auf 27 Fotos richtig.

## 0.6 (14.10.2026)

- Sets werden durchsichtig grün statt gelb markiert.
- Welle, Oval und Raute, Farben und feine Streifen sind den echten Karten nachgezeichnet.
- Erkennung an 15 echten Fotos (Kunstlicht) eingestellt: 182 von 189 Karten richtig, 10 der 15 Fotos fehlerfrei (vorher eines).

## 0.5 (14.10.2026)

Erste Version.

- Blazor WebAssembly mit .NET 10 (LTS). Die Kartenerkennung ist klassische Bildverarbeitung in C# und läuft komplett im Browser, ohne Server und ohne KI.
- Foto aufnehmen oder Bild wählen: Die App nummeriert die erkannten Karten, listet alle Sets auf und markiert sie im Foto. Gibt es kein Set, zeigt sie „Kein Set: 3 Karten nachlegen“.
- Karten lassen sich antippen und korrigieren oder ganz von Hand eingeben. Zwei Beispielbilder zum Ausprobieren.
- Läuft offline und lässt sich auf den Startbildschirm legen (PWA).
- Erkennung nur an künstlich erzeugten Kartenfotos eingestellt.

Im Zip dieser Version liegen die Dateien noch im Ordner `wwwroot`.
