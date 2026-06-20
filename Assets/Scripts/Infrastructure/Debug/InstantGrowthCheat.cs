using VContainer.Unity;
using WheatFarm.Core;
using WheatFarm.Farming;

namespace WheatFarm.Infrastructure.Cheats
{
    /// <summary>
    /// Debug cheat: while InstantGrowth is on, instantly matures growing crops via the
    /// neutral IPlantSystem.ForceMature API. PlantSystem has NO debug dependency.
    /// </summary>
    public class InstantGrowthCheat : ITickable
    {
        private readonly IChunkSystem _chunks;
        private readonly IPlantSystem _plants;
        private readonly IDebugFlags _debug;

        public InstantGrowthCheat(IChunkSystem chunks, IPlantSystem plants, IDebugFlags debug)
        {
            _chunks = chunks;
            _plants = plants;
            _debug = debug;
        }

        public void Tick()
        {
            if (_debug == null || !_debug.InstantGrowth) return;

            int res = _chunks.SubCellResolution;
            foreach (var chunk in _chunks.GetAllUnlockedChunks())
            {
                for (int i = 0; i < chunk.CellCount; i++)
                {
                    var cell = chunk.Cells[i];
                    if (!cell.HasPlant || cell.Growth >= 1f) continue;
                    _plants.ForceMature(chunk.ChunkCoord, i % res, i / res);
                }
            }
        }
    }
}
