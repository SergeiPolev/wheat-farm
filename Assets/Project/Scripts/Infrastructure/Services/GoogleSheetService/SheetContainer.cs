using Cathei.BakingSheet;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System;

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

public class ToolsSheet : Sheet<ToolsSheet.ToolsConfig>
{
	public class ToolsConfig : SheetRow<string>
	{
		public string next_id { get; private set; }
        public int spawn_shop { get; private set; }
        public string spawn_velocity { get; private set; }
        public string spawn_rotate { get; private set; }
        public float spawn_interval { get; private set; }
        public float first_spawn_accelerate { get; private set; }
        public float size { get; private set; }
        public float lifetime { get; private set; }
        public float damage_from { get; private set; }
        public float damage_to { get; private set; }
        public float damage_rate { get; private set; }
    }
}

public class MiscellaneousSheet : Sheet<MiscellaneousSheet.MiscellaneousConfig>
{
	public class MiscellaneousConfig : SheetRow
	{
		public string var_name { get; private set; }
		public string value { get; private set; }
	}

	public Dictionary<string, string> customDict { get; private set; }
	public Dictionary<string, float> floatDict { get; private set; }

	public override void VerifyAssets(SheetConvertingContext context)
	{
		base.VerifyAssets(context);
		customDict = new Dictionary<string, string>();
		floatDict = new Dictionary<string, float>();

		foreach (var config in this)
		{
			customDict.Add(config.var_name, config.value);
			if (float.TryParse(config.value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float value))
			{
				floatDict.Add(config.var_name, value);
			}
			else
			{
				var customParams = customDict[config.var_name].Split('\n');

				foreach (var line in customParams)
				{
					int separator = line.IndexOf(":", StringComparison.Ordinal);
					string s = line[(separator + 1)..].Replace("\r", "");

					if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
					{
						floatDict.Add(line[..separator], result);
					}
				}
			}
		}
	}
}