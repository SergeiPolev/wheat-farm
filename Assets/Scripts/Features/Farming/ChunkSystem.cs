using System.Collections.Generic;
using R3;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    public interface IChunkSystem
    {
        ReadOnlyReactiveProperty<int> UnlockedChunkCount { get; }
        float ChunkWorldSize { get; }
        int SubCellResolution { get; }
        float CellWorldSize { get; }

        ChunkData GetChunk(Vector2Int coord);
        ChunkData GetOrCreateChunk(Vector2Int coord);
        bool TryUnlockChunk(Vector2Int coord);
        IEnumerable<ChunkData> GetAllUnlockedChunks();
        IEnumerable<ChunkData> GetDirtyChunks();
        void ClearDirtyFlags();

        Vector2Int WorldToChunkCoord(Vector3 worldPos);
        (Vector2Int chunkCoord, int cellX, int cellY) WorldToCell(Vector3 worldPos);
        Vector3 CellToWorld(Vector2Int chunkCoord, int cellX, int cellY);

        List<(Vector2Int chunkCoord, int cellX, int cellY)> GetCellsInRadius(Vector3 worldPos, float radius);
    }

    public class ChunkSystem : IChunkSystem
    {
        private readonly Dictionary<Vector2Int, ChunkData> _chunks = new();
        private readonly ReactiveProperty<int> _unlockedCount = new(0);

        public ReadOnlyReactiveProperty<int> UnlockedChunkCount => _unlockedCount;
        public float ChunkWorldSize { get; }
        public int SubCellResolution { get; }
        public float CellWorldSize => ChunkWorldSize / SubCellResolution;

        public ChunkSystem(float chunkWorldSize = 4f, int subCellResolution = 8)
        {
            ChunkWorldSize = chunkWorldSize;
            SubCellResolution = subCellResolution;
        }

        public ChunkData GetChunk(Vector2Int coord)
        {
            return _chunks.GetValueOrDefault(coord);
        }

        public ChunkData GetOrCreateChunk(Vector2Int coord)
        {
            if (!_chunks.TryGetValue(coord, out var chunk))
            {
                chunk = new ChunkData(coord, SubCellResolution);
                _chunks[coord] = chunk;
                InitializeChunkMeshProps(chunk);
            }
            return chunk;
        }

        public bool TryUnlockChunk(Vector2Int coord)
        {
            var chunk = GetOrCreateChunk(coord);
            if (chunk.Unlocked) return false;

            chunk.Unlocked = true;
            chunk.Dirty = true;
            _unlockedCount.Value++;
            return true;
        }

        public IEnumerable<ChunkData> GetAllUnlockedChunks()
        {
            foreach (var chunk in _chunks.Values)
                if (chunk.Unlocked) yield return chunk;
        }

        public IEnumerable<ChunkData> GetDirtyChunks()
        {
            foreach (var chunk in _chunks.Values)
                if (chunk.Dirty) yield return chunk;
        }

        public void ClearDirtyFlags()
        {
            foreach (var chunk in _chunks.Values)
                chunk.Dirty = false;
        }

        public Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / ChunkWorldSize),
                Mathf.FloorToInt(worldPos.z / ChunkWorldSize));
        }

        public (Vector2Int chunkCoord, int cellX, int cellY) WorldToCell(Vector3 worldPos)
        {
            var chunkCoord = WorldToChunkCoord(worldPos);
            float localX = worldPos.x - chunkCoord.x * ChunkWorldSize;
            float localZ = worldPos.z - chunkCoord.y * ChunkWorldSize;
            int cellX = Mathf.Clamp(Mathf.FloorToInt(localX / CellWorldSize), 0, SubCellResolution - 1);
            int cellY = Mathf.Clamp(Mathf.FloorToInt(localZ / CellWorldSize), 0, SubCellResolution - 1);
            return (chunkCoord, cellX, cellY);
        }

        public Vector3 CellToWorld(Vector2Int chunkCoord, int cellX, int cellY)
        {
            float x = chunkCoord.x * ChunkWorldSize + (cellX + 0.5f) * CellWorldSize;
            float z = chunkCoord.y * ChunkWorldSize + (cellY + 0.5f) * CellWorldSize;
            return new Vector3(x, 0f, z);
        }

        public List<(Vector2Int chunkCoord, int cellX, int cellY)> GetCellsInRadius(Vector3 worldPos, float radius)
        {
            var result = new List<(Vector2Int, int, int)>();
            float sqrRadius = radius * radius;

            // Find chunk range that the radius covers
            var minCoord = WorldToChunkCoord(worldPos - new Vector3(radius, 0, radius));
            var maxCoord = WorldToChunkCoord(worldPos + new Vector3(radius, 0, radius));

            for (int cx = minCoord.x; cx <= maxCoord.x; cx++)
            {
                for (int cy = minCoord.y; cy <= maxCoord.y; cy++)
                {
                    var coord = new Vector2Int(cx, cy);
                    var chunk = GetChunk(coord);
                    if (chunk == null || !chunk.Unlocked) continue;

                    for (int x = 0; x < SubCellResolution; x++)
                    {
                        for (int y = 0; y < SubCellResolution; y++)
                        {
                            var cellWorld = CellToWorld(coord, x, y);
                            float dx = cellWorld.x - worldPos.x;
                            float dz = cellWorld.z - worldPos.z;
                            if (dx * dx + dz * dz <= sqrRadius)
                            {
                                result.Add((coord, x, y));
                            }
                        }
                    }
                }
            }

            return result;
        }

        private void InitializeChunkMeshProps(ChunkData chunk)
        {
            float cellSize = CellWorldSize;
            float chunkOriginX = chunk.ChunkCoord.x * ChunkWorldSize;
            float chunkOriginZ = chunk.ChunkCoord.y * ChunkWorldSize;

            for (int x = 0; x < chunk.Resolution; x++)
            {
                for (int y = 0; y < chunk.Resolution; y++)
                {
                    int idx = chunk.CellIndex(x, y);
                    var worldPos = new Vector3(
                        chunkOriginX + (x + 0.5f) * cellSize,
                        0f,
                        chunkOriginZ + (y + 0.5f) * cellSize);

                    // Small random offset for natural look
                    var randomOffset = new Vector3(
                        Random.Range(-cellSize * 0.2f, cellSize * 0.2f),
                        0f,
                        Random.Range(-cellSize * 0.2f, cellSize * 0.2f));

                    var props = new MeshProperties();
                    props.m = Matrix4x4.TRS(
                        worldPos + randomOffset,
                        Quaternion.Euler(0, Random.Range(0, 360), 0),
                        Vector3.one * Random.Range(0.8f, 1.2f));
                    props.gr = Matrix4x4.TRS(
                        worldPos + Vector3.up * 0.05f,
                        Quaternion.identity,
                        Vector3.one * 0.01f);
                    props.color = new Vector4(1, 1, 1, 1);
                    props.uv = new Vector4(
                        (float)x / chunk.Resolution,
                        (float)y / chunk.Resolution,
                        1f / chunk.Resolution,
                        1f / chunk.Resolution);
                    props.cropState = Vector4.zero;

                    chunk.MeshProps[idx] = props;
                }
            }
        }
    }
}
