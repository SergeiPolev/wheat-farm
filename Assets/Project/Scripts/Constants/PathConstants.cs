using System.IO;
using UnityEngine;

public static class PathConstants
{
	public static string JSONFilesBakedPath = Path.Combine(Application.dataPath, "Resources/Configs");
	public static string JSONResourcesPath = Path.Combine("Configs");
	public static string JSONFilesPath = Path.Combine(Application.persistentDataPath, "Config");
	public static string JSONMasterFilesPath = Path.Combine(Application.persistentDataPath, "Master");
}
