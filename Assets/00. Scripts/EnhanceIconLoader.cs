using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class EnhanceIconLoader
{
    private const string ResourcesFolder = "Enhance";
    private const string EditorFolder = "Assets/01. Resources/01. Sprite/Enhance";
    private static readonly Dictionary<string, Sprite> Cache = new(System.StringComparer.OrdinalIgnoreCase);
    private static bool resourcesScanned;

    public static Sprite Load(string iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return null;

        string key = iconKey.Trim();
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        EnsureResourcesCache();

        if (Cache.TryGetValue(key, out cached) && cached != null)
            return cached;

        Sprite sprite = Resources.Load<Sprite>($"{ResourcesFolder}/{key}");

#if UNITY_EDITOR
        if (sprite == null)
            sprite = LoadFromEditorAssets(key);
#endif

        if (sprite != null)
            Cache[key] = sprite;
        else
            Debug.LogWarning($"[Enhance] Icon sprite not found: {key}");

        return sprite;
    }

    private static void EnsureResourcesCache()
    {
        if (resourcesScanned) return;
        resourcesScanned = true;

        Sprite[] sprites = Resources.LoadAll<Sprite>(ResourcesFolder);
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null) continue;

            // Prefer the texture/file name (All_A) over sliced names (All_A_0).
            string textureName = sprite.texture != null ? sprite.texture.name : null;
            if (!string.IsNullOrEmpty(textureName) && !Cache.ContainsKey(textureName))
                Cache[textureName] = sprite;

            if (!Cache.ContainsKey(sprite.name))
                Cache[sprite.name] = sprite;
        }
    }

#if UNITY_EDITOR
    private static Sprite LoadFromEditorAssets(string key)
    {
        string assetPath = $"{EditorFolder}/{key}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null) return sprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite nested)
                return nested;
        }

        return null;
    }
#endif
}
