using System;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int maxHp = 1;

    public static event Action AnyDestroyed;

    private int hp;
    private int reservedDamage;
    private bool isDead;
    private readonly Dictionary<Projectile, int> reservations = new();

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

    public bool TryReserve(Projectile projectile, int amount)
    {
        if (isDead || projectile == null || amount <= 0) return false;
        if (hp <= 0 || ExpectedRemainingHp <= 0) return false;
        if (reservations.ContainsKey(projectile)) return false;

        reservations.Add(projectile, amount);
        reservedDamage += amount;
        return true;
    }

    public bool TryApplyReservedHit(Projectile projectile)
    {
        if (!TryConsumeReservation(projectile, out int amount)) return false;
        TakeDamage(amount);
        return true;
    }

    public void ReleaseReservation(Projectile projectile)
    {
        TryConsumeReservation(projectile, out _);
    }

    private bool TryConsumeReservation(Projectile projectile, out int amount)
    {
        amount = 0;
        if (projectile == null) return false;
        if (!reservations.Remove(projectile, out amount)) return false;

        reservedDamage = Mathf.Max(0, reservedDamage - amount);
        return true;
    }

    private void TakeDamage(int amount)
    {
        if (isDead || amount <= 0 || hp <= 0) return;

        hp -= amount;
        if (hp <= 0) Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Shield>() == null) return;
        Die();
    }

    private void OnDestroy()
    {
        CancelInboundProjectiles();
        AnyDestroyed?.Invoke();
    }

    private void CancelInboundProjectiles()
    {
        if (reservations.Count == 0) return;

        Projectile[] pending = new Projectile[reservations.Count];
        reservations.Keys.CopyTo(pending, 0);
        reservations.Clear();
        reservedDamage = 0;

        for (int i = 0; i < pending.Length; i++)
        {
            if (pending[i] != null)
                pending[i].OnTargetLost();
        }
    }
}
