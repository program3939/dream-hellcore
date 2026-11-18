**[English version](README.md)**

# Dreamy Hellcore

Endloses 3D-Jump-and-Run von Traum bis Hölle. Jeder Raum heißt **Hellstage N** und wird deterministisch aus seiner
Nummer generiert. Es gibt kein Ende und keinen Tod: Wer stürzt, erscheint am letzten Checkpoint wieder.

**Leitidee:** Nichts wiederholt sich. Raumtyp, Mechaniken, Raumklang und Schreckmomente wechseln ständig. Es ist
dunkel; Licht gibt es nur gezielt (Kante des Pfades, Tore und Checkpoints, das eigene kleine Licht, Schienen).
Der Terror kommt aus Verunsicherung und Erwartungsbruch, nicht aus Gore.

## Starten

| System | Start |
|---|---|
| Windows 10/11 x64 | `builds\windows-x64\DreamyHellcore.exe` |
| Linux x64 | `builds/DreamyHellcore-linux-x64.tar.gz` entpacken, dann `./DreamyHellcore` |

Beide Builds sind self-contained. Linux braucht glibc, X11 (oder XWayland) und OpenGL 3.3. Kopfhörer empfohlen.

## Steuerung

| Taste | Aktion |
|---|---|
| W A S D / Pfeiltasten, Maus | Laufen, Umsehen |
| Leertaste / Shift / Strg (oder C) | Springen / Sprinten / Ducken |
| Esc | Pausenmenü: Resume, Skip this puzzle/trial (nur solange das Tor zu ist), Reset progress (mit Bestätigung), Quit game |
| H | in einer Rätselkammer: die nächste richtige Platte markieren |
| Alt+F4 / Fenster schließen | Beenden, Fortschritt wird gespeichert |
| F11 | Vollbild |
| Bild auf / ab, Pos1 / Ende | Hellstage ±1, ×10 / ÷10 |

Oben mittig steht die aktuelle Hellstage („Hellstage 12“); sonst gibt es keine Anzeige im Bild.

## Feature-Übersicht: was ab welcher Hellstage auftritt

Die Stimmung steigt schnell: `Mood = 1 − exp(−(N−1)/12)`. Hellstage 5 ≈ 0,28 · 10 ≈ 0,53 · 20 ≈ 0,79 · 30 ≈ 0,91.
Größe und Abstraktion der Räume wachsen dagegen langsam weiter (bis Hellstage 10000 und darüber).

### Mechaniken (10)

| Ab Hellstage | Mechanik |
|---|---|
| 1 | Höhe läuft frei: Räume enden höher oder tiefer, als sie beginnen (Treppen, Abstiege) |
| 4 | Bewegte Plattformen (seitlich pendelnd, tragen den Spieler mit) |
| 6 | Sprungfelder (schleudern hoch, nächste Plattform liegt bis 3 m höher) |
| 7 | Verschwindende Plattformen (blinken vor dem Verschwinden) |
| 8 | Weite Stürze auf große Landeplattformen (bis ca. 46 m) |
| 9 | Schienen / Achterbahn: Fahrt mit Schwung aus dem Gefälle, endet immer an einem Checkpoint |
| 10 | Bröckelnde Plattformen (zittern, fallen nach 1,1 s, kehren zurück) |
| 11 | Wind in Böen (Raumtyp Sturmraum) |
| 12 | Förderbänder (schieben vor oder zur Seite) |
| 13 | Geringe Schwerkraft (Raumtyp Schweberaum) |

### Raumtypen (16, nie zweimal hintereinander derselbe Sondertyp)

| Ab Hellstage | Raumtyp |
|---|---|
| 1 | Halle (Standard) |
| 2 | Säulenwald |
| 3 | Liminaler Flur: gleichmäßige Rahmen im festen Takt, einer davon stimmt nicht |
| 4 | Treppenhaus (stetig aufwärts) |
| 5 | Abstieg (stetig abwärts, mehr Stürze) |
| 6 | Balkenbrücke (lange, schmale Balken) |
| 7 | Trittsteine (viele winzige Plattformen) |
| 8 | Symmetrie: alles gespiegelt, bis auf ein Teil, das fehlt |
| 9 | Leere (kein Dekor, kein Raumklang) · Achterbahn-Raum (mehrere Schienen) |
| 10 | Kriechgang (viele Duck-Balken) |
| 11 | Sturmraum |
| 13 | Schweberaum · Déjà-vu-Raum: der vorige Raum noch einmal, mit einer Gestalt, die vorher nicht da war |
| 14 | Mechanik-Raum (fast nur bewegliche Plattformen) |
| 15 | Schiefer Raum (der ganze Raum hängt schräg) |
| 17 | Enge (die Wände wachsen bis an den Pfad) |

