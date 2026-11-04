using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Barrier : MonoBehaviour
{
    [Serializable]
    public struct HpSpriteStage
    {
        [Range(0, 100)] public int hpPercent;
        public Sprite sprite;
    }

    private const int BufferPercentPerLevel = 20;
    private static readonly Color BufferGaugeColor = new Color(78f / 255f, 221f / 255f, 252f / 255f, 1f);

    [SerializeField] private int maxHp = 100;
    [SerializeField] private int contactDamage = 10;
    private int configuredMaxHp;
    [SerializeField] private Slider hpGauge;
    [SerializeField] private Image bufferGaugeFillImage;
    [SerializeField] private float hpGaugeTweenDuration = 0.25f;
    [SerializeField] private Color hpGaugeFullColor = new Color(0.82f, 0.95f, 0.48f, 1f);
    [SerializeField] private Color hpGaugeEmptyColor = new Color(1f, 0.42f, 0.42f, 1f);
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite fullHpSprite;
    [SerializeField] private HpSpriteStage[] damagedSprites;

    private int coreHp;
    private int bufferHp;
    private int coreMaxHp;
    private int bufferMaxHp;
    private int reinforceLevel;
    private Tween hpGaugeTween;
    private Tween bufferGaugeTween;
    private Image hpGaugeFillImage;

    public int Hp => coreHp + bufferHp;
    public int MaxHp => maxHp;
    public int ContactDamage => contactDamage;

    public event Action HpDepleted;

    private void Awake()
    {
        if (hpGauge == null)
            hpGauge = GetComponentInChildren<Slider>(true);
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (fullHpSprite == null && spriteRenderer != null)
            fullHpSprite = spriteRenderer.sprite;

        EnsureHpGaugeColors();
        ResolveHpGaugeFillImage();
        EnsureBufferGaugeFill();
        configuredMaxHp = Mathf.Max(1, maxHp);
        ApplyTitleBarrierBonus();
        InitializeHpPools();
        RefreshVisuals(animateGauge: false);
    }

    private void ApplyTitleBarrierBonus()
    {
        float multiplier = 1f + TitleEnhanceProgress.CurrentBarrierPercent / 100f;
        maxHp = Mathf.Max(1, Mathf.RoundToInt(configuredMaxHp * multiplier));
        reinforceLevel = Mathf.Max(0, TitleEnhanceProgress.GetLevel(TitleEnhanceProgress.BarrierId));
    }

    private void InitializeHpPools()
    {
        int bufferPercent = Mathf.Clamp(reinforceLevel * BufferPercentPerLevel, 0, 100);
        bufferMaxHp = Mathf.Clamp(Mathf.RoundToInt(maxHp * (bufferPercent / 100f)), 0, maxHp);
        coreMaxHp = Mathf.Max(0, maxHp - bufferMaxHp);
        bufferHp = bufferMaxHp;
        coreHp = coreMaxHp;
    }

    private void OnDestroy()
    {
        KillHpGaugeTween();
    }

    public void ApplyContactHit()
    {
        TakeDamage(contactDamage);
    }

    public void ApplyContactHit(int damage)
    {
        TakeDamage(damage > 0 ? damage : contactDamage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || Hp <= 0) return;

        int remaining = amount;
        if (bufferHp > 0)
        {
            int absorbed = Mathf.Min(bufferHp, remaining);
            bufferHp -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0)
            coreHp = Mathf.Max(0, coreHp - remaining);

        RefreshVisuals(animateGauge: true);

        if (Hp <= 0)
            HpDepleted?.Invoke();
    }

    public void RestoreFullHp()
    {
        bufferHp = bufferMaxHp;
        coreHp = coreMaxHp;
        RefreshVisuals(animateGauge: true);
    }

    private void RefreshVisuals(bool animateGauge)
    {
        RefreshGauge(animateGauge);
        RefreshSprite();
    }

    private void RefreshGauge(bool animate)
    {
        if (hpGauge == null) return;

        ResolveHpGaugeFillImage();
        EnsureBufferGaugeFill();
        hpGauge.minValue = 0f;
        hpGauge.maxValue = maxHp;
        KillHpGaugeTween();

        // 완충이 남아 있는 동안 본래 게이지는 줄지 않는다 (bufferMax + core).
        float displayHp = bufferMaxHp + coreHp;
        float bufferRatio = maxHp > 0 ? bufferHp / (float)maxHp : 0f;
        bool showBuffer = bufferMaxHp > 0;

        if (bufferGaugeFillImage != null)
        {
            bufferGaugeFillImage.color = BufferGaugeColor;
            bufferGaugeFillImage.enabled = showBuffer && bufferRatio > 0.0001f;
        }

        if (!animate || !hpGauge.gameObject.activeInHierarchy)
        {
            hpGauge.value = displayHp;
            ApplyHpGaugeColor(GetHpRatio(displayHp));
            SetBufferFillRatio(bufferRatio);
            return;
        }

        float startBuffer = GetBufferFillRatio();
        hpGaugeTween = hpGauge
            .DOValue(displayHp, hpGaugeTweenDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnUpdate(() => ApplyHpGaugeColor(GetHpRatio(hpGauge.value)));

        if (showBuffer && bufferGaugeFillImage != null)
        {
            bufferGaugeTween = DOTween
                .To(() => startBuffer, SetBufferFillRatio, bufferRatio, hpGaugeTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
        else
        {
            SetBufferFillRatio(0f);
        }
    }

    private float GetBufferFillRatio()
    {
        if (bufferGaugeFillImage == null)
            return 0f;

        RectTransform rt = bufferGaugeFillImage.rectTransform;
        return Mathf.Clamp01(rt.anchorMax.x);
    }

    private void SetBufferFillRatio(float ratio)
    {
        if (bufferGaugeFillImage == null)
            return;

        float clamped = Mathf.Clamp01(ratio);
        RectTransform rt = bufferGaugeFillImage.rectTransform;
        // 왼쪽 고정, 가로 비율만 조절 (Sliced)
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(clamped, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        bufferGaugeFillImage.color = BufferGaugeColor;
        bufferGaugeFillImage.enabled = bufferMaxHp > 0 && clamped > 0.0001f;
    }

    private void ResolveHpGaugeFillImage()
    {
        if (hpGaugeFillImage != null) return;
        if (hpGauge == null || hpGauge.fillRect == null) return;

        hpGaugeFillImage = hpGauge.fillRect.GetComponent<Image>();
    }

    private void EnsureBufferGaugeFill()
    {
        if (hpGauge == null || hpGauge.fillRect == null)
            return;

        Transform fillArea = hpGauge.fillRect.parent;
        if (fillArea == null) return;

        if (bufferGaugeFillImage == null)
        {
            Transform existing = fillArea.Find("BufferFill");
            GameObject bufferGo;
            if (existing != null)
            {
                bufferGo = existing.gameObject;
            }
            else
            {
                bufferGo = new GameObject("BufferFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bufferGo.transform.SetParent(fillArea, false);
            }

            bufferGo.transform.SetAsLastSibling();
            bufferGaugeFillImage = bufferGo.GetComponent<Image>();
        }

        RectTransform rt = bufferGaugeFillImage.rectTransform;
        rt.localScale = Vector3.one;
        rt.pivot = new Vector2(0f, 0.5f);

        Image source = hpGauge.fillRect.GetComponent<Image>();
        if (source != null && source.sprite != null)
            bufferGaugeFillImage.sprite = source.sprite;

        bufferGaugeFillImage.type = Image.Type.Sliced;
        bufferGaugeFillImage.pixelsPerUnitMultiplier = 1.5f;
        bufferGaugeFillImage.color = BufferGaugeColor;
        bufferGaugeFillImage.raycastTarget = false;
    }

    private void EnsureHpGaugeColors()
    {
        // 기존 씬 직렬화로 Color 기본값이 (0,0,0,0)이 된 경우 복구
        if (hpGaugeFullColor.maxColorComponent <= 0.001f)
            hpGaugeFullColor = new Color(0.82f, 0.95f, 0.48f, 1f);
        if (hpGaugeEmptyColor.maxColorComponent <= 0.001f)
            hpGaugeEmptyColor = new Color(1f, 0.42f, 0.42f, 1f);
    }

    private float GetHpRatio(float currentHp)
    {
        return maxHp <= 0 ? 0f : Mathf.Clamp01(currentHp / maxHp);
    }

    private void ApplyHpGaugeColor(float ratio)
    {
        if (hpGaugeFillImage == null) return;
        hpGaugeFillImage.color = EvaluateHpGaugeColor(ratio);
    }

    private Color EvaluateHpGaugeColor(float ratio)
    {
        float t = 1f - Mathf.Clamp01(ratio);

        Color.RGBToHSV(hpGaugeFullColor, out float fullH, out float fullS, out float fullV);
        Color.RGBToHSV(hpGaugeEmptyColor, out float emptyH, out float emptyS, out float emptyV);

        float hue = Mathf.Lerp(fullH, emptyH, t);
        float saturation = Mathf.Lerp(fullS, emptyS, t);
        float value = Mathf.Lerp(fullV, emptyV, t);

        Color color = Color.HSVToRGB(hue, saturation, value);
        color.a = Mathf.Lerp(hpGaugeFullColor.a, hpGaugeEmptyColor.a, t);
        return color;
    }

    private void KillHpGaugeTween()
    {
        if (hpGaugeTween != null)
        {
            hpGaugeTween.Kill();
            hpGaugeTween = null;
        }

        if (bufferGaugeTween != null)
        {
            bufferGaugeTween.Kill();
            bufferGaugeTween = null;
        }
    }

    private void RefreshSprite()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = ResolveSpriteForCurrentHp();
    }

    private Sprite ResolveSpriteForCurrentHp()
    {
        float hpPercent = maxHp > 0 ? (Hp * 100f) / maxHp : 0f;
        Sprite selected = fullHpSprite;
        int matchedPercent = int.MaxValue;

        if (damagedSprites == null) return selected;

        for (int i = 0; i < damagedSprites.Length; i++)
        {
            HpSpriteStage stage = damagedSprites[i];
            if (stage.sprite == null) continue;
            if (hpPercent > stage.hpPercent) continue;
            if (stage.hpPercent >= matchedPercent) continue;

            matchedPercent = stage.hpPercent;
            selected = stage.sprite;
        }

        return selected;
    }
}
