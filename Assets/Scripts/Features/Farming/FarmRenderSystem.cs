using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Manages per-chunk renderers. Creates ChunkCropRenderers for unlocked chunks,
    /// syncs dirty buffers, and issues draw calls each frame.
    /// </summary>
    public class FarmRenderSystem : ITickable, IDisposable
    {
        private readonly IChunkSystem _chunkSystem;
        private readonly FarmRenderConfig _config;
        private readonly Dictionary<Vector2Int, ChunkCropRenderer> _renderers = new();
        private bool _loggedOnce;

        public FarmRenderSystem(IChunkSystem chunkSystem, FarmRenderConfig config)
        {
            _chunkSystem = chunkSystem;
            _config = config;
        }

        public void Tick()
        {
            if (_config.CropMesh == null || _config.CropMaterial == null)
                return;

            // Create renderers for newly unlocked chunks
            foreach (var chunk in _chunkSystem.GetAllUnlockedChunks())
            {
                if (!_renderers.ContainsKey(chunk.ChunkCoord))
                {
                    var renderer = new ChunkCropRenderer(
                        chunk,
                        _config.CropMesh,
                        _config.CropMaterial,
                        _chunkSystem.ChunkWorldSize);
                    _renderers[chunk.ChunkCoord] = renderer;
                }
            }

            if (!_loggedOnce)
            {
                Debug.Log($"[FarmRenderSystem] mesh={_config.CropMesh.name}, mat={_config.CropMaterial.name}, renderers={_renderers.Count}");
                _loggedOnce = true;
            }

            // Sync dirty buffers and draw
            foreach (var renderer in _renderers.Values)
            {
                renderer.SyncIfDirty();
                renderer.Draw();
            }
        }

        public void Dispose()
        {
            foreach (var renderer in _renderers.Values)
                renderer.Dispose();
            _renderers.Clear();
        }
    }
}
