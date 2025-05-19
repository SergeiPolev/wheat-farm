using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Random = UnityEngine.Random;

public static class ArrayExtensions
{
	public static T GetRandom<T>(this IEnumerable<T> array)
	{
		var newArray = array.ToArray();
		var randomIndex = Random.Range(0, newArray.Length);
		return newArray[randomIndex];
	}
	public static void Shuffle<T>(this IList<T> list)
	{
		RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
		int n = list.Count;
		while (n > 1)
		{
			byte[] box = new byte[1];
			do provider.GetBytes(box);
			while (!(box[0] < n * (Byte.MaxValue / n)));
			int k = box[0] % n;
			n--;
			(list[k], list[n]) = (list[n], list[k]);
		}
	}
}