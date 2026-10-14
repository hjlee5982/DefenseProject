using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class CombatDebugView : MonoBehaviour
{
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 20f;
    [SerializeField] private float refreshInterval = 0.25f;

    private readonly StringBuilder builder = new();
    private float nextRefreshTime;

    private void Awake()
    {
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>();

        EnsureLabel();
        ApplyLabelStyle();
    }

    private void OnValidate()
    {
        if (label != null)
            ApplyLabelStyle();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void EnsureLabel()
    {
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);

        if (label == null)
        {
            GameObject textObject = new GameObject("DebugText", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 8f);
            rect.offsetMax = new Vector2(-12f, -8f);

            label = textObject.AddComponent<TextMeshProUGUI>();
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        ApplyLabelStyle();
    }

    private void ApplyLabelStyle()
    {
        if (label == null) return;

        if (font != null)
            label.font = font;

        label.fontSize = fontSize;
        label.richText = true;
        label.enableWordWrapping = false;
    }

    private void Refresh()
    {
        if (label == null) return;

        builder.Clear();
        IReadOnlyList<Item> items = inventoryManager != null
            ? inventoryManager.GetBagItems()
            : null;

        CombatBuffUtility.Bonuses buffs = CombatBuffUtility.Collect(items);
        EnhanceBonusState enhance = GameManager.Instance != null
            ? GameManager.Instance.EnhanceBonuses
            : null;

        builder.AppendLine("[Weapons]");
        AppendWeaponHeader();
        bool hasWeapon = false;
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null || item.IsBuff || item.ProjectilePrefab == null) continue;

                hasWeapon = true;
                AppendWeaponLine(item, buffs, enhance);
            }
        }

        if (!hasWeapon)
            builder.AppendLine("(none)");

        builder.AppendLine();
        builder.AppendLine("[Buffs]");
        AppendPercentLine("ExtraDamage", buffs.ExtraDamagePercent);
        AppendPercentLine("ExtraRange", buffs.ExtraAddRangePercent);
        AppendPercentLine("ExtraFireInterval", buffs.ExtraFireIntervalPercent);
        AppendPercentLine("Critical", buffs.CriticalPercent);

        builder.AppendLine();
        builder.AppendLine("[Enhance]");
        if (enhance == null)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            AppendEnhanceElementLine("Water", enhance.AttackUpWater, enhance.SpeedUpWater);
            AppendEnhanceElementLine("Rock", enhance.AttackUpRock, enhance.SpeedUpRock);
            AppendEnhanceElementLine("Ice", enhance.AttackUpIce, enhance.SpeedUpIce);
            AppendEnhanceElementLine("Wind", enhance.AttackUpWind, enhance.SpeedUpWind);
            AppendEnhanceElementLine("Fire", enhance.AttackUpFire, enhance.SpeedUpFire);
            AppendEnhanceElementLine("Dark", enhance.AttackUpDark, enhance.SpeedUpDark);
            AppendPercentLine("AllAttack", enhance.AttackUpAll);
            AppendPercentLine("AllSpeed", enhance.AttackSpeedUpAll);
        }

        label.text = builder.ToString();
    }

    private void AppendWeaponHeader()
    {
        builder.Append("<color=#000000>");
        builder.Append("<pos=0%>Name");
        builder.Append("<pos=36%>G");
        builder.Append("<pos=44%>ATK");
        builder.Append("<pos=58%>RNG");
        builder.Append("<pos=74%>INT");
        builder.Append("</color>");
        builder.AppendLine();
    }

    private void AppendWeaponLine(
        Item item,
        CombatBuffUtility.Bonuses buffs,
        EnhanceBonusState enhance)
    {
        int finalDamage = CombatStatCalculator.GetFinalDamage(item, buffs, enhance);
        float finalRange = CombatStatCalculator.GetFinalRange(item, buffs);
        float finalInterval = CombatStatCalculator.GetFinalFireInterval(item, buffs, enhance);

        string hex = ColorUtility.ToHtmlStringRGB(item.GradeColor);
        builder.Append("<color=#");
        builder.Append(hex);
        builder.Append('>');
        builder.Append("<pos=0%>");
        builder.Append(item.DisplayName);
        builder.Append("<pos=36%>G");
        builder.Append(item.Grade);
        builder.Append("<pos=44%>");
        builder.Append(finalDamage);
        builder.Append("<pos=58%>");
        builder.Append(finalRange.ToString("0.##"));
        builder.Append("<pos=74%>");
        builder.Append(finalInterval.ToString("0.##"));
        builder.Append("</color>");
        builder.AppendLine();
    }

    private void AppendPercentLine(string name, float percent)
    {
        builder.Append("<pos=0%>");
        builder.Append(name);
        builder.Append("<pos=44%>");
        builder.Append(percent.ToString("0.##"));
        builder.Append('%');
        builder.AppendLine();
    }

    private void AppendEnhanceElementLine(string name, float attackPercent, float speedPercent)
    {
        builder.Append("<pos=0%>");
        builder.Append(name);
        builder.Append(" ATK");
        builder.Append("<pos=44%>");
        builder.Append(attackPercent.ToString("0.##"));
        builder.Append('%');
        builder.AppendLine();

        builder.Append("<pos=0%>");
        builder.Append(name);
        builder.Append(" SPD");
        builder.Append("<pos=44%>");
        builder.Append(speedPercent.ToString("0.##"));
        builder.Append('%');
        builder.AppendLine();
    }
}
