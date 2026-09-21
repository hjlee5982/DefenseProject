using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private GameObject nextButton;
    [SerializeField] private GameObject debugButton;
    [SerializeField] private GameObject uiBackground;
    [SerializeField] private GameObject roundPanel;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private GameObject goldPanel;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private GameObject expPanel;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private Slider expGauge;
    [SerializeField] private GameObject levelPanel;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private GameObject enhancePanel;
    [SerializeField] private EquipSlotView equipSlot;
    [SerializeField] private GameObject objectsRoot;
    [SerializeField] private Spawner spawner;
    [SerializeField] private PlayerShooter playerShooter;
    [SerializeField] private RoundEventScheduler roundEventScheduler;

    private const int GoldPerKill = 1000;
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

    private void Awake()
    {
        spawner.enabled = false;
        playerShooter.enabled = false;
        equipSlot.gameObject.SetActive(false);
        SetObjectsActive(false);
        ResolveCombatHudPanels();
        ResolveEnhancePanel();
        RefreshCombatHudTexts();
        SetCombatHudActive(false);
        SetEnhanceActive(false);

        nextButtonComponent = nextButton.GetComponent<Button>();
        nextButtonComponent.onClick.AddListener(OnNextClicked);
        debugButton.GetComponent<Button>().onClick.AddListener(OnDebugClicked);
        inventoryManager.ExpansionStateChanged += UpdateNextButtonState;
    }

    private void Start()
    {
        EnterInitialPreparePhase();
    }

    private void OnDestroy()
    {
        UnsubscribeCombatEvents();
        UnwireEnhanceButtons();
        Time.timeScale = 1f;
        if (inventoryManager != null)
        {
            inventoryManager.ExpansionStateChanged -= UpdateNextButtonState;
        }
    }

    private void OnDebugClicked()
    {
        inventoryManager.TryRespawnShopItems();
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
        PreparePhaseMode prepareMode = ResolvePreparePhaseMode(clearedStageCount);
        isInitialExpansionPhase = false;
        int expansionUnlockCount = GetExpansionUnlockCount();
        inventoryManager.EnterPreparePhase(prepareMode, expansionUnlockCount);

        if (prepareMode != PreparePhaseMode.BagExpansion)
        {
            inventoryManager.RefreshShop();
        }

        UpdateNextButtonState();
    }

    private void EnterInitialPreparePhase()
    {
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
        if (roundText != null)
        {
            roundText.text = round.ToString();
        }

        SetCombatHudActive(true);
    }

    private void ResolveCombatHudPanels()
    {
        ResolveSiblingPanel(ref goldPanel, "Gold");
        ResolveSiblingPanel(ref expPanel, "Exp");
        ResolveSiblingPanel(ref levelPanel, "Level");
        ResolvePanelText(ref goldText, goldPanel);
        ResolvePanelText(ref expText, expPanel);
        ResolvePanelText(ref levelText, levelPanel);
        ResolveExpGauge();
    }

    private void ResolveExpGauge()
    {
        if (expGauge != null) return;
        if (roundPanel == null || roundPanel.transform.parent == null) return;

        Transform found = roundPanel.transform.parent.Find("ExpGauge");
        if (found != null)
            expGauge = found.GetComponent<Slider>();
    }

    private void ResolveEnhancePanel()
    {
        ResolveSiblingPanel(ref enhancePanel, "Enhance");
        WireEnhanceButtons();
    }

    private void ResolveSiblingPanel(ref GameObject panel, string panelName)
    {
        if (panel != null) return;
        if (roundPanel == null || roundPanel.transform.parent == null) return;

        Transform found = roundPanel.transform.parent.Find(panelName);
        if (found != null)
            panel = found.gameObject;
    }

    private static void ResolvePanelText(ref TextMeshProUGUI text, GameObject panel)
    {
        if (text != null || panel == null) return;
        text = panel.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void WireEnhanceButtons()
    {
        UnwireEnhanceButtons();
        if (enhancePanel == null) return;

        enhanceButtons = enhancePanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < enhanceButtons.Length; i++)
        {
            enhanceButtons[i].onClick.AddListener(OnEnhanceOptionSelected);
        }
    }

    private void UnwireEnhanceButtons()
    {
        if (enhanceButtons == null) return;

        for (int i = 0; i < enhanceButtons.Length; i++)
        {
            if (enhanceButtons[i] != null)
                enhanceButtons[i].onClick.RemoveListener(OnEnhanceOptionSelected);
        }

        enhanceButtons = null;
    }

    private void SetCombatHudActive(bool active)
    {
        if (roundPanel != null) roundPanel.SetActive(active);
        if (goldPanel != null) goldPanel.SetActive(active);
        if (expPanel != null) expPanel.SetActive(active);
        if (levelPanel != null) levelPanel.SetActive(active);
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

    private void RefreshCombatHudTexts()
    {
        if (goldText != null) goldText.text = gold.ToString();
        if (expText != null) expText.text = exp.ToString();
        if (levelText != null) levelText.text = level.ToString();
        RefreshExpGauge();
    }

    private void RefreshExpGauge()
    {
        if (expGauge == null) return;

        int expToNext = GetExpToNextLevel(level);
        float normalized = expToNext > 0
            ? Mathf.Clamp01((float)exp / expToNext)
            : 0f;

        expGauge.minValue = 0f;
        expGauge.maxValue = 1f;
        expGauge.value = normalized;
    }

    private int GetExpToNextLevel(int currentLevel)
    {
        // TODO: LevelCurve 데이터에서 currentLevel 기준 필요 경험치를 조회
        return DefaultExpToNextLevel;
    }

    private void OpenEnhance(int levelUps)
    {
        if (levelUps <= 0) return;

        pendingEnhanceCount += levelUps;
        if (isEnhanceOpen) return;

        isEnhanceOpen = true;
        SetEnhanceActive(true);
        Time.timeScale = 0f;
    }

    private void OnEnhanceOptionSelected()
    {
        if (!isEnhanceOpen) return;

        pendingEnhanceCount = Mathf.Max(0, pendingEnhanceCount - 1);
        if (pendingEnhanceCount > 0) return;

        CloseEnhance(force: false);
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
            SetEnhanceActive(true);
            Time.timeScale = 0f;
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

    private void OnMonsterKilled()
    {
        if (!inCombat) return;

        gold += GoldPerKill;
        exp += ExpPerKill;

        int levelUps = 0;
        while (true)
        {
            int expToNext = GetExpToNextLevel(level);
            if (expToNext <= 0 || exp < expToNext) break;

            exp -= expToNext;
            level++;
            levelUps++;
        }

        RefreshCombatHudTexts();

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
