using UnityEngine;

/// <summary>
/// 라운드별 특수 준비 단계를 관리한다.
/// 가방 확장은 interval로 설정한다. 아이템 압축은 상점 Compress 버튼으로 시작한다.
/// </summary>
public class RoundEventScheduler : MonoBehaviour
{
    [Header("Bag Expansion")]
    [Tooltip("클리어한 라운드 수가 이 간격의 배수일 때 가방 확장 UI를 표시한다. 0이면 비활성.")]
    [SerializeField] private int bagExpansionInterval = 3;

    [Tooltip("가방 확장 이벤트마다 해제할 수 있는 슬롯 수.")]
    [SerializeField] private int bagExpansionUnlockCount = 2;

    public int BagExpansionUnlockCount => bagExpansionUnlockCount;

    public bool ShouldShowBagExpansion(int clearedRoundCount)
    {
        return IsIntervalHit(clearedRoundCount, bagExpansionInterval);
    }

    private static bool IsIntervalHit(int clearedRoundCount, int interval)
    {
        return interval > 0 && clearedRoundCount > 0 && clearedRoundCount % interval == 0;
    }
}
