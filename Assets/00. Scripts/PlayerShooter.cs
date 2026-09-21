using System.Collections.Generic;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    private struct EquippedWeapon
    {
        public int equipSlotIndex;
        public float fireInterval;
        public float range;
        public int damage;
        public Projectile projectilePrefab;
        public InstantAttack instantAttackPrefab;
    }

    [SerializeField] private EquipSlotView equipSlotView;
    [SerializeField] private Transform firePoint;

    private EquippedWeapon[] weapons;
    private float[] fireTimers;
    private Animator animator;
    private bool isPlayingAttack;

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackInstantHash = Animator.StringToHash("Attack_Instant");
    private static readonly int IdleHash = Animator.StringToHash("Idle");

    private Vector3 FireOrigin => firePoint != null ? firePoint.position : transform.position;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (firePoint == null)
            firePoint = transform.Find("FirePoint");
    }

    public void SetEquippedProjectiles(IReadOnlyList<Item> items)
    {
        List<EquippedWeapon> equippedWeapons = new();
        int equipSlotIndex = 0;

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || !item.IsWeapon) continue;

            if (item.IsProjectileWeapon)
            {
                Projectile projectile = item.ProjectilePrefab;
                if (projectile == null) continue;

                equippedWeapons.Add(new EquippedWeapon
                {
                    equipSlotIndex = equipSlotIndex,
                    fireInterval = item.FireInterval,
                    range = item.Range,
                    damage = item.Damage,
                    projectilePrefab = projectile
                });
                equipSlotIndex++;
            }
            else if (item.IsInstantWeapon)
            {
                InstantAttack instantAttack = item.InstantAttackPrefab;
                if (instantAttack == null) continue;

                equippedWeapons.Add(new EquippedWeapon
                {
                    equipSlotIndex = equipSlotIndex,
                    fireInterval = item.FireInterval,
                    range = item.Range,
                    damage = item.Damage,
                    instantAttackPrefab = instantAttack
                });
                equipSlotIndex++;
            }
        }

        weapons = equippedWeapons.ToArray();
        fireTimers = new float[weapons.Length];
    }

    private void Update()
    {
        bool firedThisFrame = false;

        if (weapons != null)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                EquippedWeapon weapon = weapons[i];
                fireTimers[i] += Time.deltaTime;
                if (fireTimers[i] < weapon.fireInterval) continue;

                Monster target = FindNearestMonsterInRange(weapon.range);
                if (target == null) continue;

                if (!TryFire(weapon, target)) continue;

                PlayAttackAnimation(weapon.instantAttackPrefab != null);
                firedThisFrame = true;
                fireTimers[i] = 0f;
                if (equipSlotView != null)
                    equipSlotView.StartCooldown(weapon.equipSlotIndex, weapon.fireInterval);
            }
        }

        TryReturnToIdleAfterAttack(firedThisFrame);
    }

    private void PlayAttackAnimation(bool isInstant)
    {
        if (animator == null) return;
        isPlayingAttack = true;
        animator.Play(isInstant ? AttackInstantHash : AttackHash, 0, 0f);
    }

    private void TryReturnToIdleAfterAttack(bool firedThisFrame)
    {
        if (animator == null || !isPlayingAttack || firedThisFrame) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        bool isAttackState = info.shortNameHash == AttackHash || info.shortNameHash == AttackInstantHash;
        if (!isAttackState || info.normalizedTime < 1f) return;
        if (CanFireAnyWeapon()) return;

        isPlayingAttack = false;
        animator.Play(IdleHash, 0, 0f);
    }

    private bool CanFireAnyWeapon()
    {
        if (weapons == null) return false;

        for (int i = 0; i < weapons.Length; i++)
        {
            EquippedWeapon weapon = weapons[i];
            if (fireTimers[i] < weapon.fireInterval) continue;
            if (FindNearestMonsterInRange(weapon.range) != null)
                return true;
        }

        return false;
    }

    private bool TryFire(EquippedWeapon weapon, Monster target)
    {
        Vector3 origin = FireOrigin;

        if (weapon.projectilePrefab != null)
        {
            Projectile projectile = Instantiate(weapon.projectilePrefab, origin, Quaternion.identity);
            projectile.ApplyCombatStats(weapon.range, weapon.damage, weapon.fireInterval);
            if (projectile.Init(target))
                return true;

            Destroy(projectile.gameObject);
            return false;
        }

        if (weapon.instantAttackPrefab != null)
        {
            InstantAttack attack = Instantiate(weapon.instantAttackPrefab, origin, Quaternion.identity);
            attack.ApplyCombatStats(weapon.range, weapon.damage, weapon.fireInterval);
            if (attack.Init(target, origin))
                return true;

            Destroy(attack.gameObject);
            return false;
        }

        return false;
    }

    private Monster FindNearestMonsterInRange(float range)
    {
        Monster[] monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        Monster nearest = null;
        float nearestDistance = range;

        for (int i = 0; i < monsters.Length; i++)
        {
            Monster monster = monsters[i];
            if (monster == null || !monster.CanBeTargeted) continue;

            float distance = Vector2.Distance(transform.position, monster.transform.position);
            if (distance > nearestDistance) continue;

            nearestDistance = distance;
            nearest = monster;
        }

        return nearest;
    }
}
