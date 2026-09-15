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
        WeaponProjectile,
        Buff,
        WeaponInstant
    }

    [SerializeField] private float rotateDuration = 0.15f;
    [SerializeField] private float cellSize = 100f;
    [SerializeField] private bool disableRotation;
    [SerializeField] private ItemCategory category = ItemCategory.WeaponProjectile;
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
    [SerializeField] private InstantAttack instantAttackPrefab;
    [SerializeField] private Sprite icon;
    [SerializeField] private Color iconColor = Color.white;

    private RectTransform rectTransform;
    private RectTransform parentRect;
    private RectTransform pivotRect;
    private InventoryManager inventoryManager;
    private bool isDragging;
    private Vector2 pivotFollowPoint;
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

    private string dataId;
    private string displayName;
    private float range;
    private int damage;
    private float fireInterval;
    private bool hasCombatStats;

    public Projectile ProjectilePrefab => projectilePrefab;
    public InstantAttack InstantAttackPrefab => instantAttackPrefab;
    public Sprite Icon => icon;
    public Color IconColor => iconColor;
    public int Grade => grade;
    public int MaxGrade => gradeColors != null && gradeColors.Length > 0 ? gradeColors.Length : 1;
    public string ShopBanKey => GetTypeKey();
    public string DataId => dataId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? GetTypeKey() : displayName;
    public float Range => hasCombatStats ? range : GetFallbackRange();
    public int Damage => hasCombatStats ? damage : GetFallbackDamage();
    public float FireInterval => hasCombatStats ? fireInterval : GetFallbackFireInterval();
    public ItemCategory Category => category;
    public bool IsBuff => category == ItemCategory.Buff;
    public bool IsWeapon => category == ItemCategory.WeaponProjectile || category == ItemCategory.WeaponInstant;
    public bool IsProjectileWeapon => category == ItemCategory.WeaponProjectile;
    public bool IsInstantWeapon => category == ItemCategory.WeaponInstant;

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
        if (disableRotation) return;
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
        MoveToPointer(eventData);
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
        ApplyCategoryFromData(itemData);

        if (!itemData.HasCombatStats) return;

        hasCombatStats = true;
        range = itemData.Range;
        damage = itemData.Damage;
        fireInterval = itemData.FireInterval;
    }

    private void ApplyCategoryFromData(ItemData itemData)
    {
        string categoryText = itemData.Category;
        if (string.IsNullOrWhiteSpace(categoryText)) return;

        if (categoryText.Equals("Buff", StringComparison.OrdinalIgnoreCase))
        {
            category = ItemCategory.Buff;
            return;
        }

        if (!categoryText.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
            return;

        if (itemData.WeaponType.Equals("Instant", StringComparison.OrdinalIgnoreCase))
        {
            category = ItemCategory.WeaponInstant;
            return;
        }

        category = ItemCategory.WeaponProjectile;
    }

    private float GetFallbackRange()
    {
        if (projectilePrefab != null) return projectilePrefab.Range;
        if (instantAttackPrefab != null) return instantAttackPrefab.Range;
        return 0f;
    }

    private int GetFallbackDamage()
    {
        if (projectilePrefab != null) return projectilePrefab.Damage;
        if (instantAttackPrefab != null) return instantAttackPrefab.Damage;
        return 0;
    }

    private float GetFallbackFireInterval()
    {
        if (projectilePrefab != null) return projectilePrefab.FireInterval;
        if (instantAttackPrefab != null) return instantAttackPrefab.FireInterval;
        return 1f;
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

        pivotFollowPoint = localPoint;
        ApplyPivotFollow();
    }

    public bool TryRotate(bool clockwise)
    {
        if (disableRotation || !isDragging) return false;

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
        ApplyPivotFollow();

        if (isDragging)
        {
            inventoryManager.UpdateItemPreview(this, lastScreenPosition, lastPressCamera);
        }
    }

    private void ApplyPivotFollow()
    {
        Vector2 pivotOffset = GetPivotOffsetInParentSpace();
        pivotOffset.x *= rectTransform.localScale.x;
        pivotOffset.y *= rectTransform.localScale.y;
        rectTransform.anchoredPosition = pivotFollowPoint - pivotOffset;
    }

    private Vector2 GetPivotOffsetInParentSpace()
    {
        if (pivotRect == null) return Vector2.zero;
        return rectTransform.localRotation * (Vector3)pivotRect.anchoredPosition;
    }

    private void UpdatePreview(PointerEventData eventData)
    {
        lastScreenPosition = eventData.position;
        lastPressCamera = eventData.pressEventCamera;
        inventoryManager.UpdateItemPreview(this, lastScreenPosition, lastPressCamera);
    }
}
