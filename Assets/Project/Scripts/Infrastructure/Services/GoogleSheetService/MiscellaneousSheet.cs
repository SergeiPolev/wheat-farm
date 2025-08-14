using System;
using System.Collections.Generic;
using System.Globalization;
using Cathei.BakingSheet;

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