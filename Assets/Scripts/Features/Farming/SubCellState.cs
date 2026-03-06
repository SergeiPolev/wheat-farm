using UnityEngine;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Gameplay state of a single sub-cell within a chunk.
    /// Separate from MeshProperties (GPU data) — this is the source of truth.
    /// </summary>
    [System.Serializable]
    public struct SubCellState
    {
        /// <summary>PlantData.PlantId or empty if no plant.</summary>
        public string PlantId;

        /// <summary>Growth progress 0..1. Plant is harvestable at 1.</summary>
        public float Growth;

        /// <summary>Whether the cell has been watered (enables growth).</summary>
        public bool Watered;

        /// <summary>Fertilizer speed multiplier (1 = normal).</summary>
        public float FertilizerMultiplier;

        /// <summary>Dye tint color applied to the plant.</summary>
        public Color Color;

        /// <summary>Whether this cell is blocked (e.g. by a tree trunk or building).</summary>
        public bool Occupied;

        public bool HasPlant => !string.IsNullOrEmpty(PlantId);
        public bool IsHarvestable => HasPlant && Growth >= 1f;

        public static SubCellState Empty => new()
        {
            PlantId = null,
            Growth = 0f,
            Watered = false,
            FertilizerMultiplier = 1f,
            Color = Color.white,
            Occupied = false
        };
    }
}
