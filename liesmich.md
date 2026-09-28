# CP77 Save Manager

Windows-Tool zum Verwalten von Cyberpunk-2077-Spielständen: Anzeige nach
Charakter (Live + Storage), Ein-/Auslagern einzelner oder mehrerer Saves,
automatisches Ausräumen alter Saves, Charakter komplett archivieren oder
löschen. Kein Savegame-Editor - es werden keine Werte in den Saves verändert.

## Features

- Scannt das konfigurierte Save-Verzeichnis, gruppiert nach `playthroughID`
  (= Charakter), mit optionalem Nickname pro Charakter.
- Zeigt Live- und Storage-Saves getrennt, inkl. Vorschau (Screenshot +
  Metadaten) pro Save.
- Storen/Restoren einzelner oder mehrerer Saves (Mehrfachauswahl wie im
  Explorer üblich).
- Automatisches Ausräumen: behält konfigurierbar die letzten N Saves pro
  Charakter (wählbar, welche Save-Typen mitzählen), Rest wandert ins
  Storage-Dir.
- "Charakter in Rente schicken": alle Saves eines Charakters auf einmal
  einlagern oder endgültig löschen.
- Deutsch/Englisch umschaltbar zur Laufzeit (Menü Extras → Sprache); weitere
  Sprachen lassen sich durch eine zusätzliche JSON-Datei ergänzen, siehe unten.

## Voraussetzungen

Windows x64. Die exe ist self-contained (bringt ihre eigene .NET-Runtime mit),
es ist keine separate .NET-Installation nötig.

## Installation

Zip von Nexus/Releases entpacken, `cp77sgm.exe` starten. Kein Installer, kein
UAC-Prompt nötig. Config liegt unter `%APPDATA%\cp77sgm\config.json`;
`langs\de-DE.json` wird beim Start neben der exe angelegt, wenn der Ordner
beschreibbar ist - wenn nicht, läuft das Tool trotzdem (eingebaute Texte).

## Konfiguration

Über "Einstellungen..." in der App änderbar:

- Save-Dir (Standard: `%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077`)
- Storage-Dir (Standard: `%USERPROFILE%\Saved Games\CD Projekt Red\CP77SGM-storage`)
- Save-Dir und Storage-Dir müssen vollständige Pfade sein und dürfen weder
  identisch sein noch ineinander liegen (wird beim Speichern geprüft).
- Ausräumen-Regel: welche Save-Typen mitzählen, wie viele insgesamt behalten
  werden (Standard: QuickSave + ManualSave, 15 insgesamt)

## Projektstruktur

- `src/Cp77SaveManager.Core` - Logik ohne UI/Windows-Abhängigkeit: Scanner,
  Metadata-Parsing, Cleanup-Planung, sicheres Verschieben (Copy → Verify →
  Delete), Lokalisierung.
- `src/Cp77SaveManager.Core.Tests` - Testsuite (Hand-Harness statt
  xUnit/NUnit, siehe unten) gegen Fixture-Daten, die das Schema echter
  `metadata.9.json`-Dateien nachbilden.
- `src/Cp77SaveManager.App` - `cp77sgm.exe` (WinForms, `net8.0-windows`).

## Bauen

```
build.bat
```

baut, testet, published (self-contained, single-file, win-x64) und packt ein
Nexus-fertiges Zip unter `publish\`.
Verzeichnisse müssen vermutlich angepasst werden.
Für einen reinen Debug-Build ohne Publish reicht `dotnet build`.

Die Versionsnummer kommt einzig aus `<Version>` in
`src/Cp77SaveManager.App/Cp77SaveManager.App.csproj`.

## Sprachdateien

`langs\*.json`, eine Datei pro Sprache, Dateiname (ohne Endung) ist der
Sprach-Code (`de-DE`, `en-GB`, ...). Format:

```json
{
  "LANG": "Anzeigename im Menü",
  "STRINGS": { "KEY": "Wert", ... }
}
```

`de-DE.json` wird beim ersten Start automatisch erzeugt, falls sie fehlt, und
dient als Referenz: das Sprachmenü zeigt den Übersetzungsgrad jeder anderen
Datei relativ zu ihren Keys an (z.B. `English (en-GB, 100%)`). Ein fehlender
Key fällt einzeln auf Deutsch zurück, nie die ganze Datei.

Pull Requests mit weiteren Sprachdateien sind willkommen.

## Warum keine xUnit-Tests?

Die Testsuite ist eine kleine Hand-Harness (`Check(name, condition)` +
Exit-Code) statt xUnit/NUnit - funktional ausreichend für den aktuellen
Umfang, ließe sich aber bei Bedarf problemlos migrieren.

## Bekannte Grenzen

- `sav.dat` selbst wird nicht geparst (nur `metadata.9.json`) - kein
  Zugriff auf Inventar, Quests etc., nur die im Save-Menü sichtbaren Daten.

## Änderungen

### 0.3.0

Fixes aus einem Code-/Security-Review (Details: Projekt-Plan):

- `playthroughID` aus einem (manipulierten) Save kann keinen Pfad mehr
  außerhalb des Storage-Dirs erzeugen - ungültige IDs landen unter "Unbekannt".
- Save-Ordner mit Unterordnern oder Verknüpfungen werden nicht verschoben
  (vorher wären Unterordner verloren gegangen).
- Save-Dir/Storage-Dir werden validiert (leer, gleich, verschachtelt,
  Laufwerks-Root) - verschachtelt konnte vorher den ganzen Storage löschen.
- Junctions/Symlinks unterhalb der verwalteten Ordner werden ignoriert,
  Löschen hinter einer Verknüpfung wird verweigert.
- Ein fremder `<Name>.partial`-Ordner wird nicht mehr gelöscht.
- App startet auch, wenn der exe-Ordner schreibgeschützt ist; Deutsch bleibt
  immer im Sprachmenü; neue Keys werden in `de-DE.json` ergänzt.
- Kaputte Platzhalter in Sprachdateien führen nicht mehr zum Absturz.
- Restliche deutsche Texte aus dem Core (Fehler, Ausräumen-Grund, Labels)
  sind übersetzbar.
- Vorschaubild wird korrekt geladen (Stream-Lebensdauer).
- Nexus-Zip mit `/` statt `\` in den Pfaden.
- Ausräumen zählt standardmäßig QuickSave + ManualSave (statt AutoSave).
- Fenstertitel zeigt nur noch die Version (ohne Build-Kennung).
- `config.json` speichert Save-Typen als Namen statt Zahlen (alte Configs werden weiter gelesen).
- Rechtsklick auf "Unbekannt": Nickname/Ausräumen/Rente ausgegraut.
- Sanduhr-Cursor während Storen/Restoren/Löschen/Ausräumen.
- Löschen mehrerer Saves: Bestätigung mit scrollbarer Liste statt MessageBox.
- Save-Liste: Strg+A und "Alles auswählen" im Kontextmenü.
- Neue Menüleiste (Datei / Saves / Charakter / Extras / ?) mit allen Aktionen,
  Symbolleiste mit Neu laden / Storen / Restoren / Ausräumen, Kürzel F5 und F2.
- Info-Dialog mit Links zu Nexus Mods und GitHub.
- App-Icon auch in der Titelleiste.

### 0.2.0

- Deutsch/Englisch zur Laufzeit umschaltbar (Extras → Sprache), weitere
  Sprachen per JSON-Datei in `langs\`.

### 0.1.0

- Erster Release: Saves nach Charakter, Storen/Restoren, Ausräumen,
  Charakter in Rente schicken.
