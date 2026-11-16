using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps shipped GameData copies in sync with Assets/01. Resources/04. Data/GameData.xlsx.
/// Player builds (especially Android) load from Resources/GameData.bytes because
/// StreamingAssets cannot be opened with File I/O on mobile.
/// </summary>
public sealed class GameDataStreamingAssetsSync : IPreprocessBuildWithReport
{
    private const string SourceRelativePath = "01. Resources/04. Data/GameData.xlsx";
    private const string StreamingRelativePath = "StreamingAssets/GameData.xlsx";
    private const string ResourcesRelativePath = "Resources/GameData.bytes";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Sync(log: true);
    }

    [InitializeOnLoadMethod]
    private static void SyncOnEditorLoad()
    {
        Sync(log: false);
    }

    // 원본 xlsx가 저장되어 다시 임포트되면 복사본도 바로 갱신한다.
    private sealed class SourceChangeWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            const string sourceAssetPath = "Assets/" + SourceRelativePath;
            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (importedAssets[i] != sourceAssetPath) continue;

                // 임포트 콜백 안에서 AssetDatabase.Refresh를 부르지 않도록 다음 에디터 틱으로 미룬다.
                EditorApplication.delayCall += () => Sync(log: true);
                return;
            }
        }
    }

    [MenuItem("Tools/Sync GameData to StreamingAssets")]
    private static void SyncFromMenu()
    {
        if (Sync(log: true))
            EditorUtility.DisplayDialog(
                "GameData Sync",
                "Updated StreamingAssets/GameData.xlsx and Resources/GameData.bytes.",
                "OK");
        else
            EditorUtility.DisplayDialog("GameData Sync", "Source GameData.xlsx was not found.", "OK");
    }

    private static bool Sync(bool log)
    {
        string sourcePath = Path.Combine(Application.dataPath, SourceRelativePath);
        if (!File.Exists(sourcePath))
        {
            if (log)
                Debug.LogError($"[GameDataSync] Missing source: {sourcePath}");
            return false;
        }

        bool copiedStreaming = CopyIfNeeded(sourcePath, Path.Combine(Application.dataPath, StreamingRelativePath));
        bool copiedResources = CopyIfNeeded(sourcePath, Path.Combine(Application.dataPath, ResourcesRelativePath));

        if (copiedStreaming || copiedResources)
            AssetDatabase.Refresh();

        if (log)
        {
            Debug.Log(
                $"[GameDataSync] StreamingAssets={(copiedStreaming ? "updated" : "up-to-date")}, " +
                $"Resources={(copiedResources ? "updated" : "up-to-date")}");
        }

        return true;
    }

    private static bool CopyIfNeeded(string sourcePath, string destPath)
    {
        string destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        bool needsCopy = !File.Exists(destPath)
            || File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(destPath)
            || new FileInfo(sourcePath).Length != new FileInfo(destPath).Length;

        if (!needsCopy)
            return false;

        File.Copy(sourcePath, destPath, overwrite: true);
        return true;
    }
}
