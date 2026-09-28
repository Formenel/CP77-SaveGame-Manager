namespace Cp77SaveManager.Core.Localization;

/// <summary>
/// The master key list, in German. Two jobs:
///  1) ultimate fallback stage for Translator (see there) so the app always
///     has SOME text for every key, even with langs\ missing/corrupted;
///  2) the reference key set langs\de-DE.json is generated from when it
///     doesn't exist yet (see LocalizationService.EnsureDeDeFileExists),
///     and the reference OTHER language files' completion-% is measured
///     against once de-DE.json does exist.
///
/// Adding a new translatable string: add the key here first (this is what
/// "the app knows this key" means), then reference it via Translator.Get
/// from the UI code. langs\de-DE.json only needs to be deleted for the app
/// to regenerate it with the new key on next start; other language files
/// simply show a lower completion-% until a human adds the new key to them.
/// </summary>
public static class LocalizationDefaults
{
    public static IReadOnlyDictionary<string, string> Strings { get; } = new Dictionary<string, string>
    {
        // Window / toolbar
        ["BTN_RELOAD"] = "Neu laden",
        ["BTN_SETTINGS"] = "Einstellungen...",
        ["BTN_CLEANUP_ALL"] = "Ausräumen (alle Charaktere)...",
        ["STATUS_READY"] = "Bereit.",
        ["STATUS_SUMMARY"] = "{0} live, {1} im Storage, {2} Charakter(e). Save-Dir: {3} | Storage-Dir: {4}",

        // Top menu bar
        ["MENU_TOOLS"] = "Extras",
        ["MENU_LANGUAGE"] = "Sprache",

        // Tree context menu
        ["MENU_RENAME_ITEM"] = "Nickname ändern...",
        ["MENU_CLEANUP_CHAR_ITEM"] = "Ausräumen für diesen Charakter...",
        ["MENU_RETIRE_CHAR_ITEM"] = "Charakter in Rente schicken...",

        // Tree labels
        ["TREE_UNKNOWN_CHARACTER"] = "Unbekannt (keine/defekte Metadaten)",
        ["TREE_LIVE_COUNT"] = "Live ({0})",
        ["TREE_STORAGE_COUNT"] = "Storage ({0})",

        // List columns
        ["COL_TYPE"] = "Typ",
        ["COL_NAME"] = "Name",
        ["COL_LEVEL"] = "Level",
        ["COL_PLAYTIME"] = "Spielzeit",
        ["COL_TIMESTAMP"] = "Zeitpunkt",
        ["COL_QUEST"] = "Quest",
        ["COL_SIZE"] = "Größe",

        // List context menu
        ["MENU_STORE_ITEM"] = "Storen (ins Storage-Dir verschieben)",
        ["MENU_RESTORE_ITEM"] = "Restoren (zurück ins Save-Dir)",
        ["MENU_DELETE_ITEM"] = "Löschen...",
        ["MENU_SELECT_ALL"] = "Alles auswählen",
        ["MENU_SELECT_ALL_SHORTCUT"] = "Strg+A",

        // Preview pane
        ["PREVIEW_MULTI_SELECTED"] = "{0} Saves ausgewählt\r\nGesamtgröße: {1}",
        ["PREVIEW_FOLDER"] = "Ordner: {0}",
        ["PREVIEW_NO_METADATA"] = "Keine Metadaten lesbar (defekt oder fehlend).",
        ["PREVIEW_LIFEPATH"] = "LifePath: {0}",
        ["PREVIEW_GENDER"] = "Geschlecht: {0}",
        ["PREVIEW_LEVEL"] = "Level: {0}",
        ["PREVIEW_STREETCRED"] = "Street Cred: {0}",
        ["PREVIEW_PLAYTIME"] = "Spielzeit: {0}",
        ["PREVIEW_TIMESTAMP"] = "Zeitpunkt: {0}",
        ["PREVIEW_DIFFICULTY"] = "Schwierigkeit: {0}",
        ["PREVIEW_QUEST"] = "Quest: {0}",
        ["PREVIEW_LOCATION"] = "Ort: {0}",
        ["PREVIEW_CHECKPOINT"] = "Checkpoint: {0}",
        ["PREVIEW_MODDED"] = "Modded: {0}",
        ["PREVIEW_DLCS"] = "DLCs: {0}",
        ["PREVIEW_DLCS_NONE"] = "-",
        ["PREVIEW_VERSIONS"] = "Save-Version: {0} / Game-Version: {1} ({2})",
        ["PREVIEW_PLAYTHROUGH_ID"] = "PlaythroughID: {0}",
        ["PREVIEW_SIZE"] = "Größe: {0}",
        ["PREVIEW_OLD_FILES_WARNING"] = "⚠ Alte Dateien (*.old): {0}",

        // Shared result-message building blocks
        ["RESULT_FAILURES_HEADER"] = "Fehlgeschlagen:",

        // Rename
        ["DLG_RENAME_TITLE"] = "Nickname ändern",
        ["RENAME_HINT"] = "Nickname (leer = Auto-Label verwenden):",

        // Retire
        ["DLG_RETIRE_TITLE"] = "Charakter in Rente schicken",
        ["RETIRE_INFO"] = "\"{0}\" hat {1} Save(s) live und {2} im Storage.\r\nWas soll passieren?",
        ["RETIRE_BTN_STORE_ALL"] = "Alle live Saves einlagern (Storage)",
        ["RETIRE_BTN_DELETE_ALL"] = "ALLES löschen (live + Storage) - unwiderruflich",
        ["RETIRE_CONFIRM_DELETE_ALL"] = "Wirklich ALLE {0} Save(s) von \"{1}\" endgültig löschen?\r\nDas kann nicht rückgängig gemacht werden.",
        ["DLG_RETIRE_CONFIRM_TITLE"] = "Endgültig löschen?",
        ["DLG_RETIRE_RESULT_TITLE"] = "Charakter in Rente schicken - Ergebnis",
        ["RETIRE_VERB_STORED"] = "eingelagert",
        ["RETIRE_VERB_DELETED"] = "gelöscht",
        ["RESULT_COUNT_VERB"] = "{0} von {1} Save(s) {2}.",

        // Store / Restore / Delete (list actions)
        ["DLG_STORE_RESULT_TITLE"] = "Storen - Ergebnis",
        ["RESULT_STORE_SUCCESS"] = "{0} von {1} Save(s) erfolgreich gestoret.",
        ["DLG_RESTORE_RESULT_TITLE"] = "Restoren - Ergebnis",
        ["RESULT_RESTORE_SUCCESS"] = "{0} von {1} Save(s) erfolgreich restoret.",
        ["RESULT_RESTORE_RENAMED_HEADER"] = "Im Save-Dir bereits belegt, daher umbenannt:",
        ["DLG_DELETE_CONFIRM_TITLE"] = "Löschen?",
        ["CONFIRM_DELETE_SINGLE"] = "\"{0}\" endgültig löschen? Das kann nicht rückgängig gemacht werden.",
        ["CONFIRM_DELETE_MULTI"] = "{0} Saves endgültig löschen? Das kann nicht rückgängig gemacht werden.\r\n\r\n{1}",
        ["DLG_DELETE_RESULT_TITLE"] = "Löschen - Ergebnis",
        ["RESULT_DELETE_SUCCESS"] = "{0} von {1} Save(s) gelöscht.",

        // Cleanup
        ["DLG_CLEANUP_PREVIEW_TITLE"] = "Ausräumen - Vorschau",
        ["CLEANUP_NOTHING_TO_DO"] = "Nichts zu tun - alle Saves liegen bereits innerhalb des Limits.",
        ["CLEANUP_PREVIEW_INFO"] = "{0} Save(s) würden ins Storage-Dir verschoben, {1} bleiben live. Häkchen entfernen, um einzelne Saves davon auszunehmen.",
        ["COL_REASON"] = "Grund",
        ["BTN_MOVE"] = "Verschieben",
        ["BTN_CANCEL"] = "Abbrechen",
        ["DLG_CLEANUP_RESULT_TITLE"] = "Ausräumen - Ergebnis",
        ["RESULT_CLEANUP_SUCCESS"] = "{0} von {1} Save(s) erfolgreich ins Storage-Dir verschoben.",

        // Settings
        ["DLG_SETTINGS_TITLE"] = "Einstellungen",
        ["SETTINGS_SAVEDIR_LABEL"] = "Save-Dir (Live):",
        ["SETTINGS_STORAGEDIR_LABEL"] = "Storage-Dir:",
        ["SETTINGS_BROWSE"] = "...",
        ["SETTINGS_CONFIG_HINT"] = "Konfig-Datei (nicht änderbar): {0}",
        ["SETTINGS_CLEANUP_LABEL"] = "Ausräumen: welche Save-Typen zählen mit?",
        ["SETTINGS_KEEP_LABEL"] = "Insgesamt behalten (gemischt gezählt):",
        ["BTN_SAVE"] = "Speichern",

        // Generic OK button (nickname/settings dialogs)
        ["BTN_OK"] = "OK",
        ["BTN_YES"] = "Ja",
        ["BTN_NO"] = "Nein",

        // 0.3.0: menu bar (Datei / Saves / Charakter / Extras / ?), toolbar, About
        ["MENU_FILE"] = "Datei",
        ["MENU_EXIT"] = "Beenden",
        ["MENU_SAVES"] = "Saves",
        ["MENU_CHARACTER"] = "Charakter",
        ["MENU_HELP"] = "?",
        ["MENU_ABOUT"] = "Info...",
        ["DLG_ABOUT_TITLE"] = "Info",
        ["ABOUT_DESCRIPTION"] = "Verwaltet Cyberpunk-2077-Spielstände nach Charakter: ein-/auslagern, ausräumen, archivieren.",
        ["BTN_STORE"] = "Storen",
        ["BTN_RESTORE"] = "Restoren",
        ["SHORTCUT_RELOAD"] = "F5",
        ["SHORTCUT_RENAME"] = "F2",

        // 0.3.0: texts produced in Core (labels, cleanup reason, errors) + folder validation
        ["LABEL_UNKNOWN"] = "Unbekannt",
        ["LABEL_LEVEL"] = "Lvl {0}",
        ["CLEANUP_REASON"] = "älter als die {0} neuesten von [{1}] (gemischt gezählt)",
        ["ERR_SOURCE_MISSING"] = "Quellordner existiert nicht: {0}",
        ["ERR_DEST_EXISTS"] = "Zielordner existiert bereits: {0}",
        ["ERR_NOT_FLAT_SAVE"] = "\"{0}\" enthält Unterordner oder Verknüpfungen und wird aus Sicherheitsgründen nicht verschoben.",
        ["ERR_DEST_INSIDE_SOURCE"] = "Ziel liegt innerhalb des Quellordners: {0}",
        ["ERR_VERIFY_FAILED"] = "Verifikation fehlgeschlagen für Datei: {0}",
        ["ERR_OUTSIDE_ROOTS"] = "Sicherheitsabbruch: Pfad liegt außerhalb der verwalteten Ordner (oder hinter einer Verknüpfung): {0}",
        ["ERR_DIR_EMPTY"] = "Save-Dir und Storage-Dir dürfen nicht leer sein.",
        ["ERR_DIR_NOT_ABSOLUTE"] = "Save-Dir und Storage-Dir müssen vollständige Pfade sein (z.B. D:\\Saves).",
        ["ERR_DIR_DRIVE_ROOT"] = "Ein Laufwerks-Stammverzeichnis (z.B. D:\\) ist nicht erlaubt.",
        ["ERR_DIR_SAME"] = "Save-Dir und Storage-Dir dürfen nicht identisch sein.",
        ["ERR_DIR_NESTED"] = "Save-Dir und Storage-Dir dürfen nicht ineinander liegen.",
        ["DLG_INVALID_DIRS_TITLE"] = "Ungültige Ordner",
        ["STATUS_INVALID_DIRS"] = "Ordner-Konfiguration ungültig: {0} Bitte unter \"Einstellungen...\" korrigieren.",
    };
}
