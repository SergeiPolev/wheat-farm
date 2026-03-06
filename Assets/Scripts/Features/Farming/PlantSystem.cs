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
        /// <summary>
        /// Multiplier applied to CellWorldSize to get base crop scale.
        /// The pyramid.fbx "Plane" mesh is very small natively, needs significant scaling.
        /// </summary>
        private const float ScaleMultiplier = 6f;

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

        /// <summary>Initial growth for newly planted crops (must be > 0 for shader visibility).</summary>
        private const float InitialGrowth = 0.1f;

        public void Plant(Vector2Int chunkCoord, int cellX, int cellY, PlantData data)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null || !chunk.Unlocked) return;

            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];
            if (cell.Occupied || cell.HasPlant) return;

            cell.PlantId = data.PlantId;
            cell.Growth = InitialGrowth;
            cell.Watered = false;
            cell.FertilizerMultiplier = 1f;

            // Sync to GPU: position, crop type, color
            ref var props = ref chunk.MeshProps[idx];
            Vector3 worldPos = _chunkSystem.CellToWorld(chunkCoord, cellX, cellY);

            float baseScale = _chunkSystem.CellWorldSize * ScaleMultiplier;
            float randomScale = baseScale * Random.Range(data.ScaleRange.x, data.ScaleRange.y);
            float rotation = Random.Range(0f, 360f);
            var scale = new Vector3(randomScale, randomScale, randomScale);
            props.m = Matrix4x4.TRS(worldPos, Quaternion.Euler(0, rotation, 0), scale);
            props.gr = Matrix4x4.TRS(worldPos, Quaternion.identity, scale);

            // cropState.x = type id (must match material _Id); cropState.y = growth (>0 = visible)
            props.cropState.x = 1; // TODO: per-plant-type material _Id when multiple materials supported
            props.cropState.y = InitialGrowth;
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
                // Bushes/trees regrow from seedling stage (stay visible)
                cell.Growth = InitialGrowth;
                cell.Watered = false;
                ref var props = ref chunk.MeshProps[idx];
                props.cropState.y = InitialGrowth;
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
            props.m = Matrix4x4.zero;
            props.gr = Matrix4x4.zero;
            props.cropState = Vector4.zero;
            props.color = Vector4.zero;
        }

        private int GetPlantTypeIndex(PlantData data)
        {
            // Map PlantId to a numeric index for the shader's cropState.x
            // For now, use a simple hash. Later: proper index from PlantDatabase.
            return Mathf.Abs(data.PlantId.GetHashCode()) % 100 + 1;
        }
    }
}
