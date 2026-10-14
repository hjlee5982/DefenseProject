using UnityEngine;

/// <summary>
/// Final combat stats for a weapon:
///
/// ATK = baseDamage
///     * (1 + ExtraDamage% / 100)
///     * (1 + (AttackUp + AttackUp_Element)% / 100)
///
/// RNG = baseRange * (1 + ExtraRange% / 100)
///
/// INT = baseFireInterval
///     * (1 - ExtraFireInterval% / 100)
///     / (1 + (AttackSpeedUp + SpeedUp_Element)% / 100)
///
/// CRIT chance = Critical% / 100  (damage x2 on proc)
///
/// base* values already include item grade scaling (AddRange/AddDamage/AddFireInterval).
/// Grimoire maps to Dark enhance bonuses.
/// </summary>
public static class CombatStatCalculator
{
    public static int GetFinalDamage(
        Item item,
        CombatBuffUtility.Bonuses buffs,
        EnhanceBonusState enhance)
    {
        if (item == null) return 0;

        float value = item.Damage;
        value = CombatBuffUtility.ApplyIncreasePercent(value, buffs.ExtraDamagePercent);

        float enhanceAttackPercent = enhance != null ? enhance.GetAttackPercent(item.Category) : 0f;
        value = CombatBuffUtility.ApplyIncreasePercent(value, enhanceAttackPercent);

        return Mathf.Max(0, Mathf.RoundToInt(value));
    }

    public static float GetFinalRange(
        Item item,
        CombatBuffUtility.Bonuses buffs)
    {
        if (item == null) return 0f;
        return CombatBuffUtility.ApplyIncreasePercent(item.Range, buffs.ExtraAddRangePercent);
    }

    public static float GetFinalFireInterval(
        Item item,
        CombatBuffUtility.Bonuses buffs,
        EnhanceBonusState enhance)
    {
        if (item == null) return CombatBuffUtility.MinFireInterval;

        float value = CombatBuffUtility.ApplyFireIntervalBonus(
            item.FireInterval, buffs.ExtraFireIntervalPercent);

        float enhanceSpeedPercent = enhance != null ? enhance.GetSpeedPercent(item.Category) : 0f;
        if (enhanceSpeedPercent > 0f)
            value /= 1f + enhanceSpeedPercent / 100f;

        return Mathf.Max(CombatBuffUtility.MinFireInterval, value);
    }

    public static float GetCriticalChance(CombatBuffUtility.Bonuses buffs)
    {
        return Mathf.Clamp01(buffs.CriticalPercent / 100f);
    }
}
