using UnityEditor;
using UnityEngine;

namespace Tools
{
    public class Tools
    {
        [MenuItem("Tools/Clear prefs")]
        public static void ClearPrefs()
        {
            /*JsonSaveSystem.RemoveDataByTools();*/
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}

