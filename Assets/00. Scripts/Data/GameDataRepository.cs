using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class GameDataRepository
{
    private const string DefaultRelativePath = "01. Resources/04. Data/GameData.xlsx";
    private const string ItemSheetName = "Item";
    private const string MonsterSheetName = "Monster";
    private const string StageSheetName = "Stage";
    private const string StageSpawnSheetName = "StageSpawn";
    private const string EnhanceOptionSheetName = "EnhanceOption";
    private const string EnhanceOptionEffectSheetName = "EnhanceOptionEffect";
    private const string LocalizationSheetName = "Localization";

    private static readonly Dictionary<string, ItemData> ItemsByPrefabKey =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ItemData> ItemsById =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, MonsterData> MonstersByPrefabKey =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, MonsterData> MonstersById =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, StageData> StagesById =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, StageData> StagesByOrder = new();
    private static readonly List<StageSpawnEntry> StageSpawns = new();
    private static readonly List<EnhanceOptionData> EnhanceOptions = new();
    private static readonly Dictionary<string, List<EnhanceOptionEffectData>> EnhanceEffectsByOptionId =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Dictionary<string, string>> LocalizationByKey =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool loaded;
    private static string loadError;

    public static string LoadError => loadError;
    public static bool IsLoaded => loaded;
    public static IReadOnlyDictionary<string, ItemData> Items => ItemsByPrefabKey;
    public static IReadOnlyDictionary<string, MonsterData> Monsters => MonstersByPrefabKey;
    public static IReadOnlyList<EnhanceOptionData> AllEnhanceOptions => EnhanceOptions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Load();
    }

    public static void Load()
    {
        ItemsByPrefabKey.Clear();
        ItemsById.Clear();
        MonstersByPrefabKey.Clear();
        MonstersById.Clear();
        StagesById.Clear();
        StagesByOrder.Clear();
        StageSpawns.Clear();
        EnhanceOptions.Clear();
        EnhanceEffectsByOptionId.Clear();
        LocalizationByKey.Clear();
        loaded = false;
        loadError = null;

        string path = ResolveXlsxPath();
        if (string.IsNullOrEmpty(path))
        {
            loadError = $"xlsx not found. Tried '{DefaultRelativePath}' under Assets/StreamingAssets.";
            Debug.LogError($"[GameData] load failed: {loadError}");
            return;
        }

        LoadItemSheet(path);
        LoadMonsterSheet(path);
        LoadStageSheet(path);
        LoadStageSpawnSheet(path);
        LoadEnhanceOptionSheet(path);
        LoadEnhanceOptionEffectSheet(path);
        LoadLocalizationSheet(path);

        loaded = true;
        Debug.Log(
            $"[GameData] Loaded items={ItemsByPrefabKey.Count}, monsters={MonstersById.Count}, " +
            $"stages={StagesById.Count}, stageSpawns={StageSpawns.Count}, " +
            $"enhanceOptions={EnhanceOptions.Count}, localization={LocalizationByKey.Count}");
    }

    private static void LoadItemSheet(string path)
    {
        if (!XlsxSheetReader.TryReadSheet(path, ItemSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
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
    }

    private static void LoadMonsterSheet(string path)
    {
        if (!XlsxSheetReader.TryReadSheet(path, MonsterSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
            Debug.LogError($"[GameData] Monster sheet load failed: {error}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (!TryParseMonsterRow(rows[i], out MonsterData monsterData))
                continue;

            if (!string.IsNullOrWhiteSpace(monsterData.PrefabKey))
                MonstersByPrefabKey[monsterData.PrefabKey] = monsterData;

            if (!string.IsNullOrWhiteSpace(monsterData.Id))
                MonstersById[monsterData.Id] = monsterData;
        }
    }

    private static void LoadStageSheet(string path)
    {
        if (!XlsxSheetReader.TryReadSheet(path, StageSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
            Debug.LogError($"[GameData] Stage sheet load failed: {error}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (!TryParseStageRow(rows[i], out StageData stageData))
                continue;

            StagesById[stageData.Id] = stageData;
            StagesByOrder[stageData.Order] = stageData;
        }
    }

    private static void LoadStageSpawnSheet(string path)
    {
        if (!XlsxSheetReader.TryReadSheet(path, StageSpawnSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
            Debug.LogError($"[GameData] StageSpawn sheet load failed: {error}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (!TryParseStageSpawnRow(rows[i], out StageSpawnEntry entry))
                continue;

            StageSpawns.Add(entry);
        }
    }

    private static void LoadEnhanceOptionSheet(string path)
    {
        if (!XlsxSheetReader.TryReadSheet(path, EnhanceOptionSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
            Debug.LogError($"[GameData] EnhanceOption sheet load failed: {error}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (!TryParseEnhanceOptionRow(rows[i], out EnhanceOptionData option))
                continue;

            EnhanceOptions.Add(option);
        }
    }

    private static void LoadEnhanceOptionEffectSheet(string path)
    {
        if (!XlsxSheetReader.TryReadSheet(path, EnhanceOptionEffectSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
            Debug.LogError($"[GameData] EnhanceOptionEffect sheet load failed: {error}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (!TryParseEnhanceOptionEffectRow(rows[i], out EnhanceOptionEffectData effect))
                continue;

            if (!EnhanceEffectsByOptionId.TryGetValue(effect.OptionId, out List<EnhanceOptionEffectData> list))
            {
                list = new List<EnhanceOptionEffectData>();
                EnhanceEffectsByOptionId[effect.OptionId] = list;
            }

            list.Add(effect);
        }
    }

    private static void LoadLocalizationSheet(string path)
    {
        if (!XlsxSheetReader.TryReadSheet(path, LocalizationSheetName, out List<Dictionary<string, string>> rows, out string error))
        {
            Debug.LogError($"[GameData] Localization sheet load failed: {error}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, string> row = rows[i];
            string key = CleanCell(Get(row, "Key"));
            if (string.IsNullOrWhiteSpace(key)) continue;

            Dictionary<string, string> langs = new(StringComparer.OrdinalIgnoreCase);
            TryAddLocalizationLang(langs, "KR", Get(row, "KR"));
            TryAddLocalizationLang(langs, "EN", Get(row, "EN"));
            TryAddLocalizationLang(langs, "CN", Get(row, "CN"));
            TryAddLocalizationLang(langs, "JP", Get(row, "JP"));
            LocalizationByKey[key] = langs;
        }
    }

    private static void TryAddLocalizationLang(Dictionary<string, string> langs, string lang, string value)
    {
        string cleaned = CleanCell(value);
        if (string.IsNullOrWhiteSpace(cleaned)) return;
        langs[lang] = cleaned;
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

    public static bool TryGetMonsterByPrefabKey(string prefabKey, out MonsterData monsterData)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(prefabKey))
        {
            monsterData = null;
            return false;
        }

        return MonstersByPrefabKey.TryGetValue(prefabKey, out monsterData);
    }

    public static bool TryGetMonsterById(string id, out MonsterData monsterData)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(id))
        {
            monsterData = null;
            return false;
        }

        return MonstersById.TryGetValue(id, out monsterData);
    }

    public static bool TryGetStageByOrder(int order, out StageData stageData)
    {
        EnsureLoaded();
        return StagesByOrder.TryGetValue(order, out stageData);
    }

    public static bool TryGetStageById(string id, out StageData stageData)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(id))
        {
            stageData = null;
            return false;
        }

        return StagesById.TryGetValue(id, out stageData);
    }

    public static List<StageSpawnEntry> GetStageSpawnEntries(string stageId)
    {
        EnsureLoaded();
        List<StageSpawnEntry> result = new();
        if (string.IsNullOrWhiteSpace(stageId)) return result;

        for (int i = 0; i < StageSpawns.Count; i++)
        {
            StageSpawnEntry entry = StageSpawns[i];
            if (string.Equals(entry.StageId, stageId, StringComparison.OrdinalIgnoreCase))
                result.Add(entry);
        }

        return result;
    }

    public static List<EnhanceOptionEffectData> GetEnhanceEffects(string optionId)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(optionId))
            return new List<EnhanceOptionEffectData>();

        if (!EnhanceEffectsByOptionId.TryGetValue(optionId, out List<EnhanceOptionEffectData> list))
            return new List<EnhanceOptionEffectData>();

        return new List<EnhanceOptionEffectData>(list);
    }

    public static bool TryGetLocalization(string key, out string text, string preferredLang = "KR")
    {
        EnsureLoaded();
        text = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (!LocalizationByKey.TryGetValue(key.Trim(), out Dictionary<string, string> langs))
            return false;

        if (!string.IsNullOrWhiteSpace(preferredLang) &&
            langs.TryGetValue(preferredLang, out string preferred) &&
            !string.IsNullOrWhiteSpace(preferred))
        {
            text = preferred;
            return true;
        }

        if (langs.TryGetValue("KR", out string kr) && !string.IsNullOrWhiteSpace(kr))
        {
            text = kr;
            return true;
        }

        foreach (KeyValuePair<string, string> pair in langs)
        {
            if (string.IsNullOrWhiteSpace(pair.Value)) continue;
            text = pair.Value;
            return true;
        }

        return false;
    }

    public static List<EnhanceOptionData> PickRandomEnhanceOptions(int count)
    {
        EnsureLoaded();
        List<EnhanceOptionData> pool = new(EnhanceOptions);
        if (pool.Count == 0 || count <= 0)
            return new List<EnhanceOptionData>();

        int pickCount = Mathf.Min(count, pool.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, pool.Count);
            (pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
        }

        return pool.GetRange(0, pickCount);
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

    private static bool TryParseMonsterRow(Dictionary<string, string> row, out MonsterData monsterData)
    {
        monsterData = null;

        string id = Get(row, "ID", "Id", "MonsterID");
        string prefabKey = Get(row, "PrefabKey");
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(prefabKey))
            return false;

        monsterData = new MonsterData
        {
            Id = id?.Trim() ?? string.Empty,
            Name = Get(row, "Name")?.Trim() ?? string.Empty,
            PrefabKey = string.IsNullOrWhiteSpace(prefabKey) ? id?.Trim() ?? string.Empty : prefabKey.Trim(),
            Hp = ParseInt(Get(row, "HP", "Hp", "MaxHp")),
            MoveSpeed = ParseFloat(Get(row, "MoveSpeed", "Speed")),
            Atk = ParseInt(Get(row, "Atk", "Attack", "AttackPower", "Damage")),
            Gold = ParseInt(Get(row, "Gold", "DropGold")),
            Exp = ParseInt(Get(row, "Exp", "DropExp", "Experience"))
        };

        return true;
    }

    private static bool TryParseStageRow(Dictionary<string, string> row, out StageData stageData)
    {
        stageData = null;

        string id = Get(row, "ID", "Id", "StageID");
        if (string.IsNullOrWhiteSpace(id))
            return false;

        stageData = new StageData
        {
            Id = id.Trim(),
            Order = ParseInt(Get(row, "Order")),
            SpawnInterval = ParseFloat(Get(row, "SpawnInterval"))
        };

        return true;
    }

    private static bool TryParseStageSpawnRow(Dictionary<string, string> row, out StageSpawnEntry entry)
    {
        entry = null;

        string stageId = Get(row, "StageID", "StageId", "ID");
        string monsterId = Get(row, "MonsterID", "MonsterId", "PrefabKey");
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(monsterId))
            return false;

        entry = new StageSpawnEntry
        {
            StageId = stageId.Trim(),
            MonsterId = monsterId.Trim(),
            Count = Mathf.Max(0, ParseInt(Get(row, "Count")))
        };

        return entry.Count > 0;
    }

    private static bool TryParseEnhanceOptionRow(Dictionary<string, string> row, out EnhanceOptionData option)
    {
        option = null;

        string id = CleanCell(Get(row, "ID", "Id"));
        if (string.IsNullOrWhiteSpace(id))
            return false;

        option = new EnhanceOptionData
        {
            Id = id,
            DescKey = CleanCell(Get(row, "DescKey")),
            Icon = CleanCell(Get(row, "Icon"))
        };
        return true;
    }

    private static bool TryParseEnhanceOptionEffectRow(Dictionary<string, string> row, out EnhanceOptionEffectData effect)
    {
        effect = null;

        string optionId = CleanCell(Get(row, "ID", "Id", "OptionID", "OptionId"));
        string effectType = CleanCell(Get(row, "EffectType"));
        if (string.IsNullOrWhiteSpace(optionId) || string.IsNullOrWhiteSpace(effectType))
            return false;

        effect = new EnhanceOptionEffectData
        {
            OptionId = optionId,
            EffectType = effectType,
            Value = CleanCell(Get(row, "Value"))
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

    private static string CleanCell(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string cleaned = text.Trim();
        if (cleaned.Length >= 2 && cleaned[0] == '"' && cleaned[cleaned.Length - 1] == '"')
            cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();

        return cleaned;
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
