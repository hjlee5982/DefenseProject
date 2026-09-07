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
    [SerializeField] private EquipSlotView equipSlot;
    [SerializeField] private Spawner spawner;
    [SerializeField] private PlayerShooter playerShooter;
    [SerializeField] private RoundEventScheduler roundEventScheduler;

    private bool inCombat;
    private int aliveMonsters;
    private int clearedStageCount;
    private bool isInitialExpansionPhase;

    private Button nextButtonComponent;

    private void Awake()
    {
        spawner.enabled = false;
        playerShooter.enabled = false;
        equipSlot.gameObject.SetActive(false);
        ResolveGoldPanel();
        SetCombatHudActive(false);

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

        spawner.enabled = true;
        playerShooter.enabled = true;
    }

    private void ReturnToPrepare()
    {
        inCombat = false;
        UnsubscribeCombatEvents();

        spawner.enabled = false;
        playerShooter.enabled = false;
        ClearProjectiles();

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

    private void ResolveGoldPanel()
    {
        if (goldPanel != null) return;
        if (roundPanel == null || roundPanel.transform.parent == null) return;

        Transform found = roundPanel.transform.parent.Find("Gold");
        if (found != null)
            goldPanel = found.gameObject;
    }

    private void SetCombatHudActive(bool active)
    {
        if (roundPanel != null) roundPanel.SetActive(active);
        if (goldPanel != null) goldPanel.SetActive(active);
    }

    private void SubscribeCombatEvents()
    {
        UnsubscribeCombatEvents();
        spawner.MonsterSpawned += OnMonsterSpawned;
        Monster.AnyDestroyed += OnMonsterDestroyed;
    }

    private void UnsubscribeCombatEvents()
    {
        spawner.MonsterSpawned -= OnMonsterSpawned;
        Monster.AnyDestroyed -= OnMonsterDestroyed;
    }

    private void OnMonsterSpawned()
    {
        aliveMonsters++;
    }

    private void OnMonsterDestroyed()
    {
        if (!inCombat) return;

        aliveMonsters = Mathf.Max(0, aliveMonsters - 1);
        if (!spawner.HasFinishedSpawning) return;
        if (aliveMonsters > 0) return;

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
