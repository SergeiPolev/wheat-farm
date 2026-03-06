using UnityEngine;
using VContainer.Unity;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Initializes the farm when FarmScope starts.
    /// Unlocks starter chunks around the origin.
    /// </summary>
    public class FarmBootstrap : IStartable
    {
        private readonly IChunkSystem _chunkSystem;
        private readonly FarmRenderConfig _config;

        public FarmBootstrap(IChunkSystem chunkSystem, FarmRenderConfig config)
        {
            _chunkSystem = chunkSystem;
            _config = config;
        }

        public void Start()
        {
            int r = _config.StarterChunkRadius;
            Debug.Log($"[FarmBootstrap] Unlocking {(2 * r + 1) * (2 * r + 1)} starter chunks (radius {r})");

            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    _chunkSystem.TryUnlockChunk(new Vector2Int(x, y));
                }
            }

            Debug.Log($"[FarmBootstrap] Farm initialized with {_chunkSystem.UnlockedChunkCount.CurrentValue} chunks");
        }
    }
}
