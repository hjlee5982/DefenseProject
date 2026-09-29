using System;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private Monster[] monsterPrefabs;
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private float spawnXRange = 1.5f;
    [SerializeField] private int maxSpawnCount = 10;

    private float spawnTimer;
    private int spawnedCount;

    public bool HasFinishedSpawning => spawnedCount >= maxSpawnCount;

    public event Action MonsterSpawned;

    private void OnEnable()
    {
        spawnTimer = 0f;
        spawnedCount = 0;
    }

    private void Update()
    {
        if (spawnedCount >= maxSpawnCount) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer < spawnInterval) return;

        spawnTimer = 0f;
        Spawn();
    }

    private void Spawn()
    {
        Monster prefab = PickMonsterPrefab();
        if (prefab == null) return;

        Vector3 position = transform.position;
        position.x += SampleSpawnXOffset();
        Instantiate(prefab, position, Quaternion.identity);
        spawnedCount++;
        MonsterSpawned?.Invoke();
    }

    private Monster PickMonsterPrefab()
    {
        if (monsterPrefabs == null || monsterPrefabs.Length == 0) return null;

        int validCount = 0;
        for (int i = 0; i < monsterPrefabs.Length; i++)
        {
            if (monsterPrefabs[i] != null)
                validCount++;
        }

        if (validCount == 0) return null;

        int pick = UnityEngine.Random.Range(0, validCount);
        for (int i = 0; i < monsterPrefabs.Length; i++)
        {
            if (monsterPrefabs[i] == null) continue;
            if (pick == 0) return monsterPrefabs[i];
            pick--;
        }

        return null;
    }

    private float SampleSpawnXOffset()
    {
        const float deadZone = 0.0001f;
        float spawnerX = transform.position.x;

        for (int i = 0; i < 16; i++)
        {
            float offset = UnityEngine.Random.Range(-spawnXRange, spawnXRange);
            if (Mathf.Abs(spawnerX + offset) > deadZone)
                return offset;
        }

        float fallback = Mathf.Max(spawnXRange * 0.25f, 0.1f);
        return spawnerX >= 0f ? -fallback : fallback;
    }
}
