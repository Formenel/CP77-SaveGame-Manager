# CP77 Save Manager

Windows tool for managing Cyberpunk 2077 save games: view saves per character
(live + storage), store/restore single or multiple saves, automatically clean
up old saves, archive or delete a whole character. Not a save editor - no
values inside the saves are ever changed.

German version: [liesmich.md](liesmich.md)

## Features

- Scans the configured save directory and groups saves by `playthroughID`
  (= character), with an optional nickname per character.
- Shows live and stored saves separately, with a preview (screenshot +
  metadata) per save.
- Store/restore single or multiple saves (multi-select like in Explorer).
- Automatic cleanup: keeps the newest N saves per character (configurable
  which save types count), the rest is moved to the storage dir.
- "Retire character": store or permanently delete all saves of a character
  at once.
- German/English switchable at runtime (menu Extras → Sprache / Tools →
  Language); more languages can be added with an extra JSON file, see below.

## Requirements

Windows x64. The exe is self-contained (ships its own .NET runtime), no
separate .NET installation needed.

## Installation

Unzip the archive from Nexus/Releases, start `cp77sgm.exe`. No installer, no
UAC prompt. Config lives in `%APPDATA%\cp77sgm\config.json`; `langs\de-DE.json`
is created next to the exe on startup if that folder is writable - if not, the
tool still runs (built-in texts).

## Configuration

Via "Settings..." in the app:

- Save dir (default: `%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077`)
- Storage dir (default: `%USERPROFILE%\Saved Games\CD Projekt Red\CP77SGM-storage`)
- Both must be full paths and must not be the same folder or inside each other.
- Cleanup rule: which save types count, how many to keep in total
  (default: QuickSave + ManualSave, 15 in total)

## Project structure

- `src/Cp77SaveManager.Core` - logic without UI/Windows dependency: scanner,
  metadata parsing, cleanup planning, safe moving (copy → verify → delete),
  localization.
- `src/Cp77SaveManager.Core.Tests` - test suite (hand-rolled harness instead
  of xUnit/NUnit) against fixture data mirroring real `metadata.9.json` files.
- `src/Cp77SaveManager.App` - `cp77sgm.exe` (WinForms, `net8.0-windows`).

## Building

```
build.bat
```

builds, tests, publishes (self-contained, single-file, win-x64) and packs a
Nexus-ready zip into `publish\`. The paths inside `build.bat` probably need
adjusting. For a plain debug build `dotnet build` is enough.

The version number comes only from `<Version>` in
`src/Cp77SaveManager.App/Cp77SaveManager.App.csproj`.

## Language files

`langs\*.json`, one file per language, file name (without extension) is the
language code (`de-DE`, `en-GB`, ...). Format:

```json
{
  "LANG": "Name shown in the menu",
  "STRINGS": { "KEY": "Value", ... }
}
```

`de-DE.json` is the reference: the language menu shows each other file's
completion relative to its keys (e.g. `English (en-GB, 100%)`). A missing key
falls back to German on its own, never the whole file.

Pull requests with more language files are welcome.

## Known limitations

- `sav.dat` itself is not parsed (only `metadata.9.json`) - no access to
  inventory, quests etc., only the data visible in the in-game save menu.

## Changelog

- 0.3.0 - minor issues resolved
- 0.2.0 - German/English UI, switchable at runtime; more languages via JSON files
- 0.1.0 - first release: saves per character, store/restore, cleanup, retire character
