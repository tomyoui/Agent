public enum CombatAttribute
{
    Justice = 0,
    Doom = 1,
    Pleasure = 2,
    Harmony = 3,
    Life = 4,
    Greed = 5
}

public static class CombatAttributeUtility
{
    public static string GetKoreanName(CombatAttribute attribute)
    {
        return attribute switch
        {
            CombatAttribute.Justice => "정의",
            CombatAttribute.Doom => "파멸",
            CombatAttribute.Pleasure => "쾌락",
            CombatAttribute.Harmony => "조화",
            CombatAttribute.Life => "생명",
            CombatAttribute.Greed => "탐욕",
            _ => "알 수 없음"
        };
    }
}
