using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬의 강화 UI. 강화 버튼 → 6개 선택지 목록 → 선택지 상세(강화/취소) 흐름을 담당한다.
/// Name/MaxLevel/Require/Value는 GameData Reinforce 시트에서 가져온다.
/// </summary>
public class TitleEnhanceUI : MonoBehaviour
{
    private static readonly string[] DefaultEntryIds =
    {
        TitleEnhanceProgress.EfficiencyId,
        TitleEnhanceProgress.MagicId,
        TitleEnhanceProgress.ProductivityId,
        TitleEnhanceProgress.DiscardId,
        TitleEnhanceProgress.BarrierId,
        TitleEnhanceProgress.ChoiceCountId
    };

    [Serializable]
    public class Entry
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Min(1)] public int maxLevel = 5;
        public int level;
        public Button button;
        public TMP_Text nameText;
        public TMP_Text levelText;
        public Image iconImage;
    }

    [Header("Open")]
    [SerializeField] private Button openButton;

    [Header("Main UI")]
    [Tooltip("강화 패널이 열릴 때 숨길 타이틀 화면 UI (타이틀 이미지, 메인 메뉴 등).")]
    [SerializeField] private GameObject[] mainUiRoots;

    [Header("List Panel")]
    [SerializeField] private GameObject listRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text ownedGoldText;
    [SerializeField] private Entry[] entries;

    [Header("Icons")]
    [Tooltip("EnhanceIcons 스프라이트. 옵션 순서(1000~1005)와 동일.")]
    [SerializeField] private Sprite[] reinforceIcons;

    [Header("Detail Panel")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailLevelText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Image detailIconImage;
    [SerializeField] private Button enhanceButton;
    [SerializeField] private TMP_Text enhanceButtonText;
    [SerializeField] private Image enhanceCostIcon;
    [SerializeField] private TMP_Text enhanceCostText;
    [SerializeField] private Button cancelButton;

    private int selectedIndex = -1;

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(OpenList);
        if (closeButton != null) closeButton.onClick.AddListener(CloseList);
        if (enhanceButton != null) enhanceButton.onClick.AddListener(EnhanceSelected);
        if (cancelButton != null) cancelButton.onClick.AddListener(CloseDetail);

        for (int i = 0; i < entries.Length; i++)
        {
            int index = i;
            if (entries[i].button != null)
                entries[i].button.onClick.AddListener(() => OpenDetail(index));
        }

        if (listRoot != null) listRoot.SetActive(false);
        if (detailRoot != null) detailRoot.SetActive(false);

        ApplyReinforceData();
        ResolveIconImages();
        ApplyIcons();
        LoadLevels();
        RefreshList();
    }

    private void ResolveIconImages()
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (entry.iconImage != null || entry.button == null)
                continue;

            Transform icon = entry.button.transform.Find("IconFrame/Icon");
            if (icon != null)
                entry.iconImage = icon.GetComponent<Image>();
        }

        if (detailIconImage == null && detailRoot != null)
        {
            Transform icon = detailRoot.transform.Find("Panel/IconFrame/Icon");
            if (icon != null)
                detailIconImage = icon.GetComponent<Image>();
        }
    }

    private void ApplyIcons()
    {
        if (entries == null || reinforceIcons == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            Sprite sprite = GetReinforceIcon(i);
            if (sprite == null)
                continue;

            Entry entry = entries[i];
            if (entry.iconImage == null)
                continue;

            entry.iconImage.sprite = sprite;
            entry.iconImage.preserveAspect = true;
            entry.iconImage.enabled = true;
        }
    }

    private Sprite GetReinforceIcon(int index)
    {
        if (reinforceIcons == null || index < 0 || index >= reinforceIcons.Length)
            return null;

        return reinforceIcons[index];
    }

    private void ApplyReinforceData()
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.id) && i < DefaultEntryIds.Length)
                entry.id = DefaultEntryIds[i];

            if (!GameDataRepository.TryGetReinforce(entry.id, out ReinforceData data))
                continue;

            if (!string.IsNullOrWhiteSpace(data.Name))
                entry.displayName = data.Name;

            entry.maxLevel = Mathf.Max(1, data.MaxLevel);
        }
    }

    private void LoadLevels()
    {
        foreach (Entry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.id))
                continue;

            entry.maxLevel = TitleEnhanceProgress.GetMaxLevel(entry.id);
            entry.level = TitleEnhanceProgress.GetLevel(entry.id);
        }
    }

    /// <summary>
    /// 인스펙터/디버그용. PlayerPrefs 레벨과 소지 다이아몬드를 모두 초기화한다.
    /// </summary>
    public void ResetAllLevels()
    {
        TitleEnhanceProgress.ClearAllLevels();
        TitleEnhanceProgress.ClearDiamonds();
        foreach (Entry entry in entries)
            entry.level = 0;

        RefreshList();
        RefreshOwnedGold();
        if (selectedIndex >= 0)
            RefreshDetail();
    }

    private void OpenList()
    {
        ApplyReinforceData();
        LoadLevels();
        RefreshList();
        RefreshOwnedGold();
        SetMainUiVisible(false);
        if (detailRoot != null) detailRoot.SetActive(false);
        if (listRoot != null) listRoot.SetActive(true);
    }

    private void CloseList()
    {
        selectedIndex = -1;
        if (detailRoot != null) detailRoot.SetActive(false);
        if (listRoot != null) listRoot.SetActive(false);
        SetMainUiVisible(true);
    }

    private void OpenDetail(int index)
    {
        selectedIndex = index;
        RefreshDetail();
        if (listRoot != null) listRoot.SetActive(false);
        if (detailRoot != null) detailRoot.SetActive(true);
    }

    private void CloseDetail()
    {
        selectedIndex = -1;
        if (detailRoot != null) detailRoot.SetActive(false);
        if (listRoot != null)
        {
            RefreshList();
            RefreshOwnedGold();
            listRoot.SetActive(true);
        }
    }

    private void SetMainUiVisible(bool visible)
    {
        if (mainUiRoots == null)
            return;

        for (int i = 0; i < mainUiRoots.Length; i++)
        {
            if (mainUiRoots[i] != null)
                mainUiRoots[i].SetActive(visible);
        }
    }

    private void EnhanceSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= entries.Length)
            return;

        Entry entry = entries[selectedIndex];
        if (entry.level >= entry.maxLevel)
            return;

        int cost = GetEnhanceCost(entry);
        if (!TitleEnhanceProgress.TrySpendDiamond(cost))
            return;

        entry.level++;
        if (!string.IsNullOrWhiteSpace(entry.id))
            TitleEnhanceProgress.SetLevel(entry.id, entry.level);

        RefreshList();
        RefreshDetail();
    }

    private void RefreshList()
    {
        foreach (Entry entry in entries)
        {
            if (entry.nameText != null) entry.nameText.text = entry.displayName;
            if (entry.levelText != null) entry.levelText.text = FormatLevel(entry);
        }

        RefreshOwnedGold();
    }

    private void RefreshOwnedGold()
    {
        if (ownedGoldText == null)
            return;

        ownedGoldText.text = TitleEnhanceProgress.GetDiamond()
            .ToString("N0", CultureInfo.InvariantCulture);
    }

    private void RefreshDetail()
    {
        if (selectedIndex < 0 || selectedIndex >= entries.Length)
            return;

        Entry entry = entries[selectedIndex];
        if (detailTitleText != null) detailTitleText.text = entry.displayName;
        if (detailLevelText != null) detailLevelText.text = FormatDetailLevel(entry);
        if (detailDescriptionText != null)
        {
            detailDescriptionText.richText = true;
            detailDescriptionText.text = BuildDescription(entry);
        }

        if (detailIconImage != null)
        {
            Sprite sprite = GetReinforceIcon(selectedIndex);
            if (sprite != null)
            {
                detailIconImage.sprite = sprite;
                detailIconImage.preserveAspect = true;
                detailIconImage.enabled = true;
            }
        }

        bool canEnhance = entry.level < entry.maxLevel &&
                          TitleEnhanceProgress.GetDiamond() >= GetEnhanceCost(entry);
        if (enhanceButton != null) enhanceButton.interactable = canEnhance;
        RefreshEnhanceButtonText(entry);
    }

    private void RefreshEnhanceButtonText(Entry entry)
    {
        if (enhanceButtonText != null)
            enhanceButtonText.text = "강화";

        int cost = GetEnhanceCost(entry);
        string costText = cost.ToString("N0", CultureInfo.InvariantCulture);

        if (enhanceCostText != null)
            enhanceCostText.text = $": {costText}";

        if (enhanceCostIcon != null)
            enhanceCostIcon.enabled = true;
    }

    private static int GetEnhanceCost(Entry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entry.level >= entry.maxLevel)
            return 0;

        return TitleEnhanceProgress.GetRequireCost(entry.id, entry.level);
    }

    private static string FormatLevel(Entry entry)
    {
        return entry.level >= entry.maxLevel ? "MAX" : $"{entry.level} / {entry.maxLevel}";
    }

    private static string FormatDetailLevel(Entry entry)
    {
        if (entry.level >= entry.maxLevel)
            return "Level MAX";

        return $"Level {entry.level} / {entry.maxLevel}";
    }

    private const string NextValueColorTag = "<color=#FF0000>";
    private const string NextValueColorClose = "</color>";

    private static string HighlightNext(string value)
    {
        return $"{NextValueColorTag}{value}{NextValueColorClose}";
    }

    private static string BuildDescription(Entry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.id))
            return entry.description;

        int currentLevel = entry.level;
        int nextLevel = Mathf.Min(entry.maxLevel, currentLevel + 1);
        bool atMax = currentLevel >= entry.maxLevel;

        switch (entry.id)
        {
            case TitleEnhanceProgress.EfficiencyId:
            {
                int current = TitleEnhanceProgress.GetEfficiencyPercent(currentLevel);
                if (atMax)
                    return $"경험치, 골드 획득량이 {current}% 증가합니다.";

                int next = TitleEnhanceProgress.GetEfficiencyPercent(nextLevel);
                return $"경험치, 골드 획득량이 {current}% -> {HighlightNext($"{next}%")} 증가합니다.";
            }
            case TitleEnhanceProgress.MagicId:
            {
                int current = TitleEnhanceProgress.GetMagicPercent(currentLevel);
                if (atMax)
                    return $"공격력, 공격속도가 {current}% 증가합니다.";

                int next = TitleEnhanceProgress.GetMagicPercent(nextLevel);
                return $"공격력, 공격속도가 {current}% -> {HighlightNext($"{next}%")} 증가합니다.";
            }
            case TitleEnhanceProgress.ProductivityId:
            {
                int current = TitleEnhanceProgress.GetProductivityPercent(currentLevel);
                if (atMax)
                    return $"고등급 아이템 생산 확률이 {current}% 증가합니다.";

                int next = TitleEnhanceProgress.GetProductivityPercent(nextLevel);
                return $"고등급 아이템 생산 확률이 {current}% -> {HighlightNext($"{next}%")} 증가합니다.";
            }
            case TitleEnhanceProgress.DiscardId:
            {
                int current = TitleEnhanceProgress.GetDiscardCount(currentLevel);
                if (atMax)
                    return $"폐기 횟수가 {current}회 증가합니다.";

                int next = TitleEnhanceProgress.GetDiscardCount(nextLevel);
                return $"폐기 횟수가 {current} -> {HighlightNext($"{next}회")} 증가합니다.";
            }
            case TitleEnhanceProgress.BarrierId:
            {
                int current = TitleEnhanceProgress.GetBarrierPercent(currentLevel);
                if (atMax)
                    return $"방어막 체력이 {current}% 증가합니다.";

                int next = TitleEnhanceProgress.GetBarrierPercent(nextLevel);
                return $"방어막 체력이 {current}% -> {HighlightNext($"{next}%")} 증가합니다.";
            }
            case TitleEnhanceProgress.ChoiceCountId:
            {
                int current = TitleEnhanceProgress.GetChoiceCount(currentLevel);
                if (atMax)
                    return $"특성 선택지가 {current}개가 됩니다.";

                int next = TitleEnhanceProgress.GetChoiceCount(nextLevel);
                return $"특성 선택지가 {current} -> {HighlightNext($"{next}개")}가 됩니다.";
            }
            default:
                return entry.description;
        }
    }
}
