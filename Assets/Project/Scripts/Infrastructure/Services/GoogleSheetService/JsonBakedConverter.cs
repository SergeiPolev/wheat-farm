// BakingSheet, Maxwell Keonwoo Kang <code.athei@gmail.com>, 2022

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cathei.BakingSheet;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;
using ILogger = Microsoft.Extensions.Logging.ILogger;


public class JsonBakedConverter : ISheetConverter
{
    public virtual string Extension => "json";

    private string _loadPath;

    public JsonBakedConverter(string path)
    {
        _loadPath = path;
    }

    public static void ErrorHandler(ILogger logError, ErrorEventArgs err)
    {
        if (err.ErrorContext.Member?.ToString() == nameof(ISheetRow.Id) &&
            err.ErrorContext.OriginalObject is ISheetRow &&
            err.CurrentObject is not ISheet)
        {
            // if Id has error, the error must be handled on the sheet level
            return;
        }

        using (logError.BeginScope(err.ErrorContext.Path))
            logError.LogError(err.ErrorContext.Error, err.ErrorContext.Error.Message);

        err.ErrorContext.Handled = true;
    }

    public virtual JsonSerializerSettings GetSettings(ILogger logError)
    {
        var settings = new JsonSerializerSettings
        {
            Error = (_, err) => ErrorHandler(logError, err),
            ContractResolver = JsonSheetContractResolver.Instance
        };

        return settings;
    }

    protected virtual string Serialize(object obj, Type type, ILogger logger)
    {
        return JsonConvert.SerializeObject(obj, type, GetSettings(logger));
    }

    protected virtual object Deserialize(string json, Type type, ILogger logger)
    {
        return JsonConvert.DeserializeObject(json, type, GetSettings(logger));
    }

    // Uses only Resource folders. For some reason android is very bad at reading files with normal ways.
    public async Task<bool> Import(SheetConvertingContext context)
    {
        var sheetProps = context.Container.GetSheetProperties();

        foreach (var pair in sheetProps)
        {
            using (context.Logger.BeginScope(pair.Key))
            {
                //var path = Path.Combine(_loadPath, $"{pair.Key}.{Extension}");
                var path = Path.Combine(_loadPath, $"{pair.Key}");

                TextAsset theList = Resources.Load<TextAsset>(path);
                
                string result = theList.text;
                
                var sheet = Deserialize(result, pair.Value.PropertyType, context.Logger) as ISheet;
                pair.Value.SetValue(context.Container, sheet);
            }
        }

        return true;
    }

    public async Task<bool> Export(SheetConvertingContext context)
    {
        throw new Exception($"{GetType()} doesn't have implementation for Export");

        return true;
    }
}