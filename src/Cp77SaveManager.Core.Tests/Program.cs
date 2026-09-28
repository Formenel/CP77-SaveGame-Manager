using Cp77SaveManager.Core.Cleanup;
using Cp77SaveManager.Core.Configuration;
using Cp77SaveManager.Core.Localization;
using Cp77SaveManager.Core.Models;
using Cp77SaveManager.Core.Retirement;
using Cp77SaveManager.Core.Scanning;
using Cp77SaveManager.Core.Storage;
using System.Text.Json;

// Minimal hand-rolled test harness (no xunit - this sandbox has no network
// access to NuGet, so no test framework packages can be restored). Each
// Check() call is one assertion; the process exits non-zero if anything fails
// so this can be wired into a CI step later without extra tooling.

var failures = new List<string>();

void Check(string name, bool condition)
{
    if (condition)
    {
        Console.WriteLine($"PASS  {name}");
    }
    else
    {
        Console.WriteLine($"FAIL  {name}");
        failures.Add(name);
    }
}

// ---------------------------------------------------------------------
// 1. Folder name parsing
// ---------------------------------------------------------------------
{
    var (type, index) = SaveFolderNameParser.Parse("ManualSave-86");
    Check("parses ManualSave-86", type == SaveType.ManualSave && index == 86);

    (type, index) = SaveFolderNameParser.Parse("AutoSave-0");
    Check("parses AutoSave-0", type == SaveType.AutoSave && index == 0);

    (type, index) = SaveFolderNameParser.Parse("PointOfNoReturnSave-0");
    Check("parses PointOfNoReturnSave-0", type == SaveType.PointOfNoReturnSave && index == 0);

    (type, index) = SaveFolderNameParser.Parse("some-mod-folder");
    Check("unknown folder name does not crash", type == SaveType.Unknown && index == -1);
}

// ---------------------------------------------------------------------
// Fixture: build a fake save dir tree that mirrors Werner's real one.
// The JSON below is the verified real schema (field names/shape captured
// from an actual metadata.9.json), with only playthroughID/level/timestamp
// varied per fixture entry.
// ---------------------------------------------------------------------
string BuildMetadataJson(string playthroughId, string lifePath, string gender, double level, string timestamp, string name)
    => $$"""
    {
        "RootType": "saveMetadataContainer",
        "Data": {
            "metadata": {
                "gameDefinition": "",
                "activeQuests": "E5B7576313A99B86",
                "trackedQuestEntry": "quests/main_quest/prologue/q001_intro/premission0/02_follow_jackie",
                "trackedQuest": "LocKey#9466",
                "mainQuest": "LocKey#93496",
                "debugString": "q000_kid_rayfield_open",
                "locationName": "LocKey#10966",
                "playerPosition": { "X": -415.87, "Y": 417.79, "Z": 23.10 },
                "playTime": 2274.8661809,
                "playthroughTime": 2274.8661809,
                "lifePath": "{{lifePath}}",
                "bodyGender": "{{gender}}",
                "brainGender": "{{gender}}",
                "level": {{level}},
                "streetCred": 1.0,
                "playthroughID": "{{playthroughId}}",
                "pointOfNoReturnId": "",
                "buildPatch": "2.31",
                "difficulty": "Story",
                "saveVersion": 269,
                "gameVersion": 2310,
                "timestampString": "{{timestamp}}",
                "name": "{{name}}",
                "userName": "",
                "platform": "pc",
                "fileSize": 1772564,
                "isForced": false,
                "isCheckpoint": false,
                "isStoryMode": false,
                "isPointOfNoReturn": false,
                "isEndGameSave": false,
                "isModded": true,
                "additionalContentIds": ["EP1"]
            }
        }
    }
    """;

string tempRoot = Path.Combine(Path.GetTempPath(), "cp77sgm-test-" + Guid.NewGuid().ToString("N"));
string liveDir = Path.Combine(tempRoot, "live", "Cyberpunk 2077");
// Separate top-level temp root simulates a different drive for storage.
string storageDir = Path.Combine(tempRoot, "storage-drive", "CP77SGM-storage");
Directory.CreateDirectory(liveDir);
Directory.CreateDirectory(storageDir);

void WriteSaveFolder(string root, string folderName, string? metadataJson, int screenshotBytes, byte savDatFill = 0x42)
{
    var dir = Path.Combine(root, folderName);
    Directory.CreateDirectory(dir);
    if (metadataJson is not null)
    {
        File.WriteAllText(Path.Combine(dir, "metadata.9.json"), metadataJson);
    }
    File.WriteAllBytes(Path.Combine(dir, "sav.dat"), Enumerable.Repeat(savDatFill, 2048).ToArray());
    if (screenshotBytes > 0)
    {
        File.WriteAllBytes(Path.Combine(dir, "screenshot.png"), new byte[screenshotBytes]);
    }
}

const string ptidA = "6ab83a8fe6b8c7ff";
const string ptidB = "aaaa1111bbbb2222";

// Character A: 5 ManualSaves + 2 AutoSaves + 1 QuickSave, newest to oldest.
WriteSaveFolder(liveDir, "ManualSave-10", BuildMetadataJson(ptidA, "Streetkid", "Female", 12, "10:00:00, 27.09.2026", "ManualSave-10"), 152341);
WriteSaveFolder(liveDir, "ManualSave-9", BuildMetadataJson(ptidA, "Streetkid", "Female", 11, "09:00:00, 27.09.2026", "ManualSave-9"), 152341);
WriteSaveFolder(liveDir, "ManualSave-8", BuildMetadataJson(ptidA, "Streetkid", "Female", 10, "08:00:00, 27.09.2026", "ManualSave-8"), 152341);
WriteSaveFolder(liveDir, "ManualSave-7", BuildMetadataJson(ptidA, "Streetkid", "Female", 9, "07:00:00, 27.09.2026", "ManualSave-7"), 152341);
WriteSaveFolder(liveDir, "ManualSave-6", BuildMetadataJson(ptidA, "Streetkid", "Female", 8, "06:00:00, 27.09.2026", "ManualSave-6"), 152341);
WriteSaveFolder(liveDir, "AutoSave-5", BuildMetadataJson(ptidA, "Streetkid", "Female", 12, "10:30:00, 27.09.2026", "AutoSave-5"), 1004); // placeholder screenshot
WriteSaveFolder(liveDir, "AutoSave-4", BuildMetadataJson(ptidA, "Streetkid", "Female", 7, "05:00:00, 27.09.2026", "AutoSave-4"), 152341);
WriteSaveFolder(liveDir, "QuickSave-1", BuildMetadataJson(ptidA, "Streetkid", "Female", 12, "10:15:00, 27.09.2026", "QuickSave-1"), 152341);

