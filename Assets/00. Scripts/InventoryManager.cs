using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    [System.Serializable]
    private class ItemPrefabSpawnEntry
    {
        public Item prefab;
        public bool spawnEnabled = true;
    }

    [SerializeField] public Bag Bag;
    [SerializeField] public GameObject Shop;
    [SerializeField] private Button shopRerollButton;
    [SerializeField] private Button shopCompressButton;
    [SerializeField] private Button shopReturnButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button rotateButton0;
    [SerializeField] private Button rotateButton1;
    [SerializeField] private GameObject clearPanel;
    [SerializeField] private GameObject expansionPanel;
    [SerializeField] private TextMeshProUGUI expansionCountText;
    [Header("Item Prefabs")]
    [SerializeField] private ItemPrefabSpawnEntry[] itemPrefabs;

    [Header("Shop Grade Probabilities (합계 100%)")]
    [Tooltip("1단계 아이템이 등장할 확률 (%)")]
    [SerializeField] private float grade1Chance = 70f;
    [Tooltip("2단계 아이템이 등장할 확률 (%)")]
    [SerializeField] private float grade2Chance = 20f;
    [Tooltip("3단계 아이템이 등장할 확률 (%)")]
    [SerializeField] private float grade3Chance = 7f;
    [Tooltip("4단계 아이템이 등장할 확률 (%)")]
    [SerializeField] private float grade4Chance = 2f;
    [Tooltip("5단계 아이템이 등장할 확률 (%)")]
    [SerializeField] private float grade5Chance = 1f;

    private RectTransform[] shopSlots;
    private RectTransform[] clearSlots;
    private bool[] previewStates;
    private bool previewValid;
    private PreparePhaseMode preparePhaseMode;
    private int requiredExpansionCount;
    private readonly HashSet<Slot> selectedExpansionSlots = new();
    private readonly HashSet<string> bannedShopKeys = new();
    private readonly HashSet<int> previewIndices = new();
    private readonly List<int> placementIndices = new();
    private readonly List<Vector2Int> shapeBuffer = new();
    private readonly List<RaycastResult> raycastResults = new();
    private Item draggingItem;

    public float BagCellSize => Bag.CellSize;
    public RectTransform DragLayer => transform as RectTransform;
    public bool IsClearPrepareMode => preparePhaseMode == PreparePhaseMode.ItemCompress;
    public bool IsExpansionPrepareMode => preparePhaseMode == PreparePhaseMode.BagExpansion;
    public bool CanExpandBag => Bag.HasLockedSlots();
    public int RemainingExpansionCount => Mathf.Max(0, requiredExpansionCount - selectedExpansionSlots.Count);

    public event System.Action ExpansionStateChanged;

    public IReadOnlyList<Item> GetBagItems()
    {
        List<Item> items = new();
        Transform itemsRoot = Bag.Items;
        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            Item item = itemsRoot.GetChild(i).GetComponent<Item>();
            if (item != null) items.Add(item);
        }

        return items;
    }

    private void OnValidate()
    {
        if (itemPrefabs == null) return;

        for (int i = 0; i < itemPrefabs.Length; i++)
        {
            if (itemPrefabs[i] == null)
                itemPrefabs[i] = new ItemPrefabSpawnEntry();
        }
    }

    private void Awake()
    {
        previewStates = new bool[Bag.Slots.Length];

        CacheShopSlots();
        CacheClearSlots();
        ResolveExpansionCountText();
        BindShopRerollButton();
        BindShopCompressButton();
        BindShopReturnButton();
        BindNextButton();
        BindRotateButtons();
        UpdateModeActionButtons();

        SpawnShopItems();
    }

    private void OnDestroy()
    {
        if (shopRerollButton != null)
            shopRerollButton.onClick.RemoveListener(OnShopRerollClicked);
        if (shopCompressButton != null)
            shopCompressButton.onClick.RemoveListener(OnShopCompressClicked);
        if (shopReturnButton != null)
            shopReturnButton.onClick.RemoveListener(OnShopReturnClicked);
        UnbindRotateButtons();
    }

    private void BindShopRerollButton()
    {
        if (shopRerollButton == null && Shop != null)
        {
            Transform found = Shop.transform.Find("Reroll");
            if (found != null)
                shopRerollButton = found.GetComponent<Button>();
        }

        if (shopRerollButton == null) return;
        shopRerollButton.onClick.AddListener(OnShopRerollClicked);
    }

    private void BindShopCompressButton()
    {
        if (shopCompressButton == null && Shop != null)
        {
            Transform found = Shop.transform.Find("Optimize");
            if (found == null)
                found = Shop.transform.Find("Compress");
            if (found != null)
                shopCompressButton = found.GetComponent<Button>();
        }

        if (shopCompressButton == null) return;
        shopCompressButton.onClick.AddListener(OnShopCompressClicked);
    }

    private void BindShopReturnButton()
    {
        if (shopReturnButton == null && Shop != null)
        {
            Transform found = Shop.transform.Find("Return");
            if (found != null)
                shopReturnButton = found.GetComponent<Button>();
        }

        if (shopReturnButton == null) return;
        shopReturnButton.onClick.AddListener(OnShopReturnClicked);
    }

    private void BindNextButton()
    {
        if (nextButton == null)
        {
            Transform found = transform.Find("Next");
            if (found != null)
                nextButton = found.GetComponent<Button>();
        }
    }

    private void OnShopRerollClicked()
    {
        if (preparePhaseMode != PreparePhaseMode.Normal) return;
        RefreshShop();
    }

    private void OnShopCompressClicked()
    {
        if (preparePhaseMode != PreparePhaseMode.Normal) return;
        EnterPreparePhase(PreparePhaseMode.ItemCompress);
    }

    private void OnShopReturnClicked()
    {
        if (preparePhaseMode != PreparePhaseMode.ItemCompress) return;
        ReturnClearItemsToShop();
        ExitSpecialPreparePhase();
    }

    private void BindRotateButtons()
    {
        if (rotateButton0 == null)
        {
            Transform found = transform.Find("Rotate_0");
            if (found != null)
                rotateButton0 = found.GetComponent<Button>();
        }

        if (rotateButton1 == null)
        {
            Transform found = transform.Find("Rotate_1");
            if (found != null)
                rotateButton1 = found.GetComponent<Button>();
        }

        if (rotateButton0 != null)
            rotateButton0.onClick.AddListener(OnRotateButton0Clicked);
        if (rotateButton1 != null)
            rotateButton1.onClick.AddListener(OnRotateButton1Clicked);
    }

    private void UnbindRotateButtons()
    {
        if (rotateButton0 != null)
            rotateButton0.onClick.RemoveListener(OnRotateButton0Clicked);
        if (rotateButton1 != null)
            rotateButton1.onClick.RemoveListener(OnRotateButton1Clicked);
    }

    private void OnRotateButton0Clicked()
    {
        TryRotateDraggingItem(clockwise: false);
    }

    private void OnRotateButton1Clicked()
    {
        TryRotateDraggingItem(clockwise: true);
    }

    public void NotifyItemDragBegan(Item item)
    {
        draggingItem = item;
    }

    public void NotifyItemDragEnded(Item item)
    {
        if (draggingItem == item)
            draggingItem = null;
    }

    private void TryRotateDraggingItem(bool clockwise)
    {
        if (draggingItem == null) return;
        draggingItem.TryRotate(clockwise);
    }

    private void UpdateModeActionButtons()
    {
        bool optimizeMode = preparePhaseMode == PreparePhaseMode.ItemCompress;
        bool expansionMode = preparePhaseMode == PreparePhaseMode.BagExpansion;
        bool shopVisible = Shop != null && Shop.activeSelf;

        SetButtonActive(shopCompressButton, shopVisible && !optimizeMode);
        SetButtonActive(shopRerollButton, shopVisible && !optimizeMode);
        SetButtonActive(shopReturnButton, shopVisible && optimizeMode);

        SetButtonActive(nextButton, !optimizeMode);
        if (nextButton != null)
        {
            // 슬롯확장모드에서는 남은 확장 칸을 모두 선택해야 Next 가능
            nextButton.interactable = !expansionMode || RemainingExpansionCount <= 0;
        }

        SetButtonActive(rotateButton0, shopVisible && !optimizeMode);
        SetButtonActive(rotateButton1, shopVisible && !optimizeMode);
    }

    private static void SetButtonActive(Button button, bool active)
    {
        if (button == null) return;
        button.gameObject.SetActive(active);
    }

    private void ReturnClearItemsToShop()
    {
        if (clearSlots == null) return;

        for (int i = 0; i < clearSlots.Length; i++)
        {
            RectTransform clearSlot = clearSlots[i];
            if (clearSlot == null) continue;

            for (int childIndex = clearSlot.childCount - 1; childIndex >= 0; childIndex--)
            {
                Item item = clearSlot.GetChild(childIndex).GetComponent<Item>();
                if (item == null) continue;

                if (!TryPlaceItemOnEmptyShopSlot(item))
                    Destroy(item.gameObject);
            }
        }
    }

    private bool TryPlaceItemOnEmptyShopSlot(Item item)
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            RectTransform shopSlot = shopSlots[i];
            if (!IsShopSlotEmpty(shopSlot, item)) continue;

            item.PlaceOnShop(shopSlot);
            return true;
        }

        return false;
    }

    private void CacheShopSlots()
    {
        List<RectTransform> slots = new();
        for (int i = 0; i < Shop.transform.childCount; i++)
        {
            Transform child = Shop.transform.GetChild(i);
            if (child.GetComponent<Button>() != null) continue;

            RectTransform rect = child as RectTransform;
            if (rect == null) continue;
            slots.Add(rect);
        }

        shopSlots = slots.ToArray();
    }

    private void ResolveExpansionCountText()
    {
        if (expansionCountText != null) return;
        if (expansionPanel == null) return;

        expansionCountText = expansionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void ConfigureExpansionPanel(bool active)
    {
        if (expansionPanel == null) return;

        expansionPanel.SetActive(active);
        if (!active) return;

        ResolveExpansionCountText();

        Image panelImage = expansionPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.raycastTarget = false;
        }

        if (expansionCountText != null)
        {
            expansionCountText.raycastTarget = false;
        }
    }

    private void CacheClearSlots()
    {
        if (clearPanel == null)
        {
            clearSlots = System.Array.Empty<RectTransform>();
            return;
        }

        clearSlots = new RectTransform[clearPanel.transform.childCount];
        for (int i = 0; i < clearPanel.transform.childCount; i++)
        {
            clearSlots[i] = clearPanel.transform.GetChild(i) as RectTransform;
        }
    }

    public void EnterPreparePhase(PreparePhaseMode mode, int expansionUnlockCount = 2)
    {
        preparePhaseMode = mode;
        Bag.SetExpandableOnLockedSlots(false);

        bool showBag = mode != PreparePhaseMode.ItemCompress;
        Bag.gameObject.SetActive(showBag);
        Shop.SetActive(mode != PreparePhaseMode.BagExpansion);

        if (clearPanel != null)
        {
            clearPanel.SetActive(mode == PreparePhaseMode.ItemCompress);
            if (mode == PreparePhaseMode.ItemCompress)
            {
                ClearClearSlotItems();
            }
        }

        if (expansionPanel != null)
        {
            ConfigureExpansionPanel(mode == PreparePhaseMode.BagExpansion);
        }

        if (mode == PreparePhaseMode.BagExpansion)
        {
            requiredExpansionCount = Mathf.Max(0, expansionUnlockCount);
            ClearExpansionSelections();
            Bag.SetExpandableOnLockedSlots(true);
            UpdateExpansionCountText();
            ExpansionStateChanged?.Invoke();
        }

        UpdateModeActionButtons();
    }

    public void ExitSpecialPreparePhase()
    {
        preparePhaseMode = PreparePhaseMode.Normal;
        ClearExpansionSelections();
        Bag.SetExpandableOnLockedSlots(false);
        Bag.gameObject.SetActive(true);

        if (clearPanel != null) clearPanel.SetActive(false);
        ConfigureExpansionPanel(false);
        Shop.SetActive(true);
        UpdateModeActionButtons();
    }

    public bool TryToggleExpansionSelection(Slot slot)
    {
        if (preparePhaseMode != PreparePhaseMode.BagExpansion) return false;
        if (slot == null || !slot.IsLocked || !slot.IsExpandable) return false;

        if (slot.IsExpansionSelected)
        {
            slot.SetExpansionSelected(false);
            selectedExpansionSlots.Remove(slot);
            UpdateExpansionCountText();
            UpdateModeActionButtons();
            ExpansionStateChanged?.Invoke();
            return true;
        }

        if (selectedExpansionSlots.Count >= requiredExpansionCount) return false;

        slot.SetExpansionSelected(true);
        selectedExpansionSlots.Add(slot);
        UpdateExpansionCountText();
        UpdateModeActionButtons();
        ExpansionStateChanged?.Invoke();
        return true;
    }

    public void ApplyBagExpansion()
    {
        if (preparePhaseMode != PreparePhaseMode.BagExpansion) return;
        if (selectedExpansionSlots.Count < requiredExpansionCount) return;

        foreach (Slot slot in selectedExpansionSlots)
        {
            slot.Unlock();
        }

        selectedExpansionSlots.Clear();
        Bag.SetExpandableOnLockedSlots(false);
        UpdateExpansionCountText();
        UpdateModeActionButtons();
        ExpansionStateChanged?.Invoke();
    }

    private void ClearExpansionSelections()
    {
        foreach (Slot slot in selectedExpansionSlots)
        {
            slot.SetExpansionSelected(false);
        }

        selectedExpansionSlots.Clear();
    }

    private void UpdateExpansionCountText()
    {
        ResolveExpansionCountText();
        if (expansionCountText == null) return;
        expansionCountText.text = $"확장 가능 칸 수 : {RemainingExpansionCount}";
    }

    public void ApplyClearBans()
    {
        for (int i = 0; i < clearSlots.Length; i++)
        {
            RectTransform clearSlot = clearSlots[i];
            for (int childIndex = 0; childIndex < clearSlot.childCount; childIndex++)
            {
                Item item = clearSlot.GetChild(childIndex).GetComponent<Item>();
                if (item == null) continue;

                bannedShopKeys.Add(item.ShopBanKey);
                Destroy(item.gameObject);
            }
        }
    }

    public void RefillEmptyShopSlots()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return;

        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (!IsShopSlotEmpty(shopSlots[i], null)) continue;

            Item prefab = PickRandomShopPrefab();
            if (prefab == null) continue;

            Item item = Instantiate(prefab, shopSlots[i]);
            item.PlaceOnShop(shopSlots[i]);
        }
    }

    public void RefreshShop(bool weaponOnly = false, bool ensureAtLeastOneWeapon = false)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return;

        for (int i = 0; i < shopSlots.Length; i++)
        {
            RectTransform shopSlot = shopSlots[i];
            RemoveShopItemsFromSlot(shopSlot, immediate: true);

            Item prefab = PickRandomShopPrefab(weaponOnly);
            if (prefab == null) continue;

            Item item = Instantiate(prefab, shopSlot);
            item.PlaceOnShop(shopSlot);
        }

        if (ensureAtLeastOneWeapon && !weaponOnly)
            EnsureShopHasWeapon();
    }

    private void EnsureShopHasWeapon()
    {
        if (ShopHasWeapon()) return;

        Item weaponPrefab = PickRandomShopPrefab(weaponOnly: true);
        if (weaponPrefab == null || shopSlots.Length == 0) return;

        int index = Random.Range(0, shopSlots.Length);
        RectTransform shopSlot = shopSlots[index];
        RemoveShopItemsFromSlot(shopSlot, immediate: true);

        Item item = Instantiate(weaponPrefab, shopSlot);
        item.PlaceOnShop(shopSlot);
    }

    private bool ShopHasWeapon()
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            RectTransform shopSlot = shopSlots[i];
            for (int childIndex = 0; childIndex < shopSlot.childCount; childIndex++)
            {
                Item item = shopSlot.GetChild(childIndex).GetComponent<Item>();
                if (item != null && item.IsWeapon)
                    return true;
            }
        }

        return false;
    }

    private Item PickRandomShopPrefab(bool weaponOnly = false)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return null;

        List<Item> allowed = new();
        for (int i = 0; i < itemPrefabs.Length; i++)
        {
            ItemPrefabSpawnEntry entry = itemPrefabs[i];
            if (entry == null || entry.prefab == null) continue;
            if (!entry.spawnEnabled) continue;
            if (bannedShopKeys.Contains(entry.prefab.ShopBanKey)) continue;
            if (weaponOnly && !entry.prefab.IsWeapon) continue;
            allowed.Add(entry.prefab);
        }

        if (allowed.Count == 0) return null;

        Item picked = allowed[Random.Range(0, allowed.Count)];
        int targetGrade = RollGrade(picked.MaxGrade);
        picked.SetGrade(targetGrade);
        return picked;
    }

    private int RollGrade(int maxGrade)
    {
        float[] chances = { grade1Chance, grade2Chance, grade3Chance, grade4Chance, grade5Chance };

        float total = 0f;
        for (int i = 0; i < chances.Length; i++) total += chances[i];
        if (total <= 0f) return 1;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < chances.Length; i++)
        {
            cumulative += chances[i];
            if (roll < cumulative)
            {
                return Mathf.Clamp(i + 1, 1, maxGrade);
            }
        }

        return 1;
    }

    private void SpawnShopItems()
    {
        RefillEmptyShopSlots();
    }

    public bool IsUnderShop(Transform target)
    {
        return target != null && target.IsChildOf(Shop.transform);
    }

    public bool IsUnderClear(Transform target)
    {
        return clearPanel != null && target != null && target.IsChildOf(clearPanel.transform);
    }

    public void TryRespawnShopItems()
    {
        if (HasAnyShopItem()) return;
        RefillEmptyShopSlots();
    }

    private bool HasAnyShopItem()
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            RectTransform shopSlot = shopSlots[i];
            for (int childIndex = 0; childIndex < shopSlot.childCount; childIndex++)
            {
                if (shopSlot.GetChild(childIndex).GetComponent<Item>() != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void UpdateItemPreview(Item item, Vector2 screenPosition, Camera eventCamera)
    {
        EnsurePreviewStates();
        previewIndices.Clear();
        previewValid = false;

        if (preparePhaseMode != PreparePhaseMode.ItemCompress
            && Bag != null
            && Bag.gameObject.activeInHierarchy
            && TryGetPlacement(item, screenPosition, eventCamera, placementIndices, out bool canPlace))
        {
            previewValid = canPlace;
            for (int i = 0; i < placementIndices.Count; i++)
            {
                int index = placementIndices[i];
                if (!Bag.Slots[index].IsActivated) continue;

                previewIndices.Add(index);
            }
        }

        ApplyPreview();
    }

    public void ClearPreview()
    {
        EnsurePreviewStates();
        previewIndices.Clear();
        previewValid = false;
        ApplyPreview();
    }

    public bool TryMergeItems(Item source, Vector2 screenPosition, Camera eventCamera)
    {
        if (preparePhaseMode == PreparePhaseMode.ItemCompress) return false;

        if (!TryGetMergeTarget(source, screenPosition, out Item target))
        {
            return false;
        }

        if (!source.CanMergeWith(target))
        {
            return false;
        }

        target.UpgradeGrade();
        Destroy(source.gameObject);
        return true;
    }

    public bool TryPlaceItem(Item item, Vector2 screenPosition, Camera eventCamera)
    {
        if (preparePhaseMode == PreparePhaseMode.ItemCompress) return false;
        if (Bag == null || !Bag.gameObject.activeInHierarchy) return false;

        if (!TryGetPlacement(item, screenPosition, eventCamera, placementIndices, out bool canPlace))
        {
            return false;
        }

        if (!canPlace) return false;
        if (!Bag.TryGetSlotIndex(screenPosition, eventCamera, out int pivotIndex)) return false;

        for (int i = 0; i < placementIndices.Count; i++)
        {
            Bag.Slots[placementIndices[i]].SetActivated(false);
        }

        item.PlaceOnBag(Bag.Items, Bag.Slots[pivotIndex].RectTransform, Bag.CellSize);
        item.SetOccupiedSlots(placementIndices);
        return true;
    }

    public bool TryPlaceItemOnShop(Item item, Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryGetShopSlot(screenPosition, eventCamera, out RectTransform shopSlot))
        {
            return false;
        }

        if (!IsShopSlotEmpty(shopSlot, item))
        {
            return false;
        }

        item.PlaceOnShop(shopSlot);
        return true;
    }

    public bool TryPlaceItemOnClear(
        Item item,
        Vector2 screenPosition,
        Camera eventCamera,
        Transform dragStartParent)
    {
        if (preparePhaseMode != PreparePhaseMode.ItemCompress) return false;
        if (!IsUnderShop(dragStartParent) && !IsUnderClear(dragStartParent)) return false;

        if (!TryGetClearSlot(screenPosition, eventCamera, out RectTransform clearSlot))
        {
            return false;
        }

        if (!IsClearSlotEmpty(clearSlot, item))
        {
            return false;
        }

        item.PlaceOnClear(clearSlot);
        return true;
    }

    public void ReleaseSlots(IReadOnlyList<int> indices)
    {
        SetSlotsActivated(indices, true);
    }

    public void OccupySlots(IReadOnlyList<int> indices)
    {
        SetSlotsActivated(indices, false);
    }

    private void SetSlotsActivated(IReadOnlyList<int> indices, bool activated)
    {
        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            if (index < 0 || index >= Bag.Slots.Length) continue;

            Slot slot = Bag.Slots[index];
            if (activated && slot.IsLocked) continue;
            slot.SetActivated(activated);
        }
    }

    private bool TryGetPlacement(
        Item item,
        Vector2 screenPosition,
        Camera eventCamera,
        List<int> results,
        out bool canPlace)
    {
        results.Clear();
        canPlace = false;

        if (!Bag.TryGetSlotIndex(screenPosition, eventCamera, out int pivotIndex))
        {
            return false;
        }

        int columns = Bag.Size;
        int pivotCol = pivotIndex % columns;
        int pivotRow = pivotIndex / columns;
        bool allValid = true;

        item.GetShapeOffsets(shapeBuffer);
        for (int i = 0; i < shapeBuffer.Count; i++)
        {
            Vector2Int offset = shapeBuffer[i];
            int col = pivotCol + offset.x;
            int row = pivotRow + offset.y;

            if (col < 0 || col >= columns || row < 0)
            {
                allValid = false;
                continue;
            }

            int index = row * columns + col;
            if (index < 0 || index >= Bag.Slots.Length)
            {
                allValid = false;
                continue;
            }

            results.Add(index);

            if (!Bag.Slots[index].IsActivated)
            {
                allValid = false;
            }
        }

        if (results.Count != shapeBuffer.Count)
        {
            allValid = false;
        }

        canPlace = allValid && results.Count > 0;
        return results.Count > 0;
    }

    private void ApplyPreview()
    {
        EnsurePreviewStates();
        Slot[] slots = Bag.Slots;
        for (int i = 0; i < slots.Length; i++)
        {
            bool shouldPreview = previewIndices.Contains(i);
            if (!shouldPreview && !previewStates[i]) continue;

            previewStates[i] = shouldPreview;
            slots[i].SetPreview(shouldPreview, previewValid);
        }
    }

    private void EnsurePreviewStates()
    {
        int count = Bag.Slots.Length;
        if (previewStates != null && previewStates.Length == count) return;
        previewStates = new bool[count];
    }

    private bool TryGetShopSlot(Vector2 screenPosition, Camera eventCamera, out RectTransform shopSlot)
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    shopSlots[i],
                    screenPosition,
                    eventCamera))
            {
                shopSlot = shopSlots[i];
                return true;
            }
        }

        shopSlot = null;
        return false;
    }

    private static bool IsShopSlotEmpty(RectTransform shopSlot, Item ignore)
    {
        for (int i = 0; i < shopSlot.childCount; i++)
        {
            Item item = shopSlot.GetChild(i).GetComponent<Item>();
            if (item != null && item != ignore)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClearSlotEmpty(RectTransform clearSlot, Item ignore)
    {
        for (int i = 0; i < clearSlot.childCount; i++)
        {
            Item item = clearSlot.GetChild(i).GetComponent<Item>();
            if (item != null && item != ignore)
            {
                return false;
            }
        }

        return true;
    }

    private void ClearClearSlotItems()
    {
        for (int i = 0; i < clearSlots.Length; i++)
        {
            RectTransform clearSlot = clearSlots[i];
            for (int childIndex = clearSlot.childCount - 1; childIndex >= 0; childIndex--)
            {
                Item item = clearSlot.GetChild(childIndex).GetComponent<Item>();
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
        }
    }

    private static void RemoveShopItemsFromSlot(RectTransform shopSlot, bool immediate = false)
    {
        for (int childIndex = shopSlot.childCount - 1; childIndex >= 0; childIndex--)
        {
            Item item = shopSlot.GetChild(childIndex).GetComponent<Item>();
            if (item == null) continue;

            if (immediate)
            {
                DestroyImmediate(item.gameObject);
            }
            else
            {
                Destroy(item.gameObject);
            }
        }
    }

    private bool TryGetClearSlot(Vector2 screenPosition, Camera eventCamera, out RectTransform clearSlot)
    {
        for (int i = 0; i < clearSlots.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    clearSlots[i],
                    screenPosition,
                    eventCamera))
            {
                clearSlot = clearSlots[i];
                return true;
            }
        }

        clearSlot = null;
        return false;
    }

    private bool TryGetMergeTarget(
        Item source,
        Vector2 screenPosition,
        out Item target)
    {
        target = null;
        if (EventSystem.current == null) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            Item hitItem = raycastResults[i].gameObject.GetComponentInParent<Item>();
            if (hitItem == null || hitItem == source) continue;
            if (!hitItem.transform.IsChildOf(transform)) continue;

            target = hitItem;
            return true;
        }

        return false;
    }
}
