using UnityEngine;

public static class RandomExtensions
{
    public static bool CheckChance(this float value)
    {
        return value >= Random.Range(0.01f, 100f);
    }
    
    public static bool CheckChanceWithDecimals(this float value)
    {
        var decimals = value % 1;

        return decimals * 100f >= Random.Range(0f, 100f);
    }

    public static float IncreaseOnCheckChanceDecimals(this float value)
    {
        return CheckChanceWithDecimals(value) ? value + 1 : value;
    }
}