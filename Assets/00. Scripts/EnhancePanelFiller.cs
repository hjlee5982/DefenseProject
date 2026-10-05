using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class EnhancePanelFiller
{
    private static readonly Regex ValueTokenRegex = new(
        @"\{\s*value(?<index>\d+)?\s*\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<EnhanceOptionData> FillRandomOptions(GameObject enhancePanel, int optionCount = 3)
    {
        if (enhancePanel == null)
            return new List<EnhanceOptionData>();

        List<EnhanceOptionData> picked = GameDataRepository.PickRandomEnhanceOptions(optionCount);
        for (int i = 0; i < optionCount; i++)
        {
            Transform optionRoot = enhancePanel.transform.Find($"Option_{i}");
            if (optionRoot == null) continue;

            if (i < picked.Count)
                ApplyOption(optionRoot, picked[i]);
            else
                ClearOption(optionRoot);
        }

        return picked;
    }

    private static void ApplyOption(Transform optionRoot, EnhanceOptionData option)
    {
        List<EnhanceOptionEffectData> effects = GameDataRepository.GetEnhanceEffects(option.Id);
        List<string> values = CollectValues(effects);

        string desc = BuildDescription(option.DescKey, values);
        SetDesc(optionRoot, desc);
        SetIcon(optionRoot, option.Icon);
    }

    private static void ClearOption(Transform optionRoot)
    {
        SetDesc(optionRoot, string.Empty);
        SetIcon(optionRoot, null);
    }

    private static List<string> CollectValues(List<EnhanceOptionEffectData> effects)
    {
        List<string> values = new();
        for (int i = 0; i < effects.Count; i++)
        {
            string value = effects[i].Value;
            if (string.IsNullOrWhiteSpace(value)) continue;
            values.Add(value.Trim());
        }

        return values;
    }

    private static string BuildDescription(string descKey, List<string> values)
    {
        if (!GameDataRepository.TryGetLocalization(descKey, out string template, preferredLang: "KR"))
            return descKey ?? string.Empty;

        return ValueTokenRegex.Replace(template, match =>
        {
            Group indexGroup = match.Groups["index"];
            int index = 0;
            if (indexGroup.Success &&
                int.TryParse(indexGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                index = parsed;
            }

            if (index < 0 || index >= values.Count)
                return match.Value;

            return values[index];
        });
    }

    private static void SetDesc(Transform optionRoot, string text)
    {
        Transform desc = optionRoot.Find("DescFrame/Desc");
        if (desc == null) return;

        TextMeshProUGUI tmp = desc.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = text ?? string.Empty;
    }

    private static void SetIcon(Transform optionRoot, string iconKey)
    {
        Transform icon = optionRoot.Find("IconFrame/Icon");
        if (icon == null) return;

        Image image = icon.GetComponent<Image>();
        if (image == null) return;

        if (string.IsNullOrWhiteSpace(iconKey))
        {
            image.sprite = null;
            return;
        }

        image.sprite = EnhanceIconLoader.Load(iconKey);
        image.enabled = image.sprite != null;
    }
}
