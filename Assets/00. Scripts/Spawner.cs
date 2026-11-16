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

    [Header("Burst Spawn (스테이지 모드)")]
    [Tooltip("한 번에 동시에 생성하는 최소 마릿수")]
    [SerializeField] private int minBurstCount = 1;
    [Tooltip("한 번에 동시에 생성하는 최대 마릿수")]
    [SerializeField] private int maxBurstCount = 5;
    [Tooltip("켜면 다음 생성 대기 시간을 묶음 마릿수만큼 늘려 초당 생성 수를 기존과 같게 유지한다. 끄면 묶음마다 기본 간격만 기다린다(더 빡빡해짐).")]
    [SerializeField] private bool scaleIntervalByBurstSize = true;
    [Tooltip("같은 묶음 안에서 몬스터끼리 유지하려는 최소 X 간격")]
    [SerializeField] private float burstMinSpacing = 0.45f;

    private float spawnTimer;
    private int spawnedCount;
    private int plannedSpawnCount;
    private float activeSpawnInterval;
    private int pendingStageOrder;
    private int lastBurstSize = 1;
    private readonly Queue<StageSpawnEntry> spawnQueue = new();

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
        lastBurstSize = 1;

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

        // 옵션이 켜져 있으면 묶음 크기만큼 대기 시간을 늘려, 전체 생성 속도(마리/초)를 기존과 같게 유지한다.
        spawnTimer += Time.deltaTime;
        float wait = scaleIntervalByBurstSize ? activeSpawnInterval * lastBurstSize : activeSpawnInterval;
        if (spawnTimer < wait) return;

        spawnTimer = 0f;

        int minBurst = Mathf.Max(1, minBurstCount);
        int maxBurst = Mathf.Max(minBurst, maxBurstCount);
        int burst = Mathf.Min(UnityEngine.Random.Range(minBurst, maxBurst + 1), spawnQueue.Count);
        lastBurstSize = Mathf.Max(1, burst);

        var usedOffsets = new List<float>(burst);
        for (int i = 0; i < burst; i++)
        {
            StageSpawnEntry entry = spawnQueue.Dequeue();
            Monster prefab = ResolveMonsterPrefab(entry.MonsterId);
            if (prefab == null)
            {
                plannedSpawnCount = Mathf.Max(0, plannedSpawnCount - 1);
                continue;
            }

            SpawnPrefab(prefab, entry, usedOffsets);
        }
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
                spawnQueue.Enqueue(entry);
        }

        plannedSpawnCount = spawnQueue.Count;
    }

    private void SpawnPrefab(Monster prefab, StageSpawnEntry entry = null, List<float> usedOffsets = null)
    {
        if (prefab == null) return;

        Vector3 position = transform.position;
        float offset = SampleSpawnXOffset(usedOffsets);
        usedOffsets?.Add(offset);
        position.x += offset;
        Monster spawned = Instantiate(prefab, position, Quaternion.identity);
        if (entry != null)
            spawned.ApplyStageOverride(entry.Hp, entry.Atk, entry.Gold, entry.Exp);

        // pendingStageOrder 0 = 1스테이지 표시. 스테이지%만큼 이동 속도 증가(100 = 2배).
        spawned.ApplyStageMoveSpeedBonus(pendingStageOrder + 1);
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

    private float SampleSpawnXOffset(List<float> usedOffsets = null)
    {
        const float deadZone = 0.0001f;
        float spawnerX = transform.position.x;
        float lastValid = float.NaN;

        // 같은 묶음의 몬스터와 겹치지 않도록 간격을 확보하며 뽑는다(여의치 않으면 마지막 후보 사용).
        for (int i = 0; i < 24; i++)
        {
            float offset = UnityEngine.Random.Range(-spawnXRange, spawnXRange);
            if (Mathf.Abs(spawnerX + offset) <= deadZone)
                continue;

            lastValid = offset;
            if (IsSpacedFrom(offset, usedOffsets))
                return offset;
        }

        if (!float.IsNaN(lastValid))
            return lastValid;

        float fallback = Mathf.Max(spawnXRange * 0.25f, 0.1f);
        return spawnerX >= 0f ? -fallback : fallback;
    }

    private bool IsSpacedFrom(float offset, List<float> usedOffsets)
    {
        if (usedOffsets == null) return true;

        for (int i = 0; i < usedOffsets.Count; i++)
        {
            if (Mathf.Abs(usedOffsets[i] - offset) < burstMinSpacing)
                return false;
        }

        return true;
    }
}
