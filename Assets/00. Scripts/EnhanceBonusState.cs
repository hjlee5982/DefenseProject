public class EnhanceBonusState
{
    public float AttackUpAll { get; private set; }
    public float AttackSpeedUpAll { get; private set; }

    public float AttackUpWater { get; private set; }
    public float AttackUpRock { get; private set; }
    public float AttackUpIce { get; private set; }
    public float AttackUpWind { get; private set; }
    public float AttackUpFire { get; private set; }
    public float AttackUpDark { get; private set; }

    public float SpeedUpWater { get; private set; }
    public float SpeedUpRock { get; private set; }
    public float SpeedUpIce { get; private set; }
    public float SpeedUpWind { get; private set; }
    public float SpeedUpFire { get; private set; }
    public float SpeedUpDark { get; private set; }

    public void Reset()
    {
        AttackUpAll = 0f;
        AttackSpeedUpAll = 0f;
        AttackUpWater = AttackUpRock = AttackUpIce = AttackUpWind = AttackUpFire = AttackUpDark = 0f;
        SpeedUpWater = SpeedUpRock = SpeedUpIce = SpeedUpWind = SpeedUpFire = SpeedUpDark = 0f;
    }

    public void ApplyEffect(string effectType, float value)
    {
        if (string.IsNullOrWhiteSpace(effectType) || value == 0f) return;

        if (effectType == "AttackUp")
        {
            AttackUpAll += value;
            return;
        }

        if (effectType == "AttackSpeedUp")
        {
            AttackSpeedUpAll += value;
            return;
        }

        // AttackUp_Water, AttackUp_Water_Rock, ...
        if (effectType.StartsWith("AttackUp_"))
        {
            ApplyToElements(effectType.Substring("AttackUp_".Length), value, isAttack: true);
            return;
        }

        // SpeedUp_Water (single) / AttackSpeed_Water_Rock (dual)
        if (effectType.StartsWith("SpeedUp_"))
        {
            ApplyToElements(effectType.Substring("SpeedUp_".Length), value, isAttack: false);
            return;
        }

        if (effectType.StartsWith("AttackSpeed_"))
        {
            ApplyToElements(effectType.Substring("AttackSpeed_".Length), value, isAttack: false);
        }
    }

    private void ApplyToElements(string elementsText, float value, bool isAttack)
    {
        if (string.IsNullOrWhiteSpace(elementsText)) return;

        string[] parts = elementsText.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i])) continue;

            if (isAttack)
                AddAttackElement(parts[i], value);
            else
                AddSpeedElement(parts[i], value);
        }
    }

    private void AddAttackElement(string element, float value)
    {
        switch (element)
        {
            case "Water":
                AttackUpWater += value;
                break;
            case "Rock":
                AttackUpRock += value;
                break;
            case "Ice":
                AttackUpIce += value;
                break;
            case "Wind":
                AttackUpWind += value;
                break;
            case "Fire":
                AttackUpFire += value;
                break;
            case "Dark":
                AttackUpDark += value;
                break;
        }
    }

    private void AddSpeedElement(string element, float value)
    {
        switch (element)
        {
            case "Water":
                SpeedUpWater += value;
                break;
            case "Rock":
                SpeedUpRock += value;
                break;
            case "Ice":
                SpeedUpIce += value;
                break;
            case "Wind":
                SpeedUpWind += value;
                break;
            case "Fire":
                SpeedUpFire += value;
                break;
            case "Dark":
                SpeedUpDark += value;
                break;
        }
    }

    public float GetElementAttackPercent(Item.ItemCategory category)
    {
        switch (category)
        {
            case Item.ItemCategory.Water: return AttackUpWater;
            case Item.ItemCategory.Rock: return AttackUpRock;
            case Item.ItemCategory.Ice: return AttackUpIce;
            case Item.ItemCategory.Wind: return AttackUpWind;
            case Item.ItemCategory.Fire: return AttackUpFire;
            case Item.ItemCategory.Grimoire: return AttackUpDark;
            default: return 0f;
        }
    }

    public float GetElementSpeedPercent(Item.ItemCategory category)
    {
        switch (category)
        {
            case Item.ItemCategory.Water: return SpeedUpWater;
            case Item.ItemCategory.Rock: return SpeedUpRock;
            case Item.ItemCategory.Ice: return SpeedUpIce;
            case Item.ItemCategory.Wind: return SpeedUpWind;
            case Item.ItemCategory.Fire: return SpeedUpFire;
            case Item.ItemCategory.Grimoire: return SpeedUpDark;
            default: return 0f;
        }
    }

    public float GetAttackPercent(Item.ItemCategory category)
    {
        return AttackUpAll + GetElementAttackPercent(category);
    }

    public float GetSpeedPercent(Item.ItemCategory category)
    {
        return AttackSpeedUpAll + GetElementSpeedPercent(category);
    }
}