// Character B: 1 save, different PTID.
WriteSaveFolder(liveDir, "ManualSave-20", BuildMetadataJson(ptidB, "Corpo", "Male", 30, "12:00:00, 27.09.2026", "ManualSave-20"), 152341);

// A save with corrupt/missing metadata -> must land in the Unknown bucket, not crash.
WriteSaveFolder(liveDir, "ManualSave-99", metadataJson: null, screenshotBytes: 0);

// ---------------------------------------------------------------------
// 2. Scanner + metadata parsing
// ---------------------------------------------------------------------
var scanner = new SaveScanner();
var liveEntries = scanner.ScanLive(liveDir);

Check("scanner finds all 10 live save folders", liveEntries.Count == 10);

var manual10 = liveEntries.Single(e => e.FolderName == "ManualSave-10");
Check("metadata parsed: playthroughID", manual10.Metadata?.PlaythroughId == ptidA);
Check("metadata parsed: lifePath", manual10.Metadata?.LifePath == "Streetkid");
Check("metadata parsed: level", manual10.Metadata?.Level == 12);
Check("metadata parsed: timestamp -> DateTime", manual10.Metadata?.TryGetTimestampUtc() is not null);

var unknownEntry = liveEntries.Single(e => e.FolderName == "ManualSave-99");
Check("missing metadata -> Metadata is null, not a crash", unknownEntry.Metadata is null);
Check("missing metadata -> PlaythroughKey is empty", unknownEntry.PlaythroughKey == string.Empty);

var autoSave5 = liveEntries.Single(e => e.FolderName == "AutoSave-5");
Check("1004-byte screenshot flagged as placeholder", autoSave5.ScreenshotIsPlaceholder);
var manual9 = liveEntries.Single(e => e.FolderName == "ManualSave-9");
Check("152341-byte screenshot NOT flagged as placeholder", !manual9.ScreenshotIsPlaceholder);

// ---------------------------------------------------------------------
// 3. Grouping by playthroughID
// ---------------------------------------------------------------------
var groups = PlaythroughGroup.GroupSaves(liveEntries);
Check("groups into 3 buckets (A, B, unknown)", groups.Count == 3);

var groupA = groups.Single(g => g.PlaythroughKey == ptidA);
Check("character A has 8 saves", groupA.Saves.Count == 8);
Check("character A auto-label uses newest save's data", groupA.AutoLabel.Contains("Streetkid") && groupA.AutoLabel.Contains("Lvl 12"));

var nicknamed = PlaythroughGroup.GroupSaves(liveEntries, new Dictionary<string, string> { [ptidA] = "Val the Streetkid" });
Check("nickname overrides display label", nicknamed.Single(g => g.PlaythroughKey == ptidA).DisplayLabel == "Val the Streetkid");

// ---------------------------------------------------------------------
// 4. Cleanup planning: default rule = Auto+Manual selected, mixed pool.
// ---------------------------------------------------------------------
var rule = new CleanupRuleConfig { SelectedTypes = new List<SaveType> { SaveType.AutoSave, SaveType.ManualSave }, KeepTotal = 3 };
var planner = new CleanupPlanner();
var plan = planner.Plan(ptidA, groupA.Live, rule);

// Eligible pool (Auto+Manual) for character A, newest first:
// AutoSave-5 (10:30), ManualSave-10 (10:00), ManualSave-9 (09:00), ManualSave-8 (08:00), ManualSave-7 (07:00), ManualSave-6 (06:00), AutoSave-4 (05:00)
// keepTotal=3 -> keep AutoSave-5, ManualSave-10, ManualSave-9; move the other 4.
// QuickSave-1 is NOT in SelectedTypes -> always kept untouched regardless of age.
Check("plan moves exactly 4 saves", plan.ToMove.Count == 4);
Check("plan keeps newest 3 eligible + the untouched QuickSave = 4", plan.KeptLive.Count == 4);
Check("QuickSave-1 (not selected type) is never a move candidate", plan.ToMove.All(i => i.Save.FolderName != "QuickSave-1"));
Check("QuickSave-1 stays in KeptLive untouched", plan.KeptLive.Any(s => s.FolderName == "QuickSave-1"));
Check("oldest eligible save (ManualSave-6) is in the move list", plan.ToMove.Any(i => i.Save.FolderName == "ManualSave-6"));
Check("newest eligible save (AutoSave-5) is kept, not moved", plan.ToMove.All(i => i.Save.FolderName != "AutoSave-5"));

// ---------------------------------------------------------------------
// 5. Cross-"drive" safe move (copy -> verify -> delete) + storage layout
// ---------------------------------------------------------------------
var executor = new CleanupExecutor();
var execResults = executor.Execute(plan, storageDir);

Check("all 4 planned moves report success", execResults.All(r => r.MoveResult.Success));
Check("source folders are gone after move", execResults.All(r => !Directory.Exists(r.Item.Save.FullPath)));
Check("destination folders exist after move", execResults.All(r => Directory.Exists(r.MoveResult.DestinationPath)));

var expectedPtidFolder = Path.Combine(storageDir, ptidA);
Check("storage is namespaced by playthroughID", Directory.Exists(expectedPtidFolder));
Check("original folder name preserved in storage", Directory.Exists(Path.Combine(expectedPtidFolder, "ManualSave-6")));

// sav.dat content survived the copy intact (not just same length by accident).
var movedSavDat = Path.Combine(expectedPtidFolder, "ManualSave-6", "sav.dat");
var savDatBytes = File.ReadAllBytes(movedSavDat);
Check("moved sav.dat content is byte-identical", savDatBytes.All(b => b == 0x42) && savDatBytes.Length == 2048);

// Re-scan storage and confirm the scanner's storage-mode (PTID subfolder) path works.
var storageEntries = scanner.ScanStorage(storageDir);
Check("storage scan finds all 4 moved saves", storageEntries.Count == 4);
Check("storage-scanned entries still parse metadata correctly", storageEntries.All(e => e.Metadata?.PlaythroughId == ptidA));

