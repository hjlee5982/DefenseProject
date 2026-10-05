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

    public static event Action AnyDestroyed;
    public static event Action<Monster> Killed;

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int MoveHash = Animator.StringToHash("Move");

    private string dataId;
    private int hp;
    private int reservedDamage;
    private bool isDead;
    private bool wasKilled;
    private bool isAttacking;
    private Coroutine barrierAttackRoutine;
    private Barrier attackTargetBarrier;
    private Animator animator;
    private float displayedHpRatio = 1f;
    private Tween hpGaugeTween;
    private readonly Dictionary<MonoBehaviour, int> reservations = new();

    public string DataId => dataId;
    public int Hp => hp;
    public int AttackPower => attackPower;
    public int DropGold => dropGold;
    public int DropExp => dropExp;
    public int ReservedDamage => reservedDamage;
    public int ExpectedRemainingHp => Mathf.Max(0, hp - reservedDamage);
    public bool CanBeTargeted => !isDead && hp > 0 && ExpectedRemainingHp > 0;

    private void Awake()
    {
        ApplyGameData();
        hp = Mathf.Max(1, maxHp);
        animator = GetComponent<Animator>();
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
        CancelInboundProjectiles();
        if (wasKilled) Killed?.Invoke(this);
        AnyDestroyed?.Invoke();
    }

    private void Update()
    {
        if (isDead || isAttacking) return;

        transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
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
        if (hp <= 0) Die(killed: true);
    }

    private void Die(bool killed)
    {
        if (isDead) return;
        isDead = true;
        wasKilled = killed;
        attackTargetBarrier = null;
        if (barrierAttackRoutine != null)
        {
            StopCoroutine(barrierAttackRoutine);
            barrierAttackRoutine = null;
        }
        Destroy(gameObject);
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

    private IEnumerator BarrierAttackLoop()
    {
        while (!isDead)
        {
            animator.Play(AttackHash, 0, 0f);
            yield return WaitForAnimation(AttackHash);

            if (isDead) yield break;

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
