using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Copies enhance option icons into Assets/Resources/Enhance so player builds can load them.
/// Editor play mode can still fall back to AssetDatabase under 01. Resources.
/// </summary>
public sealed class EnhanceIconResourcesSync : IPreprocessBuildWithReport
{
    private const string SourceFolder = "Assets/01. Resources/01. Sprite/Enhance";
    private const string DestFolder = "Assets/Resources/Enhance";

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

    [MenuItem("Tools/Sync Enhance Icons to Resources")]
    private static void SyncFromMenu()
    {
        int count = Sync(log: true);
        EditorUtility.DisplayDialog(
            "Enhance Icon Sync",
            count >= 0 ? $"Synced {count} icons to Resources/Enhance." : "Source folder missing.",
            "OK");
    }

    private static int Sync(bool log)
    {
        if (!AssetDatabase.IsValidFolder(SourceFolder))
        {
            if (log)
                Debug.LogError($"[EnhanceIconSync] Missing source folder: {SourceFolder}");
            return -1;
        }

        EnsureFolder(DestFolder);

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolder });
        int copied = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!sourcePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string fileName = Path.GetFileName(sourcePath);
            string destPath = $"{DestFolder}/{fileName}";

            if (File.Exists(destPath))
            {
                string srcFull = Path.GetFullPath(sourcePath);
                string dstFull = Path.GetFullPath(destPath);
                if (File.GetLastWriteTimeUtc(srcFull) <= File.GetLastWriteTimeUtc(dstFull)
                    && new FileInfo(srcFull).Length == new FileInfo(dstFull).Length)
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(destPath);
            }

            if (AssetDatabase.CopyAsset(sourcePath, destPath))
                copied++;
            else if (log)
                Debug.LogWarning($"[EnhanceIconSync] Failed to copy {sourcePath}");
        }

        if (copied > 0)
            AssetDatabase.Refresh();

        if (log)
            Debug.Log($"[EnhanceIconSync] Copied {copied} icons → {DestFolder}");

        return copied;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
