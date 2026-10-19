#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 밸런스 튜닝용 에디터 전용 테스트 러너.
/// Play 모드의 GameScene에서 지정한 장비/강화 상태로 스테이지 전투를 실제로 돌리고 결과를 Logs/probe_out.txt에 기록한다.
/// 입력: Logs/probe_in.json (Batch 형식)
/// </summary>
public class BalanceProbe : MonoBehaviour
{
    [Serializable]
    public class Scenario
    {
        public string name;
        public int stage;
        public string[] items;      // "Water:3", "Scroll:2" (종류:등급)
        public string[] enhance;    // "AttackUp=20", "AttackUp_Water=10"
        public float timeScale = 5f;
        public float maxSeconds = 240f;
        public bool timing;         // true면 무기 없이 몬스터 이동/공격 타이밍만 측정
        public float hpScale = 1f;  // 스폰된 몬스터 HP 배율
        public float atkScale = 1f; // 스폰된 몬스터 공격력 배율
        public float countScale = 1f; // 스폰 마릿수 배율(1 미만이면 균등하게 솎아냄)
        public float interval = 0f;   // >0이면 스폰 간격 덮어쓰기
        public int dummyHp;         // >0이면 모든 몬스터 HP를 이 값으로 덮어쓰고 공격력 1로 설정(샌드백 측정용)
        public float measureStart;  // 가한 대미지를 집계할 구간 시작(초)
        public float measureEnd;    // 구간 끝(초). 0이면 집계 안 함
    }

    [Serializable]
    public class Batch
    {
        public Scenario[] scenarios;
    }

    private const string InPath = "Logs/probe_in.json";
    private const string OutPath = "Logs/probe_out.txt";

    private Spawner spawner;
    private PlayerShooter shooter;
    private Barrier barrier;
    private Transform objectsRoot;
    private Transform itemContainer;
    private int spawned;
    private int destroyed;
    private int killed;
    private long goldSum;
    private long expSum;

    public static void Launch()
    {
        if (FindAnyObjectByType<BalanceProbe>() != null) return;
        new GameObject("BalanceProbe").AddComponent<BalanceProbe>();
    }

    private void Start()
    {
        StartCoroutine(RunAll());
    }

    private static void Out(string line)
    {
        File.AppendAllText(OutPath, line + "\n", Encoding.UTF8);
    }

    private IEnumerator RunAll()
    {
        File.WriteAllText(OutPath, "", Encoding.UTF8);
        GameDataRepository.Load(); // 엑셀 수치를 다시 읽어 Play 모드 재진입 없이 반복 측정
        Batch batch;
        try
        {
            batch = JsonUtility.FromJson<Batch>(File.ReadAllText(InPath, Encoding.UTF8));
        }
        catch (Exception e)
        {
            Out("ERROR input: " + e.Message);
            yield break;
        }

        yield return null;
        yield return null;

        spawner = FindAnyObjectByType<Spawner>(FindObjectsInactive.Include);
        shooter = FindAnyObjectByType<PlayerShooter>(FindObjectsInactive.Include);
        barrier = FindAnyObjectByType<Barrier>(FindObjectsInactive.Include);
        if (spawner == null || shooter == null || barrier == null)
        {
            Out("ERROR missing spawner/shooter/barrier");
            yield break;
        }

        objectsRoot = barrier.transform.parent;
        SetField(spawner, "debugMode", false);
        SetField(barrier, "maxHp", 1000000);

        var container = new GameObject("ProbeItems", typeof(RectTransform));
        itemContainer = container.transform;
        itemContainer.SetParent(FindAnyObjectByType<Canvas>().transform, false);

        spawner.MonsterSpawned += () => spawned++;
        Monster.AnyDestroyed += () => destroyed++;
        Monster.Killed += m =>
        {
            killed++;
            if (m != null)
            {
                goldSum += m.DropGold;
                expSum += m.DropExp;
            }
        };

        for (int i = 0; i < batch.scenarios.Length; i++)
        {
            yield return RunScenario(batch.scenarios[i]);
        }

        Time.timeScale = 1f;
        Out("DONE");
    }

