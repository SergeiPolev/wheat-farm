using System;
using System.Collections.Generic;
using UnityEngine;

public static class MathExtension
{
    private static List<char> _suffixes = new List<char>() { 'K', 'M', 'B', 'T', 'Q' /* etc */ };
    
    public static int RoundOff (this int i)
    {
        return ((int)Math.Round(i / 10.0)) * 10;
    }
    public static float RoundOff (this float i)
    {
        return (float)Math.Round(i / 10f) * 10f;
    }

    public static string ShortNumber(this double value)
    {
        double number = value;
        char? suffix = null;
 
        foreach (var t in _suffixes)
        {
            if (number >= 1000)
            {
                suffix = t;
                number /= 1000;
            }
            else
            {
                break;
            }
        }
 
        return string.Format($"{number:#.##}{suffix}");
    }
    public static float Lerp3(float a, float b, float c, float t)
    {
        return t <= 0.5f ? Mathf.Lerp(a, b, t * 2f) : Mathf.Lerp(b, c, t * 2f - 1f);
    }
}