using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class EnhanceIconLoader
{
    private const string EditorFolder = "Assets/01. Resources/01. Sprite/Enhance";
    private static readonly Dictionary<string, Sprite> Cache = new(System.StringComparer.OrdinalIgnoreCase);

    public static Sprite Load(string iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return null;

        string key = iconKey.Trim();
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Sprite sprite = Resources.Load<Sprite>($"Enhance/{key}");
        if (sprite == null)
            sprite = Resources.Load<Sprite>($"01. Sprite/Enhance/{key}");

#if UNITY_EDITOR
        if (sprite == null)
        {
            string assetPath = $"{EditorFolder}/{key}.png";
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite nested)
                    {
                        sprite = nested;
                        break;
                    }
                }
            }
        }
#endif

        if (sprite != null)
            Cache[key] = sprite;
        else
            Debug.LogWarning($"[Enhance] Icon sprite not found: {key}");

        return sprite;
    }
}
