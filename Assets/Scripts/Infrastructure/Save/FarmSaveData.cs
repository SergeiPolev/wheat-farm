using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Infrastructure.Save
{
    /// <summary>
    /// Root save data for a farm session. Serialized to JSON via JsonUtility.
    /// </summary>
    [System.Serializable]
    public class FarmSaveData
    {
        public int Version = 1;
        public int Coins;
        public float DayNightTime;
        public List<ChunkSaveData> Chunks = new();
        public List<PlacedBuildingSaveData> Buildings = new();
        public List<TreeSaveData> Trees = new();
        public List<InventoryItemSaveData> Inventory = new();
        public List<ContractSaveData> ActiveContracts = new();
        public List<string> UnlockedPlants = new();
    }

    /// <summary>Serializable chunk state.</summary>
    [System.Serializable]
    public struct ChunkSaveData
    {
        public int CoordX;
        public int CoordY;
        public bool Unlocked;
        public SubCellSaveData[] SubCells;
    }

    /// <summary>Serializable sub-cell state.</summary>
    [System.Serializable]
    public struct SubCellSaveData
    {
        /// <summary>PlantData.PlantId or empty if no plant.</summary>
        public string PlantId;
        /// <summary>Growth progress 0-1.</summary>
        public float Growth;
        /// <summary>Whether the cell has been watered.</summary>
        public bool Watered;
        /// <summary>Fertilizer speed multiplier (1 = normal).</summary>
        public float FertilizerMultiplier;
        /// <summary>Dye tint color (RGBA32).</summary>
        public Color32 Color;
        /// <summary>Blocked by building/tree trunk.</summary>
        public bool Occupied;
    }

    /// <summary>Serializable placed building.</summary>
    [System.Serializable]
    public struct PlacedBuildingSaveData
    {
        public string BuildingId;
        public int ChunkCoordX;
        public int ChunkCoordY;
        public int Level;
    }

    /// <summary>Serializable placed tree.</summary>
    [System.Serializable]
    public struct TreeSaveData
    {
        public string PlantId;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float Growth;
        public Color32 Color;
    }

    /// <summary>Serializable inventory item.</summary>
    [System.Serializable]
    public struct InventoryItemSaveData
    {
        public string ItemId;
        public int Type; // cast from ItemType enum
        public int Amount;
    }

    /// <summary>Serializable active contract.</summary>
    [System.Serializable]
    public struct ContractSaveData
    {
        public string ContractId;
        public int[] Progress;
    }
}
