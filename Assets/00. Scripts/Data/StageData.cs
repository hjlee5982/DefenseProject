[System.Serializable]
public class StageData
{
    public string Id;
    public int Order;
    public float SpawnInterval;

    /// <summary>
    /// 이 스테이지 준비 단계에서 상점에 등장하는 등급(1~5) 가중치. null이면 InventoryManager 기본값을 사용한다.
    /// </summary>
    public float[] ShopGradeWeights;
}