// ---------------------------------------------------------------------
// 6. SafeFolderMover collision + resolver dedup behaviour
// ---------------------------------------------------------------------
var mover = new SafeFolderMover();
var collisionSource = Path.Combine(liveDir, "ManualSave-8"); // already moved once above? no - ManualSave-8 was moved. Recreate a fresh one to test collision.
Directory.CreateDirectory(collisionSource);
File.WriteAllBytes(Path.Combine(collisionSource, "sav.dat"), new byte[10]);
var collisionResult = mover.MoveFolder(collisionSource, expectedPtidFolder, "ManualSave-6"); // ManualSave-6 already exists there
Check("moving into an already-occupied destination name fails cleanly", !collisionResult.Success);
Check("failed move leaves the source folder untouched", Directory.Exists(collisionSource));

var resolver = new StoragePathResolver();
var fakeSaveForDedup = liveEntries.Single(e => e.FolderName == "ManualSave-6"); // metadata still in memory even though folder moved
var dedupedName = resolver.ResolveDestinationFolderName(expectedPtidFolder, fakeSaveForDedup);
Check("resolver proposes a deduped name when the plain name is taken", dedupedName != "ManualSave-6" && dedupedName.StartsWith("ManualSave-6__"));

// ---------------------------------------------------------------------
// 7. Config round-trip (default paths + save/load)
// ---------------------------------------------------------------------
var configPath = Path.Combine(tempRoot, "config", "config.json");
var configService = new ConfigService(configPath);
var loadedDefault = configService.Load();
Check("default SaveDir ends with expected CP77 path", loadedDefault.SaveDir.Replace('\\', '/').EndsWith("Saved Games/CD Projekt Red/Cyberpunk 2077"));
Check("default StorageDir is a sibling named CP77SGM-storage", loadedDefault.StorageDir.Replace('\\', '/').EndsWith("Saved Games/CD Projekt Red/CP77SGM-storage"));
Check("default cleanup selection is Quick+Manual", loadedDefault.CleanupRule.SelectedTypes.ToHashSet().SetEquals(new[] { SaveType.QuickSave, SaveType.ManualSave }));
Check("default language is de-DE", loadedDefault.Language == "de-DE");

loadedDefault.Nicknames[ptidA] = "Val the Streetkid";
loadedDefault.CleanupRule.KeepTotal = 42;
loadedDefault.Language = "en-GB";
configService.Save(loadedDefault);
var reloaded = configService.Load();
Check("config round-trips nickname", reloaded.Nicknames[ptidA] == "Val the Streetkid");
Check("config round-trips cleanup rule", reloaded.CleanupRule.KeepTotal == 42);
Check("config round-trips language", reloaded.Language == "en-GB");
Check("config stores save types as names, not numbers", File.ReadAllText(configPath).Contains("\"QuickSave\"") && File.ReadAllText(configPath).Contains("\"ManualSave\""));

// Configs written by <= 0.3.0 stored enums as numbers - must still load.
File.WriteAllText(configPath, """{ "cleanupRule": { "selectedTypes": [1, 2], "keepTotal": 7 } }""");
var legacy = configService.Load();
Check("legacy numeric save types still load", legacy.CleanupRule.KeepTotal == 7 && legacy.CleanupRule.SelectedTypes.ToHashSet().SetEquals(new[] { SaveType.AutoSave, SaveType.QuickSave }));

Directory.Delete(tempRoot, recursive: true);

// ---------------------------------------------------------------------
// 8. Old-file (*.old) detection
// ---------------------------------------------------------------------
{
    string root2 = Path.Combine(Path.GetTempPath(), "cp77sgm-test2-" + Guid.NewGuid().ToString("N"));
    string live2 = Path.Combine(root2, "live", "Cyberpunk 2077");
    string storage2 = Path.Combine(root2, "storage-drive", "CP77SGM-storage");
    Directory.CreateDirectory(live2);
    Directory.CreateDirectory(storage2);

    const string ptidC = "cccc3333dddd4444";

    WriteSaveFolder(live2, "ManualSave-1", BuildMetadataJson(ptidC, "Nomad", "Male", 5, "01:00:00, 27.09.2026", "ManualSave-1"), 152341);
    // Simulate the game's own backup file, as seen on real ManualSave-2/3/49/54.
    File.WriteAllBytes(Path.Combine(live2, "ManualSave-1", "sav.old"), new byte[512]);

    WriteSaveFolder(live2, "ManualSave-2", BuildMetadataJson(ptidC, "Nomad", "Male", 6, "02:00:00, 27.09.2026", "ManualSave-2"), 152341);

    var scanner2 = new SaveScanner();
    var entries2 = scanner2.ScanLive(live2);
    var withOld = entries2.Single(e => e.FolderName == "ManualSave-1");
    var withoutOld = entries2.Single(e => e.FolderName == "ManualSave-2");
    Check("sav.old is detected and counted", withOld.OldFileCount == 1);
    Check("save without *.old reports 0", withoutOld.OldFileCount == 0);

    // -----------------------------------------------------------------
    // 9. Individual save actions: store + delete (with root-safety guard)
    // -----------------------------------------------------------------
    var actionService = new SaveActionService();
    var managedRoots = new[] { live2, storage2 };

    var storeResult = actionService.StoreSave(withoutOld, storage2);
    Check("StoreSave moves a single save successfully", storeResult.Success);
    Check("StoreSave source folder is gone", !Directory.Exists(withoutOld.FullPath));
    Check("StoreSave lands under storage/<PTID>/<original name>", Directory.Exists(Path.Combine(storage2, ptidC, "ManualSave-2")));

    var deleteResult = actionService.DeleteSave(withOld, managedRoots);
    Check("DeleteSave deletes a single save successfully", deleteResult.Success);
    Check("DeleteSave folder is actually gone", !Directory.Exists(withOld.FullPath));

    // Root-safety guard: refuse to delete anything outside the managed roots.
    var outsideDir = Path.Combine(root2, "not-managed", "SomeFolder");
    Directory.CreateDirectory(outsideDir);
    File.WriteAllBytes(Path.Combine(outsideDir, "sav.dat"), new byte[10]);
    var outsideEntry = scanner2.ScanLive(Path.Combine(root2, "not-managed")).Single();
    var guardResult = actionService.DeleteSave(outsideEntry, managedRoots);
    Check("DeleteSave refuses to delete outside managed roots", !guardResult.Success);
    Check("DeleteSave guard leaves the outside folder untouched", Directory.Exists(outsideDir));

    // -----------------------------------------------------------------
    // 10. Character retirement: StoreAll and DeleteAll
    // -----------------------------------------------------------------
    WriteSaveFolder(live2, "ManualSave-10", BuildMetadataJson(ptidC, "Nomad", "Male", 7, "03:00:00, 27.09.2026", "ManualSave-10"), 152341);
    WriteSaveFolder(live2, "AutoSave-1", BuildMetadataJson(ptidC, "Nomad", "Male", 7, "03:30:00, 27.09.2026", "AutoSave-1"), 152341);

    var liveAfterAdd = scanner2.ScanLive(live2);
    var storageAfterAdd = scanner2.ScanStorage(storage2);
    var groupC = PlaythroughGroup.GroupSaves(liveAfterAdd.Concat(storageAfterAdd)).Single(g => g.PlaythroughKey == ptidC);
    Check("character C has 2 live + 1 stored before retirement", groupC.Live.Count() == 2 && groupC.Stored.Count() == 1);

    var retireStoreResult = actionService.Retire(groupC, RetirementMode.StoreAll, storage2, managedRoots);
    Check("RetireByStoring moves all live saves", retireStoreResult.SuccessCount == 2 && retireStoreResult.FailureCount == 0);
    Check("RetireByStoring leaves nothing live for that character", scanner2.ScanLive(live2).Count == 0);
    Check("RetireByStoring's saves now show up in storage", scanner2.ScanStorage(storage2).Count(e => e.Metadata?.PlaythroughId == ptidC) == 3);

    var liveAfterRetireStore = scanner2.ScanLive(live2);
    var storageAfterRetireStore = scanner2.ScanStorage(storage2);
    var groupCAfterStore = PlaythroughGroup.GroupSaves(liveAfterRetireStore.Concat(storageAfterRetireStore)).Single(g => g.PlaythroughKey == ptidC);

    var retireDeleteResult = actionService.Retire(groupCAfterStore, RetirementMode.DeleteAll, storage2, managedRoots);
    Check("RetireByDeleting removes all 3 saves (live+storage)", retireDeleteResult.SuccessCount == 3 && retireDeleteResult.FailureCount == 0);
    Check("RetireByDeleting leaves nothing behind for that character", !Directory.Exists(Path.Combine(storage2, ptidC)));

    Directory.Delete(root2, recursive: true);
}

