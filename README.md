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
UAC-Prompt nötig - die exe schreibt nie neben sich selbst, Config liegt unter
`%APPDATA%\cp77sgm\config.json`.

## Konfiguration

Über "Einstellungen..." in der App änderbar:

- Save-Dir (Standard: `%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077`)
- Storage-Dir (Standard: `%USERPROFILE%\Saved Games\CD Projekt Red\CP77SGM-storage`)
- Ausräumen-Regel: welche Save-Typen mitzählen, wie viele insgesamt behalten
  werden (Standard: AutoSave + ManualSave, 15 insgesamt)

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
