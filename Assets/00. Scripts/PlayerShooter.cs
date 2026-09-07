using System.Collections.Generic;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    private struct EquippedWeapon
    {
        public int equipSlotIndex;
        public float fireInterval;
        public float range;
        public Projectile projectilePrefab;
        public InstantAttack instantAttackPrefab;
    }

    [SerializeField] private EquipSlotView equipSlotView;

    private EquippedWeapon[] weapons;
    private float[] fireTimers;

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
                    fireInterval = projectile.FireInterval,
                    range = projectile.Range,
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
                    fireInterval = instantAttack.FireInterval,
                    range = instantAttack.Range,
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
        if (weapons == null) return;

        for (int i = 0; i < weapons.Length; i++)
        {
            EquippedWeapon weapon = weapons[i];
            fireTimers[i] += Time.deltaTime;
            if (fireTimers[i] < weapon.fireInterval) continue;

            Monster target = FindNearestMonsterInRange(weapon.range);
            if (target == null) continue;

            if (!TryFire(weapon, target)) continue;

            fireTimers[i] = 0f;
            if (equipSlotView != null)
                equipSlotView.StartCooldown(weapon.equipSlotIndex, weapon.fireInterval);
        }
    }

    private bool TryFire(EquippedWeapon weapon, Monster target)
    {
        if (weapon.projectilePrefab != null)
        {
            Projectile projectile = Instantiate(weapon.projectilePrefab, transform.position, Quaternion.identity);
            if (projectile.Init(target))
                return true;

            Destroy(projectile.gameObject);
            return false;
        }

        if (weapon.instantAttackPrefab != null)
        {
            InstantAttack attack = Instantiate(weapon.instantAttackPrefab, transform.position, Quaternion.identity);
            if (attack.Init(target, transform.position))
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
