using System;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private bool debugMode = true;
    [SerializeField] private Monster[] monsterPrefabs;
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private float spawnXRange = 1.5f;
    [SerializeField] private int maxSpawnCount = 10;

    private float spawnTimer;
    private int spawnedCount;
    private int plannedSpawnCount;
    private float activeSpawnInterval;
    private int pendingStageOrder;
    private readonly Queue<string> spawnQueue = new();

    public bool HasFinishedSpawning =>
        debugMode ? spawnedCount >= maxSpawnCount : spawnedCount >= plannedSpawnCount;

    public event Action MonsterSpawned;

    public void SetStageOrder(int stageOrder)
    {
        pendingStageOrder = Mathf.Max(0, stageOrder);
    }

    private void OnEnable()
    {
        spawnTimer = 0f;
        spawnedCount = 0;
        plannedSpawnCount = 0;
        spawnQueue.Clear();
        activeSpawnInterval = spawnInterval;

        if (!debugMode)
            BuildStageSpawnQueue(pendingStageOrder);
    }

    private void Update()
    {
        if (debugMode)
        {
            UpdateDebugSpawn();
            return;
        }

        UpdateStageSpawn();
    }

    private void UpdateDebugSpawn()
    {
        if (spawnedCount >= maxSpawnCount) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer < spawnInterval) return;

        spawnTimer = 0f;
        SpawnPrefab(PickRandomMonsterPrefab());
    }

    private void UpdateStageSpawn()
    {
        if (spawnQueue.Count == 0) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer < activeSpawnInterval) return;

        spawnTimer = 0f;
        string monsterId = spawnQueue.Dequeue();
        Monster prefab = ResolveMonsterPrefab(monsterId);
        if (prefab == null)
        {
            plannedSpawnCount = Mathf.Max(0, plannedSpawnCount - 1);
            return;
        }

        SpawnPrefab(prefab);
    }

    private void BuildStageSpawnQueue(int stageOrder)
    {
        spawnQueue.Clear();
        plannedSpawnCount = 0;
        activeSpawnInterval = spawnInterval;

        if (!GameDataRepository.TryGetStageByOrder(stageOrder, out StageData stage))
        {
            Debug.LogWarning($"[Spawner] Stage order {stageOrder} not found in GameData.");
            return;
        }

        if (stage.SpawnInterval > 0f)
            activeSpawnInterval = stage.SpawnInterval;

        List<StageSpawnEntry> entries = GameDataRepository.GetStageSpawnEntries(stage.Id);
        for (int i = 0; i < entries.Count; i++)
        {
            StageSpawnEntry entry = entries[i];
            for (int count = 0; count < entry.Count; count++)
                spawnQueue.Enqueue(entry.MonsterId);
        }

        plannedSpawnCount = spawnQueue.Count;
    }

    private void SpawnPrefab(Monster prefab)
    {
        if (prefab == null) return;

        Vector3 position = transform.position;
        position.x += SampleSpawnXOffset();
        Instantiate(prefab, position, Quaternion.identity);
        spawnedCount++;
        MonsterSpawned?.Invoke();
    }

    private Monster ResolveMonsterPrefab(string monsterId)
    {
        if (string.IsNullOrWhiteSpace(monsterId)) return null;

        string prefabKey = monsterId;
        if (GameDataRepository.TryGetMonsterById(monsterId, out MonsterData monsterData) &&
            !string.IsNullOrWhiteSpace(monsterData.PrefabKey))
        {
            prefabKey = monsterData.PrefabKey;
        }

        if (monsterPrefabs == null) return null;

        for (int i = 0; i < monsterPrefabs.Length; i++)
        {
            Monster prefab = monsterPrefabs[i];
            if (prefab == null) continue;
            if (string.Equals(prefab.name, prefabKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prefab.name, monsterId, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        Debug.LogWarning($"[Spawner] Prefab not found for monster '{monsterId}' (key '{prefabKey}').");
        return null;
    }

    private Monster PickRandomMonsterPrefab()
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
