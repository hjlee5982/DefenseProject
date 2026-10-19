using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private GameObject nextButton;
    [SerializeField] private GameObject uiBackground;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private Slider expGauge;
    [SerializeField] private float expGaugeTweenDuration = 0.25f;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI shopRoundText;
    [SerializeField] private TextMeshProUGUI shopLevelText;
    [SerializeField] private TextMeshProUGUI shopGoldText;
    [SerializeField] private GameObject enhancePanel;
    [SerializeField] private EquipSlotView equipSlot;
    [SerializeField] private GameObject objectsRoot;
    [SerializeField] private Spawner spawner;
    [SerializeField] private PlayerShooter playerShooter;
    [SerializeField] private RoundEventScheduler roundEventScheduler;
    [SerializeField] private Barrier barrier;
    [Header("Result")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultStageText;
    [SerializeField] private TextMeshProUGUI resultLevelText;
    [SerializeField] private TextMeshProUGUI resultGoldText;
    [SerializeField] private Button resultReturnButton;
    [SerializeField] private string titleSceneName = "TitleScene";

    private const int ExpPerKill = 10;
    private const int DefaultExpToNextLevel = 100;

    private bool inCombat;
    private int aliveMonsters;
    private int clearedStageCount;
    private bool isInitialExpansionPhase;
    private int gold;
    private int exp;
    private int level = 1;
    private bool isEnhanceOpen;
    private int pendingEnhanceCount;
    private bool pendingReturnToPrepare;

    private Button nextButtonComponent;
    private Button[] enhanceButtons;
    private readonly UnityEngine.Events.UnityAction[] enhanceButtonActions = new UnityEngine.Events.UnityAction[3];
    private readonly EnhanceOptionData[] currentEnhanceOptions = new EnhanceOptionData[3];
    private Tween expGaugeTween;
    private int displayedRound = 1;
    private bool isGameOver;
    private Barrier subscribedBarrier;

    public int Gold => gold;
    public event Action GoldChanged;
    public EnhanceBonusState EnhanceBonuses { get; } = new();
    public static GameManager Instance { get; private set; }

    public bool CanAfford(int amount) => amount <= 0 || gold >= amount;

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (gold < amount) return false;

        gold -= amount;
        RefreshCombatHudTexts();
        GoldChanged?.Invoke();
        return true;
    }

    private void Awake()
    {
        Instance = this;
        EnhanceBonuses.Reset();
        spawner.enabled = false;
        playerShooter.enabled = false;
        equipSlot.gameObject.SetActive(false);
        SetObjectsActive(false);
        ResolveCombatHudPanels();
        ResolveEnhancePanel();
        RefreshCombatHudTexts();
        SetCombatHudActive(false);
        SetEnhanceActive(false);
        SetupResultPanel();
        SubscribeBarrier();

        nextButtonComponent = nextButton.GetComponent<Button>();
        nextButtonComponent.onClick.AddListener(OnNextClicked);
        inventoryManager.ExpansionStateChanged += UpdateNextButtonState;
    }

    private void Start()
    {
        EnterInitialPreparePhase();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        KillExpGaugeTween();
        UnsubscribeCombatEvents();
        UnwireEnhanceButtons();
        UnsubscribeBarrier();
        if (resultReturnButton != null)
            resultReturnButton.onClick.RemoveListener(OnResultReturnClicked);
        Time.timeScale = 1f;
        if (inventoryManager != null)
        {
            inventoryManager.ExpansionStateChanged -= UpdateNextButtonState;
        }
    }

    private void OnNextClicked()
    {
        if (inventoryManager.IsExpansionPrepareMode)
        {
            if (inventoryManager.RemainingExpansionCount > 0) return;

            inventoryManager.ApplyBagExpansion();

            if (isInitialExpansionPhase)
            {
                isInitialExpansionPhase = false;
                inventoryManager.ExitSpecialPreparePhase();
                inventoryManager.RefreshShop(ensureAtLeastOneWeapon: true);
                UpdateNextButtonState();
                return;
            }

            StartCombat();
            return;
        }

        StartCombat();
    }

    private void StartCombat()
    {
        if (inventoryManager.IsClearPrepareMode)
        {
            inventoryManager.ApplyClearBans();
        }

        if (inventoryManager.IsExpansionPrepareMode)
        {
            inventoryManager.ExitSpecialPreparePhase();
        }

        isInitialExpansionPhase = false;

        IReadOnlyList<Item> equippedItems = inventoryManager.GetBagItems();
        equipSlot.ShowItems(equippedItems);
        playerShooter.SetEquippedProjectiles(equippedItems);

        inventoryManager.gameObject.SetActive(false);
        nextButton.SetActive(false);
        uiBackground.SetActive(false);
        equipSlot.gameObject.SetActive(true);
        ShowRound(clearedStageCount + 1);

        aliveMonsters = 0;
        inCombat = true;
        SubscribeCombatEvents();

        SetObjectsActive(true);
        spawner.SetStageOrder(clearedStageCount);
        spawner.enabled = true;
        playerShooter.enabled = true;
    }

    private void ReturnToPrepare()
    {
        inCombat = false;
        pendingReturnToPrepare = false;
        CloseEnhance(force: true);
        UnsubscribeCombatEvents();

        spawner.enabled = false;
        playerShooter.enabled = false;
        ClearProjectiles();
        SetObjectsActive(false);

        equipSlot.gameObject.SetActive(false);
        SetCombatHudActive(false);
        inventoryManager.gameObject.SetActive(true);
        nextButton.SetActive(true);
        uiBackground.SetActive(true);

        clearedStageCount++;
        displayedRound = clearedStageCount + 1;
        inventoryManager.SetShopStageOrder(clearedStageCount);
        PreparePhaseMode prepareMode = ResolvePreparePhaseMode(clearedStageCount);
        isInitialExpansionPhase = false;
        int expansionUnlockCount = GetExpansionUnlockCount();
        inventoryManager.EnterPreparePhase(prepareMode, expansionUnlockCount);

        if (prepareMode != PreparePhaseMode.BagExpansion)
        {
            inventoryManager.RefreshShop();
        }

        RefreshCombatHudTexts();
        UpdateNextButtonState();
    }

    private void EnterInitialPreparePhase()
    {
        inventoryManager.SetShopStageOrder(clearedStageCount);
        PreparePhaseMode prepareMode = inventoryManager.CanExpandBag
            ? PreparePhaseMode.BagExpansion
            : PreparePhaseMode.Normal;

        isInitialExpansionPhase = prepareMode == PreparePhaseMode.BagExpansion;

        int expansionUnlockCount = GetExpansionUnlockCount();
        inventoryManager.EnterPreparePhase(prepareMode, expansionUnlockCount);

        if (prepareMode != PreparePhaseMode.BagExpansion)
        {
            inventoryManager.RefreshShop(ensureAtLeastOneWeapon: true);
        }

        displayedRound = clearedStageCount + 1;
        RefreshCombatHudTexts();
        UpdateNextButtonState();
    }

    private int GetExpansionUnlockCount()
    {
        return roundEventScheduler != null
            ? roundEventScheduler.BagExpansionUnlockCount
            : 2;
    }

    private void UpdateNextButtonState()
    {
        if (nextButtonComponent == null) return;

        if (inventoryManager.IsExpansionPrepareMode)
        {
            nextButtonComponent.interactable = inventoryManager.RemainingExpansionCount <= 0;
            return;
        }

        nextButtonComponent.interactable = true;
    }

    private PreparePhaseMode ResolvePreparePhaseMode(int clearedRoundCount)
    {
        if (roundEventScheduler == null)
        {
            return PreparePhaseMode.Normal;
        }

        if (roundEventScheduler.ShouldShowBagExpansion(clearedRoundCount)
            && inventoryManager.CanExpandBag)
        {
            return PreparePhaseMode.BagExpansion;
        }

        return PreparePhaseMode.Normal;
    }

    private void ShowRound(int round)
    {
        displayedRound = round;
        SetRoundTexts(round);
        SetCombatHudActive(true);
    }

    private void ResolveCombatHudPanels()
    {
        ResolveExpGauge();
        ResolveGoldText();
        ResolveRoundText();
        ResolveLevelText();
        ResolveShopStatusTexts();
    }

    private void ResolveExpGauge()
    {
        if (expGauge != null) return;

        Transform found = FindStatusGaugeTransform();
        if (found != null)
            expGauge = found.GetComponent<Slider>();
    }

    private void ResolveGoldText()
    {
        if (goldText != null) return;

        Transform gold = GetStatusGaugeRoot()?.Find("Gold");
        if (gold == null) return;

        goldText = gold.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void ResolveRoundText()
    {
        if (roundText != null) return;

        Transform round = GetStatusGaugeRoot()?.Find("Round");
        if (round == null) return;

        roundText = round.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private Transform GetStatusGaugeRoot()
    {
        if (expGauge != null) return expGauge.transform;
        return FindStatusGaugeTransform();
    }

    private Transform FindStatusGaugeTransform()
    {
        Transform searchRoot = null;
        if (uiBackground != null && uiBackground.transform.parent != null)
            searchRoot = uiBackground.transform.parent;
        else if (uiBackground != null)
            searchRoot = uiBackground.transform;

        if (searchRoot == null) return null;

        Transform found = FindChildRecursive(searchRoot, "StatusGauge");
        if (found != null) return found;
        return FindChildRecursive(searchRoot, "ExpGauge");
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    private void ResolveLevelText()
    {
        if (levelText != null) return;

        Transform level = GetStatusGaugeRoot()?.Find("Level");
        if (level == null) return;

        levelText = level.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void ResolveShopStatusTexts()
    {
        if (shopRoundText != null && shopLevelText != null && shopGoldText != null) return;

        Transform shop = inventoryManager != null && inventoryManager.Shop != null
            ? inventoryManager.Shop.transform
            : null;
        Transform status = shop != null ? shop.Find("Status") : null;
        if (status == null) return;

        if (shopRoundText == null)
        {
            Transform round = status.Find("Round");
            if (round != null)
                shopRoundText = round.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (shopLevelText == null)
        {
            Transform level = status.Find("Level");
            if (level != null)
                shopLevelText = level.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (shopGoldText == null)
        {
            Transform goldRoot = status.Find("Gold");
            if (goldRoot != null)
                shopGoldText = goldRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void SetRoundTexts(int round)
    {
        string text = round.ToString();
        if (roundText != null) roundText.text = text;
        if (shopRoundText != null) shopRoundText.text = text;
    }

    private void ResolveEnhancePanel()
    {
        if (enhancePanel == null)
        {
            Transform gaugeRoot = GetStatusGaugeRoot();
            Transform parent = gaugeRoot != null ? gaugeRoot.parent : null;
            if (parent != null)
            {
                Transform found = parent.Find("Enhance");
                if (found != null)
                    enhancePanel = found.gameObject;
            }
        }

        WireEnhanceButtons();
    }

    private void WireEnhanceButtons()
    {
        UnwireEnhanceButtons();
        if (enhancePanel == null) return;

        enhanceButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            Transform optionRoot = enhancePanel.transform.Find($"Option_{i}");
            if (optionRoot == null) continue;

            Button button = optionRoot.Find("Button")?.GetComponent<Button>()
                            ?? optionRoot.GetComponentInChildren<Button>(true);
            if (button == null) continue;

            int optionIndex = i;
            UnityEngine.Events.UnityAction action = () => OnEnhanceOptionSelected(optionIndex);
            enhanceButtons[i] = button;
            enhanceButtonActions[i] = action;
            button.onClick.AddListener(action);
        }
    }

    private void UnwireEnhanceButtons()
    {
        if (enhanceButtons == null) return;

        for (int i = 0; i < enhanceButtons.Length; i++)
        {
            if (enhanceButtons[i] != null && enhanceButtonActions[i] != null)
                enhanceButtons[i].onClick.RemoveListener(enhanceButtonActions[i]);

            enhanceButtonActions[i] = null;
        }

        enhanceButtons = null;
    }

    private void SetCombatHudActive(bool active)
    {
        if (expGauge != null) expGauge.gameObject.SetActive(active);
    }

    private void SetObjectsActive(bool active)
    {
        if (objectsRoot != null) objectsRoot.SetActive(active);
    }

    private void SetEnhanceActive(bool active)
    {
        if (enhancePanel != null) enhancePanel.SetActive(active);
    }

    private void RefreshCombatHudTexts(bool animateExpGauge = false, int levelUps = 0)
    {
        ResolveShopStatusTexts();

        string goldValue = gold.ToString("N0", CultureInfo.InvariantCulture);
        if (goldText != null) goldText.text = goldValue;
        if (shopGoldText != null) shopGoldText.text = goldValue;

        string levelValue = level.ToString();
        if (levelText != null) levelText.text = levelValue;
        if (shopLevelText != null) shopLevelText.text = levelValue;

        SetRoundTexts(displayedRound);
        RefreshExpGauge(animateExpGauge, levelUps);
    }

    private void RefreshExpGauge(bool animate = false, int levelUps = 0)
    {
        if (expGauge == null) return;

        expGauge.minValue = 0f;
        expGauge.maxValue = 1f;

        float target = GetNormalizedExp();
        KillExpGaugeTween();

        if (!animate || !expGauge.gameObject.activeInHierarchy)
        {
            expGauge.value = target;
            return;
        }

        if (levelUps <= 0)
        {
            expGaugeTween = expGauge
                .DOValue(target, expGaugeTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
            return;
        }

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < levelUps; i++)
        {
            sequence.Append(
                expGauge.DOValue(1f, expGaugeTweenDuration).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() => expGauge.value = 0f);
        }

        sequence.Append(
            expGauge.DOValue(target, expGaugeTweenDuration).SetEase(Ease.OutQuad));
        expGaugeTween = sequence;
    }

    private float GetNormalizedExp()
    {
        int expToNext = GetExpToNextLevel(level);
        if (expToNext <= 0) return 0f;
        return Mathf.Clamp01((float)exp / expToNext);
    }

    private void KillExpGaugeTween()
    {
        if (expGaugeTween == null) return;
        expGaugeTween.Kill();
        expGaugeTween = null;
    }

    private int GetExpToNextLevel(int currentLevel)
    {
        if (GameDataRepository.TryGetExpToNext(currentLevel, out int expToNext))
            return expToNext;

        return DefaultExpToNextLevel;
    }

    private void OpenEnhance(int levelUps)
    {
        if (levelUps <= 0) return;

        pendingEnhanceCount += levelUps;
        if (isEnhanceOpen) return;

        isEnhanceOpen = true;
        ShowEnhanceChoices();
    }

    private void OnEnhanceOptionSelected(int optionIndex)
    {
        if (!isEnhanceOpen) return;

        if (optionIndex >= 0 && optionIndex < currentEnhanceOptions.Length)
            ApplyEnhanceOption(currentEnhanceOptions[optionIndex]);

        pendingEnhanceCount = Mathf.Max(0, pendingEnhanceCount - 1);
        if (pendingEnhanceCount > 0)
        {
            ShowEnhanceChoices();
            return;
        }

        CloseEnhance(force: false);
    }

    private void ApplyEnhanceOption(EnhanceOptionData option)
    {
        if (option == null) return;

        List<EnhanceOptionEffectData> effects = GameDataRepository.GetEnhanceEffects(option.Id);
        for (int i = 0; i < effects.Count; i++)
            ApplyEnhanceEffect(effects[i]);

        RefreshEquippedCombatStats();
    }

    private void ApplyEnhanceEffect(EnhanceOptionEffectData effect)
    {
        if (effect == null || string.IsNullOrWhiteSpace(effect.EffectType)) return;

        switch (effect.EffectType)
        {
            case "RemoveItem":
                inventoryManager?.AddItemRemoveCount(ParseEffectAmount(effect.Value, 1));
                break;
            case "RepairBarrier":
                ResolveBarrier()?.RestoreFullHp();
                break;
            default:
                EnhanceBonuses.ApplyEffect(effect.EffectType, ParseEffectFloat(effect.Value));
                break;
        }
    }

    private void RefreshEquippedCombatStats()
    {
        if (playerShooter == null || inventoryManager == null) return;
        if (!playerShooter.enabled && !inCombat) return;

        playerShooter.SetEquippedProjectiles(inventoryManager.GetBagItems());
    }

    private static float ParseEffectFloat(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0f;
        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : 0f;
    }

    private Barrier ResolveBarrier()
    {
        if (barrier != null) return barrier;
        barrier = FindAnyObjectByType<Barrier>(FindObjectsInactive.Include);
        return barrier;
    }

    private void SubscribeBarrier()
    {
        Barrier target = ResolveBarrier();
        if (target == null || subscribedBarrier == target) return;

        UnsubscribeBarrier();
        subscribedBarrier = target;
        subscribedBarrier.HpDepleted += OnBarrierHpDepleted;
    }

    private void UnsubscribeBarrier()
    {
        if (subscribedBarrier == null) return;
        subscribedBarrier.HpDepleted -= OnBarrierHpDepleted;
        subscribedBarrier = null;
    }

    private void SetupResultPanel()
    {
        if (resultPanel == null)
        {
            Transform gaugeRoot = GetStatusGaugeRoot();
            Transform parent = gaugeRoot != null ? gaugeRoot.parent : null;
            Transform found = parent != null ? parent.Find("Result") : null;
            if (found != null)
                resultPanel = found.gameObject;
        }

        if (resultPanel == null) return;

        if (resultReturnButton != null)
        {
            resultReturnButton.onClick.RemoveListener(OnResultReturnClicked);
            resultReturnButton.onClick.AddListener(OnResultReturnClicked);
        }

        resultPanel.SetActive(false);
    }

    private void OnBarrierHpDepleted()
    {
        if (isGameOver) return;
        isGameOver = true;

        inCombat = false;
        pendingReturnToPrepare = false;
        pendingEnhanceCount = 0;
        isEnhanceOpen = false;
        UnsubscribeCombatEvents();
        SetEnhanceActive(false);

        spawner.enabled = false;
        playerShooter.enabled = false;

        ShowResult();
        Time.timeScale = 0f;
    }

    private void ShowResult()
    {
        if (resultStageText != null)
            resultStageText.text = $"스테이지  {displayedRound}";
        if (resultLevelText != null)
            resultLevelText.text = $"레벨  {level}";
        if (resultGoldText != null)
            resultGoldText.text = $"골드  {gold.ToString("N0", CultureInfo.InvariantCulture)}";

        if (resultPanel != null)
        {
            resultPanel.transform.SetAsLastSibling();
            resultPanel.SetActive(true);
        }
    }

    private void OnResultReturnClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    private static int ParseEffectAmount(string value, int defaultAmount)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultAmount;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultAmount;
    }

    private void CloseEnhance(bool force)
    {
        if (force)
            pendingEnhanceCount = 0;

        if (!isEnhanceOpen && pendingEnhanceCount <= 0)
        {
            Time.timeScale = 1f;
            SetEnhanceActive(false);
            return;
        }

        if (pendingEnhanceCount > 0)
        {
            isEnhanceOpen = true;
            ShowEnhanceChoices();
            return;
        }

        isEnhanceOpen = false;
        SetEnhanceActive(false);
        Time.timeScale = 1f;

        if (pendingReturnToPrepare)
        {
            pendingReturnToPrepare = false;
            ReturnToPrepare();
        }
    }

    private void ShowEnhanceChoices()
    {
        ResolveEnhancePanel();
        List<EnhanceOptionData> picked = EnhancePanelFiller.FillRandomOptions(enhancePanel, optionCount: 3);
        for (int i = 0; i < currentEnhanceOptions.Length; i++)
            currentEnhanceOptions[i] = i < picked.Count ? picked[i] : null;

        SetEnhanceActive(true);
        Time.timeScale = 0f;
    }

    private void SubscribeCombatEvents()
    {
        UnsubscribeCombatEvents();
        spawner.MonsterSpawned += OnMonsterSpawned;
        Monster.AnyDestroyed += OnMonsterDestroyed;
        Monster.Killed += OnMonsterKilled;
    }

    private void UnsubscribeCombatEvents()
    {
        spawner.MonsterSpawned -= OnMonsterSpawned;
        Monster.AnyDestroyed -= OnMonsterDestroyed;
        Monster.Killed -= OnMonsterKilled;
    }

    private void OnMonsterSpawned()
    {
        aliveMonsters++;
    }

    private void OnMonsterKilled(Monster monster)
    {
        if (!inCombat) return;

        int gainedGold = monster != null ? Mathf.Max(0, monster.DropGold) : 0;
        int gainedExp = monster != null ? Mathf.Max(0, monster.DropExp) : ExpPerKill;

        gold += gainedGold;
        exp += gainedExp;

        int levelUps = 0;
        while (true)
        {
            int expToNext = GetExpToNextLevel(level);
            if (expToNext <= 0 || exp < expToNext) break;

            exp -= expToNext;
            level++;
            levelUps++;
        }

        RefreshCombatHudTexts(animateExpGauge: true, levelUps: levelUps);
        GoldChanged?.Invoke();

        if (levelUps > 0)
            OpenEnhance(levelUps);
    }

    private void OnMonsterDestroyed()
    {
        if (!inCombat) return;

        aliveMonsters = Mathf.Max(0, aliveMonsters - 1);
        if (!spawner.HasFinishedSpawning) return;
        if (aliveMonsters > 0) return;

        if (isEnhanceOpen || pendingEnhanceCount > 0)
        {
            pendingReturnToPrepare = true;
            return;
        }

        ReturnToPrepare();
    }

    private static void ClearProjectiles()
    {
        Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            Destroy(projectiles[i].gameObject);
        }
    }
}