// ---------------------------------------------------------------------
// 11. Timestamp preservation across a cross-"drive" move (Created,
//     LastWrite, LastAccess - for both the files AND the folder itself)
//
// Split into two blocks on purpose:
//   11a tests SafeFolderMover directly, with nothing else touching the
//       source folder between backdating and the move. This is the
//       real unit test of the preservation logic itself.
//   11b goes through the actual app path (SaveScanner.ScanLive ->
//       SaveActionService.StoreSave), i.e. what really happens when
//       Werner clicks "Storen" after having browsed the list. Listing
//       a folder's contents is itself an OS-level read that updates
//       that folder's OWN last-access-time - same as `dir` or Explorer
//       would. So by the time StoreSave runs, the live folder's access
//       time already reflects "last time the app scanned it", not
//       whatever it was before Werner ever opened the app that session.
//       SafeFolderMover still faithfully carries over whatever it's
//       given (11a proves that) - there's nothing left to fix here,
//       this is just what "access time" means on a folder that's
//       being actively browsed. 11b checks Created/LastWrite (which
//       scanning does NOT touch) and does not assert equality for the
//       folder's LastAccessTimeUtc.
// ---------------------------------------------------------------------
bool CloseEnough(DateTime a, DateTime b) => Math.Abs((a - b).TotalSeconds) < 5;
var originalTimestamp = new DateTime(2024, 3, 15, 8, 30, 0, DateTimeKind.Utc);

void BackdateSaveFolder(string dir, DateTime to)
{
    foreach (var file in Directory.GetFiles(dir))
    {
        File.SetCreationTimeUtc(file, to);
        File.SetLastWriteTimeUtc(file, to);
        File.SetLastAccessTimeUtc(file, to);
    }
    Directory.SetCreationTimeUtc(dir, to);
    Directory.SetLastWriteTimeUtc(dir, to);
    Directory.SetLastAccessTimeUtc(dir, to);
}

// 11a. SafeFolderMover in isolation - nothing reads the source between
// backdating and the move, so this is the true test of its own logic.
{
    string root3a = Path.Combine(Path.GetTempPath(), "cp77sgm-test3a-" + Guid.NewGuid().ToString("N"));
    string live3a = Path.Combine(root3a, "live", "Cyberpunk 2077");
    string storage3a = Path.Combine(root3a, "storage-drive", "CP77SGM-storage");
    Directory.CreateDirectory(live3a);
    Directory.CreateDirectory(storage3a);

    const string ptidD = "eeee5555ffff6666";
    WriteSaveFolder(live3a, "ManualSave-1", BuildMetadataJson(ptidD, "Corpo", "Female", 3, "04:00:00, 27.09.2026", "ManualSave-1"), 5000);

    var saveDir3a = Path.Combine(live3a, "ManualSave-1");
    BackdateSaveFolder(saveDir3a, originalTimestamp);

    var moverUnit = new SafeFolderMover();
    var moveResult3a = moverUnit.MoveFolder(saveDir3a, storage3a, "ManualSave-1");
    Check("[unit] SafeFolderMover: move itself succeeds", moveResult3a.Success);

    var movedFileInfoA = new FileInfo(Path.Combine(moveResult3a.DestinationPath, "sav.dat"));
    Check("[unit] moved file: CreationTimeUtc preserved (not reset to 'now')", CloseEnough(movedFileInfoA.CreationTimeUtc, originalTimestamp));
    Check("[unit] moved file: LastWriteTimeUtc preserved", CloseEnough(movedFileInfoA.LastWriteTimeUtc, originalTimestamp));
    Check("[unit] moved file: LastAccessTimeUtc preserved", CloseEnough(movedFileInfoA.LastAccessTimeUtc, originalTimestamp));

    var movedDirInfoA = new DirectoryInfo(moveResult3a.DestinationPath);
    Check("[unit] moved folder: CreationTimeUtc preserved", CloseEnough(movedDirInfoA.CreationTimeUtc, originalTimestamp));
    Check("[unit] moved folder: LastWriteTimeUtc preserved", CloseEnough(movedDirInfoA.LastWriteTimeUtc, originalTimestamp));
    Check("[unit] moved folder: LastAccessTimeUtc preserved (nothing scanned it first)", CloseEnough(movedDirInfoA.LastAccessTimeUtc, originalTimestamp));

    Directory.Delete(root3a, recursive: true);
}

