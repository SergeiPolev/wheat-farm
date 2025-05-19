using System.Globalization;
using UnityEngine;

public static class VectorExtensions
{
	public static int GetRandom(this Vector2Int range)
	{
		return Random.Range(range.x, range.y + 1);
	}
	public static float GetRandom(this Vector2 range)
	{
		return Random.Range(range.x, range.y);
	}

	public static Vector2 Rotate(this Vector2 vector, float angle)
    {
		float radians = angle * Mathf.Deg2Rad;
		float cos = Mathf.Cos(radians);
		float sin = Mathf.Sin(radians);
		float newX = vector.x * cos - vector.y * sin;
		float newY = vector.x * sin + vector.y * cos;
		return new Vector2(newX, newY);
	}



    public static Vector2 GetVector(this string str, char seporator = ';')
    {
        var elm = str.Split(seporator);
        var vec = new Vector2(float.Parse(elm[0], CultureInfo.InvariantCulture),
								float.Parse(elm[1], CultureInfo.InvariantCulture));
        return vec;
    }
}