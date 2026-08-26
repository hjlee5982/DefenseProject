using System.Collections.Generic;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Projectile[] projectilePrefabs;

    private float[] fireTimers;

    private void Awake()
    {
        RebuildTimers();
    }

    public void SetEquippedProjectiles(IReadOnlyList<Item> items)
    {
        List<Projectile> projectiles = new();
        for (int i = 0; i < items.Count; i++)
        {
            Projectile projectile = items[i].ProjectilePrefab;
            if (projectile != null) projectiles.Add(projectile);
        }

        projectilePrefabs = projectiles.ToArray();
        RebuildTimers();
    }

    private void Update()
    {
        for (int i = 0; i < projectilePrefabs.Length; i++)
        {
            Projectile prefab = projectilePrefabs[i];
            if (prefab == null) continue;

            fireTimers[i] += Time.deltaTime;
            if (fireTimers[i] < prefab.FireInterval) continue;

            Monster target = FindNearestMonsterInRange(prefab.Range);
            if (target == null) continue;

            Projectile projectile = Instantiate(prefab, transform.position, Quaternion.identity);
            if (!projectile.Init(target))
            {
                Destroy(projectile.gameObject);
                continue;
            }

            fireTimers[i] = 0f;
        }
    }

    private void RebuildTimers()
    {
        fireTimers = new float[projectilePrefabs != null ? projectilePrefabs.Length : 0];
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
