using UnityEngine;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Configuration for the farm render system.
    /// Assign crop mesh and material in the Inspector.
    /// Registered as an instance in FarmScope.
    /// </summary>
    [CreateAssetMenu(menuName = "WheatFarm/FarmRenderConfig")]
    public class FarmRenderConfig : ScriptableObject
    {
        [Header("Crop Rendering")]
        [Tooltip("Mesh used for all crop instances (GPU instanced indirect)")]
        public Mesh CropMesh;

        [Tooltip("Material with GetStructedBuffer.hlsl support")]
        public Material CropMaterial;

        [Header("Chunk Settings")]
        [Tooltip("World size of one chunk in units")]
        public float ChunkWorldSize = 4f;

        [Tooltip("Number of sub-cells per chunk axis (e.g. 8 = 8x8 = 64 cells per chunk)")]
        public int SubCellResolution = 8;

        [Header("Starter Farm")]
        [Tooltip("How many chunks to unlock at start (NxN around origin)")]
        public int StarterChunkRadius = 2;
    }
}
