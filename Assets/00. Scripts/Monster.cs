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
    [Tooltip("체크하면 방벽에 닿는 순간 한 번 타격하고 자폭한다. 해제하면 방벽 앞에서 멈춰 반복 공격한다.")]
    [SerializeField] private bool selfDestructOnContact = false;
    [SerializeField] private bool castShadow = true;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.08f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private Vector2 shadowScale = Vector2.one;
    [Tooltip("방벽 감지용 콜라이더. 비어 있으면 하위의 'Detector' 오브젝트를 찾는다.")]
    [SerializeField] private Collider2D barrierDetector;
    [Tooltip("투사체 피격용 콜라이더. 비어 있으면 하위의 'HitBox' 오브젝트를 찾는다.")]
    [SerializeField] private Collider2D hitBox;
    [SerializeField] private Image hpGaugeImage;
    [SerializeField] private float hpGaugeTweenDuration = 0.25f;
    [SerializeField] private Color hpGaugeFullColor = new Color(0.82f, 0.95f, 0.48f, 1f);
    [SerializeField] private Color hpGaugeEmptyColor = new Color(1f, 0.42f, 0.42f, 1f);
    [SerializeField] private int hitFlashCount = 3;
    [SerializeField] private float hitFlashInterval = 0.08f;

    [Header("Ranged Attack")]
    [Tooltip("체크하면 방벽에 닿지 않고, 일정 거리까지 접근하면 멈춰서 투사체를 쏜다. 근접 공격과 자폭은 하지 않는다.")]
    [SerializeField] private bool rangedAttack = false;
    [Tooltip("체크하면 플레이어가 장착한 무기 중 가장 긴 사거리 위치까지 다가와서 멈추고 공격한다(플레이어가 조준할 수 있는 거리). 장착한 무기가 없을 때는 아래 고정 거리를 쓴다.")]
    [SerializeField] private bool stopAtPlayerMaxRange = true;
    [Tooltip("방벽 윗면에서 이 거리(월드 단위)까지 다가오면 멈추고 공격을 시작한다. 위 옵션을 끄거나 장착한 무기가 없을 때만 쓰인다.")]
    [SerializeField] private float rangedAttackRange = 2f;
    [Tooltip("공격 모션이 끝난 뒤 다음 공격까지 기다리는 시간(초).")]
    [SerializeField] private float rangedAttackInterval = 0.8f;
    [SerializeField] private MonsterProjectile projectilePrefab;
    [Tooltip("투사체가 나가는 위치. 비어 있으면 하위의 'Muzzle' 오브젝트를, 그것도 없으면 히트박스 중심을 쓴다.")]
    [SerializeField] private Transform muzzle;

    [Header("Death")]
    [Tooltip("죽을 때(처치되거나 자폭할 때) 그 자리에 생성할 이펙트. 비어 있으면 아무 이펙트 없이 사라진다.")]
    [SerializeField] private GameObject deathEffectPrefab;
    [Tooltip("이펙트 크기 배율. 몬스터 크기에 맞게 조절한다.")]
    [SerializeField] private float deathEffectScale = 1f;

    public static event Action AnyDestroyed;
    public static event Action<Monster> Killed;

    private const string AttackHitEventName = nameof(OnAttackHit);
    private static readonly Dictionary<RuntimeAnimatorController, bool> AttackHitEventCache = new();
    private static readonly HashSet<string> WarnedStrayBodyPrefabs = new();

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly Color NormalHitFlashColor = Color.white;
    private static readonly Color CriticalHitFlashColor = Color.red;
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
    private bool attackClipHasHitEvent;
    private Material originalSharedMaterial;
    private Material hitFlashMaterial;
    private float displayedHpRatio = 1f;
    private Tween hpGaugeTween;
    private Tween hitFlashTween;
    private bool suppressNormalMove;
    private bool isMovementStopped;
    private Coroutine temporaryStopRoutine;
    private float animatorSpeedBeforeStop = 1f;
    private Coroutine temporarySlowRoutine;
    private float moveSpeedMultiplier = 1f;
    private float animatorSpeedBeforeSlow = 1f;
    private readonly Dictionary<MonoBehaviour, int> reservations = new();

    public string DataId => dataId;
    public int Hp => hp;
    public int AttackPower => attackPower;
    public int DropGold => dropGold;
    public int DropExp => dropExp;
    public int ReservedDamage => reservedDamage;
    public int ExpectedRemainingHp => Mathf.Max(0, hp - reservedDamage);
    public bool CanBeTargeted =>
        !isDead && hp > 0 && ExpectedRemainingHp > 0 && HasCrossedMinLine;
    public bool IsDead => isDead;
    public Collider2D HitBox => hitBox;

    /// <summary>투사체가 조준할 위치. 스프라이트/히트박스가 루트에서 떨어져 있어도 피격 판정 중심을 노린다.</summary>
    public Vector3 AimPosition
    {
        get
        {
            if (hitBox != null && hitBox.enabled)
            {
                Vector3 center = hitBox.bounds.center;
                center.z = transform.position.z;
                return center;
            }

            return transform.position;
        }
    }

    /// <summary>
    /// 투사체가 닿은 콜라이더로부터 몬스터를 찾는다. 하위 오브젝트(HitBox) 콜라이더도 인정하고,
    /// 방벽 감지용 Detector 콜라이더는 피격으로 치지 않는다.
    /// </summary>
    public static Monster FromHitCollider(Collider2D other)
    {
        if (other == null) return null;

        Monster monster = other.GetComponentInParent<Monster>();
        if (monster == null) return null;
        if (other == monster.barrierDetector) return null;
        if (monster.hitBox != null && other != monster.hitBox) return null;
        return monster;
    }

    /// <summary>FieldLayout/MinLine 아래로 내려온 뒤에만 공격 대상이 된다.</summary>
    public bool HasCrossedMinLine =>
        !TryGetMinLineWorldY(out float minLineY) || transform.position.y <= minLineY;

    private void Awake()
    {
        ApplyGameData();
        hp = Mathf.Max(1, maxHp);
        ResolveVisuals();
        ResolveColliders();
        NormalizePhysics();
        EnsureAnimationEventRelay();
        attackClipHasHitEvent = HasAttackHitEvent(animator);
        ResolveHpGaugeImage();

        if (transform.position.x > 0f)
        {
            Vector3 scale = transform.localScale;
            scale.x *= -1f;
            transform.localScale = scale;
        }

        if (castShadow && bodyRenderer != null)
            SpriteShadow.Attach(bodyRenderer.gameObject, shadowOffset, shadowColor, shadowScale);

        SyncHpGaugeFillDirection();
        RefreshHpGauge(animate: false);
    }

    // 프리팹 구조: 루트(Monster, Rigidbody2D) / Animator(SpriteRenderer, Animator) / HitBox, Detector.
    // 예전처럼 루트에 SpriteRenderer/Animator가 붙은 구조도 그대로 동작한다.
    private void ResolveVisuals()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        bodyRenderer = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (bodyRenderer != null)
            originalSharedMaterial = bodyRenderer.sharedMaterial;
    }

    private void ResolveColliders()
    {
        if (barrierDetector == null)
        {
            Transform detector = FindChildRecursive(transform, "Detector");
            if (detector != null)
                barrierDetector = detector.GetComponent<Collider2D>();
        }

        if (hitBox == null)
        {
            Transform hitBoxTransform = FindChildRecursive(transform, "HitBox");
            if (hitBoxTransform != null)
                hitBox = hitBoxTransform.GetComponent<Collider2D>();
        }

        if (hitBox == null)
            hitBox = GetComponent<Collider2D>();

        if (muzzle == null)
            muzzle = FindChildRecursive(transform, "Muzzle");
    }

    private void NormalizePhysics()
    {
        // 하위 콜라이더는 루트 Rigidbody2D에 붙어야 루트의 OnTriggerEnter2D가 호출된다.
        // 하위에 별도 Rigidbody2D(특히 Dynamic)가 있으면 중력으로 떨어지고 이벤트도 끊기므로 제거한다.
        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D body = bodies[i];
            if (body == null || body.gameObject == gameObject) continue;

            if (WarnedStrayBodyPrefabs.Add(GetTypeKey()))
            {
                Debug.LogWarning(
                    $"[Monster] '{GetTypeKey()}' prefab has an extra Rigidbody2D on '{body.name}'. " +
                    "It is removed at runtime; please remove it from the prefab.", this);
            }

            Destroy(body);
        }

        // 투사체·방벽 모두 트리거 이벤트로 판정하므로 몬스터 콜라이더는 트리거여야 한다.
        if (barrierDetector != null)
            barrierDetector.isTrigger = true;
        if (hitBox != null)
            hitBox.isTrigger = true;
    }

    private void EnsureAnimationEventRelay()
    {
        // 애니메이션 이벤트는 Animator가 붙은 오브젝트에만 전달되므로, 하위 Animator면 루트로 중계한다.
        if (animator == null || animator.gameObject == gameObject) return;
        if (animator.GetComponent<MonsterAnimationEventRelay>() == null)
            animator.gameObject.AddComponent<MonsterAnimationEventRelay>();
    }

    private static bool HasAttackHitEvent(Animator targetAnimator)
    {
        RuntimeAnimatorController controller = targetAnimator != null ? targetAnimator.runtimeAnimatorController : null;
        if (controller == null) return false;
        if (AttackHitEventCache.TryGetValue(controller, out bool cached)) return cached;

        bool found = false;
        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length && !found; i++)
        {
            if (clips[i] == null) continue;
            AnimationEvent[] events = clips[i].events;
            for (int e = 0; e < events.Length; e++)
            {
                if (events[e].functionName == AttackHitEventName)
                {
                    found = true;
                    break;
                }
            }
        }

        AttackHitEventCache[controller] = found;
        return found;
    }

    private void OnDestroy()
    {
        KillHpGaugeTween();
        KillHitFlashTween(restoreMaterial: false);
        if (temporaryStopRoutine != null)
        {
            StopCoroutine(temporaryStopRoutine);
            temporaryStopRoutine = null;
        }

        if (temporarySlowRoutine != null)
        {
            StopCoroutine(temporarySlowRoutine);
            temporarySlowRoutine = null;
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

        if (rangedAttack && IsInRangedAttackPosition())
        {
            BeginRangedAttack();
            return;
        }

        transform.Translate(Vector3.down * moveSpeed * moveSpeedMultiplier * Time.deltaTime);
    }

    public void ApplyTemporaryStop(float duration)
    {
        if (isDead || duration <= 0f) return;

        if (temporaryStopRoutine != null)
            StopCoroutine(temporaryStopRoutine);

        temporaryStopRoutine = StartCoroutine(TemporaryStopRoutine(duration));
    }

    public void ApplyTemporarySlow(float speedMultiplier, float duration)
    {
        if (isDead || duration <= 0f) return;

        if (temporarySlowRoutine != null)
            StopCoroutine(temporarySlowRoutine);

        temporarySlowRoutine = StartCoroutine(TemporarySlowRoutine(speedMultiplier, duration));
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

    private IEnumerator TemporarySlowRoutine(float speedMultiplier, float duration)
    {
        moveSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0f, 1f);

        if (animator != null)
        {
            animatorSpeedBeforeSlow = animator.speed > 0f ? animator.speed : 1f;
            if (!isMovementStopped)
                animator.speed = animatorSpeedBeforeSlow * moveSpeedMultiplier;
        }

        yield return new WaitForSeconds(duration);

        moveSpeedMultiplier = 1f;
        if (animator != null && !isMovementStopped)
            animator.speed = animatorSpeedBeforeSlow;

        temporarySlowRoutine = null;
    }

    public void ApplyConstantWindPull(Vector3 origin, float speed)
    {
        if (isDead || isMovementStopped || speed <= 0f || !HasCrossedMinLine)
            return;

        suppressNormalMove = true;

        Vector3 toOrigin = origin - transform.position;
        float distance = toOrigin.magnitude;
        if (distance > 0.0001f)
        {
            float step = speed * Time.deltaTime;
            Vector3 nextPosition = transform.position + toOrigin / distance * Mathf.Min(step, distance);
            nextPosition = ClampAgainstBarrier(nextPosition);
            transform.position = nextPosition;
        }

        ValidateBarrierAttackRange();
    }

    private static Barrier cachedBarrier;
    private static float cachedBarrierTopY;
    private static int cachedBarrierFrame = -1;

    private static RectTransform cachedMinLine;
    private static Camera cachedWorldCamera;
    private static float cachedMinLineWorldY;
    private static int cachedMinLineFrame = -1;
    private static bool cachedMinLineFound;

    private Vector3 ClampAgainstBarrier(Vector3 position)
    {
        if (!TryGetBarrierTopY(out float barrierTopY))
            return position;

        // Monsters move downward (-Y). Do not allow wind pull to drag them past the barrier line.
        if (position.y < barrierTopY)
            position.y = barrierTopY;

        return position;
    }

    private static bool TryGetMinLineWorldY(out float minLineWorldY)
    {
        if (cachedMinLineFrame != Time.frameCount)
        {
            cachedMinLineFrame = Time.frameCount;
            cachedMinLineFound = false;
            cachedMinLineWorldY = 0f;

            if (cachedMinLine == null)
            {
                GameObject minLineObject = GameObject.Find("MinLine");
                if (minLineObject != null)
                    cachedMinLine = minLineObject.GetComponent<RectTransform>();
            }

            if (cachedWorldCamera == null)
                cachedWorldCamera = Camera.main;

            if (cachedMinLine != null && cachedWorldCamera != null)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, cachedMinLine.position);
                float depth = Mathf.Abs(cachedWorldCamera.transform.position.z);
                Vector3 worldPoint = cachedWorldCamera.ScreenToWorldPoint(
                    new Vector3(screenPoint.x, screenPoint.y, depth));
                cachedMinLineWorldY = worldPoint.y;
                cachedMinLineFound = true;
            }
        }

        minLineWorldY = cachedMinLineWorldY;
        return cachedMinLineFound;
    }

    private bool TryGetBarrierTopY(out float barrierTopY)
    {
        if (attackTargetBarrier != null)
        {
            Collider2D attackBarrierCollider = attackTargetBarrier.GetComponent<Collider2D>();
            if (attackBarrierCollider != null)
            {
                barrierTopY = attackBarrierCollider.bounds.max.y;
                return true;
            }
        }

        if (cachedBarrierFrame != Time.frameCount)
        {
            cachedBarrierFrame = Time.frameCount;
            if (cachedBarrier == null)
                cachedBarrier = FindAnyObjectByType<Barrier>();

            if (cachedBarrier != null)
            {
                Collider2D barrierCollider = cachedBarrier.GetComponent<Collider2D>();
                cachedBarrierTopY = barrierCollider != null ? barrierCollider.bounds.max.y : float.NegativeInfinity;
            }
            else
            {
                cachedBarrierTopY = float.NegativeInfinity;
            }
        }

        if (cachedBarrier == null || float.IsNegativeInfinity(cachedBarrierTopY))
        {
            barrierTopY = 0f;
            return false;
        }

        barrierTopY = cachedBarrierTopY;
        return true;
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

    public bool TryApplyReservedHit(MonoBehaviour source, bool isCritical = false)
    {
        if (!TryConsumeReservation(source, out int amount)) return false;
        TakeDamage(amount, isCritical);
        return true;
    }

    public void ReleaseReservation(MonoBehaviour source)
    {
        TryConsumeReservation(source, out _);
    }

    public void ApplyDamage(int amount, bool isCritical = false)
    {
        TakeDamage(amount, isCritical);
    }

    private bool TryConsumeReservation(MonoBehaviour source, out int amount)
    {
        amount = 0;
        if (source == null) return false;
        if (!reservations.Remove(source, out amount)) return false;

        reservedDamage = Mathf.Max(0, reservedDamage - amount);
        return true;
    }

    private void TakeDamage(int amount, bool isCritical = false)
    {
        if (isDead || amount <= 0 || hp <= 0) return;

        hp = Mathf.Max(0, hp - amount);
        RefreshHpGauge(animate: true);

        bool lethal = hp <= 0;
        if (lethal)
        {
            BeginDeath(killed: true);
            FinalizeDeath();
            return;
        }

        PlayHitFlash(isCritical);
    }

    private void PlayHitFlash(bool isCritical = false, Action onComplete = null)
    {
        if (bodyRenderer == null || hitFlashCount <= 0 || !EnsureHitFlashMaterial())
        {
            onComplete?.Invoke();
            return;
        }

        KillHitFlashTween(restoreMaterial: false);
        bodyRenderer.sharedMaterial = hitFlashMaterial;
        SetFlashColor(isCritical ? CriticalHitFlashColor : NormalHitFlashColor);
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
        hitFlashMaterial.SetColor(FlashColorId, NormalHitFlashColor);
        return true;
    }

    private void SetFlashAmount(float amount)
    {
        if (hitFlashMaterial == null) return;
        hitFlashMaterial.SetFloat(FlashAmountId, amount);
    }

    private void SetFlashColor(Color color)
    {
        if (hitFlashMaterial == null) return;
        hitFlashMaterial.SetColor(FlashColorId, color);
    }

    private void RestoreOriginalMaterial()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.sharedMaterial = originalSharedMaterial;
        SetFlashAmount(0f);
        SetFlashColor(NormalHitFlashColor);
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
        SpawnDeathEffect();
        Destroy(gameObject);
    }

    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null) return;

        // 콜라이더는 이미 꺼졌으므로 스프라이트 중심에 생성한다.
        Vector3 position = bodyRenderer != null ? bodyRenderer.bounds.center : transform.position;
        position.z = transform.position.z;

        GameObject effect = Instantiate(deathEffectPrefab, position, Quaternion.identity);
        effect.transform.localScale *= deathEffectScale;
    }

    private void Die(bool killed)
    {
        BeginDeath(killed);
        FinalizeDeath();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 원거리 몬스터는 접촉 공격/자폭을 하지 않는다.
        if (rangedAttack) return;

        Barrier barrier = other.GetComponent<Barrier>();
        if (barrier == null) return;

        if (barrierDetector != null)
        {
            if (!barrierDetector.IsTouching(other)) return;

            if (selfDestructOnContact)
                SelfDestructOnBarrier(barrier);
            else
                BeginBarrierAttack(barrier);
            return;
        }

        // Detector가 없는 예전 구조의 프리팹은 항상 자폭한다.
        SelfDestructOnBarrier(barrier);
    }

    private void SelfDestructOnBarrier(Barrier barrier)
    {
        if (isDead || isAttacking) return;

        barrier.ApplyContactHit(attackPower);
        BeginContactAttackAndDestroy();
    }

    // Bat_Attack Animation Event
    public void OnAttackHit()
    {
        if (isDead) return;

        if (rangedAttack)
        {
            FireRangedProjectile();
            return;
        }

        if (attackTargetBarrier == null) return;
        if (!IsInBarrierAttackRange()) return;
        attackTargetBarrier.ApplyContactHit(attackPower);
    }

    private const float RangedBarrierStopMargin = 0.1f;
    private static PlayerShooter cachedPlayerShooter;

    private bool IsInRangedAttackPosition()
    {
        bool hasBarrier = TryGetBarrierTopY(out float barrierTopY);
        float bottomY = hitBox != null && hitBox.enabled
            ? hitBox.bounds.min.y
            : transform.position.y;

        // 사거리가 짧아도 방벽을 뚫고 지나가지 않도록, 방벽에 닿기 직전에는 무조건 멈춘다.
        if (hasBarrier && bottomY - barrierTopY <= RangedBarrierStopMargin)
            return true;

        if (stopAtPlayerMaxRange)
        {
            if (cachedPlayerShooter == null)
                cachedPlayerShooter = FindAnyObjectByType<PlayerShooter>();

            float maxRange = cachedPlayerShooter != null ? cachedPlayerShooter.MaxWeaponRange : 0f;
            if (maxRange > 0f)
            {
                // 플레이어는 MinLine을 넘은 몬스터만 조준하므로, 그 전에는 멈추지 않는다.
                if (!HasCrossedMinLine) return false;

                // PlayerShooter의 사거리 판정과 같은 기준(플레이어 ↔ 몬스터 루트 거리)을 쓴다.
                return Vector2.Distance(cachedPlayerShooter.transform.position, transform.position) <= maxRange;
            }
        }

        return hasBarrier && bottomY - barrierTopY <= rangedAttackRange;
    }

    private void BeginRangedAttack()
    {
        if (isDead || isAttacking) return;

        isAttacking = true;
        if (barrierAttackRoutine != null)
            StopCoroutine(barrierAttackRoutine);
        barrierAttackRoutine = StartCoroutine(RangedAttackLoop());
    }

    private IEnumerator RangedAttackLoop()
    {
        while (!isDead && isAttacking)
        {
            if (animator != null)
                animator.Play(AttackHash, 0, 0f);
            yield return WaitForAnimation(AttackHash);

            if (isDead || !isAttacking) yield break;

            // 공격 클립에 OnAttackHit 이벤트가 없으면 공격 모션이 끝나는 시점에 발사한다.
            if (!attackClipHasHitEvent)
                FireRangedProjectile();

            if (animator != null)
                animator.Play(MoveHash, 0, 0f);
            yield return new WaitForSeconds(rangedAttackInterval);
        }
    }

    private void FireRangedProjectile()
    {
        if (isDead || projectilePrefab == null) return;

        Barrier barrier = cachedBarrier != null ? cachedBarrier : FindAnyObjectByType<Barrier>();
        if (barrier == null) return;

        Vector3 origin = muzzle != null ? muzzle.position : AimPosition;
        MonsterProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        projectile.Launch(barrier, attackPower);
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

        if (hitBox != null)
            return hitBox.IsTouching(barrierCollider);

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

            // 공격 클립에 OnAttackHit 이벤트가 없으면 공격 모션이 끝나는 시점에 타격한다.
            if (!attackClipHasHitEvent)
                attackTargetBarrier.ApplyContactHit(attackPower);

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

    /// <summary>
    /// StageSpawn 시트의 스테이지별 스탯으로 덮어쓴다. 음수 값은 "지정 안 함"이라 기본값을 유지한다.
    /// </summary>
    public void ApplyStageOverride(int stageHp, int stageAtk, int stageGold, int stageExp)
    {
        if (stageHp > 0)
        {
            maxHp = stageHp;
            hp = stageHp;
            RefreshHpGauge(animate: false);
        }

        if (stageAtk > 0) attackPower = stageAtk;
        if (stageGold >= 0) dropGold = stageGold;
        if (stageExp >= 0) dropExp = stageExp;
    }

    /// <summary>
    /// 표시 스테이지 N 기준으로 이동 속도를 N% 증가시킨다. (100단계 = 2배)
    /// </summary>
    public void ApplyStageMoveSpeedBonus(int stageNumber)
    {
        if (stageNumber <= 0)
            return;

        moveSpeed *= 1f + stageNumber / 100f;
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
