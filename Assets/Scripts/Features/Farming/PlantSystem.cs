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
        private const float ScaleMultiplier = 10f;

        /// <summary>Initial growth for newly planted crops (must be > 0 for shader visibility).</summary>
        private const float InitialGrowth = 0.1f;

        /// <summary>Visual scale at initial growth (fraction of full size). Grows from this to 1.0.</summary>
        private const float MinGrowthScale = 0.3f;

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

                    // Update GPU data: growth value + visual scale
                    ref var props = ref chunk.MeshProps[i];
                    props.cropState.y = cell.Growth;
                    RebuildMatrix(ref cell, ref props);

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

            // Gameplay state
            cell.PlantId = data.PlantId;
            cell.Growth = InitialGrowth;
            cell.Watered = false;
            cell.FertilizerMultiplier = 1f;

            // Placement data (for matrix reconstruction during growth)
            float baseScale = _chunkSystem.CellWorldSize * ScaleMultiplier;
            cell.BaseScale = baseScale * Random.Range(data.ScaleRange.x, data.ScaleRange.y);
            cell.RotationY = Random.Range(0f, 360f);

            // Sync to GPU: set position first, then RebuildMatrix uses it
            ref var props = ref chunk.MeshProps[idx];
            Vector3 worldPos = _chunkSystem.CellToWorld(chunkCoord, cellX, cellY);

            // Seed position into matrix so RebuildMatrix can extract it
            float initScale = cell.BaseScale * MinGrowthScale;
            props.m = Matrix4x4.TRS(worldPos, Quaternion.Euler(0, cell.RotationY, 0),
                new Vector3(initScale, initScale, initScale));
            props.gr = Matrix4x4.TRS(worldPos, Quaternion.identity, Vector3.one * cell.BaseScale);

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
                // Bushes/trees regrow from seedling stage (stay visible, shrink back)
                cell.Growth = InitialGrowth;
                cell.Watered = false;
                ref var props = ref chunk.MeshProps[idx];
                props.cropState.y = InitialGrowth;
                RebuildMatrix(ref cell, ref props);
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

        /// <summary>
        /// Reconstruct the TRS matrix from cell placement data + current growth.
        /// Growth drives scale: MinGrowthScale at 0 → 1.0 at full growth.
        /// Position comes from the existing matrix (column 3) to avoid recomputation.
        /// </summary>
        private void RebuildMatrix(ref SubCellState cell, ref MeshProperties props)
        {
            // Extract position from existing matrix (set at planting or from previous rebuild)
            var pos = new Vector3(props.m.m03, props.m.m13, props.m.m23);
            // If matrix was never set (zero), fall back to zero pos (shouldn't happen for planted cells)

            float growthFraction = Mathf.InverseLerp(0f, 1f, cell.Growth);
            float visualScale = cell.BaseScale * Mathf.Lerp(MinGrowthScale, 1f, growthFraction);
            var scale = new Vector3(visualScale, visualScale, visualScale);

            props.m = Matrix4x4.TRS(pos, Quaternion.Euler(0, cell.RotationY, 0), scale);
        }

        private static void ClearCell(ref SubCellState cell, ref MeshProperties props)
        {
            cell = SubCellState.Empty;
            props.m = Matrix4x4.zero;
            props.gr = Matrix4x4.zero;
            props.cropState = Vector4.zero;
            props.color = Vector4.zero;
        }
    }
}