### Terror-Effekte

Ein **Regisseur** steuert Wellen aus Anspannung und Entlastung. Checkpoints lösen die Spannung. Er wählt nie eines
der letzten beiden Ereignisse erneut, setzt vor starken Ereignissen Stille und lässt in jedem fünften Fall nach der
Stille gar nichts geschehen.

| Ab Hellstage | Ereignisse des Regisseurs (25) |
|---|---|
| 1 | Musik-Ereignis (Spieluhr und Akkord, sonst gibt es keine Musik) |
| 5 | Plötzliche Stille |
| 6 | Dumpfer Schlag hinter dir · Beben · falscher Checkpoint-Klang · ferner Chor · der Raum verschiebt sich |
| 7 | Schwindel-Zoom · ein Schatten zieht durchs Bild |
| 8 | Der Raum kippt · Herzrasen |
| 9 | Flüstern an einem Ohr · Schwerkraft-Ruck |
| 10 | Eigenes Licht fällt aus · Blitz · alle Augen öffnen sich · Atem hinter dir · rennende Schritte |
| 11 | Nichtlinearer Schrei mit Bildrissen (jedes Mal anders) |
| 12 | Negativ-Flackern · Zeitstottern · der Verfolger steht plötzlich viel näher |
| 14 | Alle Lichter aus |
| 16 | Der Weg hinter dir ist verschwunden |
| 21 | Steuerung kurz gespiegelt |

| Ab Hellstage | Dauerhafte Systeme |
|---|---|
| 4 | Dekor verschiebt sich, während du wegsiehst · der Verfolger steht voraus im Nebel |
| 6 | Augen in den Wänden (nur im Licht sichtbar, folgen dir) |
| 8 | Gestalten im Augenwinkel · falsche Checkpoints (funktionieren, aber ihr Licht stirbt) |
| 9 | Der Verfolger kommt näher, wenn du wegsiehst · Lichter hinter dir erlöschen |
| 11 | Blitze zeigen eine stumme Menge · Schritte gehen weiter, wenn du stehen bleibst |
| 12 | Das eigene Licht fällt von selbst aus |

### Escape-Räume (Hellstage 3, 7, 11, 15, … – jede vierte)

Am Ende des Raums liegt eine Rätselkammer (16 × 16 m) vor einem verschlossenen Tor. Es gibt keinen Text: Jedes
Rätsel erklärt sich über Platten, Stifte, Lichter und Töne. Man löst es, indem man Platten betritt. Fehler töten
nicht, aber der Raum antwortet: Dunkelheit, ein Schrei, der Verfolger rückt näher, im falschen Himmel verrutscht
die Maske. Die ersten zehn Escape-Räume zeigen jedes Rätsel einmal; danach wechseln sie zufällig (nie zweimal
dasselbe hintereinander) und werden größer.

| Erstmals in Hellstage | Rätsel | Aufgabe | Schwerer |
|---|---|---|---|
| 3 | Lichter aus | Jede Platte schaltet sich selbst und ihre vier Nachbarn um. Alle müssen leuchten. | 4 × 4 ab 43 |
| 7 | Tonleiter | Jede Platte klingt. Vom tiefsten zum höchsten Ton betreten. | mehr Platten, engere Tonabstände ab 25 und 50 |
| 11 | Der Falsche | Jede Säule (Ringe, Kappe) hat einen Zwilling – bis auf eine. Vor diese treten. | ab 25 Paare statt lauter gleicher Säulen |
| 15 | Summe | Jede Platte hat einen Wert (Stifte). Die leuchtenden Werte müssen genau die Zahl der Stifte vor dem Tor ergeben. | 6 Platten ab 30 |
| 19 | Spiegelbild | Links ein festes Lichtmuster, in der Mitte eine leuchtende Achse. Rechts das Spiegelbild legen. | 4 × 4 ab 40 |
| 23 | Jeder Stein einmal | Alle Platten genau einmal betreten, immer nur auf eine direkt benachbarte. | Lücken ab 20, 4 × 4 ab 45 |
| 27 | Zählschloss | Im Raum stehen drei Arten großer Figuren. Jedes Zählwerk auf die Anzahl seiner Art stellen (die kleine Figur davor zeigt, welche Art gemeint ist). | – |
| 31 | Zähler | Eine Platte zählt um a hoch, die andere um b herunter. Den markierten Stand genau treffen; wer die Skala verlässt, beginnt bei null. | größere Skala ab 30 |
| 35 | Geheimcode | Symbolplatten nacheinander betreten. Nach jedem Versuch zeigt das Tor, wie viele Stellen stimmen. | 4 Symbole ab 35 |
| 39 | Gekoppelte Räder | Jede Platte dreht zwei Zählwerke zugleich weiter. Alle Zählwerke voll machen. | 4 Zählwerke ab 35 |

