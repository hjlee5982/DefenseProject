using UnityEngine;

/// <summary>
/// 타이틀 강화 진행도. 씬이 바뀌어도 유지되도록 PlayerPrefs에 저장한다.
/// 수치/비용/최대 레벨은 GameData Reinforce 시트를 사용한다.
/// </summary>
public static class TitleEnhanceProgress
{
    public const string EfficiencyId = "1000";
    public const string MagicId = "1001";
    public const string ProductivityId = "1002";
    public const string DiscardId = "1003";
    public const string BarrierId = "1004";
    public const string ChoiceCountId = "1005";

    public const int BaseChoiceCount = 3;

    private const string KeyPrefix = "TitleEnhance.";
    private const string DiamondKey = KeyPrefix + "Diamond";
    private const string LegacyGoldKey = KeyPrefix + "Gold";

    private static readonly string[] AllIds =
    {
        EfficiencyId,
        MagicId,
        ProductivityId,
        DiscardId,
        BarrierId,
        ChoiceCountId
    };

    public static int GetDiamond()
    {
        MigrateLegacyGoldToDiamond();
        return Mathf.Max(0, PlayerPrefs.GetInt(DiamondKey, 0));
    }

    public static void SetDiamond(int amount)
    {
        PlayerPrefs.SetInt(DiamondKey, Mathf.Max(0, amount));
        PlayerPrefs.Save();
    }

    public static void AddDiamond(int amount)
    {
        if (amount <= 0)
            return;

        SetDiamond(GetDiamond() + amount);
    }

    public static bool TrySpendDiamond(int amount)
    {
        if (amount <= 0)
            return true;

        int diamond = GetDiamond();
        if (diamond < amount)
            return false;

        SetDiamond(diamond - amount);
        return true;
    }

    private static void MigrateLegacyGoldToDiamond()
    {
        if (!PlayerPrefs.HasKey(LegacyGoldKey))
            return;

        int legacy = Mathf.Max(0, PlayerPrefs.GetInt(LegacyGoldKey, 0));
        if (legacy > 0 && !PlayerPrefs.HasKey(DiamondKey))
            PlayerPrefs.SetInt(DiamondKey, legacy);

        PlayerPrefs.DeleteKey(LegacyGoldKey);
        PlayerPrefs.Save();
    }

    public static int GetLevel(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return 0;

        int maxLevel = GetMaxLevel(id);
        return Mathf.Clamp(PlayerPrefs.GetInt(KeyPrefix + id, 0), 0, maxLevel);
    }

