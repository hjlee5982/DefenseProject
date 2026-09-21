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
            {
                slot.cooldownOverlay.fillAmount = 0f;
                slot.cooldownOverlay.enabled = false;
            }
        }
    }

    public void ShowItems(IReadOnlyList<Item> items)
    {
        InitializeSlots();

        int slotIndex = 0;
        for (int i = 0; i < items.Count && slotIndex < slots.Length; i++)
        {
            Item item = items[i];
            if (item == null || !item.IsWeapon) continue;
            if (item.IsProjectileWeapon && item.ProjectilePrefab == null) continue;
            if (item.IsInstantWeapon && item.InstantAttackPrefab == null) continue;

            SlotState slot = slots[slotIndex];
            if (slot.icon == null)
            {
                slotIndex++;
                continue;
            }

            ResetCooldown(slot);

            slot.icon.sprite = item.Icon;
            slot.icon.color = item.IconColor;
            slot.icon.enabled = item.Icon != null;
            UpdateOverlaySprite(slot);
            slotIndex++;
        }

        for (; slotIndex < slots.Length; slotIndex++)
        {
            SlotState slot = slots[slotIndex];
            if (slot.icon == null) continue;

            ResetCooldown(slot);

            slot.icon.sprite = null;
            slot.icon.color = Color.white;
            slot.icon.enabled = false;
            UpdateOverlaySprite(slot);
        }
    }

    public void StartCooldown(int slotIndex, float duration)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
        if (duration <= 0f) return;

        SlotState slot = slots[slotIndex];
        if (slot.icon == null || !slot.icon.enabled) return;

        if (slot.cooldownOverlay == null)
            slot.cooldownOverlay = EnsureCooldownOverlay(slot.icon.transform.parent, slot.icon);

        if (slot.cooldownOverlay == null) return;

        slot.cooldownRemaining = duration;
        slot.cooldownDuration = duration;
        UpdateOverlaySprite(slot);
        slot.cooldownOverlay.enabled = true;
        slot.cooldownOverlay.fillAmount = 1f;
    }

    private void InitializeSlots()
    {
        if (slots == null || slots.Length != transform.childCount)
        {
            slots = new SlotState[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform slotRoot = transform.GetChild(i);
                Image icon = ResolveIconImage(slotRoot);
                slots[i] = new SlotState
                {
                    icon = icon,
                    cooldownOverlay = EnsureCooldownOverlay(slotRoot, icon)
                };
            }

            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Transform slotRoot = transform.GetChild(i);
            if (slots[i].icon == null)
                slots[i].icon = ResolveIconImage(slotRoot);
            if (slots[i].cooldownOverlay == null)
                slots[i].cooldownOverlay = EnsureCooldownOverlay(slotRoot, slots[i].icon);
        }
    }

    private static Image ResolveIconImage(Transform slotRoot)
    {
        Transform iconTransform = slotRoot.Find("Icon");
        if (iconTransform == null)
            iconTransform = slotRoot.Find("Image");

        if (iconTransform != null)
        {
            Image named = iconTransform.GetComponent<Image>();
            if (named != null) return named;
        }

        for (int i = 0; i < slotRoot.childCount; i++)
        {
            Transform child = slotRoot.GetChild(i);
            if (child.name == "CooldownOverlay") continue;

            Image childImage = child.GetComponent<Image>();
            if (childImage != null) return childImage;
        }

        return null;
    }

    private Image EnsureCooldownOverlay(Transform slotRoot, Image icon)
    {
        if (slotRoot == null || icon == null) return null;

        Transform underIcon = icon.transform.Find("CooldownOverlay");
        if (underIcon != null)
            Destroy(underIcon.gameObject);

        Transform existing = slotRoot.Find("CooldownOverlay");
        Image overlay;
        if (existing != null)
        {
            overlay = existing.GetComponent<Image>();
            if (overlay == null)
                overlay = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            GameObject overlayObject = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer));
            overlayObject.transform.SetParent(slotRoot, false);
            overlay = overlayObject.AddComponent<Image>();
        }

        RectTransform iconRect = icon.rectTransform;
        RectTransform rect = overlay.rectTransform;
        rect.anchorMin = iconRect.anchorMin;
        rect.anchorMax = iconRect.anchorMax;
        rect.pivot = iconRect.pivot;
        rect.anchoredPosition = iconRect.anchoredPosition;
        rect.sizeDelta = iconRect.sizeDelta;
        rect.localScale = iconRect.localScale;
        rect.SetAsLastSibling();

        overlay.raycastTarget = false;
        overlay.preserveAspect = icon.preserveAspect;
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;
        overlay.color = cooldownOverlayColor;
        overlay.sprite = icon.sprite;
        overlay.enabled = false;
        return overlay;
    }

    private static void ResetCooldown(SlotState slot)
    {
        slot.cooldownRemaining = 0f;
        slot.cooldownDuration = 0f;
        if (slot.cooldownOverlay == null) return;

        slot.cooldownOverlay.fillAmount = 0f;
        slot.cooldownOverlay.enabled = false;
    }

    private static void UpdateOverlaySprite(SlotState slot)
    {
        if (slot.cooldownOverlay == null || slot.icon == null) return;

        slot.cooldownOverlay.sprite = slot.icon.sprite;
        slot.cooldownOverlay.preserveAspect = slot.icon.preserveAspect;
    }
}