// 11b. Real app path: scan (as the UI does to populate the list), then
// store. Confirms the realistic end-to-end result Werner will actually see.
{
    string root3b = Path.Combine(Path.GetTempPath(), "cp77sgm-test3b-" + Guid.NewGuid().ToString("N"));
    string live3b = Path.Combine(root3b, "live", "Cyberpunk 2077");
    string storage3b = Path.Combine(root3b, "storage-drive", "CP77SGM-storage");
    Directory.CreateDirectory(live3b);
    Directory.CreateDirectory(storage3b);

    const string ptidE = "abcd1234abcd1234";
    WriteSaveFolder(live3b, "ManualSave-1", BuildMetadataJson(ptidE, "Corpo", "Female", 3, "04:00:00, 27.09.2026", "ManualSave-1"), 5000);

    var saveDir3b = Path.Combine(live3b, "ManualSave-1");
    BackdateSaveFolder(saveDir3b, originalTimestamp);

    var scanner3 = new SaveScanner();
    var entryToMove = scanner3.ScanLive(live3b).Single(); // <- this listing already touches saveDir3b's own access time, same as Explorer/dir would
    var actionService3 = new SaveActionService();
    var moveResult3b = actionService3.StoreSave(entryToMove, storage3b);
    Check("[app-path] timestamp test: move itself succeeds", moveResult3b.Success);

    var movedFileInfoB = new FileInfo(Path.Combine(moveResult3b.DestinationPath, "sav.dat"));
    Check("[app-path] moved file: CreationTimeUtc preserved (not reset to 'now')", CloseEnough(movedFileInfoB.CreationTimeUtc, originalTimestamp));
    Check("[app-path] moved file: LastWriteTimeUtc preserved", CloseEnough(movedFileInfoB.LastWriteTimeUtc, originalTimestamp));
    Check("[app-path] moved file: LastAccessTimeUtc preserved", CloseEnough(movedFileInfoB.LastAccessTimeUtc, originalTimestamp));

    var movedDirInfoB = new DirectoryInfo(moveResult3b.DestinationPath);
    Check("[app-path] moved folder: CreationTimeUtc preserved", CloseEnough(movedDirInfoB.CreationTimeUtc, originalTimestamp));
    Check("[app-path] moved folder: LastWriteTimeUtc preserved", CloseEnough(movedDirInfoB.LastWriteTimeUtc, originalTimestamp));
    // Deliberately no LastAccessTimeUtc assertion here: scanning the live
    // folder to build the list already updated its access time to "now",
    // before StoreSave (and thus SafeFolderMover) ever got to see it. See
    // comment above 11a/11b.

    Directory.Delete(root3b, recursive: true);
}

// ---------------------------------------------------------------------
// 12. RestoreSave: Storage -> Live (Step 2). Live dir is flat (no PTID
// subfolders), and the original folder name is kept as-is - confirmed in
// Werner's own game that CP77 accepts any folder name for a live save, so
// there's no "find a free ManualSave-N slot" logic needed. Only real case to
// handle: a live save with that exact folder name already exists (slot
// numbers get reused across characters over time) - same dedup logic as
// StoreSave (StoragePathResolver.ResolveDestinationFolderName), reused here.
// ---------------------------------------------------------------------
{
    string root4 = Path.Combine(Path.GetTempPath(), "cp77sgm-test4-" + Guid.NewGuid().ToString("N"));
    string live4 = Path.Combine(root4, "live", "Cyberpunk 2077");
    string storage4 = Path.Combine(root4, "storage-drive", "CP77SGM-storage");
    Directory.CreateDirectory(live4);
    Directory.CreateDirectory(storage4);

    const string ptidF = "1111aaaa2222bbbb";

    // Plain case: nothing live with that name - restore keeps the original name.
    WriteSaveFolder(live4, "ManualSave-7", BuildMetadataJson(ptidF, "Corpo", "Male", 10, "05:00:00, 27.09.2026", "ManualSave-7"), 5000);
    var scanner4 = new SaveScanner();
    var actionService4 = new SaveActionService();
    var toStore = scanner4.ScanLive(live4).Single();
    var storeForRestoreTest = actionService4.StoreSave(toStore, storage4);
    Check("[restore] setup: store before restore succeeds", storeForRestoreTest.Success);

    var storedEntry = scanner4.ScanStorage(storage4).Single();
    var restoreResult = actionService4.RestoreSave(storedEntry, live4);
    Check("[restore] plain restore succeeds", restoreResult.Success);
    Check("[restore] restored folder keeps original name", restoreResult.DestinationPath == Path.Combine(live4, "ManualSave-7"));
    Check("[restore] source no longer in storage", !Directory.Exists(storedEntry.FullPath));
    Check("[restore] restored save shows up live again", scanner4.ScanLive(live4).Any(e => e.FolderName == "ManualSave-7"));

    // Collision case: a DIFFERENT save (different metadata) already occupies
    // the exact folder name "ManualSave-7" live (slot reuse) - restore must
    // not overwrite it, and must dedupe instead of failing.
    WriteSaveFolder(live4, "ManualSave-99", BuildMetadataJson(ptidF, "Corpo", "Male", 11, "06:00:00, 27.09.2026", "ManualSave-99"), 5000);
    var secondToStore = scanner4.ScanLive(live4).Single(e => e.FolderName == "ManualSave-99");
    actionService4.StoreSave(secondToStore, storage4); // now in storage as "ManualSave-99"

    // Simulate slot reuse: the live game later creates a NEW, different save
    // that happens to reuse the name "ManualSave-99" again.
    WriteSaveFolder(live4, "ManualSave-99", BuildMetadataJson(ptidF, "Corpo", "Male", 15, "07:00:00, 27.09.2026", "ManualSave-99"), 6000);
    var collidingLiveSave = scanner4.ScanLive(live4).Single(e => e.FolderName == "ManualSave-99");

    var storedNinetyNine = scanner4.ScanStorage(storage4).Single(e => e.FolderName == "ManualSave-99");
    var restoreCollisionResult = actionService4.RestoreSave(storedNinetyNine, live4);
    Check("[restore] collision restore still succeeds", restoreCollisionResult.Success);
    Check("[restore] collision restore does NOT overwrite the current live save", restoreCollisionResult.DestinationPath != collidingLiveSave.FullPath);
    Check("[restore] collision restore lands under a deduped name", restoreCollisionResult.DestinationPath.Contains("ManualSave-99__"));
    Check("[restore] the live save that was already there is untouched", Directory.Exists(collidingLiveSave.FullPath));
    Check("[restore] both saves now coexist live", scanner4.ScanLive(live4).Count(e => e.FolderName.StartsWith("ManualSave-99")) == 2);

    Directory.Delete(root4, recursive: true);
}

