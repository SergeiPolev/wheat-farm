using System;
using UnityEngine;

namespace WheatFarm.Farming
{
    /// <summary>
    /// A single (meshId, mesh, material) entry for multi-pass crop rendering.
    /// Each entry becomes one DrawMeshInstancedIndirect call per chunk.
    /// </summary>
    [Serializable]
    public struct CropMeshEntry
    {
        [Tooltip("Must match PlantData.MeshId and the material's _Id property")]
        public int MeshId;
        public Mesh Mesh;
        public Material Material;
    }

    /// <summary>
    /// Configuration for the farm render system.
    /// Assign crop mesh entries and ground tile in the Inspector.
    /// Registered as an instance in FarmScope.
    /// </summary>
    [CreateAssetMenu(menuName = "WheatFarm/FarmRenderConfig")]
    public class FarmRenderConfig : ScriptableObject
    {
        [Header("Crop Rendering (multi-pass)")]
        [Tooltip("One entry per plant mesh type. Each becomes a separate draw call per chunk.")]
        public CropMeshEntry[] CropMeshEntries;

        [Header("Crop Rendering (legacy fallback)")]
        [Tooltip("Used if CropMeshEntries is empty — single mesh for all crops")]
        public Mesh CropMesh;
        public Material CropMaterial;

        [Header("Ground Tile Rendering")]
        [Tooltip("Flat quad mesh for ground tiles (Unity Quad or custom)")]
        public Mesh GroundMesh;

        [Tooltip("Material with Ground Instanced shader")]
        public Material GroundMaterial;

        [Header("Chunk Settings")]
        [Tooltip("World size of one chunk in units")]
        public float ChunkWorldSize = 4f;

        [Tooltip("Number of sub-cells per chunk axis (e.g. 8 = 8x8 = 64 cells per chunk)")]
        public int SubCellResolution = 8;

        [Header("Starter Farm")]
        [Tooltip("How many chunks to unlock at start (NxN around origin)")]
        public int StarterChunkRadius = 2;

        /// <summary>
        /// Returns CropMeshEntries if configured, otherwise builds a single-entry array
        /// from the legacy CropMesh/CropMaterial fields.
        /// </summary>
        public CropMeshEntry[] GetEntries()
        {
            if (CropMeshEntries != null && CropMeshEntries.Length > 0)
                return CropMeshEntries;

            if (CropMesh != null && CropMaterial != null)
            {
                return new[]
                {
                    new CropMeshEntry { MeshId = 1, Mesh = CropMesh, Material = CropMaterial }
                };
            }

            return Array.Empty<CropMeshEntry>();
        }
    }
}
