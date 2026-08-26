using System;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private Monster monsterPrefab;
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
        Vector3 position = transform.position;
        position.x += UnityEngine.Random.Range(-spawnXRange, spawnXRange);
        Instantiate(monsterPrefab, position, Quaternion.identity);
        spawnedCount++;
        MonsterSpawned?.Invoke();
    }
}