// ---------------------------------------------------------------------
// 13. Localization (Step 3): langs\ scanning, de-DE auto-generation,
// completion-% and the 3-stage Translator fallback chain.
// ---------------------------------------------------------------------
{
    var langsDir = Path.Combine(Path.GetTempPath(), "cp77sgm-langtest-" + Guid.NewGuid());

    // --- de-DE auto-generation -----------------------------------------
    var svc = new LocalizationService(langsDir);
    Check("[i18n] de-DE.json missing before EnsureReferenceFileExists", !File.Exists(Path.Combine(langsDir, "de-DE.json")));
    svc.EnsureReferenceFileExists();
    var deDePath = Path.Combine(langsDir, "de-DE.json");
    Check("[i18n] de-DE.json created", File.Exists(deDePath));

    var generated = JsonSerializer.Deserialize<LanguageFile>(File.ReadAllText(deDePath))!;
    Check("[i18n] generated de-DE has LANG = Deutsch", generated.Lang == "Deutsch");
    Check("[i18n] generated de-DE has every LocalizationDefaults key", LocalizationDefaults.Strings.Keys.All(k => generated.Strings.ContainsKey(k)));
    Check("[i18n] generated de-DE has exactly as many keys as LocalizationDefaults", generated.Strings.Count == LocalizationDefaults.Strings.Count);

    // Human-readability: umlauts/ß must be written as literal UTF-8, not
    // \uXXXX-escaped - both decode identically, but only one is easy to
    // hand-edit in a text editor.
    var rawDeDeText = File.ReadAllText(deDePath);
    Check("[i18n] generated de-DE.json has literal UTF-8 umlauts, not \\u-escapes", rawDeDeText.Contains("Größe") && !rawDeDeText.Contains("\\u00"));

    // Existing de-DE.json must never be clobbered (a human may have edited it).
    File.WriteAllText(deDePath, JsonSerializer.Serialize(new LanguageFile { Lang = "Deutsch (angepasst)", Strings = new() { ["X"] = "Y" } }));
    svc.EnsureReferenceFileExists();
    var afterSecondCall = JsonSerializer.Deserialize<LanguageFile>(File.ReadAllText(deDePath))!;
    Check("[i18n] EnsureReferenceFileExists does not overwrite an existing de-DE.json", afterSecondCall.Lang == "Deutsch (angepasst)");
    Check("[i18n] EnsureReferenceFileExists keeps existing (hand-edited) keys", afterSecondCall.Strings.GetValueOrDefault("X") == "Y");
    Check("[i18n] EnsureReferenceFileExists adds keys missing from an existing de-DE.json", LocalizationDefaults.Strings.Keys.All(k => afterSecondCall.Strings.ContainsKey(k)));

    // Restore a real de-DE.json for the rest of this section.
    File.WriteAllText(deDePath, JsonSerializer.Serialize(new LanguageFile { Lang = "Deutsch", Strings = new Dictionary<string, string>(LocalizationDefaults.Strings) }));

    // --- scanning: skip invalid files, list valid ones with correct % --
    File.WriteAllText(Path.Combine(langsDir, "empty.json"), "{}"); // no LANG - must be skipped
    File.WriteAllText(Path.Combine(langsDir, "broken.json"), "{ this is not valid json"); // must be skipped, not throw

    var allKeys = LocalizationDefaults.Strings.Keys.ToList();
    var halfKeys = allKeys.Take(allKeys.Count / 2).ToDictionary(k => k, k => "translated");
    halfKeys["UNKNOWN_KEY_NOT_IN_REFERENCE"] = "ignored either way"; // must not count for or against completion
    halfKeys[allKeys[0]] = ""; // was "translated" above - overwritten empty, must NOT count as translated anymore
    File.WriteAllText(Path.Combine(langsDir, "en-GB.json"), JsonSerializer.Serialize(new LanguageFile { Lang = "English", Strings = halfKeys }));

    var scanned = svc.ScanAvailableLanguages();
    Check("[i18n] scan skips file with no LANG field", scanned.All(l => l.Code != "empty"));
    Check("[i18n] scan skips unparsable JSON", scanned.All(l => l.Code != "broken"));
    Check("[i18n] scan finds de-DE at 100%", scanned.Single(l => l.Code == "de-DE").CompletionPercent == 100);

    var enInfo = scanned.Single(l => l.Code == "en-GB");
    Check("[i18n] scan reports en-GB display name from LANG field", enInfo.DisplayName == "English");
    // (allKeys.Count/2 - 1) real translations counted / allKeys.Count total - the emptied-out key + the unknown extra key are excluded.
    var expectedPercent = (int)Math.Round((allKeys.Count / 2 - 1) * 100.0 / allKeys.Count, MidpointRounding.AwayFromZero);
    Check($"[i18n] en-GB completion is {expectedPercent}% (empty + unknown keys excluded)", enInfo.CompletionPercent == expectedPercent);

    // --- Translator 3-stage fallback ------------------------------------
    var partial = new Dictionary<string, string> { ["ONLY_IN_SELECTED"] = "selected-value", ["SHARED_KEY"] = "" };
    var reference = new Dictionary<string, string> { ["SHARED_KEY"] = "reference-value", ["ONLY_IN_REFERENCE"] = "ref-only" };
    var defaults = new Dictionary<string, string> { ["ONLY_IN_REFERENCE"] = "should-not-be-used", ["ONLY_IN_DEFAULTS"] = "default-value" };
    var translator = new Translator(partial, reference, defaults);

    Check("[i18n] Translator prefers selected language", translator.Get("ONLY_IN_SELECTED") == "selected-value");
    Check("[i18n] Translator falls back to reference when selected value is empty", translator.Get("SHARED_KEY") == "reference-value");
    Check("[i18n] Translator falls back to reference when key missing from selected", translator.Get("ONLY_IN_REFERENCE") == "ref-only");
    Check("[i18n] Translator falls back to defaults when key missing everywhere else", translator.Get("ONLY_IN_DEFAULTS") == "default-value");
    Check("[i18n] Translator returns bracketed key when truly unknown", translator.Get("NOWHERE_AT_ALL") == "[NOWHERE_AT_ALL]");
    Check("[i18n] Translator.Get formats with args", translator.Get("ONLY_IN_SELECTED") + "-" + string.Format("{0}/{1}", 1, 2) == "selected-value-1/2");

    // --- CreateTranslator wiring (selected + reference loaded from disk) -
    var deTranslator = svc.CreateTranslator("de-DE");
    Check("[i18n] CreateTranslator(de-DE) resolves a known key", deTranslator.Get("BTN_RELOAD") == "Neu laden");

    var enTranslator = svc.CreateTranslator("en-GB");
    Check("[i18n] CreateTranslator(en-GB) uses the translated value when present", enTranslator.Get(allKeys[1]) == "translated");
    Check("[i18n] CreateTranslator(en-GB) falls back to de-DE for the deliberately-empty key", enTranslator.Get(allKeys[0]) == LocalizationDefaults.Strings[allKeys[0]]);

    var missingTranslator = svc.CreateTranslator("does-not-exist");
    Check("[i18n] CreateTranslator for an unknown code still resolves via de-DE/defaults", missingTranslator.Get("BTN_RELOAD") == "Neu laden");

    // --- key parity: the real shipped en-GB.json must cover every key ---
    var shippedEnPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Cp77SaveManager.App", "langs", "en-GB.json");
    if (File.Exists(shippedEnPath))
    {
        var shippedEn = JsonSerializer.Deserialize<LanguageFile>(File.ReadAllText(shippedEnPath))!;
        var missingInShipped = LocalizationDefaults.Strings.Keys.Where(k => !shippedEn.Strings.ContainsKey(k) || string.IsNullOrEmpty(shippedEn.Strings[k])).ToList();
        Check("[i18n] shipped langs/en-GB.json has every LocalizationDefaults key translated", missingInShipped.Count == 0);
        if (missingInShipped.Count > 0)
        {
            Console.WriteLine("      missing/empty in en-GB.json: " + string.Join(", ", missingInShipped));
        }
    }
    else
    {
        Check("[i18n] shipped langs/en-GB.json exists at src/Cp77SaveManager.App/langs/en-GB.json", false);
    }

    Directory.Delete(langsDir, recursive: true);
}

