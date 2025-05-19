using UnityEngine;

[CreateAssetMenu(fileName = "StageStaticData", menuName = "ScriptableObjects/StaticData/Stages")]
public class StageStaticData : ScriptableObject
{
    public Stage FirstStage;
    public Stage[] Stages;
}