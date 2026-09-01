using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slot : MonoBehaviour, IPointerClickHandler
{
    private static readonly Color DefaultColor = Color.white;
    private static readonly Color DeactivatedColor = Color.gray;
    private static readonly Color ValidPreviewColor = Color.blue;
    private static readonly Color InvalidPreviewColor = Color.red;

    private Image image;
    private GameObject lockDisplay;
    private GameObject plusDisplay;
    private GameObject plusGreenDisplay;
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

        Transform lockTransform = transform.Find("Lock");
        if (lockTransform != null)
        {
            lockDisplay = lockTransform.gameObject;
            DisableRaycast(lockTransform);
        }

        Transform plusTransform = transform.Find("Plus");
        if (plusTransform != null)
        {
            plusDisplay = plusTransform.gameObject;
            DisableRaycast(plusTransform);
        }

        Transform plusGreenTransform = transform.Find("Plus_Green");
        if (plusGreenTransform != null)
        {
            plusGreenDisplay = plusGreenTransform.gameObject;
            DisableRaycast(plusGreenTransform);
        }

        ApplyLockVisibility();
        ApplyPlusVisibility();
        ApplyPlusGreenVisibility();
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
        ApplyBaseColor();
    }

    public void SetActivated(bool activated)
    {
        isActivated = activated;
        ApplyBaseColor();
    }

    public void SetPreview(bool enabled, bool isValid = true)
    {
        if (!enabled)
        {
            ApplyBaseColor();
            return;
        }

        image.color = isValid ? ValidPreviewColor : InvalidPreviewColor;
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

    private void ApplyPlusGreenVisibility()
    {
        if (plusGreenDisplay == null) return;

        bool showPlusGreen = isLocked && isExpandable && isExpansionSelected;
        plusGreenDisplay.SetActive(showPlusGreen);

        Image plusGreenImage = plusGreenDisplay.GetComponent<Image>();
        if (plusGreenImage != null)
        {
            plusGreenImage.enabled = showPlusGreen;
        }
    }

    private void ApplyLockVisibility()
    {
        if (lockDisplay == null) return;

        bool showLock = isLocked && !isExpandable;
        lockDisplay.SetActive(showLock);

        Image lockImage = lockDisplay.GetComponent<Image>();
        if (lockImage != null)
        {
            lockImage.enabled = showLock;
        }
    }

    private void ApplyPlusVisibility()
    {
        if (plusDisplay == null) return;

        bool showPlus = isLocked && isExpandable && !isExpansionSelected;
        plusDisplay.SetActive(showPlus);

        Image plusImage = plusDisplay.GetComponent<Image>();
        if (plusImage != null)
        {
            plusImage.enabled = showPlus;
        }
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