    public static void SetLevel(string id, int level)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        int maxLevel = GetMaxLevel(id);
        PlayerPrefs.SetInt(KeyPrefix + id, Mathf.Clamp(level, 0, maxLevel));
        PlayerPrefs.Save();
    }

    public static void ClearAllLevels()
    {
        for (int i = 0; i < AllIds.Length; i++)
            PlayerPrefs.DeleteKey(KeyPrefix + AllIds[i]);

        // 이전 문자열 id 키도 정리
        PlayerPrefs.DeleteKey(KeyPrefix + "efficiency");
        PlayerPrefs.DeleteKey(KeyPrefix + "magic");
        PlayerPrefs.DeleteKey(KeyPrefix + "productivity");
        PlayerPrefs.DeleteKey(KeyPrefix + "discard");
        PlayerPrefs.DeleteKey(KeyPrefix + "barrier");
        PlayerPrefs.DeleteKey(KeyPrefix + "choice_count");

        PlayerPrefs.Save();
    }

    public static void ClearDiamonds()
    {
        PlayerPrefs.DeleteKey(DiamondKey);
        PlayerPrefs.DeleteKey(LegacyGoldKey);
        PlayerPrefs.Save();
    }

    public static int GetMaxLevel(string id)
    {
        if (GameDataRepository.TryGetReinforce(id, out ReinforceData data) && data.MaxLevel > 0)
            return data.MaxLevel;

        return 1;
    }

    public static string GetName(string id)
    {
        if (GameDataRepository.TryGetReinforce(id, out ReinforceData data) &&
            !string.IsNullOrWhiteSpace(data.Name))
        {
            return data.Name;
        }

        return string.Empty;
    }

    /// <summary>
    /// currentLevel → currentLevel+1 로 올릴 때 필요한 골드.
    /// </summary>
    public static int GetRequireCost(string id, int currentLevel)
    {
        if (!GameDataRepository.TryGetReinforce(id, out ReinforceData data) ||
            data.Requires == null ||
            data.Requires.Length == 0)
        {
            return 0;
        }

        if (currentLevel < 0 || currentLevel >= data.MaxLevel)
            return 0;

        int index = Mathf.Clamp(currentLevel, 0, data.Requires.Length - 1);
        return Mathf.Max(0, data.Requires[index]);
    }

    /// <summary>
    /// 해당 레벨에서 적용되는 수치. 레벨 0은 선택지 증가만 기본 3, 나머지는 0.
    /// </summary>
    public static int GetValueAtLevel(string id, int level)
    {
        if (level <= 0)
        {
            if (id == ChoiceCountId)
                return BaseChoiceCount;
            return 0;
        }

        if (!GameDataRepository.TryGetReinforce(id, out ReinforceData data) ||
            data.Values == null ||
            data.Values.Length == 0)
        {
            return 0;
        }

        int index = Mathf.Clamp(level - 1, 0, data.Values.Length - 1);
        return data.Values[index];
    }

    public static int GetEnhanceChoiceCount()
    {
        return GetValueAtLevel(ChoiceCountId, GetLevel(ChoiceCountId));
    }

    public static int GetEfficiencyPercent(int level) => GetValueAtLevel(EfficiencyId, level);
    public static int GetMagicPercent(int level) => GetValueAtLevel(MagicId, level);
    public static int GetProductivityPercent(int level) => GetValueAtLevel(ProductivityId, level);
    public static int GetDiscardCount(int level) => GetValueAtLevel(DiscardId, level);
    public static int GetBarrierPercent(int level) => GetValueAtLevel(BarrierId, level);
    public static int GetChoiceCount(int level) => GetValueAtLevel(ChoiceCountId, level);

    public static int CurrentEfficiencyPercent => GetEfficiencyPercent(GetLevel(EfficiencyId));
    public static int CurrentMagicPercent => GetMagicPercent(GetLevel(MagicId));
    public static int CurrentProductivityPercent => GetProductivityPercent(GetLevel(ProductivityId));
    public static int CurrentDiscardCount => GetDiscardCount(GetLevel(DiscardId));
    public static int CurrentBarrierPercent => GetBarrierPercent(GetLevel(BarrierId));

    public static int ApplyEfficiencyBonus(int baseAmount)
    {
        if (baseAmount <= 0)
            return 0;

        float multiplier = 1f + CurrentEfficiencyPercent / 100f;
        return Mathf.Max(0, Mathf.RoundToInt(baseAmount * multiplier));
    }

    /// <summary>
    /// 상점 등급 가중치에 생산성 보너스를 적용한다.
    /// 생산성 레벨 비율 t(= 현재 레벨 / 최대 레벨)만큼 3~5등급 확률이 (1 + t)배가 되고,
    /// 1등급은 늘어난 양의 8배만큼 줄고, 그 차이(7배)는 2등급이 가져간다. 총합은 유지된다. (MAX일 때 3~5등급 +100%)
    /// </summary>
    public static float[] ApplyProductivityToGradeWeights(float[] source)
    {
        if (source == null || source.Length < 5)
            return source;

        float[] chances = new float[source.Length];
        for (int i = 0; i < source.Length; i++)
            chances[i] = Mathf.Max(0f, source[i]);

        int maxLevel = GetMaxLevel(ProductivityId);
        if (maxLevel <= 0)
            return chances;

        float t = Mathf.Clamp01(GetLevel(ProductivityId) / (float)maxLevel);
        if (t <= 0f)
            return chances;

        float added = 0f;
        for (int i = 2; i < 5; i++)
        {
            float increase = chances[i] * t;
            chances[i] += increase;
            added += increase;
        }

        // 1등급은 늘어난 양의 8배만큼 줄이고, 늘어난 양을 뺀 차이(7배)는 2등급에 더한다.
        float lowShift = Mathf.Min(chances[0], added * 8f);
        chances[0] -= lowShift;
        chances[1] += lowShift - Mathf.Min(lowShift, added);
        return chances;
    }
}
