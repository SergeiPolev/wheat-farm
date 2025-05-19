using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public static class EnumExtensions
{
	public static T RandomEnumValue<T> ()
	{
		var v = Enum.GetValues (typeof (T));
		return (T) v.GetValue (Random.Range(0, v.Length));
	}
	public static T RandomEnumValueExcept<T> (T except)
	{
		var v = Enum.GetValues (typeof (T));

		List<T> list = new List<T>();

		for (int i = 0; i < v.Length; i++)
		{
			list.Add((T)v.GetValue(i));
		}

		list.Remove(except);

		return list.GetRandom();
	}
}