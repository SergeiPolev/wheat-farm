using R3;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    public struct HarvestData
    {
        public string PlantId;
        public int Yield;
        public Vector3 WorldPosition;
    }

    public interface IPlantSystem
    {
        Subject<HarvestData> OnHarvested { get; }

        void Plant(Vector2Int chunkCoord, int cellX, int cellY, PlantData data);
        void Water(Vector2Int chunkCoord, int cellX, int cellY);
        void Fertilize(Vector2Int chunkCoord, int cellX, int cellY, float multiplier);
        HarvestData? Harvest(Vector2Int chunkCoord, int cellX, int cellY);
        void Uproot(Vector2Int chunkCoord, int cellX, int cellY);
        void Dye(Vector2Int chunkCoord, int cellX, int cellY, Color color);
    }

    public class PlantSystem : IPlantSystem, ITickable
    {
        private readonly IChunkSystem _chunkSystem;
        private readonly PlantDatabase _plantDb;

        public Subject<HarvestData> OnHarvested { get; } = new();

        public PlantSystem(IChunkSystem chunkSystem, PlantDatabase plantDb)
        {
            _chunkSystem = chunkSystem;
            _plantDb = plantDb;
        }

        public void Tick()
        {
            // Grow all watered plants across all unlocked chunks
            float dt = Time.deltaTime;

            foreach (var chunk in _chunkSystem.GetAllUnlockedChunks())
            {
                bool changed = false;
                for (int i = 0; i < chunk.CellCount; i++)
                {
                    ref var cell = ref chunk.Cells[i];
                    if (!cell.HasPlant || !cell.Watered || cell.Growth >= 1f)
                        continue;

                    var plantData = _plantDb.GetById(cell.PlantId);
                    if (plantData == null) continue;

                    float growthRate = cell.FertilizerMultiplier / plantData.GrowthDuration;
                    cell.Growth = Mathf.Min(1f, cell.Growth + growthRate * dt);

                    // Sync to GPU data
                    ref var props = ref chunk.MeshProps[i];
                    props.cropState.y = cell.Growth;

                    changed = true;
                }

                if (changed)
                    chunk.Dirty = true;
            }
        }

        public void Plant(Vector2Int chunkCoord, int cellX, int cellY, PlantData data)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null || !chunk.Unlocked) return;

            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];
            if (cell.Occupied || cell.HasPlant) return;

            cell.PlantId = data.PlantId;
            cell.Growth = 0f;
            cell.Watered = false;
            cell.FertilizerMultiplier = 1f;

            // Sync to GPU: set crop type (encoded as hash for now)
            ref var props = ref chunk.MeshProps[idx];
            props.cropState.x = GetPlantTypeIndex(data);
            props.cropState.y = 0f;
            props.color = new Vector4(cell.Color.r, cell.Color.g, cell.Color.b, cell.Color.a);

            chunk.Dirty = true;
        }

        public void Water(Vector2Int chunkCoord, int cellX, int cellY)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null) return;

            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];
            if (!cell.HasPlant) return;

            cell.Watered = true;
        }

        public void Fertilize(Vector2Int chunkCoord, int cellX, int cellY, float multiplier)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null) return;

            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];
            if (!cell.HasPlant) return;

            cell.FertilizerMultiplier = multiplier;
        }

        public HarvestData? Harvest(Vector2Int chunkCoord, int cellX, int cellY)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null) return null;

            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];
            if (!cell.IsHarvestable) return null;

            var plantData = _plantDb.GetById(cell.PlantId);
            if (plantData == null) return null;

            var harvestData = new HarvestData
            {
                PlantId = cell.PlantId,
                Yield = plantData.SellPrice,
                WorldPosition = _chunkSystem.CellToWorld(chunkCoord, cellX, cellY)
            };

            if (plantData.RenewableHarvest)
            {
                // Bushes/trees regrow
                cell.Growth = 0f;
                cell.Watered = false;
                ref var props = ref chunk.MeshProps[idx];
                props.cropState.y = 0f;
            }
            else
            {
                // Crops are consumed
                ClearCell(ref cell, ref chunk.MeshProps[idx]);
            }

            chunk.Dirty = true;
            OnHarvested.OnNext(harvestData);
            return harvestData;
        }

        public void Uproot(Vector2Int chunkCoord, int cellX, int cellY)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null) return;

            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];
            if (!cell.HasPlant) return;

            ClearCell(ref cell, ref chunk.MeshProps[idx]);
            chunk.Dirty = true;
        }

        public void Dye(Vector2Int chunkCoord, int cellX, int cellY, Color color)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null) return;

            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];
            if (!cell.HasPlant) return;

            cell.Color = color;
            ref var props = ref chunk.MeshProps[idx];
            props.color = new Vector4(color.r, color.g, color.b, color.a);
            chunk.Dirty = true;
        }

        private static void ClearCell(ref SubCellState cell, ref MeshProperties props)
        {
            cell = SubCellState.Empty;
            props.cropState = Vector4.zero;
            props.color = new Vector4(1, 1, 1, 1);
        }

        private int GetPlantTypeIndex(PlantData data)
        {
            // Map PlantId to a numeric index for the shader's cropState.x
            // For now, use a simple hash. Later: proper index from PlantDatabase.
            return Mathf.Abs(data.PlantId.GetHashCode()) % 100 + 1;
        }
    }
}
