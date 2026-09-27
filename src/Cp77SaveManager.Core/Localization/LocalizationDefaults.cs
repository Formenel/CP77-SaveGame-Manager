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
        ["APP_TITLE"] = "CP77 Save Manager ({0})",
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
    };
}
