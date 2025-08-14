using UnityEngine;

[CreateAssetMenu(fileName = "Crops Static Data", menuName = "Game/StaticData/Crops")]
public class CropsStaticData : ScriptableObject
{
    public CropsType[] UseCrops;
    public CropsData[] CropsData;
    public Material GroundMat;
    public Mesh GroundMesh;
}