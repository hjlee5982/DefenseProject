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
    /// 3~5등급 합계 +P, 1~2등급에서 각각 P/2 감소. 총합은 유지된다.
    /// </summary>
    public static float[] ApplyProductivityToGradeWeights(float[] source)
    {
        if (source == null || source.Length < 5)
            return source;

        float[] chances = new float[source.Length];
        for (int i = 0; i < source.Length; i++)
            chances[i] = Mathf.Max(0f, source[i]);

        float bonus = CurrentProductivityPercent;
        if (bonus <= 0f)
            return chances;

        float highAdd = bonus / 3f;
        float lowSub = bonus / 2f;

        chances[0] -= lowSub;
        chances[1] -= lowSub;
        chances[2] += highAdd;
        chances[3] += highAdd;
        chances[4] += highAdd;

        for (int i = 0; i < 2; i++)
        {
            if (chances[i] >= 0f)
                continue;

            float deficit = -chances[i];
            chances[i] = 0f;

            float highTotal = chances[2] + chances[3] + chances[4];
            if (highTotal <= 0f)
                continue;

            for (int h = 2; h < 5; h++)
            {
                float share = chances[h] / highTotal;
                chances[h] = Mathf.Max(0f, chances[h] - deficit * share);
            }
        }

        return chances;
    }
}