Jedes Rätsel wird so erzeugt, dass es garantiert lösbar ist (z. B. „Lichter aus“ und „Gekoppelte Räder“ durch
rückwärts ausgeführte Züge, „Jeder Stein einmal“ mit Wegsuche, „Zähler“ mit Erreichbarkeitsprüfung).

**Hinweise und Überspringen:** Betritt man eine Rätselkammer, stehen unten im Bild Name und Regel des Rätsels
(englisch). Mit H, nach 75 Sekunden oder nach 4 Fehlversuchen markiert ein leuchtender Ring die nächste richtige
Platte; ein Löser berechnet sie aus dem aktuellen Stand. Über Esc lässt sich jedes Rätsel und jede Prüfung
überspringen (das Tor öffnet sich).

**Prüfungen** (Reaktion statt Rätsel, Hellstage 9, 17, 25, 33, … – jede achte): Stillstehen, Flucht und Nicht
hinsehen wechseln sich ab.

Jeder Escape-Raum und jede Prüfung erscheint im Design einer Stufe: **Falscher Himmel**, Unbehagen, Grauen, Terror
oder Leere. Meist ist es die aktuelle Stufe; in etwa jedem vierten Fall kehrt der falsche Himmel zurück, auch tief
in der Hölle. Der Terror (Regisseur, Verfolger) bleibt dabei immer auf der echten Stufe.

**Falscher Himmel:** eine elfenbeinfarbene Halle in goldenem Licht, ein Wolkenmeer statt des Abgrunds, ein reiner
Dur-Chor. Links und rechts stehen in zu perfekten Reihen weiße, gesichtslose Gestalten mit Heiligenschein, dazu
eine, die viel zu groß ist. Im Chor sackt immer wieder eine Stimme einen Viertelton ab. Bei jedem Blitz und jedem
Fehler verrutscht für einen Moment die Maske: Die Halle ist schwarz und rot, die Gestalten sind schwarz.

### Klang

Keine Dauermusik. Jeder Raum hat einen eigenen Raumklang aus zwei von sechs Schichten (Wind, Brummen, Tropfen,
ferne Maschine, Knistern, hoher Ton) mit eigenen Tonhöhen und Takten. Dazu Herzschlag und Bass abhängig von der
Spannung sowie eigene Klänge für Schiene, Sprungfeld, Brechen, Sturz und Landung.

## Discord-Aktivität

Läuft Discord, meldet sich das Spiel dort als Aktivität: „Spielt Dreamy Hellcore“, darunter Hellstage und Raumtyp,
mit Logo und Spielzeit. Dafür braucht es einmalig eine eigene Discord-Anwendungs-ID; die Anleitung (etwa zwei
Minuten) steht in `discord_app_id.txt` neben der Programmdatei. Solange dort keine ID steht, wird nichts angezeigt.
Ohne Logo geht es auch ganz ohne ID: das laufende Spiel in den Discord-Einstellungen bei den registrierten Spielen
hinzufügen.

## Startparameter

| Parameter | Wirkung |
|---|---|
| `--stage N` | direkt in Hellstage N starten |
| `--fullscreen`, `--scale X` | Vollbild, interne Auflösung |
| `--screenshot DATEI.png` | Bild speichern und beenden (optional `--yaw --pitch --fly Z --flash`) |
| `--selftest DATEI.txt` | simuliert jeden generierten Sprung; listet Raumtyp, dynamische Plattformen, Schienen |
| `--audiotest ORDNER` | vier Klangproben (WAV) und Pegelmessung |
| `--bot SEKUNDEN DATEI.txt` | ein Bot spielt die angegebene Spielzeit |

