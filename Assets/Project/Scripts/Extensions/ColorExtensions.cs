using System;

public static class ColorExtensions
{
	public static (int, int, int) ConvertHexToRGB(string hexValue)
	{
		int hexColor = Convert.ToInt32(hexValue.Replace("#", ""), 16);
		int red = (hexColor >> 16) & 0xFF;
		int green = (hexColor >> 8) & 0xFF;
		int blue = hexColor & 0xFF;
		return (red, green, blue);
	}
}
