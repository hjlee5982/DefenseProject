using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class EnhanceStatusView : MonoBehaviour
{
    private const string ZeroColor = "#6F5B48";
    private const string ValueColor = "#1B5E20";
    private const string HeaderColor = "#4A2A0E";
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private TextMeshProUGUI buffLabel;
    [SerializeField] private TextMeshProUGUI enhanceLabel;
    [SerializeField] private float refreshInterval = 0.25f;

    private readonly StringBuilder builder = new();
    private float nextRefreshTime;

    private void Awake()
    {
        ResolveInventory();
        ResolveLabels();
    }

    // The inventory object is deactivated during combat, so inactive objects must be included in the lookup.
    private void ResolveInventory()
    {
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        nextRefreshTime = 0f;
        Refresh();
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void ResolveLabels()
    {
        if (buffLabel == null)
            buffLabel = FindLabel("Option_0");
        if (enhanceLabel == null)
            enhanceLabel = FindLabel("Option_1");
    }

    private TextMeshProUGUI FindLabel(string cardName)
    {
        Transform card = transform.Find(cardName);
        return card != null ? card.GetComponentInChildren<TextMeshProUGUI>(true) : null;
    }

    private void Refresh()
    {
        ResolveInventory();
        ResolveLabels();

        IReadOnlyList<Item> items = inventoryManager != null
            ? inventoryManager.GetBagItems()
            : null;

        CombatBuffUtility.Bonuses buffs = CombatBuffUtility.Collect(items);
        EnhanceBonusState enhance = GameManager.Instance != null
            ? GameManager.Instance.EnhanceBonuses
            : null;

        if (buffLabel != null)
            buffLabel.text = BuildBuffText(buffs);

        if (enhanceLabel != null)
            enhanceLabel.text = BuildEnhanceText(enhance);
    }

    private string BuildBuffText(CombatBuffUtility.Bonuses buffs)
    {
        builder.Clear();
        AppendHeader("버프");
        AppendBuffLine("공격력", buffs.ExtraDamagePercent);
        AppendBuffLine("사거리", buffs.ExtraAddRangePercent);
        AppendBuffLine("공격속도", buffs.ExtraFireIntervalPercent);
        AppendBuffLine("치명타 확률", buffs.CriticalPercent);
        return builder.ToString();
    }

    private string BuildEnhanceText(EnhanceBonusState enhance)
    {
        builder.Clear();
        AppendHeader("강화");

        builder.Append("<pos=38%><color=").Append(HeaderColor).Append(">공격력</color>");
        builder.Append("<pos=70%><color=").Append(HeaderColor).Append(">공격속도</color>");
        builder.AppendLine();

        if (enhance == null)
        {
            AppendEnhanceRow("물", "#1F4FA3", 0f, 0f);
            AppendEnhanceRow("바위", "#5A4020", 0f, 0f);
            AppendEnhanceRow("얼음", "#0F6E8A", 0f, 0f);
            AppendEnhanceRow("바람", "#1B7A42", 0f, 0f);
            AppendEnhanceRow("화염", "#B02A14", 0f, 0f);
            AppendEnhanceRow("어둠", "#4A2780", 0f, 0f);
            AppendEnhanceRow("전체", "#2A2A2A", 0f, 0f);
            return builder.ToString();
        }

        AppendEnhanceRow("물", "#1F4FA3", enhance.AttackUpWater, enhance.SpeedUpWater);
        AppendEnhanceRow("바위", "#5A4020", enhance.AttackUpRock, enhance.SpeedUpRock);
        AppendEnhanceRow("얼음", "#0F6E8A", enhance.AttackUpIce, enhance.SpeedUpIce);
        AppendEnhanceRow("바람", "#1B7A42", enhance.AttackUpWind, enhance.SpeedUpWind);
        AppendEnhanceRow("화염", "#B02A14", enhance.AttackUpFire, enhance.SpeedUpFire);
        AppendEnhanceRow("어둠", "#4A2780", enhance.AttackUpDark, enhance.SpeedUpDark);
        AppendEnhanceRow("전체", "#2A2A2A", enhance.AttackUpAll, enhance.AttackSpeedUpAll);
        return builder.ToString();
    }

    private void AppendHeader(string title)
    {
        // Small spacer above the header pushes it down toward the divider image.
        builder.AppendLine("<size=25%> </size>");
        builder.Append("<size=115%><color=").Append(HeaderColor).Append('>');
        builder.Append(title);
        builder.AppendLine("</color></size>");
        // Small spacer below the header leaves room for the divider image.
        builder.AppendLine("<size=25%> </size>");
    }

    private void AppendBuffLine(string name, float percent)
    {
        builder.Append("<pos=0%>");
        builder.Append(name);
        builder.Append("<pos=62%>");
        AppendPercent(percent);
        builder.AppendLine();
    }

    private void AppendEnhanceRow(string name, string nameColor, float attackPercent, float speedPercent)
    {
        builder.Append("<pos=0%><color=").Append(nameColor).Append('>').Append(name).Append("</color>");
        builder.Append("<pos=38%>");
        AppendPercent(attackPercent);
        builder.Append("<pos=70%>");
        AppendPercent(speedPercent);
        builder.AppendLine();
    }

    private void AppendPercent(float percent)
    {
        bool isZero = Mathf.Abs(percent) < 0.0001f;
        builder.Append("<color=").Append(isZero ? ZeroColor : ValueColor).Append('>');
        if (!isZero && percent > 0f)
            builder.Append('+');
        builder.Append(percent.ToString("0.##"));
        builder.Append("%</color>");
    }
}
