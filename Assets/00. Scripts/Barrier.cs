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
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite fullHpSprite;
    [SerializeField] private HpSpriteStage[] damagedSprites;

    private int hp;
    private Tween hpGaugeTween;

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

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || hp <= 0) return;

        hp = Mathf.Max(0, hp - amount);
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

        hpGauge.minValue = 0f;
        hpGauge.maxValue = maxHp;
        KillHpGaugeTween();

        if (!animate || !hpGauge.gameObject.activeInHierarchy)
        {
            hpGauge.value = hp;
            return;
        }

        hpGaugeTween = hpGauge
            .DOValue(hp, hpGaugeTweenDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
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
