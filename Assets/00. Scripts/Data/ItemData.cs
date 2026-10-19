[System.Serializable]
public class ItemData
{
    public string Id;
    public string Name;
    public string PrefabKey;
    public float Range;
    public float Damage;
    public float FireInterval;
    public float AddRange;
    public float AddDamage;
    public float AddFireInterval;
    public float ExtraFireInterval;
    public float ExtraDamage;
    public float Critical;
    public float ExtraAddRange;
    public bool HasCombatStats;

    // 상점 등장 가중치(같은 등급 안에서 아이템 종류를 고를 때). 비어 있으면 1.
    public float ShopWeight = 1f;
}
