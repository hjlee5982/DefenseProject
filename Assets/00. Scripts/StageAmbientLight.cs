using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 스테이지 번호에 따라 Global Light 2D의 색/강도를 바꿔 시간대(아침 → 오후 → 밤)를 표현한다.
/// Global Light 2D 오브젝트에 붙이면 되고, GameManager가 스테이지가 바뀔 때 Apply를 호출한다.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class StageAmbientLight : MonoBehaviour
{
    [System.Serializable]
    public struct Phase
    {
        public string label;
        [Tooltip("이 스테이지 번호부터 적용된다(1부터 시작).")]
        public int fromStage;
        public Color color;
        [Min(0f)] public float intensity;
    }

    // 스테이지 번호가 큰 구간이 우선한다. 순서는 상관없다.
    [SerializeField]
    private Phase[] phases =
    {
        new Phase { label = "아침",  fromStage = 1,  color = Color.white,                        intensity = 1f },
        new Phase { label = "오후",  fromStage = 41, color = new Color(1f, 0.72f, 0.5f, 1f),     intensity = 0.95f },
        new Phase { label = "밤",    fromStage = 71, color = new Color(0.28f, 0.34f, 0.7f, 1f),  intensity = 0.6f },
    };

    private Light2D globalLight;

    private void Awake()
    {
        globalLight = GetComponent<Light2D>();
    }

    public void Apply(int stageNumber)
    {
        if (globalLight == null)
            globalLight = GetComponent<Light2D>();
        if (globalLight == null || phases == null || phases.Length == 0) return;

        int best = -1;
        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i].fromStage > stageNumber) continue;
            if (best < 0 || phases[i].fromStage > phases[best].fromStage)
                best = i;
        }

        // 어느 구간에도 해당하지 않으면(시작 스테이지가 모두 더 큼) 가장 이른 구간을 쓴다.
        if (best < 0)
        {
            best = 0;
            for (int i = 1; i < phases.Length; i++)
            {
                if (phases[i].fromStage < phases[best].fromStage)
                    best = i;
            }
        }

        globalLight.color = phases[best].color;
        globalLight.intensity = phases[best].intensity;
    }
}
