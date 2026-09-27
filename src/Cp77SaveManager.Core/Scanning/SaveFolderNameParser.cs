using System.Text.RegularExpressions;
using Cp77SaveManager.Core.Models;

namespace Cp77SaveManager.Core.Scanning;

public static partial class SaveFolderNameParser
{
    // Verified folder-name patterns: ManualSave-86, AutoSave-12, QuickSave-3,
    // EndGameSave-0, PointOfNoReturnSave-0.
    [GeneratedRegex(@"^(?<type>ManualSave|AutoSave|QuickSave|EndGameSave|PointOfNoReturnSave)-(?<index>\d+)$")]
    private static partial Regex FolderNameRegex();

    public static (SaveType Type, int Index) Parse(string folderName)
    {
        var match = FolderNameRegex().Match(folderName);
        if (!match.Success)
        {
            return (SaveType.Unknown, -1);
        }

        var type = match.Groups["type"].Value switch
        {
            "ManualSave" => SaveType.ManualSave,
            "AutoSave" => SaveType.AutoSave,
            "QuickSave" => SaveType.QuickSave,
            "EndGameSave" => SaveType.EndGameSave,
            "PointOfNoReturnSave" => SaveType.PointOfNoReturnSave,
            _ => SaveType.Unknown
        };

        var index = int.Parse(match.Groups["index"].Value);
        return (type, index);
    }
}
