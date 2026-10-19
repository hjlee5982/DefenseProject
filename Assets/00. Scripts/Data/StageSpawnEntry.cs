[System.Serializable]
public class StageSpawnEntry
{
    public const int Unspecified = -1;

    public string StageId;
    public string MonsterId;
    public int Count;

    // 스테이지별 몬스터 스탯 덮어쓰기. Unspecified(-1)면 Monster 시트 기본값을 사용한다.
    public int Hp = Unspecified;
    public int Atk = Unspecified;
    public int Gold = Unspecified;
    public int Exp = Unspecified;
}
