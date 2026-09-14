using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class GameDataRepository
{
    private const string DefaultRelativePath = "01. Resources/04. Data/GameData.xlsx";
    private const string ItemSheetName = "Item";

    private static readonly Dictionary<string, ItemData> ItemsByPrefabKey =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ItemData> ItemsById =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool loaded;
    private static string loadError;

    public static string LoadError => loadError;
    public static bool IsLoaded => loaded;
    public static IReadOnlyDictionary<string, ItemData> Items => ItemsByPrefabKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Load();
    }

    public static void Load()
    {
        ItemsByPrefabKey.Clear();
        ItemsById.Clear();
        loaded = false;
        loadError = null;

        string path = ResolveXlsxPath();
        if (string.IsNullOrEmpty(path))
        {
            loadError = $"xlsx not found. Tried '{DefaultRelativePath}' under Assets/StreamingAssets.";
            Debug.LogError($"[GameData] Item sheet load failed: {loadError}");
            return;
        }

        if (!XlsxSheetReader.TryReadSheet(path, ItemSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
            loadError = error;
            Debug.LogError($"[GameData] Item sheet load failed: {error}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (!TryParseItemRow(rows[i], out ItemData itemData))
                continue;

            if (!string.IsNullOrWhiteSpace(itemData.PrefabKey))
                ItemsByPrefabKey[itemData.PrefabKey] = itemData;

            if (!string.IsNullOrWhiteSpace(itemData.Id))
                ItemsById[itemData.Id] = itemData;
        }

        loaded = true;
        Debug.Log($"[GameData] Loaded {ItemsByPrefabKey.Count} item rows from Item sheet.");
    }

    private static string ResolveXlsxPath()
    {
        string assetsPath = Path.Combine(Application.dataPath, DefaultRelativePath);
        if (File.Exists(assetsPath))
            return assetsPath;

        string streamingPath = Path.Combine(Application.streamingAssetsPath, "GameData.xlsx");
        if (File.Exists(streamingPath))
            return streamingPath;

        return null;
    }

    public static bool TryGetItemByPrefabKey(string prefabKey, out ItemData itemData)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(prefabKey))
        {
            itemData = null;
            return false;
        }

        return ItemsByPrefabKey.TryGetValue(prefabKey, out itemData);
    }

    public static bool TryGetItemById(string id, out ItemData itemData)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(id))
        {
            itemData = null;
            return false;
        }

        return ItemsById.TryGetValue(id, out itemData);
    }

    private static void EnsureLoaded()
    {
        if (!loaded && string.IsNullOrEmpty(loadError))
            Load();
    }

    private static bool TryParseItemRow(Dictionary<string, string> row, out ItemData itemData)
    {
        itemData = null;

        string id = Get(row, "ID", "Id");
        string prefabKey = Get(row, "PrefabKey");
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(prefabKey))
            return false;

        string rangeText = Get(row, "Range");
        string damageText = Get(row, "Damage");
        string fireIntervalText = Get(row, "FireInterval");
        bool hasCombatStats =
            !string.IsNullOrWhiteSpace(rangeText) ||
            !string.IsNullOrWhiteSpace(damageText) ||
            !string.IsNullOrWhiteSpace(fireIntervalText);

        itemData = new ItemData
        {
            Id = id?.Trim() ?? string.Empty,
            Name = Get(row, "Name")?.Trim() ?? string.Empty,
            PrefabKey = string.IsNullOrWhiteSpace(prefabKey) ? id?.Trim() ?? string.Empty : prefabKey.Trim(),
            Category = Get(row, "Category")?.Trim() ?? string.Empty,
            WeaponType = Get(row, "WeaponType")?.Trim() ?? string.Empty,
            Range = ParseFloat(rangeText),
            Damage = ParseInt(damageText),
            FireInterval = ParseFloat(fireIntervalText),
            HasCombatStats = hasCombatStats
        };

        return true;
    }

    private static string Get(Dictionary<string, string> row, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (row.TryGetValue(keys[i], out string value))
                return value;
        }

        return string.Empty;
    }

    private static float ParseFloat(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0f;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : 0f;
    }

    private static int ParseInt(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            return value;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float asFloat))
            return Mathf.RoundToInt(asFloat);
        return 0;
    }
}