// ---------------------------------------------------------------------
// 14. Review 2026-09-28 fixes (S1-S7, R1). Runs on the Linux sandbox too,
//     so path examples use Path.DirectorySeparatorChar where it matters.
// ---------------------------------------------------------------------
{
    string root5 = Path.Combine(Path.GetTempPath(), "cp77sgm-test5-" + Guid.NewGuid().ToString("N"));
    string live5 = Path.Combine(root5, "live", "Cyberpunk 2077");
    string storage5 = Path.Combine(root5, "storage-drive", "CP77SGM-storage");
    Directory.CreateDirectory(live5);
    Directory.CreateDirectory(storage5);
    var scanner5 = new SaveScanner();
    var actions5 = new SaveActionService();
    var resolver5 = new StoragePathResolver();

    // --- S1: playthroughID must never act as a path -----------------------
    var escapeTarget = Path.Combine(root5, "escaped");
    var badIds = new[] { @"..\..\escaped", "../../escaped", escapeTarget, @"C:\evil", ".", "..", "a/b", @"a\b", "", "CON", "nul", new string('a', 65) };
    foreach (var bad in badIds)
    {
        Check($"[S1] unsafe playthroughID rejected: \"{(bad.Length > 20 ? bad[..20] + "..." : bad)}\"", !PathSafety.IsSafePlaythroughId(bad));
    }
    Check("[S1] real 16-hex playthroughID accepted", PathSafety.IsSafePlaythroughId("6ab83a8fe6b8c7ff"));

    WriteSaveFolder(live5, "ManualSave-1", BuildMetadataJson("../../escaped", "Corpo", "Male", 5, "05:00:00, 27.09.2026", "ManualSave-1").Replace("\"../../escaped\"", JsonSerializer.Serialize(escapeTarget)), 5000);
    var evil = scanner5.ScanLive(live5).Single(e => e.FolderName == "ManualSave-1");
    Check("[S1] manipulated save lands in the Unknown bucket", evil.PlaythroughKey == "");
    Check("[S1] raw metadata value is still visible for the preview", evil.Metadata?.PlaythroughId == escapeTarget);
    Check("[S1] storage parent for manipulated save is storage/_unknown", resolver5.ResolveDestinationParent(storage5, evil) == Path.Combine(storage5, "_unknown"));
    var evilStore = actions5.StoreSave(evil, storage5);
    Check("[S1] storing manipulated save succeeds inside storage", evilStore.Success && PathSafety.IsStrictlyUnder(evilStore.DestinationPath, storage5));
    Check("[S1] nothing was written to the attacker-chosen path", !Directory.Exists(escapeTarget));

    // --- R1: save folder with a subfolder is refused, nothing lost ---------
    WriteSaveFolder(live5, "ManualSave-2", BuildMetadataJson("1234abcd1234abcd", "Corpo", "Male", 5, "05:00:00, 27.09.2026", "ManualSave-2"), 5000);
    var subDir = Path.Combine(live5, "ManualSave-2", "extra");
    Directory.CreateDirectory(subDir);
    File.WriteAllText(Path.Combine(subDir, "keep.txt"), "important");
    var withSub = scanner5.ScanLive(live5).Single(e => e.FolderName == "ManualSave-2");
    var subResult = actions5.StoreSave(withSub, storage5);
    Check("[R1] store of a save folder with a subfolder is refused", !subResult.Success);
    Check("[R1] refused source is fully intact (incl. subfolder)", File.Exists(Path.Combine(subDir, "keep.txt")) && File.Exists(Path.Combine(live5, "ManualSave-2", "sav.dat")));
    Check("[R1] no leftover temp folder in storage", !Directory.EnumerateDirectories(storage5, "*.partial*", SearchOption.AllDirectories).Any());

    // --- S4: nested / identical / empty roots are rejected ------------------
    Check("[S4] valid sibling dirs accepted", PathSafety.ValidateRoots(live5, storage5) is null);
    Check("[S4] trailing separator on a valid dir accepted", PathSafety.ValidateRoots(live5 + Path.DirectorySeparatorChar, storage5) is null);
    Check("[S4] storage inside save dir rejected", PathSafety.ValidateRoots(live5, Path.Combine(live5, "storage")) == "ERR_DIR_NESTED");
    Check("[S4] save dir inside storage rejected", PathSafety.ValidateRoots(Path.Combine(storage5, "x"), storage5) == "ERR_DIR_NESTED");
    Check("[S4] identical dirs rejected", PathSafety.ValidateRoots(live5, live5 + Path.DirectorySeparatorChar) == "ERR_DIR_SAME");
    Check("[S4] empty dir rejected", PathSafety.ValidateRoots("", storage5) == "ERR_DIR_EMPTY");
    Check("[S4] relative dir rejected", PathSafety.ValidateRoots("saves", storage5) == "ERR_DIR_NOT_ABSOLUTE");
    Check("[S4] drive root rejected", PathSafety.ValidateRoots(Path.GetPathRoot(live5)!, storage5) == "ERR_DIR_DRIVE_ROOT");
    Check("[S4] every validation key has a text", new[] { "ERR_DIR_EMPTY", "ERR_DIR_NOT_ABSOLUTE", "ERR_DIR_DRIVE_ROOT", "ERR_DIR_SAME", "ERR_DIR_NESTED" }.All(LocalizationDefaults.Strings.ContainsKey));

    // Mover second line of defence: destination inside source is refused
    // (the old code would have deleted the source incl. the fresh destination).
    var flatSource = Path.Combine(root5, "flat-source");
    Directory.CreateDirectory(flatSource);
    File.WriteAllText(Path.Combine(flatSource, "a.txt"), "a");
    var insideResult = new SafeFolderMover().MoveFolder(flatSource, Path.Combine(flatSource, "_unknown"), "flat-source");
    Check("[S4] mover refuses destination inside source", !insideResult.Success);
    Check("[S4] refused source still intact, no folder created inside it", File.Exists(Path.Combine(flatSource, "a.txt")) && !Directory.Exists(Path.Combine(flatSource, "_unknown")));

    // --- S5: a pre-existing "<dest>.partial" folder is left alone ----------
    var s5Source = Path.Combine(root5, "s5-live", "ManualSave-3");
    Directory.CreateDirectory(s5Source);
    File.WriteAllText(Path.Combine(s5Source, "sav.dat"), "x");
    var s5Parent = Path.Combine(root5, "s5-dest");
    var foreignPartial = Path.Combine(s5Parent, "ManualSave-3.partial");
    Directory.CreateDirectory(foreignPartial);
    File.WriteAllText(Path.Combine(foreignPartial, "foreign.txt"), "not ours");
    var s5Result = new SafeFolderMover().MoveFolder(s5Source, s5Parent, "ManualSave-3");
    Check("[S5] move succeeds next to a foreign .partial folder", s5Result.Success);
    Check("[S5] foreign .partial folder is NOT deleted", File.Exists(Path.Combine(foreignPartial, "foreign.txt")));

    // --- S2/S3: links below a root are ignored / refused -------------------
    var outside = Path.Combine(root5, "outside");
    Directory.CreateDirectory(outside);
    WriteSaveFolder(outside, "ManualSave-50", BuildMetadataJson("9999aaaa9999aaaa", "Corpo", "Male", 5, "05:00:00, 27.09.2026", "ManualSave-50"), 5000);
    var linkedSave = Path.Combine(live5, "ManualSave-50");
    var linkedPtid = Path.Combine(storage5, "9999aaaa9999aaaa");
    bool linksSupported = true;
    try
    {
        Directory.CreateSymbolicLink(linkedSave, Path.Combine(outside, "ManualSave-50"));
        Directory.CreateSymbolicLink(linkedPtid, outside);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
    {
        linksSupported = false; // Windows without developer mode/admin
    }
    if (linksSupported)
    {
        Check("[S2] linked save folder is recognised as reparse point", PathSafety.IsReparsePoint(linkedSave));
        Check("[S2] live scan ignores a linked save folder", scanner5.ScanLive(live5).All(e => e.FolderName != "ManualSave-50"));
        Check("[S2] storage scan ignores a linked PTID folder", scanner5.ScanStorage(storage5).All(e => e.PlaythroughKey != "9999aaaa9999aaaa"));

        var viaLink = new SaveEntry
        {
            FolderName = "ManualSave-50", SaveType = SaveType.ManualSave, Index = 50,
            FullPath = Path.Combine(linkedPtid, "ManualSave-50"), Location = SaveLocation.Storage,
            TotalSizeBytes = 0, OldFileCount = 0, DirectoryLastWriteUtc = DateTime.UtcNow
        };
        var viaLinkDelete = actions5.DeleteSave(viaLink, new[] { live5, storage5 });
        Check("[S3] delete through a linked PTID folder is refused", !viaLinkDelete.Success);
        Check("[S3] link target is untouched", File.Exists(Path.Combine(outside, "ManualSave-50", "sav.dat")));
        Directory.Delete(linkedSave);
        Directory.Delete(linkedPtid);
    }
    else
    {
        Console.WriteLine("SKIP  [S2/S3] symlink tests (creating links not permitted here)");
    }

    // --- S3/S7: root with trailing separator works, empty root doesn't throw
    var toDelete = scanner5.ScanLive(live5).Single(e => e.FolderName == "ManualSave-2");
    Directory.Delete(Path.Combine(toDelete.FullPath, "extra"), recursive: true);
    var trailingDelete = actions5.DeleteSave(toDelete, new[] { "", live5 + Path.DirectorySeparatorChar });
    Check("[S3] delete works with trailing separator on root (and empty root ignored)", trailingDelete.Success && !Directory.Exists(toDelete.FullPath));
    Check("[S3] a root itself is never 'under' itself", !PathSafety.IsSafelyUnder(live5, live5));

    // --- S7: broken placeholder in a language file doesn't crash -----------
    var brokenT = new Translator(new Dictionary<string, string> { ["K"] = "kaputt {5} {" }, new Dictionary<string, string>(), new Dictionary<string, string> { ["K"] = "ok {0}" });
    Check("[S7] broken format string falls back to next stage", brokenT.Get("K", "x") == "ok x");
    var allBrokenT = new Translator(new Dictionary<string, string> { ["K"] = "{9}" }, new Dictionary<string, string>(), new Dictionary<string, string>());
    Check("[S7] all stages broken -> bracketed key, no exception", allBrokenT.Get("K", "x") == "[K]");

    // --- S6: German stays selectable even without de-DE.json ---------------
    var noLangs = new LocalizationService(Path.Combine(root5, "no-langs-here"));
    Check("[S6] de-DE listed even when langs folder is missing", noLangs.ScanAvailableLanguages().Any(l => l.Code == "de-DE"));

    // --- R3: Core texts come from the localization table --------------------
    Check("[R3] cleanup reason uses CLEANUP_REASON", new CleanupPlanner().Plan("k", new[] { withSub }, new CleanupRuleConfig { KeepTotal = 0 }).ToMove.Single().Reason.StartsWith("älter als die 0 neuesten"));

    Directory.Delete(root5, recursive: true);
}

Console.WriteLine();
Console.WriteLine(failures.Count == 0
    ? $"ALL CHECKS PASSED"
    : $"{failures.Count} CHECK(S) FAILED: {string.Join(", ", failures)}");

return failures.Count == 0 ? 0 : 1;
