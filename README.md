# CP77 Save Manager

Windows-Tool zum Verwalten von Cyberpunk-2077-Spielständen: Anzeige nach
Charakter (Live + Storage) und automatisches Ausräumen alter Saves ins
Storage-Dir.

## Projektstruktur

- `src/Cp77SaveManager.Core` - reine Logik, keine UI, kein Windows-spezifischer
  Code: Scanner, Metadata-Parsing, Cleanup-Planung, sicheres Verschieben
  (Copy → Verify → Delete). Läuft und testet plattformneutral.
- `src/Cp77SaveManager.Core.Tests` - Test-Harness (kein xUnit, siehe unten
  warum) mit 37 Checks gegen Fixture-Daten, die exakt das Schema von echten
  `metadata.9.json`-Dateien nachbilden.
- `src/Cp77SaveManager.App` - die eigentliche `cp77sgm.exe` (WinForms,
  `net8.0-windows`).

## Wichtig: Build-Verifikation

Core + Tests wurden in dieser Sandbox tatsächlich gebaut und ausgeführt
(`dotnet build`, `dotnet run`) - alle 37 Checks sind grün. Der Code in
`Cp77SaveManager.App` (WinForms) konnte hier **nicht** kompiliert werden: das
Windows-Desktop-SDK (`Microsoft.NET.Sdk.WindowsDesktop`) ist auf Linux gar
nicht installierbar, und der Zugriff auf nuget.org ist in dieser Sandbox von
der Org-Policy blockiert, sodass auch die Referenzassemblies dafür nicht
nachgeladen werden konnten. Der WinForms-Code wurde entsprechend sorgfältig
von Hand geschrieben und durchgesehen, ist aber **noch nicht
compilerverifiziert**. Bitte als erstes bei dir lokal bauen:

```powershell
cd Cp77SaveManager
dotnet build
```

Kompilierfehler bei `Cp77SaveManager.App` bitte zurückmelden - das ist der
einzige Teil, der noch echten Build-Beweis braucht.

## Warum kein xUnit in den Tests?

Aus demselben Grund (kein NuGet-Zugriff in der Sandbox): das Test-"Framework"
ist eine ca. 200-Zeilen-Handrolled-Harness (`Check(name, condition)` +
Exit-Code). Funktional äquivalent für diesen Zweck, aber wenn du lieber
xUnit/NUnit hättest, ist das Umschreiben trivial, sobald du lokal NuGet-Zugriff
hast.

## Build & Publish (auf deiner Windows-Maschine)

```powershell
# Debug-Build zum Testen
dotnet build

# Fertige Single-File-exe (self-contained, keine .NET-Runtime-Installation nötig)
dotnet publish src/Cp77SaveManager.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Die exe liegt danach unter `publish\cp77sgm.exe`. Sie kann irgendwo liegen
(auch außerhalb von Program Files) - es wird nie neben die exe geschrieben,
Config liegt in `%APPDATA%\cp77sgm\config.json`. Kein UAC-Prompt nötig.

## Konfiguration (Defaults, alles außer dem Config-Pfad änderbar über
"Einstellungen..." in der App)

- Save-Dir: `%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077`
- Storage-Dir: `%USERPROFILE%\Saved Games\CD Projekt Red\CP77SGM-storage`
  (Geschwisterordner, wie bei dir bereits angelegt)
- Ausräumen-Regel: standardmäßig AutoSave+ManualSave, insgesamt 15 behalten
  (gemischt gezählt, nicht pro Typ)

## Changelog

**2026-09-27, zweite Runde (vor Step 2):**
- Icon (`app.ico`, original/generisch, kein CP77-Branding) eingebaut.
- "Charakter in Rente schicken" (Rechtsklick auf Charakter im Tree): Alle live Saves einlagern, ODER alles (live+Storage) endgültig löschen (mit Ja/Nein-Bestätigung).
- Einzelne Saves: Rechtsklick in der Liste → "Storen" (nur bei Live-Saves) oder "Löschen..." (mit Bestätigung), für Live und Storage.
- Sicherheitsnetz: Löschen geht nur innerhalb der konfigurierten Save-/Storage-Dirs (`SaveActionService.IsUnderAnyRoot`) - auch bei zukünftigen Bugs kann nichts außerhalb gelöscht werden.
- Vorschau-Pane zeigt jetzt "⚠ Alte Dateien (*.old): N", wenn ein Save ein `sav.old` (oder mehrere) enthält.
- Save-Liste: Spalten klickbar sortierbar (auf/absteigend), Default weiterhin Zeitpunkt absteigend.
- Konfig speichert jetzt auch: Fenster-Position/-Größe/-Maximiert-Status (multi-monitor-fähig über normale virtuelle Desktop-Koordinaten, `RestoreBounds` beim Speichern), Spaltenbreiten, aktive Sortierung.
- Kein Zip fürs Storage-Dir (bewusste Entscheidung - Kompressionstest an echtem `sav.dat` ergab nur ~19% Ersparnis, CP77 komprimiert intern schon selbst).

## Git

`.gitignore` trackt bewusst nur `src/`, `publish/` und die nötigen Root-Dateien
(README, .sln, build.bat, .gitignore/.gitattributes selbst) - alles andere im
Root (Chat-Anhänge, Test-Bilder etc.) ist ausgeschlossen. `bin/`/`obj/` unter
`src/` sind ignoriert (immer neu generierbar).

**Achtung Repo-Größe:** `publish/cp77sgm.exe` ist ein self-contained
Single-File-Build und liegt bei ~150 MB (dazu ein paar MB `.pdb`-Dateien). Wenn
`publish/` mit ins Git-Repo soll (so wie hier angelegt), wächst das Repo bei
jedem neuen Publish um diesen Betrag, weil git binäre Diffs nicht komprimiert
speichert - nach ein paar Publishes können das schnell mehrere hundert MB im
`.git`-Verzeichnis sein. Falls das stört: `publish/` stattdessen ebenfalls
ignorieren (exe nur lokal/als Release-Artefakt), oder Git LFS für `*.exe`
einrichten.

`.gitattributes` normalisiert Textdateien auf CRLF (reines Windows-Projekt) und
markiert exe/dll/pdb/ico/png/jpg explizit als binär.

## Bekannte Grenzen / nächste Schritte (siehe auch Projekt-Plan)

- `sav.dat` wird nicht geparst (proprietäres Binärformat, keine verlässliche
  öffentliche Doku) - nur `metadata.9.json` wird gelesen.
- Restore aus dem Storage-Dir zurück ins Live-Save-Dir ist noch nicht gebaut
  (Step 2, laut Werner).
- Kein Icon, keine Signierung - SmartScreen wird bei der unsignierten exe
  wahrscheinlich warnen.
