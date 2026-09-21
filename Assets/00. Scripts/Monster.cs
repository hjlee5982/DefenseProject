using System;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int maxHp = 1;

    public static event Action AnyDestroyed;
    public static event Action Killed;

    private int hp;
    private int reservedDamage;
    private bool isDead;
    private bool wasKilled;
    private readonly Dictionary<MonoBehaviour, int> reservations = new();

    public int Hp => hp;
    public int ReservedDamage => reservedDamage;
    public int ExpectedRemainingHp => Mathf.Max(0, hp - reservedDamage);
    public bool CanBeTargeted => !isDead && hp > 0 && ExpectedRemainingHp > 0;

    private void Awake()
    {
        hp = Mathf.Max(1, maxHp);
    }

    private void Update()
    {
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

        barrier.ApplyContactHit();
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
