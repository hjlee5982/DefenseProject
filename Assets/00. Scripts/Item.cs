using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Item : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum ItemCategory
    {
        Fire,
        Grimoire,
        Ice,
        Water,
        Wind,
        Rock,
        Buff
    }

    [SerializeField] private float rotateDuration = 0.15f;
    [SerializeField] private float cellSize = 100f;
    [SerializeField] private ItemCategory category = ItemCategory.Fire;
    [SerializeField] private int grade = 1;
    [SerializeField] private Color[] gradeColors =
    {
        new Color(0.564f, 1f, 0.561f, 1f),
        new Color(0.65f, 0.82f, 1f, 1f),
        new Color(0.85f, 0.7f, 1f, 1f),
        new Color(1f, 0.85f, 0.6f, 1f),
        new Color(1f, 0.7f, 0.75f, 1f)
    };
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Projectile additionalProjectilePrefab;
    [SerializeField] private Sprite icon;
    [SerializeField] private Color iconColor = Color.white;

    private RectTransform rectTransform;
    private RectTransform parentRect;
    private RectTransform pivotRect;
    private InventoryManager inventoryManager;
    private bool isDragging;
    private Vector2 pointerFollowPoint;
    private Vector2 dragGrabOffsetLocal;
    private Vector2 lastScreenPosition;
    private Camera lastPressCamera;
    private Tween rotateTween;
    private float angleZ;
    private float targetAngleZ;
    private readonly List<Vector2Int> localShapeCells = new();
    private readonly List<int> occupiedSlots = new();
    private readonly List<int> slotsAtDragStart = new();
    private readonly List<Image> gradeImages = new();

    private Transform dragStartParent;
    private Vector2 dragStartAnchoredPosition;
    private Quaternion dragStartLocalRotation;
    private Vector3 dragStartLocalScale;
    private float dragStartAngleZ;
    private float dragStartTargetAngleZ;

    public enum BuffOptionType
    {
        None,
        ExtraFireInterval,
        ExtraDamage,
        Critical,
        ExtraAddRange
    }

    private const string BuffPotionKey = "Item_Buff_Potion";
    private const string BuffScrollKey = "Item_Buff_Scroll";
    private const string BuffQuillPenKey = "Item_Buff_QuillPen";
    private const string BuffCrystalBallKey = "Item_Buff_CrystalBall";

    private string dataId;
    private string displayName;
    private float baseRange;
    private float baseDamage;
    private float baseFireInterval;
    private float addRange = 1f;
    private float addDamage = 1f;
    private float addFireInterval = 1f;
    private bool hasCombatStats;
    private BuffOptionType buffOptionType;
    private float buffOptionPercent;

    public Projectile ProjectilePrefab => projectilePrefab;
    public Projectile AdditionalProjectilePrefab => additionalProjectilePrefab;
    public Sprite Icon => icon;
    public Color IconColor => iconColor;
    public int Grade => grade;
    public Color GradeColor => GetGradeColor();
    public int MaxGrade => gradeColors != null && gradeColors.Length > 0 ? gradeColors.Length : 1;
    public string ShopBanKey => GetTypeKey();
    public string DataId => dataId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? GetTypeKey() : displayName;
    public float Range => hasCombatStats ? GetScaledRange() : GetFallbackRange();
    public int Damage => hasCombatStats ? GetScaledDamage() : GetFallbackDamage();
    public float FireInterval => hasCombatStats ? GetScaledFireInterval() : GetFallbackFireInterval();
    public ItemCategory Category => category;
    public bool IsBuff => category == ItemCategory.Buff;
    public bool IsWeapon => !IsBuff;
    public bool IsProjectileWeapon => IsWeapon && projectilePrefab != null;
    public BuffOptionType BuffOption => buffOptionType;
    public float BuffOptionPercent => buffOptionPercent;
    public float BuffContributionPercent =>
        buffOptionType == BuffOptionType.None ? 0f : grade * buffOptionPercent;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentRect = transform.parent as RectTransform;
        pivotRect = transform.Find("Pivot") as RectTransform;
        inventoryManager = GetComponentInParent<InventoryManager>();

        angleZ = Mathf.Round(rectTransform.localEulerAngles.z / 90f) * 90f;
        targetAngleZ = angleZ;
        rectTransform.localEulerAngles = new Vector3(0f, 0f, angleZ);

        CacheShapeCells();
        CacheGradeImages();
        ApplyGradeColor();
        ApplyGameData();
    }

    private void OnDestroy()
    {
        rotateTween?.Kill();
        if (isDragging && inventoryManager != null)
            inventoryManager.NotifyItemDragEnded(this);
    }

    private void Update()
    {
        if (inventoryManager != null && inventoryManager.DisableRotation) return;
        if (!isDragging) return;
        if (Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame) return;

        TryRotate(clockwise: true);
    }

    public bool CanMergeWith(Item other)
    {
        if (other == null || other == this) return false;
        if (grade != other.grade) return false;
        if (grade >= MaxGrade) return false;
        return IsSameType(other);
    }

    public bool IsSameType(Item other)
    {
        if (other == null) return false;
        return GetTypeKey() == other.GetTypeKey();
    }

    public void SetGrade(int newGrade)
    {
        grade = Mathf.Clamp(newGrade, 1, MaxGrade);
        ApplyGradeColor();
    }

    public void UpgradeGrade()
    {
        SetGrade(grade + 1);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        if (inventoryManager != null)
            inventoryManager.NotifyItemDragBegan(this);

        dragStartParent = transform.parent;
        dragStartAnchoredPosition = rectTransform.anchoredPosition;
        dragStartLocalRotation = rectTransform.localRotation;
        dragStartLocalScale = rectTransform.localScale;
        dragStartAngleZ = angleZ;
        dragStartTargetAngleZ = targetAngleZ;

        slotsAtDragStart.Clear();
        slotsAtDragStart.AddRange(occupiedSlots);
        if (slotsAtDragStart.Count > 0)
        {
            inventoryManager.ReleaseSlots(slotsAtDragStart);
            occupiedSlots.Clear();
        }

        if (inventoryManager.IsUnderShop(dragStartParent))
        {
            float scale = inventoryManager.BagCellSize / cellSize;
            rectTransform.localScale = Vector3.one * scale;
        }

        rectTransform.SetParent(inventoryManager.DragLayer, true);
        parentRect = inventoryManager.DragLayer;
        transform.SetAsLastSibling();
        BeginPointerFollow(eventData);
        UpdatePreview(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveToPointer(eventData);
        UpdatePreview(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        rotateTween?.Kill();
        angleZ = targetAngleZ;
        SetAngleZ(angleZ);

        if (inventoryManager.TryMergeItems(this, eventData.position, eventData.pressEventCamera))
        {
            inventoryManager.ClearPreview();
            inventoryManager.NotifyItemDragEnded(this);
            return;
        }

        bool placed;
        if (inventoryManager.IsClearPrepareMode)
        {
            placed =
                inventoryManager.TryPlaceItemOnClear(this, eventData.position, eventData.pressEventCamera, dragStartParent) ||
                inventoryManager.TryPlaceItemOnShop(this, eventData.position, eventData.pressEventCamera);
        }
        else
        {
            placed =
                inventoryManager.TryPlaceItem(this, eventData.position, eventData.pressEventCamera) ||
                inventoryManager.TryPlaceItemOnShop(this, eventData.position, eventData.pressEventCamera) ||
                inventoryManager.TryPlaceItemOnClear(this, eventData.position, eventData.pressEventCamera, dragStartParent);
        }

        inventoryManager.ClearPreview();

        if (!placed)
        {
            RestoreDragStartState();
            if (slotsAtDragStart.Count > 0)
            {
                inventoryManager.OccupySlots(slotsAtDragStart);
                occupiedSlots.Clear();
                occupiedSlots.AddRange(slotsAtDragStart);
            }
        }

        inventoryManager.NotifyItemDragEnded(this);
    }

    public void PlaceOnBag(Transform itemsParent, RectTransform pivotSlot, float bagCellSize)
    {
        float scale = bagCellSize / cellSize;
        rectTransform.SetParent(itemsParent, false);
        parentRect = itemsParent as RectTransform;
        rectTransform.localScale = Vector3.one * scale;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angleZ);

        Vector3 delta = pivotSlot.position - pivotRect.position;
        rectTransform.position += delta;
    }

    public void PlaceOnShop(RectTransform shopSlot)
    {
        occupiedSlots.Clear();
        rectTransform.SetParent(shopSlot, false);
        parentRect = shopSlot;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angleZ);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    public void PlaceOnClear(RectTransform clearSlot)
    {
        occupiedSlots.Clear();
        rectTransform.SetParent(clearSlot, false);
        parentRect = clearSlot;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angleZ);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    public void SetOccupiedSlots(List<int> indices)
    {
        occupiedSlots.Clear();
        occupiedSlots.AddRange(indices);
    }

    public void GetShapeOffsets(List<Vector2Int> results)
    {
        results.Clear();

        int steps = Mathf.RoundToInt(-angleZ / 90f);
        steps = ((steps % 4) + 4) % 4;

        for (int i = 0; i < localShapeCells.Count; i++)
        {
            Vector2Int cell = localShapeCells[i];
            for (int step = 0; step < steps; step++)
            {
                cell = new Vector2Int(cell.y, -cell.x);
            }

            results.Add(new Vector2Int(cell.x, -cell.y));
        }
    }

    private string GetTypeKey()
    {
        string objectName = name;
        const string cloneSuffix = "(Clone)";
        int cloneIndex = objectName.IndexOf(cloneSuffix);
        if (cloneIndex >= 0)
        {
            objectName = objectName.Substring(0, cloneIndex).TrimEnd();
        }

        return objectName;
    }

    private void ApplyGameData()
    {
        if (!GameDataRepository.TryGetItemByPrefabKey(GetTypeKey(), out ItemData itemData))
            return;

        dataId = itemData.Id;
        displayName = itemData.Name;
        ApplyBuffOptionData(itemData);

        if (!itemData.HasCombatStats) return;

        hasCombatStats = true;
        baseRange = itemData.Range;
        baseDamage = itemData.Damage;
        baseFireInterval = itemData.FireInterval;
        addRange = itemData.AddRange > 0f ? itemData.AddRange : 1f;
        addDamage = itemData.AddDamage > 0f ? itemData.AddDamage : 1f;
        addFireInterval = itemData.AddFireInterval > 0f ? itemData.AddFireInterval : 1f;
    }

    private void ApplyBuffOptionData(ItemData itemData)
    {
        buffOptionType = BuffOptionType.None;
        buffOptionPercent = 0f;

        string key = GetTypeKey();
        if (key == BuffPotionKey)
        {
            buffOptionType = BuffOptionType.ExtraFireInterval;
            buffOptionPercent = ResolveBuffPercent(itemData.ExtraFireInterval, itemData.Range);
        }
        else if (key == BuffScrollKey)
        {
            buffOptionType = BuffOptionType.ExtraDamage;
            buffOptionPercent = ResolveBuffPercent(itemData.ExtraDamage, itemData.Range);
        }
        else if (key == BuffQuillPenKey)
        {
            buffOptionType = BuffOptionType.Critical;
            buffOptionPercent = ResolveBuffPercent(itemData.Critical, itemData.Range);
        }
        else if (key == BuffCrystalBallKey)
        {
            buffOptionType = BuffOptionType.ExtraAddRange;
            buffOptionPercent = ResolveBuffPercent(itemData.ExtraAddRange, itemData.Range);
        }
    }

    private static float ResolveBuffPercent(float dedicatedValue, float rangeFallback)
    {
        if (dedicatedValue > 0f) return dedicatedValue;
        return rangeFallback > 0f ? rangeFallback : 0f;
    }

    private float GetScaledRange()
    {
        return baseRange * GetGradeMultiplier(addRange);
    }

    private int GetScaledDamage()
    {
        return Mathf.Max(0, Mathf.RoundToInt(baseDamage * GetGradeMultiplier(addDamage)));
    }

    private float GetScaledFireInterval()
    {
        float divisor = GetGradeMultiplier(addFireInterval);
        return divisor > 0f ? baseFireInterval / divisor : baseFireInterval;
    }

    private float GetGradeMultiplier(float perGradeFactor)
    {
        int steps = Mathf.Max(0, grade - 1);
        if (steps <= 0 || perGradeFactor <= 0f) return 1f;
        return Mathf.Pow(perGradeFactor, steps);
    }

    private float GetFallbackRange()
    {
        return projectilePrefab != null ? projectilePrefab.Range : 0f;
    }

    private int GetFallbackDamage()
    {
        return projectilePrefab != null ? projectilePrefab.Damage : 0;
    }

    private float GetFallbackFireInterval()
    {
        return projectilePrefab != null ? projectilePrefab.FireInterval : 1f;
    }

    private void RestoreDragStartState()
    {
        rectTransform.SetParent(dragStartParent, false);
        parentRect = dragStartParent as RectTransform;
        rectTransform.anchoredPosition = dragStartAnchoredPosition;
        rectTransform.localRotation = dragStartLocalRotation;
        rectTransform.localScale = dragStartLocalScale;
        angleZ = dragStartAngleZ;
        targetAngleZ = dragStartTargetAngleZ;
    }

    private void CacheShapeCells()
    {
        localShapeCells.Clear();
        Vector2 pivotPos = pivotRect.anchoredPosition;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "Block" && child.name != "Pivot") continue;

            Vector2 offset = ((RectTransform)child).anchoredPosition - pivotPos;
            localShapeCells.Add(new Vector2Int(
                Mathf.RoundToInt(offset.x / cellSize),
                Mathf.RoundToInt(offset.y / cellSize)));
        }
    }

    private void CacheGradeImages()
    {
        gradeImages.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "Block" && child.name != "Pivot") continue;

            Transform imageTransform = child.Find("Image");
            if (imageTransform == null) continue;

            Image image = imageTransform.GetComponent<Image>();
            if (image != null) gradeImages.Add(image);
        }
    }

    private void ApplyGradeColor()
    {
        Color color = GetGradeColor();
        for (int i = 0; i < gradeImages.Count; i++)
        {
            gradeImages[i].color = color;
        }
    }

    private Color GetGradeColor()
    {
        if (gradeColors == null || gradeColors.Length == 0)
        {
            return Color.white;
        }

        int index = Mathf.Clamp(grade - 1, 0, gradeColors.Length - 1);
        return gradeColors[index];
    }

    private void BeginPointerFollow(PointerEventData eventData)
    {
        bool snapToPivot = inventoryManager == null || inventoryManager.SnapDragToPivot;
        if (snapToPivot || pivotRect == null)
        {
            dragGrabOffsetLocal = pivotRect != null ? pivotRect.anchoredPosition : Vector2.zero;
        }
        else if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                     rectTransform,
                     eventData.position,
                     eventData.pressEventCamera,
                     out dragGrabOffsetLocal))
        {
            dragGrabOffsetLocal = pivotRect.anchoredPosition;
        }

        MoveToPointer(eventData);
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        pointerFollowPoint = localPoint;
        ApplyPointerFollow();
    }

    public bool TryRotate(bool clockwise)
    {
        if (inventoryManager != null && inventoryManager.DisableRotation) return false;
        if (!isDragging) return false;

        targetAngleZ += clockwise ? -90f : 90f;

        rotateTween?.Kill();
        rotateTween = DOTween.To(() => angleZ, SetAngleZ, targetAngleZ, rotateDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                angleZ = targetAngleZ;
                SetAngleZ(angleZ);
            });
        return true;
    }

    private void SetAngleZ(float z)
    {
        angleZ = z;
        rectTransform.localEulerAngles = new Vector3(0f, 0f, z);
        ApplyPointerFollow();

        if (isDragging)
        {
            inventoryManager.UpdateItemPreview(this, lastScreenPosition, lastPressCamera);
        }
    }

    private void ApplyPointerFollow()
    {
        Vector2 grabOffset = GetGrabOffsetInParentSpace();
        grabOffset.x *= rectTransform.localScale.x;
        grabOffset.y *= rectTransform.localScale.y;
        rectTransform.anchoredPosition = pointerFollowPoint - grabOffset;
    }

    private Vector2 GetGrabOffsetInParentSpace()
    {
        return rectTransform.localRotation * (Vector3)dragGrabOffsetLocal;
    }

    public Vector2 GetPivotScreenPosition(Camera eventCamera)
    {
        Transform pivot = pivotRect != null ? pivotRect : rectTransform;
        return RectTransformUtility.WorldToScreenPoint(eventCamera, pivot.position);
    }

    private void UpdatePreview(PointerEventData eventData)
    {
        lastScreenPosition = eventData.position;
        lastPressCamera = eventData.pressEventCamera;
        inventoryManager.UpdateItemPreview(this, lastScreenPosition, lastPressCamera);
    }
}
