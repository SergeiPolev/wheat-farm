using UnityEngine;

namespace WheatFarm.Farming
{
    /// <summary>Visual state of the ground tile under a cell.</summary>
    public enum GroundState
    {
        Grass = 0,
        Tilled = 1,
        Watered = 2,
        Fertilized = 3
    }

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

        /// <summary>Visual state of the ground tile (Grass, Tilled, Watered, Fertilized).</summary>
        public GroundState GroundState;

        /// <summary>Whether this cell is blocked (e.g. by a tree trunk or building).</summary>
        public bool Occupied;

        /// <summary>Randomized base scale (full-size) set at planting time.</summary>
        public float BaseScale;

        /// <summary>Random Y-axis rotation (degrees) set at planting time.</summary>
        public float RotationY;

        public bool HasPlant => !string.IsNullOrEmpty(PlantId);
        public bool IsHarvestable => HasPlant && Growth >= 1f;

        public static SubCellState Empty => new()
        {
            PlantId = null,
            Growth = 0f,
            Watered = false,
            FertilizerMultiplier = 1f,
            Color = Color.white,
            GroundState = GroundState.Grass,
            Occupied = false,
            BaseScale = 0f,
            RotationY = 0f
        };
    }
}
