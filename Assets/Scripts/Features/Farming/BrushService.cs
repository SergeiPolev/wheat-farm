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
    /// </summary>
    public interface IBrushAction
    {
        void Apply(ChunkData chunk, int cellX, int cellY);
    }

    public interface IBrushService
    {
        ReactiveProperty<BrushSize> CurrentSize { get; }
        float WorldRadius { get; }
        void ApplyAtWorldPos(UnityEngine.Vector3 worldPos, IBrushAction action);
    }

    public class BrushService : IBrushService
    {
        private readonly IChunkSystem _chunkSystem;

        public ReactiveProperty<BrushSize> CurrentSize { get; } = new(BrushSize.Medium);

        public float WorldRadius => (int)CurrentSize.Value * _chunkSystem.CellWorldSize;

        public BrushService(IChunkSystem chunkSystem)
        {
            _chunkSystem = chunkSystem;
        }

        public void ApplyAtWorldPos(UnityEngine.Vector3 worldPos, IBrushAction action)
        {
            var cells = _chunkSystem.GetCellsInRadius(worldPos, WorldRadius);

            foreach (var (chunkCoord, cellX, cellY) in cells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null || !chunk.Unlocked) continue;

                int idx = chunk.CellIndex(cellX, cellY);
                if (chunk.Cells[idx].Occupied) continue;

                action.Apply(chunk, cellX, cellY);
                chunk.Dirty = true;
            }
        }
    }
}
