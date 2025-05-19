using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WindowStaticData", menuName = "ScriptableObjects/StaticData/Window")]
public class WindowStaticData : ScriptableObject
{
    public List<WindowBase> Configs;
}