## Bauen

Voraussetzung: .NET SDK 8 (Raylib-cs 7.0.1 kommt per NuGet).

```
cd src
dotnet publish -c Release -r win-x64   --self-contained true -o ../builds/windows-x64
dotnet publish -c Release -r linux-x64 --self-contained true -o ../builds/linux-x64
python ../tools/pack_linux.py
```

## Projektstruktur

| Pfad | Inhalt |
|---|---|
| `src/Program.cs` | `Box`, `Rng`, `M`, `Room` (Farbbogen, Mesh-Aufbau) |
| `src/Generator.cs` | Raumgenerator: Pfad, Mechaniken, Schienen, Raumtypen, Dekor |
| `src/Game.cs` | Spielschleife, Physik, Renderer, Tests |
| `src/Game2.cs` | dynamische Plattformen, Schienenfahrt, Regisseur, Bot |
| `src/Shaders.cs` | alle GLSL-Shader |
| `src/Puzzles.cs`, `src/Game4.cs` | die zehn Rätsel der Escape-Räume (Aufbau, Logik) |
| `src/Game3.cs`, `src/Audio3.cs` | Escape-Räume und Prüfungen, falscher Himmel (Logik, Klang) |
| `src/Game5.cs` | Hellstage-Anzeige, Hinweistext und Pausenmenü |
| `src/Game6.cs` | Hinweise: Texte und Löser für alle zehn Rätsel |
| `src/Discord.cs`, `src/discord_app_id.txt` | Discord-Aktivität |
| `src/Audio.cs`, `src/Audio2.cs` | prozedurale Klangwelt |
| `konzeptbilder/` | Konzeptbilder und das Skript, das sie rendert |
| `docs/images/` | Bilder aus dem Spiel und Logo |
| `tools/pack_linux.py` | packt den Linux-Build |

## Teststand

Unter Windows 11 geprüft: Selbsttest über 75 Hellstages (1198 Sprünge, 1 Fehler, siehe unten); Bot-Läufe ohne
Sturz in Hellstage 1→4, 9→10, 12 und 30→32 (mit Schienenfahrt); Klangpegel ohne Übersteuerung; alle Shader
kompilieren. Der Linux-Build ist nur cross-kompiliert und wurde nie gestartet.

## Offene Fehler und Optimierungen

- Escape-Räume: Der Selbsttest löst alle 17 geprüften Rätselräume (alle zehn Arten) allein durch Befolgen der
  Hinweise. Von Hand angespielt ist nur Hellstage 3; die Lesbarkeit der übrigen Kammern ist nicht geprüft.
- Falscher Himmel: Die Rätselkammer ist nur an einem Testbild auf Lesbarkeit geprüft und wirkt dort noch sehr hell.
- Discord-Aktivität ungetestet (es gibt noch keine Anwendungs-ID).
- Die alten Escape-Arten „Siegel“ und „Reihenfolge“ werden nicht mehr erzeugt; ihr Code ist noch vorhanden.
- Hellstage 15 und 19 (alte Zählung): Der Bot stürzte vor dem Altar an beweglichen Plattformen (wie Hellstage 60).
- Hellstage 10000: der letzte Sprung in den nächsten Raum (bei −230 m Höhe) scheitert im Selbsttest. Ursache unbekannt.
- Hellstage 60 (Sturmraum mit Schiene): der Test-Bot stürzt wiederholt bei etwa 20 bis 25 m. Ob ein Mensch dort
  durchkommt, ist ungeprüft.
- Die 25 Regisseur-Ereignisse, die Mechaniken außer Schiene und bewegten Plattformen sowie der gesamte Ton wurden
  nicht von Hand ausprobiert, nur kompiliert und im Bot-Lauf mitgelaufen.
- Déjà-vu-Räume treten häufiger auf als die geplanten 6 % (8 von 75 geprüften Hellstages).
- Bewegte Plattformen pendeln nur seitlich; rotierende Plattformen und echte Schwerkraftumkehr fehlen.
- Die Konzeptbilder zeigen teils noch alte Elemente (leuchtende Würfel, Kasten-Figuren).
- Alte, unbenutzte Methoden `BuildWorldOld` und `BotOld` in `Game.cs` können gelöscht werden.
- Leistung in Vollbild/4K ungemessen; Maus- und Spielgefühl ungeprüft.
