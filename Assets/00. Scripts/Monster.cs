using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Monster : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int maxHp = 1;
    [SerializeField] private int attackPower = 10;
    [SerializeField] private int dropGold = 1000;
    [SerializeField] private int dropExp = 10;
    [SerializeField] private bool castShadow = true;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.08f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private Vector2 shadowScale = Vector2.one;
    [SerializeField] private Collider2D barrierDetector;
    [SerializeField] private Image hpGaugeImage;
    [SerializeField] private float hpGaugeTweenDuration = 0.25f;
    [SerializeField] private Color hpGaugeFullColor = new Color(0.82f, 0.95f, 0.48f, 1f);
    [SerializeField] private Color hpGaugeEmptyColor = new Color(1f, 0.42f, 0.42f, 1f);
    [SerializeField] private int hitFlashCount = 3;
    [SerializeField] private float hitFlashInterval = 0.08f;

    public static event Action AnyDestroyed;
    public static event Action<Monster> Killed;

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private const string HitFlashShaderName = "Defense/SpriteHitFlash";

    private string dataId;
    private int hp;
    private int reservedDamage;
    private bool isDead;
    private bool wasKilled;
    private bool isAttacking;
    private bool waitingHitFlashBeforeDestroy;
    private Coroutine barrierAttackRoutine;
    private Barrier attackTargetBarrier;
    private Animator animator;
    private SpriteRenderer bodyRenderer;
    private Material originalSharedMaterial;
    private Material hitFlashMaterial;
    private float displayedHpRatio = 1f;
    private Tween hpGaugeTween;
    private Tween hitFlashTween;
    private Tween knockbackTween;
    private bool suppressNormalMove;
    private bool isKnockback;
    private bool isMovementStopped;
    private Coroutine temporaryStopRoutine;
    private float animatorSpeedBeforeStop = 1f;
    private readonly Dictionary<MonoBehaviour, int> reservations = new();

    public string DataId => dataId;
    public int Hp => hp;
    public int AttackPower => attackPower;
    public int DropGold => dropGold;
    public int DropExp => dropExp;
    public int ReservedDamage => reservedDamage;
    public int ExpectedRemainingHp => Mathf.Max(0, hp - reservedDamage);
    public bool CanBeTargeted => !isDead && hp > 0 && ExpectedRemainingHp > 0;
    public bool IsDead => isDead;

    private void Awake()
    {
        ApplyGameData();
        hp = Mathf.Max(1, maxHp);
        animator = GetComponent<Animator>();
        bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer != null)
            originalSharedMaterial = bodyRenderer.sharedMaterial;
        ResolveHpGaugeImage();

        if (barrierDetector == null)
        {
            Transform detector = transform.Find("Detector");
            if (detector != null)
                barrierDetector = detector.GetComponent<Collider2D>();
        }

        if (transform.position.x > 0f)
        {
            Vector3 scale = transform.localScale;
            scale.x *= -1f;
            transform.localScale = scale;
        }

        if (castShadow)
            SpriteShadow.Attach(gameObject, shadowOffset, shadowColor, shadowScale);

        SyncHpGaugeFillDirection();
        RefreshHpGauge(animate: false);
    }

    private void OnDestroy()
    {
        KillHpGaugeTween();
        KillHitFlashTween(restoreMaterial: false);
        KillKnockbackTween();
        if (temporaryStopRoutine != null)
        {
            StopCoroutine(temporaryStopRoutine);
            temporaryStopRoutine = null;
        }

        if (hitFlashMaterial != null)
        {
            Destroy(hitFlashMaterial);
            hitFlashMaterial = null;
        }

        CancelInboundProjectiles();
        if (wasKilled) Killed?.Invoke(this);
        AnyDestroyed?.Invoke();
    }

    private void Update()
    {
        if (isDead) return;

        if (isKnockback)
            return;

        if (isMovementStopped)
            return;

        if (suppressNormalMove)
        {
            suppressNormalMove = false;
            return;
        }

        if (isAttacking)
        {
            ValidateBarrierAttackRange();
            return;
        }

        transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
    }

    public void ApplyTemporaryStop(float duration)
    {
        if (isDead || duration <= 0f) return;

        if (temporaryStopRoutine != null)
            StopCoroutine(temporaryStopRoutine);

        temporaryStopRoutine = StartCoroutine(TemporaryStopRoutine(duration));
    }

    private IEnumerator TemporaryStopRoutine(float duration)
    {
        isMovementStopped = true;

        if (animator != null)
        {
            animatorSpeedBeforeStop = animator.speed;
            animator.speed = 0f;
        }

        yield return new WaitForSeconds(duration);

        isMovementStopped = false;
        if (animator != null)
            animator.speed = animatorSpeedBeforeStop;

        temporaryStopRoutine = null;
    }

    public void ApplyConstantWindPull(Vector3 origin, float speed)
    {
        if (isDead || isKnockback || isMovementStopped || speed <= 0f) return;

        suppressNormalMove = true;

        Vector3 toOrigin = origin - transform.position;
        float distance = toOrigin.magnitude;
        if (distance > 0.0001f)
        {
            float step = speed * Time.deltaTime;
            transform.position += toOrigin / distance * Mathf.Min(step, distance);
        }

        ValidateBarrierAttackRange();
    }

    public void ApplyKnockback(Vector3 direction, float distance, float duration = 0.15f)
    {
        if (isDead || distance <= 0f) return;
        if (direction.sqrMagnitude <= 0.0001f) return;

        KillKnockbackTween();
        isKnockback = true;

        Vector3 targetPosition = transform.position + direction.normalized * distance;
        knockbackTween = transform.DOMove(targetPosition, Mathf.Max(0.01f, duration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                isKnockback = false;
                knockbackTween = null;
                ValidateBarrierAttackRange();
            })
            .OnKill(() =>
            {
                isKnockback = false;
                knockbackTween = null;
            });
    }

    private void KillKnockbackTween()
    {
        if (knockbackTween == null) return;
        knockbackTween.Kill();
        knockbackTween = null;
        isKnockback = false;
    }

    public bool TryReserve(MonoBehaviour source, int amount)
    {
        if (isDead || source == null || amount <= 0) return false;
        if (hp <= 0 || ExpectedRemainingHp <= 0) return false;
        if (reservations.ContainsKey(source)) return false;

        reservations.Add(source, amount);
        reservedDamage += amount;
        return true;
    }

    public bool TryApplyReservedHit(MonoBehaviour source)
    {
        if (!TryConsumeReservation(source, out int amount)) return false;
        TakeDamage(amount);
        return true;
    }

    public void ReleaseReservation(MonoBehaviour source)
    {
        TryConsumeReservation(source, out _);
    }

    private bool TryConsumeReservation(MonoBehaviour source, out int amount)
    {
        amount = 0;
        if (source == null) return false;
        if (!reservations.Remove(source, out amount)) return false;

        reservedDamage = Mathf.Max(0, reservedDamage - amount);
        return true;
    }

    private void TakeDamage(int amount)
    {
        if (isDead || amount <= 0 || hp <= 0) return;

        hp = Mathf.Max(0, hp - amount);
        RefreshHpGauge(animate: true);

        bool lethal = hp <= 0;
        if (lethal)
            BeginDeath(killed: true);

        PlayHitFlash(lethal ? FinalizeDeath : null);
    }

    private void PlayHitFlash(Action onComplete = null)
    {
        if (bodyRenderer == null || hitFlashCount <= 0 || !EnsureHitFlashMaterial())
        {
            onComplete?.Invoke();
            return;
        }

        KillHitFlashTween(restoreMaterial: false);
        bodyRenderer.sharedMaterial = hitFlashMaterial;
        SetFlashAmount(0f);

        Sequence sequence = DOTween.Sequence();
        for (int i = 0; i < hitFlashCount; i++)
        {
            sequence.AppendCallback(() => SetFlashAmount(1f));
            sequence.AppendInterval(hitFlashInterval);
            sequence.AppendCallback(() => SetFlashAmount(0f));
            sequence.AppendInterval(hitFlashInterval);
        }

        sequence.OnComplete(() =>
        {
            hitFlashTween = null;
            RestoreOriginalMaterial();
            onComplete?.Invoke();
        });
        sequence.OnKill(() =>
        {
            if (!waitingHitFlashBeforeDestroy)
                RestoreOriginalMaterial();
        });

        hitFlashTween = sequence
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private bool EnsureHitFlashMaterial()
    {
        if (hitFlashMaterial != null) return true;

        Shader shader = Shader.Find(HitFlashShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[{name}] Missing shader '{HitFlashShaderName}'. Hit flash skipped.", this);
            return false;
        }

        hitFlashMaterial = new Material(shader);
        return true;
    }

    private void SetFlashAmount(float amount)
    {
        if (hitFlashMaterial == null) return;
        hitFlashMaterial.SetFloat(FlashAmountId, amount);
    }

    private void RestoreOriginalMaterial()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.sharedMaterial = originalSharedMaterial;
        SetFlashAmount(0f);
    }

    private void KillHitFlashTween(bool restoreMaterial)
    {
        if (hitFlashTween != null)
        {
            hitFlashTween.Kill();
            hitFlashTween = null;
        }

        if (restoreMaterial)
            RestoreOriginalMaterial();
        else
            SetFlashAmount(0f);
    }

    private void BeginDeath(bool killed)
    {
        if (isDead) return;

        isDead = true;
        wasKilled = killed;
        waitingHitFlashBeforeDestroy = true;
        attackTargetBarrier = null;
        isAttacking = false;

        if (barrierAttackRoutine != null)
        {
            StopCoroutine(barrierAttackRoutine);
            barrierAttackRoutine = null;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private void FinalizeDeath()
    {
        waitingHitFlashBeforeDestroy = false;
        Destroy(gameObject);
    }

    private void Die(bool killed)
    {
        BeginDeath(killed);
        FinalizeDeath();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Barrier barrier = other.GetComponent<Barrier>();
        if (barrier == null) return;

        if (barrierDetector != null)
        {
            if (barrierDetector.IsTouching(other))
                BeginBarrierAttack(barrier);
            return;
        }

        barrier.ApplyContactHit(attackPower);
        BeginContactAttackAndDestroy();
    }

    // Bat_Attack Animation Event
    public void OnAttackHit()
    {
        if (isDead || attackTargetBarrier == null) return;
        if (!IsInBarrierAttackRange()) return;
        attackTargetBarrier.ApplyContactHit(attackPower);
    }

    private void BeginBarrierAttack(Barrier barrier)
    {
        if (isDead || isAttacking) return;

        attackTargetBarrier = barrier;
        isAttacking = true;
        if (animator == null) return;

        if (barrierAttackRoutine != null)
            StopCoroutine(barrierAttackRoutine);
        barrierAttackRoutine = StartCoroutine(BarrierAttackLoop());
    }

    private void ValidateBarrierAttackRange()
    {
        if (!isAttacking || attackTargetBarrier == null) return;
        if (IsInBarrierAttackRange()) return;
        CancelBarrierAttack();
    }

    private bool IsInBarrierAttackRange()
    {
        if (attackTargetBarrier == null) return false;

        Collider2D barrierCollider = attackTargetBarrier.GetComponent<Collider2D>();
        if (barrierCollider == null) return false;

        if (barrierDetector != null)
            return barrierDetector.IsTouching(barrierCollider);

        Collider2D bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null)
            return bodyCollider.IsTouching(barrierCollider);

        return false;
    }

    private void CancelBarrierAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;
        attackTargetBarrier = null;

        if (barrierAttackRoutine != null)
        {
            StopCoroutine(barrierAttackRoutine);
            barrierAttackRoutine = null;
        }

        if (animator != null)
            animator.Play(MoveHash, 0, 0f);
    }

    private IEnumerator BarrierAttackLoop()
    {
        while (!isDead && isAttacking)
        {
            if (!IsInBarrierAttackRange())
            {
                CancelBarrierAttack();
                yield break;
            }

            animator.Play(AttackHash, 0, 0f);
            yield return WaitForAnimation(AttackHash);

            if (isDead || !isAttacking) yield break;
            if (!IsInBarrierAttackRange())
            {
                CancelBarrierAttack();
                yield break;
            }

            animator.Play(MoveHash, 0, 0f);
            yield return WaitForAnimation(MoveHash);
        }
    }

    private void BeginContactAttackAndDestroy()
    {
        if (isDead || isAttacking) return;

        isAttacking = true;
        if (animator == null)
        {
            Die(killed: false);
            return;
        }

        animator.Play(AttackHash, 0, 0f);
        StartCoroutine(DestroyAfterAttackAnimation());
    }

    private IEnumerator DestroyAfterAttackAnimation()
    {
        yield return WaitForAnimation(AttackHash);
        Die(killed: false);
    }

    private IEnumerator WaitForAnimation(int stateHash, float timeout = 3f)
    {
        yield return null;

        while (timeout > 0f)
        {
            if (animator == null || isDead) yield break;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == stateHash && info.normalizedTime >= 1f)
                yield break;

            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    private void ResolveHpGaugeImage()
    {
        if (hpGaugeImage != null) return;

        Transform gauge = transform.Find("Canvas/Frame/Gauge");
        if (gauge == null)
            gauge = FindChildRecursive(transform, "Gauge");
        if (gauge == null) return;

        hpGaugeImage = gauge.GetComponent<Image>();
    }

    private void SyncHpGaugeFillDirection()
    {
        ResolveHpGaugeImage();
        if (hpGaugeImage == null) return;

        // 월드 기준으로 오른쪽에서 왼쪽으로 줄어들도록, 좌우 반전 시 Origin을 보정한다.
        bool mirrored = transform.lossyScale.x < 0f;
        hpGaugeImage.fillOrigin = mirrored
            ? (int)Image.OriginHorizontal.Right
            : (int)Image.OriginHorizontal.Left;
    }

    private void RefreshHpGauge(bool animate)
    {
        ResolveHpGaugeImage();
        if (hpGaugeImage == null) return;

        float targetRatio = maxHp <= 0 ? 0f : Mathf.Clamp01((float)hp / maxHp);
        KillHpGaugeTween();

        if (!animate || !hpGaugeImage.gameObject.activeInHierarchy)
        {
            displayedHpRatio = targetRatio;
            ApplyHpGaugeVisual(displayedHpRatio);
            return;
        }

        hpGaugeTween = DOTween.To(
                () => displayedHpRatio,
                value =>
                {
                    displayedHpRatio = value;
                    ApplyHpGaugeVisual(displayedHpRatio);
                },
                targetRatio,
                hpGaugeTweenDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void ApplyHpGaugeVisual(float ratio)
    {
        if (hpGaugeImage == null) return;

        hpGaugeImage.fillAmount = Mathf.Clamp01(ratio);
        hpGaugeImage.color = EvaluateHpGaugeColor(ratio);
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

    private void ApplyGameData()
    {
        if (!GameDataRepository.TryGetMonsterByPrefabKey(GetTypeKey(), out MonsterData monsterData) &&
            !GameDataRepository.TryGetMonsterById(GetTypeKey(), out monsterData))
        {
            return;
        }

        dataId = monsterData.Id;
        if (monsterData.Hp > 0) maxHp = monsterData.Hp;
        if (monsterData.MoveSpeed > 0f) moveSpeed = monsterData.MoveSpeed;
        if (monsterData.Atk > 0) attackPower = monsterData.Atk;
        dropGold = Mathf.Max(0, monsterData.Gold);
        dropExp = Mathf.Max(0, monsterData.Exp);
    }

    private string GetTypeKey()
    {
        string objectName = name;
        const string cloneSuffix = "(Clone)";
        int cloneIndex = objectName.IndexOf(cloneSuffix, StringComparison.Ordinal);
        if (cloneIndex >= 0)
            objectName = objectName.Substring(0, cloneIndex).TrimEnd();

        return objectName;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }

    private void CancelInboundProjectiles()
    {
        if (reservations.Count == 0) return;

        Projectile[] pending = new Projectile[reservations.Count];
        int count = 0;
        foreach (MonoBehaviour source in reservations.Keys)
        {
            if (source is Projectile projectile)
                pending[count++] = projectile;
        }
        reservations.Clear();
        reservedDamage = 0;

        for (int i = 0; i < count; i++)
        {
            if (pending[i] != null)
                pending[i].OnTargetLost();
        }
    }
}
