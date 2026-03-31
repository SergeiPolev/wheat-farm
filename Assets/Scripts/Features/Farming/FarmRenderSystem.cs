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
        private CropMeshEntry[] _entries;
        private bool _loggedOnce;

        public FarmRenderSystem(IChunkSystem chunkSystem, FarmRenderConfig config)
        {
            _chunkSystem = chunkSystem;
            _config = config;
        }

        public void Tick()
        {
            // Lazy-init entries (config may not be ready at construction time)
            _entries ??= _config.GetEntries();
            if (_entries.Length == 0) return;

            // Create renderers for newly unlocked chunks
            foreach (var chunk in _chunkSystem.GetAllUnlockedChunks())
            {
                if (!_renderers.ContainsKey(chunk.ChunkCoord))
                {
                    var renderer = new ChunkCropRenderer(
                        chunk,
                        _entries,
                        _config.GroundMesh,
                        _config.GroundMaterial,
                        _chunkSystem.ChunkWorldSize);
                    _renderers[chunk.ChunkCoord] = renderer;
                }
            }

            if (!_loggedOnce)
            {
                bool hasGround = _config.GroundMesh != null && _config.GroundMaterial != null;
                Debug.Log($"[FarmRenderSystem] entries={_entries.Length}, ground={hasGround}, renderers={_renderers.Count}");
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