    private IEnumerator RunScenario(Scenario s)
    {
        Time.timeScale = 1f;
        spawner.enabled = false;
        shooter.enabled = false;
        CleanupField();
        yield return null;

        objectsRoot.gameObject.SetActive(true);
        yield return null;

        SetField(barrier, "hp", 1000000);
        spawned = destroyed = killed = 0;
        goldSum = expSum = 0;

        var gm = GameManager.Instance;
        gm.EnhanceBonuses.Reset();
        if (s.enhance != null)
        {
            for (int i = 0; i < s.enhance.Length; i++)
            {
                string[] kv = s.enhance[i].Split('=');
                gm.EnhanceBonuses.ApplyEffect(kv[0], float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        var equipped = new List<Item>();
        if (s.items != null && !s.timing)
        {
            for (int i = 0; i < s.items.Length; i++)
            {
                string[] parts = s.items[i].Split(':');
                Item prefab = FindItemPrefab(parts[0]);
                if (prefab == null)
                {
                    Out($"ERROR [{s.name}] item prefab not found: {parts[0]}");
                    continue;
                }

                Item item = Instantiate(prefab, itemContainer);
                item.SetGrade(int.Parse(parts[1]));
                equipped.Add(item);
            }
        }

        yield return null;
        shooter.SetEquippedProjectiles(equipped);
        shooter.enabled = !s.timing;

        spawner.SetStageOrder(s.stage);
        spawner.enabled = true;
        ApplySpawnOverrides(s);
        Time.timeScale = s.timeScale;

        float startTime = Time.time;
        float startRealtime = Time.realtimeSinceStartup;
        int maxAlive = 0;
        float totalSpawnedHp = 0f;
        var seen = new HashSet<Monster>();
        var lastHp = new Dictionary<Monster, int>();
        double windowDamage = 0;
        double totalDamage = 0;
        bool timeout = false;
        float firstContactTime = -1f;
        var timingLog = new StringBuilder();
        float nextTimingLog = 0f;

        while (true)
        {
            yield return null;

            Monster[] monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
            float elapsed = Time.time - startTime;
            for (int i = 0; i < monsters.Length; i++)
            {
                Monster m = monsters[i];
                if (m == null) continue;

                if (seen.Add(m))
                {
                    if (s.dummyHp > 0)
                        m.ApplyStageOverride(s.dummyHp, 1, 0, 0);
                    else if (s.hpScale != 1f || s.atkScale != 1f)
                        m.ApplyStageOverride(
                            Mathf.Max(1, Mathf.RoundToInt(m.Hp * s.hpScale)),
                            Mathf.Max(1, Mathf.RoundToInt(m.AttackPower * s.atkScale)),
                            -1, -1);

                    totalSpawnedHp += m.Hp;
                    lastHp[m] = m.Hp;
                    continue;
                }

                int hpNow = m.Hp;
                int delta = lastHp[m] - hpNow;
                if (delta > 0)
                {
                    totalDamage += delta;
                    if (s.measureEnd > 0f && elapsed >= s.measureStart && elapsed <= s.measureEnd)
                        windowDamage += delta;
                }

                lastHp[m] = hpNow;
            }

            int alive = spawned - destroyed;
            if (alive > maxAlive) maxAlive = alive;

            int dmg = 1000000 - barrier.Hp;
            if (dmg > 0 && firstContactTime < 0f) firstContactTime = elapsed;

            if (s.timing && elapsed >= nextTimingLog)
            {
                nextTimingLog += 1f;
                timingLog.Append($"t={elapsed:F1} barrierDmg={dmg} alive={alive} ys=");
                for (int i = 0; i < monsters.Length && i < 3; i++)
                    if (monsters[i] != null)
                        timingLog.Append($"{monsters[i].name}@{monsters[i].transform.position.y:F2};");
                timingLog.AppendLine();
            }

            if (spawner.HasFinishedSpawning && alive <= 0) break;
            if (elapsed > s.maxSeconds) { timeout = true; break; }
            if (!s.timing && dmg >= 100000) break; // 사실상 폭주 방지
        }

        float duration = Time.time - startTime;
        float realSeconds = Time.realtimeSinceStartup - startRealtime;
        int barrierDamage = 1000000 - barrier.Hp;
        Out($"[{s.name}] stage={s.stage} duration={duration:F1}s real={realSeconds:F1}s spawned={spawned} killed={killed} " +
            $"leaked={spawned - killed} barrierDmg={barrierDamage} maxAlive={maxAlive} totalMonsterHp={totalSpawnedHp:F0} " +
            $"gold={goldSum} exp={expSum} firstContact={firstContactTime:F1}s timeout={timeout} " +
            $"dmgDealt={totalDamage:F0} windowDps={(s.measureEnd > s.measureStart ? windowDamage / (s.measureEnd - s.measureStart) : 0):F1}");
        if (s.timing)
            Out(timingLog.ToString());

        spawner.enabled = false;
        shooter.enabled = false;
        Time.timeScale = 1f;
        CleanupField();
        yield return null;
    }

    private void ApplySpawnOverrides(Scenario s)
    {
        if (s.interval > 0f)
            SetField(spawner, "activeSpawnInterval", s.interval);

        if (s.countScale >= 0.999f) return;

        FieldInfo queueField = typeof(Spawner).GetField("spawnQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        if (queueField == null) return;

        var queue = (Queue<StageSpawnEntry>)queueField.GetValue(spawner);
        var all = new List<StageSpawnEntry>(queue);
        queue.Clear();
        float acc = 0f;
        for (int i = 0; i < all.Count; i++)
        {
            acc += s.countScale;
            if (acc >= 1f)
            {
                acc -= 1f;
                queue.Enqueue(all[i]);
            }
        }

        SetField(spawner, "plannedSpawnCount", queue.Count);
    }

    private void CleanupField()
    {
        foreach (Monster m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            if (m != null) Destroy(m.gameObject);
        foreach (Projectile p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            if (p != null) Destroy(p.gameObject);
        if (itemContainer != null)
            for (int i = itemContainer.childCount - 1; i >= 0; i--)
                Destroy(itemContainer.GetChild(i).gameObject);
    }

    private static Item FindItemPrefab(string token)
    {
        string t = token.ToLowerInvariant();
        if (t == "rock") t = "rocks";
        if (t == "crystal") t = "crystalball";
        if (t == "quill") t = "quillpen";

        string[] guids = AssetDatabase.FindAssets("Item_ t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (name == t || name.EndsWith("_" + t))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.TryGetComponent(out Item item)) return item;
            }
        }

        return null;
    }

    private static void SetField(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(target, value);
    }
}
#endif
