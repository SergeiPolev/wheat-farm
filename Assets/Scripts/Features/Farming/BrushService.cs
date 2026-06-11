using System;
using System.Collections.Generic;
using R3;

namespace WheatFarm.Farming
{
    public enum BrushSize
    {
        Small = 1,
        Medium = 2,
        Large = 3
    }

    /// <summary>
    /// Action applied to each cell within brush radius.
    /// CanApply is the per-tool cell filter — also used by the brush preview,
    /// so preview and apply always agree (see BrushPredicates).
    /// </summary>
    public interface IBrushAction
    {
        bool CanApply(ChunkData chunk, int cellX, int cellY);
        void Apply(ChunkData chunk, int cellX, int cellY);
    }

    public interface IBrushService
    {
        ReactiveProperty<BrushSize> CurrentSize { get; }
        float WorldRadius { get; }
        void ApplyAtWorldPos(UnityEngine.Vector3 worldPos, IBrushAction action);

        /// <summary>All brush cells in unlocked chunks (no per-tool filtering).</summary>
        IEnumerable<(ChunkData chunk, int cellX, int cellY)> GetAffectableCells(UnityEngine.Vector3 worldPos);
    }

    public class BrushService : IBrushService, IDisposable
    {
        private readonly IChunkSystem _chunkSystem;

        public ReactiveProperty<BrushSize> CurrentSize { get; } = new(BrushSize.Medium);

        public float WorldRadius => (int)CurrentSize.Value * _chunkSystem.CellWorldSize;

        public BrushService(IChunkSystem chunkSystem)
        {
            _chunkSystem = chunkSystem;
        }

        public void Dispose()
        {
            CurrentSize.Dispose();
        }

        public IEnumerable<(ChunkData chunk, int cellX, int cellY)> GetAffectableCells(UnityEngine.Vector3 worldPos)
        {
            var cells = _chunkSystem.GetCellsInRadius(worldPos, WorldRadius);
            foreach (var (chunkCoord, cellX, cellY) in cells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null || !chunk.Unlocked) continue;
                yield return (chunk, cellX, cellY);
            }
        }

        public void ApplyAtWorldPos(UnityEngine.Vector3 worldPos, IBrushAction action)
        {
            foreach (var (chunk, cellX, cellY) in GetAffectableCells(worldPos))
            {
                if (!action.CanApply(chunk, cellX, cellY)) continue;
                action.Apply(chunk, cellX, cellY);
                chunk.Dirty = true;
            }
        }
    }
}
