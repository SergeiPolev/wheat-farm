using Cathei.BakingSheet;
using UnityEngine;

public class SheetContainer : SheetContainerBase
{
    public SheetContainer(Microsoft.Extensions.Logging.ILogger logger) : base(logger) { }
    public MiscellaneousSheet miscellaneous { get; private set; }
    public ToolsSheet tools { get; private set; }

	public bool GetMiscFloat(string key, out float value)
	{
		if (miscellaneous.floatDict.ContainsKey(key))
		{
			value = miscellaneous.floatDict[key];
			return true;
		}
		else
		{
			Debug.LogError("Sheet misc not find float key:" + key);
			value = 0f;
			return false;
		}
	}
}