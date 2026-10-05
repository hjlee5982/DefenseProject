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

    [SerializeField] private int maxHp = 100;
    [SerializeField] private int contactDamage = 10;
    [SerializeField] private Slider hpGauge;
    [SerializeField] private float hpGaugeTweenDuration = 0.25f;
    [SerializeField] private Color hpGaugeFullColor = new Color(0.82f, 0.95f, 0.48f, 1f);
    [SerializeField] private Color hpGaugeEmptyColor = new Color(1f, 0.42f, 0.42f, 1f);
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite fullHpSprite;
    [SerializeField] private HpSpriteStage[] damagedSprites;

    private int hp;
    private Tween hpGaugeTween;
    private Image hpGaugeFillImage;

    public int Hp => hp;
    public int MaxHp => maxHp;
    public int ContactDamage => contactDamage;

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
        hp = Mathf.Max(1, maxHp);
        RefreshVisuals(animateGauge: false);
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
        if (amount <= 0 || hp <= 0) return;

        hp = Mathf.Max(0, hp - amount);
        RefreshVisuals(animateGauge: true);
    }

    public void RestoreFullHp()
    {
        hp = Mathf.Max(1, maxHp);
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
        hpGauge.minValue = 0f;
        hpGauge.maxValue = maxHp;
        KillHpGaugeTween();

        if (!animate || !hpGauge.gameObject.activeInHierarchy)
        {
            hpGauge.value = hp;
            ApplyHpGaugeColor(GetHpRatio(hp));
            return;
        }

        hpGaugeTween = hpGauge
            .DOValue(hp, hpGaugeTweenDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnUpdate(() => ApplyHpGaugeColor(GetHpRatio(hpGauge.value)));
    }

    private void ResolveHpGaugeFillImage()
    {
        if (hpGaugeFillImage != null) return;
        if (hpGauge == null || hpGauge.fillRect == null) return;

        hpGaugeFillImage = hpGauge.fillRect.GetComponent<Image>();
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
        if (hpGaugeTween == null) return;
        hpGaugeTween.Kill();
        hpGaugeTween = null;
    }

    private void RefreshSprite()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = ResolveSpriteForCurrentHp();
    }

    private Sprite ResolveSpriteForCurrentHp()
    {
        float hpPercent = maxHp > 0 ? (hp * 100f) / maxHp : 0f;
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
