using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slot : MonoBehaviour, IPointerClickHandler
{
    private static readonly Color DefaultColor = Color.white;
    private static readonly Color DeactivatedColor = Color.gray;

    private Image image;
    private GameObject lockDisplay;
    private GameObject plusDisplay;
    private GameObject plusGreenDisplay;
    private GameObject blueDisplay;
    private GameObject redDisplay;
    private InventoryManager inventoryManager;
    private bool isLocked;
    private bool isExpandable;
    private bool isExpansionSelected;
    public bool isActivated = true;

    public RectTransform RectTransform { get; private set; }
    public bool IsActivated => isActivated;
    public bool IsLocked => isLocked;
    public bool IsExpandable => isExpandable;
    public bool IsExpansionSelected => isExpansionSelected;

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        inventoryManager = GetComponentInParent<InventoryManager>();

        CacheChildDisplay("Lock", out lockDisplay);
        CacheChildDisplay("Plus", out plusDisplay);
        CacheChildDisplay("Plus_Green", out plusGreenDisplay);
        CacheChildDisplay("Blue", out blueDisplay);
        CacheChildDisplay("Red", out redDisplay);

        ApplyLockVisibility();
        ApplyPlusVisibility();
        ApplyPlusGreenVisibility();
        ApplyPreviewVisibility(false, true);
        ApplyBaseColor();
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        if (!isLocked)
        {
            isExpandable = false;
        }

        ApplyLockVisibility();
        ApplyPlusVisibility();
        ApplyPlusGreenVisibility();
    }

    public void SetExpandable(bool expandable)
    {
        if (!isLocked)
        {
            isExpandable = false;
            SetExpansionSelected(false);
            ApplyPlusVisibility();
            ApplyPlusGreenVisibility();
            return;
        }

        isExpandable = expandable;
        if (!expandable)
        {
            SetExpansionSelected(false);
        }

        if (image != null)
        {
            image.raycastTarget = isLocked;
        }

        ApplyLockVisibility();
        ApplyPlusVisibility();
        ApplyPlusGreenVisibility();
    }

    public void SetExpansionSelected(bool selected)
    {
        if (!isLocked || !isExpandable)
        {
            selected = false;
        }

        isExpansionSelected = selected;
        ApplyPlusVisibility();
        ApplyPlusGreenVisibility();
        ApplyBaseColor();
    }

    public void Unlock()
    {
        isLocked = false;
        isExpandable = false;
        isExpansionSelected = false;
        isActivated = true;
        ApplyLockVisibility();
        ApplyPlusVisibility();
        ApplyPlusGreenVisibility();
        ApplyPreviewVisibility(false, true);
        ApplyBaseColor();
    }

    public void SetActivated(bool activated)
    {
        isActivated = activated;
        ApplyBaseColor();
    }

    public void SetPreview(bool enabled, bool isValid = true)
    {
        ApplyPreviewVisibility(enabled, isValid);
        if (!enabled)
            ApplyBaseColor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventoryManager == null)
        {
            inventoryManager = GetComponentInParent<InventoryManager>();
        }

        if (inventoryManager == null) return;
        inventoryManager.TryToggleExpansionSelection(this);
    }

    private void ApplyBaseColor()
    {
        image.color = isActivated ? DefaultColor : DeactivatedColor;
    }

    private void ApplyPreviewVisibility(bool enabled, bool isValid)
    {
        SetDisplayVisible(blueDisplay, enabled && isValid);
        SetDisplayVisible(redDisplay, enabled && !isValid);
    }

    private void ApplyPlusGreenVisibility()
    {
        SetDisplayVisible(plusGreenDisplay, isLocked && isExpandable && isExpansionSelected);
    }

    private void ApplyLockVisibility()
    {
        SetDisplayVisible(lockDisplay, isLocked && !isExpandable);
    }

    private void ApplyPlusVisibility()
    {
        SetDisplayVisible(plusDisplay, isLocked && isExpandable && !isExpansionSelected);
    }

    private void CacheChildDisplay(string childName, out GameObject display)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            display = null;
            return;
        }

        display = child.gameObject;
        DisableRaycast(child);
    }

    private static void SetDisplayVisible(GameObject display, bool visible)
    {
        if (display == null) return;

        display.SetActive(visible);

        Image displayImage = display.GetComponent<Image>();
        if (displayImage != null)
            displayImage.enabled = visible;
    }

    private static void DisableRaycast(Transform target)
    {
        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = false;
        }
    }
}
