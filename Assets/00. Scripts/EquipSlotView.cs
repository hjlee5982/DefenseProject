using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EquipSlotView : MonoBehaviour
{
    private class SlotState
    {
        public Image icon;
        public Image cooldownOverlay;
        public float cooldownRemaining;
        public float cooldownDuration;
    }

    [SerializeField] private Color cooldownOverlayColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);

    private SlotState[] slots;

    private void Awake()
    {
        InitializeSlots();
    }

    private void Update()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotState slot = slots[i];
            if (slot == null || slot.cooldownOverlay == null) continue;
            if (slot.cooldownRemaining <= 0f) continue;

            slot.cooldownRemaining -= Time.deltaTime;
            float fill = slot.cooldownDuration > 0f
                ? slot.cooldownRemaining / slot.cooldownDuration
                : 0f;
            slot.cooldownOverlay.fillAmount = Mathf.Clamp01(fill);

            if (slot.cooldownRemaining <= 0f)
                slot.cooldownOverlay.fillAmount = 0f;
        }
    }

    public void ShowItems(IReadOnlyList<Item> items)
    {
        InitializeSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            SlotState slot = slots[i];
            if (slot.icon == null) continue;

            slot.cooldownRemaining = 0f;
            slot.cooldownDuration = 0f;
            if (slot.cooldownOverlay != null)
            {
                slot.cooldownOverlay.fillAmount = 0f;
                slot.cooldownOverlay.enabled = false;
            }

            if (i < items.Count)
            {
                Item item = items[i];
                slot.icon.sprite = item.Icon;
                slot.icon.color = item.IconColor;
                slot.icon.enabled = item.Icon != null;
                UpdateOverlaySprite(slot);
            }
            else
            {
                slot.icon.sprite = null;
                slot.icon.color = Color.white;
                slot.icon.enabled = false;
            }
        }
    }

    public void StartCooldown(int slotIndex, float duration)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
        if (duration <= 0f) return;

        SlotState slot = slots[slotIndex];
        if (slot.icon == null || !slot.icon.enabled) return;

        slot.cooldownRemaining = duration;
        slot.cooldownDuration = duration;
        if (slot.cooldownOverlay != null)
        {
            slot.cooldownOverlay.enabled = true;
            slot.cooldownOverlay.fillAmount = 1f;
        }
    }

    private void InitializeSlots()
    {
        if (slots != null && slots.Length == transform.childCount) return;

        slots = new SlotState[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform slotRoot = transform.GetChild(i);
            Image icon = GetOrCreateIcon(slotRoot);
            slots[i] = new SlotState
            {
                icon = icon,
                cooldownOverlay = EnsureCooldownOverlay(icon)
            };
        }
    }

    private static Image GetOrCreateIcon(Transform slotRoot)
    {
        Transform misplacedOverlay = slotRoot.Find("CooldownOverlay");
        if (misplacedOverlay != null)
            Destroy(misplacedOverlay.gameObject);

        Transform iconTransform = slotRoot.Find("Icon");
        if (iconTransform != null)
        {
            Image existing = iconTransform.GetComponent<Image>();
            if (existing != null) return existing;
        }

        Image rootImage = slotRoot.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.enabled = true;
            rootImage.sprite = null;
        }

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
        iconObject.transform.SetParent(slotRoot, false);

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image icon = iconObject.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        return icon;
    }

    private Image EnsureCooldownOverlay(Image icon)
    {
        if (icon == null) return null;

        Transform existing = icon.transform.Find("CooldownOverlay");
        Image overlay;
        if (existing != null)
        {
            overlay = existing.GetComponent<Image>();
            if (overlay != null)
            {
                overlay.fillClockwise = false;
                return overlay;
            }
        }

        GameObject overlayObject = new GameObject("CooldownOverlay", typeof(RectTransform));
        overlayObject.transform.SetParent(icon.transform, false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlay = overlayObject.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;
        overlay.color = cooldownOverlayColor;
        overlay.enabled = false;
        return overlay;
    }

    private static void UpdateOverlaySprite(SlotState slot)
    {
        if (slot.cooldownOverlay == null) return;

        slot.cooldownOverlay.sprite = slot.icon.sprite;
        slot.cooldownOverlay.fillAmount = 0f;
    }
}
