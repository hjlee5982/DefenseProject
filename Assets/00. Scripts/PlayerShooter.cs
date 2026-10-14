using System.Collections.Generic;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    private struct EquippedWeapon
    {
        public int equipSlotIndex;
        public int grade;
        public Item.ItemCategory category;
        public float fireInterval;
        public float range;
        public int damage;
        public float criticalChance;
        public Projectile projectilePrefab;
        public Projectile additionalProjectilePrefab;
    }

    [SerializeField] private EquipSlotView equipSlotView;
    [SerializeField] private Transform firePoint;

    private EquippedWeapon[] weapons;
    private float[] fireTimers;
    private Animator animator;
    private bool isPlayingAttack;

    private static readonly int AttackHash = Animator.StringToHash("Attack");
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
        CombatBuffUtility.Bonuses buffs = CombatBuffUtility.Collect(items);
        EnhanceBonusState enhance = GameManager.Instance != null
            ? GameManager.Instance.EnhanceBonuses
            : null;

        List<EquippedWeapon> equippedWeapons = new();
        int equipSlotIndex = 0;

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || item.IsBuff) continue;

            Projectile projectile = item.ProjectilePrefab;
            if (projectile == null) continue;

            equippedWeapons.Add(new EquippedWeapon
            {
                equipSlotIndex = equipSlotIndex,
                grade = item.Grade,
                category = item.Category,
                fireInterval = CombatStatCalculator.GetFinalFireInterval(item, buffs, enhance),
                range = CombatStatCalculator.GetFinalRange(item, buffs),
                damage = CombatStatCalculator.GetFinalDamage(item, buffs, enhance),
                criticalChance = CombatStatCalculator.GetCriticalChance(buffs),
                projectilePrefab = projectile,
                additionalProjectilePrefab = item.AdditionalProjectilePrefab
            });
            equipSlotIndex++;
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

                Monster target = FindTargetMonsterInRange(weapon);
                if (target == null) continue;

                if (!TryFire(weapon, target)) continue;

                PlayAttackAnimation();
                firedThisFrame = true;
                fireTimers[i] = 0f;
                if (equipSlotView != null)
                    equipSlotView.StartCooldown(weapon.equipSlotIndex, weapon.fireInterval);
            }
        }

        TryReturnToIdleAfterAttack(firedThisFrame);
    }

    private void PlayAttackAnimation()
    {
        if (animator == null) return;
        isPlayingAttack = true;
        animator.Play(AttackHash, 0, 0f);
    }

    private void TryReturnToIdleAfterAttack(bool firedThisFrame)
    {
        if (animator == null || !isPlayingAttack || firedThisFrame) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash != AttackHash || info.normalizedTime < 1f) return;
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
            if (FindTargetMonsterInRange(weapon) != null)
                return true;
        }

        return false;
    }

    private bool TryFire(EquippedWeapon weapon, Monster target)
    {
        if (weapon.projectilePrefab == null) return false;

        Projectile projectile = Instantiate(weapon.projectilePrefab, FireOrigin, Quaternion.identity);
        projectile.ApplyCombatStats(weapon.range, weapon.damage, weapon.fireInterval);
        projectile.ApplyCriticalChance(weapon.criticalChance);
        projectile.ConfigureAdditionalOnHit(weapon.additionalProjectilePrefab, weapon.grade, weapon.category);

        if (projectile.Init(target))
            return true;

        Destroy(projectile.gameObject);
        return false;
    }

    private Monster FindTargetMonsterInRange(EquippedWeapon weapon)
    {
        return weapon.category == Item.ItemCategory.Wind
            ? FindFarthestMonsterInRange(weapon.range)
            : FindNearestMonsterInRange(weapon.range);
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

    private Monster FindFarthestMonsterInRange(float range)
    {
        Monster[] monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        Monster farthest = null;
        float farthestDistance = -1f;

        for (int i = 0; i < monsters.Length; i++)
        {
            Monster monster = monsters[i];
            if (monster == null || !monster.CanBeTargeted) continue;

            float distance = Vector2.Distance(transform.position, monster.transform.position);
            if (distance > range || distance <= farthestDistance) continue;

            farthestDistance = distance;
            farthest = monster;
        }

        return farthest;
    }
}
