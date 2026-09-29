using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int maxHp = 1;
    [SerializeField] private bool castShadow = true;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.08f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private Vector2 shadowScale = Vector2.one;
    [SerializeField] private Collider2D barrierDetector;

    public static event Action AnyDestroyed;
    public static event Action Killed;

    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private int hp;
    private int reservedDamage;
    private bool isDead;
    private bool wasKilled;
    private bool isAttacking;
    private Animator animator;
    private readonly Dictionary<MonoBehaviour, int> reservations = new();

    public int Hp => hp;
    public int ReservedDamage => reservedDamage;
    public int ExpectedRemainingHp => Mathf.Max(0, hp - reservedDamage);
    public bool CanBeTargeted => !isDead && hp > 0 && ExpectedRemainingHp > 0;

    private void Awake()
    {
        hp = Mathf.Max(1, maxHp);
        animator = GetComponent<Animator>();

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

        hp -= amount;
        if (hp <= 0) Die(killed: true);
    }

    private void Die(bool killed)
    {
        if (isDead) return;
        isDead = true;
        wasKilled = killed;
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Barrier barrier = other.GetComponent<Barrier>();
        if (barrier == null) return;

        if (barrierDetector != null)
        {
            if (barrierDetector.IsTouching(other))
                BeginBarrierAttack();
            return;
        }

        barrier.ApplyContactHit();
        BeginContactAttackAndDestroy();
    }

    private void BeginBarrierAttack()
    {
        if (isDead || isAttacking) return;

        isAttacking = true;
        if (animator != null)
            animator.Play(AttackHash, 0, 0f);
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
        yield return null;

        float timeout = 3f;
        while (timeout > 0f)
        {
            if (animator == null) break;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == AttackHash && info.normalizedTime >= 1f)
                break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        Die(killed: false);
    }

    private void OnDestroy()
    {
        CancelInboundProjectiles();
        if (wasKilled) Killed?.Invoke();
        AnyDestroyed?.Invoke();
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
