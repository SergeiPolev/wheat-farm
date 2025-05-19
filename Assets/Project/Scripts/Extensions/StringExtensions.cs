using System;
using System.Globalization;
using UnityEngine;

public static class StringExtensions
{
    public static string SetFontSize(this string text, float size) => $"<size={size}>{text}</size>";
    public static string AddColor(this string text, Color col) => $"<color={ColorHexFromUnityColor(col)}>{text}</color>";
    public static string AddColor(this char symbol, Color col) => $"<color={ColorHexFromUnityColor(col)}>{symbol}</color>";
    public static string AddColor(this string text, string colorHex) => $"<color={colorHex}>{text}</color>";
    public static string ColorHexFromUnityColor(this Color unityColor) => $"#{ColorUtility.ToHtmlStringRGBA(unityColor)}";
    public static string RemoveColor(this string text) => System.Text.RegularExpressions.Regex.Replace(text, "<color=[^>]+>", "").Replace("</color>", "");
    public static Color HexToColor(string hex)
    {
        hex = hex.Replace("0x", "");
        hex = hex.Replace("#", "");
        byte a = 255;
        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        if (hex.Length == 8)
        {
            a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
        }

        return new Color32(r, g, b, a);
    }

    public static int ComputeFNV1aHash(this string str)
    {
        uint hash = 2166136261;

        foreach (char c in str)
        {
            hash = (hash ^ c) * 16777619;
        }

        return unchecked((int)hash);
    }

    public static float[] ParseStringToFloatArray(string input, char separator)
    {
        string[] stringValues = input.Split(separator);
        float[] numbers = new float[stringValues.Length];

        for (int i = 0; i < stringValues.Length; i++)
        {
            if (float.TryParse(stringValues[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            {
                numbers[i] = number;
            }
            else
            {
                throw new FormatException($"Failed to convert '{stringValues[i]}' to float.");
            }
        }

        return numbers;
    }

}
