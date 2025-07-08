using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Crops Static Data", menuName = "Game/StaticData/Crops")]
public class CropsStaticData : ScriptableObject
{
    public CropsType[] UseCrops;
    public CropsData[] CropsData;
    public Material GroundMat;
    public Mesh GroundMesh;
}

[Serializable]
public class CropsData
{
    public CropsType Crop;
    public Mesh CropMesh;
    public Material CropMat;
    public string ShowName;
    public string Description;
}