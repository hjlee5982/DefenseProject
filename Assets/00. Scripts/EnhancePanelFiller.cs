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

    public const int MaxOptionSlots = 5;

    public static List<EnhanceOptionData> FillRandomOptions(GameObject enhancePanel, int optionCount = 3)
    {
        if (enhancePanel == null)
            return new List<EnhanceOptionData>();

        optionCount = Mathf.Clamp(optionCount, 1, MaxOptionSlots);
        List<EnhanceOptionData> picked = GameDataRepository.PickRandomEnhanceOptions(optionCount);
        LayoutOptions(enhancePanel, picked.Count);

        for (int i = 0; i < MaxOptionSlots; i++)
        {
            Transform optionRoot = enhancePanel.transform.Find($"Option_{i}");
            if (optionRoot == null) continue;

            bool show = i < picked.Count;
            optionRoot.gameObject.SetActive(show);
            if (show)
                ApplyOption(optionRoot, picked[i]);
            else
                ClearOption(optionRoot);
        }

        return picked;
    }

    public static void LayoutOptions(GameObject enhancePanel, int visibleCount)
    {
        if (enhancePanel == null || visibleCount <= 0) return;

        visibleCount = Mathf.Clamp(visibleCount, 1, MaxOptionSlots);

        const float DefaultCardHeight = 320f;
        const float DefaultFirstCenterY = -600f;
        const float DefaultStep = 387.88f;
        const float DefaultPanelHeight = 1722.99f;
        const float Gap = 56f;
        const float FirstTop = 450f;
        // B.png 하단 마감(노트 밑단)이 Simple 스케일에서 약 180px라서, 마지막 카드가 그 위로 오게 띄운다.
        const float BottomPadding = 200f;

        float cardHeight;
        float firstCenterY;
        float step;
        float panelHeight;
        if (visibleCount <= 3)
        {
            cardHeight = DefaultCardHeight;
            firstCenterY = DefaultFirstCenterY;
            step = DefaultStep;
            panelHeight = DefaultPanelHeight;
        }
        else
        {
            cardHeight = visibleCount == 4 ? 290f : 250f;
            firstCenterY = -(FirstTop + cardHeight * 0.5f);
            step = cardHeight + Gap;
            float lastBottom = FirstTop + cardHeight * visibleCount + Gap * (visibleCount - 1);
            panelHeight = lastBottom + BottomPadding;
        }

        ResizeNotebook(enhancePanel, panelHeight);

        float scale = cardHeight / DefaultCardHeight;
        for (int i = 0; i < MaxOptionSlots; i++)
        {
            Transform optionRoot = enhancePanel.transform.Find($"Option_{i}");
            if (optionRoot == null) continue;

            RectTransform optionRect = optionRoot as RectTransform;
            if (optionRect != null)
            {
                optionRect.anchoredPosition = new Vector2(optionRect.anchoredPosition.x, firstCenterY - step * i);
                optionRect.sizeDelta = new Vector2(optionRect.sizeDelta.x, cardHeight);
            }

            SetSize(optionRoot.Find("Button") as RectTransform, 950f, cardHeight);
            SetSize(optionRoot.Find("IconFrame") as RectTransform, 275f * scale, 275f * scale);
            SetSize(optionRoot.Find("IconFrame/Icon") as RectTransform, 200f * scale, 200f * scale);
            SetSize(optionRoot.Find("DescFrame") as RectTransform, 706.49f, 275f * scale);
            SetSize(optionRoot.Find("DescFrame/Desc") as RectTransform, 650f, 210f * scale);
        }
    }

    private static void ResizeNotebook(GameObject enhancePanel, float height)
    {
        const float TopHeight = 152.7f;

        RectTransform panelRect = enhancePanel.transform as RectTransform;
        if (panelRect != null)
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, height);

        Transform bottom = enhancePanel.transform.Find("Bottom");
        RectTransform bottomRect = bottom as RectTransform;
        if (bottomRect == null) return;

        float bottomHeight = Mathf.Max(1f, height - TopHeight);
        bottomRect.sizeDelta = new Vector2(bottomRect.sizeDelta.x, bottomHeight);
        bottomRect.anchoredPosition = new Vector2(bottomRect.anchoredPosition.x, -(TopHeight + bottomHeight * 0.5f));
    }

    private static void SetSize(RectTransform rect, float width, float height)
    {
        if (rect == null) return;
        rect.sizeDelta = new Vector2(width, height);
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
