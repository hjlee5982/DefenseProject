using System.Collections.Generic;
using UnityEngine;

public static class CombatBuffUtility
{
    public struct Bonuses
    {
        public float ExtraDamagePercent;
        public float ExtraFireIntervalPercent;
        public float CriticalPercent;
        public float ExtraAddRangePercent;
    }

    public const float MinFireInterval = 0.05f;

    public static Bonuses Collect(IReadOnlyList<Item> items)
    {
        Bonuses buffs = default;
        if (items == null) return buffs;

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || !item.IsBuff) continue;

            float contribution = item.BuffContributionPercent;
            if (contribution <= 0f) continue;

            switch (item.BuffOption)
            {
                case Item.BuffOptionType.ExtraDamage:
                    buffs.ExtraDamagePercent += contribution;
                    break;
                case Item.BuffOptionType.ExtraFireInterval:
                    buffs.ExtraFireIntervalPercent += contribution;
                    break;
                case Item.BuffOptionType.Critical:
                    buffs.CriticalPercent += contribution;
                    break;
                case Item.BuffOptionType.ExtraAddRange:
                    buffs.ExtraAddRangePercent += contribution;
                    break;
            }
        }

        return buffs;
    }

    public static float ApplyIncreasePercent(float baseValue, float percent)
    {
        return baseValue * (1f + percent / 100f);
    }

    public static float ApplyFireIntervalBonus(float baseValue, float reducePercent)
    {
        float value = baseValue * (1f - reducePercent / 100f);
        return Mathf.Max(MinFireInterval, value);
    }
}
